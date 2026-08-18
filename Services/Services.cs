// ============================================================
//  Services.cs  —  Resona
//  Fusion de : CoverArtService, LyricsService, PlaylistM3uService,
//              SettingsService, LibraryScannerService, LibraryCacheService,
//              DownloadService
// ============================================================

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Concentus.Oggfile;
using Concentus;
using Resona.Models;

namespace Resona.Services;

public static class CoverCacheService
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool exists, DateTime checkedAtUtc)> _existsCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan ExistsCacheTtl = TimeSpan.FromSeconds(30);

    public static bool Exists(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        if (_existsCache.TryGetValue(path, out var cached) &&
            (DateTime.UtcNow - cached.checkedAtUtc) < ExistsCacheTtl)
        {
            return cached.exists;
        }

        bool exists;
        try { exists = System.IO.File.Exists(path); } catch { exists = false; }
        _existsCache[path] = (exists, DateTime.UtcNow);
        return exists;
    }

    public static void ClearCache(string path)
    {
        _existsCache.TryRemove(path, out _);
        _bitmapCache.Remove(path);
    }

    private const int MaxEntries = 500;
    private static readonly Dictionary<string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> _bitmapOrder = new();

    public static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? GetBitmap(string? path, int decodePixelWidth)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_bitmapCache.TryGetValue(path, out var cached)) return cached;

        try
        {
            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = decodePixelWidth };
            bmp.UriSource = new Uri(path);

            if (_bitmapCache.Count >= MaxEntries && _bitmapOrder.Count > 0)
            {
                var oldest = _bitmapOrder.Dequeue();
                _bitmapCache.Remove(oldest);
            }

            _bitmapCache[path] = bmp;
            _bitmapOrder.Enqueue(path);
            return bmp;
        }
        catch { return null; }
    }

    public static void Clear()
    {
        _existsCache.Clear();
        _bitmapCache.Clear();
        _bitmapOrder.Clear();
    }
}

public class CoverArtService
{
    private readonly HttpClient _http;
    private readonly string _cacheDir;

    public CoverArtService(HttpClient http, string cacheDir)
    {
        _http = http;
        _cacheDir = cacheDir;
        Directory.CreateDirectory(_cacheDir);
    }

    private class ItunesResponse
    {
        [JsonPropertyName("results")] public ItunesResult[] Results { get; set; } = Array.Empty<ItunesResult>();
    }
    private class ItunesResult
    {
        [JsonPropertyName("artworkUrl100")] public string? ArtworkUrl100 { get; set; }
    }

    public async Task<string?> SaveEmbeddedCoverAsync(string trackId, byte[] imageBytes)
    {
        if (imageBytes.Length == 0) return null;
        string cachePath = Path.Combine(_cacheDir, $"{trackId}.jpg");
        try { await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes); return cachePath; }
        catch { return null; }
    }

    public async Task<string?> FindAndCacheCoverAsync(string trackId, string artist, string album)
    {
        string cachePath = Path.Combine(_cacheDir, $"{trackId}.jpg");
        if (System.IO.File.Exists(cachePath)) return cachePath;

        string? imageBytes = null;
        try
        {
            string term = Uri.EscapeDataString($"{artist} {album}");
            var response = await _http.GetFromJsonAsync<ItunesResponse>($"https://itunes.apple.com/search?term={term}&entity=album&limit=3");
            var artworkUrl = response?.Results.Length > 0 ? response.Results[0].ArtworkUrl100 : null;
            if (!string.IsNullOrEmpty(artworkUrl))
                imageBytes = artworkUrl.Replace("100x100bb", "1200x1200bb");
        }
        catch { }

        if (string.IsNullOrEmpty(imageBytes))
        {
            try
            {
                string term = Uri.EscapeDataString($"{artist} {album}");
                var deezerResp = await _http.GetFromJsonAsync<DeezerAlbumSearchResponse>($"https://api.deezer.com/search/album?q={term}&limit=3");
                if (deezerResp?.data?.Count > 0 && !string.IsNullOrEmpty(deezerResp.data[0].cover_medium))
                    imageBytes = deezerResp.data[0].cover_medium!.Replace("250x250", "500x500");
            }
            catch { }
        }

        if (string.IsNullOrEmpty(imageBytes)) return null;
        try
        {
            byte[] bytes = await _http.GetByteArrayAsync(imageBytes);
            await System.IO.File.WriteAllBytesAsync(cachePath, bytes);
            return cachePath;
        }
        catch { return null; }
    }

    private class DeezerAlbumSearchResponse
    {
        public List<DeezerAlbumData>? data { get; set; }
    }
    private class DeezerAlbumData
    {
        public string? cover_medium { get; set; }
    }
}

public class LyricsResult
{
    public string? PlainLyrics { get; set; }
    public string? SyncedLyrics { get; set; }
    public bool Found => !string.IsNullOrWhiteSpace(PlainLyrics) || !string.IsNullOrWhiteSpace(SyncedLyrics);
}

public class LyricsService
{
    private readonly HttpClient _http;

    public LyricsService(HttpClient http)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://lrclib.net/api/");
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd("Resona/1.0");
    }

    private class LrcLibResponse
    {
        [JsonPropertyName("plainLyrics")]  public string? PlainLyrics  { get; set; }
        [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; set; }
        [JsonPropertyName("duration")]     public double? Duration     { get; set; }
    }

    public async Task<LyricsResult> SearchAsync(string artist, string title, string? album = null, TimeSpan? duration = null)
    {
        try
        {
            string query = $"search?q={Uri.EscapeDataString(artist + " " + title)}";
            var results = await _http.GetFromJsonAsync<List<LrcLibResponse>>(query);
            if (results != null && results.Count > 0)
            {
                var validResults = results.Where(r => !string.IsNullOrWhiteSpace(r.PlainLyrics) || !string.IsNullOrWhiteSpace(r.SyncedLyrics)).ToList();
                if (validResults.Count > 0)
                {
                    LrcLibResponse bestMatch = validResults[0];
                    if (duration.HasValue)
                    {
                        double targetSec = duration.Value.TotalSeconds;
                        bestMatch = validResults.OrderBy(r => r.Duration.HasValue ? Math.Abs(r.Duration.Value - targetSec) : double.MaxValue).First();
                    }
                    return new LyricsResult { PlainLyrics = bestMatch.PlainLyrics, SyncedLyrics = bestMatch.SyncedLyrics };
                }
            }
        }
        catch { }

        try
        {
            string searchQuery = Uri.EscapeDataString($"{artist} {title}");
            using var netease = new HttpClient();
            netease.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0");
            var searchResp = System.Text.Encoding.UTF8.GetString(await netease.GetByteArrayAsync($"https://music.163.com/api/search/pc?s={searchQuery}&offset=0&limit=5&type=1"));
            var doc = System.Text.Json.JsonDocument.Parse(searchResp);
            if (doc.RootElement.TryGetProperty("result", out var resultElem) &&
                resultElem.TryGetProperty("songs", out var songs) &&
                songs.GetArrayLength() > 0)
            {
                long songId = songs[0].GetProperty("id").GetInt64();
                var lyricResp = System.Text.Encoding.UTF8.GetString(await netease.GetByteArrayAsync($"https://music.163.com/api/song/lyric?id={songId}&lv=-1&kv=-1&tv=-1"));
                var lyricDoc = System.Text.Json.JsonDocument.Parse(lyricResp);
                string? lrc = null, plain = null;
                if (lyricDoc.RootElement.TryGetProperty("lrc", out var lrcElem) &&
                    lrcElem.TryGetProperty("lyric", out var lrcText))
                    lrc = lrcText.GetString();
                if (lyricDoc.RootElement.TryGetProperty("klyric", out var kElem) &&
                    kElem.TryGetProperty("lyric", out var kText))
                    plain = kText.GetString();
                if (!string.IsNullOrWhiteSpace(lrc) || !string.IsNullOrWhiteSpace(plain))
                    return new LyricsResult { PlainLyrics = plain ?? lrc, SyncedLyrics = lrc };
            }
        }
        catch { }

        return new LyricsResult();
    }
}

