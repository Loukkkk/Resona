using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Resona.Models;
using Resona.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace Resona;

/// <summary>
/// Edition des paroles depuis l'overlay des paroles : un bouton (a cote de celui du choix des paroles) ouvre un editeur par lignes
/// (interrupteur "synchronisees", une ligne par parole, un champ de temps par ligne en mode synchro).
/// - Case "enregistrer dans le fichier" DECOCHEE (defaut) : les paroles ne vivent que dans Resona (LyricsOverrideService), aucun fichier touche.
/// - Case cochee : ecrase la source locale (tag, .lrc, .txt, sous-dossier Lyrics) ou integre les paroles au tag du fichier audio
///   (LyricsEditService), puis le choix manuel devient inutile et est retire.
/// Les paroles personnalisees et les decalages de synchro sont exportes / importes et effacables depuis les Parametres.
/// Contient aussi le rafraichissement des textes de l'overlay au changement de langue.
/// </summary>
public sealed partial class MainWindow
{
    private bool _lyricsEditorOpen;

    // Sons dont les paroles ont ete saisies pendant cette session : une recherche internet qui se termine apres coup ne doit pas les ecraser.
    private readonly HashSet<string> _lyricsEditedThisSession = new(StringComparer.OrdinalIgnoreCase);

    // Texte brut des paroles AFFICHEES dans l'overlay (null = aucune) : c'est ce que l'editeur propose de modifier. Le son en cours
    // peut etre une autre instance de Track que celle de la bibliotheque, dont le champ Lyrics est alors vide.
    private string? _displayedLyricsRaw;
    private bool _displayedLyricsSynced;

    private Button? _lyricsPickerBtn;
    private Button? _lyricsEditBtn;
    private bool _lyricsLanguageHooked;

    // ------------------------------------------------------------------
    //  Langue : les textes de l'overlay etaient figes a sa creation
    // ------------------------------------------------------------------

    private enum LyricsStatus { None, Searching, NotFound, NotFoundShort }
    private LyricsStatus _lyricsStatusKind;

    private static LyricsStatus DetectLyricsStatus(string text) => text switch
    {
        "Recherche des paroles..." or "Searching for lyrics..." => LyricsStatus.Searching,
        "Aucune parole trouvée pour ce morceau." or "No lyrics found for this track." => LyricsStatus.NotFound,
        "Paroles introuvables." or "Lyrics not found." => LyricsStatus.NotFoundShort,
        _ => LyricsStatus.None
    };

    private static string LyricsStatusText(LyricsStatus s, bool fr) => s switch
    {
        LyricsStatus.Searching => fr ? "Recherche des paroles..." : "Searching for lyrics...",
        LyricsStatus.NotFound => fr ? "Aucune parole trouvée pour ce morceau." : "No lyrics found for this track.",
        LyricsStatus.NotFoundShort => fr ? "Paroles introuvables." : "Lyrics not found.",
        _ => string.Empty
    };

