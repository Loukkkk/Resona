using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Resona.Models;

namespace Resona;

/// <summary>
/// Volet de gauche : affichage des catégories (cochées dans les Paramètres) dans l'ordre choisi par l'utilisateur.
///
/// Principe anti-glitch et anti-saut définitif :
///  1. Les 9 NavigationViewItem restent TOUJOURS présents et visibles dans l'arbre visuel (Visibility = Visible).
///     Leur position physique dans la collection MenuItems ne change jamais (0, 40, 80, 120, etc.).
///     Cela élimine 100% des recalculs asynchrones d'ItemsRepeater et tout décalage temporel sur la frame 0 !
///  2. L'ordre et les positions visuelles sont pilotés par TranslateTransform.Y :
///     Pour chaque élément au slot visuel s (0..N-1), son décalage exact est toY = (s - physicalIndex) * itemHeight.
///  3. Réactivation d'une catégorie :
///     L'élément réactivé est positionné IMMÉDIATEMENT à son slot vertical exact (tt.Y = toY) avec tt.X = -20
///     et Opacity = 0. Les catégories en dessous s'écartent en glissant vers le bas depuis leur position actuelle
///     exacte, tandis que la nouvelle catégorie glisse depuis la gauche (-20 -> 0) avec un fondu (0 -> 1).
///     -> ZÉRO saut depuis le haut, ZÉRO flick d'une frame !
///  4. Désactivation d'une catégorie :
///     L'élément fait un fondu vers 0 et glisse vers la gauche pendant 200ms.
///     Pendant ce fondu, les autres catégories NE BOUGENT PAS.
///     Une fois le fondu terminé, les catégories du DESSOUS glissent vers le haut pour combler l'espace.
///     Les catégories du DESSUS ne bougent JAMAIS (éliminant le bug où elles descendaient bizarrement).
/// </summary>
public sealed partial class MainWindow
{
    private readonly HashSet<NavigationViewItem> _navHiddenItems = new();
    private readonly HashSet<NavigationViewItem> _navRemoving = new();
    private bool _navSyncHooked;

    // ---- Interpolation image par image (CompositionTarget.Rendering) ----------------------------------------------------

    private sealed class NavTween
    {
        public object Owner = null!;
        public string Prop = "";
        public double From;
        public double To;
        public double DurationMs;
        public bool EaseOut;
        public long StartTicks;
        public Action<double> Apply = null!;
        public Action? Done;
    }

    private readonly List<NavTween> _navTweens = new();
    private bool _navTweenHooked;
    private readonly Stopwatch _navClock = Stopwatch.StartNew();

    private void StartNavTween(object owner, string prop, double from, double to, double ms, bool easeOut, Action<double> apply, Action? done = null)
    {
        CancelNavTween(owner, prop);
        apply(from);
        var tw = new NavTween
        {
            Owner = owner,
            Prop = prop,
            From = from,
            To = to,
            DurationMs = ms,
            EaseOut = easeOut,
            StartTicks = _navClock.ElapsedTicks,
            Apply = apply,
            Done = done
        };
        _navTweens.Add(tw);
        if (!_navTweenHooked)
        {
            _navTweenHooked = true;
            CompositionTarget.Rendering += NavTweenRendering;
        }
    }

    private void CancelNavTween(object owner, string prop)
    {
        _navTweens.RemoveAll(t => ReferenceEquals(t.Owner, owner) && t.Prop == prop);
    }

    private void CancelNavTweens(string prop)
    {
        _navTweens.RemoveAll(t => t.Prop == prop);
    }

    private void NavTweenRendering(object? sender, object e)
    {
        long now = _navClock.ElapsedTicks;
        for (int i = _navTweens.Count - 1; i >= 0; i--)
        {
            if (i >= _navTweens.Count) continue;
            var tw = _navTweens[i];
            double elapsedMs = (now - tw.StartTicks) * 1000.0 / Stopwatch.Frequency;
            double t = tw.DurationMs <= 0 ? 1.0 : Math.Min(1.0, elapsedMs / tw.DurationMs);
            double eased = tw.EaseOut ? 1.0 - Math.Pow(1.0 - t, 3) : t;
            tw.Apply(tw.From + (tw.To - tw.From) * eased);
            if (t >= 1.0)
            {
                _navTweens.RemoveAt(i);
                tw.Done?.Invoke();
            }
        }
        if (_navTweens.Count == 0 && _navTweenHooked)
        {
            CompositionTarget.Rendering -= NavTweenRendering;
            _navTweenHooked = false;
        }
    }

    // ---- Gestion des catégories du volet --------------------------------------------------------------------------------

    private NavigationViewItem NavItemForKey(string key) => key switch
    {
        "library" => NavLibrary,
        "albums" => NavAlbums,
        "playlists" => NavPlaylists,
        "queue" => NavQueue,
        "artists" => NavArtists,
        "genres" => NavGenres,
        "folders" => NavFolders,
        "statistics" => NavStatistics,
        _ => NavDownload,
    };

