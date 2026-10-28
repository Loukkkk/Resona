using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NAudio.Wave;
using Resona.Models;
using Windows.Foundation;

namespace Resona.Helpers;

/// <summary>
/// Garde-fou pour l'icone lecture / pause qui s'affiche sur la cover d'un son (bordure "PlayOverlay") au survol dans les listes de sons.
///
/// Cette icone n'etait pilotee que par des evenements souris (entree / sortie / clic). Des qu'un evenement "sortie" est manque
/// (conteneur de la liste recycle ou reaffecte a un autre son, menu clic droit ouvert, changement de son pendant le survol...),
/// l'icone restait affichee indefiniment, ou avec le mauvais glyphe, jusqu'a ce qu'on re-survole puis quitte la cover.
///
/// Ici on ne se fie plus aux evenements : on suit la position REELLE de la souris dans la liste et, a intervalle regulier, on
/// - masque toute icone affichee sur une cover qui n'est plus sous la souris ;
/// - remet le bon glyphe (lecture / pause) sur celle qui l'est, selon l'etat reel du son.
/// Ne sert qu'a corriger : l'affichage au survol reste gere par les pages comme avant.
/// </summary>
public sealed class CoverOverlayGuard
{
    private sealed class Entry
    {
        public Border Overlay = null!;
        public Grid? Cover;
        public FontIcon? Icon;
    }

    private readonly ListViewBase _list;
    private readonly bool _resetScale;
    private readonly DispatcherTimer _timer;
    private readonly ConditionalWeakTable<DependencyObject, Entry> _cache = new();
    private Point _pointer;
    private bool _inList;

    /// <param name="list">La liste de sons (conteneurs contenant une bordure nommee "PlayOverlay").</param>
    /// <param name="resetScale">true si la cover grossit au survol (ApplyBouncyScale) : on la remet alors a sa taille normale quand on masque l'icone.</param>
    public CoverOverlayGuard(ListViewBase list, bool resetScale)
    {
        _list = list;
        _resetScale = resetScale;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (s, e) => Sweep();

        // handledEventsToo = true : on veut la position de la souris meme quand un enfant (cover, bouton...) traite l'evenement.
        list.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointer), true);
        list.AddHandler(UIElement.PointerEnteredEvent, new PointerEventHandler(OnPointer), true);
        list.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(OnPointer), true);
        list.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointer), true);
        list.Loaded += (s, e) => _timer.Start();
        list.Unloaded += (s, e) => { _timer.Stop(); _inList = false; };
        if (list.IsLoaded) _timer.Start();
    }

    // PointerEntered / PointerExited remontent depuis les enfants : on ne les interprete pas, on regarde seulement OU est la souris.
    private void OnPointer(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            Point p = e.GetCurrentPoint(_list).Position;
            _pointer = p;
            _inList = p.X >= 0 && p.Y >= 0 && p.X <= _list.ActualWidth && p.Y <= _list.ActualHeight;
        }
        catch { }
    }

    /// <summary>Vrai si la souris est reellement au-dessus de cette cover (avec 4 px de tolerance pour l'agrandissement au survol).</summary>
    public bool IsPointerOver(FrameworkElement cover)
    {
        if (!_inList || !cover.IsLoaded) return false;
        try
        {
            Rect r = cover.TransformToVisual(_list).TransformBounds(new Rect(0, 0, cover.ActualWidth, cover.ActualHeight));
            r = new Rect(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8);
            return r.Contains(_pointer);
        }
        catch { return false; }   // conteneur sorti de l'arbre visuel : forcement pas sous la souris
    }

    private void Sweep()
    {
        try
        {
            if (_list.ItemsPanelRoot is not Panel panel) return;
            foreach (UIElement child in panel.Children)
            {
                Entry? entry = Resolve(child);
                if (entry == null || entry.Overlay.Visibility != Visibility.Visible) continue;

                if (entry.Cover == null || !IsPointerOver(entry.Cover))
                {
                    entry.Overlay.Visibility = Visibility.Collapsed;
                    if (_resetScale && entry.Cover != null) AnimationHelper.ApplyBouncyScale(entry.Cover, 1.0f);
                    continue;
                }

                if (entry.Icon != null)
                {
                    Track? track = entry.Cover.DataContext as Track;
                    bool playing = track != null && track.IsPlaying && App.AudioEngine.State != PlaybackState.Paused;
                    string glyph = playing ? "" : "";
                    if (entry.Icon.Glyph != glyph) entry.Icon.Glyph = glyph;
                }
            }
        }
        catch { }
    }

    // L'arbre visuel d'un conteneur est stable (il est recycle, pas recree) : on retrouve l'icone une fois et on la garde en cache.
    private Entry? Resolve(UIElement container)
    {
        if (_cache.TryGetValue(container, out Entry? cached)) return cached;
        DependencyObject root = (container as ContentControl)?.ContentTemplateRoot ?? container;
        Border? overlay = FindByName<Border>(root, "PlayOverlay");
        if (overlay == null) return null;   // contenu pas encore realise : on reessaiera au prochain passage
        var entry = new Entry { Overlay = overlay, Cover = overlay.Parent as Grid, Icon = overlay.Child as FontIcon };
        _cache.Add(container, entry);
        return entry;
    }

    private static T? FindByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == name) return t;
            T? deeper = FindByName<T>(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }
}
