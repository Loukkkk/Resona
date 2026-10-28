using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Resona.Models;
using Resona.Services;

namespace Resona.Views;

/// <summary>Poignee de la liste des categories (curseur "deplacer" au survol).</summary>
internal sealed class CategoryGrip : Grid
{
    public string Key { get; }

    private UIElement? _child;
    public UIElement? Child
    {
        get => _child;
        set
        {
            if (_child != null) Children.Remove(_child);
            _child = value;
            if (value != null) Children.Add(value);
        }
    }

    public CategoryGrip(string key)
    {
        Key = key;
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
    }
}

/// <summary>
/// Parametres > Categories affichees dans le volet : chaque ligne a une poignee a gauche de sa case a cocher.
/// On maintient la poignee avec la souris et on glisse : les autres lignes s'ecartent avec une animation et
/// le volet de gauche est reordonne EN DIRECT (voir MainWindow.NavOrder.cs). L'ordre est enregistre dans AppSettings.NavCategoryOrder.
/// </summary>
public sealed partial class SettingsPage
{
    private bool _catInit;
    private StackPanel? _catPanel;
    private TextBlock? _catHint;
    private readonly Dictionary<string, Grid> _catRows = new();
    private readonly Dictionary<string, TranslateTransform> _catTransforms = new();
    private readonly List<Storyboard> _catStoryboards = new();
    private List<string> _catOrder = new();
    private List<string> _catDragStart = new();
    private List<string> _catDragLive = new();
    private string? _catDragKey;
    private double _catDragStartY;
    private int _catDragStartSlot;
    private int _catDragSlot;
    private int _catToken;

    private bool _catLoadedHooked;
    private CheckBox? _catQueueBox;

    private void InitCategoryReorder()
    {
        if (!_catLoadedHooked)
        {
            _catLoadedHooked = true;
            // Page preChargee masquee : son contenu n'est dans l'arbre visuel qu'une fois reellement affiche.
            // On retente donc a chaque etape (chargement, premiere mise en page) jusqu'a ce que ca marche.
            void TryLater()
            {
                if (_catInit) return;
                InitCategoryReorder();
                SyncCategoryOrderFromSettings();
            }
            Loaded += (s, e) => TryLater();
            SettingsRoot.Loaded += (s, e) => TryLater();
            SettingsRoot.SizeChanged += (s, e) => TryLater();
        }
        try
        {
            InitCategoryReorderCore();
        }
        catch (Exception ex)
        {
            LogService.Write("Settings", ex, "Ordre des categories");
        }
    }

