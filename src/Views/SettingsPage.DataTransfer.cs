using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Resona.Models;
using Resona.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Resona.Views;

/// <summary>
/// Parametres : import / export des reglages et de la bibliotheque (section "Sauvegarde")
/// et mise a jour de yt-dlp (section "Mises a jour").
/// </summary>
public sealed partial class SettingsPage
{
    // =====================================================================
    //  Sauvegarde : import / export
    // =====================================================================

    private static IntPtr BackupWindowHandle => WindowNative.GetWindowHandle(App.MainWindowInstance);

    private static (string Name, string Desc) BackupCategoryText(BackupCategory c)
    {
        var tr = Strings.Current;
        return c switch
        {
            BackupCategory.Settings   => (tr.Backup_Cat_Settings,   tr.Backup_Cat_SettingsDesc),
            BackupCategory.Folders    => (tr.Backup_Cat_Folders,    tr.Backup_Cat_FoldersDesc),
            BackupCategory.Playlists  => (tr.Backup_Cat_Playlists,  tr.Backup_Cat_PlaylistsDesc),
            BackupCategory.Statistics => (tr.Backup_Cat_Statistics, tr.Backup_Cat_StatisticsDesc),
            BackupCategory.AiData     => (tr.Backup_Cat_Ai,         tr.Backup_Cat_AiDesc),
            BackupCategory.Lyrics     => (tr.Backup_Cat_Lyrics,     tr.Backup_Cat_LyricsDesc),
            BackupCategory.LyricsEdits => (tr.Backup_Cat_LyricsEdits, tr.Backup_Cat_LyricsEditsDesc),
            _ => (c.ToString(), ""),
        };
    }

    private void ShowBackupStatus(string? text)
    {
        BackupStatusText.Text = text ?? "";
        BackupStatusText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetBackupBusy(bool busy, string? text = null)
    {
        ExportBackupButton.IsEnabled = !busy;
        ImportBackupButton.IsEnabled = !busy;
        if (busy) ShowBackupStatus(text);
    }

    /// <summary>Fenetre de choix des categories. Renvoie null si l'utilisateur annule.</summary>
    private async Task<List<BackupCategory>?> AskBackupCategoriesAsync(
        string title, string intro, IEnumerable<BackupCategory> categories, string primaryText,
        string? note = null, string? header = null)
    {
        var tr = Strings.Current;
        var panel = new StackPanel { Spacing = 10, MinWidth = 380 };

        if (!string.IsNullOrEmpty(header))
            panel.Children.Add(new TextBlock { Text = header, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, FontSize = 12 });
        panel.Children.Add(new TextBlock { Text = intro, TextWrapping = TextWrapping.Wrap });

        var boxes = new List<(BackupCategory Category, CheckBox Box)>();
        foreach (var c in categories)
        {
            var (name, desc) = BackupCategoryText(c);
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(new TextBlock { Text = name, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = desc, TextWrapping = TextWrapping.Wrap, Opacity = 0.6, FontSize = 12 });
            // Tout est coche par defaut.
            var box = new CheckBox { Content = content, IsChecked = true };
            boxes.Add((c, box));
            panel.Children.Add(box);
        }

        if (!string.IsNullOrEmpty(note))
            panel.Children.Add(new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, Opacity = 0.6, FontSize = 12 });

        var warning = new TextBlock { Text = tr.Backup_NothingSelected, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        panel.Children.Add(warning);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer { MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel },
            PrimaryButtonText = primaryText,
            CloseButtonText = tr.Backup_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        dialog.PrimaryButtonClick += (s, args) =>
        {
            if (!boxes.Any(b => b.Box.IsChecked == true)) { args.Cancel = true; warning.Visibility = Visibility.Visible; }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Category).ToList();
    }

    private async Task ShowBackupMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Strings.Current.Backup_Close,
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void ExportBackup_Click(object sender, RoutedEventArgs e)
    {
        var tr = Strings.Current;
        try
        {
            var cats = await AskBackupCategoriesAsync(tr.Backup_ExportTitle, tr.Backup_ChooseExport,
                Enum.GetValues<BackupCategory>(), tr.Backup_Export);
            if (cats == null) return;

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = System.IO.Path.GetFileNameWithoutExtension(DataTransferService.SuggestedFileName())
            };
            picker.FileTypeChoices.Add(tr.Backup_FileType, new List<string> { ".zip" });
            InitializeWithWindow.Initialize(picker, BackupWindowHandle);
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            SetBackupBusy(true, tr.Backup_Exporting);
            await DataTransferService.ExportAsync(file.Path, cats);
            ShowBackupStatus($"{tr.Backup_ExportDone} {file.Path}");
        }
        catch (Exception ex)
        {
            LogService.Write("Backup", ex, "Export");
            ShowBackupStatus($"{tr.Backup_ExportError} {ex.Message}");
        }
        finally
        {
            ExportBackupButton.IsEnabled = true;
            ImportBackupButton.IsEnabled = true;
        }
    }

