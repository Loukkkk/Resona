using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Resona.Models;

namespace Resona.Services;

/// <summary>Un titre du fichier de playlist retrouvé sur le PC (par son nom de fichier).</summary>
public sealed class PcFoundFile
{
    public int EntryIndex { get; init; }
    public PlaylistImportEntry Entry { get; init; } = new();
    public string FilePath { get; init; } = string.Empty;
    public double Score { get; init; }
}

/// <summary>
/// Analyse rapide du PC pour retrouver des titres absents de la bibliothèque : on ne lit que les NOMS de fichiers
/// (aucune lecture de tags), dans les dossiers musicaux usuels, puis on réutilise le matcher flou de l'import.
/// </summary>
public static class PcTrackFinder
{
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".vs", "bin", "obj", "packages", "$RECYCLE.BIN", "System Volume Information", "AppData"
    };

    private static readonly HashSet<string> WeakWords = new(StringComparer.Ordinal)
    {
        "the", "and", "feat", "with", "from", "for", "you", "your", "les", "des", "une", "que", "pour", "dans", "par", "sur"
    };

    private static readonly Regex LeadingNumber = new(@"^\s*\d{1,3}\s*[\.\)_–—-]\s+", RegexOptions.Compiled);
    private static readonly Regex SpacedDash = new(@"^(?<a>.+?)\s+[-–—−]\s+(?<b>.+)$", RegexOptions.Compiled);

    /// <summary>Dossiers parcourus : ceux de la bibliothèque + Musique, Téléchargements, Bureau, Documents (sans doublons ni imbrications).</summary>
    public static List<string> GetSearchRoots()
    {
        var raw = new List<string>();
        var settings = App.Settings?.Current;
        if (settings != null)
        {
            raw.AddRange(settings.MusicFolders ?? new List<string>());
            if (!string.IsNullOrWhiteSpace(settings.DownloadFolder)) raw.Add(settings.DownloadFolder);
        }

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        raw.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        raw.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        raw.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        if (!string.IsNullOrWhiteSpace(profile)) raw.Add(Path.Combine(profile, "Downloads"));

        var valid = raw
            .Where(f => !string.IsNullOrWhiteSpace(f) && Directory.Exists(f))
            .Select(f => Path.GetFullPath(f).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f.Length)
            .ToList();

        var roots = new List<string>();
        foreach (string folder in valid)
        {
            bool covered = roots.Any(p => folder.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (!covered) roots.Add(folder);
        }
        return roots;
    }

    /// <summary>Liste les fichiers audio (chemins seulement). Progression : (entrées parcourues, fichiers audio trouvés).</summary>
    public static Task<List<string>> ScanAudioFilesAsync(IProgress<(int Scanned, int Audio)>? progress, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var files = new List<string>();
            var excluded = new HashSet<string>(App.Settings?.Current?.ExcludedFilePaths ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MaxRecursionDepth = 12,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            int scanned = 0;
            foreach (string root in GetSearchRoots())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var enumerable = new FileSystemEnumerable<string>(root, (ref FileSystemEntry e) => e.ToFullPath(), options)
                    {
                        ShouldIncludePredicate = (ref FileSystemEntry e) =>
                        {
                            if (e.IsDirectory) return false;
                            scanned++;
                            return LibraryScannerService.IsSupportedAudio(e.FileName.ToString());
                        },
                        ShouldRecursePredicate = (ref FileSystemEntry e) => !SkipDirs.Contains(e.FileName.ToString())
                    };

                    foreach (string path in enumerable)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (excluded.Contains(path)) continue;
                        files.Add(path);
                        if (files.Count % 100 == 0) progress?.Report((scanned, files.Count));
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    LogService.Write("PcTrackFinder", ex, $"Analyse impossible : {root}");
                }
                progress?.Report((scanned, files.Count));
            }
            return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }, ct);
    }

    /// <summary>Apparie les titres manquants aux fichiers trouvés. Seuls les appariements ≥ FuzzyTrackMatcher.AcceptScore sont renvoyés.</summary>
    public static List<PcFoundFile> Match(IReadOnlyList<PlaylistImportEntry> entries, IReadOnlyList<string> files, CancellationToken ct)
    {
        var result = new List<PcFoundFile>();
        if (entries.Count == 0 || files.Count == 0) return result;

        // 1) Pré-filtre rapide par mots : un fichier n'est gardé que s'il partage au moins la moitié des mots du titre d'une entrée.
        var wordToEntries = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var needed = new int[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            var words = SignificantWords(FuzzyTrackMatcher.StripNoiseAndParentheses(entries[i].Title));
            if (words.Count == 0) words = SignificantWords(entries[i].Title);
            needed[i] = Math.Max(1, (int)Math.Ceiling(words.Count * 0.5));
            foreach (string w in words)
            {
                if (!wordToEntries.TryGetValue(w, out var list)) wordToEntries[w] = list = new List<int>();
                list.Add(i);
            }
        }

        var candidates = new List<Track>();
        var hits = new Dictionary<int, int>();
        foreach (string path in files)
        {
            ct.ThrowIfCancellationRequested();
            string name = CleanFileName(path);
            hits.Clear();
            foreach (string w in SignificantWords(name))
            {
                if (!wordToEntries.TryGetValue(w, out var list)) continue;
                foreach (int idx in list) hits[idx] = hits.TryGetValue(idx, out int c) ? c + 1 : 1;
            }
            bool keep = false;
            foreach (var kv in hits) { if (kv.Value >= needed[kv.Key]) { keep = true; break; } }
            if (!keep) continue;

            // « Artiste - Titre » ou « Titre - Artiste » : on propose les deux sens, le matcher écarte celui qui ne colle pas.
            var dash = SpacedDash.Match(name);
            if (dash.Success)
            {
                string a = dash.Groups["a"].Value.Trim();
                string b = dash.Groups["b"].Value.Trim();
                candidates.Add(new Track { FilePath = path, Artist = a, Title = b });
                candidates.Add(new Track { FilePath = path, Artist = b, Title = a });
            }
            else
            {
                candidates.Add(new Track { FilePath = path, Artist = string.Empty, Title = name });
            }
        }
        if (candidates.Count == 0) return result;

        // 2) Appariement flou sur les seuls fichiers retenus.
        var matcher = new FuzzyTrackMatcher(candidates);
        for (int i = 0; i < entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = entries[i];
            var (track, score) = matcher.Match(entry);

            // Nom de fichier du type « Artiste Titre » sans séparateur : on teste aussi « Artiste Titre » comme un seul titre.
            if (!string.IsNullOrWhiteSpace(entry.Artist))
            {
                var combo = matcher.Match(new PlaylistImportEntry { Title = entry.Artist + " " + entry.Title, Duration = entry.Duration });
                if (combo.matchedTrack != null && combo.score > score) { track = combo.matchedTrack; score = combo.score; }
            }

            if (track != null && score >= FuzzyTrackMatcher.AcceptScore)
                result.Add(new PcFoundFile { EntryIndex = i, Entry = entry, FilePath = track.FilePath, Score = score });
        }
        return result;
    }

    private static string CleanFileName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).Replace('_', ' ');
        return LeadingNumber.Replace(name, string.Empty).Trim();
    }

    private static HashSet<string> SignificantWords(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        string[] all = FuzzyTrackMatcher.NormalizeText(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string w in all)
            if (w.Length >= 3 && !WeakWords.Contains(w)) set.Add(w);
        if (set.Count == 0) foreach (string w in all) set.Add(w);
        return set;
    }
}
