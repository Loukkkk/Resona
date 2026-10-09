using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// NB : les accents des textes sont ecrits en \uXXXX pour que ce fichier ne depende d'aucun encodage.
namespace Resona
{
    public static class UpdateManager
    {
        private const string ReleasesPage = "https://github.com/Loukkkk/Resona/releases/latest";

        private static bool Fr => Models.Strings.Current.IsFr;

        /// <summary>Fichier de la release GitHub proposable en telechargement direct.</summary>
        private sealed record UpdateAsset(string Name, string Url, long Size);

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

                    string currentTag = "v3.2";
                    bool hasUpdate = latestTag != currentTag && !string.IsNullOrEmpty(latestTag) && latestTag != "v1.0";

                    if (hasUpdate)
                    {
                        if (manualCheck || latestTag != App.Settings.Current.LastKnownVersion)
                        {
                            UpdateAsset? asset = PickAsset(doc.RootElement);

                            string message = Fr ? $"La version {latestTag} est disponible !" : $"Version {latestTag} is available!";
                            if (asset != null)
                            {
                                string size = FormatSize(asset.Size);
                                message += "\n" + asset.Name + (size.Length > 0 ? $" ({size})" : "");
                            }

                            var dialog = new ContentDialog
                            {
                                Title = Fr ? "Mise \u00e0 jour disponible" : "Update Available",
                                Content = message,
                                CloseButtonText = Fr ? "Plus tard" : "Later",
                                XamlRoot = xamlRoot
                            };

                            if (asset != null)
                            {
                                // Telechargement direct (choix de l'emplacement) + lien vers la page GitHub.
                                dialog.PrimaryButtonText = Fr ? "T\u00e9l\u00e9charger directement" : "Download directly";
                                dialog.SecondaryButtonText = Fr ? "Voir sur GitHub" : "View on GitHub";
                                dialog.DefaultButton = ContentDialogButton.Primary;
                            }
                            else
                            {
                                // Aucun fichier exploitable dans la release : comportement d'origine (page GitHub).
                                dialog.PrimaryButtonText = Fr ? "T\u00e9l\u00e9charger" : "Download";
                            }

                            var result = await dialog.ShowAsync();

                            App.Settings.Current.LastKnownVersion = latestTag;
                            App.Settings.SaveSync();

                            if (asset != null && result == ContentDialogResult.Primary)
                            {
                                await DownloadAssetAsync(xamlRoot, asset);
                            }
                            else if (result == ContentDialogResult.Secondary || (asset == null && result == ContentDialogResult.Primary))
                            {
                                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(ReleasesPage));
                            }
                        }
                    }
                    else if (manualCheck)
                    {
                        var dialog = new ContentDialog
                        {
                            Title = Fr ? "\u00c0 jour" : "Up to date",
                            Content = Fr ? "Vous avez la derni\u00e8re version." : "You have the latest version.",
                            CloseButtonText = "OK",
                            CenterCloseButton = true,
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
                        Title = Fr ? "Erreur" : "Error",
                        Content = Fr ? "Impossible de v\u00e9rifier les mises \u00e0 jour." : "Could not check for updates.",
                        CloseButtonText = "OK",
                        CenterCloseButton = true,
                        XamlRoot = xamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        // ------------------------------------------------------------------
        //  Choix du fichier de la release
        // ------------------------------------------------------------------

        private static UpdateAsset? PickAsset(JsonElement release)
        {
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            string ownArch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                _ => "x64",
            };

            UpdateAsset? best = null;
            int bestScore = 0;

            foreach (var item in assets.EnumerateArray())
            {
                string name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                string url = item.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                long size = item.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
                if (name.Length == 0 || url.Length == 0) continue;

                string lower = name.ToLowerInvariant();
                int score = Path.GetExtension(lower) switch
                {
                    ".exe" => 30,
                    ".msi" => 25,
                    ".msix" or ".appx" or ".msixbundle" => 20,
                    ".zip" => 10,
                    _ => -100,   // blockmap, sha256, yml, txt... : jamais proposes
                };
                if (score < 0) continue;

                // Architecture : bonus si elle correspond a la machine, malus si le nom en annonce une autre.
                bool has64 = lower.Contains("x64") || lower.Contains("x86_64") || lower.Contains("amd64");
                bool hasArm = lower.Contains("arm64");
                bool has86 = lower.Contains("x86") && !lower.Contains("x86_64");
                bool mentionsArch = has64 || hasArm || has86;
                bool matches = ownArch switch
                {
                    "arm64" => hasArm,
                    "x86" => has86,
                    _ => has64,
                };
                if (matches) score += 50;
                else if (mentionsArch) score -= 50;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new UpdateAsset(name, url, size);
                }
            }
            return best;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            return $"{bytes / 1048576.0:0.#} {(Fr ? "Mo" : "MB")}";
        }

