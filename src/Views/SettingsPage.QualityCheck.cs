using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Resona.Models;
using Resona.Services;
using Windows.UI;

namespace Resona.Views;

/// <summary>
/// Parametres > Bibliotheque musicale : « Controle qualite de la bibliotheque ».
/// Ouvre une fenetre qui analyse la bibliotheque (sons sans cover, fichiers corrompus ou introuvables, doublons, tags manquants /
/// parasites / incoherents) puis propose une correction rapide par probleme, ou pour toute une categorie (voir LibraryQualityService).
/// </summary>
public sealed partial class SettingsPage
{
    private bool _qualityCheckOpen;
    private CancellationTokenSource? _qualityCts;

    private const int QualityMaxRowsPerCategory = 50;

    private sealed class QualityRowUi
    {
        public Grid Root = null!;
        public Button? FixButton;
        public CheckBox? WriteBox;   // corrections de tags : "Ecrire dans le fichier" (decochee par defaut)
        public TextBlock Status = null!;
        public bool Busy;
        public bool Done;
    }

    private sealed class QualityCategoryUi
    {
        public QualityIssueKind Kind;
        public List<QualityIssue> Issues = new();
        public Dictionary<QualityIssue, QualityRowUi> Rows = new();
        public TextBlock CountText = null!;
        public Button? FixAllButton;
        public string FixAllLabel = string.Empty;
        public bool WriteToFiles;    // valeur de la categorie (aussi appliquee aux lignes non affichees)
        public int Remaining;
    }

