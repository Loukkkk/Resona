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
/// Ce converter inclut un cache et décode les images à une taille réduite
/// (DecodePixelWidth) pour accélérer le rendu : les covers de 40Ã—40 ne nécessitent
/// pas de charger la résolution complète (souvent 1200Ã—1200 sur disque).
///
/// Important : ce converter retourne TOUJOURS un Brush non-null (soit l'ImageBrush
/// de la cover, soit le fallback AppSurfaceBrush). Retourner null poserait problème
/// avec la virtualisation du ListView (DataTemplate recyclé garde l'ancienne cover).
/// </summary>
public class StringToImageBrushConverter : IValueConverter
{
    private static readonly Stretch CoverStretch = Stretch.UniformToFill;

    // Cache des BitmapImage décodés à la taille cible. Cache borné pour éviter
    // une fuite mémoire quand la session parcourt des milliers de covers distinctes.
    private static readonly Dictionary<string, ImageBrush> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> _cacheOrder = new();
    private const int MaxCacheEntries = 500;

    // Taille de décodage cible. Les covers en bibliothèque font 40Ã—40, donc 80 px
    // de décodage est amplement suffisant (hiDPI Ã—2).
    private const int DecodeSize = 80;

    // Brush de fallback retourné quand pas de cover (pour écraser une éventuelle
    // ancienne cover dans un DataTemplate recyclé par la virtualisation).
    private static Brush? _fallbackBrush;

    public static void ClearCache(string path)
    {
        _cache.Remove(path);
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

        if (_cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            var bmp = new BitmapImage
            {
                DecodePixelWidth = DecodeSize
            };
            bmp.UriSource = new Uri(path);

            var brush = new ImageBrush
            {
                ImageSource = bmp,
                Stretch = CoverStretch
            };
            // Éviction FIFO avant insertion
            if (_cache.Count >= MaxCacheEntries && _cacheOrder.Count > 0)
            {
                var oldest = _cacheOrder.Dequeue();
                _cache.Remove(oldest);
            }
            _cache[path] = brush;
            _cacheOrder.Enqueue(path);
            return brush;
        }
        catch
        {
            return GetFallbackBrush();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

