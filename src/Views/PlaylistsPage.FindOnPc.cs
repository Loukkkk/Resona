using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Resona.Models;
using Resona.Services;
using Windows.Foundation;

namespace Resona.Views;

/// <summary>
/// Import de playlist : quand des titres sont introuvables dans la bibliothèque, propose une analyse rapide du PC
/// (noms de fichiers uniquement) pour les retrouver. L'analyse est faite une seule fois par import, même avec plusieurs fichiers.
/// </summary>
public sealed partial class PlaylistsPage
{
    // Session d'import = l'instance de la boîte « Importation… » (une nouvelle à chaque import).
    private ContentDialog? _pcSessionOwner;
    private List<string>? _pcAudioIndex;
    private bool? _pcScanChoice;          // null = pas encore demandé ; true = analyse acceptée ; false = refusée/annulée pour cet import
    private int _importFoundOnPcCount;

    private static readonly Regex QuotedMissingLine = new("^\"(?<t>.+)\"(?:\\s+-\\s+(?<a>.+))?$", RegexOptions.Compiled);

    private void EnsurePcSession(ContentDialog owner)
    {
        if (ReferenceEquals(_pcSessionOwner, owner)) return;
        _pcSessionOwner = owner;
        _pcAudioIndex = null;
        _pcScanChoice = null;
        _importFoundOnPcCount = 0;
    }

    /// <summary>Ligne « introuvable » d'un M3U (« "Titre" - Artiste » ou chemin) → entrée de recherche.</summary>
    private static PlaylistImportEntry EntryFromMissingLine(string line)
    {
        var m = QuotedMissingLine.Match(line);
        if (m.Success)
        {
            return new PlaylistImportEntry
            {
                Title = m.Groups["t"].Value.Trim(),
                Artist = m.Groups["a"].Success ? m.Groups["a"].Value.Trim() : string.Empty,
                RawLine = line
            };
        }
        string name = Path.GetFileNameWithoutExtension(line.Replace('/', '\\'));
        return new PlaylistImportEntry { Title = string.IsNullOrWhiteSpace(name) ? line : name, RawLine = line };
    }

