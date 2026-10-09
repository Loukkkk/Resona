// ============================================================
//  MainWindow.Christmas.cs  —  Resona
//  Theme Noel (5e fond d'application) : decorations vectorielles animees
//  (guirlande lumineuse, neige, sapin, cadeaux, bonhomme de neige, boules suspendues,
//  etoiles scintillantes, traineau du Pere Noel) et animation d'activation.
//
//  - Meme principe que MainWindow.Halloween.cs : tout est dessine en vecteurs XAML construits par code,
//    aucun fichier image a ajouter.
//  - Les couches (ChristmasLayer / ChristmasIntroLayer, dans MainWindow.xaml) ont IsHitTestVisible="False" :
//    elles ne bloquent jamais un clic.
//  - UpdateChristmasDecorations() est appelee par ApplyBackdrop() et par les bascules du mini lecteur.
//  - L'animation d'activation ne se joue QUE quand l'utilisateur choisit Noel (pas a chaque lancement),
//    et pas du tout si les animations sont reduites dans Windows (decors fixes dans ce cas).
//  - Le theme n'est propose qu'en decembre (voir SeasonalThemes dans Models.cs).
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Resona.Models;
using Windows.Foundation;
using Windows.UI;

namespace Resona;

public sealed partial class MainWindow
{
    private sealed record ChristmasFlake(Ellipse Element, TranslateTransform Move, double XFrac, double YFrac,
                                         double Seconds, double SwayAmp, double SwaySeconds);

    private sealed record ChristmasSparkle(FrameworkElement Element, double XFrac, double YFrac);

    private bool _christmasInit;        // premier passage effectue (pas d'animation au lancement de l'app)
    private bool _christmasWasActive;
    private bool _christmasBuilt;
    private bool _christmasSizeHooked;
    private Storyboard? _christmasIdle;
    private Storyboard? _christmasSnow;
    private Storyboard? _christmasSleighSb;
    private Storyboard? _christmasGarlandSb;
    private Storyboard? _christmasIntro;
    private Canvas? _christmasGarland;
    private DispatcherTimer? _christmasResizeTimer;
    private TranslateTransform? _christmasSleighMove;
    private RotateTransform? _christmasSleighTilt;
    private RotateTransform? _christmasSnowmanRot;
    private readonly List<RotateTransform> _christmasSwings = new();
    private readonly List<UIElement> _christmasTwinkles = new();
    private readonly List<ChristmasFlake> _christmasFlakes = new();
    private readonly List<ChristmasSparkle> _christmasSparkles = new();

    // ------------------------------------------------------------------
    //  Point d'entree
    // ------------------------------------------------------------------

    private void UpdateChristmasDecorations()
    {
        try
        {
            if (ChristmasLayer == null || ChristmasIntroLayer == null) return;

            bool active = App.Settings.Current.Backdrop == AppBackdropStyle.Christmas;
            bool show = active && !_isMiniPlayerMode;

            if (show)
            {
                if (!_christmasBuilt) BuildChristmasDecorations();
                if (!_christmasSizeHooked)
                {
                    _christmasSizeHooked = true;
                    ChristmasLayer.SizeChanged += (s, e) => ScheduleChristmasRelayout();
                }
                ChristmasLayer.Visibility = Visibility.Visible;
                StartChristmasAnimations();
            }
            else
            {
                StopChristmasAnimations();
                ChristmasLayer.Visibility = Visibility.Collapsed;
            }

            if (!active) StopChristmasIntro();
            else if (!_christmasWasActive && _christmasInit && !_isMiniPlayerMode) PlayChristmasIntro();

            _christmasWasActive = active;
        }
        catch
        {
            // Decoration purement cosmetique : ne jamais faire planter l'app pour ca.
        }
        finally
        {
            _christmasInit = true;
        }
    }

    // La fenetre change de taille en continu pendant un redimensionnement : on attend un court instant
    // avant de recalculer la guirlande, la neige et le traineau.
    private void ScheduleChristmasRelayout()
    {
        if (_christmasResizeTimer == null)
        {
            _christmasResizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _christmasResizeTimer.Tick += (s, e) =>
            {
                _christmasResizeTimer!.Stop();
                RelayoutChristmas();
            };
        }
        _christmasResizeTimer.Stop();
        _christmasResizeTimer.Start();
    }

    private void RelayoutChristmas()
    {
        try
        {
            if (!_christmasBuilt || ChristmasLayer.Visibility != Visibility.Visible) return;
            BuildChristmasGarland();
            ApplyChristmasScatterLayout();
            if (HalloweenAnimationsAllowed())
            {
                _christmasGarlandSb?.Begin();
                StartChristmasSnow();
                StartChristmasSleigh();
            }
        }
        catch { }
    }

    // ------------------------------------------------------------------
    //  Petits utilitaires de dessin
    // ------------------------------------------------------------------

