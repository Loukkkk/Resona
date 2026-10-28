using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Resona.Models;
using Resona.Services;
using Windows.UI;

namespace Resona;

/// <summary>
/// Selecteur manuel de paroles : un bouton dans l'overlay des paroles ouvre une recherche libre sur LRCLIB
/// (par defaut "Artiste Titre", modifiable : pour une reprise on peut chercher l'original). Le choix est memorise
/// pour ce fichier (LyricsOverrideService) et prime ensuite sur toute recherche automatique.
/// </summary>
public sealed partial class MainWindow
{
    private bool _lyricsPickerOpen;

    /// <summary>Bouton rond a droite de la barre de decalage, en bas de l'overlay des paroles (meme style que la barre).</summary>
    private Button CreateLyricsPickerButton()
    {
        bool fr = Strings.Current.IsFr;
        var btn = new Button
        {
            Content = new FontIcon { Glyph = "\uE721", FontSize = 14, Opacity = 0.8 },
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(200, 22, 22, 28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
            BorderThickness = new Thickness(1)
        };
        // Style "leger" : on fixe nous-memes les couleurs des etats survol / appui. Sans ca, le modele par defaut du bouton
        // fait un bref eclair clair au debut du survol (transition vers sa couleur de survol translucide).
        btn.Resources["ButtonBackground"] = new SolidColorBrush(Color.FromArgb(200, 22, 22, 28));
        btn.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(215, 34, 34, 42));
        btn.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(225, 28, 28, 36));
        btn.Resources["ButtonBorderBrush"] = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
        btn.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        btn.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
        btn.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        btn.Resources["ButtonForegroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        // L'overlay se ferme au moindre clic : on empeche la propagation, comme pour la barre de decalage.
        btn.Tapped += (s, e) => { e.Handled = true; };
        btn.PointerPressed += (s, e) => { e.Handled = true; };
        btn.Click += async (s, e) => { await ShowLyricsPickerAsync(); };
        ToolTipService.SetToolTip(btn, fr ? "Choisir d'autres paroles" : "Choose different lyrics");
        return btn;
    }