    /// <summary>
    /// Propose de retrouver les titres manquants sur le PC. Renvoie, par index dans <paramref name="entries"/>, le fichier validé par l'utilisateur.
    /// </summary>
    private async Task<Dictionary<int, string>> FindMissingOnPcAsync(string fileName, IReadOnlyList<PlaylistImportEntry> entries, ContentDialog loadingDialog)
    {
        var accepted = new Dictionary<int, string>();
        if (entries.Count == 0) return accepted;

        EnsurePcSession(loadingDialog);
        if (_pcScanChoice == false) return accepted;

        bool fr = Models.Strings.Current.IsFr;
        await HideDialogAsync(loadingDialog);   // une seule boîte de dialogue à la fois
        try
        {
            if (_pcScanChoice == null)
            {
                var ask = new ContentDialog
                {
                    Title = fr ? "Titres introuvables" : "Missing tracks",
                    Content = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Text = fr
                            ? $"{entries.Count} titre(s) de « {fileName} » ne sont pas dans ta bibliothèque.\n\nLancer une analyse rapide de ce PC pour les retrouver ? Seuls les noms de fichiers sont lus (dossiers de la bibliothèque, Musique, Téléchargements, Bureau, Documents)."
                            : $"{entries.Count} track(s) from “{fileName}” are not in your library.\n\nRun a quick scan of this PC to find them? Only file names are read (library folders, Music, Downloads, Desktop, Documents)."
                    },
                    PrimaryButtonText = fr ? "Analyser" : "Scan",
                    CloseButtonText = fr ? "Ignorer" : "Skip",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };
                _pcScanChoice = await ask.ShowAsync() == ContentDialogResult.Primary;
                if (_pcScanChoice != true) return accepted;
            }

            var index = _pcAudioIndex;
            if (index == null)
            {
                index = await ScanPcWithProgressAsync();
                if (index == null)
                {
                    _pcScanChoice = false;   // annulé ou en erreur : on ne redemande pas pour cet import
                    return accepted;
                }
                _pcAudioIndex = index;
            }

            List<PcFoundFile> found;
            try
            {
                found = await Task.Run(() => PcTrackFinder.Match(entries, index, CancellationToken.None));
            }
            catch (Exception ex)
            {
                LogService.Write("PlaylistImport", ex, "Appariement des fichiers du PC");
                return accepted;
            }
            if (found.Count == 0) return accepted;

            var rejected = await AskPcFindReviewAsync(fileName, found);
            foreach (var f in found)
            {
                if (!rejected.Contains(f)) accepted[f.EntryIndex] = f.FilePath;
            }
            _importFoundOnPcCount += accepted.Count;
            return accepted;
        }
        finally { _ = loadingDialog.ShowAsync(); }
    }

    /// <summary>Ferme une boîte et attend qu'elle soit réellement fermée (Hide() est ignoré si elle vient d'être réaffichée et n'est pas encore ouverte).</summary>
    private static async Task HideDialogAsync(ContentDialog dialog)
    {
        var closed = new TaskCompletionSource();
        TypedEventHandler<Microsoft.UI.Xaml.Controls.ContentDialog, ContentDialogClosedEventArgs>? handler = null;
        handler = (s, a) => { dialog.Closed -= handler; closed.TrySetResult(); };
        dialog.Closed += handler;

        for (int i = 0; i < 20 && !closed.Task.IsCompleted; i++)
        {
            dialog.Hide();
            await Task.WhenAny(closed.Task, Task.Delay(50));
        }
        if (!closed.Task.IsCompleted) dialog.Closed -= handler;   // jamais ouverte : rien à attendre
    }

    /// <summary>Analyse des fichiers audio du PC avec une boîte de progression annulable. Renvoie null si annulé ou en échec.</summary>
    private async Task<List<string>?> ScanPcWithProgressAsync()
    {
        bool fr = Models.Strings.Current.IsFr;
        using var cts = new CancellationTokenSource();

        var status = new TextBlock
        {
            Text = fr ? "Analyse en cours…" : "Scanning…",
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
        panel.Children.Add(new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(status);

        var dialog = new ContentDialog
        {
            Title = fr ? "Analyse du PC" : "Scanning this PC",
            Content = panel,
            CloseButtonText = fr ? "Annuler" : "Cancel",
            XamlRoot = this.XamlRoot
        };

        var progress = new Progress<(int Scanned, int Audio)>(p =>
            status.Text = fr ? $"{p.Audio} fichier(s) audio trouvé(s)…" : $"{p.Audio} audio file(s) found…");

        var scanTask = PcTrackFinder.ScanAudioFilesAsync(progress, cts.Token);
        var dialogTask = dialog.ShowAsync().AsTask();

        await Task.WhenAny(scanTask, dialogTask);
        if (scanTask.IsCompleted)
        {
            // Analyse terminée : on ferme la boîte (Hide peut être ignoré si elle n'est pas encore ouverte, d'où la boucle).
            while (!dialogTask.IsCompleted)
            {
                dialog.Hide();
                await Task.WhenAny(dialogTask, Task.Delay(50));
            }
        }
        else
        {
            cts.Cancel();   // l'utilisateur a cliqué sur Annuler
        }

        try { return await scanTask; }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex)
        {
            LogService.Write("PlaylistImport", ex, "Analyse du PC");
            return null;
        }
    }

    /// <summary>Relecture des fichiers trouvés. Renvoie ceux que l'utilisateur REFUSE (décochés ; tout est refusé si la boîte est fermée).</summary>
    private async Task<HashSet<PcFoundFile>> AskPcFindReviewAsync(string fileName, List<PcFoundFile> found)
    {
        bool fr = Models.Strings.Current.IsFr;
        var boxes = new List<(CheckBox Box, PcFoundFile Found)>();
        var list = new StackPanel { Spacing = 10, Width = 600 };

        foreach (var f in found.OrderBy(x => x.Score))
        {
            var text = new StackPanel { Spacing = 1 };
            text.Children.Add(new TextBlock { Text = (fr ? "Playlist : " : "Playlist: ") + f.Entry.ToString(), TextWrapping = TextWrapping.Wrap, Opacity = 0.65, FontSize = 12 });
            text.Children.Add(new TextBlock { Text = (fr ? "Trouvé : " : "Found: ") + Path.GetFileName(f.FilePath) + $"  ({Math.Round(f.Score * 100)} %)", TextWrapping = TextWrapping.Wrap });
            text.Children.Add(new TextBlock { Text = Path.GetDirectoryName(f.FilePath) ?? string.Empty, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.5, FontSize = 11 });

            var box = new CheckBox { IsChecked = f.Score >= 0.78, Content = text, MinWidth = 0 };
            boxes.Add((box, f));
            list.Children.Add(box);
        }

        var header = new TextBlock
        {
            Text = fr
                ? $"{found.Count} titre(s) retrouvé(s) sur ce PC pour « {fileName} ». Décoche ceux qui ne sont pas les bons :"
                : $"{found.Count} track(s) found on this PC for “{fileName}”. Untick the ones that are wrong:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Width = 600
        };
        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list });

        var dialog = new ContentDialog
        {
            Title = fr ? "Titres retrouvés sur le PC" : "Tracks found on this PC",
            Content = root,
            PrimaryButtonText = fr ? "Ajouter la sélection" : "Add selection",
            CloseButtonText = fr ? "Ignorer" : "Skip",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        dialog.Resources["ContentDialogMinWidth"] = 660.0;
        dialog.Resources["ContentDialogMaxWidth"] = 660.0;

        var result = await dialog.ShowAsync();
        var rejected = new HashSet<PcFoundFile>();
        foreach (var (box, f) in boxes)
        {
            if (result != ContentDialogResult.Primary || box.IsChecked != true) rejected.Add(f);
        }
        return rejected;
    }
}
