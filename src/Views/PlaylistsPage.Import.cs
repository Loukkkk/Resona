using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Resona.Models;
using Resona.Services;

namespace Resona.Views;

/// <summary>
/// Import de playlists depuis un fichier d'export (Spotify CSV, Soundiiz, TuneMyMusic, TXT...) : appariement flou avec la bibliothèque,
/// relecture des correspondances incertaines, fichiers illisibles signalés sans arrêter les autres, liste des titres introuvables.
/// Les formats M3U / M3U8 gardent leur chemin habituel (App.PlaylistIO).
/// </summary>
public sealed partial class PlaylistsPage
{
    // Remplis pendant un import, affichés dans le message final (BuildImportDetails).
    private readonly List<string> _importWarnings = new();
    private readonly List<string> _importMissingNames = new();

    private async Task<(List<string>, List<string>, string?)> ResolveImportFileAsync(string path, List<Track> library, ContentDialog loadingDialog)
    {
        bool fr = Models.Strings.Current.IsFr;
        string ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext is not (".csv" or ".tsv" or ".txt"))
        {
            var m3u = await App.PlaylistIO.ImportAsync(path, library);
            var m3uResolved = m3u.Item1;
            var m3uMissing = m3u.Item2;
            if (m3uMissing.Count > 0)
            {
                // Chemins qui n'existent plus (dossier déplacé…) : on tente de retrouver les fichiers sur le PC.
                var m3uEntries = m3uMissing.Select(EntryFromMissingLine).ToList();
                var m3uFound = await FindMissingOnPcAsync(Path.GetFileName(path), m3uEntries, loadingDialog);
                if (m3uFound.Count > 0)
                {
                    var m3uSeen = new HashSet<string>(m3uResolved, StringComparer.OrdinalIgnoreCase);
                    foreach (string p in m3uFound.Values)
                        if (m3uSeen.Add(p)) m3uResolved.Add(p);
                    m3uMissing = m3uMissing.Where((_, i) => !m3uFound.ContainsKey(i)).ToList();
                }
            }
            return (m3uResolved, m3uMissing, m3u.Item3);
        }

        string fileName = Path.GetFileName(path);
        PlaylistImportAnalysis analysis;
        try
        {
            analysis = await PlaylistImportService.AnalyzeExportFileAsync(path, library);
        }
        catch (Exception ex)
        {
            _importWarnings.Add($"{fileName} : " + (fr ? "fichier illisible (" : "unreadable file (") + ex.Message + ")");
            return (new List<string>(), new List<string>(), null);
        }

        if (analysis.Matches.Count == 0)
        {
            _importWarnings.Add($"{fileName} : " + (fr ? "aucune piste reconnue dans ce fichier." : "no track recognised in this file."));
            return (new List<string>(), new List<string>(), null);
        }

        // Correspondances trouvées mais moyennement sûres : l'utilisateur les valide avant l'ajout.
        var rejected = new HashSet<PlaylistImportMatch>();
        var uncertain = analysis.Matches.Where(m => m.NeedsReview).ToList();
        if (uncertain.Count > 0)
        {
            loadingDialog.Hide();   // une seule boîte de dialogue à la fois
            try { rejected = await AskImportReviewAsync(fileName, uncertain); }
            finally { _ = loadingDialog.ShowAsync(); }
        }

