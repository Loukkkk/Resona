using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Resona.Models;

namespace Resona.Services;

// ------------------------------------------------------------
//  Controle qualite de la bibliotheque (Parametres > Bibliotheque musicale)
//  Analyse la bibliotheque (fichiers, covers, tags) et propose des corrections rapides.
//  - L'analyse ne modifie rien.
//  - Les corrections "RemoveFromApp" ne suppriment jamais les fichiers du disque.
//  - Les corrections "WriteTags" ecrivent dans les fichiers audio (comme l'edition d'un son).
// ------------------------------------------------------------

/// <summary>Types de problemes, dans l'ordre d'affichage (du plus grave au plus leger).</summary>
public enum QualityIssueKind
{
    MissingFile,
    Unreadable,
    Duplicate,
    MissingCover,
    MissingTags,
    NoisyTags,
    InconsistentSpelling,
    InconsistentAlbum
}

public enum QualityFixKind
{
    None,
    RemoveFromApp,
    FetchCover,
    WriteTags
}

public sealed class QualityIssue
{
    public QualityIssueKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;

    /// <summary>Sons concernes par la correction (pour un doublon : uniquement les exemplaires en trop).</summary>
    public List<Track> Tracks { get; init; } = new();

    public QualityFixKind Fix { get; init; } = QualityFixKind.None;
    public string FixLabel { get; init; } = string.Empty;

    /// <summary>RemoveFromApp : ajoute aussi les fichiers a la liste d'exclusion (sinon un rescan les reajouterait).</summary>
    public bool ExcludeOnRemove { get; init; }

    /// <summary>WriteTags : nouvelles valeurs a ecrire, son par son.</summary>
    public List<(Track Track, AutoTagResult Tags)> TagChanges { get; init; } = new();
}

public sealed class QualityReport
{
    public int AnalyzedTracks { get; set; }
    public List<QualityIssue> Issues { get; } = new();
}

public readonly record struct QualityFixResult(int Ok, int Failed, int Skipped);