    private async void ImportBackup_Click(object sender, RoutedEventArgs e)
    {
        var tr = Strings.Current;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".zip");
            InitializeWithWindow.Initialize(picker, BackupWindowHandle);
            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            var manifest = DataTransferService.ReadManifest(file.Path, out var available);
            if (manifest == null || available.Count == 0)
            {
                await ShowBackupMessageAsync(tr.Backup_ImportTitle, tr.Backup_ImportInvalid);
                return;
            }

            string header = string.Format(tr.Backup_ImportFrom, manifest.CreatedUtc.ToLocalTime().ToString("g"), manifest.AppVersion);
            var cats = await AskBackupCategoriesAsync(tr.Backup_ImportTitle, tr.Backup_ChooseImport,
                Enum.GetValues<BackupCategory>().Where(available.Contains), tr.Backup_Import, tr.Backup_ImportNote, header);
            if (cats == null) return;

            SetBackupBusy(true, tr.Backup_Importing);
            var report = await DataTransferService.ImportAsync(file.Path, cats);
            ShowBackupStatus(null);
            await ShowImportReportAsync(report, cats);
        }
        catch (Exception ex)
        {
            LogService.Write("Backup", ex, "Import");
            ShowBackupStatus($"{tr.Backup_ImportError} {ex.Message}");
        }
        finally
        {
            ExportBackupButton.IsEnabled = true;
            ImportBackupButton.IsEnabled = true;
        }
    }

    private async Task ShowImportReportAsync(BackupImportReport r, IReadOnlyCollection<BackupCategory> cats)
    {
        var tr = Strings.Current;
        var lines = new List<string>();
        if (cats.Contains(BackupCategory.Settings) && r.SettingsApplied) lines.Add(tr.Backup_ReportSettings);
        if (cats.Contains(BackupCategory.Folders)) lines.Add(string.Format(tr.Backup_ReportFolders, r.FoldersAdded, r.FoldersSkipped));
        if (cats.Contains(BackupCategory.Playlists)) lines.Add(string.Format(tr.Backup_ReportPlaylists, r.PlaylistsImported, r.PlaylistTracksAdded, r.PlaylistTracksMissing));
        if (cats.Contains(BackupCategory.Statistics)) lines.Add(string.Format(tr.Backup_ReportStats, r.StatsUpdated));
        if (cats.Contains(BackupCategory.AiData))
        {
            lines.Add(string.Format(tr.Backup_ReportAi, r.AiMappingsMerged));
            if (r.DisplayEditsApplied > 0 || r.DisplayEditsMissing > 0) lines.Add(string.Format(tr.Backup_ReportDisplayEdits, r.DisplayEditsApplied, r.DisplayEditsMissing));
        }
        if (cats.Contains(BackupCategory.Lyrics)) lines.Add(string.Format(tr.Backup_ReportLyrics, r.LyricsApplied));
        if (cats.Contains(BackupCategory.LyricsEdits)) lines.Add(string.Format(tr.Backup_ReportLyricsEdits, r.LyricsEditsApplied, r.LyricsEditsKept, r.LyricsEditsMissing));

        var panel = new StackPanel { Spacing = 8, MinWidth = 380 };
        panel.Children.Add(new TextBlock { Text = string.Join("\n", lines), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(r.SafetyBackupPath))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"{tr.Backup_ReportSafety}\n{r.SafetyBackupPath}",
                TextWrapping = TextWrapping.Wrap, Opacity = 0.6, FontSize = 12, IsTextSelectionEnabled = true
            });
        }
        panel.Children.Add(new TextBlock { Text = tr.Backup_RestartPrompt, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });

        var dialog = new ContentDialog
        {
            Title = tr.Backup_ImportDoneTitle,
            Content = new ScrollViewer { MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel },
            PrimaryButtonText = tr.Backup_RestartNow,
            CloseButtonText = tr.Backup_Later,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) RestartApplication();
    }

    /// <summary>Relance l'application : le nouveau processus demarre apres 2 s, le temps que celui-ci se ferme.</summary>
    private static void RestartApplication()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & start \"\" \"{exe}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            Application.Current.Exit();
        }
        catch (Exception ex) { LogService.Write("Backup", ex, "Redemarrage"); }
    }

    // =====================================================================
    //  Mises a jour : yt-dlp
    // =====================================================================

    private bool _ytDlpUiInit;

    private async void YtDlpAutoUpdateSwitch_Loaded(object sender, RoutedEventArgs e)
    {
        _ytDlpUiInit = true;
        try { YtDlpAutoUpdateSwitch.IsOn = App.Settings.Current.AutoUpdateYtDlp; }
        finally { _ytDlpUiInit = false; }
        await RefreshYtDlpInfoAsync();
    }

    private async void YtDlpAutoUpdateSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_ytDlpUiInit) return;
        bool on = YtDlpAutoUpdateSwitch.IsOn;
        if (App.Settings.Current.AutoUpdateYtDlp == on) return;

        App.Settings.Current.AutoUpdateYtDlp = on;
        App.Settings.SaveSync();

        if (on)
        {
            // On verifie tout de suite (la verification respecte quand meme la limite d'une fois par 24 h).
            try { await DownloadService.AutoUpdateYtDlpIfDueAsync(); }
            catch (Exception ex) { LogService.Write("YtDlp", ex, "Mise a jour automatique de yt-dlp"); }
            await RefreshYtDlpInfoAsync();
        }
    }

    private async void YtDlpUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var tr = Strings.Current;
        YtDlpUpdateButton.IsEnabled = false;
        ShowYtDlpStatus(tr.YtDlp_Updating);
        try
        {
            var result = await DownloadService.UpdateYtDlpAsync();
            string message = result.Status switch
            {
                DownloadService.YtDlpUpdateStatus.Updated      => string.Format(tr.YtDlp_Updated, result.Version),
                DownloadService.YtDlpUpdateStatus.UpToDate     => tr.YtDlp_UpToDate,
                DownloadService.YtDlpUpdateStatus.Busy         => tr.YtDlp_Busy,
                DownloadService.YtDlpUpdateStatus.NotInstalled => tr.YtDlp_NotInstalled,
                _                                              => tr.YtDlp_Failed,
            };

            if (result.Status is DownloadService.YtDlpUpdateStatus.Updated or DownloadService.YtDlpUpdateStatus.UpToDate)
            {
                App.Settings.Current.LastYtDlpUpdateCheckUtc = DateTime.UtcNow.ToString("o");
                App.Settings.SaveSync();
            }
            ShowYtDlpStatus(message);
        }
        catch (Exception ex)
        {
            LogService.Write("YtDlp", ex, "Mise a jour manuelle de yt-dlp");
            ShowYtDlpStatus(tr.YtDlp_Failed);
        }
        await RefreshYtDlpInfoAsync();
    }

    private void ShowYtDlpStatus(string text)
    {
        YtDlpStatusText.Text = text;
        YtDlpStatusText.Visibility = Visibility.Visible;
    }

    private async Task RefreshYtDlpInfoAsync()
    {
        var tr = Strings.Current;
        bool local = DownloadService.IsYtDlpLocal;
        string shown = tr.YtDlp_NotInstalled;
        if (local)
        {
            string version = await new DownloadService().GetVersionAsync();
            shown = version.Length > 0 ? version : "?";
        }
        YtDlpVersionText.Text = string.Format(tr.YtDlp_Version, shown);
        YtDlpUpdateButton.IsEnabled = local;
    }
}
