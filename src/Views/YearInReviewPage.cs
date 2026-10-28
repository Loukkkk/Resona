// ============================================================
//  YearInReviewPage.cs  —  Resona
//  Ecran "Year in review" : une suite de pages animees (style "story"), navigables au clic / aux fleches, exportable en image.
//  Page 100 % code (pas de XAML). Affichee a la place du contenu courant par MainWindow.ShowYearInReview(), qui restaure
//  l'ecran precedent a la fermeture.
//  Navigation : clic (a gauche = page precedente, ailleurs = suivante), fleches / Espace / Entree, Echap pour fermer.
// ============================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Resona.Models;
using Resona.Services;
using Windows.Foundation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Resona.Views;

public sealed class YearInReviewPage : Page
{
    private static string L(string fr, string en) => Strings.Current.IsFr ? fr : en;
    private static CultureInfo Culture => CultureInfo.GetCultureInfo(Strings.Current.IsFr ? "fr-FR" : "en-US");

    private static readonly Windows.UI.Color AccentColor = Windows.UI.Color.FromArgb(255, 150, 118, 255);

    private readonly YearInReviewData _d;
    private readonly IReadOnlyDictionary<string, string?> _covers;
    private readonly Action _onClose;

    private readonly List<(Func<FrameworkElement> Build, Windows.UI.Color From, Windows.UI.Color To)> _slides = new();
    private readonly Grid _root = new();
    private readonly Grid _stage = new() { Margin = new Thickness(40, 84, 40, 56) };
    private readonly Grid _progress = new() { ColumnSpacing = 6, Height = 4, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(64, 56, 64, 0) };
    private readonly List<Border> _segments = new();
    private int _index = -1;
    private int _token;
    private TextBlock? _exportStatus;
    private readonly CompositeTransform _rootTransform = new();
    // Tout ce qui est devant le fond (page, progression, boutons, aide) : c'est CE calque qui bouge a l'apparition / a la fermeture,
    // le fond en degrade (_root) reste immobile et ne fait que se fondre.
    private readonly Grid _foreground = new();

    public YearInReviewPage(YearInReviewData data, IReadOnlyDictionary<string, string?> coverPathByTrackId, Action onClose)
    {
        _d = data;
        _covers = coverPathByTrackId;
        _onClose = onClose;
        IsTabStop = true;

        BuildSlideList();
        BuildChrome();
        Content = _root;

        Loaded += (_, _) =>
        {
            Focus(FocusState.Programmatic);
            if (_index < 0)
            {
                PlayEntrance();
                Show(0);
            }
        };
        Unloaded += (_, _) => _token++;   // arrete les compteurs animes en cours
        KeyDown += OnKeyDown;
    }

    // ------------------------------------------------------------------ structure

    private static Windows.UI.Color C(byte r, byte g, byte b) => Windows.UI.Color.FromArgb(255, r, g, b);

    private void BuildSlideList()
    {
        var dark = C(11, 11, 20);
        _slides.Add((SlideIntro, C(52, 30, 112), dark));
        _slides.Add((SlideTotal, C(14, 72, 90), dark));
        if (_d.TopTracks.Count > 0) _slides.Add((SlideTopTrack, C(96, 30, 84), dark));
        if (_d.TopTracks.Count > 1) _slides.Add((SlideTopTracks, C(84, 58, 14), dark));
        if (_d.TopArtists.Count > 0) _slides.Add((SlideArtists, C(20, 74, 54), dark));
        if (_d.TopGenres.Count > 0) _slides.Add((SlideGenres, C(36, 52, 118), dark));
        _slides.Add((SlideRhythm, C(100, 36, 40), dark));
        _slides.Add((SlideOutro, C(52, 30, 112), dark));
    }

