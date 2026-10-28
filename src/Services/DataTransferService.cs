// ============================================================
//  DataTransferService.cs  —  Resona
//  Import / export des reglages et des donnees de la bibliotheque dans un fichier .zip :
//    manifest.json    : identifie la sauvegarde (produit, version du format, version de l'app, date)
//    settings.json    : reglages de l'app (sans les dossiers / chemins / positions propres a ce PC)
//    folders.json     : dossiers de musique, dossier de telechargement, fichiers exclus
//    playlists.json   : playlists + Favoris (titres references par chemin, pas par identifiant interne) + covers/
//    statistics.json  : nombre d'ecoutes par titre + temps d'ecoute total + historique date des ecoutes (Year in review)
//    display.json     : donnees d'affichage : correspondances artistes / albums / genres, modifs titre par titre (titre, artiste, album,
//                       genre, annee, n° de piste : uniquement dans l'app, fichiers audio intacts), pochettes (choisies ou automatiques,
//                       hors celles deja integrees au fichier audio). Ancien nom : ai.json (toujours importable).
//    lyrics.json      : paroles en cache
//    lyrics_edits.json: paroles personnalisees (choisies ou modifiees a la main, non re-telechargeables)
//  Les identifiants de titres (Guid) sont propres a chaque base : on exporte donc les titres par chemin de fichier
//  (+ artiste / titre / duree) et on refait la correspondance a l'import.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Resona.Models;

namespace Resona.Services;

public enum BackupCategory { Settings, Folders, Playlists, Statistics, AiData, Lyrics, LyricsEdits }

public sealed class BackupManifest
{
    public string   Product       { get; set; } = "Resona";
    public int      FormatVersion { get; set; } = 1;
    public string   AppVersion    { get; set; } = "";
    public DateTime CreatedUtc    { get; set; } = DateTime.UtcNow;
}

public sealed class BackupImportReport
{
    public bool SettingsApplied;
    public int  FoldersAdded, FoldersSkipped;
    public int  PlaylistsImported, PlaylistTracksAdded, PlaylistTracksMissing;
    public int  StatsUpdated;
    public int  HistoryEventsAdded;
    public int  AiMappingsMerged;
    public int  DisplayEditsApplied, DisplayEditsMissing;
    public int  LyricsApplied;
    public int  LyricsEditsApplied, LyricsEditsKept, LyricsEditsMissing;
    public string? SafetyBackupPath;
}

// ---- Formats de fichiers (internes) ----
internal sealed class BackupTrackRef
{
    public string Path    { get; set; } = "";
    public string Artist  { get; set; } = "";
    public string Title   { get; set; } = "";
    public double Seconds { get; set; }
}

internal sealed class BackupPlaylist
{
    public string   Id           { get; set; } = "";
    public string   Name         { get; set; } = "";
    public bool     IsSystem     { get; set; }
    public DateTime DateCreated  { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
    public string?  CoverFile    { get; set; }
    // Playlist intelligente : regles (JSON). Absent des anciennes sauvegardes (null = playlist classique).
    public string?  SmartRulesJson { get; set; }
    public List<BackupTrackRef> Tracks { get; set; } = new();
}

internal sealed class BackupPlaylistsFile { public List<BackupPlaylist> Playlists { get; set; } = new(); }

internal sealed class BackupStatItem { public BackupTrackRef Track { get; set; } = new(); public int Count { get; set; } }
internal sealed class BackupStatsFile
{
    public long ListenSeconds { get; set; }
    public List<BackupStatItem> Items { get; set; } = new();
    // Historique date des ecoutes (Year in review). Absent des anciennes sauvegardes : on n'y touche pas dans ce cas.
    public List<PlayEvent>? History { get; set; }
    public DateTime? HistoryStartUtc { get; set; }
}

internal sealed class BackupFoldersFile
{
    public List<string> MusicFolders       { get; set; } = new();
    public List<string> ExcludedFilePaths  { get; set; } = new();
    public string       DownloadFolder     { get; set; } = "";
}

internal sealed class BackupDisplayEdit
{
    // Titre référencé par ses tags d'ORIGINE (ceux du fichier) pour être retrouvé sur un PC où la modif n'est pas encore appliquée.
    public BackupTrackRef Track { get; set; } = new();
    // null = champ inchangé par rapport au fichier.
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    // Artiste de l'album : absent des anciennes sauvegardes (null = inchange).
    public string? AlbumArtist { get; set; }
    public int? Year { get; set; }
    public int? TrackNumber { get; set; }
    public string? CoverFile { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? LocalCoverPath { get; set; }
}

internal sealed class BackupAiFile
{
    public Dictionary<string, string> ArtistMappings { get; set; } = new();
    public Dictionary<string, string> AlbumMappings  { get; set; } = new();
    public Dictionary<string, string> GenreMappings  { get; set; } = new();
    // Genre par titre (clé « Artiste - Titre ») : absent des anciennes sauvegardes.
    public Dictionary<string, string> TrackGenreMappings { get; set; } = new();
    // Absent des anciennes sauvegardes : on n'y touche pas dans ce cas.
    public List<BackupDisplayEdit>? TrackEdits { get; set; }
}

internal sealed class BackupLyricsItem { public string Path { get; set; } = ""; public string Lyrics { get; set; } = ""; public bool Synced { get; set; } public double? Offset { get; set; } }
// Paroles choisies a la main (selecteur) : le titre est reference par chemin + artiste + titre + duree pour etre retrouve sur un autre PC.
internal sealed class BackupLyricsOverrideItem { public BackupTrackRef Track { get; set; } = new(); public string Lyrics { get; set; } = ""; public bool Synced { get; set; } public string Label { get; set; } = ""; public double? Offset { get; set; } }
internal sealed class BackupLyricsFile
{
    public List<BackupLyricsItem> Items { get; set; } = new();
    public List<BackupLyricsOverrideItem> Overrides { get; set; } = new();
}

// Paroles personnalisees : a part des paroles en cache (lourdes, re-telechargeables) car petites et impossibles a retrouver.
internal sealed class BackupLyricsEditsFile { public List<BackupLyricsOverrideItem> Items { get; set; } = new(); }

public static class DataTransferService
{
    private const int CurrentFormatVersion = 1;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string AppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona");
    public static string BackupsDir => Path.Combine(AppDataDir, "backups");

