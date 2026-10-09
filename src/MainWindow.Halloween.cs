// ============================================================
//  MainWindow.Halloween.cs  —  Resona
//  Theme Halloween (4e fond d'application) : decorations vectorielles
//  (toiles, araignees, citrouilles, chauves-souris) et animation d'activation.
//
//  - Tout est dessine en vecteurs XAML construits par code : aucun fichier image a ajouter.
//  - Les couches (HalloweenLayer / HalloweenIntroLayer, dans MainWindow.xaml) ont
//    IsHitTestVisible="False" : elles ne bloquent jamais un clic.
//  - UpdateHalloweenDecorations() est appelee par ApplyBackdrop() et par les bascules du mini lecteur.
//  - L'animation d'activation ne se joue QUE quand l'utilisateur choisit Halloween (pas a chaque lancement),
//    et pas du tout si les animations sont reduites dans Windows.
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Resona.Models;
using Windows.Foundation;
using Windows.UI;

namespace Resona;

public sealed partial class MainWindow
{
    private bool _halloweenInit;        // premier passage effectue (pas d'animation au lancement de l'app)
    private bool _halloweenWasActive;
    private bool _halloweenBuilt;
    private bool _halloweenSizeHooked;
    private Storyboard? _halloweenIdle;
    private Storyboard? _halloweenBats;
    private Storyboard? _halloweenIntro;
    private readonly List<RotateTransform> _halloweenSpiderRots = new();
    private readonly List<UIElement> _halloweenFlickers = new();
    private readonly List<(TranslateTransform Move, ScaleTransform Flap)> _halloweenBatTransforms = new();

    // ------------------------------------------------------------------
    //  Point d'entree
    // ------------------------------------------------------------------

    private void UpdateHalloweenDecorations()
    {
        try
        {
            if (HalloweenLayer == null || HalloweenIntroLayer == null) return;

            bool active = App.Settings.Current.Backdrop == AppBackdropStyle.Halloween;
            bool show = active && !_isMiniPlayerMode;

            if (show)
            {
                if (!_halloweenBuilt) BuildHalloweenDecorations();
                if (!_halloweenSizeHooked)
                {
                    _halloweenSizeHooked = true;
                    HalloweenLayer.SizeChanged += (s, e) =>
                    {
                        if (HalloweenLayer.Visibility == Visibility.Visible) StartHalloweenBats();
                    };
                }
                HalloweenLayer.Visibility = Visibility.Visible;
                StartHalloweenAnimations();
            }
            else
            {
                StopHalloweenAnimations();
                HalloweenLayer.Visibility = Visibility.Collapsed;
            }

            if (!active) StopHalloweenIntro();
            else if (!_halloweenWasActive && _halloweenInit && !_isMiniPlayerMode) PlayHalloweenIntro();

            _halloweenWasActive = active;
        }
        catch
        {
            // Decoration purement cosmetique : ne jamais faire planter l'app pour ca.
        }
        finally
        {
            _halloweenInit = true;
        }
    }

    private static bool HalloweenAnimationsAllowed()
    {
        try { return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled; }
        catch { return true; }
    }

    // ------------------------------------------------------------------
    //  Petits utilitaires de dessin
    // ------------------------------------------------------------------

