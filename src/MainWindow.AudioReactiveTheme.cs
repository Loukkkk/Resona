using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Resona.Models;
using Windows.UI;

namespace Resona;

/// <summary>
/// Thème réactif au son : module avec équilibre et élégance la luminosité et les nuances
/// du thème de couleur sélectionné (preset ou thème personnalisé) au rythme de la musique.
/// </summary>
public sealed partial class MainWindow
{
    private float _reactiveEnergy;
    private float _runningBassAvg;
    private bool  _reactiveResetPending;

    public void UpdateAudioReactiveThemeState()
    {
        if (!App.Settings.Current.AudioReactiveThemeEnabled)
        {
            ResetAudioReactiveTheme();
        }
    }

    private void ResetAudioReactiveTheme()
    {
        _reactiveEnergy = 0f;
        _runningBassAvg = 0f;
        _reactiveResetPending = false;
        try
        {
            App.ApplyThemeResources(notify: false);
            ApplyCustomBackground();
        }
        catch
        {
            // Silencieux : cosmétique uniquement
        }
    }

    public void TickAudioReactiveTheme()
    {
        bool isEnabled = App.Settings.Current.AudioReactiveThemeEnabled;
        if (!isEnabled)
        {
            if (_reactiveResetPending)
                ResetAudioReactiveTheme();
            return;
        }

        bool isPlaying = App.AudioEngine.State == NAudio.Wave.PlaybackState.Playing;
        float targetEnergy = 0f;

        if (isPlaying)
        {
            float rms = App.AudioEngine.CurrentRmsLevel;
            float bass = App.AudioEngine.CurrentBassLevel;

            // Détection équilibrée des à-coups (impacts de basse / kick)
            float delta = Math.Max(0f, bass - _runningBassAvg);
            _runningBassAvg = _runningBassAvg * 0.94f + bass * 0.06f;
            float punch = Math.Clamp(delta * 2.8f, 0f, 1f);

            // Énergie musicale dosée : dynamique présente sans saturer en permanence
            float raw = bass * 1.3f + rms * 0.5f + punch * 0.9f;
            targetEnergy = Math.Clamp(raw * 0.75f, 0f, 1.0f);
        }
        else
        {
            _runningBassAvg = 0f;
        }

        // Attaque réactive mais douce (0.38) et descente fluide (0.10) pour éviter l'effet stroboscopique
        if (targetEnergy > _reactiveEnergy)
            _reactiveEnergy += (targetEnergy - _reactiveEnergy) * 0.38f;
        else
            _reactiveEnergy += (targetEnergy - _reactiveEnergy) * 0.10f;

        if (_reactiveEnergy > 0.005f)
        {
            _reactiveResetPending = true;
            ApplyAudioReactiveColors(_reactiveEnergy);
        }
        else if (_reactiveResetPending)
        {
            ResetAudioReactiveTheme();
        }
    }

    private void ApplyAudioReactiveColors(float energy)
    {
        try
        {
            var settings = App.Settings.Current;
            var preset = ThemePresets.Resolve(settings);
            var res = Application.Current.Resources;
            bool isSolid = settings.Backdrop.IsSolidLike();
            double e = Math.Clamp(energy, 0.0, 1.0);

            // 1. Accent principal : pulsation lumineuse élégante et éclat dosé (+18%)
            var (h, s, v) = CustomTheme.ToHsv(preset.AccentHex);
            double hMod = (h + 7.0 * e + 360.0) % 360.0;
            double sMod = Math.Clamp(s + 0.08 * e, 0.0, 1.0);
            double vMod = Math.Min(1.0, v + 0.10 * e);
            var baseModAccent = ParseHexColor(CustomTheme.FromHsv(hMod, sMod, vMod));

            // Éclaircissement mesuré (illumine les contrôles et sliders sans les décolorer)
            var reactiveAccent = LightenColor(baseModAccent, 0.18 * e);

            SetBrushColorDirect(res, "AppAccentBrush", reactiveAccent);
            SetBrushColorDirect(res, "AccentFillColorDefaultBrush", reactiveAccent);
            SetBrushColorDirect(res, "AppTimecodeBrush", reactiveAccent);

            // 2. Accent secondaire : suit en harmonie subtile
            var (h2, s2, v2) = CustomTheme.ToHsv(preset.AccentSecondaryHex);
            double h2Mod = (h2 + 6.0 * e + 360.0) % 360.0;
            var baseModSec = ParseHexColor(CustomTheme.FromHsv(h2Mod, Math.Clamp(s2 + 0.06 * e, 0.0, 1.0), Math.Min(1.0, v2 + 0.10 * e)));
            var reactiveSec = LightenColor(baseModSec, 0.20 * e);
            SetBrushColorDirect(res, "AppAccentSecondaryBrush", reactiveSec);

            if (isSolid)
            {
                var baseBg = ParseHexColor(preset.BackgroundHex);
                var baseSf = ParseHexColor(preset.SurfaceHex);

                // 3. Fond profond : respiration d'ambiance très douce sans délaver le fond sombre
                var reactiveBg = BlendColor(baseBg, reactiveAccent, 0.10 * e, 6 * e);
                SetBrushColorDirect(res, "AppDeepBackgroundBrush", reactiveBg);

                // 4. Surface (lecteur / cartes) : légère lueur chaleureuse
                var reactiveSf = BlendColor(baseSf, reactiveAccent, 0.08 * e, 5 * e);
                SetBrushColorDirect(res, "AppSurfaceBrush", reactiveSf);

                // 5. Dégradé personnalisé (si actif)
                if (_customGradientBrush != null)
                {
                    var gradColors = CustomTheme.EffectiveGradient(settings);
                    if (_customGradientBrush.GradientStops.Count == gradColors.Count)
                    {
                        for (int i = 0; i < gradColors.Count; i++)
                        {
                            var (gh, gs, gv) = CustomTheme.ToHsv(gradColors[i]);
                            var shifted = ParseHexColor(CustomTheme.FromHsv((gh + 6.0 * e + 360.0) % 360.0, gs, Math.Min(1.0, gv + 0.10 * e)));
                            _customGradientBrush.GradientStops[i].Color = LightenColor(shifted, 0.14 * e);
                        }
                    }
                }
            }
        }
        catch
        {
            // Purement cosmétique : aucune exception ne doit impacter l'application
        }
    }

    private static Color LightenColor(Color c, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromArgb(255,
            (byte)Math.Min(255, c.R + (255 - c.R) * amount),
            (byte)Math.Min(255, c.G + (255 - c.G) * amount),
            (byte)Math.Min(255, c.B + (255 - c.B) * amount));
    }

    private static Color BlendColor(Color baseCol, Color glowCol, double factor, double brighten = 0)
    {
        factor = Math.Clamp(factor, 0.0, 1.0);
        double r = baseCol.R + (glowCol.R - baseCol.R) * factor + brighten;
        double g = baseCol.G + (glowCol.G - baseCol.G) * factor + brighten;
        double b = baseCol.B + (glowCol.B - baseCol.B) * factor + brighten;
        return Color.FromArgb(255,
            (byte)Math.Clamp((int)Math.Round(r), 0, 255),
            (byte)Math.Clamp((int)Math.Round(g), 0, 255),
            (byte)Math.Clamp((int)Math.Round(b), 0, 255));
    }

    private static void SetBrushColorDirect(ResourceDictionary res, string key, Color color)
    {
        if (res.TryGetValue(key, out object? obj) && obj is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }
}