    // Ancien nom du fichier des donnees d'affichage : les sauvegardes deja exportees restent importables.
    private const string LegacyAiEntryName = "ai.json";

    public static string SuggestedFileName() => $"Resona-backup-{DateTime.Now:yyyyMMdd}.zip";

    private static string EntryName(BackupCategory c) => c switch
    {
        BackupCategory.Settings   => "settings.json",
        BackupCategory.Folders    => "folders.json",
        BackupCategory.Playlists  => "playlists.json",
        BackupCategory.Statistics => "statistics.json",
        BackupCategory.AiData     => "display.json",
        BackupCategory.Lyrics     => "lyrics.json",
        BackupCategory.LyricsEdits => "lyrics_edits.json",
        _ => "",
    };

    // ------------------------------------------------------------------
    //  Lecture / ecriture JSON dans le zip
    // ------------------------------------------------------------------

    private static async Task WriteJsonAsync(ZipArchive zip, string name, object data)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, data, data.GetType(), JsonOpts);
    }

    private static T? ReadJson<T>(ZipArchive zip, string name) where T : class
    {
        var entry = zip.GetEntry(name);
        if (entry == null) return null;
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOpts);
    }

    // ------------------------------------------------------------------
    //  Export
    // ------------------------------------------------------------------

    private static BackupTrackRef ToRef(Track t) => new()
    {
        Path = t.FilePath, Artist = t.Artist, Title = t.Title, Seconds = Math.Round(t.Duration.TotalSeconds, 1)
    };

    private static AppSettings BuildSettingsExport()
    {
        // Copie profonde, puis on retire tout ce qui est propre a ce PC ou gere par une autre categorie.
        var s = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(App.Settings.Current)) ?? new AppSettings();
        s.MusicFolders = new(); s.ExcludedFilePaths = new(); s.DownloadFolder = "";
        s.WindowX = -1; s.WindowY = -1; s.WindowWidth = 1200; s.WindowHeight = 800;
        s.LastKnownVersion = ""; s.LastSeenVersion = ""; s.LastYtDlpUpdateCheckUtc = "";
        s.SavedQueueIds = new();
        s.ArtistMappings = new(); s.AlbumMappings = new(); s.GenreMappings = new(); s.TrackGenreMappings = new();
        return s;
    }

    public static async Task ExportAsync(string zipPath, IReadOnlyCollection<BackupCategory> categories)
    {
        string? dir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tmp = zipPath + ".tmp";

        try
        {
            List<Track>? tracks = null;
            async Task<List<Track>> Tracks() => tracks ??= await App.Cache.LoadAllTracksAsync();

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                await WriteJsonAsync(zip, "manifest.json", new BackupManifest { AppVersion = UpdateManager.CurrentTag });

                if (categories.Contains(BackupCategory.Settings))
                    await WriteJsonAsync(zip, EntryName(BackupCategory.Settings), BuildSettingsExport());

                if (categories.Contains(BackupCategory.Folders))
                {
                    var cur = App.Settings.Current;
                    await WriteJsonAsync(zip, EntryName(BackupCategory.Folders), new BackupFoldersFile
                    {
                        MusicFolders = cur.MusicFolders.ToList(),
                        ExcludedFilePaths = cur.ExcludedFilePaths.ToList(),
                        DownloadFolder = cur.DownloadFolder ?? ""
                    });
                }

                if (categories.Contains(BackupCategory.Playlists))
                {
                    var byId = (await Tracks()).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
                    var file = new BackupPlaylistsFile();
                    int coverIndex = 0;
                    foreach (var p in await App.Cache.LoadAllPlaylistsAsync())
                    {
                        var dto = new BackupPlaylist
                        {
                            Id = p.Id, Name = p.Name, IsSystem = p.IsSystem,
                            DateCreated = p.DateCreated, DateModified = p.DateModified,
                            SmartRulesJson = p.SmartRulesJson
                        };
                        foreach (string id in p.TrackIds)
                            if (byId.TryGetValue(id, out var t)) dto.Tracks.Add(ToRef(t));

                        if (!p.IsSystem && !string.IsNullOrEmpty(p.CoverImagePath) && System.IO.File.Exists(p.CoverImagePath))
                        {
                            try
                            {
                                string name = $"covers/{++coverIndex}{Path.GetExtension(p.CoverImagePath).ToLowerInvariant()}";
                                var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                                using (var es = entry.Open())
                                using (var src = System.IO.File.OpenRead(p.CoverImagePath))
                                    await src.CopyToAsync(es);
                                dto.CoverFile = name;
                            }
                            catch { dto.CoverFile = null; }
                        }
                        file.Playlists.Add(dto);
                    }
                    await WriteJsonAsync(zip, EntryName(BackupCategory.Playlists), file);
                }

                if (categories.Contains(BackupCategory.Statistics))
                {
                    var byId = (await Tracks()).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
                    var file = new BackupStatsFile { ListenSeconds = App.PlayStats.ListenSeconds };
                    foreach (var kv in App.PlayStats.SnapshotCounts())
                        if (byId.TryGetValue(kv.Key, out var t)) file.Items.Add(new BackupStatItem { Track = ToRef(t), Count = kv.Value });
                    // Historique date : les evenements embarquent deja titre / artiste / album / genre (pas besoin de la bibliotheque).
                    App.PlayHistory.Flush();
                    file.History = App.PlayHistory.Snapshot();
                    file.HistoryStartUtc = App.PlayHistory.TrackingStartUtc;
                    await WriteJsonAsync(zip, EntryName(BackupCategory.Statistics), file);
                }

                if (categories.Contains(BackupCategory.AiData))
                {
                    var cur = App.Settings.Current;
                    var file = new BackupAiFile
                    {
                        ArtistMappings = new(cur.ArtistMappings),
                        AlbumMappings = new(cur.AlbumMappings),
                        GenreMappings = new(cur.GenreMappings),
                        TrackGenreMappings = new(cur.TrackGenreMappings),
                    };

                    // Modifs d'affichage titre par titre (DB uniquement) : on ne garde que ce qui diffère des tags du fichier.
                    var edits = await CollectDisplayEditsAsync(await Tracks());
                    int coverIndex = 0;
                    var writtenCovers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);   // une pochette partagée (album) n'est écrite qu'une fois
                    foreach (var ed in edits)
                    {
                        if (string.IsNullOrEmpty(ed.LocalCoverPath)) continue;
                        if (writtenCovers.TryGetValue(ed.LocalCoverPath, out string? already)) { ed.CoverFile = already; continue; }
                        try
                        {
                            string name = $"display-covers/{++coverIndex}{Path.GetExtension(ed.LocalCoverPath).ToLowerInvariant()}";
                            var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                            using (var es = entry.Open())
                            using (var src = System.IO.File.OpenRead(ed.LocalCoverPath))
                                await src.CopyToAsync(es);
                            ed.CoverFile = name;
                        }
                        catch { ed.CoverFile = null; }
                        writtenCovers[ed.LocalCoverPath] = ed.CoverFile;
                    }
                    file.TrackEdits = edits;
                    await WriteJsonAsync(zip, EntryName(BackupCategory.AiData), file);
                }

                if (categories.Contains(BackupCategory.Lyrics))
                {
                    var file = new BackupLyricsFile();
                    foreach (var t in await Tracks())
                    {
                        if (!string.IsNullOrWhiteSpace(t.Lyrics))
                            file.Items.Add(new BackupLyricsItem
                            {
                                Path = t.FilePath,
                                Lyrics = t.Lyrics!,
                                Synced = t.LyricsSynced,
                                // Le decalage de synchro suit les paroles : ici celles du cache (celles des titres personnalises partent avec eux).
                                Offset = LyricsOverrideService.TryGet(t.FilePath, out _) ? null : App.MainWindowInstance?.GetLyricsOffsetSeconds(t.FilePath)
                            });
                    }
                    await WriteJsonAsync(zip, EntryName(BackupCategory.Lyrics), file);
                }

                // Paroles personnalisees (choisies ou modifiees a la main) : exportees avec la reference du titre (chemin, artiste, titre, duree).
                if (categories.Contains(BackupCategory.LyricsEdits))
                {
                    var file = new BackupLyricsEditsFile();
                    foreach (var t in await Tracks())
                    {
                        if (LyricsOverrideService.TryGet(t.FilePath, out var custom))
                            file.Items.Add(new BackupLyricsOverrideItem
                            {
                                Track = new BackupTrackRef { Path = t.FilePath, Artist = t.Artist, Title = t.Title, Seconds = t.Duration.TotalSeconds },
                                Lyrics = custom.Lyrics,
                                Synced = custom.Synced,
                                Label = custom.Label,
                                Offset = App.MainWindowInstance?.GetLyricsOffsetSeconds(t.FilePath)
                            });
                    }
                    await WriteJsonAsync(zip, EntryName(BackupCategory.LyricsEdits), file);
                }
            }

            System.IO.File.Move(tmp, zipPath, true);
        }
        catch
        {
            try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Compare chaque titre de la base aux tags de son fichier audio : tout ce qui diffère est une modif faite uniquement
    /// dans l'app (affichage). Les fichiers introuvables sont ignorés (impossible de comparer).
    /// </summary>
    private static async Task<List<BackupDisplayEdit>> CollectDisplayEditsAsync(List<Track> tracks)
    {
        var found = new System.Collections.Concurrent.ConcurrentBag<BackupDisplayEdit>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };

        await Parallel.ForEachAsync(tracks, options, (t, ct) =>
        {
            try
            {
                if (string.IsNullOrEmpty(t.FilePath) || !System.IO.File.Exists(t.FilePath)) return ValueTask.CompletedTask;
                Track? f = App.Scanner.ExtractMetadata(t.FilePath, out byte[]? embedded);
                if (f == null) return ValueTask.CompletedTask;

                var ed = new BackupDisplayEdit
                {
                    Track = new BackupTrackRef { Path = t.FilePath, Artist = f.Artist, Title = f.Title, Seconds = Math.Round(t.Duration.TotalSeconds, 1) }
                };
                bool any = false;
                if (!string.Equals(t.Title, f.Title, StringComparison.Ordinal)) { ed.Title = t.Title; any = true; }
                if (!string.Equals(t.Artist, f.Artist, StringComparison.Ordinal)) { ed.Artist = t.Artist; any = true; }
                if (!string.Equals(t.Album, f.Album, StringComparison.Ordinal)) { ed.Album = t.Album; any = true; }
                if (!string.Equals(t.Genre ?? "", f.Genre ?? "", StringComparison.Ordinal)) { ed.Genre = t.Genre ?? ""; any = true; }
                if (!string.Equals(t.AlbumArtist ?? "", f.AlbumArtist ?? "", StringComparison.Ordinal)) { ed.AlbumArtist = t.AlbumArtist ?? ""; any = true; }
                if (t.Year != f.Year) { ed.Year = t.Year; any = true; }
                if (t.TrackNumber != f.TrackNumber) { ed.TrackNumber = t.TrackNumber; any = true; }

                // Pochette (choisie ou récupérée automatiquement) : exportée sauf si c'est exactement celle intégrée au fichier audio
                // (elle est ré-extraite toute seule au scan).
                string? cp = t.CoverArtPath;
                if (!string.IsNullOrEmpty(cp) && System.IO.File.Exists(cp) && !IsSameAsEmbedded(cp, embedded))
                {
                    ed.LocalCoverPath = cp;
                    any = true;
                }

                if (any) found.Add(ed);
            }
            catch (Exception ex)
            {
                LogService.Write("Backup", ex, "Comparaison tags / fichier");
            }
            return ValueTask.CompletedTask;
        });

        return found.OrderBy(e => e.Track.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsSameAsEmbedded(string coverPath, byte[]? embedded)
    {
        if (embedded == null || embedded.Length == 0) return false;
        try
        {
            if (new FileInfo(coverPath).Length != embedded.Length) return false;
            return System.IO.File.ReadAllBytes(coverPath).AsSpan().SequenceEqual(embedded);
        }
        catch { return false; }
    }

    /// <summary>Vrai si le fichier existe et a exactement le contenu de l'entrée du zip (pour ne pas dupliquer une pochette déjà présente).</summary>
    private static bool EntryEqualsFile(ZipArchiveEntry entry, string? path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return false;
            if (new FileInfo(path).Length != entry.Length) return false;
            using var a = entry.Open();
            using var b = System.IO.File.OpenRead(path);
            var bufA = new byte[81920];
            var bufB = new byte[81920];
            int read;
            while ((read = a.Read(bufA, 0, bufA.Length)) > 0)
            {
                b.ReadExactly(bufB, 0, read);
                if (!bufA.AsSpan(0, read).SequenceEqual(bufB.AsSpan(0, read))) return false;
            }
            return true;
        }
        catch { return false; }
    }

    /// <summary>Sauvegarde automatique de l'etat actuel avant un import (les 5 plus recentes sont conservees).</summary>
    public static async Task<string?> CreateSafetyBackupAsync(IReadOnlyCollection<BackupCategory> categories)
    {
        // Les paroles sont re-telechargeables et alourdiraient inutilement la sauvegarde de securite.
        var cats = categories.Where(c => c != BackupCategory.Lyrics).ToList();
        if (cats.Count == 0) return null;
        try
        {
            Directory.CreateDirectory(BackupsDir);
            string path = Path.Combine(BackupsDir, $"before-import-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            await ExportAsync(path, cats);

            var old = new DirectoryInfo(BackupsDir).GetFiles("before-import-*.zip").OrderByDescending(f => f.CreationTimeUtc).Skip(5);
            foreach (var f in old) { try { f.Delete(); } catch { } }
            return path;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------
    //  Import
    // ------------------------------------------------------------------

    /// <summary>Lit le manifeste. Null si le fichier n'est pas une sauvegarde Resona valide. Renvoie aussi les categories presentes.</summary>
    public static BackupManifest? ReadManifest(string zipPath, out HashSet<BackupCategory> available)
    {
        available = new HashSet<BackupCategory>();
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var manifest = ReadJson<BackupManifest>(zip, "manifest.json");
            if (manifest == null || manifest.Product != "Resona" || manifest.FormatVersion > CurrentFormatVersion) return null;
            foreach (BackupCategory c in Enum.GetValues(typeof(BackupCategory)))
                if (zip.GetEntry(EntryName(c)) != null || (c == BackupCategory.AiData && zip.GetEntry(LegacyAiEntryName) != null)) available.Add(c);
            return manifest;
        }
        catch { return null; }
    }

    private sealed class TrackResolver
    {
        private readonly Dictionary<string, Track> _byPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Track>> _byArtistTitle = new();
        private readonly Dictionary<string, List<Track>> _byFileName = new(StringComparer.OrdinalIgnoreCase);

        private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

        public TrackResolver(IEnumerable<Track> tracks)
        {
            foreach (var t in tracks)
            {
                _byPath[t.FilePath] = t;
                string key = Norm(t.Artist) + "|" + Norm(t.Title);
                if (!_byArtistTitle.TryGetValue(key, out var l1)) _byArtistTitle[key] = l1 = new();
                l1.Add(t);
                string file = Path.GetFileName(t.FilePath);
                if (!_byFileName.TryGetValue(file, out var l2)) _byFileName[file] = l2 = new();
                l2.Add(t);
            }
        }

        /// <summary>1) meme chemin  2) meme artiste + titre (+ duree a 2 s pres)  3) meme nom de fichier, s'il est unique.</summary>
        public Track? Resolve(BackupTrackRef r)
        {
            if (!string.IsNullOrEmpty(r.Path) && _byPath.TryGetValue(r.Path, out var exact)) return exact;

            if (!string.IsNullOrWhiteSpace(r.Title) &&
                _byArtistTitle.TryGetValue(Norm(r.Artist) + "|" + Norm(r.Title), out var list))
            {
                var match = r.Seconds > 0
                    ? list.FirstOrDefault(x => Math.Abs(x.Duration.TotalSeconds - r.Seconds) <= 2)
                    : list.FirstOrDefault();
                if (match != null) return match;
            }

            if (!string.IsNullOrEmpty(r.Path) &&
                _byFileName.TryGetValue(Path.GetFileName(r.Path), out var files) && files.Count == 1)
                return files[0];

            return null;
        }
    }

    public static async Task<BackupImportReport> ImportAsync(string zipPath, IReadOnlyCollection<BackupCategory> categories)
    {
        var report = new BackupImportReport();
        var settingsService = App.Settings;

        // Filet de securite : l'etat actuel est sauvegarde avant toute modification.
        report.SafetyBackupPath = await CreateSafetyBackupAsync(categories);

        using var zip = ZipFile.OpenRead(zipPath);

        TrackResolver? resolver = null;
        async Task<TrackResolver> GetResolver() => resolver ??= new TrackResolver(await App.Cache.LoadAllTracksAsync());

        // ---- Reglages : remplaces, mais on garde ce qui est propre a ce PC ----
        if (categories.Contains(BackupCategory.Settings))
        {
            var imported = ReadJson<AppSettings>(zip, EntryName(BackupCategory.Settings));
            if (imported != null)
            {
                var cur = settingsService.Current;
                imported.MusicFolders = cur.MusicFolders;
                imported.ExcludedFilePaths = cur.ExcludedFilePaths;
                imported.DownloadFolder = cur.DownloadFolder;
                imported.WindowX = cur.WindowX; imported.WindowY = cur.WindowY;
                imported.WindowWidth = cur.WindowWidth; imported.WindowHeight = cur.WindowHeight;
                imported.LastKnownVersion = cur.LastKnownVersion;
                imported.LastSeenVersion = cur.LastSeenVersion;
                imported.LastYtDlpUpdateCheckUtc = cur.LastYtDlpUpdateCheckUtc;
                imported.SavedQueueIds = cur.SavedQueueIds;
                imported.ArtistMappings = cur.ArtistMappings;
                imported.AlbumMappings = cur.AlbumMappings;
                imported.GenreMappings = cur.GenreMappings;
                imported.TrackGenreMappings = cur.TrackGenreMappings;
                imported.HasCompletedOnboarding = cur.HasCompletedOnboarding;
                settingsService.Current = imported;
                SeasonalThemes.EnforceSeason(settingsService.Current);
                report.SettingsApplied = true;
            }
        }

        // ---- Dossiers : fusionnes (on n'ajoute que les dossiers qui existent sur ce PC) ----
        if (categories.Contains(BackupCategory.Folders))
        {
            var f = ReadJson<BackupFoldersFile>(zip, EntryName(BackupCategory.Folders));
            if (f != null)
            {
                var cur = settingsService.Current;
                foreach (string folder in f.MusicFolders)
                {
                    if (cur.MusicFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) continue;
                    if (Directory.Exists(folder)) { cur.MusicFolders.Add(folder); report.FoldersAdded++; }
                    else report.FoldersSkipped++;
                }
                foreach (string path in f.ExcludedFilePaths)
                    if (!cur.ExcludedFilePaths.Contains(path, StringComparer.OrdinalIgnoreCase)) cur.ExcludedFilePaths.Add(path);
                if (!string.IsNullOrWhiteSpace(f.DownloadFolder) && Directory.Exists(f.DownloadFolder))
                    cur.DownloadFolder = f.DownloadFolder;
            }
        }

        // ---- Donnees d'affichage : correspondances, modifs de titres et pochettes fusionnees (l'import l'emporte) ----
        if (categories.Contains(BackupCategory.AiData))
        {
            var ai = ReadJson<BackupAiFile>(zip, EntryName(BackupCategory.AiData)) ?? ReadJson<BackupAiFile>(zip, LegacyAiEntryName);
            if (ai != null)
            {
                var cur = settingsService.Current;
                foreach (var kv in ai.ArtistMappings) { cur.ArtistMappings[kv.Key] = kv.Value; report.AiMappingsMerged++; }
                foreach (var kv in ai.AlbumMappings)  { cur.AlbumMappings[kv.Key] = kv.Value;  report.AiMappingsMerged++; }
                foreach (var kv in ai.GenreMappings)  { cur.GenreMappings[kv.Key] = kv.Value;  report.AiMappingsMerged++; }
                if (ai.TrackGenreMappings != null)
                    foreach (var kv in ai.TrackGenreMappings) { cur.TrackGenreMappings[kv.Key] = kv.Value; report.AiMappingsMerged++; }

                // Modifs d'affichage titre par titre : l'import l'emporte sur les champs concernés (comme les correspondances).
                if (ai.TrackEdits is { Count: > 0 })
                {
                    var res = await GetResolver();
                    var changed = new List<Track>();
                    string coversDir = Path.Combine(AppDataDir, "Covers");
                    var extractedCovers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // pochette partagée : extraite une seule fois
                    foreach (var ed in ai.TrackEdits)
                    {
                        var t = res.Resolve(ed.Track);
                        if (t == null) { report.DisplayEditsMissing++; continue; }

                        bool any = false;
                        if (ed.Title != null && t.Title != ed.Title) { t.Title = ed.Title; any = true; }
                        if (ed.Artist != null && t.Artist != ed.Artist) { t.Artist = ed.Artist; any = true; }
                        if (ed.Album != null && t.Album != ed.Album) { t.Album = ed.Album; any = true; }
                        if (ed.Genre != null && (t.Genre ?? "") != ed.Genre) { t.Genre = ed.Genre; any = true; }
                        if (ed.AlbumArtist != null && (t.AlbumArtist ?? "") != ed.AlbumArtist) { t.AlbumArtist = ed.AlbumArtist; any = true; }
                        if (ed.Year.HasValue && t.Year != ed.Year.Value) { t.Year = ed.Year.Value; any = true; }
                        if (ed.TrackNumber.HasValue && t.TrackNumber != ed.TrackNumber.Value) { t.TrackNumber = ed.TrackNumber.Value; any = true; }

                        if (!string.IsNullOrEmpty(ed.CoverFile))
                        {
                            try
                            {
                                var entry = zip.GetEntry(ed.CoverFile);
                                string ext = Path.GetExtension(ed.CoverFile).ToLowerInvariant();
                                if (entry != null && ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" && !EntryEqualsFile(entry, t.CoverArtPath))
                                {
                                    if (!extractedCovers.TryGetValue(ed.CoverFile, out string? dest))
                                    {
                                        Directory.CreateDirectory(coversDir);
                                        dest = Path.Combine(coversDir, $"display_{Guid.NewGuid():N}{ext}");
                                        entry.ExtractToFile(dest, true);
                                        extractedCovers[ed.CoverFile] = dest;
                                    }
                                    t.CoverArtPath = dest;
                                    any = true;
                                }
                            }
                            catch { }
                        }

                        if (any) { changed.Add(t); report.DisplayEditsApplied++; }
                    }
                    if (changed.Count > 0) await App.Cache.UpsertTracksBatchedAsync(changed);
                }
            }
        }

        // ---- Playlists : fusionnees (titres retrouves par chemin, puis artiste + titre) ----
        if (categories.Contains(BackupCategory.Playlists))
        {
            var file = ReadJson<BackupPlaylistsFile>(zip, EntryName(BackupCategory.Playlists));
            if (file != null)
            {
                var res = await GetResolver();
                var existing = await App.Cache.LoadAllPlaylistsAsync();
                string coversDir = Path.Combine(AppDataDir, "PlaylistCovers");

                foreach (var dto in file.Playlists)
                {
                    bool isFavorites = dto.IsSystem || dto.Id == Resona.Models.Playlist.FavoritesId;
                    Resona.Models.Playlist? target = isFavorites
                        ? existing.FirstOrDefault(p => p.Id == Resona.Models.Playlist.FavoritesId)
                        : existing.FirstOrDefault(p => !p.IsSystem &&
                              (p.Id == dto.Id || string.Equals(p.Name, dto.Name, StringComparison.OrdinalIgnoreCase)));

                    if (target == null)
                    {
                        if (isFavorites) continue; // pas de playlist Favoris en base : rien a fusionner
                        target = new Resona.Models.Playlist { Id = dto.Id, Name = dto.Name, DateCreated = dto.DateCreated };
                        existing.Add(target);
                    }

                    // Playlist intelligente : les regles sont reprises pour une playlist creee par l'import ou deja intelligente
                    // (une playlist classique du meme nom n'est pas transformee).
                    if (!target.IsSystem && !string.IsNullOrWhiteSpace(dto.SmartRulesJson) && (target.TrackIds.Count == 0 || target.IsSmart()))
                        target.SmartRulesJson = dto.SmartRulesJson;

                    var ids = new HashSet<string>(target.TrackIds);
                    foreach (var r in dto.Tracks)
                    {
                        var t = res.Resolve(r);
                        if (t == null) { report.PlaylistTracksMissing++; continue; }
                        if (ids.Add(t.Id)) { target.TrackIds.Add(t.Id); report.PlaylistTracksAdded++; }
                    }

                    // Pochette personnalisee : seulement si la playlist n'en a pas deja une.
                    bool hasCover = !string.IsNullOrEmpty(target.CoverImagePath) && System.IO.File.Exists(target.CoverImagePath);
                    if (!target.IsSystem && !hasCover && !string.IsNullOrEmpty(dto.CoverFile))
                    {
                        try
                        {
                            var entry = zip.GetEntry(dto.CoverFile);
                            string ext = Path.GetExtension(dto.CoverFile).ToLowerInvariant();
                            if (entry != null && ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp")
                            {
                                Directory.CreateDirectory(coversDir);
                                string dest = Path.Combine(coversDir, Guid.NewGuid().ToString("N") + ext);
                                entry.ExtractToFile(dest, true);
                                target.CoverImagePath = dest;
                            }
                        }
                        catch { }
                    }

                    target.DateModified = DateTime.UtcNow;
                    await App.Cache.UpsertPlaylistAsync(target);
                    report.PlaylistsImported++;
                }
            }
        }

        // ---- Statistiques : on garde la valeur la plus elevee par titre ----
        if (categories.Contains(BackupCategory.Statistics))
        {
            var file = ReadJson<BackupStatsFile>(zip, EntryName(BackupCategory.Statistics));
            if (file != null)
            {
                var res = await GetResolver();
                var counts = new Dictionary<string, int>();
                foreach (var item in file.Items)
                {
                    var t = res.Resolve(item.Track);
                    if (t == null) continue;
                    counts[t.Id] = Math.Max(counts.GetValueOrDefault(t.Id), item.Count);
                }
                report.StatsUpdated = App.PlayStats.MergeCounts(counts, file.ListenSeconds);
                await App.PlayStats.SaveAsync();

                // Historique date : fusion (les ecoutes deja presentes ne sont pas dupliquees).
                if (file.History != null)
                    report.HistoryEventsAdded = App.PlayHistory.MergeEvents(file.History, file.HistoryStartUtc);
            }
        }

        // ---- Paroles : on complete seulement les titres qui n'en ont pas ----
        if (categories.Contains(BackupCategory.Lyrics))
        {
            var file = ReadJson<BackupLyricsFile>(zip, EntryName(BackupCategory.Lyrics));
            if (file != null)
            {
                var res = await GetResolver();
                var changed = new List<Track>();
                foreach (var item in file.Items)
                {
                    var t = res.Resolve(new BackupTrackRef { Path = item.Path });
                    if (t != null && item.Offset.HasValue) App.MainWindowInstance?.SetLyricsOffsetIfMissing(t.FilePath, item.Offset.Value);
                    if (t == null || !string.IsNullOrWhiteSpace(t.Lyrics) || string.IsNullOrWhiteSpace(item.Lyrics)) continue;
                    t.Lyrics = item.Lyrics;
                    t.LyricsSynced = item.Synced;
                    changed.Add(t);
                }
                if (changed.Count > 0) await App.Cache.UpsertTracksBatchedAsync(changed);
                report.LyricsApplied = changed.Count;

                // Paroles choisies a la main : un choix deja fait sur ce PC est conserve, on ne complete que les titres sans choix.
                foreach (var ov in file.Overrides)
                {
                    var target = res.Resolve(ov.Track);
                    if (target == null || string.IsNullOrWhiteSpace(ov.Lyrics)) continue;
                    if (LyricsOverrideService.TryGet(target.FilePath, out _)) continue;
                    LyricsOverrideService.Set(target.FilePath, ov.Lyrics, ov.Synced, ov.Label);
                    report.LyricsApplied++;
                }
            }
        }

        // ---- Paroles personnalisees : un choix deja present sur ce PC est conserve, on ne complete que les titres sans choix ----
        if (categories.Contains(BackupCategory.LyricsEdits))
        {
            var file = ReadJson<BackupLyricsEditsFile>(zip, EntryName(BackupCategory.LyricsEdits));
            if (file != null)
            {
                var res = await GetResolver();
                foreach (var item in file.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Lyrics)) continue;
                    var target = res.Resolve(item.Track);
                    if (target == null) { report.LyricsEditsMissing++; continue; }
                    if (LyricsOverrideService.TryGet(target.FilePath, out _)) { report.LyricsEditsKept++; continue; }
                    LyricsOverrideService.Set(target.FilePath, item.Lyrics, item.Synced, item.Label);
                    if (item.Offset.HasValue) App.MainWindowInstance?.SetLyricsOffsetIfMissing(target.FilePath, item.Offset.Value);
                    report.LyricsEditsApplied++;
                }
            }
        }

        // Une seule ecriture des reglages a la fin.
        if (categories.Contains(BackupCategory.Settings) || categories.Contains(BackupCategory.Folders) || categories.Contains(BackupCategory.AiData))
            settingsService.SaveSync();

        return report;
    }
}
