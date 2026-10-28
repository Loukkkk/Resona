using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Resona;

/// <summary>
/// Animation "equalizer" optionnelle en bas de la grosse cover centrale (vue lecteur "En cours de lecture").
/// Desactivee par defaut (Parametres > Apparence). Les barres suivent le niveau RMS et les basses du moteur audio
/// (gauche = plutot les basses, droite = plutot le niveau global), avec une oscillation propre a chaque barre.
/// </summary>
public sealed partial class MainWindow
{
    private const int EqBarCount = 24;
    private const double EqBarMaxHeight = 64.0;

    private readonly List<ScaleTransform> _eqBarScales = new();
    private readonly float[] _eqLevels = new float[EqBarCount];
    private float _eqSmoothedRms;
    private float _eqSmoothedBass;
    private SolidColorBrush? _eqBrush;   // brush partage par toutes les barres (couleur = moyenne de la cover, contrastee)
    private Color _eqColorApplied;

    /// <summary>Appele quand l'option change dans les Parametres.</summary>
    public void ApplyNowPlayingEqualizerSetting()
    {
        UpdateEqualizerVisibility();
    }

    // Visible uniquement en mode grosse cover ET si l'option est activee. Renvoie true si l'animation doit tourner.
    private bool UpdateEqualizerVisibility()
    {
        if (NowPlayingEqualizerHost == null) return false;

        bool show = _isNowPlayingModeActive && App.Settings.Current.NowPlayingEqualizerEnabled;
        Visibility wanted = show ? Visibility.Visible : Visibility.Collapsed;
        if (NowPlayingEqualizerHost.Visibility != wanted)
        {
            NowPlayingEqualizerHost.Visibility = wanted;
            if (show)
            {
                EnsureEqualizerBars();
            }
            else
            {
                ResetEqualizerBars();
            }
        }
        return show;
    }

    private void EnsureEqualizerBars()
    {
        if (_eqBarScales.Count > 0 || NowPlayingEqualizerBars == null) return;

        _eqBrush ??= new SolidColorBrush(Microsoft.UI.Colors.White);
        UpdateEqualizerColor(true);

        for (int i = 0; i < EqBarCount; i++)
        {
            var scale = new ScaleTransform { ScaleY = 0.05 };
            var bar = new Rectangle
            {
                Width = 8,
                Height = EqBarMaxHeight,
                RadiusX = 4,
                RadiusY = 4,
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = _eqBrush,
                Opacity = 0.9,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = scale
            };
            NowPlayingEqualizerBars.Children.Add(bar);
            _eqBarScales.Add(scale);
        }
    }

    // Couleur des barres : moyenne de la cover (la meme que celle du gradient du lecteur), poussee en saturation/luminosite
    // pour rester bien visible sur le degrade sombre. Mise a jour uniquement quand la couleur moyenne change (changement de son).
    private void UpdateEqualizerColor(bool force)
    {
        if (_eqBrush == null) return;
        Color avg = _lastComputedAvgColor;
        if (!force && avg == _eqColorApplied) return;
        _eqColorApplied = avg;
        _eqBrush.Color = ContrastedCoverColor(avg);
    }

    private static Color ContrastedCoverColor(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;

        double h = 0;
        if (d > 0)
        {
            if (max == r) h = ((g - b) / d) % 6.0;
            else if (max == g) h = (b - r) / d + 2.0;
            else h = (r - g) / d + 4.0;
            h *= 60.0;
            if (h < 0) h += 360.0;
        }
        double s = max <= 0 ? 0 : d / max;

        // Cover quasi grise / noir et blanc : pas de teinte a pousser, on reste sur un blanc doux.
        if (s < 0.12) return Color.FromArgb(255, 245, 245, 245);

        s = Math.Clamp(s * 1.35 + 0.2, 0.6, 1.0);
        const double v = 1.0;

        double cc = v * s;
        double x = cc * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
        double m = v - cc;
        double r1, g1, b1;
        if (h < 60) { r1 = cc; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = cc; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = cc; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = cc; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = cc; }
        else { r1 = cc; g1 = 0; b1 = x; }
        r = r1 + m; g = g1 + m; b = b1 + m;

        // Bleus / violets sombres : on eclaircit jusqu'a une luminance minimale pour qu'ils ressortent sur le fond sombre.
        double lum = 0.299 * r + 0.587 * g + 0.114 * b;
        if (lum < 0.6)
        {
            double t = (0.6 - lum) / (1.0 - lum);
            r += (1.0 - r) * t;
            g += (1.0 - g) * t;
            b += (1.0 - b) * t;
        }

        return Color.FromArgb(255, (byte)Math.Round(r * 255.0), (byte)Math.Round(g * 255.0), (byte)Math.Round(b * 255.0));
    }

    private void ResetEqualizerBars()
    {
        _eqSmoothedRms = 0f;
        _eqSmoothedBass = 0f;
        for (int i = 0; i < _eqBarScales.Count; i++)
        {
            _eqLevels[i] = 0f;
            _eqBarScales[i].ScaleY = 0.05;
        }
    }

    /// <summary>Appele par le timer de position (~30 fps).</summary>
    public void TickNowPlayingEqualizer()
    {
        try
        {
            if (!UpdateEqualizerVisibility() || _eqBarScales.Count != EqBarCount) return;

            bool playing = App.AudioEngine.State == NAudio.Wave.PlaybackState.Playing;
            UpdateEqualizerColor(false);
            float rmsTarget = playing ? Math.Min(1f, App.AudioEngine.CurrentRmsLevel * 8f) : 0f;
            float bassTarget = playing ? Math.Min(1f, App.AudioEngine.CurrentBassLevel * 3f) : 0f;

            // Attaque rapide, descente plus douce : evite l'effet stroboscopique.
            _eqSmoothedRms += (rmsTarget - _eqSmoothedRms) * (rmsTarget > _eqSmoothedRms ? 0.5f : 0.15f);
            _eqSmoothedBass += (bassTarget - _eqSmoothedBass) * (bassTarget > _eqSmoothedBass ? 0.5f : 0.15f);

            double t = Environment.TickCount64 / 1000.0;
            for (int i = 0; i < EqBarCount; i++)
            {
                float p = i / (float)(EqBarCount - 1);
                float source = _eqSmoothedBass * (1f - p) + _eqSmoothedRms * p;
                float wobble = 0.55f + 0.45f * (float)Math.Sin(t * (3.2 + i * 0.37) + i * 1.7);
                float target = playing ? Math.Clamp(Math.Max(0.06f, source * wobble * 1.15f), 0.05f, 1f) : 0.05f;

                _eqLevels[i] += (target - _eqLevels[i]) * (target > _eqLevels[i] ? 0.55f : 0.18f);
                _eqBarScales[i].ScaleY = Math.Max(0.05, _eqLevels[i]);
            }
        }
        catch
        {
            // Purement cosmetique : aucune exception ne doit impacter le lecteur
        }
    }
}
