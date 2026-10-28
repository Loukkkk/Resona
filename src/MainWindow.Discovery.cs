using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Resona.Models;
using Resona.Services;
using Windows.UI;

namespace Resona;

/// <summary>
/// Mode Discovery : entrée « Artistes similaires » du menu clic droit d'un son (BuildTrackMenu). Ouvre une fenêtre qui liste les
/// artistes proches (Deezer, voir DiscoveryService), indique ceux qui sont déjà dans la bibliothèque (avec un bouton pour écouter
/// leurs titres) et permet d'ouvrir les autres sur Deezer.
/// </summary>
public sealed partial class MainWindow
{
    private bool _similarArtistsOpen;

    private async Task ShowSimilarArtistsAsync(Track track)
    {
        if (_similarArtistsOpen || track == null) return;
        bool fr = Strings.Current.IsFr;
        string artist = track.Artist?.Trim() ?? string.Empty;
        _similarArtistsOpen = true;
        try
        {
            // Largeur fixe (contenu et fenêtre) : la liste ne doit pas faire "respirer" la boîte de dialogue.
            var host = new StackPanel { Width = 640, Spacing = 4 };
            var dialog = new ContentDialog
            {
                XamlRoot = ((UIElement)ContentFrame).XamlRoot,
                Title = fr ? "Artistes similaires" : "Similar artists",
                Content = new ScrollViewer
                {
                    MaxHeight = 480,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = host
                },
                CloseButtonText = fr ? "Fermer" : "Close",
                DefaultButton = ContentDialogButton.Close,
                CenterCloseButton = true
            };
            dialog.Resources["ContentDialogMinWidth"] = 700.0;
            dialog.Resources["ContentDialogMaxWidth"] = 700.0;

            dialog.Opened += async (s, e) => await LoadSimilarArtistsAsync(dialog, host, artist, fr);
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LogService.Write("Discovery", ex, "Artistes similaires");
        }
        finally
        {
            _similarArtistsOpen = false;
        }
    }

    private static TextBlock SimilarInfoText(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.75,
        Margin = new Thickness(0, 6, 0, 6)
    };

    private async Task LoadSimilarArtistsAsync(ContentDialog dialog, StackPanel host, string artist, bool fr)
    {
        host.Children.Clear();

        if (string.IsNullOrWhiteSpace(artist))
        {
            host.Children.Add(SimilarInfoText(fr ? "Ce son n'a pas d'artiste renseigné : impossible de chercher des artistes similaires." : "This track has no artist tag: cannot look for similar artists."));
            return;
        }

        dialog.Title = fr ? $"Artistes similaires à {artist}" : $"Artists similar to {artist}";

        var loading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 8) };
        loading.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
        loading.Children.Add(new TextBlock { Text = fr ? "Recherche sur Deezer…" : "Searching on Deezer…", VerticalAlignment = VerticalAlignment.Center });
        host.Children.Add(loading);

        RelatedArtistsResult result;
        try
        {
            result = await DiscoveryService.GetRelatedAsync(artist);
        }
        catch (Exception ex)
        {
            LogService.Write("Discovery", ex, $"Artistes similaires ({artist})");
            host.Children.Clear();
            host.Children.Add(SimilarInfoText(fr
                ? $"Impossible de contacter Deezer ({ex.Message}). Vérifie ta connexion internet puis réessaie."
                : $"Could not reach Deezer ({ex.Message}). Check your internet connection and try again."));
            return;
        }

        host.Children.Clear();
        if (!result.ArtistFound)
        {
            host.Children.Add(SimilarInfoText(fr ? $"« {artist} » est introuvable sur Deezer." : $"“{artist}” was not found on Deezer."));
            return;
        }
        if (result.Related.Count == 0)
        {
            host.Children.Add(SimilarInfoText(fr ? $"Deezer ne propose aucun artiste similaire à « {result.ArtistName} »." : $"Deezer has no similar artists for “{result.ArtistName}”."));
            return;
        }

        List<Track> librarySnapshot = _library.ToList();
        List<SimilarArtistResult> items = await Task.Run(() => DiscoveryService.Annotate(result.Related, librarySnapshot));

        int owned = items.Count(i => i.LibraryTracks.Count > 0);
        host.Children.Add(new TextBlock
        {
            Text = fr
                ? $"{items.Count} suggestions, dont {owned} dans ta bibliothèque. Source : Deezer."
                : $"{items.Count} suggestions, {owned} of them in your library. Source: Deezer.",
            FontSize = 12,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 6)
        });

        foreach (SimilarArtistResult item in items.Take(30))
        {
            host.Children.Add(BuildSimilarArtistRow(dialog, item, fr));
        }
    }

    private UIElement BuildSimilarArtistRow(ContentDialog dialog, SimilarArtistResult item, bool fr)
    {
        var grid = new Grid { ColumnSpacing = 12, Padding = new Thickness(6), Background = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)), CornerRadius = new CornerRadius(8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Photo ronde (une forme remplie par l'image : un Border arrondi ne rogne pas son contenu).
        var photo = new Grid { Width = 48, Height = 48 };
        var circle = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 48, Height = 48, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
        photo.Children.Add(circle);
        bool hasPicture = false;
        if (!string.IsNullOrEmpty(item.PictureUrl))
        {
            try
            {
                circle.Fill = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(item.PictureUrl)) { DecodePixelWidth = 96 },
                    Stretch = Stretch.UniformToFill
                };
                hasPicture = true;
            }
            catch { }
        }
        if (!hasPicture)
        {
            photo.Children.Add(new FontIcon { Glyph = "\uE77B", FontSize = 20, Opacity = 0.6 });
        }
        Grid.SetColumn(photo, 0);
        grid.Children.Add(photo);

        int count = item.LibraryTracks.Count;
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        texts.Children.Add(new TextBlock
        {
            Text = item.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });
        texts.Children.Add(new TextBlock
        {
            Text = count > 0
                ? (fr ? $"{count} titre(s) dans ta bibliothèque" : $"{count} track(s) in your library")
                : (fr ? "Pas dans ta bibliothèque" : "Not in your library"),
            FontSize = 12,
            Opacity = 0.65
        });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (count > 0)
        {
            var play = new Button { Content = new FontIcon { Glyph = "\uE768", FontSize = 14 }, Width = 36, Height = 36, Padding = new Thickness(0) };
            ToolTipService.SetToolTip(play, fr ? "Écouter ses titres de ta bibliothèque" : "Play its tracks from your library");
            play.Click += (s, e) =>
            {
                dialog.Hide();
                PlaySimilarArtistTracks(item, fr);
            };
            buttons.Children.Add(play);
        }

        var open = new Button { Content = new FontIcon { Glyph = "\uE774", FontSize = 14 }, Width = 36, Height = 36, Padding = new Thickness(0) };
        ToolTipService.SetToolTip(open, fr ? "Ouvrir sur Deezer" : "Open on Deezer");
        open.Click += async (s, e) =>
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.deezer.com/artist/{item.Id}")); }
            catch (Exception ex) { LogService.Write("Discovery", ex, "Ouverture de Deezer"); }
        };
        buttons.Children.Add(open);

        Grid.SetColumn(buttons, 2);
        grid.Children.Add(buttons);
        return grid;
    }

    private void PlaySimilarArtistTracks(SimilarArtistResult item, bool fr)
    {
        List<Track> tracks = item.LibraryTracks
            .OrderBy(t => t.Album ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Title ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (tracks.Count == 0) return;

        PlayTrack(tracks[0], tracks, false, false, (fr ? "Artiste : " : "Artist: ") + item.Name);
        EnableContinuousPlaybackIfOff();
    }
}