    private static string F(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    private static SolidColorBrush Br(string hex, double opacity = 1.0)
        => new SolidColorBrush(ParseHexColor(hex)) { Opacity = opacity };

    private static Microsoft.UI.Xaml.Shapes.Path MakePath(string data, Microsoft.UI.Xaml.Media.Brush? fill, Microsoft.UI.Xaml.Media.Brush? stroke, double thickness = 1.0)
    {
        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
    }

    // Silhouette de chauve-souris, 52 x 16, coordonnees toutes positives (un Path n'affiche pas les negatives).
    private const string BatData =
        "M26,8 C22,2 18,0 14,2 C12,4 8,0 0,2 C4,6 4,10 8,12 C12,10 16,12 18,14 C20,10 23,10 26,14 " +
        "C29,10 32,10 34,14 C36,12 40,10 44,12 C48,10 48,6 52,2 C44,0 40,4 38,2 C34,0 30,2 26,8 Z";

    private static void KeyAnim(Storyboard sb, DependencyObject target, string property, params (double Seconds, double Value)[] keys)
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        foreach (var (seconds, value) in keys)
        {
            anim.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds)),
                Value = value
            });
        }
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);
        sb.Children.Add(anim);
    }

    // Toile d'araignee dans un coin : 4 rayons (0/30/60/90 deg) + 4 anneaux legerement affaisses.
    private static string WebData(double size)
    {
        var sb = new StringBuilder();
        double[] angles = { 0, 30, 60, 90 };
        foreach (double a in angles)
        {
            double r = a * Math.PI / 180.0;
            sb.Append($"M0,0 L{F(size * Math.Cos(r))},{F(size * Math.Sin(r))} ");
        }
        foreach (double k in new[] { 0.3, 0.55, 0.8, 1.0 })
        {
            double radius = size * k;
            for (int i = 0; i < 3; i++)
            {
                double a1 = angles[i] * Math.PI / 180.0;
                double a2 = angles[i + 1] * Math.PI / 180.0;
                double am = (a1 + a2) / 2.0;
                double x1 = radius * Math.Cos(a1), y1 = radius * Math.Sin(a1);
                double x2 = radius * Math.Cos(a2), y2 = radius * Math.Sin(a2);
                double cx = radius * 0.82 * Math.Cos(am), cy = radius * 0.82 * Math.Sin(am);
                sb.Append($"M{F(x1)},{F(y1)} Q{F(cx)},{F(cy)} {F(x2)},{F(y2)} ");
            }
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    //  Construction des decorations permanentes
    // ------------------------------------------------------------------

    private (FrameworkElement Element, RotateTransform Rotation) MakeSpider(double thread)
    {
        var bodyFill = Br("#2B1840");
        var edge = Br("#9B4DFF");
        var threadBrush = Br("#D8C8FF", 0.6);

        var canvas = new Canvas { Width = 40, Height = thread + 36 };
        canvas.Children.Add(new Line { X1 = 20, Y1 = 0, X2 = 20, Y2 = thread + 4, Stroke = threadBrush, StrokeThickness = 1 });

        double y0 = thread + 10;
        var legs = new StringBuilder();
        foreach (int s in new[] { -1, 1 })
        {
            legs.Append($"M20,{F(y0)} L{F(20 + s * 10)},{F(y0 - 10)} L{F(20 + s * 17)},{F(y0 - 2)} ");
            legs.Append($"M20,{F(y0 + 2)} L{F(20 + s * 13)},{F(y0 - 4)} L{F(20 + s * 19)},{F(y0 + 6)} ");
            legs.Append($"M20,{F(y0 + 4)} L{F(20 + s * 13)},{F(y0 + 4)} L{F(20 + s * 18)},{F(y0 + 14)} ");
            legs.Append($"M20,{F(y0 + 6)} L{F(20 + s * 10)},{F(y0 + 12)} L{F(20 + s * 14)},{F(y0 + 20)} ");
        }
        canvas.Children.Add(MakePath(legs.ToString(), null, edge, 1.2));

        var abdomen = new Ellipse { Width = 16, Height = 18, Fill = bodyFill, Stroke = edge, StrokeThickness = 1 };
        Canvas.SetLeft(abdomen, 12); Canvas.SetTop(abdomen, y0 + 5);
        canvas.Children.Add(abdomen);

        var head = new Ellipse { Width = 10, Height = 10, Fill = bodyFill, Stroke = edge, StrokeThickness = 1 };
        Canvas.SetLeft(head, 15); Canvas.SetTop(head, y0 - 3);
        canvas.Children.Add(head);

        foreach (double ex in new[] { 17.0, 20.5 })
        {
            var eye = new Ellipse { Width = 2.5, Height = 2.5, Fill = Br("#FF3B3B") };
            Canvas.SetLeft(eye, ex); Canvas.SetTop(eye, y0 - 1);
            canvas.Children.Add(eye);
        }

        var rot = new RotateTransform();
        canvas.RenderTransformOrigin = new Point(0.5, 0.0);   // le fil est fixe en haut
        canvas.RenderTransform = rot;
        return (canvas, rot);
    }

    private (FrameworkElement Element, UIElement Face) MakePumpkin(double size)
    {
        var orange = Br("#FF7A1A");
        var edge = Br("#A63F00");
        var canvas = new Canvas { Width = 56, Height = 52 };

        Ellipse Lobe(double l, double t, double w, double h)
        {
            var e = new Ellipse { Width = w, Height = h, Fill = orange, Stroke = edge, StrokeThickness = 1.2 };
            Canvas.SetLeft(e, l); Canvas.SetTop(e, t);
            return e;
        }
        canvas.Children.Add(Lobe(2, 10, 26, 40));
        canvas.Children.Add(Lobe(28, 10, 26, 40));
        canvas.Children.Add(Lobe(14, 6, 28, 44));
        canvas.Children.Add(MakePath("M28,8 L26,0 L32,0 L31,8 Z", Br("#4A7A2A"), Br("#2E4A19"), 1));

        // Visage lumineux (c'est lui qui "vacille").
        var glow = Br("#FFD15C");
        var face = new Canvas { Width = 56, Height = 52 };
        face.Children.Add(MakePath("M17,26 L23,26 L20,19 Z", glow, null));
        face.Children.Add(MakePath("M33,26 L39,26 L36,19 Z", glow, null));
        face.Children.Add(MakePath("M28,30 L26,34 L30,34 Z", glow, null));
        face.Children.Add(MakePath("M17,38 L21,41 L25,38 L28,42 L31,38 L35,41 L39,38 L37,45 L19,45 Z", glow, null));
        canvas.Children.Add(face);

        var view = new Viewbox { Width = size, Height = size * 52.0 / 56.0, Child = canvas };
        return (view, face);
    }

    private Microsoft.UI.Xaml.Shapes.Path MakeBat(double scale, out TranslateTransform move, out ScaleTransform flap)
    {
        var bat = MakePath(BatData, Br("#3A2060"), Br("#9B4DFF"), 0.8);
        flap = new ScaleTransform { ScaleX = scale, ScaleY = scale };
        move = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(flap);
        group.Children.Add(move);
        bat.RenderTransformOrigin = new Point(0.5, 0.5);
        bat.RenderTransform = group;
        bat.HorizontalAlignment = HorizontalAlignment.Left;
        bat.VerticalAlignment = VerticalAlignment.Top;
        return bat;
    }

    private void BuildHalloweenDecorations()
    {
        var layer = HalloweenLayer;
        layer.Children.Clear();
        _halloweenSpiderRots.Clear();
        _halloweenFlickers.Clear();
        _halloweenBatTransforms.Clear();

        // --- Toiles dans les deux coins du haut (traits fins, tres transparents) ---
        var webBrush = Br("#D8C8FF", 0.38);
        var webLeft = MakePath(WebData(150), null, webBrush, 1);
        webLeft.HorizontalAlignment = HorizontalAlignment.Left;
        webLeft.VerticalAlignment = VerticalAlignment.Top;
        layer.Children.Add(webLeft);

        var webRight = MakePath(WebData(130), null, webBrush, 1);
        webRight.HorizontalAlignment = HorizontalAlignment.Right;
        webRight.VerticalAlignment = VerticalAlignment.Top;
        webRight.RenderTransformOrigin = new Point(0.5, 0.5);
        webRight.RenderTransform = new ScaleTransform { ScaleX = -1 };   // miroir horizontal
        layer.Children.Add(webRight);

        // --- Deux araignees suspendues a un fil, a droite (loin des titres de pages, a gauche des boutons de fenetre) ---
        var (spider1, rot1) = MakeSpider(55);
        spider1.HorizontalAlignment = HorizontalAlignment.Right;
        spider1.VerticalAlignment = VerticalAlignment.Top;
        spider1.Margin = new Thickness(0, 0, 190, 0);
        layer.Children.Add(spider1);
        _halloweenSpiderRots.Add(rot1);

        var (spider2, rot2) = MakeSpider(95);
        spider2.HorizontalAlignment = HorizontalAlignment.Right;
        spider2.VerticalAlignment = VerticalAlignment.Top;
        spider2.Margin = new Thickness(0, 0, 330, 0);
        layer.Children.Add(spider2);
        _halloweenSpiderRots.Add(rot2);

        // --- Citrouilles posees au-dessus de la barre de lecture, cote droit (le cote gauche est occupe par la navigation) ---
        var (pumpkinBig, faceBig) = MakePumpkin(54);
        pumpkinBig.HorizontalAlignment = HorizontalAlignment.Right;
        pumpkinBig.VerticalAlignment = VerticalAlignment.Bottom;
        pumpkinBig.Margin = new Thickness(0, 0, 30, 94);
        layer.Children.Add(pumpkinBig);
        _halloweenFlickers.Add(faceBig);

        var (pumpkinSmall, faceSmall) = MakePumpkin(38);
        pumpkinSmall.HorizontalAlignment = HorizontalAlignment.Right;
        pumpkinSmall.VerticalAlignment = VerticalAlignment.Bottom;
        pumpkinSmall.Margin = new Thickness(0, 0, 92, 94);
        layer.Children.Add(pumpkinSmall);
        _halloweenFlickers.Add(faceSmall);

        // --- Trois chauves-souris qui traversent lentement le haut de la fenetre ---
        double[] tops = { 52, 96, 74 };
        double[] scales = { 0.8, 0.6, 0.7 };
        for (int i = 0; i < 3; i++)
        {
            var bat = MakeBat(scales[i], out var move, out var flap);
            bat.Margin = new Thickness(0, tops[i], 0, 0);
            move.X = -100;   // hors ecran tant que l'animation ne tourne pas
            layer.Children.Add(bat);
            _halloweenBatTransforms.Add((move, flap));
        }

        // --- Animations "au repos" : balancement des araignees + vacillement des citrouilles ---
        var idle = new Storyboard();
        double[] swingSeconds = { 2.3, 2.9 };
        for (int i = 0; i < _halloweenSpiderRots.Count; i++)
        {
            var swing = new DoubleAnimation
            {
                From = -6, To = 6,
                Duration = TimeSpan.FromSeconds(swingSeconds[i % swingSeconds.Length]),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(swing, _halloweenSpiderRots[i]);
            Storyboard.SetTargetProperty(swing, "Angle");
            idle.Children.Add(swing);
        }
        for (int i = 0; i < _halloweenFlickers.Count; i++)
        {
            var flicker = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            double stretch = 1.0 + i * 0.35;
            (double t, double v)[] frames = { (0.0, 1.0), (0.18, 0.72), (0.34, 0.96), (0.62, 0.78), (0.95, 1.0) };
            foreach (var (t, v) in frames)
            {
                flicker.KeyFrames.Add(new LinearDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t * stretch)),
                    Value = v
                });
            }
            Storyboard.SetTarget(flicker, _halloweenFlickers[i]);
            Storyboard.SetTargetProperty(flicker, "Opacity");
            idle.Children.Add(flicker);
        }
        _halloweenIdle = idle;
        _halloweenBuilt = true;
    }

    // ------------------------------------------------------------------
    //  Animations permanentes (balancement, vacillement, chauves-souris)
    // ------------------------------------------------------------------

    private void StartHalloweenAnimations()
    {
        if (!HalloweenAnimationsAllowed())
        {
            // Animations reduites dans Windows : decorations fixes.
            StopHalloweenAnimations();
            return;
        }
        _halloweenIdle?.Begin();
        StartHalloweenBats();
    }

    private void StopHalloweenAnimations()
    {
        try { _halloweenIdle?.Stop(); } catch { }
        try { _halloweenBats?.Stop(); } catch { }
    }

    private void StartHalloweenBats()
    {
        if (!_halloweenBuilt || !HalloweenAnimationsAllowed()) return;
        try { _halloweenBats?.Stop(); } catch { }

        // Distance a parcourir = largeur courante de la fenetre (rappelee quand la fenetre est redimensionnee).
        double width = Math.Max(HalloweenLayer.ActualWidth, 800) + 200;
        var sb = new Storyboard();
        for (int i = 0; i < _halloweenBatTransforms.Count; i++)
        {
            var (move, flap) = _halloweenBatTransforms[i];
            double baseScale = flap.ScaleX;

            var cross = new DoubleAnimation
            {
                From = -100, To = width,
                Duration = TimeSpan.FromSeconds(20 + i * 6),
                BeginTime = TimeSpan.FromSeconds(i * 5),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(cross, move);
            Storyboard.SetTargetProperty(cross, "X");
            sb.Children.Add(cross);

            var bob = new DoubleAnimation
            {
                From = 0, To = 18,
                Duration = TimeSpan.FromSeconds(1.6 + i * 0.4),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(bob, move);
            Storyboard.SetTargetProperty(bob, "Y");
            sb.Children.Add(bob);

            var beat = new DoubleAnimation
            {
                From = baseScale, To = baseScale * 0.5,
                Duration = TimeSpan.FromSeconds(0.22),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(beat, flap);
            Storyboard.SetTargetProperty(beat, "ScaleY");
            sb.Children.Add(beat);
        }
        _halloweenBats = sb;
        sb.Begin();
    }

    // ------------------------------------------------------------------
    //  Animation d'activation (~3 s) : flash orange/violet, brume qui monte,
    //  volee de chauves-souris, puis "Happy Halloween" qui apparait et s'efface.
    // ------------------------------------------------------------------

    private void StopHalloweenIntro()
    {
        try { _halloweenIntro?.Stop(); } catch { }
        _halloweenIntro = null;
        if (HalloweenIntroLayer != null)
        {
            HalloweenIntroLayer.Visibility = Visibility.Collapsed;
            HalloweenIntroLayer.Children.Clear();
        }
    }

    private void PlayHalloweenIntro()
    {
        if (!HalloweenAnimationsAllowed()) return;
        StopHalloweenIntro();

        var layer = HalloweenIntroLayer;
        double w = Math.Max(RootGrid.ActualWidth, 800);
        double h = Math.Max(RootGrid.ActualHeight, 600);
        var sb = new Storyboard();
        var orange = ParseHexColor("#FF7A1A");
        var violet = ParseHexColor("#9B4DFF");

        // 1) Flash orange -> violet (deux eclairs rapides)
        var flash = new Grid
        {
            Opacity = 0,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = orange, Offset = 0.0 },
                    new GradientStop { Color = violet, Offset = 1.0 }
                }
            }
        };
        layer.Children.Add(flash);
        KeyAnim(sb, flash, "Opacity", (0.0, 0.0), (0.12, 0.55), (0.40, 0.0), (0.55, 0.35), (0.95, 0.0));

        // 2) Brume qui monte depuis le bas
        var fogMove = new TranslateTransform { Y = h * 0.6 };
        var fog = new Border
        {
            Height = h * 0.6,
            VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = 0,
            RenderTransform = fogMove,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0, 155, 77, 255), Offset = 0.0 },
                    new GradientStop { Color = Color.FromArgb(150, 155, 77, 255), Offset = 1.0 }
                }
            }
        };
        layer.Children.Add(fog);
        KeyAnim(sb, fogMove, "Y", (0.2, h * 0.6), (1.6, 0.0));
        KeyAnim(sb, fog, "Opacity", (0.2, 0.0), (1.0, 0.9), (2.2, 0.9), (3.0, 0.0));

        // 3) Volee de chauves-souris : traversent la fenetre en montant legerement
        var rnd = new Random(31);
        for (int i = 0; i < 9; i++)
        {
            double scale = 1.1 + rnd.NextDouble() * 0.9;
            var bat = MakeBat(scale, out var move, out var flap);
            bat.Margin = new Thickness(0, h * (0.2 + 0.65 * rnd.NextDouble()), 0, 0);
            move.X = -120;
            layer.Children.Add(bat);

            double begin = i * 0.18;
            double dur = 1.7 + rnd.NextDouble() * 0.8;
            double rise = -(80 + rnd.NextDouble() * 120);
            KeyAnim(sb, move, "X", (0.0, -120), (begin, -120), (begin + dur, w + 120));
            KeyAnim(sb, move, "Y", (0.0, 0.0), (begin, 0.0), (begin + dur, rise));

            var beat = new DoubleAnimation
            {
                From = scale, To = scale * 0.45,
                Duration = TimeSpan.FromSeconds(0.14),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(TimeSpan.FromSeconds(3.0))
            };
            Storyboard.SetTarget(beat, flap);
            Storyboard.SetTargetProperty(beat, "ScaleY");
            sb.Children.Add(beat);
        }

        // 4) "Happy Halloween" : apparait (1.3 s -> 1.9 s), reste, puis s'efface (2.5 s -> 3.0 s)
        string message = Resona.Models.Strings.Current.IsFr ? "Joyeux Halloween" : "Happy Halloween";
        var title = new Grid
        {
            Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var titleScale = new CompositeTransform { ScaleX = 0.7, ScaleY = 0.7 };
        title.RenderTransform = titleScale;
        title.Children.Add(new TextBlock
        {
            Text = message, FontSize = 64, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 120, Foreground = new SolidColorBrush(violet),
            Margin = new Thickness(4, 4, 0, 0), Opacity = 0.8
        });
        title.Children.Add(new TextBlock
        {
            Text = message, FontSize = 64, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 120, Foreground = new SolidColorBrush(orange)
        });
        layer.Children.Add(title);
        KeyAnim(sb, title, "Opacity", (0.0, 0.0), (1.3, 0.0), (1.9, 1.0), (2.5, 1.0), (3.0, 0.0));
        KeyAnim(sb, titleScale, "ScaleX", (0.0, 0.7), (1.3, 0.7), (1.9, 1.0), (3.0, 1.06));
        KeyAnim(sb, titleScale, "ScaleY", (0.0, 0.7), (1.3, 0.7), (1.9, 1.0), (3.0, 1.06));

        sb.Completed += (s, e) =>
        {
            if (ReferenceEquals(_halloweenIntro, sb)) StopHalloweenIntro();
        };
        _halloweenIntro = sb;
        layer.Visibility = Visibility.Visible;
        sb.Begin();
    }
}