public class PlaylistM3uService
{
    public async Task<(List<string> resolvedPaths, List<string> missing)> ImportAsync(string playlistFilePath)
    {
        var resolved = new List<string>();
        var missing  = new List<string>();
        string baseDir = Path.GetDirectoryName(playlistFilePath) ?? string.Empty;
        var lines = await System.IO.File.ReadAllLinesAsync(playlistFilePath, DetectEncoding(playlistFilePath));
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string fullPath = Path.IsPathRooted(line)
                ? line
                : Path.GetFullPath(Path.Combine(baseDir, line));
            if (System.IO.File.Exists(fullPath)) resolved.Add(fullPath);
            else                       missing.Add(line);
        }
        return (resolved, missing);
    }

    public async Task ExportAsync(string outputPath, IEnumerable<Track> tracks, bool useRelativePaths = true)
    {
        bool isM3u8 = Path.GetExtension(outputPath).Equals(".m3u8", StringComparison.OrdinalIgnoreCase);
        var encoding = isM3u8 ? new UTF8Encoding(false) : Encoding.GetEncoding(1252);
        string baseDir = Path.GetDirectoryName(outputPath) ?? string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        foreach (var track in tracks)
        {
            int durationSeconds = (int)track.Duration.TotalSeconds;
            sb.AppendLine($"#EXTINF:{durationSeconds},{track.Artist} - {track.Title}");
            sb.AppendLine(useRelativePaths ? Path.GetRelativePath(baseDir, track.FilePath) : track.FilePath);
        }
        await System.IO.File.WriteAllTextAsync(outputPath, sb.ToString(), encoding);
    }

    private static Encoding DetectEncoding(string filePath)
    {
        if (Path.GetExtension(filePath).Equals(".m3u8", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8;
        using var reader = new StreamReader(filePath, Encoding.UTF8, true);
        reader.Read();
        return reader.CurrentEncoding;
    }
}

public class SettingsService
{
    private readonly string _filePath;
    public AppSettings Current { get; private set; } = new();
    public event EventHandler? SettingsChanged;

    public SettingsService(string appDataDir)
    {
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "settings.json");
    }

    public async Task LoadAsync()
    {
        if (!System.IO.File.Exists(_filePath)) { Current = new AppSettings(); await SaveAsync(); return; }
        try
        {
            string json = await System.IO.File.ReadAllTextAsync(_filePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { Current = new AppSettings(); }
    }

    public void LoadSync()
    {
        if (!System.IO.File.Exists(_filePath)) { Current = new AppSettings(); SaveSync(); return; }
        try
        {
            string json = System.IO.File.ReadAllText(_filePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { Current = new AppSettings(); }
    }

    private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public void SaveSync()
    {
        _saveLock.Wait();
        try
        {
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_filePath, json);
        }
        catch { }
        finally
        {
            _saveLock.Release();
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(_filePath, json);
        }
        catch { }
        finally
        {
            _saveLock.Release();
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class LibraryScannerService
{
    private static readonly string[] SupportedExtensions =
    {
        ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma", ".alac", ".aiff", ".aif",
        ".opus", ".ape", ".mp4", ".m4b", ".dsf", ".dff", ".wv", ".tta", ".spx"
    };

    public IEnumerable<string> EnumerateAudioFiles(string rootFolder)
    {
        if (!Directory.Exists(rootFolder)) yield break;
        var stack = new Stack<string>();
        stack.Push(rootFolder);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch { continue; }
            foreach (var entry in entries)
            {
                if (Directory.Exists(entry)) stack.Push(entry);
                else if (SupportedExtensions.Contains(Path.GetExtension(entry).ToLowerInvariant()))
                    yield return entry;
            }
        }
    }

    public Track? ExtractMetadata(string filePath) => ExtractMetadata(filePath, out _);

    internal static string ResolveRealExtension(string filePath)
    {
        try
        {
            using var fs = System.IO.File.OpenRead(filePath);
            var header = new byte[12];
            int read = fs.Read(header, 0, 12);
            if (read < 8) return filePath;

            if (header[0] == 0x66 && header[1] == 0x4C && header[2] == 0x61 && header[3] == 0x43)
                return Path.ChangeExtension(filePath, ".flac");
            if (header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
                return Path.ChangeExtension(filePath, ".ogg");
            if (header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
                return Path.ChangeExtension(filePath, ".m4a");

            long audioOffset = 0;
            if (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
            {
                fs.Seek(6, SeekOrigin.Begin);
                var szBytes = new byte[4];
                fs.Read(szBytes, 0, 4);
                int id3Size = ((szBytes[0] & 0x7f) << 21) | ((szBytes[1] & 0x7f) << 14)
                            | ((szBytes[2] & 0x7f) << 7)  |  (szBytes[3] & 0x7f);
                audioOffset = 10 + id3Size;

                fs.Seek(audioOffset, SeekOrigin.Begin);
                var afterHeader = new byte[8];
                if (fs.Read(afterHeader, 0, 8) >= 8 &&
                    afterHeader[4] == 0x66 && afterHeader[5] == 0x74 &&
                    afterHeader[6] == 0x79 && afterHeader[7] == 0x70)
                    return Path.ChangeExtension(filePath, ".m4a");
            }

            fs.Seek(audioOffset, SeekOrigin.Begin);
            var sync = new byte[3];
            if (fs.Read(sync, 0, 3) < 3) return filePath;

            if (sync[0] != 0xFF || (sync[1] & 0xE0) != 0xE0) return filePath;

            int layer = (sync[1] >> 1) & 0x03;
            if (layer == 0)
                return Path.ChangeExtension(filePath, ".m4a");
        }
        catch { }
        return filePath;
    }

    public Track? ExtractMetadata(string filePath, out byte[]? embeddedCoverBytes)
    {
        embeddedCoverBytes = null;
        string resolvedPath = ResolveRealExtension(filePath);
        string? tempLink = null;
        try
        {
            string pathToOpen = filePath;
            if (resolvedPath != filePath)
            {
                tempLink = Path.Combine(Path.GetTempPath(),
                    Guid.NewGuid().ToString("N") + Path.GetExtension(resolvedPath));
                try { System.IO.File.CreateSymbolicLink(tempLink, filePath); }
                catch { System.IO.File.Copy(filePath, tempLink, overwrite: true); }
                pathToOpen = tempLink;
            }

            using var file = TagLib.File.Create(pathToOpen);
            var tag = file.Tag;
            if (tag.Pictures.Length > 0) embeddedCoverBytes = tag.Pictures[0].Data.Data;

            return new Track
            {
                FilePath     = filePath,
                Title        = string.IsNullOrWhiteSpace(tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : tag.Title,
                Artist       = tag.Performers.Length > 0 ? string.Join(", ", tag.Performers) : "Artiste inconnu",
                Album        = string.IsNullOrWhiteSpace(tag.Album) ? "Album inconnu" : tag.Album,
                AlbumArtist  = tag.AlbumArtists.Length > 0 ? string.Join(", ", tag.AlbumArtists) : string.Empty,
                Duration     = file.Properties.Duration,
                TrackNumber  = (int)tag.Track,
                Year         = (int)tag.Year,
                Genre        = tag.Genres.Length > 0 ? string.Join(", ", tag.Genres) : string.Empty,
                LastModified = System.IO.File.GetLastWriteTimeUtc(filePath)
            };
        }
        catch
        {
            try
            {
                TimeSpan duration = TimeSpan.Zero;
                try
                {
                    string localFfmpeg = System.IO.Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                    string exe = System.IO.File.Exists(localFfmpeg) ? localFfmpeg : "ffmpeg";
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = $"-i \"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        string output = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();
                        var match = System.Text.RegularExpressions.Regex.Match(output, @"Duration:\s*(\d+):(\d{2}):(\d{2})\.(\d+)");
                        if (match.Success)
                        {
                            duration = new TimeSpan(0, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value) * 10);
                        }
                    }
                }
                catch { }

                return new Track
                {
                    FilePath     = filePath,
                    Title        = Path.GetFileNameWithoutExtension(filePath),
                    Artist       = "Artiste inconnu",
                    Album        = "Album inconnu",
                    Duration     = duration,
                    LastModified = System.IO.File.GetLastWriteTimeUtc(filePath)
                };
            }
            catch { return null; }
        }
        finally
        {
            if (tempLink != null) try { System.IO.File.Delete(tempLink); } catch { }
        }
    }

    public byte[]? ExtractEmbeddedCover(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            var pictures = file.Tag.Pictures;
            return pictures.Length > 0 ? pictures[0].Data.Data : null;
        }
        catch { return null; }
    }
}

public class LibraryCacheService
{
    private readonly string _connectionString;

    public LibraryCacheService(string appDataDir)
    {
        Directory.CreateDirectory(appDataDir);
        string dbPath = Path.Combine(appDataDir, "library_cache.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Tracks (
                Id TEXT PRIMARY KEY,
                FilePath TEXT NOT NULL UNIQUE,
                Title TEXT, Artist TEXT, Album TEXT, AlbumArtist TEXT,
                DurationTicks INTEGER, TrackNumber INTEGER, Year INTEGER,
                Genre TEXT, CoverArtPath TEXT, Lyrics TEXT, LyricsSynced INTEGER,
                NormalizationGainDb REAL, IsAnalyzed INTEGER,
                DateAdded TEXT, LastModified TEXT
            );
            CREATE TABLE IF NOT EXISTS Playlists (
                Id TEXT PRIMARY KEY,
                Name TEXT, TrackIdsJson TEXT, CoverImagePath TEXT, DateCreated TEXT, DateModified TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_tracks_path ON Tracks(FilePath);
            """;
        await cmd.ExecuteNonQueryAsync();

        // Migrate existing databases for CoverImagePath
        try
        {
            var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Playlists ADD COLUMN CoverImagePath TEXT;";
            await alterCmd.ExecuteNonQueryAsync();
        }
        catch { /* Column probably already exists */ }
    }

    public async Task ClearAnalysisAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Tracks SET IsAnalyzed = 0";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Track>> LoadAllTracksAsync()
    {
        var tracks = new List<Track>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tracks ORDER BY Artist, Album, TrackNumber";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tracks.Add(new Track
            {
                Id                  = SafeGetString(reader, "Id"),
                FilePath            = SafeGetString(reader, "FilePath"),
                Title               = SafeGetString(reader, "Title"),
                Artist              = SafeGetString(reader, "Artist"),
                Album               = SafeGetString(reader, Resona.Models.Strings.Current.CS_Services_Album),
                AlbumArtist         = SafeGetString(reader, "AlbumArtist"),
                Duration            = TimeSpan.FromTicks(SafeGetInt64(reader, "DurationTicks")),
                TrackNumber         = SafeGetInt32(reader, "TrackNumber"),
                Year                = SafeGetInt32(reader, "Year"),
                Genre               = SafeGetString(reader, Resona.Models.Strings.Current.CS_Services_Genre),
                CoverArtPath        = SafeGetStringOrNull(reader, "CoverArtPath"),
                Lyrics              = SafeGetStringOrNull(reader, "Lyrics"),
                LyricsSynced        = SafeGetInt32(reader, "LyricsSynced") == 1,
                NormalizationGainDb = SafeGetDouble(reader, "NormalizationGainDb"),
                IsAnalyzed          = SafeGetInt32(reader, "IsAnalyzed") == 1,
                DateAdded           = SafeGetDateTime(reader, "DateAdded"),
                LastModified        = SafeGetDateTime(reader, "LastModified")
            });
        }
        return tracks;
    }

    public async Task UpsertTrackAsync(Track track)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Tracks (Id, FilePath, Title, Artist, Album, AlbumArtist, DurationTicks,
                TrackNumber, Year, Genre, CoverArtPath, Lyrics, LyricsSynced, NormalizationGainDb,
                IsAnalyzed, DateAdded, LastModified)
            VALUES ($id, $path, $title, $artist, $album, $albumArtist, $duration,
                $trackNum, $year, $genre, $cover, $lyrics, $synced, $gain,
                $analyzed, $added, $modified)
            ON CONFLICT(FilePath) DO UPDATE SET
                Title=excluded.Title, Artist=excluded.Artist, Album=excluded.Album,
                AlbumArtist=excluded.AlbumArtist, DurationTicks=excluded.DurationTicks,
                TrackNumber=excluded.TrackNumber, Year=excluded.Year, Genre=excluded.Genre,
                CoverArtPath=excluded.CoverArtPath, Lyrics=excluded.Lyrics,
                LyricsSynced=excluded.LyricsSynced, NormalizationGainDb=excluded.NormalizationGainDb,
                IsAnalyzed=excluded.IsAnalyzed, LastModified=excluded.LastModified;
            """;
        cmd.Parameters.AddWithValue("$id",          track.Id);
        cmd.Parameters.AddWithValue("$path",        track.FilePath);
        cmd.Parameters.AddWithValue("$title",       track.Title);
        cmd.Parameters.AddWithValue("$artist",      track.Artist);
        cmd.Parameters.AddWithValue("$album",       track.Album);
        cmd.Parameters.AddWithValue("$albumArtist", track.AlbumArtist);
        cmd.Parameters.AddWithValue("$duration",    track.Duration.Ticks);
        cmd.Parameters.AddWithValue("$trackNum",    track.TrackNumber);
        cmd.Parameters.AddWithValue("$year",        track.Year);
        cmd.Parameters.AddWithValue("$genre",       track.Genre);
        cmd.Parameters.AddWithValue("$cover",       (object?)track.CoverArtPath   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lyrics",      (object?)track.Lyrics         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$synced",      track.LyricsSynced ? 1 : 0);
        cmd.Parameters.AddWithValue("$gain",        track.NormalizationGainDb);
        cmd.Parameters.AddWithValue("$analyzed",    track.IsAnalyzed ? 1 : 0);
        cmd.Parameters.AddWithValue("$added",       track.DateAdded.ToString("o"));
        cmd.Parameters.AddWithValue("$modified",    track.LastModified.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertTracksBatchedAsync(IEnumerable<Track> tracks)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        
        var cmd = conn.CreateCommand();
        cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = """
            INSERT INTO Tracks (Id, FilePath, Title, Artist, Album, AlbumArtist, DurationTicks,
                TrackNumber, Year, Genre, CoverArtPath, Lyrics, LyricsSynced, NormalizationGainDb,
                IsAnalyzed, DateAdded, LastModified)
            VALUES ($id, $path, $title, $artist, $album, $albumArtist, $duration,
                $trackNum, $year, $genre, $cover, $lyrics, $synced, $gain,
                $analyzed, $added, $modified)
            ON CONFLICT(FilePath) DO UPDATE SET
                Title=excluded.Title, Artist=excluded.Artist, Album=excluded.Album,
                AlbumArtist=excluded.AlbumArtist, DurationTicks=excluded.DurationTicks,
                TrackNumber=excluded.TrackNumber, Year=excluded.Year, Genre=excluded.Genre,
                CoverArtPath=excluded.CoverArtPath, Lyrics=excluded.Lyrics,
                LyricsSynced=excluded.LyricsSynced, NormalizationGainDb=excluded.NormalizationGainDb,
                IsAnalyzed=excluded.IsAnalyzed, LastModified=excluded.LastModified;
            """;

        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pPath = cmd.Parameters.Add("$path", SqliteType.Text);
        var pTitle = cmd.Parameters.Add("$title", SqliteType.Text);
        var pArtist = cmd.Parameters.Add("$artist", SqliteType.Text);
        var pAlbum = cmd.Parameters.Add("$album", SqliteType.Text);
        var pAlbumArtist = cmd.Parameters.Add("$albumArtist", SqliteType.Text);
        var pDuration = cmd.Parameters.Add("$duration", SqliteType.Integer);
        var pTrackNum = cmd.Parameters.Add("$trackNum", SqliteType.Integer);
        var pYear = cmd.Parameters.Add("$year", SqliteType.Integer);
        var pGenre = cmd.Parameters.Add("$genre", SqliteType.Text);
        var pCover = cmd.Parameters.Add("$cover", SqliteType.Text);
        var pLyrics = cmd.Parameters.Add("$lyrics", SqliteType.Text);
        var pSynced = cmd.Parameters.Add("$synced", SqliteType.Integer);
        var pGain = cmd.Parameters.Add("$gain", SqliteType.Real);
        var pAnalyzed = cmd.Parameters.Add("$analyzed", SqliteType.Integer);
        var pAdded = cmd.Parameters.Add("$added", SqliteType.Text);
        var pModified = cmd.Parameters.Add("$modified", SqliteType.Text);

        foreach (var track in tracks)
        {
            pId.Value = track.Id;
            pPath.Value = track.FilePath;
            pTitle.Value = track.Title ?? (object)DBNull.Value;
            pArtist.Value = track.Artist ?? (object)DBNull.Value;
            pAlbum.Value = track.Album ?? (object)DBNull.Value;
            pAlbumArtist.Value = track.AlbumArtist ?? (object)DBNull.Value;
            pDuration.Value = track.Duration.Ticks;
            pTrackNum.Value = track.TrackNumber > 0 ? track.TrackNumber : (object)DBNull.Value;
            pYear.Value = track.Year > 0 ? track.Year : (object)DBNull.Value;
            pGenre.Value = track.Genre ?? (object)DBNull.Value;
            pCover.Value = track.CoverArtPath ?? (object)DBNull.Value;
            pLyrics.Value = track.Lyrics ?? (object)DBNull.Value;
            pSynced.Value = track.LyricsSynced ? 1 : 0;
            pGain.Value = track.NormalizationGainDb;
            pAnalyzed.Value = track.IsAnalyzed ? 1 : 0;
            pAdded.Value = track.DateAdded.ToString("o");
            pModified.Value = track.LastModified.ToString("o");
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task UpdateTrackAsync(Track track)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Tracks SET
                Title=$title, Artist=$artist, Album=$album,
                AlbumArtist=$albumArtist, TrackNumber=$trackNum, Year=$year, Genre=$genre,
                CoverArtPath=$cover, NormalizationGainDb=$gain, IsAnalyzed=$analyzed, LastModified=$modified
            WHERE Id=$id
            """;
        cmd.Parameters.AddWithValue("$id",          track.Id);
        cmd.Parameters.AddWithValue("$title",       track.Title);
        cmd.Parameters.AddWithValue("$artist",      track.Artist);
        cmd.Parameters.AddWithValue("$album",       track.Album);
        cmd.Parameters.AddWithValue("$albumArtist", track.AlbumArtist);
        cmd.Parameters.AddWithValue("$trackNum",    track.TrackNumber);
        cmd.Parameters.AddWithValue("$year",        track.Year);
        cmd.Parameters.AddWithValue("$genre",       track.Genre);
        cmd.Parameters.AddWithValue("$cover",       (object?)track.CoverArtPath   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$gain",        track.NormalizationGainDb);
        cmd.Parameters.AddWithValue("$analyzed",    track.IsAnalyzed ? 1 : 0);
        cmd.Parameters.AddWithValue("$modified",    track.LastModified.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<HashSet<string>> GetCachedFilePathsAsync()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT FilePath FROM Tracks";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) set.Add(reader.GetString(0));
        return set;
    }

    public async Task DeleteTrackAsync(string trackId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tracks WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", trackId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Models.Playlist>> LoadAllPlaylistsAsync()
    {
        var list = new List<Models.Playlist>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, TrackIdsJson, CoverImagePath, DateCreated, DateModified FROM Playlists ORDER BY DateCreated DESC";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
            var ids  = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            list.Add(new Models.Playlist
            {
                Id           = reader.GetString(0),
                Name         = reader.GetString(1),
                TrackIds     = ids,
                CoverImagePath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                DateCreated  = DateTime.Parse(reader.GetString(4)),
                DateModified = DateTime.Parse(reader.GetString(5))
            });
        }
        return list;
    }

    public async Task UpsertPlaylistAsync(Models.Playlist playlist)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Playlists (Id, Name, TrackIdsJson, CoverImagePath, DateCreated, DateModified)
            VALUES ($id, $name, $json, $cover, $created, $modified)
            ON CONFLICT(Id) DO UPDATE SET
                Name=excluded.Name, TrackIdsJson=excluded.TrackIdsJson,
                CoverImagePath=excluded.CoverImagePath,
                DateModified=excluded.DateModified;
            """;
        cmd.Parameters.AddWithValue("$id",       playlist.Id);
        cmd.Parameters.AddWithValue("$name",     playlist.Name);
        cmd.Parameters.AddWithValue("$json",     JsonSerializer.Serialize(playlist.TrackIds));
        cmd.Parameters.AddWithValue("$cover",    string.IsNullOrEmpty(playlist.CoverImagePath) ? (object)DBNull.Value : playlist.CoverImagePath);
        cmd.Parameters.AddWithValue("$created",  playlist.DateCreated.ToString("o"));
        cmd.Parameters.AddWithValue("$modified", playlist.DateModified.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeletePlaylistAsync(string playlistId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Playlists WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", playlistId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string SafeGetString(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string? SafeGetStringOrNull(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int SafeGetInt32(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    private static long SafeGetInt64(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0L : reader.GetInt64(ordinal);
    }

    private static double SafeGetDouble(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0.0 : reader.GetDouble(ordinal);
    }

    private static DateTime SafeGetDateTime(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return DateTime.MinValue;
        return DateTime.TryParse(reader.GetString(ordinal), out var dt) ? dt : DateTime.MinValue;
    }
}

public class SafeWaveFileReader : NAudio.Wave.WaveFileReader
{
    private readonly string? _tempPath;
    public SafeWaveFileReader(string path, string? tempPath) : base(path)
    {
        _tempPath = tempPath;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_tempPath != null)
        {
            try { System.IO.File.Delete(_tempPath); } catch { }
        }
    }
}

public class SafeMediaFoundationReader : NAudio.Wave.MediaFoundationReader
{
    public string? TempPath { get; set; }

    public SafeMediaFoundationReader(string path, string? tempPath) : base(path)
    {
        TempPath = tempPath;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (TempPath != null)
        {
            try { System.IO.File.Delete(TempPath); } catch { }
        }
    }
}

public class Id3SkippingStream : Stream
{
    private readonly Stream _source;
    private readonly long _offset;
    private readonly bool _disposeSource;

    public Id3SkippingStream(Stream source, long offset, bool disposeSource = true)
    {
        _source = source;
        _offset = offset;
        _disposeSource = disposeSource;
        _source.Position = _offset;
    }

    public override bool CanRead => _source.CanRead;
    public override bool CanSeek => _source.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _source.Length - _offset;
    public override long Position
    {
        get => _source.Position - _offset;
        set => _source.Position = value + _offset;
    }
    public override void Flush() => _source.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Begin) return _source.Seek(offset + _offset, SeekOrigin.Begin) - _offset;
        if (origin == SeekOrigin.Current) return _source.Seek(offset, SeekOrigin.Current) - _offset;
        if (origin == SeekOrigin.End) return _source.Seek(offset, SeekOrigin.End) - _offset;
        throw new NotSupportedException();
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing && _disposeSource) _source.Dispose();
        base.Dispose(disposing);
    }
}

public class SafeStreamMediaFoundationReader : NAudio.Wave.StreamMediaFoundationReader
{
    private readonly Stream _sourceStream;
    public SafeStreamMediaFoundationReader(Stream stream) : base(stream)
    {
        _sourceStream = stream;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { _sourceStream.Dispose(); } catch { }
        }
    }
}

public static class SafeAudioReader
{
    public static NAudio.Wave.WaveStream Create(string filePath)
    {
        string resolvedExt = Path.GetExtension(LibraryScannerService.ResolveRealExtension(filePath)).ToLowerInvariant();
        if (resolvedExt == ".m4a")
        {
            var fsIn = System.IO.File.OpenRead(filePath);
            var head = new byte[10];
            long offset = 0;
            if (fsIn.Read(head, 0, 10) == 10 && head[0] == 0x49 && head[1] == 0x44 && head[2] == 0x33)
            {
                int id3Size = ((head[6] & 0x7f) << 21) | ((head[7] & 0x7f) << 14) | ((head[8] & 0x7f) << 7) | (head[9] & 0x7f);
                offset = 10 + id3Size;
            }

            fsIn.Seek(offset, SeekOrigin.Begin);
            bool isDash = false;
            var ftyp = new byte[12];
            if (fsIn.Read(ftyp, 0, 12) == 12 && ftyp[4] == 0x66 && ftyp[5] == 0x74 && ftyp[6] == 0x79 && ftyp[7] == 0x70)
            {
                if (ftyp[8] == 0x64 && ftyp[9] == 0x61 && ftyp[10] == 0x73 && ftyp[11] == 0x68)
                {
                    isDash = true;
                }
            }

            if (!isDash)
            {
                // Pour les M4A standards, on stream directement depuis le fichier original en ignorant l'ID3 !
                // Cela élimine complètement le délai de copie du fichier en cache.
                var skipStream = new Id3SkippingStream(fsIn, offset);
                try
                {
                    var wmf = new SafeStreamMediaFoundationReader(skipStream);
                    if (wmf.TotalTime.TotalSeconds > 0)
                    {
                        return wmf;
                    }
                    wmf.Dispose();
                }
                catch { skipStream.Dispose(); }
            }
            else
            {
                fsIn.Dispose(); // On le ferme, FFmpeg va s'en charger
            }

            // Si le streaming a échoué (ou si c'est un DASH), on fait un fallback : 
            // On strip l'ID3 vers un fichier temporaire pour ffmpeg
            string strippedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".m4a");
            using (var tempFsIn = System.IO.File.OpenRead(filePath))
            {
                tempFsIn.Seek(offset, SeekOrigin.Begin);
                using (var fsOut = System.IO.File.Create(strippedPath))
                {
                    tempFsIn.CopyTo(fsOut);
                }
            }

            // Si c'est un fichier "dash" (ex: Frostpunk), WMF a du mal même sans ID3. 
            // On le remux de force via FFmpeg pour reconstruire un conteneur propre.
            if (isDash)
            {
                string tempM4a = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".m4a");
                bool ffmpegSuccess = false;
                try
                {
                    var process = new System.Diagnostics.Process();
                    string localFfmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                    process.StartInfo.FileName = System.IO.File.Exists(localFfmpeg) ? localFfmpeg : "ffmpeg";
                    // Remux instantané au lieu d'un décodage lent en WAV
                    process.StartInfo.Arguments = $"-y -i \"{strippedPath}\" -c:a copy -f mp4 \"{tempM4a}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode == 0 && System.IO.File.Exists(tempM4a))
                    {
                        ffmpegSuccess = true;
                    }
                }
                catch { }

                try { System.IO.File.Delete(strippedPath); } catch { }

                if (ffmpegSuccess)
                {
                    var reader = new SafeMediaFoundationReader(tempM4a, tempM4a);
                    if (reader.TotalTime.TotalSeconds > 0) return reader;
                }
            }

            // Fallback ultime FFmpeg si WMF échoue complètement (décodage complet)
            string tempWavFallback = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".wav");
            bool fallbackSuccess = false;
            try
            {
                var process = new System.Diagnostics.Process();
                string localFfmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                process.StartInfo.FileName = System.IO.File.Exists(localFfmpeg) ? localFfmpeg : "ffmpeg";
                process.StartInfo.Arguments = $"-y -i \"{strippedPath}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{tempWavFallback}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
                if (process.ExitCode == 0 && System.IO.File.Exists(tempWavFallback))
                {
                    fallbackSuccess = true;
                }
            }
            catch { }

            try { System.IO.File.Delete(strippedPath); } catch { }

            if (fallbackSuccess)
                return new SafeWaveFileReader(tempWavFallback, tempWavFallback);

            throw new InvalidOperationException("Impossible de décoder ce fichier via WMF ou FFmpeg.");
        }
        return new SafeMediaFoundationReader(filePath, null);
    }
}

public class AudioEngineService : IDisposable
{
    private WasapiOut?                _output;
    private NAudio.Wave.WaveStream?   _fileReader;
    private OpusSampleProvider?       _opusReader;
    private VolumeSampleProvider?     _volumeProvider;
    private RmsCaptureSampleProvider? _rmsCapture;
    private EqualizerSampleProvider?  _equalizer;
    private ISampleProvider?          _finalProvider;

    public event EventHandler? PlaybackStopped;

    public float CurrentRmsLevel => _rmsCapture?.CurrentRms ?? 0f;

    public bool          IsExclusiveMode  { get; private set; }
    public PlaybackState State            => _output?.PlaybackState ?? PlaybackState.Stopped;
    public TimeSpan      CurrentPosition  => _fileReader?.CurrentTime ?? _opusReader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan      TotalDuration    => _fileReader?.TotalTime   ?? _opusReader?.TotalTime   ?? TimeSpan.Zero;

    private float  _userVolume         = 1.0f;
    private double _normalizationGainDb = 0;

    private readonly SemaphoreSlim _playLock = new(1, 1);

    private record PrewarmedReader(NAudio.Wave.WaveStream? FileReader, OpusSampleProvider? OpusReader, ISampleProvider? SampleProvider);
    private Task<PrewarmedReader?>? _prewarmTask;
    private string?                   _prewarmPath;
    private DateTime                  _lastPrewarmUtc = DateTime.MinValue;
    private const double PrewarmDebounceMs = 300;

    public async Task PlayAsync(Track track, bool preferExclusive = false, double initialGainDb = 0, Action<string>? onDownloadProgress = null)
    {
        await _playLock.WaitAsync();
        try
        {
            Stop();

            _normalizationGainDb = initialGainDb; 
            ISampleProvider? sampleProvider = null;
            string ext = Path.GetExtension(track.FilePath).ToLowerInvariant();
            string resolvedExt = Path.GetExtension(LibraryScannerService.ResolveRealExtension(track.FilePath)).ToLowerInvariant();

            Task<PrewarmedReader?>? prewarmBuildTask = GetPrewarmedReaderAsync(track.FilePath, track.Duration);
            Task wasapiTask = InitOutputAsync(preferExclusive);

            try
            {
                var prewarmed = await prewarmBuildTask;
                if (prewarmed != null)
                {
                    _fileReader = prewarmed.FileReader;
                    _opusReader = prewarmed.OpusReader;
                    sampleProvider = prewarmed.SampleProvider;
                }
            }
            catch { }

            if (sampleProvider == null)
            {
                if (resolvedExt is ".opus" or ".ogg")
                {
                    try
                    {
                        _opusReader = new OpusSampleProvider(track.FilePath, track.Duration);
                        sampleProvider = _opusReader;
                    }
                    catch { }
                }

                if (sampleProvider == null)
                {
                    try
                    {
                        _fileReader    = new AudioFileReader(track.FilePath);
                        sampleProvider = _fileReader as ISampleProvider;
                    }
                    catch
                    {
                        if (resolvedExt == ".mp3")
                        {
                            try
                            {
                                var mp3 = new NAudio.Wave.Mp3FileReader(track.FilePath);
                                _fileReader = mp3;
                                sampleProvider = mp3.ToSampleProvider();
                            }
                            catch { }
                        }
                    }
                }

                // Fallback ultime : Media Foundation sur le Stream direct 
                // (Permet de lire les M4A même s'ils ont l'extension .mp3)
                if (sampleProvider == null)
                {
                    try
                    {
                        if (resolvedExt == ".m4a") await DownloadService.EnsureBinariesAsync(onDownloadProgress);
                        var mfReader = SafeAudioReader.Create(track.FilePath);
                        _fileReader = mfReader;
                        sampleProvider = mfReader.ToSampleProvider();
                    }
                    catch (Exception ex)
                    {
                        throw new NotSupportedException($"Format audio non pris en charge ou illisible.\nFichier : {track.FilePath}", ex);
                    }
                }
            }

            if (sampleProvider == null)
                throw new InvalidOperationException($"Impossible de créer un lecteur.\nFichier : {track.FilePath}");

            _rmsCapture     = new RmsCaptureSampleProvider(sampleProvider);
            _volumeProvider = new VolumeSampleProvider(_rmsCapture) { Volume = ComputeLinearGain() };
            _equalizer      = new EqualizerSampleProvider(_volumeProvider);
            _equalizer.SetEnabled(App.Settings.Current.EqualizerEnabled);
            if (App.Settings.Current.EqualizerEnabled)
            {
                var bands = App.Settings.Current.EqualizerBands;
                for (int i = 0; i < bands.Length && i < 10; i++)
                    _equalizer.SetBand(i, (float)bands[i]);
            }
            _finalProvider  = _equalizer;

            await wasapiTask;

            var output = _output;
            if (output == null)
            {
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    _output = new WasapiOut(AudioClientShareMode.Shared, true, 100);
                    output = _output;
                }
                catch (Exception ex)
                {
                    Stop();
                    throw new InvalidOperationException("Le périphérique audio n'a pas pu être initialisé.", ex);
                }
            }

            if (output == null || _finalProvider == null)
            {
                Stop();
                throw new InvalidOperationException("Initialisation échouée.");
            }

            try
            {
                output.Init(_finalProvider);
                output.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
                output.Play();
            }
            catch (Exception ex)
            {
                Stop();
                throw new InvalidOperationException("Le lecteur audio n'a pas pu démarrer.", ex);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlayAsync error: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            _playLock.Release();
        }
    }

    public void PrewarmOpus(string path, TimeSpan duration = default)
    {
        if (string.IsNullOrEmpty(path)) return;

        if ((DateTime.UtcNow - _lastPrewarmUtc).TotalMilliseconds < PrewarmDebounceMs) return;
        if (path == _prewarmPath) return;

        DiscardPrewarmLocked();

        _lastPrewarmUtc = DateTime.UtcNow;
        _prewarmPath    = path;
        var capturedPath = path;
        var capturedDur  = duration;
        _prewarmTask = Task.Run(() =>
        {
            try 
            { 
                string resolvedExt = Path.GetExtension(LibraryScannerService.ResolveRealExtension(capturedPath)).ToLowerInvariant();
                if (resolvedExt is ".opus" or ".ogg")
                {
                    var opus = new OpusSampleProvider(capturedPath, capturedDur);
                    return new PrewarmedReader(null, opus, opus);
                }
                
                NAudio.Wave.WaveStream? wave = null;
                ISampleProvider? provider = null;

                try
                {
                    wave = new AudioFileReader(capturedPath);
                    provider = wave as ISampleProvider;
                }
                catch
                {
                    if (resolvedExt == ".mp3")
                    {
                        try
                        {
                            var mp3 = new NAudio.Wave.Mp3FileReader(capturedPath);
                            wave = mp3;
                            provider = mp3.ToSampleProvider();
                        }
                        catch { }
                    }
                }

                if (provider == null)
                {
                    try
                    {
                        var mf = SafeAudioReader.Create(capturedPath);
                        wave = mf;
                        provider = mf.ToSampleProvider();
                    }
                    catch { }
                }

                if (provider != null)
                {
                    return new PrewarmedReader(wave, null, provider);
                }
                return (PrewarmedReader?)null;
            }
            catch { return (PrewarmedReader?)null; }
        });
    }

    private async Task<PrewarmedReader?> GetPrewarmedReaderAsync(string path, TimeSpan duration)
    {
        if (_prewarmPath == path && _prewarmTask != null)
        {
            try
            {
                var reader = await _prewarmTask;
                _prewarmTask = null;
                _prewarmPath = null;
                return reader;
            }
            catch { _prewarmTask = null; _prewarmPath = null; }
        }
        else
        {
            DiscardPrewarmLocked();
        }

        return await Task.Run(() =>
        {
            try 
            { 
                string resolvedExt = Path.GetExtension(LibraryScannerService.ResolveRealExtension(path)).ToLowerInvariant();
                if (resolvedExt is ".opus" or ".ogg")
                {
                    var opus = new OpusSampleProvider(path, duration);
                    return new PrewarmedReader(null, opus, opus);
                }
                return (PrewarmedReader?)null;
            }
            catch { return (PrewarmedReader?)null; }
        });
    }

    private void DiscardPrewarmLocked()
    {
        var task = _prewarmTask;
        _prewarmTask = null;
        _prewarmPath = null;
        if (task == null) return;
        _ = task.ContinueWith(t =>
        {
            try { if (t.Result is IDisposable d) d.Dispose(); } catch { }
        }, TaskScheduler.Default);
    }

    private async Task InitOutputAsync(bool preferExclusive)
    {
        await Task.Run(() =>
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            if (preferExclusive)
            {
                try
                {
                    _output         = new WasapiOut(device, AudioClientShareMode.Exclusive, true, 100);
                    IsExclusiveMode = true;
                    return;
                }
                catch { }
            }

            _output?.Dispose();
            _output         = new WasapiOut(AudioClientShareMode.Shared, true, 100);
            IsExclusiveMode = false;
        });
    }

    private float ComputeLinearGain()
    {
        double linear = Math.Pow(10, _normalizationGainDb / 20.0) * _userVolume * 0.5;
        return (float)Math.Clamp(linear, 0.0, 4.0);
    }

    public void SetUserVolume(float volume01)
    {
        _userVolume = Math.Clamp(volume01, 0f, 1f);
        if (_volumeProvider != null) _volumeProvider.Volume = ComputeLinearGain();
    }

    public void SetNormalizationGain(double gainDb)
    {
        _normalizationGainDb = gainDb;
        if (_volumeProvider == null) return;

        float targetVolume = ComputeLinearGain();
        float startVolume  = _volumeProvider.Volume;
        if (Math.Abs(targetVolume - startVolume) < 0.01f) { _volumeProvider.Volume = targetVolume; return; }

        _ = Task.Run(async () =>
        {
            const int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                _volumeProvider.Volume = startVolume + (targetVolume - startVolume) * t;
                await Task.Delay(10);
            }
            _volumeProvider.Volume = targetVolume;
        });
    }

    public void SetEqualizerEnabled(bool enabled)
    {
        _equalizer?.SetEnabled(enabled);
    }

    public void SetEqualizerBand(int index, float gainDb)
    {
        _equalizer?.SetBand(index, gainDb);
    }

    public void Pause()  => _output?.Pause();
    public void Resume() => _output?.Play();

    public void Seek(TimeSpan position)
    {
        if (_fileReader != null) _fileReader.CurrentTime = position;
        if (_opusReader != null) _opusReader.CurrentTime = position;
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _fileReader?.Dispose();
        _fileReader = null;
        _opusReader?.Dispose();
        _opusReader = null;
        _equalizer = null;
        _volumeProvider = null;
        _rmsCapture = null;
        _finalProvider = null;

        DiscardPrewarmLocked();
    }

    public void Dispose() => Stop();
}

public class OpusSampleProvider : ISampleProvider, IDisposable
{
    private const int OpusSampleRate = 48000;
    private const int OpusChannels   = 2;

    private readonly string       _filePath;
    private FileStream?           _fs;
    private OpusOggReadStream?    _stream;

    private readonly float[] _ring = new float[16384];
    private int _ringHead;
    private int _ringCount;

    private TimeSpan _currentTime;
    private readonly object _lock = new();

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(OpusSampleRate, OpusChannels);
    public TimeSpan   TotalTime  { get; }

    public TimeSpan CurrentTime
    {
        get => _currentTime;
        set
        {
            lock (_lock)
            {
                if (_stream == null) OpenStreamLocked();
                if (_stream == null) return;

                if (value <= TimeSpan.Zero && _stream.CanSeek)
                {
                    OpenStreamLocked();
                    return;
                }

                if (_stream.CanSeek)
                {
                    try
                    {
                        _stream.SeekTo(value);
                        ResetRingLocked();
                        try { _currentTime = _stream.CurrentTime; } catch { _currentTime = value; }
                        return;
                    }
                    catch { }
                }

                long targetSamples = (long)(value.TotalSeconds * OpusSampleRate * OpusChannels);
                long skipped = 0;
                while (skipped < targetSamples && _stream.HasNextPacket)
                {
                    var pcm = _stream.DecodeNextPacket();
                    if (pcm == null) break;
                    skipped += pcm.Length;
                }
                ResetRingLocked();
                _currentTime = value;
            }
        }
    }

    public OpusSampleProvider(string filePath) : this(filePath, TimeSpan.Zero) { }

    public OpusSampleProvider(string filePath, TimeSpan knownDuration)
    {
        _filePath = filePath;
        TotalTime = knownDuration > TimeSpan.Zero ? knownDuration : EstimateDurationFromTags(filePath);
        lock (_lock) { OpenStreamLocked(); }
    }

    private static TimeSpan EstimateDurationFromTags(string path)
    {
        try   { using var f = TagLib.File.Create(path); return f.Properties.Duration; }
        catch { return TimeSpan.Zero; }
    }

    private void OpenStreamLocked()
    {
        _stream = null;
        _fs?.Dispose();
        _fs     = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
        var dec = OpusCodecFactory.CreateDecoder(OpusSampleRate, OpusChannels);
        _stream = new OpusOggReadStream(dec, _fs);
        ResetRingLocked();
        _currentTime = TimeSpan.Zero;

        PrimeFirstPacketLocked();
    }

    private void PrimeFirstPacketLocked()
    {
        if (_stream == null || !_stream.HasNextPacket) return;
        try
        {
            short[]? pcm = _stream.DecodeNextPacket();
            if (pcm == null) return;
            int toCopy = Math.Min(pcm.Length, _ring.Length);
            for (int i = 0; i < toCopy; i++)
            {
                _ring[(_ringHead + _ringCount) % _ring.Length] = pcm[i] / 32768f;
                _ringCount++;
            }
        }
        catch { }
    }

    private void ResetRingLocked()
    {
        _ringHead = 0;
        _ringCount = 0;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_stream == null) return 0;

            int written = 0;
            while (written < count)
            {
                while (_ringCount > 0 && written < count)
                {
                    buffer[offset + written++] = _ring[_ringHead];
                    _ringHead = (_ringHead + 1) % _ring.Length;
                    _ringCount--;
                }
                if (written == count) break;
                if (!_stream.HasNextPacket) break;

                short[]? pcm = _stream.DecodeNextPacket();
                if (pcm == null) break;
                int maxFit = _ring.Length - _ringCount;
                int toCopy = Math.Min(pcm.Length, maxFit);
                for (int i = 0; i < toCopy; i++)
                {
                    _ring[(_ringHead + _ringCount) % _ring.Length] = pcm[i] / 32768f;
                    _ringCount++;
                }
            }

            try { _currentTime = _stream.CurrentTime; } catch { }
            return written;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _stream = null;
            _fs?.Dispose();
            _fs = null;
        }
    }
}

