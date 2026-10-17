// ============================================================
//  PlaylistsPage.Share.cs  —  Resona
//  Menu clic droit d'une playlist : "Exporter en page web..." / "Exporter en image..." (partage : pochettes + liste des titres).
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Resona.Models;
using Resona.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Resona.Views;

public sealed partial class PlaylistsPage
{
    /// <summary>Ajoute les entrees d'export au menu clic droit (une seule playlist selectionnee).</summary>
    private void AddShareMenuItems(MenuFlyout menu, Playlist playlist, bool singleSelection)
    {
        if (!singleSelection) return;
        bool fr = Resona.Models.Strings.Current.IsFr;

        var webItem = new MenuFlyoutItem { Text = fr ? "Exporter en page web..." : "Export as web page...", Icon = new FontIcon { Glyph = "" } };
        webItem.Click += async (s, args) => await ShareExportAsync(playlist, asImage: false);

        var imageItem = new MenuFlyoutItem { Text = fr ? "Exporter en image..." : "Export as image...", Icon = new FontIcon { Glyph = "" } };
        imageItem.Click += async (s, args) => await ShareExportAsync(playlist, asImage: true);

        menu.Items.Add(webItem);
        menu.Items.Add(imageItem);
        if (!playlist.IsSystem) menu.Items.Add(new MenuFlyoutSeparator());
    }

    private async Task ShareExportAsync(Playlist playlist, bool asImage)
    {
        bool fr = Resona.Models.Strings.Current.IsFr;
        List<Track> tracks = ResolvePlaylisttracks(playlist);

        if (tracks.Count == 0)
        {
            await new ContentDialog
            {
                Title = fr ? "Playlist vide" : "Empty playlist",
                Content = fr ? "Aucune piste à exporter." : "No tracks to export.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        if (asImage) picker.FileTypeChoices.Add(fr ? "Image PNG" : "PNG image", new List<string> { ".png" });
        else picker.FileTypeChoices.Add(fr ? "Page web (HTML)" : "Web page (HTML)", new List<string> { ".html" });
        picker.SuggestedFileName = string.Concat((playlist.Name ?? "Playlist").Split(Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrEmpty(picker.SuggestedFileName)) picker.SuggestedFileName = "Playlist";

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        var busy = new ContentDialog
        {
            Title = fr ? "Export en cours..." : "Exporting...",
            Content = new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center },
            XamlRoot = this.XamlRoot
        };
        _ = busy.ShowAsync();

        try
        {
            string name = playlist.Name ?? "Playlist";
            string? cover = playlist.IsSystem ? null : playlist.CoverImagePath;

            // Generation hors du thread d'interface (decodage et redimensionnement des pochettes).
            byte[] data = await Task.Run(() =>
            {
                using var exporter = new PlaylistShareExporter(name, tracks, cover, fr);
                return asImage ? exporter.RenderPng() : Encoding.UTF8.GetBytes(exporter.BuildHtml());
            });

            await File.WriteAllBytesAsync(file.Path, data);
            busy.Hide();

            string note = "";
            if (asImage && tracks.Count > PlaylistShareExporter.MaxImageTracks)
            {
                note = fr
                    ? $"\nL'image montre les {PlaylistShareExporter.MaxImageTracks} premiers titres ; la page web contient toute la liste."
                    : $"\nThe image shows the first {PlaylistShareExporter.MaxImageTracks} tracks; the web page contains the full list.";
            }

            var done = new ContentDialog
            {
                Title = fr ? "Export terminé" : "Export complete",
                Content = file.Name + note,
                PrimaryButtonText = fr ? "Ouvrir" : "Open",
                CloseButtonText = fr ? "Fermer" : "Close",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };
            if (await done.ShowAsync() == ContentDialogResult.Primary)
            {
                await Windows.System.Launcher.LaunchFileAsync(file);
            }
        }
        catch (Exception ex)
        {
            busy.Hide();
            LogService.Write("PlaylistShare", ex, "Export de la playlist " + playlist.Name);
            await new ContentDialog
            {
                Title = fr ? "Erreur" : "Error",
                Content = (fr ? "Erreur lors de l'export : " : "Error during export: ") + ex.Message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }
}
