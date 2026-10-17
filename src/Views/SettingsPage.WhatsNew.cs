using Microsoft.UI.Xaml;

namespace Resona.Views;

/// <summary>Parametres > Mises a jour : bouton "Voir les nouveautes" (patch note de la version, pris sur GitHub).</summary>
public sealed partial class SettingsPage
{
    private async void WhatsNewButton_Click(object sender, RoutedEventArgs e)
    {
        WhatsNewButton.IsEnabled = false;
        try
        {
            await WhatsNewManager.ShowAsync(this.XamlRoot);
        }
        finally
        {
            WhatsNewButton.IsEnabled = true;
        }
    }
}