public class RmsCaptureSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    public WaveFormat WaveFormat  => _source.WaveFormat;
    public float      CurrentRms  { get; private set; }

    public RmsCaptureSampleProvider(ISampleProvider source) => _source = source;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read > 0)
        {
            double sum = 0;
            for (int i = offset; i < offset + read; i++) sum += buffer[i] * buffer[i];
            CurrentRms = (float)Math.Sqrt(sum / read);
        }
        return read;
    }
}

public class NormalizationService
{
    public async Task<double> AnalyzeAsync(string filePath)
        => await Task.Run(() => { try { return AnalyzeInternal(filePath); } catch { return 0.0; } });

    private double AnalyzeInternal(string filePath)
    {
        double targetRms = App.Settings.Current.NormalizationTargetRms;
        double maxGain = App.Settings.Current.NormalizationMaxGain;
        double minGain = -20.0;
        double targetDb = -0.1;

        try
        {
            using var file = TagLib.File.Create(filePath);
            if (file.Tag != null)
            {
                // Format standard reconnu par TagLib#
                if (!double.IsNaN(file.Tag.ReplayGainTrackGain))
                {
                    return Math.Clamp(file.Tag.ReplayGainTrackGain, minGain, maxGain);
                }

                // Recherche manuelle dans les tags ID3v2 (cas fréquent si ajouté par d'autres logiciels)
                var id3v2 = file.GetTag(TagLib.TagTypes.Id3v2) as TagLib.Id3v2.Tag;
                if (id3v2 != null)
                {
                    foreach (var frame in id3v2.GetFrames("TXXX").OfType<TagLib.Id3v2.UserTextInformationFrame>())
                    {
                        if (frame.Description.Equals("replaygain_track_gain", StringComparison.OrdinalIgnoreCase))
                        {
                            var text = frame.Text.Length > 0 ? frame.Text[0] : null;
                            if (text != null)
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(text, @"([-+]?[0-9]*\.?[0-9]+)");
                                if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double gain))
                                    return Math.Clamp(gain, minGain, maxGain);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        if (DownloadService.IsFfmpegPresent)
        {
            try
            {
                string exe = File.Exists(DownloadService.FfmpegPath) ? DownloadService.FfmpegPath : "ffmpeg";
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"-i \"{filePath}\" -af volumedetect -f null -",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    try { proc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                    string output = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    var matchMax = System.Text.RegularExpressions.Regex.Match(output, @"max_volume:\s*([-+]?[0-9]*\.?[0-9]+)\s*dB");
                    var matchMean = System.Text.RegularExpressions.Regex.Match(output, @"mean_volume:\s*([-+]?[0-9]*\.?[0-9]+)\s*dB");
                    
                    if (matchMax.Success && double.TryParse(matchMax.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double maxVol))
                    {
                        double meanVol = maxVol; // Fallback to maxVol if mean is not found
                        if (matchMean.Success && double.TryParse(matchMean.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double m))
                        {
                            meanVol = m;
                        }

                        double gainRms = targetRms - meanVol;
                        double maxAllowedGain = targetDb - maxVol;
                        double gain = Math.Min(gainRms, maxAllowedGain);

                        return Math.Clamp(gain, minGain, maxGain);
                    }
                }
            }
            catch { }
        }

        ISampleProvider? sampleProvider = null;
        IDisposable?     reader         = null;
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext is ".opus" or ".ogg")
        {
            try
            {
                var op = new OpusSampleProvider(filePath);
                reader = op; sampleProvider = op;
            }
            catch { }
        }

        if (sampleProvider == null)
        {
            try
            {
                var af = new AudioFileReader(filePath);
                reader = af; sampleProvider = af.ToSampleProvider();
            }
            catch { }
        }

        if (sampleProvider == null)
        {
            try
            {
                var mf = SafeAudioReader.Create(filePath);
                reader = mf; sampleProvider = mf.ToSampleProvider();
            }
                
            catch { return 0.0; }
        }

        try
        {
            var buffer = new float[8192];
            double globalPeak = 0;
            double sumSquares = 0;
            long totalSamples = 0;
            int read;

            while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    float v = Math.Abs(buffer[i]);
                    if (v > globalPeak) globalPeak = v;
                    sumSquares += v * v;
                    totalSamples++;
                }
            }

            if (globalPeak <= 0 || totalSamples == 0) return 0.0;

            double meanSquare = sumSquares / totalSamples;
            double meanVol = 10 * Math.Log10(meanSquare); // RMS in dB
            double peakDb = 20 * Math.Log10(globalPeak); // Peak in dB
            
            double gainRms = targetRms - meanVol;
            double maxAllowedGain = targetDb - peakDb;
            
            double gain = Math.Min(gainRms, maxAllowedGain);

            return Math.Clamp(gain, minGain, maxGain);
        }
        finally { reader?.Dispose(); }
    }
}

