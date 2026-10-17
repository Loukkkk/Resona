using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Resona.Models;
using Resona.Services;

// NB : les accents des textes sont ecrits en \uXXXX (ou viennent de Strings.cs) pour que ce fichier ne depende d'aucun encodage.
namespace Resona
{
    /// <summary>
    /// Nouveautes de l'application : recupere le patch note de la version actuelle sur la release GitHub
    /// et l'affiche dans une fenetre (bouton des Parametres, ou automatiquement au premier lancement apres une mise a jour).
    /// </summary>
    public static class WhatsNewManager
    {
        private const string Repo = "Loukkkk/Resona";
        private const string ReleasesListPage = "https://github.com/Loukkkk/Resona/releases";

        /// <summary>
        /// Verrou partage avec UpdateManager : WinUI n'autorise qu'un seul ContentDialog ouvert a la fois par XamlRoot.
        /// </summary>
        public static readonly SemaphoreSlim DialogLock = new(1, 1);

        private static bool _startupHandled;

        private static Strings S => Strings.Current;

        private sealed record ReleaseNotes(string Tag, string Title, string Body, string Url);

        // ------------------------------------------------------------------
        //  Points d'entree
        // ------------------------------------------------------------------

        /// <summary>
        /// Au demarrage : affiche automatiquement le patch note si c'est le premier lancement apres une mise a jour.
        /// Une premiere installation (pas de cache / de reglages existants) ne l'affiche pas.
        /// </summary>
        public static async Task ShowOnStartupIfNeededAsync(XamlRoot? xamlRoot)
        {
            if (_startupHandled || xamlRoot == null) return;
            _startupHandled = true;

            try
            {
                var settings = App.Settings.Current;
                string current = UpdateManager.CurrentTag;
                if (settings.LastSeenVersion == current) return;

                // Mise a jour = l'app existait deja : soit elle avait deja memorise une version, soit (premiere version
                // qui connait cette fonction) un cache / des reglages existaient avant ce lancement.
                bool isUpgrade = !string.IsNullOrEmpty(settings.LastSeenVersion)
                                 || (App.WasExistingInstall && settings.HasCompletedOnboarding);
                if (!isUpgrade)
                {
                    MarkSeen(current);   // toute premiere installation : rien a montrer, on memorise la version
                    return;
                }

                await Task.Delay(1500);  // laisse l'interface s'afficher d'abord

                // Pas de repli sur une autre release ici : l'affichage automatique ne concerne que la release de CETTE version.
                ReleaseNotes? notes = await FetchAsync(fallbackToLatest: false);
                if (notes == null) return;   // release introuvable : on reessaiera au prochain lancement

                if (string.IsNullOrWhiteSpace(notes.Body))
                {
                    MarkSeen(current);       // release sans notes : rien a afficher
                    return;
                }

                // On memorise la version AVANT d'afficher la fenetre : si l'app est fermee pendant que la fenetre est ouverte
                // (ou si l'affichage echoue), le patch note ne reapparait pas a chaque lancement suivant.
                MarkSeen(current);
                var result = await ShowQueuedAsync(() => BuildNotesDialog(xamlRoot, notes));
                await OpenIfPrimaryAsync(result, notes.Url);
            }
            catch (Exception ex)
            {
                // Hors ligne ou erreur reseau : silencieux, on reessaiera au prochain lancement.
                LogService.Write("WhatsNew", ex, "Affichage automatique des nouveautes");
            }
        }

        /// <summary>Bouton des Parametres : affiche toujours les nouveautes de la version actuelle (avec un message en cas de souci).</summary>
        public static async Task ShowAsync(XamlRoot xamlRoot)
        {
            ReleaseNotes? notes = null;
            bool failed = false;
            try
            {
                notes = await FetchAsync(fallbackToLatest: true);
            }
            catch (Exception ex)
            {
                failed = true;
                LogService.Write("WhatsNew", ex, "Recuperation des nouveautes");
            }

            if (notes != null && !string.IsNullOrWhiteSpace(notes.Body))
            {
                var result = await ShowQueuedAsync(() => BuildNotesDialog(xamlRoot, notes));
                await OpenIfPrimaryAsync(result, notes.Url);
                return;
            }

            string message = failed ? S.WhatsNew_Error : (notes == null ? S.WhatsNew_NotFound : S.WhatsNew_NoNotes);
            string url = notes?.Url is { Length: > 0 } u ? u : ReleasesListPage;
            var msgResult = await ShowQueuedAsync(() => BuildMessageDialog(xamlRoot, message));
            await OpenIfPrimaryAsync(msgResult, url);
        }

