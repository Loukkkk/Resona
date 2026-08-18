using System;
using Microsoft.UI.Xaml.Data;

namespace Resona.Converters;

/// <summary>
/// Convertit une valeur en secondes (double) affichée par défaut dans le tooltip
/// natif du Slider (ex: "127") en format minutes:secondes (ex: "2:07"), pour la
/// barre de progression de lecture.
/// </summary>
public class SecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double seconds = value switch
        {
            double d => d,
            int i => i,
            _ => 0
        };

        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

