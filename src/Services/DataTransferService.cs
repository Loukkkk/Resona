// ============================================================
//  DataTransferService.cs  —  Resona
//  Import / export des reglages et des donnees de la bibliotheque dans un fichier .zip :
//    manifest.json    : identifie la sauvegarde (produit, version du format, version de l'app, date)
//    settings.json    : reglages de l'app (sans les dossiers / chemins / positions propres a ce PC)
//    folders.json     : dossiers de musique, dossier de telechargement, fichiers exclus
//    playlists.json   : playlists + Favoris (titres references par chemin, pas par identifiant interne) + covers/
//    statistics.json  : nombre d'ecoutes par titre + temps d'ecoute total
//    ai.json          : correspondances artistes / albums / genres + contenu de ai_backup.json
//    lyrics.json      : paroles en cache
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

public enum BackupCategory { Settings, Folders, Playlists, Statistics, AiData, Lyrics }

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
    public int  AiMappingsMerged;
    public bool AiBackupWritten, AiBackupKept;
    public int  LyricsApplied;
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
    public List<BackupTrackRef> Tracks { get; set; } = new();
}

internal sealed class BackupPlaylistsFile { public List<BackupPlaylist> Playlists { get; set; } = new(); }

internal sealed class BackupStatItem { public BackupTrackRef Track { get; set; } = new(); public int Count { get; set; } }
internal sealed class BackupStatsFile { public long ListenSeconds { get; set; } public List<BackupStatItem> Items { get; set; } = new(); }

internal sealed class BackupFoldersFile
{
    public List<string> MusicFolders       { get; set; } = new();
    public List<string> ExcludedFilePaths  { get; set; } = new();
    public string       DownloadFolder     { get; set; } = "";
}

internal sealed class BackupAiFile
{
    public Dictionary<string, string> ArtistMappings { get; set; } = new();
    public Dictionary<string, string> AlbumMappings  { get; set; } = new();
    public Dictionary<string, string> GenreMappings  { get; set; } = new();
    public string? TagBackupJson { get; set; }
}

internal sealed class BackupLyricsItem { public string Path { get; set; } = ""; public string Lyrics { get; set; } = ""; public bool Synced { get; set; } }
internal sealed class BackupLyricsFile { public List<BackupLyricsItem> Items { get; set; } = new(); }

public static class DataTransferService
{
    private const int CurrentFormatVersion = 1;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string AppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona");
    private static string AiBackupPath => Path.Combine(AppDataDir, "ai_backup.json");
    public static string BackupsDir => Path.Combine(AppDataDir, "backups");

    public static string SuggestedFileName() => $"Resona-backup-{DateTime.Now:yyyyMMdd}.zip";

    private static string EntryName(BackupCategory c) => c switch
    {
        BackupCategory.Settings   => "settings.json",
        BackupCategory.Folders    => "folders.json",
        BackupCategory.Playlists  => "playlists.json",
        BackupCategory.Statistics => "statistics.json",
        BackupCategory.AiData     => "ai.json",
        BackupCategory.Lyrics     => "lyrics.json",
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
        s.ArtistMappings = new(); s.AlbumMappings = new(); s.GenreMappings = new();
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
                            DateCreated = p.DateCreated, DateModified = p.DateModified
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
                    };
                    try { if (System.IO.File.Exists(AiBackupPath)) file.TagBackupJson = await System.IO.File.ReadAllTextAsync(AiBackupPath); } catch { }
                    await WriteJsonAsync(zip, EntryName(BackupCategory.AiData), file);
                }

                if (categories.Contains(BackupCategory.Lyrics))
                {
                    var file = new BackupLyricsFile();
                    foreach (var t in await Tracks())
                        if (!string.IsNullOrWhiteSpace(t.Lyrics))
                            file.Items.Add(new BackupLyricsItem { Path = t.FilePath, Lyrics = t.Lyrics!, Synced = t.LyricsSynced });
                    await WriteJsonAsync(zip, EntryName(BackupCategory.Lyrics), file);
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
                if (zip.GetEntry(EntryName(c)) != null) available.Add(c);
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

        // ---- Donnees IA : correspondances fusionnees (l'import l'emporte), ai_backup.json garde s'il existe deja ----
        if (categories.Contains(BackupCategory.AiData))
        {
            var ai = ReadJson<BackupAiFile>(zip, EntryName(BackupCategory.AiData));
            if (ai != null)
            {
                var cur = settingsService.Current;
                foreach (var kv in ai.ArtistMappings) { cur.ArtistMappings[kv.Key] = kv.Value; report.AiMappingsMerged++; }
                foreach (var kv in ai.AlbumMappings)  { cur.AlbumMappings[kv.Key] = kv.Value;  report.AiMappingsMerged++; }
                foreach (var kv in ai.GenreMappings)  { cur.GenreMappings[kv.Key] = kv.Value;  report.AiMappingsMerged++; }

                if (!string.IsNullOrWhiteSpace(ai.TagBackupJson))
                {
                    if (System.IO.File.Exists(AiBackupPath)) report.AiBackupKept = true;
                    else
                    {
                        try { await System.IO.File.WriteAllTextAsync(AiBackupPath, ai.TagBackupJson); report.AiBackupWritten = true; } catch { }
                    }
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
                    if (t == null || !string.IsNullOrWhiteSpace(t.Lyrics) || string.IsNullOrWhiteSpace(item.Lyrics)) continue;
                    t.Lyrics = item.Lyrics;
                    t.LyricsSynced = item.Synced;
                    changed.Add(t);
                }
                if (changed.Count > 0) await App.Cache.UpsertTracksBatchedAsync(changed);
                report.LyricsApplied = changed.Count;
            }
        }

        // Une seule ecriture des reglages a la fin.
        if (categories.Contains(BackupCategory.Settings) || categories.Contains(BackupCategory.Folders) || categories.Contains(BackupCategory.AiData))
            settingsService.SaveSync();

        return report;
    }
}
