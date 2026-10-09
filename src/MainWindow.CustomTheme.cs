// ============================================================
//  MainWindow.CustomTheme.cs  —  Resona
//  Fond en degrade du theme de couleur personnalise + rafraichissement leger pendant le glisser du selecteur.
//  ApplyCustomBackground() est appelee par ApplyBackdrop() et RefreshThemeDependentUI() : quand le degrade n'est pas
//  actif (ou quand on quitte le theme personnalise), on remet le fond uni habituel.
// ============================================================

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Resona.Models;
using Windows.Foundation;

namespace Resona;

public sealed partial class MainWindow
{
    // Brush de degrade reutilise : pendant le glisser on MODIFIE ses couleurs au lieu d'en recreer un a chaque fois.
    private LinearGradientBrush? _customGradientBrush;

    public void ApplyCustomBackground()
    {
        try
        {
            var settings = App.Settings.Current;
            // Mica / Acrylic : ApplyBackdrop() gere le fond transparent, on n'y touche pas.
            if (!settings.Backdrop.IsSolidLike()) return;

            var colors = CustomTheme.EffectiveGradient(settings);
            if (colors.Count >= CustomTheme.MinGradientColors)
            {
                bool reuse = _customGradientBrush != null
                             && ReferenceEquals(((Panel)RootGrid).Background, _customGradientBrush)
                             && _customGradientBrush.GradientStops.Count == colors.Count;
                if (reuse)
                {
                    for (int i = 0; i < colors.Count; i++)
                        _customGradientBrush!.GradientStops[i].Color = ParseHexColor(colors[i]);
                    return;
                }

                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)   // diagonale haut-gauche -> bas-droite
                };
                for (int i = 0; i < colors.Count; i++)
                {
                    brush.GradientStops.Add(new GradientStop
                    {
                        Color = ParseHexColor(colors[i]),
                        Offset = (double)i / (colors.Count - 1)
                    });
                }
                _customGradientBrush = brush;
                ((Panel)RootGrid).Background = brush;
            }
            else
            {
                _customGradientBrush = null;
                ((Panel)RootGrid).Background = (Brush)Application.Current.Resources["AppDeepBackgroundBrush"];
            }
        }
        catch
        {
            // Purement cosmetique : en cas de souci on garde le fond uni.
        }
    }

    /// <summary>
    /// Rafraichissement LEGER du theme personnalise (sans ForceUIRepaint, qui re-themait tous les boutons de l'app).
    /// updatePlayerBar = false pendant le glisser (apercu en direct), true une fois le selecteur ferme.
    /// </summary>
    public void RefreshCustomThemeLight(bool updatePlayerBar)
    {
        try
        {
            if (updatePlayerBar)
            {
                _coverColorCache.Clear();
            }

            // Le degrade du lecteur depend de la couleur de surface du theme : on le met a jour AUSSI pendant le glisser
            // (apercu en direct). Le cout est faible : la couleur moyenne de la pochette vient du cache _avgColorCache
            // (independant du theme) et l'appelant limite deja les mises a jour a ~6 par seconde.
            if (_currentIndex >= 0)
            {
                UpdatePlayerBarColorAsync(_library[_currentIndex]);
            }
            else if (updatePlayerBar && App.Settings.Current.Backdrop.IsSolidLike())
            {
                ((Panel)PlayerBar).Background = (Brush)Application.Current.Resources["AppSurfaceBrush"];
            }
            ApplyCustomBackground();
            ApplyNativeWindowBackgroundFix();
        }
        catch
        {
            // Cosmetique : ne jamais faire planter l'app pour un rafraichissement de couleur.
        }
    }
}
