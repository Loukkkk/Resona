using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Resona.Models;
using Resona.Services;

namespace Resona.Views;

public sealed partial class AlbumsPage : Page
{
    private async void AICleanupBtn_Click(object sender, RoutedEventArgs e)
    {
        var items = _library
            .Where(t => !string.IsNullOrWhiteSpace(t.Album) 
                     && !t.Album.Equals("Unknown Album", StringComparison.OrdinalIgnoreCase) 
                     && !t.Album.Equals(Models.Strings.Current.CS_AlbumInconnu, StringComparison.OrdinalIgnoreCase) && !t.Album.Equals("Unknown album", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Album!)
            .Distinct()
            .ToList();
        await AIHelper.RunAICleanup(this, items, "Albums", _library, (track, mapping) => !string.IsNullOrEmpty(track.Album) && mapping.ContainsKey(track.Album.Trim()), (track, mapping) => 
        {
            if (!string.IsNullOrEmpty(track.Album) && mapping.TryGetValue(track.Album.Trim(), out var newAlbum))
            {
                App.Settings.Current.AlbumMappings[track.Album] = newAlbum;
            }
        }, () => {
            _builtLibraryHash = 0; // Force reload
            LoadData(_library);
        });
    }
}

public sealed partial class ArtistsPage : Page
{
    private async void AICleanupBtn_Click(object sender, RoutedEventArgs e)
    {
        var items = _library
            .Where(t => !string.IsNullOrWhiteSpace(t.Artist)
                     && !t.Artist.Equals("Unknown Artist", StringComparison.OrdinalIgnoreCase)
                     && !t.Artist.Equals("Artiste inconnu", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Artist!)
            .Distinct()
            .ToList();
        await AIHelper.RunAICleanup(this, items, "Artistes", _library, (track, mapping) => !string.IsNullOrEmpty(track.Artist) && mapping.ContainsKey(track.Artist.Trim()), (track, mapping) => 
        {
            if (!string.IsNullOrEmpty(track.Artist) && mapping.TryGetValue(track.Artist.Trim(), out var newArtist))
            {
                App.Settings.Current.ArtistMappings[track.Artist] = newArtist;
            }
        }, () => {
            _builtLibraryHash = 0; // Force reload
            LoadData(_library);
        });
    }
}

public sealed partial class GenresPage : Page
{
    private async void AICleanupBtn_Click(object sender, RoutedEventArgs e)
    {
        var items = _library.Select(t => $"{t.Artist} - {t.Title}").Distinct().ToList();
        await AIHelper.RunAICleanup(this, items, "Genres", _library, (track, mapping) => mapping.ContainsKey($"{track.Artist} - {track.Title}".Trim()), (track, mapping) => 
        {
            if (mapping.TryGetValue($"{track.Artist} - {track.Title}".Trim(), out var newGenre))
            {
                App.Settings.Current.GenreMappings[track.Genre] = newGenre;
            }
        }, () => {
            _builtLibraryHash = 0; // Force reload
            LoadData(_library);
        });
    }
}

public static class AIHelper
{
    private static bool _isDialogShowing = false;

    public static async Task<string?> ShowManualAIDialog(XamlRoot xamlRoot, string title, string instructions, string hiddenData)
    {
        if (_isDialogShowing) return null;
        _isDialogShowing = true;
        try
        {
            var promptBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = false,
                Text = instructions.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                Height = 150,
                Margin = new Thickness(0, 8, 0, 8)
            };
            var copyBtn = new Button { Content = Resona.Models.Strings.Current.CS_AIDialog_CopyBtn, Margin = new Thickness(0, 0, 0, 16) };
            copyBtn.Click += (s, e) =>
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(promptBox.Text + "\n\n" + hiddenData);
                Clipboard.SetContent(dataPackage);
                copyBtn.Content = Resona.Models.Strings.Current.CS_AIDialog_Copied;
            };