public static class LibraryQualityService
{
    // Mentions parasites typiques des titres telecharges (« (Official Video) », « [Lyrics] », « (HD) »...).
    private static readonly Regex NoiseSuffix = new(
        @"\s*[\(\[]\s*(?:official\s+(?:music\s+)?(?:video|audio|lyric\s+video|visuali[sz]er)|(?:official\s+)?lyrics?(?:\s+video)?|lyric\s+video|music\s+video|audio(?:\s+officiel)?|clip\s+officiel(?:le)?|paroles|hd|hq|4k|mv)\s*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TopicSuffix = new(@"\s*-\s*Topic\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LeadingTrackNumber = new(@"^\d{1,3}[\s._-]+", RegexOptions.CultureInvariant);
    private static readonly Regex ArtistTitleName = new(@"^(?<a>.+?)\s+[-\u2013\u2014]\s+(?<t>.+)$", RegexOptions.CultureInvariant);
    private static readonly Regex MultiSpace = new(@"\s{2,}", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> VariousArtists = new()
    {
        "various artists", "various", "va", "divers", "artistes varies", "artistes divers", "compilation"
    };

    // ============================================================
    //  Analyse
    // ============================================================

    public static Task<QualityReport> AnalyzeAsync(IReadOnlyList<Track> library, IProgress<(int Done, int Total)>? progress, CancellationToken ct)
        => Task.Run(() => Analyze(library, progress, ct), ct);

    private static QualityReport Analyze(IReadOnlyList<Track> library, IProgress<(int Done, int Total)>? progress, CancellationToken ct)
    {
        bool fr = Strings.Current.IsFr;
        var report = new QualityReport { AnalyzedTracks = library.Count };

        // ---------- Phase 1 : fichiers (presence, corruption) ----------
        var fileIssues = new ConcurrentBag<QualityIssue>();
        var sizes = new ConcurrentDictionary<string, long>();
        int done = 0;
        int total = library.Count;
        var options = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct };
        Parallel.ForEach(library, options, t =>
        {
            QualityIssue? issue = CheckFile(t, fr, out long size);
            sizes[t.Id] = size;
            if (issue != null) fileIssues.Add(issue);
            int d = Interlocked.Increment(ref done);
            if (d % 20 == 0 || d == total) progress?.Report((d, total));
        });
        ct.ThrowIfCancellationRequested();

        var badIds = new HashSet<string>();
        foreach (QualityIssue issue in fileIssues)
        {
            report.Issues.Add(issue);
            badIds.Add(issue.Tracks[0].Id);
        }
        List<Track> healthy = library.Where(t => !badIds.Contains(t.Id)).ToList();

        // ---------- Phase 2 : analyses en memoire ----------
        string? playingId = App.NowPlayingId;
        AddDuplicates(report, healthy, sizes, playingId, fr);
        AddMissingCovers(report, healthy, fr);
        var handled = new HashSet<string>();
        AddMissingTags(report, healthy, handled, fr);
        AddNoisyTags(report, healthy, handled, fr);
        AddSpelling(report, healthy, fr);
        AddAlbumConsistency(report, healthy, fr);

        report.Issues.Sort((a, b) =>
        {
            int c = a.Kind.CompareTo(b.Kind);
            return c != 0 ? c : string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
        });
        return report;
    }

    private static QualityIssue? CheckFile(Track t, bool fr, out long size)
    {
        size = -1;
        string path = t.FilePath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new QualityIssue
            {
                Kind = QualityIssueKind.MissingFile,
                Title = DisplayName(t),
                Detail = (fr ? "Fichier introuvable : " : "File not found: ") + path,
                Tracks = { t },
                Fix = QualityFixKind.RemoveFromApp,
                FixLabel = fr ? "Retirer de l'app" : "Remove from app",
                ExcludeOnRemove = false // si le fichier revient, un scan le reajoutera
            };
        }

        try { size = new FileInfo(path).Length; } catch { size = 0; }

        string? reason = null;
        if (size == 0)
        {
            reason = fr ? "Fichier vide (0 octet)" : "Empty file (0 bytes)";
        }
        else if (size < 2048)
        {
            reason = fr ? "Fichier anormalement petit (tronqué ?)" : "Abnormally small file (truncated?)";
        }
        else
        {
            try
            {
                // Extension qui ne correspond pas au contenu : le scanner sait la gerer, on ne conclut pas ici.
                if (!string.Equals(LibraryScannerService.ResolveRealExtension(path), path, StringComparison.OrdinalIgnoreCase))
                    return null;

                using var f = TagLib.File.Create(path);
                if (f.PossiblyCorrupt)
                {
                    string why = string.Join(" ; ", f.CorruptionReasons ?? Enumerable.Empty<string>());
                    reason = (fr ? "Fichier peut-être corrompu" : "File possibly corrupted") + (why.Length > 0 ? " : " + why : "");
                }
                else if (f.Properties.Duration == TimeSpan.Zero && t.Duration == TimeSpan.Zero)
                {
                    reason = fr ? "Durée nulle : l'audio semble illisible" : "Zero duration: the audio seems unreadable";
                }
            }
            catch (TagLib.CorruptFileException ex)
            {
                reason = (fr ? "Fichier corrompu : " : "Corrupted file: ") + ex.Message;
            }
            catch (TagLib.UnsupportedFormatException)
            {
                // Format non gere par TagLib : le lecteur sait le lire autrement, ce n'est pas un probleme.
            }
            catch
            {
                // Fichier verrouille ou erreur d'E/S : on ne conclut pas.
            }
        }

        if (reason == null) return null;

        return new QualityIssue
        {
            Kind = QualityIssueKind.Unreadable,
            Title = DisplayName(t),
            Detail = reason + "\n" + path,
            Tracks = { t },
            Fix = QualityFixKind.RemoveFromApp,
            FixLabel = fr ? "Retirer de l'app" : "Remove from app",
            ExcludeOnRemove = true
        };
    }

    // ---------- Doublons ----------