        private static void MarkSeen(string tag)
        {
            App.Settings.Current.LastSeenVersion = tag;
            App.Settings.SaveSync();
        }

        private static async Task OpenIfPrimaryAsync(ContentDialogResult result, string url)
        {
            if (result != ContentDialogResult.Primary) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            try { await Windows.System.Launcher.LaunchUriAsync(uri); } catch { }
        }

        // ------------------------------------------------------------------
        //  Recuperation du patch note (release GitHub du tag de la version actuelle)
        // ------------------------------------------------------------------

        /// <summary>Notes de la release du tag actuel. Null si cette release n'existe pas ; leve une exception en cas d'erreur reseau.</summary>
        private static async Task<ReleaseNotes?> FetchAsync(bool fallbackToLatest)
        {
            string tag = UpdateManager.CurrentTag;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.TryParseAdd("Resona/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await http.GetAsync($"https://api.github.com/repos/{Repo}/releases/tags/{tag}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // La release de la version actuelle n'existe pas (encore) sur GitHub : cas d'une version de developpement
                // ou d'une release pas encore publiee. On affiche alors le patch note de la derniere release publiee.
                return fallbackToLatest ? await FetchLatestPublishedAsync(http) : null;
            }
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return ParseRelease(doc.RootElement, tag);
        }

        /// <summary>Notes de la derniere release publiee (repli quand le tag de la version actuelle n'a pas de release). Null s'il n'y en a aucune.</summary>
        private static async Task<ReleaseNotes?> FetchLatestPublishedAsync(HttpClient http)
        {
            using var response = await http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string latestTag = doc.RootElement.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "" : "";
            return ParseRelease(doc.RootElement, latestTag);
        }

        private static ReleaseNotes ParseRelease(JsonElement root, string tag)
        {
            string Read(string name) =>
                root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : "";

            return new ReleaseNotes(tag, Read("name"), Read("body").Trim(), Read("html_url"));
        }

        // ------------------------------------------------------------------
        //  Fenetres
        // ------------------------------------------------------------------

