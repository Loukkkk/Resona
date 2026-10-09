// ============================================================
//  MainWindow.Grain.cs  —  Resona
//  Grain d'arriere-plan : texture de bruit generee une seule fois (WriteableBitmap), affichee par l'Image "AppGrainLayer"
//  (premier enfant de RootGrid : au-dessus du fond, sous toute l'interface). Le curseur de reglage ne change que l'opacite.
// ============================================================

using System;
using System.Runtime.InteropServices.WindowsRuntime;   // PixelBuffer.AsStream()
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Resona.Models;

namespace Resona;

public sealed partial class MainWindow
{
    private WriteableBitmap? _grainBitmap;

    // Opacite de la couche pour un reglage a 100 % : volontairement faible, le grain est deja tres present a ce niveau.
    private const double GrainMaxOpacity = 0.09;

    public void ApplyGrain()
    {
        try
        {
            if (AppGrainLayer == null) return;
            var settings = App.Settings.Current;
            int amount = Math.Clamp(settings.GrainAmount, 0, 100);
            // Le grain fait partie du theme de couleur (Couleur unie / Halloween) : pas de grain sur Mica / Acrylic.
            if (!settings.Backdrop.IsSolidLike() || !settings.GrainEnabled || amount <= 0)
            {
                AppGrainLayer.Visibility = Visibility.Collapsed;
                return;
            }

            if (_grainBitmap == null) _grainBitmap = BuildGrainBitmap(1024);
            if (!ReferenceEquals(AppGrainLayer.Source, _grainBitmap)) AppGrainLayer.Source = _grainBitmap;
            AppGrainLayer.Opacity = amount / 100.0 * GrainMaxOpacity;
            AppGrainLayer.Visibility = Visibility.Visible;
        }
        catch
        {
            // Purement cosmetique : on n'echoue jamais pour du grain.
        }
    }

    // Bruit gris approximativement gaussien (somme de 3 tirages), centre sur un gris plutot sombre : la couche eclaircit
    // peu un fond sombre tout en laissant des points clairs ET sombres (rendu "pellicule"). Graine fixe -> rendu stable.
    private static WriteableBitmap BuildGrainBitmap(int size)
    {
        var bitmap = new WriteableBitmap(size, size);
        var pixels = new byte[size * size * 4];
        var rnd = new Random(1337);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            double gauss = (rnd.NextDouble() + rnd.NextDouble() + rnd.NextDouble() - 1.5) / 0.5;   // ~N(0,1)
            byte v = (byte)Math.Clamp((int)Math.Round(96 + gauss * 64), 0, 255);
            pixels[i] = v;       // B
            pixels[i + 1] = v;   // G
            pixels[i + 2] = v;   // R
            pixels[i + 3] = 255; // A
        }
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixels, 0, pixels.Length);
        }
        bitmap.Invalidate();
        return bitmap;
    }
}