    private static void AddDuplicates(QualityReport report, List<Track> healthy, ConcurrentDictionary<string, long> sizes, string? playingId, bool fr)
    {
        var groups = healthy
            .Where(t => !IsUnknownArtist(t.Artist) && Norm(t.Title).Length > 0)
            .GroupBy(t => Norm(t.Artist) + "|" + Norm(t.Title))
            .Where(g => g.Count() > 1);

        foreach (var g in groups)
        {
            var ordered = g.OrderBy(t => t.Duration).ToList();
            var clusters = new List<List<Track>>();
            List<Track>? current = null;
            foreach (Track t in ordered)
            {
                if (current != null && (t.Duration - current[^1].Duration).TotalSeconds <= 2.5)
                {
                    current.Add(t);
                }
                else
                {
                    current = new List<Track> { t };
                    clusters.Add(current);
                }
            }

            foreach (var cluster in clusters.Where(c => c.Count >= 2))
            {
                // On garde le son en cours de lecture, sinon le plus gros fichier (meilleure qualite probable), sinon le plus ancien.
                Track keeper = cluster
                    .OrderByDescending(t => t.Id == playingId ? 1 : 0)
                    .ThenByDescending(t => sizes.TryGetValue(t.Id, out long s) ? s : 0)
                    .ThenBy(t => t.DateAdded)
                    .First();
                List<Track> extras = cluster.Where(t => !ReferenceEquals(t, keeper)).ToList();

                var sb = new StringBuilder();
                sb.Append(fr ? "Conservé : " : "Kept: ").Append(keeper.FilePath).Append(" (").Append(FormatSize(sizes, keeper)).Append(')');
                foreach (Track e in extras)
                    sb.Append('\n').Append(fr ? "Retiré de l'app : " : "Removed from app: ").Append(e.FilePath).Append(" (").Append(FormatSize(sizes, e)).Append(')');

                report.Issues.Add(new QualityIssue
                {
                    Kind = QualityIssueKind.Duplicate,
                    Title = DisplayName(keeper),
                    Detail = sb.ToString(),
                    Tracks = extras,
                    Fix = QualityFixKind.RemoveFromApp,
                    FixLabel = fr ? "Retirer le doublon" : "Remove duplicate",
                    ExcludeOnRemove = true
                });
            }
        }
    }