        // ------------------------------------------------------------------
        //  Telechargement direct (explorateur de fichiers -> enregistrement avec progression)
        // ------------------------------------------------------------------

        private static async Task DownloadAssetAsync(XamlRoot xamlRoot, UpdateAsset asset)
        {
            Windows.Storage.StorageFile? file = null;
            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads,
                    SuggestedFileName = Path.GetFileNameWithoutExtension(asset.Name)
                };
                string ext = Path.GetExtension(asset.Name);
                if (string.IsNullOrEmpty(ext)) ext = ".zip";
                picker.FileTypeChoices.Add(Fr ? "Fichier de mise \u00e0 jour" : "Update file", new List<string> { ext });
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));

                file = await picker.PickSaveFileAsync();
                if (file == null) return;   // annule dans l'explorateur

                var statusText = new TextBlock
                {
                    Text = Fr ? "Pr\u00e9paration du t\u00e9l\u00e9chargement..." : "Preparing download...",
                    TextWrapping = TextWrapping.Wrap
                };
                var progressBar = new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    IsIndeterminate = true,
                    MinWidth = 320
                };
                var panel = new StackPanel { Spacing = 12 };
                panel.Children.Add(statusText);
                panel.Children.Add(progressBar);

                var progressDialog = new ContentDialog
                {
                    Title = Fr ? "T\u00e9l\u00e9chargement en cours" : "Downloading",
                    Content = panel,
                    CloseButtonText = Fr ? "Annuler" : "Cancel",
                    CenterCloseButton = true,
                    XamlRoot = xamlRoot
                };

                using var cts = new CancellationTokenSource();
                var opened = new TaskCompletionSource();
                progressDialog.Opened += (s, e) => opened.TrySetResult();
                progressDialog.CloseButtonClick += (s, e) => cts.Cancel();
                var dialogTask = progressDialog.ShowAsync().AsTask();

                bool completed = false;
                bool cancelled = false;
                string? error = null;

                try
                {
                    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                    http.DefaultRequestHeaders.UserAgent.TryParseAdd("Resona/1.0");
                    using var response = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    response.EnsureSuccessStatusCode();

                    long total = response.Content.Headers.ContentLength ?? asset.Size;
                    using var input = await response.Content.ReadAsStreamAsync(cts.Token);
                    using (var output = await file.OpenStreamForWriteAsync())
                    {
                        output.SetLength(0);
                        var buffer = new byte[81920];
                        long readTotal = 0;
                        int read;
                        var lastUiUpdate = Stopwatch.StartNew();
                        while ((read = await input.ReadAsync(buffer.AsMemory(), cts.Token)) > 0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                            readTotal += read;

                            if (lastUiUpdate.ElapsedMilliseconds >= 100)
                            {
                                lastUiUpdate.Restart();
                                if (total > 0)
                                {
                                    progressBar.IsIndeterminate = false;
                                    progressBar.Value = Math.Min(100.0, readTotal * 100.0 / total);
                                    statusText.Text = $"{readTotal / 1048576.0:0.#} / {total / 1048576.0:0.#} {(Fr ? "Mo" : "MB")}";
                                }
                                else
                                {
                                    statusText.Text = $"{readTotal / 1048576.0:0.#} {(Fr ? "Mo" : "MB")}";
                                }
                            }
                        }
                        await output.FlushAsync(cts.Token);
                    }
                    completed = true;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                finally
                {
                    // On attend que la fenetre soit vraiment ouverte avant de la fermer (telechargement tres court).
                    await Task.WhenAny(opened.Task, Task.Delay(2000));
                    try { progressDialog.Hide(); } catch { }
                    try { await dialogTask; } catch { }
                }

                if (!completed)
                {
                    try { await file.DeleteAsync(); } catch { }   // pas de fichier partiel laisse sur le disque
                    if (!cancelled && error != null)
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = Fr ? "Erreur" : "Error",
                            Content = (Fr ? "Le t\u00e9l\u00e9chargement a \u00e9chou\u00e9 : " : "The download failed: ") + error,
                            CloseButtonText = "OK",
                            CenterCloseButton = true,
                            XamlRoot = xamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                    return;
                }

                var doneDialog = new ContentDialog
                {
                    Title = Fr ? "T\u00e9l\u00e9chargement termin\u00e9" : "Download complete",
                    Content = (Fr ? "Fichier enregistr\u00e9 :\n" : "File saved:\n") + file.Path,
                    PrimaryButtonText = Fr ? "Ouvrir le dossier" : "Open folder",
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                if (await doneDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file.Path}\"") { UseShellExecute = true }); } catch { }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = Fr ? "Erreur" : "Error",
                        Content = (Fr ? "Impossible de t\u00e9l\u00e9charger la mise \u00e0 jour : " : "Could not download the update: ") + ex.Message,
                        CloseButtonText = "OK",
                        CenterCloseButton = true,
                        XamlRoot = xamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
                catch { }
            }
        }
    }
}
