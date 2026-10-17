using System;
using Microsoft.UI.Xaml;
using Resona.Models;
using Resona.Services;

namespace Resona.Views;

/// <summary>
/// Parametres > Systeme : integration a Windows.
///  - bouton qui ouvre "Applications par defaut" de Windows (Windows interdit a une application de se definir elle-meme par defaut) ;
///  - interrupteur du menu "Ouvrir avec Resona" de l'Explorateur (dossiers et selection de sons).
/// </summary>
public sealed partial class SettingsPage
{
    private void InitWindowsIntegrationUi(AppSettings current)
    {
        bool fr = Strings.Current.IsFr;

        DefaultAppsButton.Content = fr ? "Définir Resona comme lecteur par défaut des sons" : "Set Resona as the default player for audio files";
        DefaultAppsHint.Text = fr
            ? "Ouvre les paramètres Windows : choisissez Resona pour le type de fichier voulu (mp3, flac...). Un double-clic sur un son le lira alors dans Resona, sans l'ajouter à la bibliothèque. Si Windows ne propose pas encore Resona, lancez l'application une fois puis réessayez."
            : "Opens Windows settings: pick Resona for the file type you want (mp3, flac...). Double-clicking a sound will then play it in Resona without adding it to the library. If Windows doesn't list Resona yet, launch the app once and try again.";

        ExplorerMenuSwitch.Header = fr ? "Menu « Ouvrir avec Resona » dans l'Explorateur" : "“Open with Resona” in the File Explorer menu";
        ExplorerMenuSwitch.OnContent = fr ? "Activé" : "On";
        ExplorerMenuSwitch.OffContent = fr ? "Désactivé" : "Off";
        ExplorerMenuSwitch.IsOn = current.ExplorerContextMenuEnabled;
        ExplorerMenuHint.Text = fr
            ? "Ajoute « Ouvrir avec Resona » au clic droit sur un dossier ou sur un ou plusieurs sons. Sous Windows 11, il se trouve dans « Afficher plus d'options »."
            : "Adds “Open with Resona” to the right-click menu of a folder or of one or more sounds. On Windows 11 it is under “Show more options”.";
    }

    private async void DefaultApps_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Resona est (re)declare dans le registre juste avant, pour apparaitre dans la liste de Windows.
            await System.Threading.Tasks.Task.Run(WindowsIntegration.Apply);
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:defaultapps"));
        }
        catch (Exception ex)
        {
            LogService.Write("WindowsIntegration", ex, "Ouverture des applications par defaut");
        }
    }

    private async void ExplorerMenu_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Current.ExplorerContextMenuEnabled = ExplorerMenuSwitch.IsOn;
        await App.Settings.SaveAsync();
        try { await System.Threading.Tasks.Task.Run(WindowsIntegration.Apply); }
        catch (Exception ex) { LogService.Write("WindowsIntegration", ex, "Menu de l'Explorateur"); }
    }
}