    private static int PhysicalIndex(NavigationViewItem item) => (item.Tag as string) switch
    {
        "library" => 0,
        "albums" => 1,
        "playlists" => 2,
        "queue" => 3,
        "artists" => 4,
        "genres" => 5,
        "folders" => 6,
        "statistics" => 7,
        "download" => 8,
        _ => 0
    };

    private double GetItemHeight()
    {
        if (NavLibrary.ActualHeight > 0) return NavLibrary.ActualHeight;
        foreach (object o in RootNav.MenuItems)
        {
            if (o is NavigationViewItem { ActualHeight: > 0 } nvi && !ReferenceEquals(nvi, DummyEndItem))
                return nvi.ActualHeight;
        }
        return 40.0;
    }

    public void RefreshNavCategories() => ApplyNavCategories(RootNav.IsLoaded);

    private void ApplyNavCategories(bool animate)
    {
        AppSettings cur = App.Settings.Current;
        var desired = new List<NavigationViewItem>();
        var hidden = new List<NavigationViewItem>();

        foreach (string key in NavCategories.ResolveOrder(cur))
        {
            NavigationViewItem item = NavItemForKey(key);
            if (NavCategories.IsVisible(cur, key))
                desired.Add(item);
            else
                hidden.Add(item);
        }

        bool live = RootNav.IsLoaded;
        if (live) DisableNavTransitions();

        double itemHeight = GetItemHeight();
        if (live && itemHeight <= 0)
        {
            ScheduleNavSync();
            return;
        }

        // 1. Un élément redevient visible alors qu'il était en train de disparaître : annuler la disparition
        foreach (var it in desired)
        {
            if (_navRemoving.Remove(it))
            {
                CancelNavTween(it, "Opacity");
                CancelNavTween(it, "X");
                it.IsHitTestVisible = true;
                it.IsTabStop = true;
                var tt = EnsureNavTransform(it);
                StartNavTween(it, "Opacity", it.Opacity, 1.0, 200, false, v => it.Opacity = v);
                StartNavTween(it, "X", tt.X, 0.0, 200, true, v => tt.X = v);
            }
        }

        // 2. Éléments à masquer : lancer leur disparition (fondu + glissement gauche)
        // Tant qu'ils disparaissent (_navRemoving), ils restent dans activeItems pour que les
        // autres catégories ne bougent pas avant la fin du fondu.
        foreach (var item in hidden)
        {
            if (_navHiddenItems.Contains(item) && !_navRemoving.Contains(item))
            {
                continue;
            }

            if (live && animate)
            {
                _navHiddenItems.Add(item);
                if (_navRemoving.Add(item))
                {
                    StartNavRemoval(item);
                }
            }
            else
            {
                _navRemoving.Remove(item);
                _navHiddenItems.Add(item);
                CancelNavTween(item, "Opacity");
                CancelNavTween(item, "X");
                CancelNavTween(item, "Y");
                item.Opacity = 0.0;
                item.IsHitTestVisible = false;
                item.IsTabStop = false;
                item.Visibility = Visibility.Visible;
                var tt = EnsureNavTransform(item);
                tt.X = -500.0;
            }
        }

        // 3. Liste des éléments actuellement actifs (affichés ou en cours de disparition)
        var activeItems = new List<NavigationViewItem>();
        foreach (string key in NavCategories.ResolveOrder(cur))
        {
            var item = NavItemForKey(key);
            if (desired.Contains(item) || _navRemoving.Contains(item))
            {
                activeItems.Add(item);
            }
        }

        // 4. Identifier les éléments nouvellement activés
        var newlyShown = new List<NavigationViewItem>();
        foreach (var it in desired)
        {
            if (_navHiddenItems.Contains(it) && !_navRemoving.Contains(it))
            {
                newlyShown.Add(it);
                _navHiddenItems.Remove(it);
            }
        }

        // Pour les éléments nouvellement activés :
        // Poser immédiatement leur tt.Y à leur position cible, avec tt.X = -20 et Opacity = 0.
        // Zéro saut depuis le haut, zéro flick !
        foreach (var it in newlyShown)
        {
            int slot = activeItems.IndexOf(it);
            int phys = PhysicalIndex(it);
            double targetY = (slot - phys) * itemHeight;

            var tt = EnsureNavTransform(it);
            CancelNavTween(it, "Y");
            CancelNavTween(it, "X");
            CancelNavTween(it, "Opacity");

            tt.Y = targetY;
            tt.X = -20.0;
            it.Opacity = 0.0;
            it.IsHitTestVisible = true;
            it.IsTabStop = true;
            it.Visibility = Visibility.Visible;
            StripItemTransitions(it);
        }

        // 5. Appliquer les positions cibles aux éléments actifs
        for (int slot = 0; slot < activeItems.Count; slot++)
        {
            var it = activeItems[slot];
            int phys = PhysicalIndex(it);
            double targetY = (slot - phys) * itemHeight;
            var tt = EnsureNavTransform(it);

            if (newlyShown.Contains(it))
            {
                tt.Y = targetY;
            }
            else if (_navRemoving.Contains(it))
            {
                // En cours de disparition : reste à sa place
            }
            else
            {
                if (animate && live)
                {
                    AnimateNavY(it, targetY);
                }
                else
                {
                    CancelNavTween(it, "Y");
                    tt.Y = targetY;
                }
            }
        }

        // 6. Ranger les éléments masqués proprement sous la liste, invisibles et hors-champ
        int parkSlot = activeItems.Count;
        foreach (var it in hidden)
        {
            if (_navRemoving.Contains(it)) continue;
            _navHiddenItems.Add(it);

            int phys = PhysicalIndex(it);
            double parkY = (parkSlot - phys) * itemHeight;
            parkSlot++;

            var tt = EnsureNavTransform(it);
            CancelNavTween(it, "Y");
            CancelNavTween(it, "X");
            CancelNavTween(it, "Opacity");

            tt.Y = parkY;
            tt.X = -500.0;
            it.Opacity = 0.0;
            it.IsHitTestVisible = false;
            it.IsTabStop = false;
            it.Visibility = Visibility.Visible;
        }

        // 7. Lancer l'animation d'apparition fluide pour les éléments nouvellement activés
        foreach (var it in newlyShown)
        {
            if (animate && live)
            {
                PlayNavEntrance(it);
            }
            else
            {
                var tt = EnsureNavTransform(it);
                tt.X = 0;
                it.Opacity = 1.0;
            }
        }
    }

