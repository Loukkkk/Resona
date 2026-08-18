using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Resona.Converters;

/// <summary>
/// Convertit un chemin de fichier (string, potentiellement null) en ImageSource.
/// Le convertisseur implicite de WinUI plante avec une ArgumentException
/// ("Paramètre incorrect") quand on lui passe null directement sur Image.Source
/// via x:Bind — ce convertisseur explicite évite le problème en retournant
/// simplement null (= pas d'image affichée) au lieu de laisser planter l'app.
/// </summary>
public class StringToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return new BitmapImage(new Uri(path));
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