    private static string FormatMmSs(double seconds)
    {
        if (seconds <= 0) return "?:??";
        var t = TimeSpan.FromSeconds(Math.Round(seconds));
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    private static ListViewItem BuildLyricsCandidateItem(LyricsCandidate c, Track track, bool fr)
    {
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(0, 6, 0, 6) };
        panel.Children.Add(new TextBlock
        {
            Text = $"{c.Artist} — {c.Title}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });

        string gap = string.Empty;
        if (track.Duration.TotalSeconds > 10.0 && c.DurationSec > 0.0)
        {
            double d = c.DurationSec - track.Duration.TotalSeconds;
            gap = $" ({(d >= 0 ? "+" : "−")}{Math.Abs(d):0} s)";
        }
        string kind = c.IsSynced ? (fr ? "synchronisées" : "synced") : (fr ? "texte seul" : "plain text");
        panel.Children.Add(new TextBlock { Text = $"{FormatMmSs(c.DurationSec)}{gap} · {kind}", FontSize = 11, Opacity = 0.6 });

        if (!string.IsNullOrWhiteSpace(c.Preview))
        {
            panel.Children.Add(new TextBlock
            {
                Text = c.Preview,
                FontSize = 12,
                Opacity = 0.5,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }
        return new ListViewItem { Content = panel };
    }

    private async System.Threading.Tasks.Task ShowLyricsPickerAsync()
    {
        if (_lyricsPickerOpen) return;
        if (_currentIndex < 0 || _currentIndex >= _library.Count) return;

        Track track = _library[_currentIndex];
        bool fr = Strings.Current.IsFr;
        _lyricsPickerOpen = true;
        try
        {
            string artist = SanitizeUnknown(track.Artist);
            string title = SanitizeUnknown(track.Title);
            string defaultQuery = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
            bool hasOverride = LyricsOverrideService.TryGet(track.FilePath, out _);

            var queryBox = new TextBox
            {
                Text = defaultQuery,
                PlaceholderText = fr ? "Artiste et titre" : "Artist and title",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            var searchBtn = new Button { Content = fr ? "Rechercher" : "Search", VerticalAlignment = VerticalAlignment.Center };
            var topRow = new Grid { ColumnSpacing = 8 };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(searchBtn, 1);
            topRow.Children.Add(queryBox);
            topRow.Children.Add(searchBtn);

            var hint = new TextBlock
            {
                Text = fr
                    ? "Pour une reprise, cherche le titre et l'artiste de l'original."
                    : "For a cover, search for the original song's title and artist.",
                FontSize = 11,
                Opacity = 0.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var status = new TextBlock { FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 6) };
            var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 340 };

            var root = new StackPanel { MinWidth = 480 };
            root.Children.Add(topRow);
            root.Children.Add(hint);
            root.Children.Add(status);
            root.Children.Add(list);

            var dialog = new ContentDialog
            {
                XamlRoot = ((UIElement)ContentFrame).XamlRoot,
                Title = fr ? "Choisir d'autres paroles" : "Choose different lyrics",
                Content = root,
                PrimaryButtonText = fr ? "Utiliser ces paroles" : "Use these lyrics",
                CloseButtonText = fr ? "Annuler" : "Cancel",
                // Pas de bouton par defaut : sinon le bouton "Utiliser" prend la couleur d'accent par intermittence pendant la
                // recherche (perte de focus du bouton Rechercher). Son style d'accent est fixe a la place.
                DefaultButton = ContentDialogButton.None,
                PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                IsPrimaryButtonEnabled = false
            };
            if (hasOverride)
            {
                dialog.SecondaryButtonText = fr ? "Revenir à la recherche auto" : "Back to automatic search";
            }

            var shown = new List<LyricsCandidate>();

            bool searching = false;
            void SetPrimaryEnabled(bool enabled)
            {
                if (dialog.IsPrimaryButtonEnabled != enabled) dialog.IsPrimaryButtonEnabled = enabled;
            }

            async System.Threading.Tasks.Task RunSearchAsync()
            {
                string q = queryBox.Text.Trim();
                if (q.Length == 0 || searching) return;
                searching = true;

                List<LyricsCandidate> results;
                try
                {
                    list.Items.Clear();
                    shown.Clear();
                    SetPrimaryEnabled(false);
                    status.Text = fr ? "Recherche en cours..." : "Searching...";
                    results = await App.Lyrics.SearchCandidatesAsync(q, track.Duration);
                }
                finally
                {
                    searching = false;
                }

                shown.AddRange(results);
                foreach (var c in results) list.Items.Add(BuildLyricsCandidateItem(c, track, fr));
                status.Text = results.Count == 0
                    ? (fr ? "Aucun résultat. Essaie un autre artiste ou titre." : "No result. Try another artist or title.")
                    : (fr ? $"{results.Count} résultat(s), les plus proches de la durée du morceau en premier." : $"{results.Count} result(s), closest to the track duration first.");
            }

            searchBtn.Click += async (s, e) => { await RunSearchAsync(); };
            queryBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    e.Handled = true;
                    await RunSearchAsync();
                }
            };
            list.SelectionChanged += (s, e) => { SetPrimaryEnabled(list.SelectedIndex >= 0); };
            dialog.Opened += async (s, e) => { await RunSearchAsync(); };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && list.SelectedIndex >= 0 && list.SelectedIndex < shown.Count)
            {
                ApplyPickedLyrics(track, shown[list.SelectedIndex]);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                ResetPickedLyrics(track);
            }
        }
        catch (Exception ex)
        {
            LogService.Write("Lyrics", $"Selecteur de paroles : {ex.Message}");
        }
        finally
        {
            _lyricsPickerOpen = false;
        }
    }

    private bool IsCurrentTrack(Track track)
        => _currentIndex >= 0 && _currentIndex < _library.Count && ReferenceEquals(_library[_currentIndex], track);

    private void ApplyPickedLyrics(Track track, LyricsCandidate c)
    {
        string text = c.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        LyricsOverrideService.Set(track.FilePath, text, c.IsSynced, $"{c.Artist} - {c.Title}");

        track.Lyrics = text;
        track.LyricsSynced = c.IsSynced;
        if (!track.IsExternal) { _ = App.Cache.UpdateTrackAsync(track); }

        if (IsCurrentTrack(track))
        {
            _lrcLines.Clear();
            _lrcCurrentIndex = -1;
            LoadLyricsAsync(text, c.IsSynced);
        }
    }

    private void ResetPickedLyrics(Track track)
    {
        LyricsOverrideService.Remove(track.FilePath);

        track.Lyrics = null;
        track.LyricsSynced = false;
        if (!track.IsExternal) { _ = App.Cache.UpdateTrackAsync(track); }

        if (IsCurrentTrack(track))
        {
            _lrcLines.Clear();
            _lrcCurrentIndex = -1;
            UpdateLyricsOverlayTrackInfo(track);
            _ = FindMissingLyricsAsync(track);
        }
    }
}