    private void HookLyricsOverlayLanguage()
    {
        if (_lyricsLanguageHooked) return;
        _lyricsLanguageHooked = true;
        Strings.Current.PropertyChanged += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "IsFr") DispatcherQueue.TryEnqueue(RefreshLyricsOverlayLanguage);
        };
        RefreshLyricsOverlayLanguage();
    }

    /// <summary>Remet dans la langue courante tout ce que l'overlay avait fige : infobulles, bouton Google, message d'etat.</summary>
    private void RefreshLyricsOverlayLanguage()
    {
        if (_lyricsOverlay == null) return;
        bool fr = Strings.Current.IsFr;

        if (_lyricsPickerBtn != null) ToolTipService.SetToolTip(_lyricsPickerBtn, fr ? "Choisir d'autres paroles" : "Choose different lyrics");
        if (_lyricsEditBtn != null) ToolTipService.SetToolTip(_lyricsEditBtn, fr ? "Modifier / corriger les paroles" : "Edit / fix the lyrics");
        if (_lyricsGoogleBtn != null) _lyricsGoogleBtn.Content = fr ? "Chercher sur Google" : "Search on Google";
        if (_lyricsOffsetResetBtn != null) ToolTipService.SetToolTip(_lyricsOffsetResetBtn, fr ? "Réinitialiser le décalage (0s)" : "Reset sync offset (0s)");
        if (_lyricsOffsetBar?.Child is StackPanel offsetStack && offsetStack.Children.Count >= 4)
        {
            ToolTipService.SetToolTip(offsetStack.Children[1], fr ? "Avancer les paroles (-0.5s) [Touche : [ ou -]" : "Show lyrics earlier (-0.5s) [Key: [ or -]");
            ToolTipService.SetToolTip(offsetStack.Children[3], fr ? "Retarder les paroles (+0.5s) [Touche : ] ou +]" : "Show lyrics later (+0.5s) [Key: ] or +]");
        }

        // Message d'etat affiche ("Recherche...", "Aucune parole...") : on le retraduit sans toucher a de vraies paroles.
        if (_lyricsStatusKind != LyricsStatus.None) ShowPlainLyricsCore(LyricsStatusText(_lyricsStatusKind, fr));
    }

    // ------------------------------------------------------------------
    //  Decalages de synchro : acces pour l'export / import / effacement
    // ------------------------------------------------------------------

    private static string LyricsOffsetKey(string path) => path.Trim().ToLowerInvariant();

    /// <summary>Decalage de synchro memorise pour ce fichier (valeur interne : ajoutee a la position de lecture), null s'il n'y en a pas.</summary>
    public double? GetLyricsOffsetSeconds(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        lock (_lyricsTrackOffsets)
        {
            return _lyricsTrackOffsets.TryGetValue(LyricsOffsetKey(filePath), out double v) ? v : null;
        }
    }

    /// <summary>Import : un decalage deja regle sur ce PC est conserve. Renvoie vrai si celui-ci a ete applique.</summary>
    public bool SetLyricsOffsetIfMissing(string? filePath, double seconds)
    {
        if (string.IsNullOrWhiteSpace(filePath) || Math.Abs(seconds) < 0.01) return false;
        lock (_lyricsTrackOffsets)
        {
            string key = LyricsOffsetKey(filePath);
            if (_lyricsTrackOffsets.ContainsKey(key)) return false;
            _lyricsTrackOffsets[key] = Math.Round(seconds, 1);
        }
        AfterLyricsOffsetsChanged();
        return true;
    }

    public void RemoveLyricsOffsets(IEnumerable<string> filePaths)
    {
        lock (_lyricsTrackOffsets)
        {
            foreach (string p in filePaths)
            {
                if (!string.IsNullOrWhiteSpace(p)) _lyricsTrackOffsets.Remove(LyricsOffsetKey(p));
            }
        }
        AfterLyricsOffsetsChanged();
    }

    /// <summary>Vider "Paroles" (cache) : retire les decalages des sons SANS paroles personnalisees (les autres suivent leurs paroles personnalisees).</summary>
    public void ClearCachedLyricsOffsets()
    {
        lock (_lyricsTrackOffsets)
        {
            foreach (string key in _lyricsTrackOffsets.Keys.ToList())
            {
                if (!LyricsOverrideService.TryGet(key, out _)) _lyricsTrackOffsets.Remove(key);
            }
        }
        AfterLyricsOffsetsChanged();
    }

    public void ClearAllLyricsOffsets()
    {
        lock (_lyricsTrackOffsets) { _lyricsTrackOffsets.Clear(); }
        AfterLyricsOffsetsChanged();
    }

    private void AfterLyricsOffsetsChanged()
    {
        SaveLyricsOffsetsAsync();
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                UpdateLyricsOffsetUI();
                TickSyncedLyrics(App.AudioEngine.CurrentPosition);
            }
            catch { }
        });
    }

    // ------------------------------------------------------------------
    //  Bouton
    // ------------------------------------------------------------------

    /// <summary>Bouton rond de la barre du bas de l'overlay des paroles (meme style que la barre de decalage et le bouton de choix).</summary>
    private Button CreateLyricsEditButton()
    {
        bool fr = Strings.Current.IsFr;
        var btn = new Button
        {
            Content = new FontIcon { Glyph = "\uE70F", FontSize = 14, Opacity = 0.8 },
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(200, 22, 22, 28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
            BorderThickness = new Thickness(1)
        };
        btn.Resources["ButtonBackground"] = new SolidColorBrush(Color.FromArgb(200, 22, 22, 28));
        btn.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(215, 34, 34, 42));
        btn.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(225, 28, 28, 36));
        btn.Resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
        btn.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        btn.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
        btn.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        btn.Resources["ButtonForegroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        // L'overlay se ferme au moindre clic : on empeche la propagation, comme pour les autres boutons de la barre.
        btn.Tapped += (s, e) => { e.Handled = true; };
        btn.PointerPressed += (s, e) => { e.Handled = true; };
        btn.Click += async (s, e) => { await ShowLyricsEditorAsync(); };
        ToolTipService.SetToolTip(btn, fr ? "Modifier / corriger les paroles" : "Edit / fix the lyrics");
        return btn;
    }

    private static string NormalizeLyricsText(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

    private static string SidecarDisplayName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        string name = System.IO.Path.GetFileName(path);
        string? parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
        return string.Equals(parent, "Lyrics", StringComparison.OrdinalIgnoreCase) ? System.IO.Path.Combine(parent!, name) : name;
    }

    // ------------------------------------------------------------------
    //  Editeur par lignes
    // ------------------------------------------------------------------

    /// <summary>
    /// Une ligne = une parole. Mode synchronise : chaque ligne a un temps (mm:ss.xx) avec trois aides pour aller vite :
    /// Entree dans une parole cree la suivante deja datee a l'instant actuel de la musique, l'horloge date la ligne, le triangle se place dans la musique.
    /// </summary>
    private sealed class LyricsRowsEditor
    {
        private static readonly Regex TimeTags = new(@"\[(\d{1,3}:\d{2}(?:[.:]\d{1,3})?)\]", RegexOptions.Compiled);
        private static readonly Regex LeadingTags = new(@"^((?:\[\d{1,3}:\d{2}(?:[.:]\d{1,3})?\])+)\s*(.*)$", RegexOptions.Compiled);
        private static readonly Regex MetaLine = new(@"^\[[A-Za-z]+:.*\]$", RegexOptions.Compiled);

        private sealed class RowUi
        {
            public Grid Grid = null!;
            public TranslateTransform Transform = new();
            public Border Handle = null!;
            public TextBox Time = null!;
            public TextBox Text = null!;
            public Button Seek = null!;
            public Button Stamp = null!;
        }

        private readonly bool _fr;
        private readonly Func<double> _stampDelta;
        private readonly ScrollViewer _scroll;
        private readonly StackPanel _rows = new() { Spacing = 4 };
        private readonly List<RowUi> _items = new();
        private readonly List<string> _meta = new();   // lignes [ar:..] [ti:..] [offset:..] : conservees telles quelles
        private readonly ToggleSwitch _syncSwitch;
        private bool _loading;

        private bool _isDragging;
        private bool _dragMoved;
        private RowUi? _draggedItem;
        private double _dragStartPointerY;
        private int _dragStartItemIndex;
        private int _currentDropIndex;

        public FrameworkElement Root { get; }
        public bool Dirty { get; private set; }
        public bool Synced => _syncSwitch.IsOn;
        public bool HasContent => _items.Any(i => !string.IsNullOrWhiteSpace(i.Text.Text));
        public event Action? Changed;

        public LyricsRowsEditor(bool fr, Func<double> stampDelta)
        {
            _fr = fr;
            _stampDelta = stampDelta;

            _syncSwitch = new ToggleSwitch
            {
                OnContent = fr ? "Synchronisées" : "Synced",
                OffContent = fr ? "Texte simple" : "Plain text",
                VerticalAlignment = VerticalAlignment.Center
            };
            _syncSwitch.Toggled += (s, e) =>
            {
                if (_loading) return;
                ApplyMode();
                MarkDirty();
            };

            var addBtn = new Button { Content = fr ? "+ Ajouter une ligne" : "+ Add a line", VerticalAlignment = VerticalAlignment.Center };
            addBtn.Click += (s, e) => AddRow(_items.Count, string.Empty, Synced ? Format(NowStamp()) : string.Empty, focus: true, markDirty: true);

            var pasteBtn = new Button { Content = fr ? "Coller du texte" : "Paste text", VerticalAlignment = VerticalAlignment.Center };
            pasteBtn.Click += async (s, e) => await PasteAsync();
            ToolTipService.SetToolTip(pasteBtn, fr
                ? "Ajoute les lignes du presse-papiers (texte simple ou paroles .lrc déjà datées)"
                : "Adds the lines from the clipboard (plain text or already timed .lrc lyrics)");

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            toolbar.Children.Add(_syncSwitch);
            toolbar.Children.Add(addBtn);
            toolbar.Children.Add(pasteBtn);

            _rows.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            _rows.PointerMoved += (s, e) =>
            {
                if (!_isDragging || _draggedItem == null) return;

                var posInRows = e.GetCurrentPoint(_rows).Position;
                double deltaY = posInRows.Y - _dragStartPointerY;

                if (!_dragMoved)
                {
                    if (Math.Abs(deltaY) > 4)
                    {
                        _dragMoved = true;
                        _draggedItem.Grid.Opacity = 0.85;
                        _draggedItem.Grid.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                        Canvas.SetZIndex(_draggedItem.Grid, 100);
                    }
                    else
                    {
                        return;
                    }
                }

                _draggedItem.Transform.Y = deltaY;

                try
                {
                    var posInScroll = e.GetCurrentPoint(_scroll).Position;
                    if (posInScroll.Y < 30 && _scroll.VerticalOffset > 0)
                    {
                        _scroll.ChangeView(null, Math.Max(0, _scroll.VerticalOffset - 12), null, true);
                    }
                    else if (posInScroll.Y > _scroll.ActualHeight - 30)
                    {
                        _scroll.ChangeView(null, _scroll.VerticalOffset + 12, null, true);
                    }
                }
                catch { }

                double rowH = GetRowHeight();
                int slotShift = (int)Math.Round(deltaY / rowH);
                int targetSlot = Math.Clamp(_dragStartItemIndex + slotShift, 0, _items.Count - 1);

                if (targetSlot != _currentDropIndex)
                {
                    _currentDropIndex = targetSlot;

                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (i == _dragStartItemIndex) continue;

                        double targetOffset = 0;
                        if (_currentDropIndex > _dragStartItemIndex)
                        {
                            if (i > _dragStartItemIndex && i <= _currentDropIndex)
                            {
                                targetOffset = -rowH;
                            }
                        }
                        else if (_currentDropIndex < _dragStartItemIndex)
                        {
                            if (i >= _currentDropIndex && i < _dragStartItemIndex)
                            {
                                targetOffset = rowH;
                            }
                        }

                        SetRowOffsetAnimated(_items[i], targetOffset);
                    }
                }
            };

            void EndDrag(PointerRoutedEventArgs e)
            {
                if (!_isDragging || _draggedItem == null) return;

                var item = _draggedItem;
                _isDragging = false;
                _draggedItem = null;

                try { _rows.ReleasePointerCapture(e.Pointer); } catch { }

                if (!_dragMoved)
                {
                    item.Grid.Opacity = 1.0;
                    item.Grid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    Canvas.SetZIndex(item.Grid, 0);
                    if (item.Handle.Child is FontIcon ic) ic.Opacity = 0.6;
                    if (item.Handle.ContextFlyout is MenuFlyout fl)
                    {
                        fl.ShowAt(item.Handle);
                    }
                    e.Handled = true;
                    return;
                }

                int from = _dragStartItemIndex;
                int to = _currentDropIndex;
                double rowH = GetRowHeight();
                double dropTargetY = (to - from) * rowH;

                AnimateTranslateY(item.Transform, dropTargetY, 130, () =>
                {
                    foreach (var it in _items)
                    {
                        it.Transform.Y = 0;
                    }

                    item.Grid.Opacity = 1.0;
                    item.Grid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    Canvas.SetZIndex(item.Grid, 0);
                    if (item.Handle.Child is FontIcon ic) ic.Opacity = 0.6;

                    if (from != to && from >= 0 && from < _items.Count && to >= 0 && to < _items.Count)
                    {
                        var grid = _rows.Children[from];
                        _items.RemoveAt(from);
                        _rows.Children.RemoveAt(from);
                        _items.Insert(to, item);
                        _rows.Children.Insert(to, grid);

                        item.Grid.StartBringIntoView();
                        MarkDirty();
                    }
                });

                e.Handled = true;
            }
            _rows.PointerReleased += (s, e) => EndDrag(e);
            _rows.PointerCaptureLost += (s, e) => EndDrag(e);

            _scroll = new ScrollViewer
            {
                Height = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _rows,
                Padding = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                IsTabStop = true,
                UseSystemFocusVisuals = false
            };
            var frame = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Child = _scroll
            };

            var hint = new TextBlock
            {
                Text = fr
                    ? "En mode synchronisé : Entrée dans une parole crée la suivante déjà datée à l'instant actuel de la musique, l'horloge date la ligne, le triangle se place dans la musique. Utilise la poignée pour réorganiser (glisser-déposer, clic ou Alt+Haut/Bas)."
                    : "In synced mode: Enter in a lyric creates the next one already timed to the music's current position, the clock times the line, the triangle jumps to it in the music. Use handle to reorder (drag & drop, click or Alt+Up/Down).",
                FontSize = 11,
                Opacity = 0.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var root = new StackPanel { Width = 660, Spacing = 6, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            root.Children.Add(toolbar);
            root.Children.Add(frame);
            root.Children.Add(hint);

            PointerEventHandler outsideHandler = (s, e) => UnfocusIfOutside(e.OriginalSource);
            _scroll.AddHandler(UIElement.PointerPressedEvent, outsideHandler, true);
            frame.AddHandler(UIElement.PointerPressedEvent, outsideHandler, true);
            root.AddHandler(UIElement.PointerPressedEvent, outsideHandler, true);

            Root = root;
        }

        public void UnfocusIfOutside(object? originalSource)
        {
            DependencyObject? curr = originalSource as DependencyObject;
            while (curr != null)
            {
                if (curr is TextBox || curr is Button || curr is ToggleSwitch || curr is CheckBox || (curr is FrameworkElement fe && fe.Name == "LyricsDragHandle"))
                    return;
                curr = VisualTreeHelper.GetParent(curr);
            }
            try { _scroll.Focus(FocusState.Programmatic); } catch { }
        }

        // ---------- temps ----------

        private double NowStamp()
        {
            double pos = 0;
            try { pos = App.AudioEngine.CurrentPosition.TotalSeconds; } catch { }
            return Math.Max(0, pos + _stampDelta());
        }

        private static string Format(double sec)
        {
            if (double.IsNaN(sec) || sec < 0) sec = 0;
            var t = TimeSpan.FromSeconds(sec);
            return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}:{(t.Milliseconds / 10):00}";
        }

        private static bool TryParseTime(string? s, out double seconds)
        {
            seconds = 0;
            s = (s ?? string.Empty).Trim();
            if (s.Length == 0) return false;
            if (TryParseLrcTime(s, out TimeSpan ts)) { seconds = ts.TotalSeconds; return true; }
            return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0;
        }

        // ---------- chargement ----------

        public void Load(string text)
        {
            _loading = true;
            try
            {
                _items.Clear();
                _rows.Children.Clear();
                _meta.Clear();
                bool lrc = LocalLyricsService.LooksLikeLrc(text);
                _syncSwitch.IsOn = lrc;
                AppendParsed(text, lrc);
                if (_items.Count == 0) AddRow(0, string.Empty, lrc ? "00:00:00" : string.Empty, focus: false, markDirty: false);
                ApplyMode();
            }
            finally { _loading = false; }
            Dirty = false;
        }

        private void AppendParsed(string text, bool lrc)
        {
            string[] lines = NormalizeLyricsText(text).Split('\n');
            if (lrc)
            {
                var parsed = new List<(double Time, string Text)>();
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    Match m = LeadingTags.Match(line);
                    if (m.Success)
                    {
                        string body = m.Groups[2].Value.Trim();
                        if (body.Length == 0) continue;   // marqueur vide : l'affichage l'ignore
                        foreach (Match tag in TimeTags.Matches(m.Groups[1].Value))
                        {
                            if (TryParseTime(tag.Groups[1].Value, out double t)) parsed.Add((t, body));
                        }
                    }
                    else if (MetaLine.IsMatch(line)) _meta.Add(line);
                    else parsed.Add((double.NaN, line));   // ligne sans temps : a dater
                }
                foreach (var p in parsed.OrderBy(x => double.IsNaN(x.Time) ? double.MaxValue : x.Time))
                {
                    AddRow(_items.Count, p.Text, double.IsNaN(p.Time) ? "00:00:00" : Format(p.Time), focus: false, markDirty: false);
                }
            }
            else
            {
                int last = lines.Length;
                while (last > 0 && string.IsNullOrWhiteSpace(lines[last - 1])) last--;
                for (int i = 0; i < last; i++) AddRow(_items.Count, lines[i].TrimEnd(), string.Empty, focus: false, markDirty: false);
            }
        }

        private async Task PasteAsync()
        {
            try
            {
                DataPackageView view = Clipboard.GetContent();
                if (!view.Contains(StandardDataFormats.Text)) return;
                string text = await view.GetTextAsync();
                if (string.IsNullOrWhiteSpace(text)) return;

                bool lrc = LocalLyricsService.LooksLikeLrc(text);
                _loading = true;
                try
                {
                    if (lrc && !Synced) _syncSwitch.IsOn = true;
                    // Une seule ligne de depart encore vide laisse la place au texte colle.
                    bool startedEmpty = _items.Count == 1 && string.IsNullOrWhiteSpace(_items[0].Text.Text) && string.IsNullOrWhiteSpace(_items[0].Time.Text);
                    RowUi? placeholder = startedEmpty ? _items[0] : null;
                    AppendParsed(text, lrc);
                    if (placeholder != null && _items.Count > 1) RemoveRow(placeholder, focusPrevious: false);
                    ApplyMode();
                }
                finally { _loading = false; }
                MarkDirty();
            }
            catch (Exception ex) { LogService.Write("Lyrics", ex, "Collage dans l'editeur de paroles"); }
        }

        // ---------- lignes ----------

        private void ApplyMode()
        {
            var vis = Synced ? Visibility.Visible : Visibility.Collapsed;
            foreach (RowUi r in _items)
            {
                r.Seek.Visibility = vis;
                r.Time.Visibility = vis;
                r.Stamp.Visibility = vis;
                if (Synced && string.IsNullOrWhiteSpace(r.Time.Text))
                {
                    r.Time.Text = "00:00:00";
                }
            }
        }

        private void MarkDirty()
        {
            Dirty = true;
            Changed?.Invoke();
        }

        private double GetRowHeight()
        {
            if (_items.Count > 0 && _items[0].Grid.ActualHeight > 0)
            {
                return _items[0].Grid.ActualHeight + _rows.Spacing;
            }
            return 40.0;
        }

        private static void AnimateTranslateY(TranslateTransform transform, double toY, double durationMs = 140, Action? onCompleted = null)
        {
            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = toY,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Y");
            sb.Children.Add(anim);
            sb.Completed += (s, e) =>
            {
                transform.Y = toY;
                onCompleted?.Invoke();
            };
            sb.Begin();
        }

        private static void SetRowOffsetAnimated(RowUi item, double targetY)
        {
            if (Math.Abs(item.Transform.Y - targetY) < 1.0) return;
            AnimateTranslateY(item.Transform, targetY, 130);
        }

        private void MoveRow(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return;
            if (fromIndex < 0 || fromIndex >= _items.Count) return;
            if (toIndex < 0 || toIndex >= _items.Count) return;

            double rowH = GetRowHeight();
            var fromItem = _items[fromIndex];
            var toItem = _items[toIndex];

            Canvas.SetZIndex(fromItem.Grid, 10);
            double fromTargetY = (toIndex - fromIndex) * rowH;
            double toTargetY = (fromIndex - toIndex) * rowH;

            AnimateTranslateY(fromItem.Transform, fromTargetY, 140);
            AnimateTranslateY(toItem.Transform, toTargetY, 140, () =>
            {
                fromItem.Transform.Y = 0;
                toItem.Transform.Y = 0;
                Canvas.SetZIndex(fromItem.Grid, 0);

                var grid = _rows.Children[fromIndex];
                _items.RemoveAt(fromIndex);
                _rows.Children.RemoveAt(fromIndex);
                _items.Insert(toIndex, fromItem);
                _rows.Children.Insert(toIndex, grid);

                fromItem.Grid.StartBringIntoView();
                MarkDirty();
            });
        }

        private MenuFlyout CreateHandleFlyout(RowUi ui)
        {
            bool fr = _fr;
            var flyout = new MenuFlyout();

            var upItem = new MenuFlyoutItem
            {
                Text = fr ? "Monter (Alt+Haut)" : "Move up (Alt+Up)",
                Icon = new FontIcon { Glyph = "\uE74A", FontSize = 12 }
            };
            var downItem = new MenuFlyoutItem
            {
                Text = fr ? "Descendre (Alt+Bas)" : "Move down (Alt+Down)",
                Icon = new FontIcon { Glyph = "\uE74B", FontSize = 12 }
            };
            var topItem = new MenuFlyoutItem
            {
                Text = fr ? "Placer tout en haut" : "Move to top",
                Icon = new FontIcon { Glyph = "\uE898", FontSize = 12 }
            };
            var bottomItem = new MenuFlyoutItem
            {
                Text = fr ? "Placer tout en bas" : "Move to bottom",
                Icon = new FontIcon { Glyph = "\uE896", FontSize = 12 }
            };

            upItem.Click += (s, e) => { int idx = _items.IndexOf(ui); if (idx > 0) MoveRow(idx, idx - 1); };
            downItem.Click += (s, e) => { int idx = _items.IndexOf(ui); if (idx >= 0 && idx < _items.Count - 1) MoveRow(idx, idx + 1); };
            topItem.Click += (s, e) => { int idx = _items.IndexOf(ui); if (idx > 0) MoveRow(idx, 0); };
            bottomItem.Click += (s, e) => { int idx = _items.IndexOf(ui); if (idx >= 0 && idx < _items.Count - 1) MoveRow(idx, _items.Count - 1); };

            flyout.Opening += (s, e) =>
            {
                int idx = _items.IndexOf(ui);
                upItem.IsEnabled = idx > 0;
                topItem.IsEnabled = idx > 0;
                downItem.IsEnabled = idx >= 0 && idx < _items.Count - 1;
                bottomItem.IsEnabled = idx >= 0 && idx < _items.Count - 1;
            };

            flyout.Items.Add(upItem);
            flyout.Items.Add(downItem);
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(topItem);
            flyout.Items.Add(bottomItem);

            return flyout;
        }

        private static Button IconButton(string glyph, string tip)
        {
            var b = new Button
            {
                Content = new FontIcon { Glyph = glyph, FontSize = 12 },
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(b, tip);
            return b;
        }

        private void AddRow(int index, string text, string timeText, bool focus, bool markDirty)
        {
            bool fr = _fr;
            var ui = new RowUi();

            ui.Handle = new Border
            {
                Name = "LyricsDragHandle",
                Width = 26,
                Height = 32,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new FontIcon
                {
                    Glyph = "\uE700",
                    FontSize = 12,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            ToolTipService.SetToolTip(ui.Handle, fr
                ? "Déplacer la ligne (glisser-déposer, clic pour options, ou Alt+Haut/Bas)"
                : "Move row (drag & drop, click for options, or Alt+Up/Down)");

            var handleFlyout = CreateHandleFlyout(ui);
            ui.Handle.ContextFlyout = handleFlyout;

            var handleIcon = (FontIcon)ui.Handle.Child;
            ui.Handle.PointerEntered += (s, e) =>
            {
                handleIcon.Opacity = 1.0;
                ui.Handle.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
            };
            ui.Handle.PointerExited += (s, e) =>
            {
                if (!_isDragging)
                {
                    handleIcon.Opacity = 0.6;
                    ui.Handle.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            };

            ui.Handle.PointerPressed += (s, e) =>
            {
                var pt = e.GetCurrentPoint(ui.Handle);
                if (pt.Properties.IsLeftButtonPressed)
                {
                    _isDragging = true;
                    _dragMoved = false;
                    _draggedItem = ui;
                    _dragStartItemIndex = _items.IndexOf(ui);
                    _currentDropIndex = _dragStartItemIndex;
                    _dragStartPointerY = e.GetCurrentPoint(_rows).Position.Y;
                    _rows.CapturePointer(e.Pointer);
                    e.Handled = true;
                }
            };

            ui.Seek = IconButton("\uE768", fr ? "Se placer à cet instant dans la musique" : "Jump to this time in the music");

            string initialTime = !string.IsNullOrWhiteSpace(timeText)
                ? timeText
                : (Synced ? "00:00:00" : string.Empty);

            ui.Time = new TextBox
            {
                Width = 108,
                Height = 32,
                Text = initialTime,
                PlaceholderText = "00:00:00",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 7, 6, 3)
            };
            ui.Stamp = IconButton("\uE916", fr ? "Dater avec l'instant actuel de la musique" : "Set to the music's current time");
            ui.Text = new TextBox
            {
                Text = text,
                PlaceholderText = fr ? "Parole…" : "Lyric…",
                IsSpellCheckEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            Button add = IconButton("\uE710", fr ? "Insérer une ligne dessous" : "Insert a line below");
            Button del = IconButton("\uE74D", fr ? "Supprimer la ligne" : "Delete the line");

            var grid = new Grid
            {
                ColumnSpacing = 4,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4, 2, 4, 2),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                RenderTransform = ui.Transform
            };
            for (int i = 0; i < 7; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            }
            var cells = new FrameworkElement[] { ui.Handle, ui.Seek, ui.Time, ui.Stamp, ui.Text, add, del };
            for (int i = 0; i < cells.Length; i++)
            {
                Grid.SetColumn(cells[i], i);
                grid.Children.Add(cells[i]);
            }
            ui.Grid = grid;

            ui.Seek.Click += (s, e) =>
            {
                if (!TryParseTime(ui.Time.Text, out double t)) return;
                try { App.AudioEngine.Seek(TimeSpan.FromSeconds(Math.Max(0, t - _stampDelta()))); } catch { }
            };
            ui.Stamp.Click += (s, e) => ui.Time.Text = Format(NowStamp());
            add.Click += (s, e) => InsertAfter(ui, stamp: true);
            del.Click += (s, e) => { RemoveRow(ui, focusPrevious: true); MarkDirty(); };

            ui.Text.TextChanged += (s, e) => { if (!_loading) MarkDirty(); };
            ui.Time.TextChanged += (s, e) =>
            {
                ui.Time.ClearValue(Control.BorderBrushProperty);
                if (!_loading) MarkDirty();
            };

            ui.Time.GotFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(ui.Time.Text) && Synced)
                {
                    ui.Time.Text = "00:00:00";
                }
            };

            ui.Time.LostFocus += (s, e) =>
            {
                string raw = ui.Time.Text.Trim();
                if (raw.Length > 0 && TryParseTime(raw, out double t))
                {
                    ui.Time.Text = Format(t);
                }
                else if (Synced)
                {
                    ui.Time.Text = "00:00:00";
                }
            };

            ui.Time.KeyDown += (s, e) =>
            {
                bool alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                if (alt && e.Key == Windows.System.VirtualKey.Up)
                {
                    int idx = _items.IndexOf(ui);
                    if (idx > 0)
                    {
                        e.Handled = true;
                        MoveRow(idx, idx - 1);
                        ui.Time.Focus(FocusState.Programmatic);
                    }
                }
                else if (alt && e.Key == Windows.System.VirtualKey.Down)
                {
                    int idx = _items.IndexOf(ui);
                    if (idx >= 0 && idx < _items.Count - 1)
                    {
                        e.Handled = true;
                        MoveRow(idx, idx + 1);
                        ui.Time.Focus(FocusState.Programmatic);
                    }
                }
                else if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    e.Handled = true;
                    string raw = ui.Time.Text.Trim();
                    if (raw.Length > 0 && TryParseTime(raw, out double t))
                    {
                        ui.Time.Text = Format(t);
                    }
                    ui.Text.Focus(FocusState.Programmatic);
                }
            };

            ui.Text.KeyDown += (s, e) =>
            {
                bool alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                if (alt && e.Key == Windows.System.VirtualKey.Up)
                {
                    int idx = _items.IndexOf(ui);
                    if (idx > 0)
                    {
                        e.Handled = true;
                        MoveRow(idx, idx - 1);
                        ui.Text.Focus(FocusState.Programmatic);
                    }
                }
                else if (alt && e.Key == Windows.System.VirtualKey.Down)
                {
                    int idx = _items.IndexOf(ui);
                    if (idx >= 0 && idx < _items.Count - 1)
                    {
                        e.Handled = true;
                        MoveRow(idx, idx + 1);
                        ui.Text.Focus(FocusState.Programmatic);
                    }
                }
                else if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    e.Handled = true;
                    InsertAfter(ui, stamp: true);
                }
                else if (e.Key == Windows.System.VirtualKey.Back && ui.Text.Text.Length == 0 && _items.Count > 1)
                {
                    e.Handled = true;
                    RemoveRow(ui, focusPrevious: true);
                    MarkDirty();
                }
            };

            index = Math.Clamp(index, 0, _items.Count);
            _items.Insert(index, ui);
            _rows.Children.Insert(index, grid);

            var vis = Synced ? Visibility.Visible : Visibility.Collapsed;
            ui.Seek.Visibility = vis;
            ui.Time.Visibility = vis;
            ui.Stamp.Visibility = vis;

            if (focus)
            {
                ui.Text.Loaded += (s, e) =>
                {
                    ui.Text.Focus(FocusState.Programmatic);
                    ui.Grid.StartBringIntoView();
                };
            }
            if (markDirty) MarkDirty();
        }

        private void InsertAfter(RowUi after, bool stamp)
        {
            int index = _items.IndexOf(after) + 1;
            AddRow(index, string.Empty, Synced && stamp ? Format(NowStamp()) : string.Empty, focus: true, markDirty: true);
        }

        private void RemoveRow(RowUi ui, bool focusPrevious)
        {
            int index = _items.IndexOf(ui);
            if (index < 0) return;
            _items.RemoveAt(index);
            _rows.Children.Remove(ui.Grid);
            if (_items.Count == 0)
            {
                AddRow(0, string.Empty, string.Empty, focus: false, markDirty: false);
                return;
            }
            if (focusPrevious) _items[Math.Max(0, index - 1)].Text.Focus(FocusState.Programmatic);
        }

        // ---------- resultat ----------

        /// <summary>Texte final (LRC en mode synchronise, une parole par ligne sinon). Null + message si un temps est invalide.</summary>
        public string? Build(out string error)
        {
            error = string.Empty;
            var lines = new List<string>();

            if (Synced)
            {
                var entries = new List<(double Time, string Text)>();
                for (int i = 0; i < _items.Count; i++)
                {
                    RowUi r = _items[i];
                    string text = r.Text.Text.Trim();
                    if (text.Length == 0) continue;
                    if (!TryParseTime(r.Time.Text, out double t))
                    {
                        r.Time.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 120, 120));
                        r.Grid.StartBringIntoView();
                        error = _fr
                            ? $"Ligne {i + 1} : temps manquant ou invalide (format mm:ss:xx)."
                            : $"Line {i + 1}: missing or invalid time (format mm:ss:xx).";
                        return null;
                    }
                    entries.Add((t, text));
                }
                lines.AddRange(_meta);
                lines.AddRange(entries.OrderBy(x => x.Time).Select(x => $"[{Format(x.Time)}]{x.Text}"));
            }
            else
            {
                lines.AddRange(_items.Select(r => r.Text.Text.TrimEnd()));
                while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
                while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
            }
            return string.Join("\n", lines);
        }
    }

    // ------------------------------------------------------------------
    //  Fenetre d'edition
    // ------------------------------------------------------------------

    private async Task ShowLyricsEditorAsync()
    {
        if (_lyricsEditorOpen) return;
        Track? track = _nowPlayingTrack ?? GetCurrentTrackForUi();
        if (track == null || string.IsNullOrWhiteSpace(track.FilePath)) return;

        bool fr = Strings.Current.IsFr;
        _lyricsEditorOpen = true;
        try
        {
            bool hadCustom = LyricsOverrideService.TryGet(track.FilePath, out var customEntry);
            // Ce qui est affiche dans l'overlay en premier : l'instance du son en cours peut ne pas porter ses paroles (voir _displayedLyricsRaw).
            string original = NormalizeLyricsText(
                !string.IsNullOrWhiteSpace(_displayedLyricsRaw) ? _displayedLyricsRaw
                : !string.IsNullOrWhiteSpace(track.Lyrics) ? track.Lyrics
                : hadCustom ? customEntry.Lyrics
                : string.Empty);

            var header = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(track.Artist) ? track.Title : $"{track.Artist} — {track.Title}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };
            var sourceInfo = new TextBlock { FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };

            // Temps a ecrire pour qu'une ligne apparaisse a la position de lecture P : l'affichage utilise P + [offset:] du fichier + decalage manuel.
            var editor = new LyricsRowsEditor(fr, () => GetTrackLyricsOffset(track) + _lrcHeaderOffset.TotalSeconds);
            editor.Load(original);

            var saveCheck = new CheckBox
            {
                Content = fr ? "Enregistrer dans le fichier (remplace la source)" : "Save to file (replaces the source)",
                IsChecked = false,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var saveNote = new TextBlock { FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(32, 0, 0, 0) };
            var errorText = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 120, 120)),
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };

            // Largeur fixe : tout le contenu a une largeur imposee et la fenetre a la meme largeur mini et maxi.
            var root = new StackPanel { Width = 660, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            root.Children.Add(header);
            root.Children.Add(sourceInfo);
            root.Children.Add(editor.Root);
            root.Children.Add(saveCheck);
            root.Children.Add(saveNote);
            root.Children.Add(errorText);

            PointerEventHandler dialogOutsideHandler = (s, e) => editor.UnfocusIfOutside(e.OriginalSource);
            root.AddHandler(UIElement.PointerPressedEvent, dialogOutsideHandler, true);

            var dialog = new ContentDialog
            {
                XamlRoot = ((UIElement)ContentFrame).XamlRoot,
                Title = fr ? "Modifier les paroles" : "Edit lyrics",
                Content = new ScrollViewer { MaxHeight = 640, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = root },
                PrimaryButtonText = fr ? "Enregistrer" : "Save",
                CloseButtonText = fr ? "Annuler" : "Cancel",
                DefaultButton = ContentDialogButton.None,
                PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                IsPrimaryButtonEnabled = false
            };
            dialog.AddHandler(UIElement.PointerPressedEvent, dialogOutsideHandler, true);
            dialog.Resources["ContentDialogMinWidth"] = 720.0;
            dialog.Resources["ContentDialogMaxWidth"] = 720.0;
            if (hadCustom)
            {
                dialog.SecondaryButtonText = fr ? "Revenir à l'original" : "Back to original";
            }

            LyricsEditService.LyricsSource? source = null;

            string DescribeCurrentSource()
            {
                if (hadCustom) return fr ? "Source : paroles personnalisées (stockées uniquement dans Resona)." : "Source: custom lyrics (stored only inside Resona).";
                if (source == null) return fr ? "Recherche de la source…" : "Locating the source…";
                if (source.Kind == LyricsEditService.SourceKind.EmbeddedTag) return fr ? "Source : tag du fichier audio." : "Source: the audio file's tag.";
                if (source.Kind == LyricsEditService.SourceKind.Sidecar) return (fr ? "Source : " : "Source: ") + SidecarDisplayName(source.FilePath) + ".";
                return string.IsNullOrWhiteSpace(original)
                    ? (fr ? "Aucune parole pour ce morceau : tu peux les saisir ici." : "No lyrics for this track: you can type them here.")
                    : (fr ? "Source : recherche en ligne (gardée en cache par Resona)." : "Source: online search (cached by Resona).");
            }

            void RefreshNote()
            {
                if (saveCheck.IsChecked == true)
                {
                    string note;
                    if (source == null) note = fr ? "Recherche de la destination…" : "Locating the destination…";
                    else if (source.Kind == LyricsEditService.SourceKind.Sidecar)
                        note = fr ? $"Le fichier « {SidecarDisplayName(source.FilePath)} » sera remplacé." : $"The file “{SidecarDisplayName(source.FilePath)}” will be replaced.";
                    else if (source.Kind == LyricsEditService.SourceKind.EmbeddedTag)
                        note = fr ? "Le tag « paroles » du fichier audio sera remplacé." : "The lyrics tag of the audio file will be replaced.";
                    else
                        note = fr ? "Les paroles seront intégrées au tag du fichier audio." : "The lyrics will be embedded in the audio file's tag.";

                    if (source == null || source.Kind != LyricsEditService.SourceKind.Sidecar)
                    {
                        note += fr ? " La lecture est interrompue un court instant pour pouvoir écrire dans le fichier." : " Playback pauses briefly so the file can be written.";
                    }
                    saveNote.Text = note;
                }
                else
                {
                    saveNote.Text = fr ? "Visible uniquement dans Resona : aucun fichier n'est modifié." : "Only visible inside Resona: no file is modified.";
                }
            }

            void RefreshSaveEnabled()
            {
                dialog.IsPrimaryButtonEnabled = editor.HasContent && (editor.Dirty || saveCheck.IsChecked == true);
            }

            sourceInfo.Text = DescribeCurrentSource();
            RefreshNote();
            editor.Changed += RefreshSaveEnabled;
            saveCheck.Checked += (s, e) => { RefreshNote(); RefreshSaveEnabled(); };
            saveCheck.Unchecked += (s, e) => { RefreshNote(); RefreshSaveEnabled(); };
            dialog.Opened += async (s, e) =>
            {
                source = await Task.Run(() => LyricsEditService.LocateSource(track.FilePath));
                sourceInfo.Text = DescribeCurrentSource();
                RefreshNote();
                RefreshSaveEnabled();
            };

            bool committed = false;
            string committedText = string.Empty;
            LyricsEditService.SaveResult? savedResult = null;

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    errorText.Visibility = Visibility.Collapsed;

                    string? text = editor.Build(out string buildError);
                    if (text == null)
                    {
                        errorText.Text = buildError;
                        errorText.Visibility = Visibility.Visible;
                        args.Cancel = true;
                        return;
                    }

                    // Meme controle que l'affichage : sinon l'overlay montrerait "Aucune parole trouvee" apres l'enregistrement.
                    if (!LyricsService.HasActualLyrics(text))
                    {
                        errorText.Text = fr
                            ? "Les paroles doivent contenir au moins 2 lignes de texte (et pas de suites de « ? »)."
                            : "The lyrics must contain at least 2 lines of text (and no runs of “?”).";
                        errorText.Visibility = Visibility.Visible;
                        args.Cancel = true;
                        return;
                    }

                    if (saveCheck.IsChecked == true)
                    {
                        dialog.IsPrimaryButtonEnabled = false;
                        editor.Root.IsHitTestVisible = false;
                        saveCheck.IsEnabled = false;
                        saveNote.Text = fr ? "Écriture dans le fichier…" : "Writing to the file…";

                        var result = await LyricsEditService.SaveToFileAsync(track.FilePath, text);

                        editor.Root.IsHitTestVisible = true;
                        saveCheck.IsEnabled = true;
                        if (!result.Success)
                        {
                            errorText.Text = (fr ? "Impossible d'écrire dans le fichier : " : "Could not write to the file: ") + result.Error
                                + (fr ? "\nTu peux décocher la case pour garder ces paroles seulement dans Resona." : "\nYou can untick the box to keep these lyrics inside Resona only.");
                            errorText.Visibility = Visibility.Visible;
                            RefreshNote();
                            RefreshSaveEnabled();
                            args.Cancel = true;
                            return;
                        }
                        savedResult = result;
                    }

                    committedText = text;
                    committed = true;
                }
                catch (Exception ex)
                {
                    LogService.Write("Lyrics", ex, "Editeur de paroles");
                    editor.Root.IsHitTestVisible = true;
                    saveCheck.IsEnabled = true;
                    errorText.Text = ex.Message;
                    errorText.Visibility = Visibility.Visible;
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            ContentDialogResult dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.Primary && committed)
            {
                await ApplyEditedLyricsAsync(track, committedText, savedResult != null);
            }
            else if (dialogResult == ContentDialogResult.Secondary)
            {
                ResetCustomLyricsFromEditor(track);
            }
        }
        catch (Exception ex)
        {
            LogService.Write("Lyrics", ex, "Editeur de paroles");
        }
        finally
        {
            _lyricsEditorOpen = false;
        }
    }

    /// <summary>Met a jour le son et l'affichage. savedToFile : le fichier est la source de verite, pas de choix manuel a garder.</summary>
    private async Task ApplyEditedLyricsAsync(Track track, string text, bool savedToFile)
    {
        bool fr = Strings.Current.IsFr;
        bool synced = LocalLyricsService.LooksLikeLrc(text);

        if (savedToFile)
        {
            LyricsOverrideService.Remove(track.FilePath);
        }
        else
        {
            LyricsOverrideService.Set(track.FilePath, text, synced, fr ? "Modifiées manuellement" : "Edited manually");
        }
        _lyricsEditedThisSession.Add(track.Id);
        SetTrackLyricsEverywhere(track.FilePath, text, synced);

        // Ecrit dans le fichier : on met aussi a jour le cache disque, sinon un ancien texte en cache passerait avant le fichier au prochain lancement.
        // (Pas en mode "Resona seulement" : le texte modifie ne doit pas s'incruster dans le cache, "Revenir a l'original" doit pouvoir le retirer.)
        if (savedToFile && !track.IsExternal)
        {
            try { await App.Cache.UpsertTrackAsync(track); }
            catch (Exception ex) { LogService.Write("Lyrics", ex, "Mise a jour du cache des paroles"); }
        }

        // Le son en cours est reconnu par son chemin : l'instance de Track affichee peut differer de celle de la bibliotheque.
        if (string.Equals(track.FilePath, _nowPlayingFilePath, StringComparison.OrdinalIgnoreCase)
            || ReferenceEquals(track, GetCurrentTrackForUi()))
        {
            _lrcLines.Clear();
            _lrcCurrentIndex = -1;
            LoadLyricsAsync(text, synced);
            try { TickSyncedLyrics(App.AudioEngine.CurrentPosition); } catch { }
        }
    }

    /// <summary>"Revenir a l'original" : retire les paroles personnalisees et relance la detection (local puis internet).</summary>
    private void ResetCustomLyricsFromEditor(Track track)
    {
        ResetPickedLyrics(track);
        SetTrackLyricsEverywhere(track.FilePath, null, false);
        _lyricsEditedThisSession.Remove(track.Id);
        if (!track.IsExternal) { _ = App.Cache.UpsertTrackAsync(track); }
    }

    private void SetTrackLyricsEverywhere(string filePath, string? text, bool synced)
    {
        void Apply(IEnumerable<Track>? tracks)
        {
            if (tracks == null) return;
            foreach (Track t in tracks)
            {
                if (string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    t.Lyrics = text;
                    t.LyricsSynced = synced;
                }
            }
        }
        Apply(_library);
        Apply(_queue);
        Apply(_manualQueue);
        if (_nowPlayingTrack != null) Apply(new[] { _nowPlayingTrack });
    }

    /// <summary>Parametres > Vider le cache > "Paroles personnalisees" : efface tous les choix / modifications manuels (et leurs decalages de synchro).</summary>
    public async void ClearCustomLyrics()
    {
        try
        {
            List<string> paths = LyricsOverrideService.ClearAll();
            _lyricsEditedThisSession.Clear();
            if (paths.Count == 0) return;
            RemoveLyricsOffsets(paths);

            var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            var affected = _library.Where(t => set.Contains(t.FilePath)).ToList();
            foreach (Track t in affected)
            {
                t.Lyrics = null;
                t.LyricsSynced = false;
            }
            if (affected.Count > 0) await App.Cache.UpsertTracksBatchedAsync(affected);

            Track? current = _nowPlayingTrack ?? GetCurrentTrackForUi();
            if (current != null && set.Contains(current.FilePath))
            {
                current.Lyrics = null;
                current.LyricsSynced = false;
                _lrcLines.Clear();
                _lrcCurrentIndex = -1;
                UpdateLyricsOverlayTrackInfo(current);
                _ = FindMissingLyricsAsync(current);
            }
        }
        catch (Exception ex)
        {
            LogService.Write("Lyrics", ex, "Effacement des paroles personnalisees");
        }
    }
}
