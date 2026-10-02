using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Resona.Converters;

/// <summary>
/// Convertit un chemin de fichier (string, potentiellement null) en ImageBrush
/// utilisable comme Background d'un Border/Control.
///
/// Pourquoi un ImageBrush et pas une Image enfant ?
/// En WinUI 3, un contrôle Image avec Stretch=UniformToFill aligne son contenu
/// à GAUCHE quand la cover est plus large que le conteneur — même en mettant
/// HorizontalAlignment=Center. L'ImageBrush, elle, clippe et centre parfaitement
/// (comportement identique à un CSS background-size: cover). C'est donc le brush
/// qu'il faut utiliser pour les covers carrées dans les listes/catégories.
///
/// L'image décodée vient du cache partagé de l'app (CoverCacheService) : une seule
/// BitmapImage par pochette, décodée à la plus grande taille demandée, partagée avec les
/// pages Albums/Artistes/Playlists et le lecteur. Le brush, lui, est un petit objet créé
/// à la demande autour de cette image partagée (les covers de 40×40 ne nécessitent pas
/// de charger la résolution complète, souvent 1200×1200 sur disque).
///
/// Important : ce converter retourne TOUJOURS un Brush non-null (soit l'ImageBrush
/// de la cover, soit le fallback transparent). Retourner null poserait problème
/// avec la virtualisation du ListView (DataTemplate recyclé garde l'ancienne cover).
/// </summary>
public class StringToImageBrushConverter : IValueConverter
{
    private static readonly Stretch CoverStretch = Stretch.UniformToFill;

    // Taille de décodage demandée au cache partagé. Les covers en bibliothèque font 40×40, donc 80 px
    // de décodage est amplement suffisant (hiDPI ×2). Si une autre page a déjà décodé cette cover
    // plus grand, le cache renvoie directement cette version.
    private const int DecodeSize = 80;

    // Brush de fallback retourné quand pas de cover (pour écraser une éventuelle
    // ancienne cover dans un DataTemplate recyclé par la virtualisation).
    private static Brush? _fallbackBrush;

    // Conservé pour compatibilité : le cache vit désormais dans CoverCacheService.
    public static void ClearCache(string path)
    {
        Resona.Services.CoverCacheService.ClearCache(path);
    }

    private static Brush GetFallbackBrush()
    {
        if (_fallbackBrush != null) return _fallbackBrush;
        _fallbackBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        return _fallbackBrush;
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return GetFallbackBrush();

        try
        {
            BitmapImage? bmp = Resona.Services.CoverCacheService.GetBitmap(path, DecodeSize);
            if (bmp == null)
                return GetFallbackBrush();

            return new ImageBrush
            {
                ImageSource = bmp,
                Stretch = CoverStretch
            };
        }
        catch
        {
            return GetFallbackBrush();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