        /// <summary>Affiche une fenetre en respectant le verrou (une seule a la fois) ; reessaie si WinUI en a deja une d'ouverte.</summary>
        private static async Task<ContentDialogResult> ShowQueuedAsync(Func<ContentDialog> factory)
        {
            await DialogLock.WaitAsync();
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    try { return await factory().ShowAsync(); }
                    catch (Exception) when (attempt < 4) { await Task.Delay(1500); }
                }
            }
            finally
            {
                DialogLock.Release();
            }
        }

        private static ContentDialog BuildNotesDialog(XamlRoot xamlRoot, ReleaseNotes notes)
        {
            var panel = new StackPanel { Spacing = 10 };

            // Titre de la release (ex. "Resona v3.3 - Nouveau theme") s'il apporte quelque chose de plus que le numero.
            string title = notes.Title.Trim();
            if (title.Length > 0 && !string.Equals(title, notes.Tag, StringComparison.OrdinalIgnoreCase))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            panel.Children.Add(new ScrollViewer
            {
                MaxHeight = 380,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, 12, 0),
                Content = BuildMarkdown(notes.Body)
            });

            var dialog = new ContentDialog
            {
                Title = S.WhatsNew_Title + " \u2014 " + notes.Tag,
                Content = panel,
                CloseButtonText = S.WhatsNew_Close,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            };
            if (!string.IsNullOrEmpty(notes.Url)) dialog.PrimaryButtonText = S.WhatsNew_ViewOnGitHub;
            return dialog;
        }

        private static ContentDialog BuildMessageDialog(XamlRoot xamlRoot, string message) => new ContentDialog
        {
            Title = S.WhatsNew_Title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = S.WhatsNew_ViewOnGitHub,
            CloseButtonText = S.WhatsNew_Close,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        // ------------------------------------------------------------------
        //  Mini rendu Markdown (titres, listes, gras, italique, code, liens) pour le corps de la release
        // ------------------------------------------------------------------

        private static readonly Regex HeadingRegex = new(@"^\s*(#{1,6})\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex BulletRegex = new(@"^(\s*)[-*+]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex NumberedRegex = new(@"^(\s*)(\d+)[.)]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex RuleRegex = new(@"^(-{3,}|\*{3,}|_{3,})$", RegexOptions.Compiled);
        private static readonly Regex InlineRegex = new(
            @"\*\*(?<b>.+?)\*\*|`(?<c>[^`]+)`|\[(?<lt>[^\]]+)\]\((?<lu>[^)\s]+)\)|(?<![\*\w])\*(?<i>[^*\s][^*]*?)\*(?![\*\w])|(?<u>https?://[^\s<>)]+)",
            RegexOptions.Compiled);

        private static RichTextBlock BuildMarkdown(string markdown)
        {
            var rtb = new RichTextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

            string text = Regex.Replace(markdown.Replace("\r\n", "\n"), "<!--.*?-->", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd();
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || RuleRegex.IsMatch(trimmed)) continue;

                var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };

                var heading = HeadingRegex.Match(line);
                var bullet = BulletRegex.Match(line);
                var numbered = NumberedRegex.Match(line);

                if (heading.Success)
                {
                    double size = heading.Groups[1].Length switch { 1 => 20, 2 => 17, 3 => 15, _ => 14 };
                    paragraph.Margin = new Thickness(0, 10, 0, 4);
                    AddInlines(paragraph, heading.Groups[2].Value.Trim(), size, true);
                }
                else if (bullet.Success)
                {
                    double indent = bullet.Groups[1].Value.Replace("\t", "    ").Length / 2 * 14;
                    paragraph.Margin = new Thickness(10 + indent, 0, 0, 3);
                    paragraph.Inlines.Add(new Run { Text = "\u2022  " });
                    AddInlines(paragraph, bullet.Groups[2].Value.Trim(), 0, false);
                }
                else if (numbered.Success)
                {
                    double indent = numbered.Groups[1].Value.Replace("\t", "    ").Length / 2 * 14;
                    paragraph.Margin = new Thickness(10 + indent, 0, 0, 3);
                    paragraph.Inlines.Add(new Run { Text = numbered.Groups[2].Value + ".  " });
                    AddInlines(paragraph, numbered.Groups[3].Value.Trim(), 0, false);
                }
                else
                {
                    AddInlines(paragraph, trimmed, 0, false);
                }

                rtb.Blocks.Add(paragraph);
            }

            return rtb;
        }

        private static void AddInlines(Paragraph paragraph, string text, double size, bool bold)
        {
            int pos = 0;
            foreach (Match m in InlineRegex.Matches(text))
            {
                if (m.Index > pos)
                    paragraph.Inlines.Add(MakeRun(text.Substring(pos, m.Index - pos), size, bold));

                if (m.Groups["b"].Success)
                {
                    paragraph.Inlines.Add(MakeRun(m.Groups["b"].Value, size, true));
                }
                else if (m.Groups["c"].Success)
                {
                    var run = MakeRun(m.Groups["c"].Value, size, bold);
                    run.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");
                    paragraph.Inlines.Add(run);
                }
                else if (m.Groups["lt"].Success)
                {
                    paragraph.Inlines.Add(MakeLink(m.Groups["lt"].Value, m.Groups["lu"].Value, size, bold));
                }
                else if (m.Groups["i"].Success)
                {
                    var run = MakeRun(m.Groups["i"].Value, size, bold);
                    run.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    paragraph.Inlines.Add(run);
                }
                else if (m.Groups["u"].Success)
                {
                    string url = m.Groups["u"].Value;
                    string trimmedUrl = url.TrimEnd('.', ',', ';', ':', '!', '?');
                    paragraph.Inlines.Add(MakeLink(trimmedUrl, trimmedUrl, size, bold));
                    if (trimmedUrl.Length < url.Length)
                        paragraph.Inlines.Add(MakeRun(url.Substring(trimmedUrl.Length), size, bold));
                }

                pos = m.Index + m.Length;
            }

            if (pos < text.Length)
                paragraph.Inlines.Add(MakeRun(text.Substring(pos), size, bold));
        }

        private static Run MakeRun(string text, double size, bool bold)
        {
            var run = new Run { Text = text };
            if (size > 0) run.FontSize = size;
            if (bold) run.FontWeight = FontWeights.SemiBold;
            return run;
        }

        private static Inline MakeLink(string label, string url, double size, bool bold)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return MakeRun(label, size, bold);

            var link = new Hyperlink { NavigateUri = uri };
            link.Inlines.Add(MakeRun(label, size, bold));
            return link;
        }
    }
}
