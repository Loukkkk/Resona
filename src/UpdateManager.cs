using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Resona
{
    public static class UpdateManager
    {
        public static async Task CheckForUpdatesAsync(XamlRoot xamlRoot, bool manualCheck)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.TryParseAdd("Resona/1.0");
                var response = await http.GetStringAsync("https://api.github.com/repos/Loukkkk/Resona/releases/latest");
                var doc = JsonDocument.Parse(response);
                
                if (doc.RootElement.TryGetProperty("tag_name", out var tagElem))
                {
                    string latestTag = tagElem.GetString() ?? "";
                    
                    string currentTag = "v2.1";
                    bool hasUpdate = latestTag != currentTag && !string.IsNullOrEmpty(latestTag) && latestTag != "v1.0";
                    
                    if (hasUpdate)
                    {
                        if (manualCheck || latestTag != App.Settings.Current.LastKnownVersion)
                        {
                            var dialog = new ContentDialog
                            {
                                Title = Models.Strings.Current.IsFr ? "Mise à jour disponible" : "Update Available",
                                Content = Models.Strings.Current.IsFr ? $"La version {latestTag} est disponible !" : $"Version {latestTag} is available!",
                                PrimaryButtonText = Models.Strings.Current.IsFr ? "Télécharger" : "Download",
                                CloseButtonText = Models.Strings.Current.IsFr ? "Plus tard" : "Later",
                                XamlRoot = xamlRoot
                            };
                            var result = await dialog.ShowAsync();
                            if (result == ContentDialogResult.Primary)
                            {
                                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Loukkkk/Resona/releases/latest"));
                            }
                            App.Settings.Current.LastKnownVersion = latestTag;
                            App.Settings.SaveSync();
                        }
                    }
                    else if (manualCheck)
                    {
                        var dialog = new ContentDialog
                        {
                            Title = Models.Strings.Current.IsFr ? "À jour" : "Up to date",
                            Content = Models.Strings.Current.IsFr ? "Vous avez la dernière version." : "You have the latest version.",
                            CloseButtonText = "OK",
                            XamlRoot = xamlRoot
                        };
                        await dialog.ShowAsync();
                    }
                }
            }
            catch
            {
                if (manualCheck)
                {
                    var dialog = new ContentDialog
                    {
                        Title = Models.Strings.Current.IsFr ? "Erreur" : "Error",
                        Content = Models.Strings.Current.IsFr ? "Impossible de vérifier les mises à jour." : "Could not check for updates.",
                        CloseButtonText = "OK",
                        XamlRoot = xamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }
    }
}