    private static LinearDoubleKeyFrame LinKey(double seconds, double value) => new LinearDoubleKeyFrame
    {
        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds)),
        Value = value
    };

    // Scintillement : l'opacite descend a "low" puis remonte, en boucle.
    private static void AddTwinkle(Storyboard sb, UIElement target, double period, double offsetSeconds, double low)
    {
        var anim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(offsetSeconds)
        };
        anim.KeyFrames.Add(LinKey(0, 1.0));
        anim.KeyFrames.Add(LinKey(period * 0.5, low));
        anim.KeyFrames.Add(LinKey(period, 1.0));
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
    }

    private static string StarData(double cx, double cy, double outer, double inner)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 10; i++)
        {
            double r = i % 2 == 0 ? outer : inner;
            double a = -Math.PI / 2 + i * Math.PI / 5;
            sb.Append(i == 0 ? "M" : " L");
            sb.Append($"{F(cx + r * Math.Cos(a))},{F(cy + r * Math.Sin(a))}");
        }
        sb.Append(" Z");
        return sb.ToString();
    }

    // Etincelle a 4 branches (12 x 12).
    private const string SparkleData = "M6,0 L7.6,4.4 L12,6 L7.6,7.6 L6,12 L4.4,7.6 L0,6 L4.4,4.4 Z";

    private static Microsoft.UI.Xaml.Shapes.Path MakeSparkle(double size, string hex)
    {
        var p = MakePath(SparkleData, Br(hex), null);
        p.Stretch = Stretch.Uniform;
        p.Width = size;
        p.Height = size;
        p.HorizontalAlignment = HorizontalAlignment.Left;
        p.VerticalAlignment = VerticalAlignment.Top;
        return p;
    }

    // ------------------------------------------------------------------
    //  Elements de decor
    // ------------------------------------------------------------------

    // Ampoule de guirlande (18 x 22) : halo + culot + ampoule.
    private static Canvas MakeBulb(string hex)
    {
        var c = new Canvas { Width = 18, Height = 22 };
        var glow = new Ellipse { Width = 18, Height = 18, Fill = Br(hex, 0.28) };
        Canvas.SetLeft(glow, 0); Canvas.SetTop(glow, 3);
        c.Children.Add(glow);
        var cap = new Rectangle { Width = 4, Height = 4, Fill = Br("#4A4A4A") };
        Canvas.SetLeft(cap, 7); Canvas.SetTop(cap, 0);
        c.Children.Add(cap);
        var bulb = new Ellipse { Width = 8, Height = 12, Fill = Br(hex) };
        Canvas.SetLeft(bulb, 5); Canvas.SetTop(bulb, 3);
        c.Children.Add(bulb);
        return c;
    }

    // Boule de Noel suspendue a un fil (le fil est fixe en haut, la boule se balance).
    private (FrameworkElement Element, RotateTransform Rotation) MakeOrnament(double thread, string fillHex, string edgeHex)
    {
        var canvas = new Canvas { Width = 30, Height = thread + 34 };
        canvas.Children.Add(new Line { X1 = 15, Y1 = 0, X2 = 15, Y2 = thread + 6, Stroke = Br("#CFE8FF", 0.6), StrokeThickness = 1 });

        var cap = new Rectangle { Width = 8, Height = 5, Fill = Br("#FFD24C"), RadiusX = 1, RadiusY = 1 };
        Canvas.SetLeft(cap, 11); Canvas.SetTop(cap, thread + 4);
        canvas.Children.Add(cap);

        var ball = new Ellipse { Width = 24, Height = 24, Fill = Br(fillHex), Stroke = Br(edgeHex), StrokeThickness = 1 };
        Canvas.SetLeft(ball, 3); Canvas.SetTop(ball, thread + 8);
        canvas.Children.Add(ball);

        var shine = new Ellipse { Width = 6, Height = 9, Fill = Br("#FFFFFF", 0.5) };
        Canvas.SetLeft(shine, 8); Canvas.SetTop(shine, thread + 12);
        canvas.Children.Add(shine);

        var rot = new RotateTransform();
        canvas.RenderTransformOrigin = new Point(0.5, 0.0);
        canvas.RenderTransform = rot;
        return (canvas, rot);
    }

    // Sapin (64 x 92) : tronc, branches, petites lumieres et etoile.
    private (FrameworkElement Element, List<UIElement> Lights, UIElement Star) MakeChristmasTree(double size)
    {
        var canvas = new Canvas { Width = 64, Height = 92 };

        var trunk = new Rectangle { Width = 10, Height = 8, Fill = Br("#6B3F1D") };
        Canvas.SetLeft(trunk, 27); Canvas.SetTop(trunk, 82);
        canvas.Children.Add(trunk);

        canvas.Children.Add(MakePath(
            "M32,14 L47,38 L40,38 L52,60 L43,60 L57,82 L7,82 L21,60 L12,60 L24,38 L17,38 Z",
            Br("#1F9150"), Br("#0E5A30"), 1.2));

        var lights = new List<UIElement>();
        string[] colors = { "#FF4D5E", "#FFD24C", "#4DA3FF", "#FFFFFF" };
        (double x, double y)[] spots =
        {
            (29, 30), (35, 44), (22, 54), (40, 62), (30, 68), (17, 76), (46, 76), (31, 56)
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var dot = new Ellipse { Width = 5, Height = 5, Fill = Br(colors[i % colors.Length]) };
            Canvas.SetLeft(dot, spots[i].x); Canvas.SetTop(dot, spots[i].y);
            canvas.Children.Add(dot);
            lights.Add(dot);
        }

        var star = MakePath(StarData(32, 10, 9, 4), Br("#FFD24C"), Br("#E0A800"), 1);
        canvas.Children.Add(star);

        var view = new Viewbox { Width = size, Height = size * 92.0 / 64.0, Child = canvas };
        return (view, lights, star);
    }

    // Cadeau (36 x 34) : boite, couvercle, ruban et noeud.
    private static FrameworkElement MakeGift(double size, string boxHex, string ribbonHex)
    {
        var canvas = new Canvas { Width = 36, Height = 34 };

        var box = new Rectangle { Width = 32, Height = 22, Fill = Br(boxHex), Stroke = Br("#000000", 0.25), StrokeThickness = 1 };
        Canvas.SetLeft(box, 2); Canvas.SetTop(box, 11);
        canvas.Children.Add(box);

        var lid = new Rectangle { Width = 36, Height = 7, Fill = Br(boxHex), Stroke = Br("#000000", 0.25), StrokeThickness = 1 };
        Canvas.SetLeft(lid, 0); Canvas.SetTop(lid, 6);
        canvas.Children.Add(lid);

        var ribbon = new Rectangle { Width = 6, Height = 27, Fill = Br(ribbonHex) };
        Canvas.SetLeft(ribbon, 15); Canvas.SetTop(ribbon, 6);
        canvas.Children.Add(ribbon);

        foreach (double bx in new[] { 9.0, 17.0 })
        {
            var loop = new Ellipse { Width = 10, Height = 8, Stroke = Br(ribbonHex), StrokeThickness = 2 };
            Canvas.SetLeft(loop, bx); Canvas.SetTop(loop, 0);
            canvas.Children.Add(loop);
        }

        return new Viewbox { Width = size, Height = size * 34.0 / 36.0, Child = canvas };
    }

    // Bonhomme de neige (56 x 76) : se balance legerement sur place.
    private (FrameworkElement Element, RotateTransform Rotation) MakeSnowman(double size)
    {
        var snow = Br("#F4F8FF");
        var edge = Br("#9DB4D0");
        var canvas = new Canvas { Width = 56, Height = 76 };

        Ellipse Ball(double l, double t, double d)
        {
            var e = new Ellipse { Width = d, Height = d, Fill = snow, Stroke = edge, StrokeThickness = 1 };
            Canvas.SetLeft(e, l); Canvas.SetTop(e, t);
            return e;
        }
        Rectangle Block(double l, double t, double w, double h, string hex, double radius = 0)
        {
            var r = new Rectangle { Width = w, Height = h, Fill = Br(hex), RadiusX = radius, RadiusY = radius };
            Canvas.SetLeft(r, l); Canvas.SetTop(r, t);
            return r;
        }

        // Bras (branches) derriere le corps
        canvas.Children.Add(MakePath("M17,38 L5,30 M5,30 L3,26 M5,30 L2,31 M39,38 L51,30 M51,30 L53,26 M51,30 L54,31", null, Br("#6B3F1D"), 2));

        canvas.Children.Add(Ball(13, 44, 30));
        canvas.Children.Add(Ball(17, 28, 22));
        canvas.Children.Add(Ball(20, 14, 16));

        // Chapeau
        canvas.Children.Add(Block(18, 12, 20, 3, "#222222", 1));
        canvas.Children.Add(Block(21, 0, 14, 13, "#222222", 1));
        canvas.Children.Add(Block(21, 8, 14, 3, "#C1272D"));

        // Visage
        foreach (double ex in new[] { 24.0, 30.0 })
        {
            var eye = new Ellipse { Width = 2.4, Height = 2.4, Fill = Br("#1A1A1A") };
            Canvas.SetLeft(eye, ex); Canvas.SetTop(eye, 19);
            canvas.Children.Add(eye);
        }
        canvas.Children.Add(MakePath("M28,22 L37,24 L28,26 Z", Br("#FF8A1F"), null));

        // Echarpe
        canvas.Children.Add(Block(17, 29, 22, 5, "#D62839", 2));
        canvas.Children.Add(Block(32, 32, 5, 12, "#D62839", 2));

        // Boutons
        foreach (double by in new[] { 37.0, 43.0, 53.0 })
        {
            var b = new Ellipse { Width = 3, Height = 3, Fill = Br("#2A2A2A") };
            Canvas.SetLeft(b, 26.5); Canvas.SetTop(b, by);
            canvas.Children.Add(b);
        }

        var view = new Viewbox { Width = size, Height = size * 76.0 / 56.0, Child = canvas };
        var rot = new RotateTransform();
        view.RenderTransformOrigin = new Point(0.5, 1.0);
        view.RenderTransform = rot;
        return (view, rot);
    }

    private static void AddReindeer(Canvas c, double x, double y, bool lead)
    {
        var fur = Br("#8B5A2B");
        var dark = Br("#5A3A1A");

        c.Children.Add(MakePath(
            $"M{F(x + 8)},{F(y + 18)} L{F(x + 5)},{F(y + 27)} M{F(x + 14)},{F(y + 19)} L{F(x + 15)},{F(y + 28)} " +
            $"M{F(x + 22)},{F(y + 19)} L{F(x + 20)},{F(y + 28)} M{F(x + 27)},{F(y + 18)} L{F(x + 31)},{F(y + 26)}",
            null, dark, 1.6));

        var body = new Ellipse { Width = 26, Height = 12, Fill = fur };
        Canvas.SetLeft(body, x + 4); Canvas.SetTop(body, y + 8);
        c.Children.Add(body);

        c.Children.Add(MakePath($"M{F(x + 24)},{F(y + 12)} L{F(x + 30)},{F(y + 4)} L{F(x + 34)},{F(y + 11)} Z", fur, null));

        var head = new Ellipse { Width = 10, Height = 9, Fill = fur };
        Canvas.SetLeft(head, x + 28); Canvas.SetTop(head, y + 2);
        c.Children.Add(head);

        var nose = new Ellipse { Width = 4, Height = 4, Fill = Br(lead ? "#FF2D2D" : "#3A2412") };
        Canvas.SetLeft(nose, x + 36); Canvas.SetTop(nose, y + 5);
        c.Children.Add(nose);

        c.Children.Add(MakePath(
            $"M{F(x + 31)},{F(y + 2)} L{F(x + 30)},{F(y - 5)} M{F(x + 30)},{F(y - 1)} L{F(x + 27)},{F(y - 3)} " +
            $"M{F(x + 34)},{F(y + 2)} L{F(x + 36)},{F(y - 4)} M{F(x + 35)},{F(y - 1)} L{F(x + 38)},{F(y - 3)}",
            null, Br("#D9B27C"), 1.2));

        var tail = new Ellipse { Width = 5, Height = 5, Fill = Br("#F4EBDD") };
        Canvas.SetLeft(tail, x + 1); Canvas.SetTop(tail, y + 9);
        c.Children.Add(tail);
    }

    // Traineau du Pere Noel avec deux rennes (176 x 64), tourne vers la droite.
    private Canvas MakeSleigh(double scale, out TranslateTransform move, out RotateTransform tilt)
    {
        var c = new Canvas { Width = 176, Height = 64 };
        var gold = Br("#FFD24C");

        AddReindeer(c, 88, 16, false);
        AddReindeer(c, 130, 14, true);

        // Renes
        c.Children.Add(MakePath("M46,32 Q70,34 94,26 M94,26 Q112,24 138,22", null, Br("#FFD24C", 0.9), 0.9));

        // Patins et traineau
        c.Children.Add(MakePath("M2,52 L62,52 C70,52 72,46 66,43", null, gold, 2.5));
        c.Children.Add(MakePath("M16,52 L16,44 M48,52 L48,44", null, gold, 1.8));
        c.Children.Add(MakePath("M4,26 L62,26 C64,40 54,46 34,46 L14,46 C6,46 2,38 4,26 Z", Br("#C1272D"), gold, 1.2));

        // Hotte de cadeaux
        var sack = new Ellipse { Width = 24, Height = 22, Fill = Br("#8D6B3F"), Stroke = Br("#5A4326"), StrokeThickness = 1 };
        Canvas.SetLeft(sack, 6); Canvas.SetTop(sack, 12);
        c.Children.Add(sack);

        // Pere Noel
        var coat = new Ellipse { Width = 18, Height = 16, Fill = Br("#D62839") };
        Canvas.SetLeft(coat, 30); Canvas.SetTop(coat, 22);
        c.Children.Add(coat);
        var face = new Ellipse { Width = 9, Height = 9, Fill = Br("#FFD9B3") };
        Canvas.SetLeft(face, 35); Canvas.SetTop(face, 14);
        c.Children.Add(face);
        var beard = new Ellipse { Width = 9, Height = 6, Fill = Br("#FFFFFF") };
        Canvas.SetLeft(beard, 35); Canvas.SetTop(beard, 18);
        c.Children.Add(beard);
        c.Children.Add(MakePath("M34,16 L41,6 L47,16 Z", Br("#D62839"), null));
        var brim = new Rectangle { Width = 13, Height = 3, Fill = Br("#FFFFFF") };
        Canvas.SetLeft(brim, 34); Canvas.SetTop(brim, 15);
        c.Children.Add(brim);
        var pom = new Ellipse { Width = 5, Height = 5, Fill = Br("#FFFFFF") };
        Canvas.SetLeft(pom, 38.5); Canvas.SetTop(pom, 3);
        c.Children.Add(pom);

        var scaleT = new ScaleTransform { ScaleX = scale, ScaleY = scale };
        tilt = new RotateTransform();
        move = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(scaleT);
        group.Children.Add(tilt);
        group.Children.Add(move);
        c.RenderTransformOrigin = new Point(0.5, 0.5);
        c.RenderTransform = group;
        c.HorizontalAlignment = HorizontalAlignment.Left;
        c.VerticalAlignment = VerticalAlignment.Top;
        return c;
    }

    // ------------------------------------------------------------------
    //  Construction des decorations permanentes
    // ------------------------------------------------------------------

    private void BuildChristmasDecorations()
    {
        var layer = ChristmasLayer;
        layer.Children.Clear();
        _christmasSwings.Clear();
        _christmasTwinkles.Clear();
        _christmasFlakes.Clear();
        _christmasSparkles.Clear();
        _christmasSnowmanRot = null;

        // --- Guirlande lumineuse sur toute la largeur du haut (reconstruite quand la fenetre change de taille) ---
        _christmasGarland = new Canvas
        {
            Height = 46,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        layer.Children.Add(_christmasGarland);
        BuildChristmasGarland();

        // --- Etincelles qui scintillent un peu partout ---
        var rnd = new Random(24);
        string[] sparkleColors = { "#FFF3B0", "#FFFFFF", "#BFE3FF" };
        for (int i = 0; i < 14; i++)
        {
            var sp = MakeSparkle(7 + rnd.NextDouble() * 7, sparkleColors[i % sparkleColors.Length]);
            sp.Opacity = 0.8;
            layer.Children.Add(sp);
            _christmasSparkles.Add(new ChristmasSparkle(sp, 0.04 + rnd.NextDouble() * 0.92, 0.1 + rnd.NextDouble() * 0.72));
            _christmasTwinkles.Add(sp);
        }

        // --- Flocons de neige (animes dans StartChristmasSnow) ---
        for (int i = 0; i < 64; i++)
        {
            bool big = rnd.NextDouble() < 0.12;
            double d = big ? 6.5 + rnd.NextDouble() * 2.5 : 2.5 + rnd.NextDouble() * 3.0;
            var move = new TranslateTransform();
            var flake = new Ellipse
            {
                Width = d, Height = d,
                Fill = Br("#FFFFFF"),
                Opacity = big ? 0.9 : 0.45 + rnd.NextDouble() * 0.35,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = move
            };
            layer.Children.Add(flake);
            double seconds = big ? 7 + rnd.NextDouble() * 4 : 10 + rnd.NextDouble() * 12;
            _christmasFlakes.Add(new ChristmasFlake(flake, move, rnd.NextDouble(), rnd.NextDouble(), seconds,
                                                    8 + rnd.NextDouble() * 22, 2.2 + rnd.NextDouble() * 2.8));
        }

        // --- Deux boules de Noel suspendues, a droite (loin des titres de pages, a gauche des boutons de fenetre) ---
        var (ornament1, rot1) = MakeOrnament(55, "#D62839", "#8A1220");
        ornament1.HorizontalAlignment = HorizontalAlignment.Right;
        ornament1.VerticalAlignment = VerticalAlignment.Top;
        ornament1.Margin = new Thickness(0, 12, 190, 0);
        layer.Children.Add(ornament1);
        _christmasSwings.Add(rot1);

        var (ornament2, rot2) = MakeOrnament(95, "#3A86FF", "#1D4E9E");
        ornament2.HorizontalAlignment = HorizontalAlignment.Right;
        ornament2.VerticalAlignment = VerticalAlignment.Top;
        ornament2.Margin = new Thickness(0, 12, 330, 0);
        layer.Children.Add(ornament2);
        _christmasSwings.Add(rot2);

        // --- Sapin, cadeaux et bonhomme de neige poses au-dessus de la barre de lecture, cote droit ---
        var (tree, lights, star) = MakeChristmasTree(70);
        tree.HorizontalAlignment = HorizontalAlignment.Right;
        tree.VerticalAlignment = VerticalAlignment.Bottom;
        tree.Margin = new Thickness(0, 0, 24, 90);
        layer.Children.Add(tree);
        _christmasTwinkles.AddRange(lights);
        _christmasTwinkles.Add(star);

        var giftBig = MakeGift(36, "#C1272D", "#FFD24C");
        giftBig.HorizontalAlignment = HorizontalAlignment.Right;
        giftBig.VerticalAlignment = VerticalAlignment.Bottom;
        giftBig.Margin = new Thickness(0, 0, 100, 94);
        layer.Children.Add(giftBig);

        var giftSmall = MakeGift(28, "#2FBF71", "#FFFFFF");
        giftSmall.HorizontalAlignment = HorizontalAlignment.Right;
        giftSmall.VerticalAlignment = VerticalAlignment.Bottom;
        giftSmall.Margin = new Thickness(0, 0, 142, 94);
        layer.Children.Add(giftSmall);

        var (snowman, snowmanRot) = MakeSnowman(50);
        snowman.HorizontalAlignment = HorizontalAlignment.Right;
        snowman.VerticalAlignment = VerticalAlignment.Bottom;
        snowman.Margin = new Thickness(0, 0, 180, 92);
        layer.Children.Add(snowman);
        _christmasSnowmanRot = snowmanRot;

        // --- Traineau du Pere Noel : traverse le haut de la fenetre de temps en temps ---
        var sleigh = MakeSleigh(0.85, out var sleighMove, out var sleighTilt);
        sleigh.Margin = new Thickness(0, 46, 0, 0);
        sleighMove.X = -300;   // hors ecran tant que l'animation ne tourne pas
        layer.Children.Add(sleigh);
        _christmasSleighMove = sleighMove;
        _christmasSleighTilt = sleighTilt;

        // --- Animations "au repos" : balancement des boules et du bonhomme, scintillement des lumieres ---
        var idle = new Storyboard();
        double[] swingSeconds = { 2.4, 3.1 };
        for (int i = 0; i < _christmasSwings.Count; i++)
        {
            var swing = new DoubleAnimation
            {
                From = -7, To = 7,
                Duration = TimeSpan.FromSeconds(swingSeconds[i % swingSeconds.Length]),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(swing, _christmasSwings[i]);
            Storyboard.SetTargetProperty(swing, "Angle");
            idle.Children.Add(swing);
        }
        if (_christmasSnowmanRot != null)
        {
            var sway = new DoubleAnimation
            {
                From = -3, To = 3,
                Duration = TimeSpan.FromSeconds(2.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(sway, _christmasSnowmanRot);
            Storyboard.SetTargetProperty(sway, "Angle");
            idle.Children.Add(sway);
        }
        for (int i = 0; i < _christmasTwinkles.Count; i++)
        {
            AddTwinkle(idle, _christmasTwinkles[i], 0.9 + (i % 6) * 0.3, (i % 5) * 0.17, 0.2);
        }
        _christmasIdle = idle;

        ApplyChristmasScatterLayout();
        _christmasBuilt = true;
    }

    // Guirlande : fil qui s'affaisse entre des points d'accroche + ampoules de couleur qui scintillent.
    private void BuildChristmasGarland()
    {
        if (_christmasGarland == null) return;
        try { _christmasGarlandSb?.Stop(); } catch { }

        var host = _christmasGarland;
        host.Children.Clear();

        double w = Math.Max(ChristmasLayer.ActualWidth, 800);
        const double seg = 90, sag = 11;
        int segments = (int)Math.Ceiling(w / seg) + 1;
        double controlY = 2 + 2 * sag;

        var wire = new StringBuilder("M0,2 ");
        for (int i = 0; i < segments; i++)
        {
            double x0 = i * seg, x1 = x0 + seg;
            wire.Append($"Q{F((x0 + x1) / 2)},{F(controlY)} {F(x1)},2 ");
        }
        host.Children.Add(MakePath(wire.ToString(), null, Br("#1E7A4C", 0.9), 1.6));

        var sb = new Storyboard();
        string[] colors = { "#FF4D5E", "#FFD24C", "#3DDC84", "#4DA3FF" };
        int n = 0;
        for (int i = 0; i < segments; i++)
        {
            foreach (double t in new[] { 0.2, 0.5, 0.8 })
            {
                double x = i * seg + t * seg;
                double y = 2 * ((1 - t) * (1 - t) + t * t) + 2 * controlY * (1 - t) * t;
                var bulb = MakeBulb(colors[n % colors.Length]);
                Canvas.SetLeft(bulb, x - 9);
                Canvas.SetTop(bulb, y - 2);
                host.Children.Add(bulb);
                AddTwinkle(sb, bulb, 1.1 + (n % 5) * 0.35, (n % 7) * 0.23, 0.35);
                n++;
            }
        }
        _christmasGarlandSb = sb;
    }

    // Place flocons et etincelles selon la taille courante de la fenetre.
    private void ApplyChristmasScatterLayout()
    {
        double w = Math.Max(ChristmasLayer.ActualWidth, 800);
        double h = Math.Max(ChristmasLayer.ActualHeight, 600);
        foreach (var f in _christmasFlakes)
        {
            f.Element.Margin = new Thickness(f.XFrac * w, 0, 0, 0);
            f.Move.Y = f.YFrac * h;   // position fixe quand les animations sont reduites / arretees
        }
        foreach (var s in _christmasSparkles)
        {
            s.Element.Margin = new Thickness(s.XFrac * w, s.YFrac * h, 0, 0);
        }
    }

    // ------------------------------------------------------------------
    //  Animations permanentes (neige, scintillements, balancement, traineau)
    // ------------------------------------------------------------------

    private void StartChristmasAnimations()
    {
        if (!HalloweenAnimationsAllowed())
        {
            // Animations reduites dans Windows : decorations fixes.
            StopChristmasAnimations();
            ApplyChristmasScatterLayout();
            return;
        }
        ApplyChristmasScatterLayout();
        _christmasIdle?.Begin();
        _christmasGarlandSb?.Begin();
        StartChristmasSnow();
        StartChristmasSleigh();
    }

    private void StopChristmasAnimations()
    {
        try { _christmasIdle?.Stop(); } catch { }
        try { _christmasGarlandSb?.Stop(); } catch { }
        try { _christmasSnow?.Stop(); } catch { }
        try { _christmasSleighSb?.Stop(); } catch { }
    }

    private void StartChristmasSnow()
    {
        if (!_christmasBuilt || !HalloweenAnimationsAllowed()) return;
        try { _christmasSnow?.Stop(); } catch { }

        double h = Math.Max(ChristmasLayer.ActualHeight, 600);
        var sb = new Storyboard();
        foreach (var f in _christmasFlakes)
        {
            var fall = new DoubleAnimation
            {
                From = -16, To = h + 16,
                Duration = TimeSpan.FromSeconds(f.Seconds),
                BeginTime = TimeSpan.FromSeconds(-f.YFrac * f.Seconds),   // demarre a mi-chute : la neige est deja repartie
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(fall, f.Move);
            Storyboard.SetTargetProperty(fall, "Y");
            sb.Children.Add(fall);

            var drift = new DoubleAnimation
            {
                From = -f.SwayAmp, To = f.SwayAmp,
                Duration = TimeSpan.FromSeconds(f.SwaySeconds),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(drift, f.Move);
            Storyboard.SetTargetProperty(drift, "X");
            sb.Children.Add(drift);
        }
        _christmasSnow = sb;
        sb.Begin();
    }

    private void StartChristmasSleigh()
    {
        if (!_christmasBuilt || _christmasSleighMove == null || _christmasSleighTilt == null || !HalloweenAnimationsAllowed()) return;
        try { _christmasSleighSb?.Stop(); } catch { }

        double w = Math.Max(ChristmasLayer.ActualWidth, 800) + 300;
        var sb = new Storyboard();

        // Traversee (16 s) puis pause hors ecran avant la suivante.
        var cross = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(3) };
        cross.KeyFrames.Add(LinKey(0, -300));
        cross.KeyFrames.Add(LinKey(16, w));
        cross.KeyFrames.Add(LinKey(40, w));
        Storyboard.SetTarget(cross, _christmasSleighMove);
        Storyboard.SetTargetProperty(cross, "X");
        sb.Children.Add(cross);

        var bob = new DoubleAnimation
        {
            From = 0, To = 16,
            Duration = TimeSpan.FromSeconds(1.5),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(bob, _christmasSleighMove);
        Storyboard.SetTargetProperty(bob, "Y");
        sb.Children.Add(bob);

        var tiltAnim = new DoubleAnimation
        {
            From = -3, To = 3,
            Duration = TimeSpan.FromSeconds(1.5),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(tiltAnim, _christmasSleighTilt);
        Storyboard.SetTargetProperty(tiltAnim, "Angle");
        sb.Children.Add(tiltAnim);

        _christmasSleighSb = sb;
        sb.Begin();
    }

    // ------------------------------------------------------------------
    //  Animation d'activation (~3 s) : flash rouge/vert, congere qui monte, rafale de neige,
    //  traineau qui traverse, etincelles, puis "Joyeux Noel" qui apparait et s'efface.
    // ------------------------------------------------------------------

    private void StopChristmasIntro()
    {
        try { _christmasIntro?.Stop(); } catch { }
        _christmasIntro = null;
        if (ChristmasIntroLayer != null)
        {
            ChristmasIntroLayer.Visibility = Visibility.Collapsed;
            ChristmasIntroLayer.Children.Clear();
        }
    }

    private void PlayChristmasIntro()
    {
        if (!HalloweenAnimationsAllowed()) return;
        StopChristmasIntro();

        var layer = ChristmasIntroLayer;
        double w = Math.Max(RootGrid.ActualWidth, 800);
        double h = Math.Max(RootGrid.ActualHeight, 600);
        var sb = new Storyboard();
        var red = ParseHexColor("#E63946");
        var green = ParseHexColor("#2FBF71");

        // 1) Flash rouge -> vert (deux eclairs rapides)
        var flash = new Grid
        {
            Opacity = 0,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = red, Offset = 0.0 },
                    new GradientStop { Color = green, Offset = 1.0 }
                }
            }
        };
        layer.Children.Add(flash);
        KeyAnim(sb, flash, "Opacity", (0.0, 0.0), (0.12, 0.5), (0.40, 0.0), (0.55, 0.3), (0.95, 0.0));

        // 2) Congere de neige qui monte depuis le bas
        var driftMove = new TranslateTransform { Y = h * 0.45 };
        var drift = new Border
        {
            Height = h * 0.45,
            VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = 0,
            RenderTransform = driftMove,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0, 220, 235, 255), Offset = 0.0 },
                    new GradientStop { Color = Color.FromArgb(150, 220, 235, 255), Offset = 1.0 }
                }
            }
        };
        layer.Children.Add(drift);
        KeyAnim(sb, driftMove, "Y", (0.2, h * 0.45), (1.6, 0.0));
        KeyAnim(sb, drift, "Opacity", (0.2, 0.0), (1.0, 0.85), (2.2, 0.85), (3.0, 0.0));

        // 3) Rafale de neige : gros flocons qui tombent vite
        var rnd = new Random(12);
        for (int i = 0; i < 40; i++)
        {
            double d = 4 + rnd.NextDouble() * 8;
            var move = new TranslateTransform { Y = -30 };
            var flake = new Ellipse
            {
                Width = d, Height = d,
                Fill = Br("#FFFFFF"),
                Opacity = 0.85,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(rnd.NextDouble() * w, 0, 0, 0),
                RenderTransform = move
            };
            layer.Children.Add(flake);

            double begin = rnd.NextDouble() * 1.4;
            double dur = 1.4 + rnd.NextDouble() * 1.0;
            KeyAnim(sb, move, "Y", (0.0, -30), (begin, -30), (begin + dur, h + 30));
            KeyAnim(sb, move, "X", (0.0, 0), (begin, 0), (begin + dur, (rnd.NextDouble() - 0.5) * 180));
        }

        // 4) Traineau qui traverse la fenetre en montant legerement
        var sleigh = MakeSleigh(1.3, out var sleighMove, out var sleighTilt);
        sleigh.Margin = new Thickness(0, h * 0.2, 0, 0);
        sleighMove.X = -320;
        layer.Children.Add(sleigh);
        KeyAnim(sb, sleighMove, "X", (0.0, -320), (0.3, -320), (2.9, w + 320));
        KeyAnim(sb, sleighMove, "Y", (0.0, 0), (0.3, 0), (2.9, -60));
        KeyAnim(sb, sleighTilt, "Angle", (0.0, -4), (1.5, 4), (2.9, -2));

        // 5) Etincelles dorees qui apparaissent et disparaissent
        string[] sparkleColors = { "#FFF3B0", "#FFD24C", "#FFFFFF" };
        for (int i = 0; i < 12; i++)
        {
            var sp = MakeSparkle(14 + rnd.NextDouble() * 14, sparkleColors[i % sparkleColors.Length]);
            sp.Opacity = 0;
            sp.Margin = new Thickness(rnd.NextDouble() * w, h * (0.1 + 0.75 * rnd.NextDouble()), 0, 0);
            layer.Children.Add(sp);
            double b = 0.3 + rnd.NextDouble() * 2.0;
            KeyAnim(sb, sp, "Opacity", (0.0, 0.0), (b, 0.0), (b + 0.35, 1.0), (b + 0.9, 0.0));
        }

        // 6) "Joyeux Noel" : apparait (1.3 s -> 1.9 s), reste, puis s'efface (2.5 s -> 3.0 s)
        string message = Resona.Models.Strings.Current.IsFr ? "Joyeux No\u00ebl" : "Merry Christmas";
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
            CharacterSpacing = 120, Foreground = new SolidColorBrush(green),
            Margin = new Thickness(4, 4, 0, 0), Opacity = 0.85
        });
        title.Children.Add(new TextBlock
        {
            Text = message, FontSize = 64, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 120, Foreground = new SolidColorBrush(ParseHexColor("#FF5A66"))
        });
        layer.Children.Add(title);
        KeyAnim(sb, title, "Opacity", (0.0, 0.0), (1.3, 0.0), (1.9, 1.0), (2.5, 1.0), (3.0, 0.0));
        KeyAnim(sb, titleScale, "ScaleX", (0.0, 0.7), (1.3, 0.7), (1.9, 1.0), (3.0, 1.06));
        KeyAnim(sb, titleScale, "ScaleY", (0.0, 0.7), (1.3, 0.7), (1.9, 1.0), (3.0, 1.06));

        sb.Completed += (s, e) =>
        {
            if (ReferenceEquals(_christmasIntro, sb)) StopChristmasIntro();
        };
        _christmasIntro = sb;
        layer.Visibility = Visibility.Visible;
        sb.Begin();
    }
}