public class PlayStatsService
{
    private readonly string _filePath;
    private Dictionary<string, int> _playCounts = new();
    private TimeSpan _totalListenTime = TimeSpan.Zero;

    public long     TotalPlays       => _playCounts.Values.Sum(v => (long)v);
    public TimeSpan TotalListenTime  => _totalListenTime;

    public int GetPlayCount(string trackId)
        => _playCounts.TryGetValue(trackId, out int c) ? c : 0;

    public Track? MostPlayedTrack(List<Track> library)
    {
        if (_playCounts.Count == 0) return null;
        var top = _playCounts.OrderByDescending(kv => kv.Value).First();
        return library.FirstOrDefault(t => t.Id == top.Key);
    }

    public PlayStatsService()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Resona");
        Directory.CreateDirectory(dir);
        _filePath = System.IO.Path.Combine(dir, "playstats.json");
        Load();
    }

    public void RecordPlay(string trackId)
    {
        _playCounts[trackId] = GetPlayCount(trackId) + 1;
        _ = SaveAsync();
    }

    public void AddListenTime(TimeSpan delta)
    {
        if (delta.TotalSeconds > 0 && delta.TotalSeconds < 60)
        {
            _totalListenTime += delta;
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var data = new { counts = _playCounts, listenSeconds = (long)_totalListenTime.TotalSeconds };
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var doc  = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("counts", out var counts))
                _playCounts = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, int>>(counts.GetRawText()) ?? new();
            if (doc.RootElement.TryGetProperty("listenSeconds", out var ls))
                _totalListenTime = TimeSpan.FromSeconds(ls.GetInt64());
        }
        catch { _playCounts = new(); _totalListenTime = TimeSpan.Zero; }
    }
}