    private async void QualityCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_qualityCheckOpen || App.MainWindowInstance == null) return;
        _qualityCheckOpen = true;
        bool fr = Models.Strings.Current.IsFr;
        var cts = new CancellationTokenSource();
        _qualityCts = cts;
        try
        {
            // Largeur fixe (contenu et fenetre) : la liste ne doit pas faire "respirer" la boite de dialogue.
            var host = new StackPanel { Width = 640, Spacing = 8 };
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = Models.Strings.Current.QualityCheck_Title,
                Content = new ScrollViewer
                {
                    MaxHeight = 520,
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

            dialog.Opened += async (s, a) => await RunQualityAnalysisAsync(dialog, host, fr, cts.Token);
            dialog.Closed += (s, a) => cts.Cancel();
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LogService.Write("QualityCheck", ex, "Controle qualite");
        }
        finally
        {
            cts.Cancel();
            _qualityCheckOpen = false;
        }
    }

    private async Task RunQualityAnalysisAsync(ContentDialog dialog, StackPanel host, bool fr, CancellationToken ct)
    {
        host.Children.Clear();

        List<Track> library = App.MainWindowInstance?.GetLibrarySnapshot() ?? new List<Track>();
        if (library.Count == 0)
        {
            host.Children.Add(QualityInfoText(fr ? "La bibliothèque est vide : il n'y a rien à analyser." : "The library is empty: there is nothing to analyze."));
            return;
        }

        var label = new TextBlock { Text = fr ? "Analyse de la bibliothèque…" : "Analyzing the library…", Margin = new Thickness(0, 6, 0, 0) };
        var bar = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, library.Count), Value = 0 };
        var counter = new TextBlock { FontSize = 12, Opacity = 0.65 };
        host.Children.Add(label);
        host.Children.Add(bar);
        host.Children.Add(counter);

        var progress = new Progress<(int Done, int Total)>(p =>
        {
            bar.Maximum = Math.Max(1, p.Total);
            bar.Value = p.Done;
            counter.Text = p.Done + " / " + p.Total;
        });

        QualityReport report;
        try
        {
            report = await LibraryQualityService.AnalyzeAsync(library, progress, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            LogService.Write("QualityCheck", ex, "Analyse de la bibliotheque");
            host.Children.Clear();
            host.Children.Add(QualityInfoText(fr
                ? "L'analyse a échoué (voir le journal dans Paramètres > Journal)."
                : "The analysis failed (see the log in Settings > Log)."));
            return;
        }
        if (ct.IsCancellationRequested) return;

        RenderQualityReport(dialog, host, report, fr, ct);
    }

    private void RenderQualityReport(ContentDialog dialog, StackPanel host, QualityReport report, bool fr, CancellationToken ct)
    {
        host.Children.Clear();

        if (report.Issues.Count == 0)
        {
            host.Children.Add(QualityInfoText(fr
                ? $"Aucun problème détecté sur {report.AnalyzedTracks} son(s). Ta bibliothèque est en bonne santé."
                : $"No problem detected on {report.AnalyzedTracks} track(s). Your library is in good shape."));
            return;
        }

        host.Children.Add(new TextBlock
        {
            Text = fr
                ? $"{report.Issues.Count} problème(s) détecté(s) sur {report.AnalyzedTracks} son(s) analysé(s)."
                : $"{report.Issues.Count} problem(s) found across {report.AnalyzedTracks} analyzed track(s).",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Margin = new Thickness(0, 2, 0, 4)
        });

        foreach (var group in report.Issues.GroupBy(i => i.Kind).OrderBy(g => g.Key))
            host.Children.Add(BuildQualityCategory(dialog, group.Key, group.ToList(), fr, ct));
    }

    private static TextBlock QualityInfoText(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.75,
        Margin = new Thickness(0, 6, 0, 6)
    };

    private static string QualityKindTitle(QualityIssueKind kind, bool fr) => kind switch
    {
        QualityIssueKind.MissingFile => fr ? "Fichiers introuvables" : "Missing files",
        QualityIssueKind.Unreadable => fr ? "Fichiers corrompus ou illisibles" : "Corrupted or unreadable files",
        QualityIssueKind.Duplicate => fr ? "Doublons probables" : "Probable duplicates",
        QualityIssueKind.MissingCover => fr ? "Sons sans cover" : "Tracks without cover",
        QualityIssueKind.MissingTags => fr ? "Tags manquants" : "Missing tags",
        QualityIssueKind.NoisyTags => fr ? "Tags « sales »" : "Messy tags",
        QualityIssueKind.InconsistentSpelling => fr ? "Orthographes incohérentes" : "Inconsistent spelling",
        QualityIssueKind.InconsistentAlbum => fr ? "Albums incohérents" : "Inconsistent albums",
        _ => kind.ToString()
    };

    private static string QualityKindDescription(QualityIssueKind kind, bool fr) => kind switch
    {
        QualityIssueKind.MissingFile => fr
            ? "Ces sons sont dans la bibliothèque mais leur fichier n'existe plus sur le disque. Correction : les retirer de l'app."
            : "These tracks are in the library but their file no longer exists on disk. Fix: remove them from the app.",
        QualityIssueKind.Unreadable => fr
            ? "Fichiers vides, tronqués ou dont l'audio est illisible. Correction : les retirer de l'app (les fichiers ne sont pas supprimés du disque)."
            : "Empty, truncated or unreadable audio files. Fix: remove them from the app (files are not deleted from disk).",
        QualityIssueKind.Duplicate => fr
            ? "Même artiste, même titre et durée quasi identique. Correction : retirer l'exemplaire en trop de l'app (le plus gros fichier est conservé, rien n'est supprimé du disque)."
            : "Same artist, same title and nearly identical duration. Fix: remove the extra copy from the app (the largest file is kept, nothing is deleted from disk).",
        QualityIssueKind.MissingCover => fr
            ? "Aucune pochette associée. Correction : chercher d'abord dans les fichiers du même album, puis en ligne."
            : "No cover art attached. Fix: look inside the album's files first, then online.",
        QualityIssueKind.MissingTags => fr
            ? "Artiste absent ou titre identique au nom du fichier. Correction possible quand le fichier est nommé « Artiste - Titre »."
            : "Missing artist or title identical to the file name. A fix is possible when the file is named “Artist - Title”.",
        QualityIssueKind.NoisyTags => fr
            ? "Mentions parasites dans les tags (« Official Video », « - Topic », nom de l'artiste répété dans le titre…). Correction : nettoyer."
            : "Junk in tags (“Official Video”, “- Topic”, artist name repeated in the title…). Fix: clean up.",
        QualityIssueKind.InconsistentSpelling => fr
            ? "Même artiste, album ou genre écrit de plusieurs façons (majuscules, accents, espaces). Correction : unifier avec l'orthographe la plus utilisée."
            : "Same artist, album or genre written in several ways (case, accents, spacing). Fix: unify with the most common spelling.",
        QualityIssueKind.InconsistentAlbum => fr
            ? "Dans un même album : années différentes ou artiste de l'album absent sur certains sons. Correction : harmoniser."
            : "Within the same album: different years or album artist missing on some tracks. Fix: harmonize.",
        _ => string.Empty
    };

    private UIElement BuildQualityCategory(ContentDialog dialog, QualityIssueKind kind, List<QualityIssue> issues, bool fr, CancellationToken ct)
    {
        var cat = new QualityCategoryUi { Kind = kind, Issues = issues, Remaining = issues.Count };
        var root = new StackPanel { Spacing = 4 };

        // ---- En-tete : zone cliquable (plier / deplier) + bouton "tout corriger" ----
        var header = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255))
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var toggleArea = new Grid { ColumnSpacing = 8, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        toggleArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toggleArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var chevron = new FontIcon { Glyph = "\uE76C", FontSize = 12, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
        toggleArea.Children.Add(chevron);

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(new TextBlock { Text = QualityKindTitle(kind, fr), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        cat.CountText = new TextBlock { Opacity = 0.65, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(cat.CountText);
        Grid.SetColumn(titleRow, 1);
        toggleArea.Children.Add(titleRow);
        header.Children.Add(toggleArea);

        QualityFixKind catFixKind = issues.Select(i => i.Fix).FirstOrDefault(f => f != QualityFixKind.None);
        bool hasCatFix = catFixKind != QualityFixKind.None;
        if (hasCatFix)
        {
            cat.FixAllLabel = catFixKind == QualityFixKind.RemoveFromApp ? (fr ? "Tout retirer" : "Remove all")
                            : catFixKind == QualityFixKind.FetchCover ? (fr ? "Tout chercher" : "Find all")
                            : (fr ? "Tout corriger" : "Fix all");
            string confirmLabel = fr ? "Confirmer ?" : "Confirm?";
            bool needsConfirm = catFixKind != QualityFixKind.FetchCover; // retrait / ecriture dans les fichiers : double clic volontaire
            bool armed = false;
            var fixAll = new Button { Content = cat.FixAllLabel, Padding = new Thickness(10, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center };
            cat.FixAllButton = fixAll;

            var revert = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            revert.Tick += (s, a) =>
            {
                revert.Stop();
                if (armed) { armed = false; fixAll.Content = cat.FixAllLabel; }
            };

            fixAll.Click += async (s, a) =>
            {
                if ((needsConfirm || (catFixKind == QualityFixKind.FetchCover && cat.WriteToFiles)) && !armed)
                {
                    armed = true;
                    fixAll.Content = confirmLabel;
                    revert.Start();
                    return;
                }
                revert.Stop();
                armed = false;
                await FixQualityCategoryAsync(cat, fr, ct);
            };
            Grid.SetColumn(fixAll, 1);
            header.Children.Add(fixAll);
        }
        root.Children.Add(header);
        UpdateQualityCount(cat, fr);

        // ---- Corps (replie par defaut) ----
        var body = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
        body.Children.Add(new TextBlock
        {
            Text = QualityKindDescription(kind, fr),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.65,
            Margin = new Thickness(4, 2, 4, 4)
        });
        bool hasWriteOption = issues.Any(i => i.Fix == QualityFixKind.WriteTags || i.Fix == QualityFixKind.FetchCover);
        if (hasWriteOption)
        {
            bool coverCat = kind == QualityIssueKind.MissingCover;
            body.Children.Add(new TextBlock
            {
                Text = coverCat
                    ? (fr
                        ? "Par défaut, la cover trouvée n'est associée que dans l'app (fichiers audio intacts). Coche « Intégrer dans le fichier » pour l'écrire aussi dans le fichier audio : c'est irréversible."
                        : "By default the cover found is only attached inside the app (audio files untouched). Tick “Embed in file” to also write it into the audio file: this cannot be undone.")
                    : (fr
                        ? "Par défaut, la correction ne change que l'affichage dans l'app (fichiers audio intacts, inclus dans l'export/import des données). Coche « Écrire dans le fichier » pour modifier aussi le fichier audio : c'est irréversible."
                        : "By default the fix only changes what the app displays (audio files untouched, included in the data export/import). Tick “Write to file” to also modify the audio file: this cannot be undone."),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.8,
                Margin = new Thickness(4, 0, 4, 2)
            });
            var allBox = new CheckBox
            {
                Content = coverCat
                    ? (fr ? "Intégrer dans les fichiers (toute la catégorie)" : "Embed in files (whole category)")
                    : (fr ? "Écrire dans les fichiers (toute la catégorie)" : "Write to files (whole category)"),
                IsChecked = false,
                Margin = new Thickness(4, 0, 4, 4)
            };
            allBox.Click += (s, a) =>
            {
                bool on = allBox.IsChecked == true;
                cat.WriteToFiles = on;
                foreach (var r in cat.Rows.Values)
                    if (r.WriteBox != null && r.WriteBox.IsEnabled) r.WriteBox.IsChecked = on;
            };
            body.Children.Add(allBox);
        }
        foreach (QualityIssue issue in issues.Take(QualityMaxRowsPerCategory))
            body.Children.Add(BuildQualityRow(dialog, cat, issue, fr, ct));
        if (issues.Count > QualityMaxRowsPerCategory)
        {
            int more = issues.Count - QualityMaxRowsPerCategory;
            body.Children.Add(new TextBlock
            {
                Text = fr
                    ? $"… et {more} autre(s)" + (hasCatFix ? $" (inclus dans « {cat.FixAllLabel} »)." : ".")
                    : $"… and {more} more" + (hasCatFix ? $" (included in “{cat.FixAllLabel}”)." : "."),
                FontSize = 12,
                Opacity = 0.65,
                Margin = new Thickness(4, 2, 4, 0)
            });
        }
        root.Children.Add(body);

        toggleArea.Tapped += (s, a) =>
        {
            bool open = body.Visibility != Visibility.Visible;
            body.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            chevron.Glyph = open ? "\uE70D" : "\uE76C";
        };

        return root;
    }

    private UIElement BuildQualityRow(ContentDialog dialog, QualityCategoryUi cat, QualityIssue issue, bool fr, CancellationToken ct)
    {
        var ui = new QualityRowUi();
        var grid = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(10, 6, 6, 6),
            Background = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)),
            CornerRadius = new CornerRadius(8)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ui.Root = grid;

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        texts.Children.Add(new TextBlock
        {
            Text = issue.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });
        if (!string.IsNullOrEmpty(issue.Detail))
        {
            texts.Children.Add(new TextBlock { Text = issue.Detail, FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap });
        }
        ui.Status = new TextBlock { FontSize = 12, Opacity = 0.9, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        texts.Children.Add(ui.Status);
        if (issue.Fix == QualityFixKind.WriteTags || issue.Fix == QualityFixKind.FetchCover)
        {
            ui.WriteBox = new CheckBox
            {
                Content = issue.Fix == QualityFixKind.FetchCover
                    ? (fr ? "Intégrer dans le fichier (irréversible)" : "Embed in file (cannot be undone)")
                    : (fr ? "Écrire dans le fichier (irréversible)" : "Write to file (cannot be undone)"),
                IsChecked = cat.WriteToFiles,
                MinHeight = 28,
                Margin = new Thickness(0, 2, 0, 0)
            };
            texts.Children.Add(ui.WriteBox);
        }
        grid.Children.Add(texts);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        if (issue.Fix != QualityFixKind.None)
        {
            var fix = new Button { Content = issue.FixLabel, Padding = new Thickness(10, 4, 10, 4) };
            fix.Click += async (s, a) =>
            {
                await FixQualityIssueAsync(cat, issue, fr, ct);
                App.MainWindowInstance?.RefreshAllLibraryViews();
            };
            ui.FixButton = fix;
            buttons.Children.Add(fix);
        }

        // Actions manuelles (un seul son concerne, fichier present)
        if (issue.Tracks.Count == 1 && issue.Kind != QualityIssueKind.MissingFile)
        {
            Track track = issue.Tracks[0];

            var edit = new Button { Content = new FontIcon { Glyph = "\uE70F", FontSize = 14 }, Width = 36, Height = 36, Padding = new Thickness(0) };
            ToolTipService.SetToolTip(edit, fr ? "Modifier les tags (ferme cette fenêtre)" : "Edit tags (closes this window)");
            edit.Click += async (s, a) =>
            {
                dialog.Hide();
                await Task.Delay(250); // laisse la boite de dialogue se fermer avant d'en ouvrir une autre
                if (App.MainWindowInstance != null)
                    await App.MainWindowInstance.ShowAutoTagDialogAsync(track); // editeur de tags (et non la bio Wikipedia de ShowTrackInfo)
            };
            buttons.Children.Add(edit);

            var folder = new Button { Content = new FontIcon { Glyph = "\uE838", FontSize = 14 }, Width = 36, Height = 36, Padding = new Thickness(0) };
            ToolTipService.SetToolTip(folder, fr ? "Afficher dans l'Explorateur" : "Show in File Explorer");
            folder.Click += (s, a) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + track.FilePath + "\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { LogService.Write("QualityCheck", ex, "Ouverture de l'Explorateur"); }
            };
            buttons.Children.Add(folder);
        }

        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        cat.Rows[issue] = ui;
        return grid;
    }

    private void UpdateQualityCount(QualityCategoryUi cat, bool fr)
    {
        cat.CountText.Text = cat.Remaining > 0
            ? (fr ? $"({cat.Remaining} à traiter)" : $"({cat.Remaining} to review)")
            : (fr ? "✓ terminé" : "✓ done");
        if (cat.FixAllButton != null && cat.Remaining == 0) cat.FixAllButton.IsEnabled = false;
    }

    private async Task FixQualityIssueAsync(QualityCategoryUi cat, QualityIssue issue, bool fr, CancellationToken ct)
    {
        cat.Rows.TryGetValue(issue, out QualityRowUi? row);
        if (row != null && (row.Busy || row.Done)) return;

        if (row != null)
        {
            row.Busy = true;
            if (row.FixButton != null) row.FixButton.IsEnabled = false;
            row.Status.Visibility = Visibility.Visible;
            row.Status.Text = fr ? "En cours…" : "Working…";
        }

        QualityFixResult result;
        bool wroteToFile = false;
        try
        {
            bool writeToFiles = (issue.Fix == QualityFixKind.WriteTags || issue.Fix == QualityFixKind.FetchCover) && (row?.WriteBox != null ? row.WriteBox.IsChecked == true : cat.WriteToFiles);
            wroteToFile = writeToFiles;
            if (row?.WriteBox != null) row.WriteBox.IsEnabled = false;
            result = await LibraryQualityService.ApplyFixAsync(issue, ct, writeToFiles);
        }
        catch (Exception ex)
        {
            LogService.Write("QualityCheck", ex, "Correction (" + issue.Kind + ")");
            result = new QualityFixResult(0, Math.Max(1, issue.Tracks.Count), 0);
        }

        bool success = result.Ok > 0 && result.Failed == 0 && result.Skipped == 0;
        if (row != null)
        {
            row.Busy = false;
            if (success)
            {
                row.Done = true;
                row.Status.Text = issue.Fix == QualityFixKind.WriteTags
                    ? (wroteToFile ? (fr ? "✓ Corrigé (fichier modifié)" : "✓ Fixed (file modified)") : (fr ? "✓ Corrigé dans l'app (fichier intact)" : "✓ Fixed in the app (file untouched)"))
                    : issue.Fix == QualityFixKind.FetchCover
                        ? (wroteToFile ? (fr ? "✓ Cover ajoutée (intégrée au fichier)" : "✓ Cover added (embedded in file)") : (fr ? "✓ Cover ajoutée dans l'app (fichier intact)" : "✓ Cover added in the app (file untouched)"))
                        : (fr ? "✓ Corrigé" : "✓ Fixed");
                row.Root.Opacity = 0.5;
                if (row.FixButton != null) row.FixButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (row.FixButton != null) row.FixButton.IsEnabled = true;
                if (row.WriteBox != null) row.WriteBox.IsEnabled = true;
                row.Status.Text = result.Failed == 0 && result.Skipped > 0
                    ? (fr ? "Ignoré (son en cours de lecture ou fenêtre fermée)." : "Skipped (track currently playing or window closed).")
                    : (fr ? "Échec : fichier verrouillé, en lecture seule, introuvable, ou aucune cover trouvée." : "Failed: file locked, read-only, not found, or no cover found.");
            }
        }

        if (success)
        {
            cat.Remaining = Math.Max(0, cat.Remaining - 1);
            UpdateQualityCount(cat, fr);
        }
    }

    private async Task FixQualityCategoryAsync(QualityCategoryUi cat, bool fr, CancellationToken ct)
    {
        if (cat.FixAllButton == null) return;
        cat.FixAllButton.IsEnabled = false;

        var pending = cat.Issues
            .Where(i => i.Fix != QualityFixKind.None && !(cat.Rows.TryGetValue(i, out var r) && r.Done))
            .ToList();

        int n = 0;
        foreach (QualityIssue issue in pending)
        {
            if (ct.IsCancellationRequested) break;
            n++;
            cat.FixAllButton.Content = n + " / " + pending.Count;
            await FixQualityIssueAsync(cat, issue, fr, ct);
            if (issue.Fix == QualityFixKind.FetchCover) await Task.Delay(800); // ne pas saturer les services de recherche de covers
        }

        App.MainWindowInstance?.RefreshAllLibraryViews();
        cat.FixAllButton.Content = cat.FixAllLabel;
        cat.FixAllButton.IsEnabled = cat.Remaining > 0;
    }
}
