// ============================================================
//  Services.cs  Ã¢â‚¬â€  Resona
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

    public string BuildCoverSearchQuery(string title, string artist, string album)
    {
        string safeTitle = title ?? "";
        string safeArtist = artist ?? "";
        string safeAlbum = album ?? "";

        // Remove unknown keywords
        if (safeArtist.ToLowerInvariant().Contains("inconnu") || safeArtist.ToLowerInvariant().Contains("unknown")) safeArtist = "";
        if (safeAlbum.ToLowerInvariant().Contains("inconnu") || safeAlbum.ToLowerInvariant().Contains("unknown")) safeAlbum = "";

        // Clean title
        safeTitle = System.Text.RegularExpressions.Regex.Replace(safeTitle, @"(?i)\(?(official|lyrics|audio|video|music video|hd|1080p|hq)[^\)]*\)?", "").Trim();
        safeTitle = System.Text.RegularExpressions.Regex.Replace(safeTitle, @"(?i)\[(official|lyrics|audio|video|music video|hd|1080p|hq)[^\]]*\]", "").Trim();
        
        var parts = new System.Collections.Generic.List<string> { safeTitle };
        if (!string.IsNullOrWhiteSpace(safeArtist)) parts.Add(safeArtist);

        string query = string.Join(" ", parts);
        query = System.Text.RegularExpressions.Regex.Replace(query, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(query) ? "" : query + " music";
    }

    
    
    public async Task<System.Collections.Generic.List<string>> SearchMusicBrainzImagesAsync(string query, int count = 5)
    {
        var results = new System.Collections.Generic.List<string>();
        try
        {
            var url = $"https://musicbrainz.org/ws/2/release/?query={Uri.EscapeDataString(query)}&fmt=json&limit={count * 2}";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Resona/2.2 (https://github.com/Resona)");
            var json = await client.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("releases", out var releases))
            {
                foreach (var release in releases.EnumerateArray())
                {
                    if (results.Count >= count) break;
                    if (release.TryGetProperty("id", out var idProp))
                    {
                        string mbid = idProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(mbid))
                        {
                            // Try CoverArtArchive
                            string coverUrl = $"https://coverartarchive.org/release/{mbid}/front-500";
                            results.Add(coverUrl);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MusicBrainz Search Error: " + ex.Message);
        }
        return results;
    }
public async Task<System.Collections.Generic.List<string>> SearchAppleMusicImagesAsync(string query, int count = 5)
    {
        var results = new System.Collections.Generic.List<string>();
        try
        {
            var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&entity=album&limit={count * 2}";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var json = await client.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (results.Count >= count) break;
                    if (item.TryGetProperty("artworkUrl100", out var artUrl))
                    {
                        string highResUrl = artUrl.GetString()?.Replace("100x100bb", "600x600bb") ?? "";
                        if (!string.IsNullOrEmpty(highResUrl) && !results.Contains(highResUrl))
                        {
                            results.Add(highResUrl);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Apple Music Search Error: " + ex.Message);
        }
        return results;
    }
public async Task<System.Collections.Generic.List<string>> SearchGoogleImagesAsync(string query, int count = 5)
    {
        // Recherche Google Images reelle (tbm=isch), filtree sur les images carrees (ratio 1:1 via tbs=iar:s),
        // avec repli automatique sur Bing Images si Google echoue (blocage, captcha, structure changee...).
        var results = await SearchGoogleImagesInternalAsync(query, count);
        if (results.Count == 0)
        {
            try { results = await SearchBingImagesAsync(query, count); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Bing Images fallback error: " + ex.Message); }
        }
        return results;
    }

    private static readonly string[] DesktopUserAgents =
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:127.0) Gecko/20100101 Firefox/127.0",
    };

    private static string PickUserAgent()
    {
        int idx = Random.Shared.Next(DesktopUserAgents.Length);
        return DesktopUserAgents[idx];
    }

    private async Task<System.Collections.Generic.List<string>> SearchGoogleImagesInternalAsync(string query, int count)
    {
        var results = new System.Collections.Generic.List<string>();
        try
        {
            // tbm=isch = recherche d'images ; tbs=iar:s = ratio carre (square) uniquement ;
            // safe=active pour eviter le contenu inapproprie ; gl/hl=en pour eviter les variantes locales.
            var url = "https://www.google.com/search"
                + $"?q={Uri.EscapeDataString(query)}"
                + "&tbm=isch"
                + "&tbs=iar:s"
                + "&safe=active"
                + "&gl=us"
                + "&hl=en"
                + "&pws=0";

            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };
            // Cookies de consentement pre-acceptes pour eviter la page d'interstitiel RGPD (frequente en UE),
            // qui sinon remplace la page de resultats et fait echouer totalement l'extraction (0 image trouvee).
            handler.CookieContainer.Add(new Uri("https://www.google.com"), new System.Net.Cookie("CONSENT", "YES+cb", "/", ".google.com"));
            handler.CookieContainer.Add(new Uri("https://www.google.com"), new System.Net.Cookie("SOCS", "CAESHAgBEhJnd3NfMjAyNDAxMDEtMF9SQzIaAmVuIAEaBgiA_LyaBg", "/", ".google.com"));

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.Add("User-Agent", PickUserAgent());
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

            var html = await client.GetStringAsync(url);

            System.Diagnostics.Debug.WriteLine($"Google Images: HTML length={html.Length} for '{query}'");

            // Google embarque les resultats sous forme de tableaux JSON dans le HTML plutot que dans des balises
            // <img src=...> exploitables directement. Selon le contexte, les URLs apparaissent soit echappees (\/)
            // soit non echappees (/) dans ce JSON brut : on matche les deux formes. On exclut les assets statiques
            // propres a Google (gstatic, favicon, logos...).
            var candidates = new System.Collections.Generic.List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(
                html,
                "\"(https?:(?:\\\\/\\\\/|//)[^\"\\\\]+?\\.(?:jpg|jpeg|png|webp))\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string raw = m.Groups[1].Value.Replace("\\/", "/");
                string imgUrl;
                try { imgUrl = System.Text.RegularExpressions.Regex.Unescape(raw); }
                catch { imgUrl = raw; }

                if (IsUnwantedImageHost(imgUrl)) continue;
                if (!candidates.Contains(imgUrl)) candidates.Add(imgUrl);
            }

            // Repli : variante de template ou les URLs sont dans des attributs data-src/data-iurl non JSON.
            if (candidates.Count == 0)
            {
                var altMatches = System.Text.RegularExpressions.Regex.Matches(
                    html,
                    "(?:data-src|data-iurl)=\"(https?://[^\"]+?\\.(?:jpg|jpeg|png|webp)[^\"]*)\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match m in altMatches)
                {
                    string imgUrl = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
                    if (IsUnwantedImageHost(imgUrl)) continue;
                    if (!candidates.Contains(imgUrl)) candidates.Add(imgUrl);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Google Images: {candidates.Count} candidats trouves pour '{query}'");

            foreach (var imgUrl in candidates)
            {
                if (results.Count >= count) break;
                results.Add(imgUrl);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Google Images Search Error: " + ex.Message);
        }
        return results;
    }

    private async Task<System.Collections.Generic.List<string>> SearchBingImagesAsync(string query, int count)
    {
        var results = new System.Collections.Generic.List<string>();
        try
        {
            // qft=+filterui:aspect-square force les resultats carres cote Bing.
            // setlang/cc=us + mkt=en-US pour eviter les variantes locales et certains blocages regionaux.
            var url = "https://www.bing.com/images/search"
                + $"?q={Uri.EscapeDataString(query)}"
                + "&qft=+filterui:aspect-square"
                + "&form=IRFLTR"
                + "&setlang=en-US"
                + "&mkt=en-US"
                + "&cc=US";

            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(8);
            // Un jeu de headers plus complet (Accept, Sec-Fetch-*, etc.) reduit fortement le risque de reponse
            // "bot-safe" allegee de Bing qui ne contient aucun resultat exploitable.
            client.DefaultRequestHeaders.Add("User-Agent", PickUserAgent());
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            var html = await client.GetStringAsync(url);

            System.Diagnostics.Debug.WriteLine($"Bing Images: HTML length={html.Length} for '{query}'");

            var candidates = new System.Collections.Generic.List<string>();

            // Bing expose l'URL de l'image source dans l'attribut murl des balises <a class="iusc" m='{"murl":"..."}'>
            var matches = System.Text.RegularExpressions.Regex.Matches(html, "\"murl\":\"([^\"]+)\"");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string imgUrl;
                try { imgUrl = System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value); }
                catch { imgUrl = m.Groups[1].Value; }

                if (IsUnwantedImageHost(imgUrl)) continue;
                if (!candidates.Contains(imgUrl)) candidates.Add(imgUrl);
            }

            // Repli : Bing encode parfois l'URL reelle dans le parametre mediaurl= d'un lien th?id=... plutot
            // que dans le blob JSON "murl" (variante de rendu selon le marche/A-B test).
            if (candidates.Count == 0)
            {
                var altMatches = System.Text.RegularExpressions.Regex.Matches(
                    html,
                    "mediaurl=([^&\"]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match m in altMatches)
                {
                    string imgUrl = Uri.UnescapeDataString(m.Groups[1].Value);
                    if (IsUnwantedImageHost(imgUrl)) continue;
                    if (!candidates.Contains(imgUrl)) candidates.Add(imgUrl);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Bing Images: {candidates.Count} candidats trouves pour '{query}'");

            foreach (var imgUrl in candidates)
            {
                if (results.Count >= count) break;
                results.Add(imgUrl);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Bing Images Search Error: " + ex.Message);
        }
        return results;
    }

    private static bool IsUnwantedImageHost(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        string lower = url.ToLowerInvariant();
        // On exclut les assets d'interface des moteurs de recherche eux-memes (logos, icones UI),
        // qui n'ont rien a voir avec des pochettes d'album.
        return lower.Contains("gstatic.com/images") ||
               lower.Contains("google.com/logos") ||
               lower.Contains("bing.com/rp/") ||
               lower.Contains("th.bing.com/th/id/or") ||
               lower.Contains("/favicon") ||
               lower.EndsWith(".svg") ||
               lower.EndsWith(".gif");
    }

    public async Task<string?> FindAndCacheCoverAsync(string trackId, string artist, string album, string title = "", string? filePath = null)
    {
        string cachePath = Path.Combine(_cacheDir, $"{trackId}.jpg");
        if (System.IO.File.Exists(cachePath)) return cachePath;

        string query = BuildCoverSearchQuery(title, artist, album);
        if (string.IsNullOrWhiteSpace(query)) return null;

        string? imageUrl = null;
        
        if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
        {
            var fpResult = await AutoTagService.FingerprintLookupAsync(filePath);
            if (fpResult != null && !string.IsNullOrEmpty(fpResult.CoverPath))
            {
                imageUrl = fpResult.CoverPath;
            }
        }

        if (string.IsNullOrEmpty(imageUrl))
        {
            try
            {
                var results = await SearchGoogleImagesAsync(query, 1);
                if (results != null && results.Count > 0)
                {
                    imageUrl = results[0];
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(imageUrl))
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(imageUrl);
                await System.IO.File.WriteAllBytesAsync(cachePath, bytes);
                return cachePath;
            }
            catch { }
        }
        return null;
    }

    public async Task<string?> SaveEmbeddedCoverAsync(string trackId, byte[] imageBytes)
    {
        if (imageBytes.Length == 0) return null;
        string cachePath = Path.Combine(_cacheDir, $"{trackId}.jpg");
        try { await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes); return cachePath; }
        catch { return null; }
    }

    

    } 

public class LyricsResult { public string? PlainLyrics { get; set; } public string? SyncedLyrics { get; set; } public bool Found => !string.IsNullOrWhiteSpace(PlainLyrics) || !string.IsNullOrWhiteSpace(SyncedLyrics); } 

public class LyricsService
{
    private readonly HttpClient _http;
    
    public LyricsService(HttpClient http) 
    { 
        _http = http; 
    } 
    
    private class LrcLibResponse 
    { 
        [System.Text.Json.Serialization.JsonPropertyName("trackName")]
        public string? TrackName { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("plainLyrics")] 
        public string? PlainLyrics { get; set; } 
        
        [System.Text.Json.Serialization.JsonPropertyName("syncedLyrics")] 
        public string? SyncedLyrics { get; set; } 
        
        [System.Text.Json.Serialization.JsonPropertyName("duration")] 
        public double? Duration { get; set; } 
    } 

    private string CleanMetadata(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string output = input;
        var pattern = @"(?i)[\(\[][^\)\]]*(official|video|audio|lyric|visualizer|live|remaster|edit|ft\.|feat\.|featuring|version|mv|vocal)[^\)\]]*[\)\]]";
        output = System.Text.RegularExpressions.Regex.Replace(output, pattern, "");
        var pattern2 = @"(?i)(official video|official music video|official audio|lyrics|lyric video|audio|visualizer|official)";
        output = System.Text.RegularExpressions.Regex.Replace(output, pattern2, "");
        output = System.Text.RegularExpressions.Regex.Replace(output, @"\s+", " ");
        return output.Trim(' ', '-', '_');
    }

    private bool IsMatch(string? qArtist, string? qTitle, string? rArtist, string? rTitle)
    {
        var qa = (qArtist ?? "").ToLowerInvariant().Trim();
        var qt = (qTitle ?? "").ToLowerInvariant().Trim();
        var ra = (rArtist ?? "").ToLowerInvariant().Trim();
        var rt = (rTitle ?? "").ToLowerInvariant().Trim();

        if (string.IsNullOrEmpty(qt)) return false;

        var rgx = new System.Text.RegularExpressions.Regex("[^a-z0-9 ]");
        qa = rgx.Replace(qa, "");
        qt = rgx.Replace(qt, "");
        ra = rgx.Replace(ra, "");
        rt = rgx.Replace(rt, "");

        bool titleMatch = rt.Contains(qt) || qt.Contains(rt);
        bool artistMatch = string.IsNullOrEmpty(qa) || string.IsNullOrEmpty(ra) || ra.Contains(qa) || qa.Contains(ra);

        return titleMatch && artistMatch;
    }

    public async Task<LyricsResult> SearchAsync(string artist, string title, string? album = null, TimeSpan? duration = null)
    {
        string cArtist = CleanMetadata(artist);
        string cTitle = CleanMetadata(title);
        
        // 1. LRCLIB Search
        try
        {
            string query = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(cArtist + " " + cTitle)}";
            var request = new HttpRequestMessage(HttpMethod.Get, query);
            request.Headers.UserAgent.TryParseAdd("Resona/2.2");
            
            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var results = await response.Content.ReadFromJsonAsync<List<LrcLibResponse>>();
                if (results != null && results.Count > 0)
                {
                    var validResults = results
                        .Where(r => (!string.IsNullOrWhiteSpace(r.PlainLyrics) || !string.IsNullOrWhiteSpace(r.SyncedLyrics)) 
                                    && IsMatch(cArtist, cTitle, r.ArtistName, r.TrackName))
                        .ToList();
                        
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
        }
        catch { }

        // 2. Netease Fallback
        try
        {
            string searchQuery = Uri.EscapeDataString($"{cArtist} {cTitle}");
            using var netease = new HttpClient();
            netease.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0");
            var searchResp = System.Text.Encoding.UTF8.GetString(await netease.GetByteArrayAsync($"https://music.163.com/api/search/pc?s={searchQuery}&offset=0&limit=5&type=1"));
            var doc = System.Text.Json.JsonDocument.Parse(searchResp);
            
            if (doc.RootElement.TryGetProperty("result", out var resultElem) &&
                resultElem.TryGetProperty("songs", out var songs) &&
                songs.GetArrayLength() > 0)
            {
                bool foundMatch = false;
                long songId = 0;
                
                // Find best matching song in top 5 results
                foreach (var song in songs.EnumerateArray())
                {
                    string rTitle = song.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string rArtist = "";
                    if (song.TryGetProperty("artists", out var artists) && artists.GetArrayLength() > 0)
                    {
                        rArtist = artists[0].TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                    }
                    
                    if (IsMatch(cArtist, cTitle, rArtist, rTitle))
                    {
                        songId = song.GetProperty("id").GetInt64();
                        foundMatch = true;
                        break;
                    }
                }

                if (foundMatch)
                {
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
        }
        catch { }

        return new LyricsResult();
    }
}

public class PlaylistM3uService
{
    public async Task<(List<string> resolvedPaths, List<string> missing, string? coverPath)> ImportAsync(string playlistFilePath)
	{
        var resolved = new List<string>();
        var missing = new List<string>();
		string? coverPath = null;
        string baseDir = Path.GetDirectoryName(playlistFilePath) ?? string.Empty;
        var lines = await System.IO.File.ReadAllLinesAsync(playlistFilePath, DetectEncoding(playlistFilePath));
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
			if (line.StartsWith("#EXTIMG:")) { string cPath = line.Substring(8).Trim(); string fullCPath = Path.IsPathRooted(cPath) ? cPath : Path.GetFullPath(Path.Combine(baseDir, cPath)); if (System.IO.File.Exists(fullCPath)) coverPath = fullCPath; continue; }
			if (line.StartsWith("#")) continue;
            string fullPath = Path.IsPathRooted(line)
                ? line
                : Path.GetFullPath(Path.Combine(baseDir, line));
            if (System.IO.File.Exists(fullPath)) resolved.Add(fullPath);
            else                       missing.Add(line);
        }
        if (string.IsNullOrEmpty(coverPath)) { string name = Path.GetFileNameWithoutExtension(playlistFilePath); foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" }) { string p = Path.Combine(baseDir, name + ext); if (System.IO.File.Exists(p)) { coverPath = p; break; } } }
			return (resolved, missing, coverPath);
    }

    public async Task ExportAsync(string outputPath, IEnumerable<Track> tracks, bool useRelativePaths = true, string? coverImagePath = null)
	{
        bool isM3u8 = Path.GetExtension(outputPath).Equals(".m3u8", StringComparison.OrdinalIgnoreCase);
        var encoding = isM3u8 ? new UTF8Encoding(false) : Encoding.GetEncoding(1252);
        string baseDir = Path.GetDirectoryName(outputPath) ?? string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
		if (!string.IsNullOrEmpty(coverImagePath) && System.IO.File.Exists(coverImagePath)) { try { string ext = Path.GetExtension(coverImagePath); string newCoverName = Path.GetFileNameWithoutExtension(outputPath) + ext; string newCoverPath = Path.Combine(baseDir, newCoverName); if (coverImagePath != newCoverPath) System.IO.File.Copy(coverImagePath, newCoverPath, true); sb.AppendLine($"#EXTIMG:{newCoverName}"); } catch { } }
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
    public AppSettings Current { get; set; } = new();
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

                        Track t = new Track
            {
                FilePath     = filePath,
                Title        = string.IsNullOrWhiteSpace(tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : tag.Title,
                Artist       = tag.Performers.Length > 0 ? string.Join(", ", tag.Performers) : Models.Strings.Current.CS_ArtisteInconnu,
                Album        = string.IsNullOrWhiteSpace(tag.Album) ? Models.Strings.Current.CS_AlbumInconnu : tag.Album,
                AlbumArtist  = tag.AlbumArtists.Length > 0 ? string.Join(", ", tag.AlbumArtists) : string.Empty,
                Duration     = file.Properties.Duration,
                TrackNumber  = (int)tag.Track,
                Year         = (int)tag.Year,
                Genre        = tag.Genres.Length > 0 ? string.Join(", ", tag.Genres) : string.Empty,
                LastModified = System.IO.File.GetLastWriteTimeUtc(filePath),
                DateAdded = new DateTime(Math.Min(System.IO.File.GetCreationTimeUtc(filePath).Ticks, System.IO.File.GetLastWriteTimeUtc(filePath).Ticks))
            };
            if (t.Duration == TimeSpan.Zero)
            {
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
                            t.Duration = new TimeSpan(0, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value) * 10);
                        }
                    }
                }
                catch { }
            }
            return t;
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
                    Artist       = Models.Strings.Current.CS_ArtisteInconnu,
                    Album        = Models.Strings.Current.CS_AlbumInconnu,
                    Duration     = duration,
                    LastModified = System.IO.File.GetLastWriteTimeUtc(filePath),
                    DateAdded = new DateTime(Math.Min(System.IO.File.GetCreationTimeUtc(filePath).Ticks, System.IO.File.GetLastWriteTimeUtc(filePath).Ticks))
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

        public async Task ClearLyricsCacheAsync()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Tracks SET Lyrics = NULL, LyricsSynced = 0";
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task ClearCoversCacheAsync()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Tracks SET CoverArtPath = NULL";
        await cmd.ExecuteNonQueryAsync();
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

        public async Task ClearVisuallyModifiedTagsAsync()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Tracks SET Title = NULL, Artist = NULL, Album = NULL, Genre = NULL, Year = NULL, TrackNumber = NULL";
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task ClearAllDataAsync()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tracks; DELETE FROM Playlists;";
        await cmd.ExecuteNonQueryAsync();
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
                IsAnalyzed=excluded.IsAnalyzed, DateAdded=excluded.DateAdded, LastModified=excluded.LastModified;
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
                IsAnalyzed=excluded.IsAnalyzed, DateAdded=excluded.DateAdded, LastModified=excluded.LastModified;
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
                // Cela ÃƒÂ©limine complÃƒÂ¨tement le dÃƒÂ©lai de copie du fichier en cache.
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

            // Si le streaming a ÃƒÂ©chouÃƒÂ© (ou si c'est un DASH), on fait un fallback : 
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

            // Si c'est un fichier "dash" (ex: Frostpunk), WMF a du mal mÃƒÂªme sans ID3. 
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
                    // Remux instantanÃƒÂ© au lieu d'un dÃƒÂ©codage lent en WAV
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

            // Fallback ultime FFmpeg si WMF ÃƒÂ©choue complÃƒÂ¨tement (dÃƒÂ©codage complet)
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

            throw new InvalidOperationException("Impossible de dÃƒÂ©coder ce fichier via WMF ou FFmpeg.");
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
                // (Permet de lire les M4A mÃƒÂªme s'ils ont l'extension .mp3)
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
                        var msg = Resona.Models.Strings.Current.IsFr ? $"Format audio non pris en charge ou illisible (le fichier est peut-être corrompu ou c'est un fichier WebM/Opus déguisé en MP3).\nFichier : {track.FilePath}" : $"Unsupported or unreadable audio format (the file might be corrupted or it's a WebM/Opus file disguised as MP3).\nFile : {track.FilePath}";
                        throw new NotSupportedException(msg, ex);
                    }
                }
            }

            if (sampleProvider == null) { var msg2 = Resona.Models.Strings.Current.IsFr ? $"Impossible de créer un lecteur.\nFichier : {track.FilePath}" : $"Cannot create audio reader.\nFile : {track.FilePath}"; throw new InvalidOperationException(msg2); }

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
                    throw new InvalidOperationException("Le pÃƒÂ©riphÃƒÂ©rique audio n'a pas pu ÃƒÂªtre initialisÃƒÂ©.", ex);
                }
            }

            if (output == null || _finalProvider == null)
            {
                Stop();
                throw new InvalidOperationException("Initialisation ÃƒÂ©chouÃƒÂ©e.");
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
                throw new InvalidOperationException("Le lecteur audio n'a pas pu dÃƒÂ©marrer.", ex);
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
        if (Math.Abs(targetVolume - startVolume) < 0.01f) { if (_volumeProvider != null) _volumeProvider.Volume = targetVolume; return; }

        _ = Task.Run(async () =>
        {
            const int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                if (_volumeProvider != null) _volumeProvider.Volume = startVolume + (targetVolume - startVolume) * t;
                await Task.Delay(10);
            }
            if (_volumeProvider != null) _volumeProvider.Volume = targetVolume;
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

                // Recherche manuelle dans les tags ID3v2 (cas frÃƒÂ©quent si ajoutÃƒÂ© par d'autres logiciels)
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

        if (!File.Exists(YtDlpPath))
        {
            await EnsureBinaryAsync(
                YtDlpPath,
                "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe",
                "yt-dlp.exe",
                onProgress);
        }

        if (!File.Exists(FfmpegPath))
            await EnsureFfmpegAsync(onProgress);
    }

    public static async Task EnsureFfmpegAsync(Action<string>? onProgress)
    {
        onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "TÃƒÂ©lÃƒÂ©chargement de ffmpeg..." : "Downloading ffmpeg...");
        const string zipUrl  = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        string tmpDir  = Path.Combine(Path.GetTempPath(), $"Resona_ffmpeg_{Guid.NewGuid():N}");
        string zipPath = Path.Combine(tmpDir, "ffmpeg.zip");
        try
        {
            Directory.CreateDirectory(tmpDir);

            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Resona");
            http.Timeout = TimeSpan.FromMinutes(10);
            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "TÃƒÂ©lÃƒÂ©chargement de ffmpeg (~160 Mo)..." : "Downloading ffmpeg (~160 MB)...");

            using (var httpStream = await http.GetStreamAsync(zipUrl))
            using (var fileStream  = File.Create(zipPath))
                await httpStream.CopyToAsync(fileStream);

            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "Extraction de ffmpeg.exe..." : "Extracting ffmpeg.exe...");
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
                onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "ffmpeg installÃƒÂ©." : "ffmpeg installed.");
            }
            else
            {
                onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "Ã¢ÂÅ’ ffmpeg.exe introuvable dans l'archive." : "Ã¢ÂÅ’ ffmpeg.exe not found in archive.");
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? $"Erreur ffmpeg : {ex.Message}" : $"ffmpeg error: {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    private static async Task EnsureBinaryAsync(string localPath, string url, string name, Action<string>? onProgress)
    {
        if (File.Exists(localPath)) return;
        onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? $"TÃƒÂ©lÃƒÂ©chargement de {name}..." : $"Downloading {name}...");
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Resona");
            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, bytes);
            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? $"{name} installÃƒÂ©." : $"{name} installed.");
        }
        catch (Exception ex)
        {
            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? $"Erreur lors du tÃƒÂ©lÃƒÂ©chargement de {name} : {ex.Message}" : $"Error downloading {name}: {ex.Message}");
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
            throw new FileNotFoundException("yt-dlp introuvable mÃƒÂªme aprÃƒÂ¨s tentative d'installation automatique.");

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
            throw new Exception(Resona.Models.Strings.Current.IsFr ? $"yt-dlp s'est termin\u00E9 avec le code {proc.ExitCode}." : $"yt-dlp exited with code {proc.ExitCode}.");
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

        return $"--extractor-args \"youtube:player_client=android\" " +
               $"-x --audio-format {formatExt} {bitrateArg} " +
               $"--embed-thumbnail --embed-metadata " +
               $"--parse-metadata \"%(upload_date>%Y)s:%(meta_date)s\" " +
               $"--no-mtime --output \"{output}\" " +
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
    private const string UserAgent = "Resona/2.2 (https://github.com/Resona)";

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
        // Step 1: Try AcoustID audio fingerprint first — it's never wrong, only absent.
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            var fpResult = await FingerprintLookupAsync(filePath);
            // Ne plus forcer Score=1.0 ici : FingerprintLookupAsync renvoie deja un score fiable,
            // proportionnel a la confiance reelle d'AcoustID (voir MinAcoustIdScore la-bas). Un match
            // fingerprint a faible confiance ne doit pas etre traite comme une certitude absolue.
            if (fpResult != null)
            {
                return fpResult;
            }
        }

        // Step 2: Fingerprint found nothing — fall back to text-based sources in parallel.
        string cleanedTitle = CleanTitle(!string.IsNullOrEmpty(filePath)
            ? System.IO.Path.GetFileNameWithoutExtension(filePath)
            : title);

        string searchArtist = (artist ?? "").Split(new[] { ',', ';', '/', '&', '|', '\\', '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => s.Length > 0) ?? "";

        if (searchArtist.Contains("inconnu", StringComparison.OrdinalIgnoreCase) || searchArtist.Contains("unknown", StringComparison.OrdinalIgnoreCase))
            searchArtist = "";

        // On ne concatene plus systematiquement l'artiste dans cleanedTitle : ca polluait le titre de
        // reference utilise ensuite par IsResultValid (le titre trouve, souvent court, etait compare a
        // un "titre" contenant en realite titre + artiste + parfois du bruit comme un nom de chaine TV,
        // ce qui faisait chuter le ratio de mots communs sous le seuil et rejetait des resultats corrects).
        // L'artiste reste utilise separement comme parametre de recherche pour les moteurs qui l'acceptent.
        string searchQueryTitle = !string.IsNullOrWhiteSpace(searchArtist) && !cleanedTitle.Contains(searchArtist, StringComparison.OrdinalIgnoreCase)
            ? $"{cleanedTitle} {searchArtist}"
            : cleanedTitle;

        var appleWebTask = AppleMusicWebLookupAsync(searchArtist, searchQueryTitle, duration);
        var itunesTask   = ItunesLookupAsync(searchArtist, searchQueryTitle, cleanedTitle, searchArtist, duration);
        var mbTask       = MusicBrainzLookupAsync(searchArtist, searchQueryTitle, duration);
        var deezerTask   = DeezerLookupAsync(searchArtist, searchQueryTitle, duration);

        System.Diagnostics.Debug.WriteLine($"AutoTag texte: titre original='{title}', titre nettoye='{cleanedTitle}', artiste recherche='{searchArtist}'");

        var tasks = new List<Task<AutoTagResult?>> { appleWebTask, itunesTask, mbTask, deezerTask };
        var timeoutTask = Task.Delay(10000);

        var candidates = new List<AutoTagResult>();
        while (tasks.Count > 0)
        {
            var finished = await Task.WhenAny(tasks.Concat(new Task[] { timeoutTask }));
            if (finished == timeoutTask) break;

            tasks.Remove((Task<AutoTagResult?>)finished);
            var res = ((Task<AutoTagResult?>)finished).Result;

            string sourceName = finished == appleWebTask ? "AppleMusicWeb" : finished == itunesTask ? "iTunes" : finished == mbTask ? "MusicBrainz" : "Deezer";
            if (res != null)
            {
                bool valid = IsResultValid(res, cleanedTitle, searchArtist);
                System.Diagnostics.Debug.WriteLine($"AutoTag texte [{sourceName}]: trouve '{res.Title}' - '{res.Artist}' (album='{res.Album}'), score={res.Score:F3}, valide={valid}");
                if (valid)
                {
                    candidates.Add(res);
                    if (res.Score >= 0.8) { System.Diagnostics.Debug.WriteLine($"AutoTag texte: acceptation immediate de [{sourceName}] (score >= 0.8)"); return res; }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"AutoTag texte [{sourceName}]: aucun resultat.");
            }
        }

        var best = candidates.OrderByDescending(c => c.Score).FirstOrDefault();
        if (best != null)
        {
            System.Diagnostics.Debug.WriteLine($"AutoTag texte: meilleur candidat retenu = '{best.Title}' - '{best.Artist}' (score={best.Score:F3})");
            return best;
        }

        // Step 3: Last chance fallback
        var allText = $"{cleanedTitle} {searchArtist}".Trim();
        var words = allText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var fallbackFull = await ItunesLookupAsync("", allText, cleanedTitle, searchArtist, duration);
            if (fallbackFull != null && IsResultValid(fallbackFull, cleanedTitle, searchArtist) && fallbackFull.Score >= 0.72) return fallbackFull;

            string keywordQuery = string.Join(" ", words.Take(3));
            var fallbackItunes = await ItunesLookupAsync("", keywordQuery, cleanedTitle, searchArtist, duration);
            if (fallbackItunes != null && IsResultValid(fallbackItunes, cleanedTitle, searchArtist) && fallbackItunes.Score >= 0.72) return fallbackItunes;
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

        // Retire les guillemets typographiques qui encadrent souvent le vrai titre dans les noms de
        // fichiers d'evenements (ex: Le magnifique "Nightcall" par ... -> on les neutralise en espaces
        // pour ne pas casser le mot lui-meme).
        s = System.Text.RegularExpressions.Regex.Replace(s, "[\u201C\u201D\u00AB\u00BB\uFF02\u201E\u2033\"]+", " ");

        // Remplacer les caracteres de parentheses/crochets/japonais par des espaces
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[\(\)\[\]\{\}]+", " ");

        // Enlever les mots-clÃƒÂ©s parasites (sans enlever cover, remix, etc. qui font partie du vrai titre)
        string[] keywords = { "official", "music video", "lyric", "lyrics", "audio", "visualizer", "remaster", "remastered", "edit", "clip", "hq", "hd", "4k", "1080p", "720p", "explicit", "clean", "ncs", "free download", "preview", "amv", "video", "music" };
        
        foreach (var r in keywords)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, $@"(?i)\b{r}\b", "");
        }

        // Retire les formules d'introduction et de contexte d'evenement frequentes sur les rips de
        // captations live/cerimonies (ex: "JO PARIS 2024 - Le magnifique Nightcall par X au Stade de
        // France"). Ces mots noient le vrai titre/artiste et faussent le matching texte plus loin.
        string[] eventPhrases = {
            @"\bJO\s*PARIS\s*20\d{2}\b", @"\bJEUX\s+OLYMPIQUES\b", @"\bLE\s+MAGNIFIQUE\b",
            @"\bAU\s+STADE\s+DE\s+FRANCE\b", @"\bCEREMONIE\s+D['\u2019]OUVERTURE\b",
            @"\bCEREMONIE\s+DE\s+CLOTURE\b", @"\bLIVE\s+PERFORMANCE\b"
        };
        foreach (var p in eventPhrases)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, p, " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // "par X, Y et Z" est une formule d'attribution d'artistes frequente sur les rips d'evenements ;
        // elle casse le matching car "par" et "et" polluent le sac de mots. On la neutralise en gardant
        // les noms mais en retirant les connecteurs.
        s = System.Text.RegularExpressions.Regex.Replace(s, @"(?i)\bpar\b", " ");

        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

        return s;
    }

    private static bool IsResultValid(AutoTagResult result, string originalTitle, string originalArtist)
    {
        if (string.IsNullOrWhiteSpace(result.Title)) return false;
        if (string.IsNullOrWhiteSpace(result.Artist)) return false;

        if (result.Artist.Contains("inconnu", StringComparison.OrdinalIgnoreCase) || result.Artist.Contains("unknown", StringComparison.OrdinalIgnoreCase)) return false;
        if (result.Title.Contains("inconnu", StringComparison.OrdinalIgnoreCase) || result.Title.Contains("unknown", StringComparison.OrdinalIgnoreCase)) return false;

        if (result.Title.Contains(" - "))
        {
            var parts = result.Title.Split(new[] { " - " }, 2, StringSplitOptions.None);
            if (parts[0].Trim().Equals(result.Artist, StringComparison.OrdinalIgnoreCase))
                return false; 
        }
        
        string origFull = $"{originalTitle} {originalArtist}".ToLowerInvariant();
        string resFull = $"{result.Title} {result.Artist}".ToLowerInvariant();
        
        var stopWords = new HashSet<string> { "the", "and", "for", "with", "from", "feat", "ft", "music", "audio", "official", "video", "lyrics", "clip" };
        var origWords = origFull.Split(new[] { ' ', '-', '(', ')', '[', ']', ',', '“', '”', '＂', '"' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => !stopWords.Contains(w)).ToList();
        
        if (origWords.Count > 0)
        {
            int matches = origWords.Count(w => resFull.Contains(w));
            double ratio = (double)matches / origWords.Count;
            if (ratio < 0.20) return false;
            
            var origTitleWords = originalTitle.ToLowerInvariant().Split(new[] { ' ', '-', '(', ')', '[', ']', ',', '“', '”', '＂', '"' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Where(w => !stopWords.Contains(w)).ToList();
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
        
        // Custom permissive subset check for "rosÃƒÂ© apt" -> "ROSÃƒÆ’Ã¢â‚¬Â° & Bruno Mars APT."
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
    private static readonly string FpcalcExe = Path.Combine(FpcalcDir, "chromaprint-fpcalc-1.5.1-windows-x86_64", "fpcalc.exe");
    private const string AcoustIdApiKey = "ZmBZcnuMkk";

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

            System.Diagnostics.Debug.WriteLine($"AcoustID: fpcalc OK pour '{filePath}' -- duration={duration}s, fingerprint length={fingerprint.Length} chars, fingerprint(head)={fingerprint.Substring(0, Math.Min(60, fingerprint.Length))}...");

            string acUrl = $"https://api.acoustid.org/v2/lookup?client={AcoustIdApiKey}" +
                           $"&meta=recordings+releasegroups+compress&duration={duration}" +
                           $"&fingerprint={Uri.EscapeDataString(fingerprint)}";
            string acRawJson;
            try
            {
                acRawJson = await _http.GetStringAsync(acUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AcoustID: requete HTTP echouee -- {ex.Message}");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"AcoustID: reponse brute (tronquee) = {acRawJson.Substring(0, Math.Min(1000, acRawJson.Length))}");

            var acResp = System.Text.Json.JsonSerializer.Deserialize<AcoustIdResponse>(acRawJson);
            if (acResp?.results == null || acResp.results.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("AcoustID: aucun resultat dans la reponse.");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"AcoustID: {acResp.results.Count} resultat(s) recu(s). Scores = [{string.Join(", ", acResp.results.Select(r => r.score?.ToString("F3") ?? "null"))}]");

            // Seuil minimum recommande par AcoustID pour considerer un match fiable : en dessous, le
            // fingerprint est trop peu similaire pour etre une identification sure (bruit, extrait court,
            // piste voisine dans l'album...). Sans ce filtre, on acceptait n'importe quel score et on
            // forcait Score=1.0 ensuite, ce qui faisait passer des matchs foireux pour des identifications
            // certaines.
            const double MinAcoustIdScore = 0.5;

            var bestResult = acResp.results
                .Where(r => (r.score ?? 0) >= MinAcoustIdScore && r.recordings?.Count > 0)
                .OrderByDescending(r => r.score ?? 0)
                .FirstOrDefault();
            if (bestResult?.recordings == null || bestResult.recordings.Count == 0) return null;

            // Parmi les recordings retournes pour ce fingerprint (souvent le meme enregistrement audio
            // reference plusieurs fois : single, album, reedition...), on ne peut pas deviner lequel est
            // le "bon" sans info supplementaire. On prend le plus riche en metadonnees (releasegroups)
            // comme avant, mais desormais seulement apres avoir garanti que le score global est fiable.
            var bestRecording = bestResult.recordings
                .OrderByDescending(r => r.releasegroups?.Count ?? 0)
                .FirstOrDefault();
            if (bestRecording == null) return null;

            // Le score final propage la vraie confiance AcoustID (remise a l'echelle 0.5-1.0 pour rester
            // coherent avec MinScoreThreshold/HighConfidenceThreshold utilises ailleurs dans le pipeline),
            // au lieu d'etre force a 1.0 : si jamais le resultat texte-matche mal ensuite, IsResultValid
            // garde une chance de le rejeter plutot que de le traiter comme une certitude absolue.
            double acoustIdConfidence = bestResult.score ?? MinAcoustIdScore;
            double propagatedScore = 0.5 + (Math.Clamp(acoustIdConfidence, MinAcoustIdScore, 1.0) - MinAcoustIdScore) / (1.0 - MinAcoustIdScore) * 0.5;

            if (!string.IsNullOrEmpty(bestRecording.id))
            {
                var mbResult = await MusicBrainzLookupByMbidAsync(bestRecording.id);
                if (mbResult != null)
                {
                    mbResult.Score = propagatedScore;
                    return mbResult;
                }
            }

            var artist = bestRecording.artists?.FirstOrDefault()?.name ?? "";
            var title = bestRecording.title ?? "";
            var album = bestRecording.releasegroups?.FirstOrDefault()?.title ?? "";

            return new AutoTagResult
            {
                Title = title, Artist = artist, Album = album, Score = propagatedScore
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
                Artist = recording.artistCredit?.FirstOrDefault()?.name ?? recording.artistCredit?.FirstOrDefault()?.artist?.name,
                Album = firstRelease?.title,
                Year = firstRelease?.date is string d && d.Length >= 4
                       && int.TryParse(d[..4], out var y) ? y : null,
                TrackCount = firstRelease?.trackCount,
                Genre = tag?.name,
                CoverPath = firstRelease?.id != null ? $"https://coverartarchive.org/release/{firstRelease.id}/front" : null,
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
        [System.Text.Json.Serialization.JsonPropertyName("artist-credit")]
        public List<ArtistCreditWrapper>? artistCredit { get; set; }
        public List<Release>? releases { get; set; }
        public List<Tag>? tags { get; set; }
        public int? length { get; set; }
    }

    private class ArtistCreditWrapper
    {
        public ArtistCredit? artist { get; set; }
        public string? name { get; set; }
    }

    private class ArtistCredit
    {
        public string? name { get; set; }
    }

    private class Release
    {
        public string? id { get; set; }
        public string? title { get; set; }
        public string? date { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("track-count")]
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







