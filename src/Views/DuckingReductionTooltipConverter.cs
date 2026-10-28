using Microsoft.UI.Xaml.Data;
using Resona.Services;

namespace Resona.Views;

/// <summary>Infobulle du curseur "Reduction du volume" (ducking) : "-12 dB", ou "Muet" au bout du curseur (coupure totale).</summary>
public sealed class DuckingReductionTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double v = value is double d ? d : 0;
        if (v >= DuckingService.MuteThresholdDb)
            return Resona.Models.Strings.Current.SettingsPage_Text_DuckingMuteShort;
        return $"-{v:0} dB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