    private static string FormatSize(ConcurrentDictionary<string, long> sizes, Track t)
        => sizes.TryGetValue(t.Id, out long s) && s > 0 ? (s / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB" : "?";

    // ---------- Covers ----------

    private static bool HasCover(Track t) => !string.IsNullOrEmpty(t.CoverArtPath) && File.Exists(t.CoverArtPath);

    private static void AddMissingCovers(QualityReport report, List<Track> healthy, bool fr)
    {
        var groups = new Dictionary<string, List<Track>>();
        foreach (Track t in healthy)
        {
            if (HasCover(t)) continue;
            bool known = !IsUnknownArtist(t.Artist) && !IsUnknownAlbum(t.Album);
            string key = known ? "a|" + Norm(t.Artist) + "|" + Norm(t.Album) : "t|" + t.Id;
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Track>();
            list.Add(t);
        }

        foreach (var (key, list) in groups)
        {
            Track first = list[0];
            bool isAlbum = key.StartsWith("a|", StringComparison.Ordinal);
            report.Issues.Add(new QualityIssue
            {
                Kind = QualityIssueKind.MissingCover,
                Title = isAlbum ? first.Album + " — " + first.Artist : DisplayName(first),
                Detail = isAlbum
                    ? (fr ? $"{list.Count} son(s) sans cover" : $"{list.Count} track(s) without cover")
                    : (fr ? "Aucune cover" : "No cover"),
                Tracks = list,
                Fix = QualityFixKind.FetchCover,
                FixLabel = fr ? "Chercher la cover" : "Find cover"
            });
        }
    }

    // ---------- Tags manquants (artiste / titre) ----------

    private static void AddMissingTags(QualityReport report, List<Track> healthy, HashSet<string> handled, bool fr)
    {
        foreach (Track t in healthy)
        {
            string fileName = Path.GetFileNameWithoutExtension(t.FilePath ?? string.Empty);
            bool missingArtist = IsUnknownArtist(t.Artist);
            bool titleFromFile = Norm(t.Title).Length > 0 && Norm(t.Title) == Norm(fileName);

            (string Artist, string Title)? parsed = ParseArtistTitle(fileName);

            // On ne signale « titre = nom du fichier » que si le nom suit le modele « Artiste - Titre » (sinon c'est probablement voulu).
            bool flagTitle = titleFromFile && parsed != null;
            if (!missingArtist && !flagTitle) continue;

            var parts = new List<string>();
            if (missingArtist) parts.Add(fr ? "artiste manquant" : "missing artist");
            if (flagTitle) parts.Add(fr ? "titre identique au nom du fichier" : "title identical to file name");

            var changes = new List<(Track Track, AutoTagResult Tags)>();
            string preview = string.Empty;
            if (parsed != null)
            {
                var data = new AutoTagResult();
                string newTitle = CleanTitle(parsed.Value.Title, null);
                if (newTitle.Length == 0) newTitle = parsed.Value.Title;

                if (missingArtist)
                {
                    data.Artist = CleanArtist(parsed.Value.Artist);
                    if (titleFromFile) data.Title = newTitle;
                }
                else if (flagTitle && Norm(parsed.Value.Artist) == Norm(t.Artist))
                {
                    data.Title = newTitle;
                }

                if (data.Artist != null || data.Title != null)
                {
                    changes.Add((t, data));
                    var sb = new StringBuilder();
                    if (data.Artist != null) sb.Append(fr ? "Artiste → " : "Artist → ").Append('“').Append(data.Artist).Append('”');
                    if (data.Title != null)
                    {
                        if (sb.Length > 0) sb.Append("  ·  ");
                        sb.Append(fr ? "Titre → " : "Title → ").Append('“').Append(data.Title).Append('”');
                    }
                    preview = sb.ToString();
                }
            }

            bool hasFix = changes.Count > 0;
            string detail = char.ToUpper(string.Join(", ", parts)[0], CultureInfo.CurrentCulture) + string.Join(", ", parts).Substring(1)
                            + (hasFix
                                ? "\n" + preview
                                : (fr ? "\nPas de correction automatique : utilise le bouton Modifier." : "\nNo automatic fix: use the Edit button."));

            report.Issues.Add(new QualityIssue
            {
                Kind = QualityIssueKind.MissingTags,
                Title = Path.GetFileName(t.FilePath ?? string.Empty),
                Detail = detail,
                Tracks = { t },
                Fix = hasFix ? QualityFixKind.WriteTags : QualityFixKind.None,
                FixLabel = fr ? "Déduire du nom du fichier" : "Fill from file name",
                TagChanges = changes
            });
            if (hasFix) handled.Add(t.Id);
        }
    }

    private static (string Artist, string Title)? ParseArtistTitle(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        string name = fileName.Trim();

        Match m = ArtistTitleName.Match(name);
        if (!m.Success)
        {
            // « 01 - Artiste - Titre » : on retire le numero de piste puis on reessaie.
            string stripped = LeadingTrackNumber.Replace(name, string.Empty);
            if (stripped == name) return null;
            m = ArtistTitleName.Match(stripped);
            if (!m.Success) return null;
        }

        string a = m.Groups["a"].Value.Trim();
        string title = m.Groups["t"].Value.Trim();
        if (a.Length == 0 || title.Length == 0) return null;
        return (a, title);
    }

    // ---------- Tags « sales » ----------

    private static void AddNoisyTags(QualityReport report, List<Track> healthy, HashSet<string> handled, bool fr)
    {
        foreach (Track t in healthy)
        {
            if (handled.Contains(t.Id)) continue;

            string? knownArtist = IsUnknownArtist(t.Artist) ? null : t.Artist;
            string title = t.Title ?? string.Empty;
            string newTitle = CleanTitle(title, knownArtist);
            string? newArtist = knownArtist != null ? CleanArtist(knownArtist) : null;

            bool titleChanged = newTitle.Length > 0 && !string.Equals(newTitle, title, StringComparison.Ordinal);
            bool artistChanged = !string.IsNullOrEmpty(newArtist) && !string.Equals(newArtist, knownArtist, StringComparison.Ordinal);
            if (!titleChanged && !artistChanged) continue;

            var data = new AutoTagResult();
            var lines = new List<string>();
            if (titleChanged) { data.Title = newTitle; lines.Add((fr ? "Titre : " : "Title: ") + "“" + title + "” → “" + newTitle + "”"); }
            if (artistChanged) { data.Artist = newArtist; lines.Add((fr ? "Artiste : " : "Artist: ") + "“" + knownArtist + "” → “" + newArtist + "”"); }

            report.Issues.Add(new QualityIssue
            {
                Kind = QualityIssueKind.NoisyTags,
                Title = DisplayName(t),
                Detail = string.Join("\n", lines),
                Tracks = { t },
                Fix = QualityFixKind.WriteTags,
                FixLabel = fr ? "Nettoyer" : "Clean up",
                TagChanges = { (t, data) }
            });
        }
    }

    private static string CleanTitle(string title, string? artist)
    {
        string s = NoiseSuffix.Replace(title ?? string.Empty, string.Empty).Trim();

        if (!string.IsNullOrEmpty(artist))
        {
            foreach (string sep in new[] { " - ", " \u2013 ", " \u2014 " })
            {
                string prefix = artist + sep;
                if (s.Length > prefix.Length && s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(prefix.Length).Trim();
                    break;
                }
            }
        }

        return MultiSpace.Replace(s, " ").Trim();
    }

    private static string CleanArtist(string artist) => TopicSuffix.Replace(artist ?? string.Empty, string.Empty).Trim();

    // ---------- Orthographes incoherentes (artiste / album / genre) ----------

    private static void AddSpelling(QualityReport report, List<Track> healthy, bool fr)
    {
        AddSpellingFor(report, healthy, fr,
            fr ? "Artiste" : "Artist",
            t => t.Artist ?? string.Empty,
            t => Norm(t.Artist),
            IsUnknownArtist,
            (d, v) => d.Artist = v);

        AddSpellingFor(report, healthy, fr,
            fr ? "Album" : "Album",
            t => t.Album ?? string.Empty,
            t => Norm(t.Album) + "|" + Norm(PrimaryArtist(t)),
            IsUnknownAlbum,
            (d, v) => d.Album = v);

        AddSpellingFor(report, healthy, fr,
            fr ? "Genre" : "Genre",
            t => t.Genre ?? string.Empty,
            t => Norm(t.Genre),
            IsUnknownGenre,
            (d, v) => d.Genre = v);
    }

    private static void AddSpellingFor(
        QualityReport report, List<Track> healthy, bool fr, string fieldLabel,
        Func<Track, string> getter, Func<Track, string> keyFn, Func<string?, bool> isUnknown, Action<AutoTagResult, string> setter)
    {
        var groups = healthy
            .Where(t => !isUnknown(getter(t)) && keyFn(t).Replace("|", "").Length > 0)
            .GroupBy(keyFn)
            .Where(g => g.Select(getter).Distinct(StringComparer.Ordinal).Count() > 1);

        foreach (var g in groups)
        {
            var counts = g.GroupBy(getter, StringComparer.Ordinal)
                .Select(x => (Value: x.Key, Count: x.Count()))
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Value.Count(char.IsUpper))
                .ThenBy(x => x.Value, StringComparer.Ordinal)
                .ToList();
            string canonical = counts[0].Value;

            var changes = new List<(Track Track, AutoTagResult Tags)>();
            foreach (Track t in g)
            {
                if (string.Equals(getter(t), canonical, StringComparison.Ordinal)) continue;
                var d = new AutoTagResult();
                setter(d, canonical);
                changes.Add((t, d));
            }
            if (changes.Count == 0) continue;

            string variants = string.Join(" / ", counts.Select(c => "“" + c.Value + "” (" + c.Count + ")"));
            report.Issues.Add(new QualityIssue
            {
                Kind = QualityIssueKind.InconsistentSpelling,
                Title = fieldLabel + " : " + canonical,
                Detail = (fr ? "Variantes : " : "Variants: ") + variants + " → “" + canonical + "”",
                Tracks = changes.Select(c => c.Track).ToList(),
                Fix = QualityFixKind.WriteTags,
                FixLabel = fr ? "Unifier" : "Unify",
                TagChanges = changes
            });
        }
    }