    private void InitCategoryReorderCore()
    {
        if (_catInit) return;
        var checks = new (string Key, CheckBox Box)[]
        {
            ("library", ShowLibraryCheck), ("albums", ShowAlbumsCheck), ("playlists", ShowPlaylistsCheck),
            ("artists", ShowArtistsCheck), ("genres", ShowGenresCheck), ("folders", ShowFoldersCheck),
            ("statistics", ShowStatisticsCheck), ("download", ShowDownloadCheck),
        };
        var panel = ShowLibraryCheck.Parent as StackPanel ?? VisualTreeHelper.GetParent(ShowLibraryCheck) as StackPanel;
        if (panel == null)
        {
            // La case referencee par le champ n'est pas dans l'arbre : on retrouve les cases reellement affichees par leur nom.
            var all = FindAllOfType<CheckBox>(this).ToList();
            var shown = all.FirstOrDefault(c => c.Name == "ShowLibraryCheck");
            if (shown == null) return;   // pas encore dans l'arbre : on reessaiera au chargement de la page
            panel = VisualTreeHelper.GetParent(shown) as StackPanel;
            if (panel == null) return;
            var byName = new Dictionary<string, string>
            {
                ["library"] = "ShowLibraryCheck", ["albums"] = "ShowAlbumsCheck", ["playlists"] = "ShowPlaylistsCheck",
                ["artists"] = "ShowArtistsCheck", ["genres"] = "ShowGenresCheck", ["folders"] = "ShowFoldersCheck",
                ["statistics"] = "ShowStatisticsCheck", ["download"] = "ShowDownloadCheck",
            };
            checks = checks.Select(c => (Key: c.Key, Box: all.FirstOrDefault(x => x.Name == byName[c.Key]) ?? c.Box)).ToArray();
        }
        _catInit = true;
        _catPanel = panel;
        var card = panel.Parent as Panel ?? VisualTreeHelper.GetParent(panel) as Panel;

        // File d'attente : case creee ici (elle n'existe pas dans le XAML), calquee sur celle de Playlists.
        var refBox = checks[2].Box;
        var queueBox = new CheckBox
        {
            IsChecked = App.Settings.Current.ShowQueueCategory,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = refBox.FontSize,
            Foreground = refBox.Foreground
        };
        if (refBox.Style != null) queueBox.Style = refBox.Style;
        RoutedEventHandler onQueueChanged = (s, e) =>
        {
            App.Settings.Current.ShowQueueCategory = queueBox.IsChecked == true;
            _ = App.Settings.SaveAsync();
            App.MainWindowInstance?.RefreshNavCategories();
        };
        queueBox.Checked += onQueueChanged;
        queueBox.Unchecked += onQueueChanged;
        _catQueueBox = queueBox;
        checks = checks.Append((Key: "queue", Box: queueBox)).ToArray();

        panel.Children.Clear();
        panel.Spacing = 8;
        Brush fg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        foreach (var (key, box) in checks)
        {
            var grip = new CategoryGrip(key)
            {
                Width = 28,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new FontIcon
                {
                    Glyph = "\uE700",
                    FontSize = 14,
                    Opacity = 0.6,
                    Foreground = fg,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            grip.PointerPressed += CategoryGrip_PointerPressed;
            grip.PointerMoved += CategoryGrip_PointerMoved;
            grip.PointerReleased += CategoryGrip_PointerReleased;
            grip.PointerCaptureLost += CategoryGrip_PointerCaptureLost;
            grip.PointerCanceled += CategoryGrip_PointerCaptureLost;
            grip.PointerEntered += (s, e) => { if (_catDragKey == null) ((FontIcon)grip.Child).Opacity = 1.0; };
            grip.PointerExited += (s, e) => { if (_catDragKey == null) ((FontIcon)grip.Child).Opacity = 0.6; };

            var tt = new TranslateTransform();
            var row = new Grid
            {
                Height = 36,
                ColumnSpacing = 6,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Colors.Transparent),
                Tag = key,
                RenderTransform = tt
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            box.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(grip, 0);
            Grid.SetColumn(box, 1);
            row.Children.Add(grip);
            row.Children.Add(box);
            _catRows[key] = row;
            _catTransforms[key] = tt;
        }

        // Petite phrase d'aide au-dessus de la liste (suit la langue de l'application).
        _catHint = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.6,
            FontSize = 12,
            Foreground = fg
        };
        UpdateCategoryHint();
        Strings.Current.PropertyChanged += (s, e) => DispatcherQueue?.TryEnqueue(UpdateCategoryHint);

        if (card != null)
        {
            card.Children.Remove(panel);
            var host = new StackPanel { Spacing = 12 };
            host.Children.Add(_catHint);
            host.Children.Add(panel);
            card.Children.Add(host);
        }
    }

    private void UpdateCategoryHint()
    {
        if (_catHint == null) return;
        if (_catQueueBox != null) _catQueueBox.Content = Strings.Current.IsFr ? "File d'attente" : "Queue";
        _catHint.Text = Strings.Current.IsFr
            ? "Maintenez la poignée à gauche d'une catégorie et glissez-la pour changer son ordre dans le volet."
            : "Hold the handle on the left of a category and drag it to change its order in the side pane.";
        foreach (var row in _catRows.Values)
        {
            if (row.Children.OfType<CategoryGrip>().FirstOrDefault() is { } g)
                ToolTipService.SetToolTip(g, Strings.Current.IsFr ? "Glisser pour réorganiser" : "Drag to reorder");
        }
    }

    /// <summary>Relit l'ordre enregistre et replace les lignes en consequence (sans animation).</summary>
    private void SyncCategoryOrderFromSettings()
    {
        if (!_catInit) return;
        _catOrder = NavCategories.ResolveOrder(App.Settings.Current);
        ArrangeCategoryRows();
    }

    private void ArrangeCategoryRows()
    {
        if (_catPanel == null) return;
        _catPanel.Children.Clear();
        foreach (string key in _catOrder)
        {
            if (!_catRows.TryGetValue(key, out var row)) continue;
            _catTransforms[key].Y = 0;
            Canvas.SetZIndex(row, 0);
            _catPanel.Children.Add(row);
        }
    }

    private double CategoryPitch()
    {
        double h = 36;
        if (_catRows.Values.FirstOrDefault() is { } r && r.ActualHeight > 0) h = r.ActualHeight;
        return h + (_catPanel?.Spacing ?? 8);
    }

    private readonly Dictionary<string, Storyboard> _catRunningStoryboards = new();
    private readonly Dictionary<string, double> _catTargets = new();

    private void AnimateCategoryRow(string key, TranslateTransform tt, double to, int ms, Action? done = null)
    {
        // Si la ligne est déjà en train d'animer vers la même cible, on la laisse continuer sans réinitialiser
        if (_catTargets.TryGetValue(key, out double currentTarget) && Math.Abs(currentTarget - to) < 0.5)
        {
            return;
        }

        _catTargets[key] = to;

        if (Math.Abs(tt.Y - to) < 0.5)
        {
            if (_catRunningStoryboards.TryGetValue(key, out var cur))
            {
                cur.Stop();
                _catRunningStoryboards.Remove(key);
            }
            tt.Y = to;
            done?.Invoke();
            return;
        }

        // Relever la position courante pour démarrer la nouvelle animation sans à-coup
        double fromY = tt.Y;

        if (_catRunningStoryboards.TryGetValue(key, out var oldSb))
        {
            oldSb.Stop();
            _catRunningStoryboards.Remove(key);
        }

        tt.Y = fromY;

        var anim = new DoubleAnimation
        {
            From = fromY,
            To = to,
            Duration = TimeSpan.FromMilliseconds(ms),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(anim, tt);
        Storyboard.SetTargetProperty(anim, "Y");
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Completed += (s, e) =>
        {
            tt.Y = to;
            if (_catRunningStoryboards.TryGetValue(key, out var cur) && ReferenceEquals(cur, sb))
                _catRunningStoryboards.Remove(key);
            done?.Invoke();
        };
        _catRunningStoryboards[key] = sb;
        _catStoryboards.Add(sb);
        sb.Begin();
    }

    // Fin de toute animation en cours : l'ordre logique est applique pour de bon (les lignes sont reordonnees dans le panneau,
    // les decalages visuels remis a zero : meme rendu, sans saut).
    private void FinalizeCategoryLayout()
    {
        foreach (var sb in _catStoryboards) sb.Stop();
        _catStoryboards.Clear();
        _catRunningStoryboards.Clear();
        _catTargets.Clear();
        ArrangeCategoryRows();
    }

    private void CategoryGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_catPanel == null || sender is not CategoryGrip grip) return;
        var point = e.GetCurrentPoint(grip);
        if (!point.Properties.IsLeftButtonPressed) return;

        _catToken++;
        FinalizeCategoryLayout();

        _catDragKey = grip.Key;
        _catDragStart = _catOrder.ToList();
        _catDragLive = _catOrder.ToList();
        _catDragStartSlot = _catDragStart.IndexOf(grip.Key);
        _catDragSlot = _catDragStartSlot;
        _catDragStartY = e.GetCurrentPoint(_catPanel).Position.Y;

        var row = _catRows[grip.Key];
        row.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(34, 128, 128, 128));
        Canvas.SetZIndex(row, 10);
        ((FontIcon)grip.Child).Opacity = 1.0;
        grip.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void CategoryGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_catDragKey == null || _catPanel == null || sender is not CategoryGrip grip || grip.Key != _catDragKey) return;

        int n = _catDragStart.Count;
        double pitch = CategoryPitch();
        double y = e.GetCurrentPoint(_catPanel).Position.Y;
        double dy = Math.Clamp(y - _catDragStartY, -_catDragStartSlot * pitch, (n - 1 - _catDragStartSlot) * pitch);
        _catTransforms[_catDragKey].Y = dy;

        int newSlot = Math.Clamp((int)Math.Round(_catDragStartSlot + dy / pitch), 0, n - 1);
        if (newSlot == _catDragSlot) return;
        _catDragSlot = newSlot;

        var live = _catDragStart.ToList();
        live.Remove(_catDragKey);
        live.Insert(newSlot, _catDragKey);
        _catDragLive = live;

        // Les autres lignes s'ecartent en glissant.
        foreach (string k in live)
        {
            if (k == _catDragKey) continue;
            double target = (live.IndexOf(k) - _catDragStart.IndexOf(k)) * pitch;
            AnimateCategoryRow(k, _catTransforms[k], target, 160);
        }

        // Le volet de gauche suit en temps reel.
        App.Settings.Current.NavCategoryOrder = live.ToList();
        App.MainWindowInstance?.RefreshNavCategories();
    }

    private void CategoryGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is CategoryGrip grip) EndCategoryDrag(grip, e.Pointer);
    }

    private void CategoryGrip_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is CategoryGrip grip) EndCategoryDrag(grip, null);
    }

    private void EndCategoryDrag(CategoryGrip grip, Microsoft.UI.Xaml.Input.Pointer? pointer)
    {
        if (_catDragKey == null || grip.Key != _catDragKey) return;
        string key = _catDragKey;
        _catDragKey = null;
        if (pointer != null) { try { grip.ReleasePointerCapture(pointer); } catch { } }
        ((FontIcon)grip.Child).Opacity = 0.6;

        var row = _catRows[key];
        row.Background = new SolidColorBrush(Colors.Transparent);

        _catOrder = _catDragLive.ToList();
        App.Settings.Current.NavCategoryOrder = _catOrder.ToList();
        _ = App.Settings.SaveAsync();

        // La ligne lachee se range doucement dans son emplacement, puis le panneau est reordonne pour de bon.
        double target = (_catDragSlot - _catDragStartSlot) * CategoryPitch();
        int token = _catToken;
        AnimateCategoryRow(key, _catTransforms[key], target, 140, () =>
        {
            if (token == _catToken && _catDragKey == null) FinalizeCategoryLayout();
        });
    }
}
