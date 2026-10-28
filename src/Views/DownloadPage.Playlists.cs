using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Resona.Models;
using Resona.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Resona.Views;

// Telechargement de playlists : recherche de playlists, lien de playlist, choix des sons a telecharger.
public sealed partial class DownloadPage
{
    private bool _playlistSearchMode;
    private bool _isLoadingPlaylist;
    private string? _defaultSearchPlaceholder;
    private ToggleButton _searchModeToggle = null!;

    private static bool IsFr => Strings.Current.IsFr;

    // ------------------------------------------------------------------
    //  Bouton de bascule recherche normale <-> recherche de playlists
    // ------------------------------------------------------------------

    private void InitPlaylistUi()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new FontIcon { Glyph = "\uE8FD", FontSize = 13 });
        content.Children.Add(new TextBlock { Text = "Playlists", FontSize = 13 });

        _searchModeToggle = new ToggleButton
        {
            Content = content,
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(14, 0, 14, 0),
        };
        ToolTipService.SetToolTip(_searchModeToggle, IsFr
            ? "Basculer entre la recherche de morceaux et la recherche de playlists"
            : "Switch between track search and playlist search");
        _searchModeToggle.Checked += (s, e) => SetSearchMode(true);
        _searchModeToggle.Unchecked += (s, e) => SetSearchMode(false);

        PanelSearch.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_searchModeToggle, 2);
        PanelSearch.Children.Add(_searchModeToggle);
    }

    private void SetSearchMode(bool playlistMode)
    {
        _playlistSearchMode = playlistMode;
        _defaultSearchPlaceholder ??= SearchBox.PlaceholderText;
        SearchBox.PlaceholderText = playlistMode
            ? (IsFr ? "Rechercher une playlist (ex : lofi hip hop)" : "Search for a playlist (e.g. lofi hip hop)")
            : _defaultSearchPlaceholder;
        SearchResultsPanel.Children.Clear();
        SearchResultsPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = string.Empty;
    }

    // ------------------------------------------------------------------
    //  Recherche de playlists
    // ------------------------------------------------------------------

    private async Task SearchPlaylistsAsync(string text)
    {
        SearchResultsPanel.Children.Clear();
        SearchResultsPanel.Visibility = Visibility.Visible;
        StatusText.Text = IsFr ? "Recherche de playlists..." : "Searching playlists...";
        ProgressRing.IsActive = true;
        try
        {
            List<YtPlaylistHit> hits = await DownloadService.SearchPlaylistsAsync(text);
            SearchResultsPanel.Children.Clear();
            if (hits.Count == 0)
            {
                SearchResultsPanel.Children.Add(new TextBlock
                {
                    Text = IsFr ? "Aucune playlist trouvée." : "No playlist found.",
                    Opacity = 0.6,
                    FontSize = 13,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                });
                StatusText.Text = string.Empty;
                return;
            }

            foreach (YtPlaylistHit hit in hits)
            {
                var grid = new Grid
                {
                    ColumnSpacing = 12,
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
                    CornerRadius = new CornerRadius(8)
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = hit.Title,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 1,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                };
                var info = new TextBlock
                {
                    Text = hit.Info,
                    Opacity = 0.5,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                };
                var button = new Button
                {
                    Content = new FontIcon { Glyph = "\uE8FD", FontSize = 13 },
                    Background = (Brush)Application.Current.Resources["AppAccentBrush"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorInverseBrush"],
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6, 10, 6),
                    VerticalAlignment = VerticalAlignment.Center
                };
                ToolTipService.SetToolTip(button, IsFr ? "Choisir les sons à télécharger" : "Choose the tracks to download");
                string url = hit.Url;
                button.Click += async (s, e) => await OpenPlaylistFlowAsync(url);

                Grid.SetColumn(title, 0);
                Grid.SetColumn(info, 1);
                Grid.SetColumn(button, 2);
                grid.Children.Add(title);
                grid.Children.Add(info);
                grid.Children.Add(button);
                SearchResultsPanel.Children.Add(grid);
            }
            StatusText.Text = $"{hits.Count} playlists";
        }
        catch (Exception ex)
        {
            StatusText.Text = "\u274C " + ex.Message;
        }
        finally
        {
            ProgressRing.IsActive = false;
        }
    }

    // ------------------------------------------------------------------
    //  Lien colle dans l'onglet URL : video seule ou playlist ?
    // ------------------------------------------------------------------

    private enum UrlKind { Single, SingleWithMix, Playlist, VideoInPlaylist }

    private static UrlKind ClassifyUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri)) return UrlKind.Single;
        string host = uri.Host.ToLowerInvariant();
        string path = uri.AbsolutePath.ToLowerInvariant();

        if (host.Contains("youtube.com") || host.Contains("youtu.be"))
        {
            Match m = Regex.Match(uri.Query, @"[?&]list=([^&]+)");
            if (!m.Success) return UrlKind.Single;
            string list = m.Groups[1].Value;
            bool hasVideo = path == "/watch" || host.Contains("youtu.be") || Regex.IsMatch(uri.Query, @"[?&]v=");
            // Les mix (RD...) et radios (UL...) sont generes a la volee : on ne les traite pas comme des playlists.
            bool isMix = list.StartsWith("RD", StringComparison.OrdinalIgnoreCase) || list.StartsWith("UL", StringComparison.OrdinalIgnoreCase);
            if (isMix && hasVideo) return UrlKind.SingleWithMix;
            return hasVideo ? UrlKind.VideoInPlaylist : UrlKind.Playlist;
        }
        if (host.Contains("soundcloud.com") && path.Contains("/sets/")) return UrlKind.Playlist;
        return UrlKind.Single;
    }

    private async Task HandleUrlDownloadAsync(string url)
    {
        if (_isDownloading || _isLoadingPlaylist) return;

        switch (ClassifyUrl(url))
        {
            case UrlKind.Single:
                await StartDownloadAsync(url);
                return;
            case UrlKind.SingleWithMix:
                await StartDownloadAsync(url, null, true);
                return;
            case UrlKind.Playlist:
                await OpenPlaylistFlowAsync(url);
                return;
            case UrlKind.VideoInPlaylist:
                var dialog = new ContentDialog
                {
                    Title = IsFr ? "Vidéo ou playlist ?" : "Video or playlist?",
                    Content = IsFr
                        ? "Ce lien pointe vers une vidéo qui fait partie d'une playlist."
                        : "This link points to a video that belongs to a playlist.",
                    PrimaryButtonText = IsFr ? "Cette vidéo" : "This video",
                    SecondaryButtonText = IsFr ? "Choisir dans la playlist" : "Pick from the playlist",
                    CloseButtonText = IsFr ? "Annuler" : "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };
                ContentDialogResult r = await dialog.ShowAsync();
                if (r == ContentDialogResult.Primary) await StartDownloadAsync(url, null, true);
                else if (r == ContentDialogResult.Secondary) await OpenPlaylistFlowAsync(url);
                return;
        }
    }

    // ------------------------------------------------------------------
    //  Lecture de la playlist -> choix des sons -> telechargement
    // ------------------------------------------------------------------

    private async Task OpenPlaylistFlowAsync(string url)
    {
        if (_isDownloading)
        {
            StatusText.Text = IsFr ? "Un téléchargement est déjà en cours." : "A download is already running.";
            return;
        }
        if (_isLoadingPlaylist) return;

        _isLoadingPlaylist = true;
        ProgressRing.IsActive = true;
        StatusText.Text = IsFr ? "Lecture de la playlist..." : "Reading the playlist...";
        YtPlaylistInfo info;
        try
        {
            info = await DownloadService.ListPlaylistAsync(url);
        }
        catch (Exception ex)
        {
            StatusText.Text = "\u274C " + ex.Message;
            return;
        }
        finally
        {
            _isLoadingPlaylist = false;
            ProgressRing.IsActive = false;
        }

        if (info.Entries.Count == 0 || !info.Entries.Any(e => e.Available))
        {
            StatusText.Text = IsFr ? "Playlist vide ou inaccessible." : "Playlist empty or unavailable.";
            return;
        }
        StatusText.Text = string.Empty;

        List<YtPlaylistEntry>? selected = await ShowPlaylistPickerAsync(info);
        if (selected == null || selected.Count == 0) return;

        // Retour sur l'onglet URL pour afficher la progression, comme pour un morceau seul.
        UrlBox.Text = url;
        TabUrl_Click(this, new RoutedEventArgs());
        await StartDownloadAsync(url, CompressIndices(selected.Select(e => e.Index)), false);
    }

    /// <summary>[1,2,3,5,7,8] -> "1-3,5,7-8" (format --playlist-items de yt-dlp).</summary>
    private static string CompressIndices(IEnumerable<int> indices)
    {
        var sorted = indices.Distinct().OrderBy(i => i).ToList();
        var parts = new List<string>();
        int i = 0;
        while (i < sorted.Count)
        {
            int start = sorted[i], end = start;
            while (i + 1 < sorted.Count && sorted[i + 1] == end + 1) { end = sorted[i + 1]; i++; }
            parts.Add(start == end ? start.ToString() : $"{start}-{end}");
            i++;
        }
        return string.Join(",", parts);
    }

    // ------------------------------------------------------------------
    //  Interface de choix des sons
    // ------------------------------------------------------------------

    private async Task<List<YtPlaylistEntry>?> ShowPlaylistPickerAsync(YtPlaylistInfo info)
    {
        var rows = new List<(YtPlaylistEntry Entry, CheckBox Box)>();
        int availableCount = info.Entries.Count(e => e.Available);
        var primaryBrush = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        var counter = new TextBlock { Opacity = 0.7, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var list = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 14, 0) };

        string dialogTitle = string.IsNullOrWhiteSpace(info.Title) ? "Playlist" : info.Title;
        var dialog = new ContentDialog
        {
            // TextBlock tronque (…) : sinon un long titre deborde des deux cotes du dialogue.
            Title = new TextBlock { Text = dialogTitle, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1, MaxWidth = 600 },
            CloseButtonText = IsFr ? "Annuler" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        dialog.Resources["ContentDialogMinWidth"] = 680.0;
        dialog.Resources["ContentDialogMaxWidth"] = 680.0;

        void Refresh()
        {
            int n = rows.Count(r => r.Box.IsChecked == true);
            counter.Text = IsFr ? $"{n} / {availableCount} sélectionné(s)" : $"{n} / {availableCount} selected";
            dialog.PrimaryButtonText = (IsFr ? "Télécharger" : "Download") + $" ({n})";
            dialog.IsPrimaryButtonEnabled = n > 0;
        }

        foreach (YtPlaylistEntry entry in info.Entries)
        {
            var line = new Grid { ColumnSpacing = 8 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var idx = new TextBlock { Text = entry.Index.ToString(), Opacity = 0.45, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = primaryBrush };
            var title = new TextBlock { Text = entry.Title, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1, VerticalAlignment = VerticalAlignment.Center, Foreground = primaryBrush };
            var dur = new TextBlock { Text = entry.Duration, Opacity = 0.5, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = primaryBrush };
            Grid.SetColumn(idx, 0); Grid.SetColumn(title, 1); Grid.SetColumn(dur, 2);
            line.Children.Add(idx); line.Children.Add(title); line.Children.Add(dur);

            var box = new CheckBox
            {
                Content = line,
                Padding = new Thickness(6, 0, 0, 0),
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsChecked = entry.Available,
                IsEnabled = entry.Available,
            };
            if (!entry.Available)
                ToolTipService.SetToolTip(box, IsFr ? "Vidéo privée, supprimée ou inaccessible" : "Private, deleted or unavailable video");
            box.Click += (s, e) => Refresh();
            rows.Add((entry, box));
            list.Children.Add(box);
        }

        // Filtre + cocher / decocher (s'appliquent aux lignes visibles).
        var filter = new TextBox
        {
            PlaceholderText = IsFr ? "Filtrer les titres..." : "Filter titles...",
            MinWidth = 240,
            Foreground = (Brush)Application.Current.Resources["TextBoxForegroundBrush"],
        };
        filter.TextChanged += (s, e) =>
        {
            string f = filter.Text.Trim();
            foreach (var r in rows)
                r.Box.Visibility = f.Length == 0 || r.Entry.Title.Contains(f, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
        };

        var selectAll = new Button { Content = IsFr ? "Tout cocher" : "Select all" };
        selectAll.Click += (s, e) =>
        {
            foreach (var r in rows)
                if (r.Entry.Available && r.Box.Visibility == Visibility.Visible) r.Box.IsChecked = true;
            Refresh();
        };
        var selectNone = new Button { Content = IsFr ? "Tout décocher" : "Select none" };
        selectNone.Click += (s, e) =>
        {
            foreach (var r in rows)
                if (r.Box.Visibility == Visibility.Visible) r.Box.IsChecked = false;
            Refresh();
        };

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(filter);
        toolbar.Children.Add(selectAll);
        toolbar.Children.Add(selectNone);

        var scroller = new ScrollViewer
        {
            MaxHeight = 380,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = list
        };

        var host = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        host.Children.Add(toolbar);
        host.Children.Add(scroller);
        host.Children.Add(counter);
        dialog.Content = host;
        Refresh();

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        return rows.Where(r => r.Box.IsChecked == true && r.Entry.Available).Select(r => r.Entry).ToList();
    }
}