    private void ScheduleNavSync()
    {
        if (_navSyncHooked) return;
        _navSyncHooked = true;
        void Handler(object? s, object e)
        {
            double h = GetItemHeight();
            if (h <= 0 || !RootNav.IsLoaded) return;
            RootNav.LayoutUpdated -= Handler;
            _navSyncHooked = false;
            ApplyNavCategories(false);
        }
        RootNav.LayoutUpdated += Handler;
    }

    private static TranslateTransform EnsureNavTransform(NavigationViewItem it)
    {
        if (it.RenderTransform is TranslateTransform tt) return tt;
        tt = new TranslateTransform();
        it.RenderTransform = tt;
        return tt;
    }

    private void AnimateNavY(NavigationViewItem it, double to)
    {
        var tt = EnsureNavTransform(it);
        if (Math.Abs(tt.Y - to) < 0.5)
        {
            CancelNavTween(it, "Y");
            tt.Y = to;
            return;
        }
        StartNavTween(it, "Y", tt.Y, to, 160, true, v => tt.Y = v);
    }

    private void PlayNavEntrance(NavigationViewItem item)
    {
        var tt = EnsureNavTransform(item);
        CancelNavTween(item, "X");
        CancelNavTween(item, "Opacity");
        tt.X = -20.0;
        item.Opacity = 0.0;
        item.IsHitTestVisible = true;
        item.IsTabStop = true;
        StartNavTween(item, "Opacity", 0.0, 1.0, 260, false, v => item.Opacity = v);
        StartNavTween(item, "X", -20.0, 0.0, 260, true, v => tt.X = v);
    }

    private void StartNavRemoval(NavigationViewItem item)
    {
        var tt = EnsureNavTransform(item);
        CancelNavTween(item, "X");
        CancelNavTween(item, "Opacity");
        item.IsHitTestVisible = false;
        item.IsTabStop = false;
        StartNavTween(item, "X", tt.X, -20.0, 200, false, v => tt.X = v);
        StartNavTween(item, "Opacity", item.Opacity, 0.0, 200, false, v => item.Opacity = v, () =>
        {
            if (!_navRemoving.Remove(item)) return;            // disparition annulée entre-temps
            AppSettings cur = App.Settings.Current;
            string key = item.Tag as string ?? "";
            if (NavCategories.IsVisible(cur, key)) return;    // redevenu visible entre-temps
            CancelNavTween(item, "X");
            tt.X = -500.0;
            item.Opacity = 0.0;
            _navHiddenItems.Add(item);
            ApplyNavCategories(true);
        });
    }

    private bool _transitionsDisabled;

    private void DisableNavTransitions()
    {
        if (_transitionsDisabled) return;
        _transitionsDisabled = true;
        try
        {
            foreach (var obj in WalkVisualTree(RootNav))
            {
                if (obj is UIElement fe)
                {
                    fe.Transitions = new TransitionCollection();
                    if (fe is Panel p) p.ChildrenTransitions = new TransitionCollection();
                    if (fe is ContentPresenter cp) cp.ContentTransitions = new TransitionCollection();
                    if (fe is ContentControl cc) cc.ContentTransitions = new TransitionCollection();
                }
            }
        }
        catch { }
    }

    private static void StripItemTransitions(NavigationViewItem item)
    {
        try
        {
            item.Transitions = new TransitionCollection();
            item.ContentTransitions = new TransitionCollection();
        }
        catch { }
    }
}