public class DownloadOptions
{
    public string          OutputDirectory { get; set; } = string.Empty;
    public DownloadFormat  Format          { get; set; } = DownloadFormat.Opus;
    public string          Codec           { get; set; } = string.Empty;
    public DownloadBitrate Bitrate         { get; set; } = DownloadBitrate.Best;
}

public class DownloadService
{
    private static readonly string BinDir = AppContext.BaseDirectory;

    public static string YtDlpPath  => Path.Combine(BinDir, "yt-dlp.exe");
    public static string FfmpegPath => Path.Combine(BinDir, "ffmpeg.exe");

    public static async Task EnsureBinariesAsync(Action<string>? onProgress = null)
    {
        Directory.CreateDirectory(BinDir);

        if (!IsYtDlpPresent)
            await EnsureBinaryAsync(
                YtDlpPath,
                "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
                "yt-dlp.exe",
                onProgress);

        if (!File.Exists(FfmpegPath))
            await EnsureFfmpegAsync(onProgress);
    }

    public static async Task EnsureFfmpegAsync(Action<string>? onProgress)
    {
        onProgress?.Invoke("Téléchargement de ffmpeg...");
        const string zipUrl  = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        string tmpDir  = Path.Combine(Path.GetTempPath(), $"Resona_ffmpeg_{Guid.NewGuid():N}");
        string zipPath = Path.Combine(tmpDir, "ffmpeg.zip");
        try
        {
            Directory.CreateDirectory(tmpDir);

            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Resona");
            http.Timeout = TimeSpan.FromMinutes(10);
            onProgress?.Invoke("Téléchargement de ffmpeg (build essentials ~75 Mo)...");

            using (var httpStream = await http.GetStreamAsync(zipUrl))
            using (var fileStream  = File.Create(zipPath))
                await httpStream.CopyToAsync(fileStream);

            onProgress?.Invoke("Extraction de ffmpeg.exeâ€¦");
            string? extractedPath = null;
            using (var zip = System.IO.Compression.ZipFile.OpenRead(zipPath))
            {
                var entry = zip.Entries.FirstOrDefault(e =>
                    e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
                    e.FullName.Contains("/bin/", StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    extractedPath = Path.Combine(tmpDir, "ffmpeg.exe");
                    using var entryStream = entry.Open();
                    using var destStream  = File.Create(extractedPath);
                    await entryStream.CopyToAsync(destStream);
                }
            }

            if (extractedPath != null && File.Exists(extractedPath))
            {
                File.Copy(extractedPath, FfmpegPath, overwrite: true);
                onProgress?.Invoke("ffmpeg installé.");
            }
            else
            {
                onProgress?.Invoke("âš ï¸ ffmpeg.exe introuvable dans l'archive.");
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"Erreur ffmpeg : {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    private static async Task EnsureBinaryAsync(string localPath, string url, string name, Action<string>? onProgress)
    {
        if (File.Exists(localPath)) return;
        onProgress?.Invoke($"Téléchargement de {name}â€¦");
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Resona");
            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, bytes);
            onProgress?.Invoke($"{name} installé.");
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"Erreur lors du téléchargement de {name} : {ex.Message}");
        }
    }

    private static string FindYtDlp()
    {
        if (File.Exists(YtDlpPath)) return YtDlpPath;
        return "yt-dlp";
    }

    public async Task<string> GetVersionAsync()
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = FindYtDlp(),
                    Arguments              = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };
            proc.Start();
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Trim();
        }
        catch { return string.Empty; }
    }

    public static bool IsYtDlpPresent  => File.Exists(YtDlpPath)  || IsBinaryInPath("yt-dlp");
    public static bool IsFfmpegPresent => File.Exists(FfmpegPath) || IsBinaryInPath("ffmpeg");

    private static bool IsBinaryInPath(string name)
    {
        try
        {
            using var p = new Process { StartInfo = new ProcessStartInfo
                { FileName = name, Arguments = "--version", UseShellExecute = false,
                  CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            p.Start(); p.WaitForExit(); return p.ExitCode == 0;
        }
        catch { return false; }
    }

    public async Task DownloadAsync(string url, DownloadOptions opts, Action<string>? onProgress = null)
    {
        await EnsureBinariesAsync(onProgress);

        string ytDlp = FindYtDlp();
        if (ytDlp == "yt-dlp" && !await IsBinaryAvailableAsync(ytDlp))
            throw new FileNotFoundException("yt-dlp introuvable même après tentative d'installation automatique.");

        string args = BuildArguments(url, opts);

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = ytDlp,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                WorkingDirectory       = opts.OutputDirectory,
            }
        };

        proc.OutputDataReceived += (s, e) => { if (e.Data != null) onProgress?.Invoke(e.Data); };
        proc.ErrorDataReceived  += (s, e) => { if (e.Data != null) onProgress?.Invoke(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new Exception($"yt-dlp a terminé avec le code {proc.ExitCode}.");
    }

    private static string BuildArguments(string url, DownloadOptions opts)
    {
        string formatExt = opts.Format switch
        {
            DownloadFormat.Mp3    => "mp3",
            DownloadFormat.Flac   => "flac",
            DownloadFormat.Opus   => "opus",
            DownloadFormat.M4a    => "m4a",
            DownloadFormat.Wav    => "wav",
            DownloadFormat.Vorbis => "vorbis",
            _                     => "opus"
        };

        string codec = string.IsNullOrWhiteSpace(opts.Codec)
            ? opts.Format switch
            {
                DownloadFormat.Mp3    => "mp3",
                DownloadFormat.Flac   => "flac",
                DownloadFormat.Opus   => "libopus",
                DownloadFormat.M4a    => "aac",
                DownloadFormat.Wav    => "pcm_s16le",
                DownloadFormat.Vorbis => "libvorbis",
                _                     => "libopus"
            }
            : opts.Codec;

        bool isLossless = opts.Format is DownloadFormat.Flac or DownloadFormat.Wav;
        string bitrateArg = (opts.Bitrate == DownloadBitrate.Best || isLossless)
            ? string.Empty
            : $"--audio-quality {BitrateToKbps(opts.Bitrate)}K";

        string output = Path.Combine(opts.OutputDirectory, "%(title)s.%(ext)s").Replace("\\", "/");

        return $"-x --audio-format {formatExt} {bitrateArg} " +
               $"--embed-thumbnail --embed-metadata " +
               $"--parse-metadata \"%(upload_date>%Y)s:%(meta_date)s\" " +
               $"--output \"{output}\" " +
               $"--no-playlist-reverse " +
               $"--progress " +
               $"\"{url}\"";
    }

    private static int BitrateToKbps(DownloadBitrate b) => b switch
    {
        DownloadBitrate.Kbps320 => 320,
        DownloadBitrate.Kbps256 => 256,
        DownloadBitrate.Kbps192 => 192,
        DownloadBitrate.Kbps128 => 128,
        _                       => 0
    };

    private static async Task<bool> IsBinaryAvailableAsync(string name)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = name, Arguments = "--version",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                }
            };
            proc.Start();
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}