        // Titres absents de la bibliothèque (ou dont la correspondance a été refusée) : on propose de les retrouver sur le PC.
        var unresolved = analysis.Matches.Where(m => m.Track == null || rejected.Contains(m)).ToList();
        var foundOnPc = new Dictionary<PlaylistImportMatch, string>();
        if (unresolved.Count > 0)
        {
            var byIndex = await FindMissingOnPcAsync(fileName, unresolved.Select(m => m.Entry).ToList(), loadingDialog);
            foreach (var kv in byIndex) foundOnPc[unresolved[kv.Key]] = kv.Value;
        }

        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var m in analysis.Matches)
        {
            if (m.Track != null && !rejected.Contains(m) && !string.IsNullOrWhiteSpace(m.Track.FilePath))
            {
                if (seen.Add(m.Track.FilePath)) resolved.Add(m.Track.FilePath);   // un titre présent deux fois dans le fichier n'est ajouté qu'une fois
            }
            else if (foundOnPc.TryGetValue(m, out string? pcPath))
            {
                if (seen.Add(pcPath)) resolved.Add(pcPath);   // retrouvé sur le PC : ajouté à la bibliothèque par l'import
            }
            else
            {
                missing.Add(m.Entry.ToString());
            }
        }
        _importMissingNames.AddRange(missing);
        return (resolved, missing, analysis.CoverPath);
    }

    /// <summary>Renvoie les correspondances que l'utilisateur REFUSE (décochées ; tout est refusé si la boîte est fermée).</summary>
    private async Task<HashSet<PlaylistImportMatch>> AskImportReviewAsync(string fileName, List<PlaylistImportMatch> uncertain)
    {
        bool fr = Models.Strings.Current.IsFr;
        var boxes = new List<(CheckBox Box, PlaylistImportMatch Match)>();
        var list = new StackPanel { Spacing = 10, Width = 600 };

        foreach (var m in uncertain.OrderBy(x => x.Score))
        {
            string fileSide = m.Entry.ToString();
            string libSide = string.IsNullOrWhiteSpace(m.Track!.Artist) ? m.Track.Title : $"{m.Track.Title} - {m.Track.Artist}";

            var text = new StackPanel { Spacing = 1 };
            text.Children.Add(new TextBlock { Text = (fr ? "Fichier : " : "File: ") + fileSide, TextWrapping = TextWrapping.Wrap, Opacity = 0.65, FontSize = 12 });
            text.Children.Add(new TextBlock { Text = (fr ? "Bibliothèque : " : "Library: ") + libSide + $"  ({Math.Round(m.Score * 100)} %)", TextWrapping = TextWrapping.Wrap });

            // Cochée d'office quand le score est proche de « sûr » ; à cocher soi-même en dessous.
            var box = new CheckBox { IsChecked = m.Score >= 0.78, Content = text, MinWidth = 0 };
            boxes.Add((box, m));
            list.Children.Add(box);
        }

        var header = new TextBlock
        {
            Text = fr
                ? $"{uncertain.Count} correspondance(s) incertaine(s) dans « {fileName} ». Décoche celles qui ne sont pas les bonnes :"
                : $"{uncertain.Count} uncertain match(es) in “{fileName}”. Untick the ones that are wrong:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Width = 600
        };
        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list });

        var dialog = new ContentDialog
        {
            Title = fr ? "Vérifier les correspondances" : "Check the matches",
            Content = root,
            PrimaryButtonText = fr ? "Importer la sélection" : "Import selection",
            CloseButtonText = fr ? "Tout ignorer" : "Skip all",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        dialog.Resources["ContentDialogMinWidth"] = 660.0;
        dialog.Resources["ContentDialogMaxWidth"] = 660.0;

        var result = await dialog.ShowAsync();
        var rejected = new HashSet<PlaylistImportMatch>();
        foreach (var (box, match) in boxes)
        {
            if (result != ContentDialogResult.Primary || box.IsChecked != true) rejected.Add(match);
        }
        return rejected;
    }

    /// <summary>Complément du message final : fichiers ignorés et titres introuvables (les 15 premiers).</summary>
    private string BuildImportDetails()
    {
        bool fr = Models.Strings.Current.IsFr;
        var sb = new StringBuilder();

        if (_importFoundOnPcCount > 0)
        {
            sb.Append("\n\n").Append(fr
                ? $"{_importFoundOnPcCount} titre(s) retrouvé(s) sur le PC et ajouté(s) à la bibliothèque."
                : $"{_importFoundOnPcCount} track(s) found on this PC and added to the library.");
            _importFoundOnPcCount = 0;
        }

        if (_importWarnings.Count > 0)
        {
            sb.Append("\n\n").Append(fr ? "Fichiers ignorés :" : "Skipped files:");
            foreach (string w in _importWarnings) sb.Append("\n• ").Append(w);
        }

        if (_importMissingNames.Count > 0)
        {
            sb.Append("\n\n").Append(fr ? "Introuvables dans la bibliothèque :" : "Not found in the library:");
            foreach (string n in _importMissingNames.Take(15)) sb.Append("\n• ").Append(n);
            if (_importMissingNames.Count > 15)
            {
                sb.Append("\n").Append(fr ? $"… et {_importMissingNames.Count - 15} autre(s)" : $"… and {_importMissingNames.Count - 15} more");
            }
        }
        return sb.ToString();
    }
}