    private static FrameworkElement BuildCloseCross()
    {
        var stroke = new SolidColorBrush(Microsoft.UI.Colors.White);
        var cross = new Grid { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        cross.Children.Add(new Microsoft.UI.Xaml.Shapes.Line { X1 = 0, Y1 = 0, X2 = 14, Y2 = 14, Stroke = stroke, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        cross.Children.Add(new Microsoft.UI.Xaml.Shapes.Line { X1 = 14, Y1 = 0, X2 = 0, Y2 = 14, Stroke = stroke, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        return cross;
    }

    private void BuildChrome()
    {
        _foreground.Children.Add(_stage);

        for (int i = 0; i < _slides.Count; i++)
        {
            _progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var seg = new Border { CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(70, 255, 255, 255)) };
            Grid.SetColumn(seg, i);
            _segments.Add(seg);
            _progress.Children.Add(seg);
        }
        _foreground.Children.Add(_progress);

        var close = new Button
        {
            // Croix dessinee avec deux traits (et non avec un glyphe de police, dont les marges internes la decalaient).
            Content = BuildCloseCross(),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10, 6, 0, 0),
            // Taille fixe + contenu centre : la croix reste au milieu du carre qui apparait au survol.
            Width = 40,
            Height = 40,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(close, L("Fermer", "Close"));
        close.Click += (_, _) => CloseAnimated();
        _foreground.Children.Add(close);

        var hint = new TextBlock
        {
            Text = L("Cliquez ou utilisez ← → pour naviguer  •  Échap pour fermer", "Click or use ← → to navigate  •  Esc to close"),
            FontSize = 12,
            Opacity = 0.4,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 16),
            IsHitTestVisible = false
        };
        _foreground.Children.Add(hint);
        _root.Children.Add(_foreground);

        _root.Tapped += OnTapped;
    }

    // ------------------------------------------------------------------ navigation

    private bool _closing;

    /// <summary>Apparition : fondu + leger zoom arriere (l'ecran "se pose" sur l'application).</summary>
    private void PlayEntrance()
    {
        Opacity = 0;
        var scale = _rootTransform;
        scale.ScaleX = 1.06;
        scale.ScaleY = 1.06;
        scale.TranslateY = 0;
        _foreground.RenderTransformOrigin = new Point(0.5, 0.5);
        _foreground.RenderTransform = scale;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(600);

        var fade = new DoubleAnimation { From = 0, To = 1, Duration = duration, EasingFunction = ease };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var zoomX = new DoubleAnimation { From = 1.06, To = 1, Duration = duration, EasingFunction = ease };
        Storyboard.SetTarget(zoomX, scale);
        Storyboard.SetTargetProperty(zoomX, "ScaleX");

        var zoomY = new DoubleAnimation { From = 1.06, To = 1, Duration = duration, EasingFunction = ease };
        Storyboard.SetTarget(zoomY, scale);
        Storyboard.SetTargetProperty(zoomY, "ScaleY");

        var sb = new Storyboard();
        sb.Children.Add(fade);
        sb.Children.Add(zoomX);
        sb.Children.Add(zoomY);
        sb.Begin();
    }

    /// <summary>Fermeture en fondu, puis retrait reel de l'ecran (callback fourni par MainWindow).</summary>
    private void CloseAnimated()
    {
        if (_closing) return;
        _closing = true;
        _token++;   // arrete les compteurs

        // Inverse de l'apparition : d'abord les elements du bilan partent (fondu + zoom 1 -> 1.06), PUIS le fond se fond a son tour.
        var fade = new DoubleAnimation { From = Opacity, To = 0, BeginTime = TimeSpan.FromMilliseconds(300), Duration = TimeSpan.FromMilliseconds(450), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var slide = new DoubleAnimation { From = _rootTransform.TranslateY, To = 0, Duration = TimeSpan.FromMilliseconds(450), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(slide, _rootTransform);
        Storyboard.SetTargetProperty(slide, "TranslateY");
        _foreground.RenderTransformOrigin = new Point(0.5, 0.5);
        _foreground.RenderTransform = _rootTransform;
        var shrinkX = new DoubleAnimation { From = _rootTransform.ScaleX, To = 1.06, Duration = TimeSpan.FromMilliseconds(450), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(shrinkX, _rootTransform);
        Storyboard.SetTargetProperty(shrinkX, "ScaleX");
        var shrinkY = new DoubleAnimation { From = _rootTransform.ScaleY, To = 1.06, Duration = TimeSpan.FromMilliseconds(450), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(shrinkY, _rootTransform);
        Storyboard.SetTargetProperty(shrinkY, "ScaleY");

        // Les elements (contenu, progression, boutons) se fondent en premier.
        var contentFade = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(450), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(contentFade, _foreground);
        Storyboard.SetTargetProperty(contentFade, "Opacity");

        var sb = new Storyboard();
        sb.Children.Add(contentFade);
        sb.Children.Add(fade);
        sb.Children.Add(slide);
        sb.Children.Add(shrinkX);
        sb.Children.Add(shrinkY);
        sb.Completed += (_, _) => _onClose();
        sb.Begin();
    }

    private void Next() { if (_index < _slides.Count - 1) Show(_index + 1); }
    private void Prev() { if (_index > 0) Show(_index - 1); }

    private void Show(int i)
    {
        _index = i;
        _token++;
        Focus(FocusState.Programmatic);   // pour recevoir les touches fleches / Espace
        var (build, from, to) = _slides[i];

        _root.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0.55, 1),
            GradientStops =
            {
                new GradientStop { Color = from, Offset = 0 },
                new GradientStop { Color = to, Offset = 1 }
            }
        };

        _stage.Children.Clear();
        _exportStatus = null;
        _stage.Children.Add(build());

        for (int s = 0; s < _segments.Count; s++)
            _segments[s].Background = new SolidColorBrush(s <= i
                ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                : Windows.UI.Color.FromArgb(70, 255, 255, 255));
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        Focus(FocusState.Pointer);
        double x = e.GetPosition(_root).X;
        if (x < _root.ActualWidth * 0.3) Prev(); else Next();
    }

    private static bool IsInsideButton(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Right:
            case Windows.System.VirtualKey.Down:
            case Windows.System.VirtualKey.Space:
            case Windows.System.VirtualKey.Enter:
                Next(); e.Handled = true; break;
            case Windows.System.VirtualKey.Left:
            case Windows.System.VirtualKey.Up:
                Prev(); e.Handled = true; break;
            case Windows.System.VirtualKey.Escape:
                CloseAnimated(); e.Handled = true; break;
        }
    }

    // ------------------------------------------------------------------ outils d'interface

    private static SolidColorBrush White(byte alpha = 255) => new(Windows.UI.Color.FromArgb(alpha, 255, 255, 255));

    private static TextBlock TB(string text, double size, bool bold = false, double opacity = 1.0, TextAlignment align = TextAlignment.Center)
        => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Opacity = opacity,
            Foreground = White(),
            TextAlignment = align,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };

    private static StackPanel NewPanel() => new()
    {
        Width = 760,
        Spacing = 14,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>Centre le contenu et le reduit (jamais l'agrandit) si la fenetre est trop petite.</summary>
    private static FrameworkElement Wrap(FrameworkElement content) => new Viewbox
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Child = content
    };

    /// <summary>Apparition : fondu + leger glissement vers le haut, avec un delai (pour echelonner les elements).</summary>
    private static void Reveal(UIElement el, int delayMs, double fromY = 28)
    {
        el.Opacity = 0;
        var tt = new TranslateTransform { Y = fromY };
        el.RenderTransform = tt;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var begin = TimeSpan.FromMilliseconds(delayMs);

        var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(550), BeginTime = begin, EasingFunction = ease };
        Storyboard.SetTarget(fade, el);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var slide = new DoubleAnimation { From = fromY, To = 0, Duration = TimeSpan.FromMilliseconds(550), BeginTime = begin, EasingFunction = ease };
        Storyboard.SetTarget(slide, tt);
        Storyboard.SetTargetProperty(slide, "Y");

        var sb = new Storyboard();
        sb.Children.Add(fade);
        sb.Children.Add(slide);
        sb.Begin();
    }

    /// <summary>Barre qui "pousse" depuis son origine (bas pour une colonne, gauche pour une barre horizontale).</summary>
    private static void Grow(FrameworkElement bar, bool vertical, int delayMs)
    {
        var st = new ScaleTransform();
        bar.RenderTransform = st;
        string prop;
        if (vertical) { bar.RenderTransformOrigin = new Point(0.5, 1); st.ScaleY = 0; prop = "ScaleY"; }
        else { bar.RenderTransformOrigin = new Point(0, 0.5); st.ScaleX = 0; prop = "ScaleX"; }

        var grow = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(800),
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(grow, st);
        Storyboard.SetTargetProperty(grow, prop);
        var sb = new Storyboard();
        sb.Children.Add(grow);
        sb.Begin();
    }

    /// <summary>Compteur anime : le texte monte de 0 a la valeur finale. S'arrete si on change de page.</summary>
    private async Task CountUp(TextBlock tb, double target, Func<double, string> format, int durationMs, int token, int delayMs = 0)
    {
        tb.Text = format(0);
        if (delayMs > 0) await Task.Delay(delayMs);
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < durationMs)
        {
            if (token != _token) return;
            double p = sw.ElapsedMilliseconds / (double)durationMs;
            p = 1 - Math.Pow(1 - p, 3);   // ralentit vers la fin
            tb.Text = format(target * p);
            await Task.Delay(16);
        }
        if (token == _token) tb.Text = format(target);
    }

    private FrameworkElement CoverTile(string trackId, double size, double radius)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(radius),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(46, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        string? path = null;
        if (!string.IsNullOrEmpty(trackId)) _covers.TryGetValue(trackId, out path);

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            border.Background = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(path)) { DecodePixelWidth = (int)(size * 2) },
                Stretch = Stretch.UniformToFill
            };
        }
        else
        {
            border.Child = new FontIcon
            {
                Glyph = "\uE8D6",
                FontSize = size * 0.4,
                Opacity = 0.6,
                Foreground = White()
            };
        }
        return border;
    }

    // ------------------------------------------------------------------ formats

    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours:N0} h {ts.Minutes:00} min";
        return $"{(int)ts.TotalMinutes} min";
    }

    private static string Plays(int n) => $"{n.ToString("N0", Culture)} {L(n > 1 ? "écoutes" : "écoute", n > 1 ? "plays" : "play")}";

    private string CoverageText()
    {
        if (_d.IsPartial && _d.CoverageStart.HasValue)
            return L("Depuis le ", "Since ") + _d.CoverageStart.Value.ToString("d MMMM yyyy", Culture);
        if (_d.IsOngoing) return L("À ce jour", "So far");
        return "";
    }

    // ------------------------------------------------------------------ pages

    private FrameworkElement SlideIntro()
    {
        var p = NewPanel();
        var year = TB(_d.Year.ToString(CultureInfo.InvariantCulture), 150, true);
        Reveal(year, 0);
        var sub = TB(L("Votre année en musique", "Your year in music"), 34, false, 0.9);
        Reveal(sub, 350);
        p.Children.Add(year);
        p.Children.Add(sub);

        string cov = CoverageText();
        if (cov.Length > 0)
        {
            var c = TB(cov, 18, false, 0.6);
            c.Margin = new Thickness(0, 18, 0, 0);
            Reveal(c, 650);
            p.Children.Add(c);
        }
        return Wrap(p);
    }

    private FrameworkElement SlideTotal()
    {
        int tok = _token;
        var p = NewPanel();

        var label = TB(L("Vous avez écouté", "You listened to"), 28, false, 0.8);
        Reveal(label, 0);
        var big = TB("", 96, true);
        Reveal(big, 150);
        _ = CountUp(big, _d.TotalSeconds, FormatDuration, 1800, tok, 250);
        var after = TB(L("de musique", "of music"), 28, false, 0.8);
        Reveal(after, 400);
        var stats = TB(string.Format(Culture, L("{0} écoutes  •  {1} titres différents  •  {2} artistes", "{0} plays  •  {1} different tracks  •  {2} artists"),
            _d.TotalPlays.ToString("N0", Culture), _d.DistinctTracks.ToString("N0", Culture), _d.DistinctArtists.ToString("N0", Culture)), 18, false, 0.6);
        stats.Margin = new Thickness(0, 22, 0, 0);
        Reveal(stats, 900);

        p.Children.Add(label);
        p.Children.Add(big);
        p.Children.Add(after);
        p.Children.Add(stats);
        return Wrap(p);
    }

    private FrameworkElement SlideTopTrack()
    {
        int tok = _token;
        var t = _d.TopTracks[0];
        var p = NewPanel();

        var label = TB(L("Votre titre n°1", "Your #1 track"), 28, false, 0.8);
        Reveal(label, 0);
        var cover = CoverTile(t.TrackId, 260, 24);
        Reveal(cover, 200);
        var title = TB(t.Title, 40, true);
        Reveal(title, 450);
        var artist = TB(t.Artist ?? "", 24, false, 0.8);
        Reveal(artist, 600);
        var plays = TB("", 30, true);
        plays.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 180, 255));
        Reveal(plays, 750);
        _ = CountUp(plays, t.Plays, v => Plays((int)Math.Round(v)), 1400, tok, 800);

        p.Children.Add(label);
        p.Children.Add(cover);
        p.Children.Add(title);
        if (!string.IsNullOrEmpty(t.Artist)) p.Children.Add(artist);
        p.Children.Add(plays);
        return Wrap(p);
    }

    private FrameworkElement SlideTopTracks()
    {
        var p = NewPanel();
        p.Spacing = 10;
        var label = TB(L("Vos titres les plus écoutés", "Your most played tracks"), 28, false, 0.8);
        label.Margin = new Thickness(0, 0, 0, 12);
        Reveal(label, 0);
        p.Children.Add(label);

        for (int i = 0; i < _d.TopTracks.Count; i++)
        {
            var t = _d.TopTracks[i];
            var row = new Grid { ColumnSpacing = 16, Height = 64 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rank = TB((i + 1).ToString(CultureInfo.InvariantCulture), 30, true, i == 0 ? 1.0 : 0.6, TextAlignment.Left);
            rank.VerticalAlignment = VerticalAlignment.Center;
            rank.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(rank, 0);

            var cover = CoverTile(t.TrackId, 64, 10);
            Grid.SetColumn(cover, 1);

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = TB(t.Title, 22, true, 1.0, TextAlignment.Left);
            title.HorizontalAlignment = HorizontalAlignment.Left;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            texts.Children.Add(title);
            if (!string.IsNullOrEmpty(t.Artist))
            {
                var artist = TB(t.Artist, 16, false, 0.65, TextAlignment.Left);
                artist.HorizontalAlignment = HorizontalAlignment.Left;
                artist.TextTrimming = TextTrimming.CharacterEllipsis;
                artist.TextWrapping = TextWrapping.NoWrap;
                texts.Children.Add(artist);
            }
            Grid.SetColumn(texts, 2);

            var plays = TB(Plays(t.Plays), 18, false, 0.8, TextAlignment.Right);
            plays.VerticalAlignment = VerticalAlignment.Center;
            plays.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(plays, 3);

            row.Children.Add(rank);
            row.Children.Add(cover);
            row.Children.Add(texts);
            row.Children.Add(plays);
            Reveal(row, 250 + i * 150, 40);
            p.Children.Add(row);
        }
        return Wrap(p);
    }

    private FrameworkElement SlideArtists()
    {
        var p = NewPanel();
        p.Spacing = 18;
        var label = TB(L("Vos artistes favoris", "Your top artists"), 28, false, 0.8);
        label.Margin = new Thickness(0, 0, 0, 10);
        Reveal(label, 0);
        p.Children.Add(label);

        double max = Math.Max(1, _d.TopArtists.Max(a => a.Seconds));
        for (int i = 0; i < _d.TopArtists.Count; i++)
        {
            var a = _d.TopArtists[i];
            var block = new StackPanel { Spacing = 6 };

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = TB(a.Name, i == 0 ? 30 : 24, true, 1.0, TextAlignment.Left);
            name.HorizontalAlignment = HorizontalAlignment.Left;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            var time = TB(FormatDuration(a.Seconds), 18, false, 0.75, TextAlignment.Right);
            time.HorizontalAlignment = HorizontalAlignment.Right;
            time.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(time, 1);
            head.Children.Add(name);
            head.Children.Add(time);

            var track = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = White(36) };
            var fill = new Border
            {
                Height = 10,
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 760 * (a.Seconds / max),
                Background = new SolidColorBrush(i == 0 ? AccentColor : Windows.UI.Color.FromArgb(200, 255, 255, 255))
            };
            track.Child = fill;
            Grow(fill, vertical: false, 350 + i * 150);

            block.Children.Add(head);
            block.Children.Add(track);
            Reveal(block, 250 + i * 150, 30);
            p.Children.Add(block);
        }
        return Wrap(p);
    }

    private FrameworkElement SlideGenres()
    {
        var p = NewPanel();
        var label = TB(L("Votre son", "Your sound"), 28, false, 0.8);
        Reveal(label, 0);
        var top = TB(_d.TopGenres[0].Name, 80, true);
        Reveal(top, 250);
        var caption = TB(L("est votre genre n°1", "is your #1 genre"), 26, false, 0.8);
        Reveal(caption, 500);
        p.Children.Add(label);
        p.Children.Add(top);
        p.Children.Add(caption);

        if (_d.TopGenres.Count > 1)
        {
            var others = TB(L("Suivi de ", "Followed by ") + string.Join(", ", _d.TopGenres.Skip(1).Select(g => g.Name)), 20, false, 0.65);
            others.Margin = new Thickness(0, 20, 0, 0);
            Reveal(others, 800);
            p.Children.Add(others);
        }
        return Wrap(p);
    }

    private FrameworkElement SlideRhythm()
    {
        var p = NewPanel();
        var label = TB(L("Votre rythme", "Your rhythm"), 28, false, 0.8);
        Reveal(label, 0);
        p.Children.Add(label);

        // Graphique : une colonne par mois
        const double maxBar = 170;
        double maxMonth = Math.Max(1, _d.MonthSeconds.Max());
        int best = Array.IndexOf(_d.MonthSeconds, _d.MonthSeconds.Max());
        var chart = new Grid { ColumnSpacing = 10, Height = maxBar + 34, Margin = new Thickness(0, 14, 0, 6) };
        for (int m = 0; m < 12; m++)
        {
            chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            double v = _d.MonthSeconds[m];
            double h = v > 0 ? Math.Max(8, v / maxMonth * maxBar) : 4;

            var col = new Grid();
            col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(maxBar) });
            col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var bar = new Border
            {
                Height = h,
                CornerRadius = new CornerRadius(6),
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(m == best && v > 0 ? AccentColor : Windows.UI.Color.FromArgb(90, 255, 255, 255))
            };
            Grow(bar, vertical: true, 300 + m * 60);

            string name = Culture.DateTimeFormat.GetAbbreviatedMonthName(m + 1).TrimEnd('.');
            var lbl = TB(name.Length > 3 ? name.Substring(0, 3) : name, 13, false, m == best && v > 0 ? 1.0 : 0.6);
            lbl.Margin = new Thickness(0, 8, 0, 0);
            Grid.SetRow(lbl, 1);

            col.Children.Add(bar);
            col.Children.Add(lbl);
            Grid.SetColumn(col, m);
            chart.Children.Add(col);
        }
        Reveal(chart, 150, 10);
        p.Children.Add(chart);

        // Faits marquants
        var facts = new List<string>();
        if (_d.BusiestDay.HasValue)
            facts.Add(string.Format(Culture, L("Votre jour le plus chargé : {0} ({1})", "Your busiest day: {0} ({1})"),
                _d.BusiestDay.Value.ToString("d MMMM", Culture), FormatDuration(_d.BusiestDaySeconds)));
        if (_d.PeakHour.HasValue)
            facts.Add(string.Format(Culture, L("Vous écoutez surtout vers {0} h", "You listen most around {0}:00"), _d.PeakHour.Value));
        if (_d.LongestStreakDays >= 2)
            facts.Add(string.Format(Culture, L("Votre plus longue série : {0} jours d'affilée", "Your longest streak: {0} days in a row"), _d.LongestStreakDays));

        for (int i = 0; i < facts.Count; i++)
        {
            var f = TB(facts[i], 20, false, 0.85);
            Reveal(f, 1100 + i * 250);
            p.Children.Add(f);
        }
        return Wrap(p);
    }

    private FrameworkElement SlideOutro()
    {
        var p = NewPanel();
        var title = TB(L("Merci pour cette année en musique", "Thanks for a year in music"), 38, true);
        Reveal(title, 0);
        p.Children.Add(title);

        string summary = FormatDuration(_d.TotalSeconds) + "  •  " + Plays(_d.TotalPlays);
        if (_d.TopTracks.Count > 0) summary += "\n" + L("Titre n°1 : ", "#1 track: ") + _d.TopTracks[0].Title;
        var sum = TB(summary, 20, false, 0.75);
        Reveal(sum, 300);
        p.Children.Add(sum);

        string cov = CoverageText();
        if (_d.IsPartial && cov.Length > 0)
        {
            var note = TB(L("Ce bilan ne couvre que la période « ", "This review only covers the period \"") + cov.ToLowerInvariant()
                + L(" ». Resona enregistre vos écoutes depuis cette date.", "\". Resona has been recording your listening since that date."), 15, false, 0.5);
            Reveal(note, 450);
            p.Children.Add(note);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
        var export = new Button
        {
            Content = L("Exporter en image", "Export as image"),
            Padding = new Thickness(22, 10, 22, 10)
        };
        try { export.Style = (Style)Application.Current.Resources["AccentButtonStyle"]; } catch { /* style par defaut */ }
        export.Click += ExportClick;
        var close = new Button { Content = L("Fermer", "Close"), Padding = new Thickness(22, 10, 22, 10) };
        close.Click += (_, _) => CloseAnimated();
        buttons.Children.Add(export);
        buttons.Children.Add(close);
        Reveal(buttons, 600);
        p.Children.Add(buttons);

        _exportStatus = TB("", 14, false, 0.7);
        p.Children.Add(_exportStatus);
        return Wrap(p);
    }

    // ------------------------------------------------------------------ export

    private async void ExportClick(object sender, RoutedEventArgs e)
    {
        var status = _exportStatus;
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = $"Resona-{_d.Year}-in-review"
            };
            picker.FileTypeChoices.Add("PNG", new List<string> { ".png" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            bool fr = Strings.Current.IsFr;
            byte[] bytes = await Task.Run(() =>
            {
                using var exporter = new YearInReviewExporter(_d, _covers, fr);
                return exporter.RenderPng();
            });
            await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);

            if (status != null) status.Text = L("Image enregistrée ✓", "Image saved ✓");
        }
        catch
        {
            if (status != null) status.Text = L("Impossible d'enregistrer l'image.", "Couldn't save the image.");
        }
    }
}