            var responseBox = new TextBox
            {
                PlaceholderText = Resona.Models.Strings.Current.CS_AIDialog_Placeholder,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = 150,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = Resona.Models.Strings.Current.CS_AIDialog_Step1, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(promptBox);
            panel.Children.Add(copyBtn);
            panel.Children.Add(new TextBlock { Text = Resona.Models.Strings.Current.CS_AIDialog_Step2, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = Resona.Models.Strings.Current.CS_AIDialog_Step3, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 0) });
            panel.Children.Add(responseBox);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = Resona.Models.Strings.Current.CS_Pages_AI_Appliquer,
                CloseButtonText = Resona.Models.Strings.Current.CS_Pages_AI_Annuler,
                XamlRoot = xamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                return responseBox.Text;
            }
            return null;
        }
        catch (System.Exception)
        {
            return null;
        }
        finally
        {
            _isDialogShowing = false;
        }
    }

    public static async Task RunAICleanup(Page page, List<string> items, string typeName, List<Track> library, Func<Track, Dictionary<string, string>, bool> shouldModify, Action<Track, Dictionary<string, string>> applyMapping, Action reloadUI)
    {
        if (!App.Settings.Current.AIEnabled || items.Count == 0) return;

        var promptParts = AIService.GenerateCleanupPromptParts(items, typeName);
        string? responseJson = await ShowManualAIDialog(page.XamlRoot, Models.Strings.Current.IsFr ? $"Nettoyage IA - {typeName}" : $"AI Cleanup - {typeName}", promptParts.Instructions, promptParts.JsonData);
        await Task.Delay(800); // Prevent WinUI ContentDialog overlap crash

        if (string.IsNullOrWhiteSpace(responseJson)) return;

        try
        {
            var mapping = AIService.ParseCleanupResponse(responseJson);

            if (mapping.Count > 0)
            {
                var loadingDialog = new ContentDialog
                {
                    Title = new TextBlock { Text = Resona.Models.Strings.Current.CS_AIDialog_Wait, HorizontalAlignment = HorizontalAlignment.Center },
                    Content = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children = {
                            new ProgressRing { IsActive = true, Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = Resona.Models.Strings.Current.CS_AIDialog_Analyzing, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center }
                        }
                    },
                    XamlRoot = page.XamlRoot
                };
                _ = loadingDialog.ShowAsync();
                await Task.Delay(100);

                var tracksToModify = library.Where(t => shouldModify(t, mapping)).ToList();
                if (tracksToModify.Count > 0)
                {
                    await Task.Run(async () => 
                    {
                        foreach (var track in tracksToModify) { applyMapping(track, mapping); }
                        // Do not save Tracks to DB, the AI mappings are stored in AppSettings!
                    });
                }
                loadingDialog.Hide();
                reloadUI();

                var completeDialog = new ContentDialog
                {
                    Title = Models.Strings.Current.IsFr ? "Termin\u00E9" : "Done",
                    Content = Models.Strings.Current.IsFr ? $"{typeName} nettoyés avec succès. {tracksToModify.Count} éléments modifiés." : $"{typeName} successfully cleaned up. {tracksToModify.Count} items modified.",
                    CloseButtonText = "OK",
                    XamlRoot = page.XamlRoot
                };
                _ = completeDialog.ShowAsync();
            }
            else 
            {
                var noChangeDialog = new ContentDialog
                {
                    Title = Models.Strings.Current.IsFr ? "Termin\u00E9" : "Done",
                    Content = Models.Strings.Current.IsFr ? "Aucune modification trouvée dans la réponse de l'IA." : "No modifications found in the AI response.",
                    CloseButtonText = "OK",
                    XamlRoot = page.XamlRoot
                };
                _ = noChangeDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            var errorDialog = new ContentDialog
            {
                Title = Models.Strings.Current.IsFr ? "Erreur" : "Error",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = page.XamlRoot
            };
            _ = errorDialog.ShowAsync();
        }
    }
}