public class AutoTagResult
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public int? TrackNumber { get; set; }
    public int? TrackCount { get; set; }
    public string? AlbumArtist { get; set; }
    public string? CoverPath { get; set; }
    public double Score { get; set; }
}

public static class AutoTagService
{
    private static readonly HttpClient _http = new();
    private const string UserAgent = "Resona/1.0 (https://github.com/Resona)";

    static AutoTagService()
    {
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd(UserAgent);
    }

    private const double MinScoreThreshold = 0.72;
    private const double HighConfidenceThreshold = 0.85;

    private static readonly Dictionary<int, string> DeezerGenres = new()
    {
        [0] = "Not Given", [1] = "Pop", [2] = "Rap/Hip Hop", [3] = "Rock",
        [4] = "Electronic", [5] = "R&B", [6] = "Reggae", [7] = "Classical",
        [8] = "Jazz", [9] = "Blues", [10] = "Folk", [11] = "Country",
        [12] = "Metal", [13] = "Latin", [14] = "Dance", [15] = "Indie",
        [16] = "Alternative", [17] = "Soul", [18] = "Funk", [19] = "Punk",
        [20] = "Gospel", [21] = "Salsa", [22] = "Techno", [23] = "House",
        [24] = "Trance", [25] = "Dubstep", [26] = "Ambient", [27] = "Lo-fi",
        [84] = "K-Pop", [85] = "J-Pop", [113] = "Afrobeat",
    };

    private static double CalculateScore(string searchArtist, string searchTitle, string foundArtist, string foundTitle)
    {
        string combinedSearch = $"{searchArtist} {searchTitle}".Trim().ToLower();
        string titleSearch = (searchTitle ?? "").ToLower();
        string artistSearch = (searchArtist ?? "").ToLower();
        string fTitle = (foundTitle ?? "").ToLower();
        string fArtist = (foundArtist ?? "").ToLower();

        double combinedScore = Math.Max(
            JaroWinkler($"{fArtist} {fTitle}".Trim(), combinedSearch),
            JaroWinkler($"{fTitle} {fArtist}".Trim(), combinedSearch)
        );

        double individualScore = 0;
        if (!string.IsNullOrWhiteSpace(artistSearch))
        {
            individualScore = Math.Max(
                JaroWinkler(fTitle, titleSearch) * 0.6 + JaroWinkler(fArtist, artistSearch) * 0.4,
                JaroWinkler(fTitle, artistSearch) * 0.6 + JaroWinkler(fArtist, titleSearch) * 0.4
            );
        }
        else
        {
            individualScore = JaroWinkler(fTitle, titleSearch);
        }

        return Math.Max(combinedScore, individualScore);
    }

