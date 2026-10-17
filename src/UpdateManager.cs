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

        /// <summary>Tag de la version actuelle de l'app, deduit automatiquement de la version de l'assembly (balise Version du .csproj) :
        /// 3.2.0 -> "v3.2", 3.2.1 -> "v3.2.1". Sert a la verification des mises a jour ET aux nouveautes (patch note de cette version).</summary>
        public static string CurrentTag { get; } = ComputeCurrentTag();

        private static string ComputeCurrentTag()
        {
            var v = typeof(UpdateManager).Assembly.GetName().Version ?? new Version(0, 0);
            return v.Build > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}" : $"v{v.Major}.{v.Minor}";
        }

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

                    string currentTag = CurrentTag;
                    bool hasUpdate = latestTag != currentTag && !string.IsNullOrEmpty(latestTag) && latestTag != "v1.0";

                    if (hasUpdate)
                    {
                        if (manualCheck || latestTag != App.Settings.Current.LastKnownVersion)
                        {
                            UpdateAssets assets = PickAssets(doc.RootElement);
                            bool installedWithSetup = IsInstalledWithSetup();

                            // Deux boutons quand la release propose a la fois l'installateur et la version portable ;
                            // sinon un seul bouton de telechargement direct.
                            UpdateAsset? primary;
                            UpdateAsset? secondary = null;
                            if (assets.Installer != null && assets.Portable != null)
                            {
                                primary = assets.Installer;
                                secondary = assets.Portable;
                            }
                            else
                            {
                                primary = assets.Installer ?? assets.Portable ?? assets.Other;
                            }

                            // Contenu : message + une description par fichier propose + lien vers la page GitHub.
                            var panel = new StackPanel { Spacing = 12 };
                            panel.Children.Add(new TextBlock
                            {
                                Text = Fr ? $"La version {latestTag} est disponible !" : $"Version {latestTag} is available!",
                                TextWrapping = TextWrapping.Wrap
                            });

                            void AddAssetInfo(UpdateAsset asset, string title, string description, bool recommended)
                            {
                                var info = new StackPanel { Spacing = 2 };
                                info.Children.Add(new TextBlock
                                {
                                    Text = recommended ? title + (Fr ? " \u2014 recommand\u00e9" : " \u2014 recommended") : title,
                                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                    TextWrapping = TextWrapping.Wrap
                                });
                                info.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
                                string size = FormatSize(asset.Size);
                                info.Children.Add(new TextBlock
                                {
                                    Text = asset.Name + (size.Length > 0 ? $" ({size})" : ""),
                                    TextWrapping = TextWrapping.Wrap,
                                    Opacity = 0.6,
                                    FontSize = 12
                                });
                                panel.Children.Add(info);
                            }

                            if (secondary != null)
                            {
                                // Les deux fichiers : on explique la difference. Recommande = celui qui correspond a l'installation actuelle.
                                AddAssetInfo(primary!,
                                    Fr ? "Installateur (Setup)" : "Installer (Setup)",
                                    Fr ? "Installation propre : mise \u00e0 jour sur place, d\u00e9sinstallation depuis Windows."
                                       : "Clean install: updates in place, uninstall from Windows.",
                                    installedWithSetup);
                                AddAssetInfo(secondary,
                                    Fr ? "Version portable (zip)" : "Portable version (zip)",
                                    Fr ? "\u00c0 extraire o\u00f9 vous voulez, rien n'est install\u00e9."
                                       : "Extract it anywhere, nothing gets installed.",
                                    !installedWithSetup);
                            }
                            else if (primary != null)
                            {
                                string size = FormatSize(primary.Size);
                                panel.Children.Add(new TextBlock
                                {
                                    Text = primary.Name + (size.Length > 0 ? $" ({size})" : ""),
                                    TextWrapping = TextWrapping.Wrap,
                                    Opacity = 0.8
                                });
                            }

                            if (primary != null)
                            {
                                panel.Children.Add(new HyperlinkButton
                                {
                                    Content = Fr ? "Voir sur GitHub" : "View on GitHub",
                                    NavigateUri = new Uri(ReleasesPage),
                                    Padding = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Left
                                });
                            }

                            var dialog = new ContentDialog
                            {
                                Title = Fr ? "Mise \u00e0 jour disponible" : "Update Available",
                                Content = panel,
                                CloseButtonText = Fr ? "Plus tard" : "Later",
                                XamlRoot = xamlRoot
                            };

                            if (secondary != null)
                            {
                                dialog.PrimaryButtonText = Fr ? "Installateur (Setup)" : "Installer (Setup)";
                                dialog.SecondaryButtonText = Fr ? "Version portable (zip)" : "Portable (zip)";
                                dialog.DefaultButton = installedWithSetup ? ContentDialogButton.Primary : ContentDialogButton.Secondary;
                            }
                            else if (primary != null)
                            {
                                // Telechargement direct (choix de l'emplacement) + lien vers la page GitHub.
                                dialog.PrimaryButtonText = Fr ? "T\u00e9l\u00e9charger directement" : "Download directly";
                                dialog.DefaultButton = ContentDialogButton.Primary;
                            }
                            else
                            {
                                // Aucun fichier exploitable dans la release : comportement d'origine (page GitHub).
                                dialog.PrimaryButtonText = Fr ? "T\u00e9l\u00e9charger" : "Download";
                            }

                            // Une seule fenetre a la fois : si les nouveautes sont affichees, on attend leur fermeture.
                            await WhatsNewManager.DialogLock.WaitAsync();
                            ContentDialogResult result;
                            try { result = await dialog.ShowAsync(); }
                            finally { WhatsNewManager.DialogLock.Release(); }

                            App.Settings.Current.LastKnownVersion = latestTag;
                            App.Settings.SaveSync();

                            if (result == ContentDialogResult.Primary)
                            {
                                if (primary != null)
                                    await DownloadAssetAsync(xamlRoot, primary);
                                else
                                    _ = Windows.System.Launcher.LaunchUriAsync(new Uri(ReleasesPage));
                            }
                            else if (result == ContentDialogResult.Secondary && secondary != null)
                            {
                                await DownloadAssetAsync(xamlRoot, secondary);
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

        /// <summary>Fichiers proposes pour une release : installateur (Setup.exe), version portable (zip), ou autre paquet.</summary>
        private sealed record UpdateAssets(UpdateAsset? Installer, UpdateAsset? Portable, UpdateAsset? Other);

        private static UpdateAssets PickAssets(JsonElement release) => new(
            PickBest(release, ext => ext == ".exe"),
            PickBest(release, ext => ext == ".zip"),
            PickBest(release, ext => ext is ".msi" or ".msix" or ".appx" or ".msixbundle"));

        /// <summary>Meilleur fichier de la release parmi les extensions acceptees, selon l'architecture de la machine.</summary>
        private static UpdateAsset? PickBest(JsonElement release, Func<string, bool> accepts)
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
                // blockmap, sha256, yml, txt... : jamais proposes (seules les extensions demandees sont acceptees)
                if (!accepts(Path.GetExtension(lower))) continue;
                int score = 10;

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

        /// <summary>
        /// Vrai si Resona a ete installe avec l'installateur : Inno Setup place son desinstalleur (unins000.exe)
        /// a cote de l'executable. Une version zip portable n'en a pas.
        /// </summary>
        private static bool IsInstalledWithSetup()
        {
            try { return File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe")); }
            catch { return false; }
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
                // Installateur : on propose de le lancer tout de suite (il ferme Resona et se met a jour sur place).
                bool isInstaller = string.Equals(Path.GetExtension(file.Path), ".exe", StringComparison.OrdinalIgnoreCase);
                if (isInstaller)
                    doneDialog.SecondaryButtonText = Fr ? "Installer maintenant" : "Install now";

                var doneResult = await doneDialog.ShowAsync();
                if (doneResult == ContentDialogResult.Primary)
                {
                    try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file.Path}\"") { UseShellExecute = true }); } catch { }
                }
                else if (doneResult == ContentDialogResult.Secondary && isInstaller)
                {
                    try { Process.Start(new ProcessStartInfo(file.Path) { UseShellExecute = true }); }
                    catch (Exception ex) { Resona.Services.LogService.Write("Update", ex, "Lancement de l'installateur"); }
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