    // ---------- Albums incoherents (annee, artiste de l'album) ----------

    private static void AddAlbumConsistency(QualityReport report, List<Track> healthy, bool fr)
    {
        var groups = healthy
            .Where(t => !IsUnknownAlbum(t.Album))
            .GroupBy(t => Norm(t.Album) + "|" + Norm(PrimaryArtist(t)))
            .Where(g => g.Count() >= 2);

        foreach (var g in groups)
        {
            List<Track> list = g.ToList();
            string primaryNorm = Norm(PrimaryArtist(list[0]));
            bool various = VariousArtists.Contains(primaryNorm);

            var map = new Dictionary<Track, AutoTagResult>();
            var lines = new List<string>();

            // Annee : on harmonise sur l'annee la plus frequente (pas pour les compilations, dont les annees different legitimement).
            if (!various)
            {
                var years = list.Where(t => t.Year > 0)
                    .GroupBy(t => t.Year)
                    .Select(x => (Year: x.Key, Count: x.Count()))
                    .OrderByDescending(x => x.Count).ThenByDescending(x => x.Year)
                    .ToList();
                if (years.Count > 0)
                {
                    int common = years[0].Year;
                    var odd = list.Where(t => t.Year != common).ToList();
                    if (odd.Count > 0)
                    {
                        foreach (Track t in odd) GetOrAdd(map, t).Year = common;
                        string detail = years.Count >= 2
                            ? string.Join(", ", years.Select(y => y.Year + " (" + y.Count + ")"))
                            : (fr ? "année absente sur " + odd.Count + " son(s)" : "year missing on " + odd.Count + " track(s)");
                        lines.Add((fr ? "Année : " : "Year: ") + detail + " → " + common);
                    }
                }
            }

            // Artiste de l'album : renseigne sur certains sons seulement.
            var withAa = list.Where(t => !string.IsNullOrWhiteSpace(t.AlbumArtist)).ToList();
            var withoutAa = list.Where(t => string.IsNullOrWhiteSpace(t.AlbumArtist)).ToList();
            if (withAa.Count > 0 && withoutAa.Count > 0)
            {
                string commonAa = withAa.GroupBy(t => t.AlbumArtist, StringComparer.Ordinal)
                    .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal)
                    .First().Key;
                foreach (Track t in withoutAa) GetOrAdd(map, t).AlbumArtist = commonAa;
                lines.Add((fr ? "Artiste de l'album absent sur " + withoutAa.Count + " son(s) → “" : "Album artist missing on " + withoutAa.Count + " track(s) → “") + commonAa + "”");
            }

            if (map.Count == 0) continue;

            Track first = list[0];
            report.Issues.Add(new QualityIssue
            {
                Kind = QualityIssueKind.InconsistentAlbum,
                Title = first.Album + " — " + PrimaryArtist(first),
                Detail = string.Join("\n", lines),
                Tracks = map.Keys.ToList(),
                Fix = QualityFixKind.WriteTags,
                FixLabel = fr ? "Harmoniser" : "Harmonize",
                TagChanges = map.Select(kv => (kv.Key, kv.Value)).ToList()
            });
        }
    }

    private static AutoTagResult GetOrAdd(Dictionary<Track, AutoTagResult> map, Track t)
    {
        if (!map.TryGetValue(t, out var d)) map[t] = d = new AutoTagResult();
        return d;
    }

    // ============================================================
    //  Corrections
    // ============================================================

    /// <param name="writeToFiles">Corrections de tags uniquement : faux (defaut) = seule la base de l'app change (reversible, fichiers intacts) ; vrai = ecrit aussi dans le fichier audio (irreversible).</param>
    public static async Task<QualityFixResult> ApplyFixAsync(QualityIssue issue, CancellationToken ct = default, bool writeToFiles = false)
    {
        switch (issue.Fix)
        {
            case QualityFixKind.RemoveFromApp: return await RemoveFromAppAsync(issue.Tracks, issue.ExcludeOnRemove);
            case QualityFixKind.FetchCover:    return await FetchCoversAsync(issue.Tracks, ct, writeToFiles);
            case QualityFixKind.WriteTags:     return await WriteTagsAsync(issue.TagChanges, ct, writeToFiles);
            default:                           return default;
        }
    }

    /// <summary>Retire des sons de l'app. Les fichiers ne sont JAMAIS supprimes du disque. Le son en cours de lecture est ignore.</summary>
    private static async Task<QualityFixResult> RemoveFromAppAsync(List<Track> tracks, bool exclude)
    {
        string? playing = App.NowPlayingId;
        List<Track> toRemove = tracks.Where(t => t.Id != playing).ToList();
        int skipped = tracks.Count - toRemove.Count;
        if (toRemove.Count == 0) return new QualityFixResult(0, 0, skipped);

        try
        {
            List<string> paths = toRemove.Select(t => t.FilePath).ToList();
            if (exclude)
            {
                var list = new List<string>(App.Settings.Current.ExcludedFilePaths ?? new List<string>());
                foreach (string p in paths)
                    if (!string.IsNullOrEmpty(p) && !list.Contains(p, StringComparer.OrdinalIgnoreCase)) list.Add(p);
                App.Settings.Current.ExcludedFilePaths = list;
                await App.Settings.SaveAsync();
            }
            await App.Cache.DeleteTracksByPathsAsync(paths);
            App.MainWindowInstance?.RemoveTracksFromLibrary(paths);
            return new QualityFixResult(toRemove.Count, 0, skipped);
        }
        catch (Exception ex)
        {
            LogService.Write("QualityCheck", ex, "Retrait de sons de l'app");
            return new QualityFixResult(0, toRemove.Count, skipped);
        }
    }

    /// <summary>Cherche d'abord une cover integree dans les fichiers, puis en ligne.</summary>
    /// <param name="writeToFiles">Faux (defaut) = la cover n'est associee que dans l'app ; vrai = elle est aussi integree dans le fichier audio (irreversible).</param>
    private static async Task<QualityFixResult> FetchCoversAsync(List<Track> tracks, CancellationToken ct, bool writeToFiles = false)
    {
        if (tracks.Count == 0) return default;
        try
        {
            Track first = tracks[0];
            string artist = IsUnknownArtist(first.Artist) ? string.Empty : first.Artist;
            string album = IsUnknownAlbum(first.Album) ? string.Empty : first.Album;
            string cacheKey = CoverArtService.GetAlbumCacheKey(artist, album, first.Id);

            string? path = null;
            foreach (Track t in tracks)
            {
                ct.ThrowIfCancellationRequested();
                string filePath = t.FilePath;
                byte[]? bytes = await Task.Run(() => App.Scanner.ExtractEmbeddedCover(filePath));
                if (bytes != null && bytes.Length > 0)
                {
                    path = await App.CoverArt.SaveEmbeddedCoverAsync(cacheKey, bytes);
                    if (path != null) break;
                }
            }

            if (path == null)
            {
                ct.ThrowIfCancellationRequested();
                path = await App.CoverArt.FindAndCacheCoverAsync(first.Id, artist, album, first.Title, first.FilePath);
            }

            if (path == null) return new QualityFixResult(0, tracks.Count, 0);

            int ok = 0, failed = 0;
            foreach (Track t in tracks)
            {
                if (writeToFiles)
                {
                    bool written = false;
                    try
                    {
                        string filePath = t.FilePath;
                        string coverPath = path;
                        if (File.Exists(filePath))
                            await App.AudioEngine.RunWithFileReleasedAsync(filePath, () => { written = AutoTagService.WriteMetadata(filePath, new AutoTagResult { CoverPath = coverPath }); });
                    }
                    catch (Exception ex)
                    {
                        LogService.Write("QualityCheck", ex, "Ecriture de la cover dans le fichier");
                        written = false;
                    }
                    if (!written) { failed++; continue; }
                    try { t.LastModified = File.GetLastWriteTimeUtc(t.FilePath); } catch { }
                }

                t.CoverArtPath = path;
                await App.Cache.UpdateTrackAsync(t);
                ok++;
            }
            CoverCacheService.ClearCache(path);
            return new QualityFixResult(ok, failed, 0);
        }
        catch (OperationCanceledException)
        {
            return new QualityFixResult(0, 0, tracks.Count);
        }
        catch (Exception ex)
        {
            LogService.Write("QualityCheck", ex, "Recherche de cover");
            return new QualityFixResult(0, tracks.Count, 0);
        }
    }

    /// <summary>Ecrit les nouveaux tags dans les fichiers (le fichier en cours de lecture est libere puis la lecture reprend).</summary>
    private static async Task<QualityFixResult> WriteTagsAsync(List<(Track Track, AutoTagResult Tags)> changes, CancellationToken ct, bool writeToFiles)
    {
        int ok = 0, failed = 0, skipped = 0;
        foreach (var (track, data) in changes)
        {
            if (ct.IsCancellationRequested) { skipped++; continue; }

            bool written = false;
            if (!writeToFiles) written = true;   // mode "dans l'app uniquement" : le fichier audio n'est pas touche
            else try
            {
                if (!File.Exists(track.FilePath)) { failed++; continue; }
                string filePath = track.FilePath;
                await App.AudioEngine.RunWithFileReleasedAsync(filePath, () => { written = AutoTagService.WriteMetadata(filePath, data); });
            }
            catch (Exception ex)
            {
                LogService.Write("QualityCheck", ex, "Ecriture des tags");
                written = false;
            }

            if (!written) { failed++; continue; }

            if (data.Title != null) track.Title = data.Title;
            if (data.Artist != null) track.Artist = data.Artist;
            if (data.Album != null) track.Album = data.Album;
            if (data.AlbumArtist != null) track.AlbumArtist = data.AlbumArtist;
            if (data.Genre != null) track.Genre = data.Genre;
            if (data.Year.HasValue) track.Year = data.Year.Value;

            try
            {
                if (writeToFiles) track.LastModified = File.GetLastWriteTimeUtc(track.FilePath);
                await App.Cache.UpdateTrackAsync(track);
            }
            catch (Exception ex)
            {
                LogService.Write("QualityCheck", ex, "Mise a jour du cache apres ecriture des tags");
            }
            ok++;
        }
        return new QualityFixResult(ok, failed, skipped);
    }

    // ============================================================
    //  Utilitaires
    // ============================================================

    private static string DisplayName(Track t) => (t.Artist ?? string.Empty) + " — " + (t.Title ?? string.Empty);

    private static string PrimaryArtist(Track t)
        => !string.IsNullOrWhiteSpace(t.AlbumArtist) ? t.AlbumArtist : (t.Artist ?? string.Empty);

    private static bool IsUnknownArtist(string? a)
        => string.IsNullOrWhiteSpace(a) || a == "Artiste inconnu" || a == "Unknown artist" || a == Strings.Current.CS_ArtisteInconnu;

    private static bool IsUnknownAlbum(string? a)
        => string.IsNullOrWhiteSpace(a) || a == "Album inconnu" || a == "Unknown album" || a == Strings.Current.CS_AlbumInconnu;

    private static bool IsUnknownGenre(string? g)
        => string.IsNullOrWhiteSpace(g) || g == "Genre inconnu" || g == "Unknown genre" || g == "Inconnu" || g == "Unknown";

    /// <summary>Forme comparable d'un texte : minuscules, sans accents ni ponctuation, espaces normalises.</summary>
    private static string Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        string decomposed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