    private static async Task<AutoTagResult?> AppleMusicWebLookupAsync(string searchArtist, string title, TimeSpan? duration = null)
    {
        try
        {
            string query = $"{searchArtist} {title}".Trim();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://music.apple.com/fr/search?term={Uri.EscapeDataString(query)}");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            string html = await response.Content.ReadAsStringAsync();

            var matchId = System.Text.RegularExpressions.Regex.Match(html, @"music\.apple\.com/[a-z]{2}/album/[^/]+/[0-9]+\?i=([0-9]+)");
            string appleId = "";
            if (matchId.Success)
            {
                appleId = matchId.Groups[1].Value;
            }
            else
            {
                var matchSong = System.Text.RegularExpressions.Regex.Match(html, @"music\.apple\.com/[a-z]{2}/song/[^/]+/([0-9]+)");
                if (matchSong.Success)
                {
                    appleId = matchSong.Groups[1].Value;
                }
            }

            if (!string.IsNullOrEmpty(appleId))
            {
                var json = System.Text.Encoding.UTF8.GetString(await _http.GetByteArrayAsync($"https://itunes.apple.com/lookup?id={appleId}"));
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    var best = results.EnumerateArray().FirstOrDefault();
                    string foundTitle = best.TryGetProperty("trackName", out var t) ? (t.GetString() ?? "") : "";
                    string foundArtist = best.TryGetProperty("artistName", out var a) ? (a.GetString() ?? "") : "";
                    
                    double score = CalculateScore(searchArtist, title, foundArtist, foundTitle);
                    if (duration.HasValue)
                    {
                        double targetSec = duration.Value.TotalSeconds;
                        if (targetSec > 0 && best.TryGetProperty("trackTimeMillis", out var tm) && tm.TryGetInt32(out int ms))
                        {
                            double expectedMs = targetSec * 1000;
                            double ratio = Math.Min(expectedMs, ms) / Math.Max(expectedMs, ms);
                            if (ratio > 0.8) score += ratio * 0.2;
                        }
                    }

                    var res = new AutoTagResult
                    {
                        Title = foundTitle,
                        Artist = foundArtist,
                        Album = best.TryGetProperty("collectionName", out var alb) ? alb.GetString() : null,
                        Year = best.TryGetProperty("releaseDate", out var rd) && rd.GetString()?.Length >= 4 && int.TryParse(rd.GetString()!.Substring(0, 4), out int y) ? y : null,
                        Genre = best.TryGetProperty("primaryGenreName", out var g) ? g.GetString() : null,
                        CoverPath = best.TryGetProperty("artworkUrl100", out var aw) ? aw.GetString()?.Replace("100x100bb", "600x600bb") : null,
                        Score = score
                    };
                    return res;
                }
            }
        }
        catch { }
        return null;
    }

    public static async Task<AutoTagResult?> LookupAsync(string artist, string title, TimeSpan? duration = null, string? filePath = null)
    {
        // DEBUG: Log what we receive and what we send
        string debugLog = $"[{DateTime.Now:HH:mm:ss}] LookupAsync called\n  artist={artist}\n  title={title}\n  filePath={filePath}\n";

        // Always prefer filename for search — metadata titles are often incomplete or wrong
        if (!string.IsNullOrEmpty(filePath))
        {
            title = CleanTitle(System.IO.Path.GetFileNameWithoutExtension(filePath));
            if (!string.IsNullOrWhiteSpace(artist) && !artist.Contains("inconnu", StringComparison.OrdinalIgnoreCase) && !title.Contains(artist, StringComparison.OrdinalIgnoreCase))
            {
                title = $"{title} {artist}";
            }
        }
        else
        {
            title = CleanTitle(title);
        }

        debugLog += $"  cleanedTitle={title}\n";
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "autotag_debug.log"), debugLog); } catch { }

        string searchArtist = (artist ?? "").Split(new[] { ',', ';', '/', '&', '|', '\\', '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => s.Length > 0) ?? "";

        if (searchArtist.Contains("inconnu", StringComparison.OrdinalIgnoreCase))
        {
            searchArtist = "";
        }

        // 1. Fetch from Apple Music Web (No IP Ban)
        var appleWebTask = AppleMusicWebLookupAsync(searchArtist, title, duration);

        // 1. Priority to iTunes
        var itunesTask = ItunesLookupAsync(searchArtist, title, title, searchArtist, duration);

        // 2. Fallbacks
        var mbTask = MusicBrainzLookupAsync(searchArtist, title, duration);
        var deezerTask = DeezerLookupAsync(searchArtist, title, duration);
        
        Task<AutoTagResult?> fpTask = Task.FromResult<AutoTagResult?>(null);
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            fpTask = FingerprintLookupAsync(filePath);
        }

        var tasks = new List<Task<AutoTagResult?>> { appleWebTask, itunesTask, mbTask, deezerTask, fpTask };
        var timeoutTask = Task.Delay(10000); // 10 secondes maximum
        
        var candidates = new List<AutoTagResult>();
        while (tasks.Count > 0)
        {
            var finished = await Task.WhenAny(tasks.Concat(new Task[] { timeoutTask }));
            if (finished == timeoutTask) break;
            
            tasks.Remove((Task<AutoTagResult?>)finished);
            var res = ((Task<AutoTagResult?>)finished).Result;
            
            if (res != null && IsResultValid(res, title, searchArtist))
            {
                if (finished == fpTask)
                {
                    double textScore = Math.Max(JaroWinkler($"{res.Artist} {res.Title}".ToLower(), $"{searchArtist} {title}".ToLower()), JaroWinkler(res.Title?.ToLower() ?? "", title.ToLower()) * 0.6 + JaroWinkler(res.Artist?.ToLower() ?? "", searchArtist.ToLower()) * 0.4);
                    res.Score = 0.7 + textScore * 0.3;
                }

                candidates.Add(res);
                if (res.Score >= 0.8) return res; // EARLY EXIT for max speed!
            }
        }

        var best = candidates.OrderByDescending(c => c.Score).FirstOrDefault();
        var allText = $"{title} {searchArtist}".Trim();
        var words = allText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var fallbackFull = await ItunesLookupAsync("", allText, title, searchArtist, duration);
            if (fallbackFull != null && IsResultValid(fallbackFull, title, searchArtist) && fallbackFull.Score >= 0.4) return fallbackFull;

            string keywordQuery = string.Join(" ", words.Take(3));
            var fallbackItunes = await ItunesLookupAsync("", keywordQuery, title, searchArtist, duration);
            if (fallbackItunes != null && IsResultValid(fallbackItunes, title, searchArtist) && fallbackItunes.Score >= 0.4) return fallbackItunes;
            
            string artistQuery = string.Join(" ", words.Take(2));
            var fallbackItunes2 = await ItunesLookupAsync("", artistQuery, title, searchArtist, duration);
            if (fallbackItunes2 != null && IsResultValid(fallbackItunes2, title, searchArtist) && fallbackItunes2.Score >= 0.3) return fallbackItunes2;
        }

        return null;
    }

    private static async Task<AutoTagResult?> ItunesLookupAsync(string artist, string title, string originalTitle, string originalArtist, TimeSpan? duration = null)
    {
        try
        {
            string q = Uri.EscapeDataString($"{artist} {title}".Trim());
            var json = System.Text.Encoding.UTF8.GetString(await _http.GetByteArrayAsync($"https://itunes.apple.com/search?term={q}&entity=song&limit=15"));
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                foreach (var item in results.EnumerateArray())
                {
                    string foundTitle = item.TryGetProperty("trackName", out var t) ? (t.GetString() ?? "") : "";
                    string foundArtist = item.TryGetProperty("artistName", out var a) ? (a.GetString() ?? "") : "";
                    
                    var res = new AutoTagResult { Title = foundTitle, Artist = foundArtist };
                    if (!IsResultValid(res, originalTitle, originalArtist)) continue;

                    string foundAlbum = item.TryGetProperty("collectionName", out var c) ? (c.GetString() ?? "") : "";
                    string cover = item.TryGetProperty("artworkUrl100", out var art) ? (art.GetString()?.Replace("100x100bb", "600x600bb") ?? "") : "";
                    
                    int? year = null;
                    if (item.TryGetProperty("releaseDate", out var rd) && rd.GetString() is string date && date.Length >= 4)
                    {
                        if (int.TryParse(date.Substring(0, 4), out int y)) year = y;
                    }
                    
                    int? trackNum = item.TryGetProperty("trackNumber", out var tn) ? tn.GetInt32() : null;
                    string? genre = item.TryGetProperty("primaryGenreName", out var gn) ? gn.GetString() : null;
                    
                    res.Album = foundAlbum;
                    res.CoverPath = cover;
                    res.Year = year;
                    res.TrackNumber = trackNum;
                    res.Genre = genre;
                    res.Score = 1.0;
                    return res;
                }
            }
            
            // Fallback: Search just the title
            q = Uri.EscapeDataString($"{title}".Trim());
            json = System.Text.Encoding.UTF8.GetString(await _http.GetByteArrayAsync($"https://itunes.apple.com/search?term={q}&entity=song&limit=15"));
            doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out results) && results.GetArrayLength() > 0)
            {
                foreach (var item in results.EnumerateArray())
                {
                    string foundTitle = item.TryGetProperty("trackName", out var t) ? (t.GetString() ?? "") : "";
                    string foundArtist = item.TryGetProperty("artistName", out var a) ? (a.GetString() ?? "") : "";
                    
                    var res = new AutoTagResult { Title = foundTitle, Artist = foundArtist };
                    if (!IsResultValid(res, originalTitle, originalArtist)) continue;

                    string foundAlbum = item.TryGetProperty("collectionName", out var c) ? (c.GetString() ?? "") : "";
                    string cover = item.TryGetProperty("artworkUrl100", out var art) ? (art.GetString()?.Replace("100x100bb", "600x600bb") ?? "") : "";
                    
                    int? year = null;
                    if (item.TryGetProperty("releaseDate", out var rd) && rd.GetString() is string date && date.Length >= 4)
                    {
                        if (int.TryParse(date.Substring(0, 4), out int y)) year = y;
                    }
                    
                    int? trackNum = item.TryGetProperty("trackNumber", out var tn) ? tn.GetInt32() : null;
                    string? genre = item.TryGetProperty("primaryGenreName", out var gn) ? gn.GetString() : null;
                    
                    res.Album = foundAlbum;
                    res.CoverPath = cover;
                    res.Year = year;
                    res.TrackNumber = trackNum;
                    res.Genre = genre;
                    res.Score = 1.0;
                    return res;
                }
            }
        }
        catch { }
        return null;
    }

    private static async Task<AutoTagResult?> DeezerLookupAsync(string artist, string title, TimeSpan? duration = null)
    {
        try
        {
            string q = Uri.EscapeDataString($"{artist} {title}".Trim());
            var response = await _http.GetFromJsonAsync<DeezerSearchResponse>($"https://api.deezer.com/search?q={q}&limit=8");
            if (response?.data == null || response.data.Count == 0) return null;

            double targetSec = duration?.TotalSeconds ?? 0;
            var titleLower = title.ToLower();
            var artistLower = artist?.ToLower() ?? "";

            var best = ScoreDeezerResults(response.data, titleLower, artistLower, targetSec);
            if (best.d == null || best.Score < 0.6)
            {
                string titleOnly = Uri.EscapeDataString(title);
                var retry = await _http.GetFromJsonAsync<DeezerSearchResponse>($"https://api.deezer.com/search?q={titleOnly}&limit=8");
                if (retry?.data != null)
                {
                    best = ScoreDeezerResults(retry.data, titleLower, artistLower, targetSec);
                }
            }

            if (best.d == null || best.Score < MinScoreThreshold) return null;

            string? genreName = null;
            if (best.d.genre_id.HasValue && DeezerGenres.TryGetValue(best.d.genre_id.Value, out var gn))
                genreName = gn;

            return new AutoTagResult
            {
                Title = best.d.title,
                Artist = best.d.artist?.name,
                Album = best.d.album?.title,
                Year = int.TryParse(best.d.release_date?.Split('-')[0], out var y) ? y : null,
                TrackNumber = best.d.track_position,
                Genre = genreName,
                Score = best.Score
            };
        }
        catch { return null; }
    }

    private static (DeezerTrack? d, double Score) ScoreDeezerResults(
        List<DeezerTrack> data, string titleLower, string artistLower, double targetSec)
    {
        return data
            .Select(d =>
            {
                string foundTitle = (d.title ?? "").ToLower();
                string foundArtist = (d.artist?.name ?? "").ToLower();
                
                double score = CalculateScore(artistLower, titleLower, foundArtist, foundTitle);

                if (targetSec > 0 && d.duration.HasValue && d.duration.Value > 0)
                {
                    double ratio = Math.Min(d.duration.Value, targetSec) / Math.Max(d.duration.Value, targetSec);
                    score += ratio * 0.30;
                }
                return (d, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
    }

    private static async Task<AutoTagResult?> MusicBrainzLookupAsync(string artist, string title, TimeSpan? duration = null)
    {
        try
        {
            int targetMs = duration.HasValue ? (int)duration.Value.TotalMilliseconds : 0;
            
            // Remove punctuation that breaks Lucene query syntax
            string mbTitle = System.Text.RegularExpressions.Regex.Replace(title, @"[^\w\s]", "").Trim();
            string mbArtist = System.Text.RegularExpressions.Regex.Replace(artist ?? "", @"[^\w\s]", "").Trim();

            var words = mbTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var keyWords = words.Where(w => w.Length > 2).Take(4).ToList();
            if (keyWords.Count == 0) keyWords = words.Take(2).ToList();

            List<string> queries = [];
            if (!string.IsNullOrWhiteSpace(mbArtist))
                queries.Add($"artist:({mbArtist}) AND recording:({mbTitle})");
            queries.Add($"recording:({mbTitle})");
            if (keyWords.Count > 0)
                queries.Add($"recording:({string.Join(" AND ", keyWords)})");
            if (keyWords.Count > 1)
                queries.Add($"recording:({keyWords[0]})");
            if (!string.IsNullOrWhiteSpace(mbArtist) && keyWords.Count > 1)
                queries.Add($"artist:({mbArtist}) AND recording:({keyWords[0]})");

            var candidates = new List<(Recording rec, double score)>();
            var titleLower = title.ToLower();
            var artistLower = artist?.ToLower() ?? "";

            foreach (var q in queries)
            {
                string url = $"https://musicbrainz.org/ws/2/recording/?query={Uri.EscapeDataString(q)}&fmt=json&limit=8";
                try
                {
                    var response = await _http.GetFromJsonAsync<MusicBrainzResponse>(url);
                    if (response?.recordings == null) continue;
                    foreach (var r in response.recordings)
                    {
                        if (string.IsNullOrWhiteSpace(r.title)) continue;

                        string foundTitle = r.title.ToLower();
                        string foundArtist = r.artistCredit?.FirstOrDefault()?.name?.ToLower() ?? "";
                        
                        double score = CalculateScore(artistLower, titleLower, foundArtist, foundTitle);
                        
                        if (targetMs > 0 && r.length.HasValue && r.length.Value > 0)
                        {
                            double ratio = Math.Min(r.length.Value, targetMs) / Math.Max(r.length.Value, targetMs);
                            score += ratio * 0.30;
                        }
                        if (score >= MinScoreThreshold) candidates.Add((r, score));
                    }
                }
                catch { }
            }

            var best = candidates.OrderByDescending(c => c.score).FirstOrDefault();
            if (best.rec == null || best.score < MinScoreThreshold) return null;

            var firstRelease = best.rec.releases?.FirstOrDefault();
            var tag = best.rec.tags?.OrderByDescending(t => t.count).FirstOrDefault();
            var bestArtist = best.rec.artistCredit?.FirstOrDefault()?.name ?? artist;

            return new AutoTagResult
            {
                Title = best.rec.title,
                Artist = bestArtist,
                Album = firstRelease?.title,
                Year = firstRelease?.date is string d && d.Length >= 4
                       && int.TryParse(d[..4], out var y) ? y : null,
                TrackCount = firstRelease?.trackCount,
                Genre = tag?.name,
                Score = best.score
            };
        }
        catch { return null; }
    }

    private static string CleanTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        string s = raw.Trim();
        // Only strip actual audio file extensions, not arbitrary dots
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\.(mp3|flac|wav|ogg|m4a|aac|wma|opus|aiff|alac)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        

        // Remplacer les caracteres de parentheses/crochets/japonais par des espaces
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[\(\)\[\]\{\}]+", " ");

        // Enlever les mots-clés parasites (sans enlever cover, remix, etc. qui font partie du vrai titre)
        string[] keywords = { "official", "music video", "lyric", "lyrics", "audio", "visualizer", "remaster", "remastered", "edit", "clip", "hq", "hd", "4k", "1080p", "720p", "explicit", "clean", "ncs", "free download", "preview", "amv", "video", "music" };
        
        foreach (var r in keywords)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, $@"(?i)\b{r}\b", "");
        }
        
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

        return s;
    }

    private static bool IsResultValid(AutoTagResult result, string originalTitle, string originalArtist)
    {
        if (string.IsNullOrWhiteSpace(result.Title)) return false;
        if (string.IsNullOrWhiteSpace(result.Artist)) return false;

        if (result.Artist.Contains("inconnu", StringComparison.OrdinalIgnoreCase)) return false;
        if (result.Title.Contains("inconnu", StringComparison.OrdinalIgnoreCase)) return false;

        if (result.Title.Contains(" - "))
        {
            var parts = result.Title.Split(new[] { " - " }, 2, StringSplitOptions.None);
            if (parts[0].Trim().Equals(result.Artist, StringComparison.OrdinalIgnoreCase))
                return false; 
        }
        
        string origFull = $"{originalTitle} {originalArtist}".ToLowerInvariant();
        string resFull = $"{result.Title} {result.Artist}".ToLowerInvariant();
        
        var stopWords = new HashSet<string> { "the", "and", "for", "with", "from", "feat", "ft", "music", "audio", "official", "video", "lyrics", "clip" };
        var origWords = origFull.Split(new[] { ' ', '-', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length > 2 && !stopWords.Contains(w)).ToList();
        
        if (origWords.Count > 0)
        {
            int matches = origWords.Count(w => resFull.Contains(w));
            double ratio = (double)matches / origWords.Count;
            if (ratio < 0.20) return false;
            
            var origTitleWords = originalTitle.ToLowerInvariant().Split(new[] { ' ', '-', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Where(w => w.Length > 2 && !stopWords.Contains(w)).ToList();
            if (origTitleWords.Count > 0)
            {
                string resTitleLower = result.Title.ToLowerInvariant();
                int titleMatches = origTitleWords.Count(w => resTitleLower.Contains(w));
                double titleRatio = (double)titleMatches / origTitleWords.Count;
                if (titleRatio < 0.20) return false;
            }
        }

        return true; 
    }

    private static double JaroWinkler(string a, string b)
    {
        if (a == b) return 1.0;
        
        // Custom permissive subset check for "rosé apt" -> "ROSÃ‰ & Bruno Mars APT."
        var aWords = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToArray();
        var bWords = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToArray();
        
        if (aWords.Length == 0) aWords = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (bWords.Length == 0) bWords = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        int aMatches = aWords.Count(w => b.Contains(w));
        int bMatches = bWords.Count(w => a.Contains(w));
        
        double aRatio = aWords.Length > 0 ? (double)aMatches / aWords.Length : 0;
        double bRatio = bWords.Length > 0 ? (double)bMatches / bWords.Length : 0;
        
        double combinedRatio = (aRatio + bRatio) / 2.0;
        double maxRatio = Math.Max(aRatio, bRatio);
        double lengthPenalty = (double)Math.Min(a.Length, b.Length) / Math.Max(a.Length, b.Length);

        if (maxRatio >= 0.50)
        {
            double subsetScore = 0.5 + (combinedRatio * 0.5);
            subsetScore *= (0.5 + 0.5 * lengthPenalty);
            if (subsetScore >= 0.7) return subsetScore;
        }

        int len = Math.Max(a.Length, b.Length);
        if (len == 0) return 1.0;
        int matchDist = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
        var aMatch = new bool[a.Length];
        var bMatch = new bool[b.Length];
        int matches = 0;
        for (int i = 0; i < a.Length; i++)
            for (int j = Math.Max(0, i - matchDist); j < Math.Min(b.Length, i + matchDist + 1); j++)
                if (!bMatch[j] && a[i] == b[j]) { aMatch[i] = bMatch[j] = true; matches++; break; }
        if (matches == 0) return 0;
        int trans = 0;
        for (int i = 0, k = 0; i < a.Length; i++)
        {
            if (!aMatch[i]) continue;
            while (!bMatch[k]) k++;
            if (a[i] != b[k]) trans++;
            k++;
        }
        double jaro = ((double)matches / a.Length + (double)matches / b.Length + (double)(matches - trans / 2) / matches) / 3.0;
        int prefix = 0;
        for (int i = 0; i < Math.Min(4, Math.Min(a.Length, b.Length)); i++)
            if (a[i] == b[i]) prefix++; else break;
        double jw = jaro + prefix * 0.1 * (1 - jaro);

        double lp = (double)Math.Min(a.Length, b.Length) / Math.Max(a.Length, b.Length);
        if (lp < 0.5) jw *= (0.5 + lp);
        return jw;
    }

    public static bool WriteMetadata(string filePath, AutoTagResult data)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            bool changed = false;

            if (data.Title != null) { file.Tag.Title = data.Title; changed = true; }
            if (data.Artist != null) { file.Tag.Performers = new[] { data.Artist }; changed = true; }
            if (data.Album != null) { file.Tag.Album = data.Album; changed = true; }
            if (data.Genre != null) { file.Tag.Genres = new[] { data.Genre }; changed = true; }
            if (data.Year.HasValue) { file.Tag.Year = (uint)data.Year.Value; changed = true; }
            if (data.TrackNumber.HasValue) { file.Tag.Track = (uint)data.TrackNumber.Value; changed = true; }
            if (data.TrackCount.HasValue) { file.Tag.TrackCount = (uint)data.TrackCount.Value; changed = true; }
            if (data.AlbumArtist != null) { file.Tag.AlbumArtists = new[] { data.AlbumArtist }; changed = true; }

            if (data.CoverPath != null && File.Exists(data.CoverPath))
            {
                var pic = new TagLib.Picture(data.CoverPath);
                file.Tag.Pictures = new TagLib.IPicture[] { pic };
                changed = true;
            }

            if (changed) file.Save();
            return changed;
        }
        catch { return false; }
    }

    private static string? _fpcalcPath;
    private static readonly string FpcalcDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Resona", "fpcalc");
    private static readonly string FpcalcExe = Path.Combine(FpcalcDir, "fpcalc.exe");
    private const string AcoustIdApiKey = "8XazkMEWCk";

    public static async Task<AutoTagResult?> FingerprintLookupAsync(string filePath)
    {
        try
        {
            await EnsureFpcalcAsync();
            if (!File.Exists(FpcalcExe)) return null;

            var psi = new ProcessStartInfo
            {
                FileName = FpcalcExe,
                Arguments = $"\"{filePath}\" -length 120",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;

            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0) return null;

            string? fingerprint = null;
            int? duration = null;
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("FINGERPRINT="))
                    fingerprint = line["FINGERPRINT=".Length..].Trim();
                else if (line.StartsWith("DURATION=") && int.TryParse(line["DURATION=".Length..].Trim(), out var d))
                    duration = d;
            }

            if (string.IsNullOrEmpty(fingerprint) || duration == null || duration == 0)
                return null;

            string acUrl = $"https://api.acoustid.org/v2/lookup?client={AcoustIdApiKey}" +
                           $"&meta=recordings+releasegroups+compress&duration={duration}" +
                           $"&fingerprint={Uri.EscapeDataString(fingerprint)}";
            var acResp = await _http.GetFromJsonAsync<AcoustIdResponse>(acUrl);
            if (acResp?.results == null || acResp.results.Count == 0) return null;

            var bestResult = acResp.results
                .OrderByDescending(r => r.score ?? 0)
                .FirstOrDefault(r => r.recordings?.Count > 0);
            if (bestResult?.recordings == null || bestResult.recordings.Count == 0) return null;

            var bestRecording = bestResult.recordings
                .OrderByDescending(r => r.releasegroups?.Count ?? 0)
                .FirstOrDefault();
            if (bestRecording == null) return null;

            if (!string.IsNullOrEmpty(bestRecording.id))
            {
                var mbResult = await MusicBrainzLookupByMbidAsync(bestRecording.id);
                if (mbResult != null) return mbResult;
            }

            var artist = bestRecording.artists?.FirstOrDefault()?.name ?? "";
            var title = bestRecording.title ?? "";
            var album = bestRecording.releasegroups?.FirstOrDefault()?.title ?? "";

            return new AutoTagResult
            {
                Title = title, Artist = artist, Album = album, Score = bestResult.score ?? 0.5
            };
        }
        catch { return null; }
    }

    private static async Task EnsureFpcalcAsync()
    {
        if (File.Exists(FpcalcExe)) { _fpcalcPath = FpcalcExe; return; }

        Directory.CreateDirectory(FpcalcDir);
        string zipPath = Path.Combine(FpcalcDir, "fpcalc.zip");

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.TryParseAdd(UserAgent);
            var bytes = await http.GetByteArrayAsync(
                "https://github.com/acoustid/chromaprint/releases/download/v1.5.1/chromaprint-fpcalc-1.5.1-windows-x86_64.zip");
            await File.WriteAllBytesAsync(zipPath, bytes);

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, FpcalcDir);
            File.Delete(zipPath);
        }
        catch
        {
        }

        _fpcalcPath = File.Exists(FpcalcExe) ? FpcalcExe : null;
    }

    private static async Task<AutoTagResult?> MusicBrainzLookupByMbidAsync(string mbid)
    {
        try
        {
            string url = $"https://musicbrainz.org/ws/2/recording/{mbid}?fmt=json&inc=artists+releases+tags";
            var recording = await _http.GetFromJsonAsync<Recording>(url);
            if (recording == null || string.IsNullOrEmpty(recording.title)) return null;

            var firstRelease = recording.releases?.FirstOrDefault();
            var tag = recording.tags?.OrderByDescending(t => t.count).FirstOrDefault();

            return new AutoTagResult
            {
                Title = recording.title,
                Artist = recording.artistCredit?.FirstOrDefault()?.name,
                Album = firstRelease?.title,
                Year = firstRelease?.date is string d && d.Length >= 4
                       && int.TryParse(d[..4], out var y) ? y : null,
                TrackCount = firstRelease?.trackCount,
                Genre = tag?.name,
                Score = 0.95
            };
        }
        catch { return null; }
    }

    private class AcoustIdResponse
    {
        public List<AcoustIdResult>? results { get; set; }
    }

    private class AcoustIdResult
    {
        public double? score { get; set; }
        public List<AcoustIdRecording>? recordings { get; set; }
    }

    private class AcoustIdRecording
    {
        public string? id { get; set; }
        public string? title { get; set; }
        public List<AcoustIdArtist>? artists { get; set; }
        public List<AcoustIdReleaseGroup>? releasegroups { get; set; }
    }

    private class AcoustIdArtist
    {
        public string? name { get; set; }
    }

    private class AcoustIdReleaseGroup
    {
        public string? title { get; set; }
    }

    private class DeezerSearchResponse
    {
        public List<DeezerTrack>? data { get; set; }
    }

    private class DeezerTrack
    {
        public string? title { get; set; }
        public DeezerArtist? artist { get; set; }
        public DeezerAlbum? album { get; set; }
        public string? release_date { get; set; }
        public int? track_position { get; set; }
        public int? genre_id { get; set; }
        public int? duration { get; set; }
    }

    private class DeezerArtist
    {
        public string? name { get; set; }
    }

    private class DeezerAlbum
    {
        public string? title { get; set; }
    }

    private class MusicBrainzResponse
    {
        public List<Recording>? recordings { get; set; }
    }

    private class Recording
    {
        public string? id { get; set; }
        public string? title { get; set; }
        public List<ArtistCredit>? artistCredit { get; set; }
        public List<Release>? releases { get; set; }
        public List<Tag>? tags { get; set; }
        public int? length { get; set; }
    }

    private class ArtistCredit
    {
        public string? name { get; set; }
    }

    private class Release
    {
        public string? title { get; set; }
        public string? date { get; set; }
        public int? trackCount { get; set; }
    }

    private class Tag
    {
        public string? name { get; set; }
        public int? count { get; set; }
    }
}







public static class LyricsTranslatorService
{
    public static async Task<string> TranslateTextAsync(string text, string targetLanguage = "fr")
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        try
        {
            using var http = new System.Net.Http.HttpClient();
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLanguage}&dt=t&q={Uri.EscapeDataString(text)}";
            var response = await http.GetStringAsync(url);
            var doc = System.Text.Json.JsonDocument.Parse(response);
            var sb = new System.Text.StringBuilder();
            foreach (var part in doc.RootElement[0].EnumerateArray())
            {
                if (part.ValueKind == System.Text.Json.JsonValueKind.Array && part.GetArrayLength() > 0)
                {
                    sb.Append(part[0].GetString());
                }
            }
            return sb.ToString();
        }
        catch { return text; }
    }
}
