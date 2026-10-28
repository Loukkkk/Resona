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
        if (_bitmapCache.Remove(path, out var removedNode)) _bitmapLru.Remove(removedNode);
    }

    // Un seul cache pour toute l'app (Bibliotheque, Albums, Artistes, Playlists, lecteur) : une seule image par
    // pochette, decodee a la plus grande taille deja demandee. ~400 entrees x 25-130 Ko = ~25 Mo au maximum.
    private const int MaxEntries = 400;
    private static readonly Dictionary<string, LinkedListNode<(string key, Microsoft.UI.Xaml.Media.Imaging.BitmapImage bmp, int width)>> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
    // Liste LRU : le plus recemment utilise en tete, on evince par la queue.
    private static readonly LinkedList<(string key, Microsoft.UI.Xaml.Media.Imaging.BitmapImage bmp, int width)> _bitmapLru = new();

    public static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? GetBitmap(string? path, int decodePixelWidth)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_bitmapCache.TryGetValue(path, out var cachedNode))
        {
            // Acces recent : on remonte l'entree en tete pour qu'elle soit evincee en dernier.
            _bitmapLru.Remove(cachedNode);
            _bitmapLru.AddFirst(cachedNode);
            // Une seule image par pochette : on garde la plus grande taille deja demandee. Si la taille
            // demandee est plus grande que celle en cache (ex. carte Album 180 px apres Artistes 80 px),
            // on redecode a la nouvelle taille pour ne pas afficher une image floue.
            if (cachedNode.Value.width >= decodePixelWidth) return cachedNode.Value.bmp;
            try
            {
                var bigger = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = decodePixelWidth };
                bigger.UriSource = new Uri(path);
                cachedNode.Value = (path, bigger, decodePixelWidth);
                return bigger;
            }
            catch { return cachedNode.Value.bmp; }
        }

        try
        {
            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = decodePixelWidth };
            bmp.UriSource = new Uri(path);

            // Eviction LRU : on retire les entrees les moins recemment utilisees.
            while (_bitmapCache.Count >= MaxEntries && _bitmapLru.Last != null)
            {
                var last = _bitmapLru.Last;
                _bitmapLru.RemoveLast();
                _bitmapCache.Remove(last.Value.key);
            }

            _bitmapCache[path] = _bitmapLru.AddFirst((path, bmp, decodePixelWidth));
            return bmp;
        }
        catch { return null; }
    }

    public static void Clear()
    {
        _existsCache.Clear();
        _bitmapCache.Clear();
        _bitmapLru.Clear();
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
            client.DefaultRequestHeaders.Add("User-Agent", "Resona/2.4 (https://github.com/Resona)");
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

    /// <summary>
    /// Clé de cache stable pour une pochette, basée sur artiste+album quand ils sont connus
    /// (identique entre deux scans, donc pas de doublons ni de fichiers orphelins accumulés
    /// sur le disque), avec repli sur trackId si artiste/album sont vides.
    /// </summary>
    public static string GetAlbumCacheKey(string? artist, string? album, string fallbackTrackId)
    {
        string a = (artist ?? "").Trim().ToLowerInvariant();
        string b = (album ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
        {
            return fallbackTrackId;
        }
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(a + "|" + b));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<string?> FindAndCacheCoverAsync(string trackId, string artist, string album, string title = "", string? filePath = null)
    {
        string cacheKey = GetAlbumCacheKey(artist, album, trackId);
        string cachePath = Path.Combine(_cacheDir, $"{cacheKey}.jpg");
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

    public async Task<string?> SaveEmbeddedCoverAsync(string cacheKey, byte[] imageBytes)
    {
        if (imageBytes.Length == 0) return null;
        string cachePath = Path.Combine(_cacheDir, $"{cacheKey}.jpg");
        // Si le fichier existe déjà (même clé album, ex. un autre track du même album
        // traité juste avant), on réutilise le fichier existant au lieu de le réécrire :
        // évite les accès disque inutiles et les doublons.
        if (System.IO.File.Exists(cachePath)) return cachePath;
        try { await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes); return cachePath; }
        catch { return null; }
    }

    

    } 

public class LyricsResult { public string? Source { get; set; } public bool TitleOnly { get; set; } public string? PlainLyrics { get; set; } public string? SyncedLyrics { get; set; } public bool Found => !string.IsNullOrWhiteSpace(PlainLyrics) || !string.IsNullOrWhiteSpace(SyncedLyrics); } 

/// <summary>Une fiche de paroles proposee dans le selecteur manuel.</summary>
public class LyricsCandidate
{
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double DurationSec { get; set; }
    public string? Synced { get; set; }
    public string? Plain { get; set; }
    public string Preview { get; set; } = string.Empty;
    public bool IsSynced => !string.IsNullOrWhiteSpace(Synced);
    public string Text => (IsSynced ? Synced : Plain) ?? string.Empty;
}

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

        [System.Text.Json.Serialization.JsonPropertyName("instrumental")]
        public bool? Instrumental { get; set; }
    } 

    public static bool HasActualLyrics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (IsCorruptedLyrics(text)) return false;
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var timeRegex = new System.Text.RegularExpressions.Regex(@"^\[\d{1,2}:\d{2}([.:]\d{1,3})?([+-]\d+)?\]");
        var metadataRegex = new System.Text.RegularExpressions.Regex(@"^\[[a-zA-Z]+:.*\]$");
        int actualLines = 0;

        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (string.IsNullOrEmpty(l)) continue;
            if (metadataRegex.IsMatch(l)) continue;

            string clean = timeRegex.Replace(l, "").Trim();
            if (string.IsNullOrEmpty(clean)) continue;

            var lower = clean.ToLowerInvariant();
            if (lower is "instrumental" or "[instrumental]" or "(instrumental)" or "pure music" or "no lyrics" or "paroles introuvables")
                continue;
            if (clean.Contains("作词") || clean.Contains("作曲") || clean.Contains("编曲") || clean.Contains("纯音乐") || clean.Contains("填词"))
                continue;
            if (lower.StartsWith("lyrics by") || lower.StartsWith("composed by") || lower.StartsWith("written by") || lower.StartsWith("arranged by") || lower.StartsWith("produced by"))
                continue;

            actualLines++;
        }

        return actualLines >= 2;
    }

    private static readonly System.Text.RegularExpressions.Regex MisattributedCoverRegex = new System.Text.RegularExpressions.Regex(
        @"(?i)[\(（\[【][^\)）\]】]*(?:原曲|원곡|original\s*song|original\s*track|original\s*by|original\s*:|cover\s*of)\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool HasCyrillic(string? text) => !string.IsNullOrWhiteSpace(text) && System.Text.RegularExpressions.Regex.IsMatch(text, @"[\p{IsCyrillic}]");
    private static bool HasCjk(string? text) => !string.IsNullOrWhiteSpace(text) && System.Text.RegularExpressions.Regex.IsMatch(text, @"[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}\p{IsHangulSyllables}]");

    private static double GetFirstTimestampSec(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc)) return 999.0;
        var lines = lrc.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var timeRegex = new System.Text.RegularExpressions.Regex(@"^\[(\d{1,2}):(\d{2}(?:[.:]\d{1,3})?)\]");
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var m = timeRegex.Match(line);
            if (m.Success)
            {
                string minStr = m.Groups[1].Value;
                string secStr = m.Groups[2].Value.Replace(':', '.');
                if (double.TryParse(minStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double min) &&
                    double.TryParse(secStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sec))
                {
                    string text = line.Substring(m.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(text) && !System.Text.RegularExpressions.Regex.IsMatch(text, @"^(by|lyrics|synced|paroles|titre|album)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return min * 60.0 + sec;
                    }
                }
            }
        }
        return 999.0;
    }

    private static int GetLineCount(string? lrc) => string.IsNullOrWhiteSpace(lrc) ? 0 : lrc.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static bool IsCorruptedLyrics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        // Détecte les textes corrompus par un mauvais encodage de caractères (séries de ???)
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\?{3,}")) return true;

        int questionMarks = text.Count(c => c == '?');
        int lettersOrDigits = text.Count(c => char.IsLetterOrDigit(c));
        if (lettersOrDigits > 10 && questionMarks > 6 && (double)questionMarks / (lettersOrDigits + questionMarks) > 0.15)
        {
            return true;
        }
        return false;
    }

    private static string? ExtractBracketedTitle(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(input, @"[『「【《〈\[\(](.*?)[』」】》〉\]\)]");
        if (m.Success)
        {
            string candidate = m.Groups[1].Value.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(candidate, @"(?i)^(official|video|audio|lyric|theme song|ost|hd|mv|hq|visualizer|album version|remastered)"))
            {
                if (candidate.Length >= 2) return candidate;
            }
        }
        return null;
    }

    private string CleanMetadata(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string output = input;

        // 1. Balises de métadonnées entre parenthèses et crochets occidentaux ou asiatiques
        var pattern = @"(?i)[\(\[（【『「《〔〈][^\)\]）】』」》〕〉]*(official|video|audio|lyric|visualizer|live|remaster|edit|ft\.|feat\.|featuring|version|mv|vocal|theme song|opening|ending|ost|soundtrack|utattemita|歌ってみた)[^\)\]）】』」》〕〉]*[\)\]）】』」》〕〉]";
        output = System.Text.RegularExpressions.Regex.Replace(output, pattern, "");

        // 2. Mentions de types vidéo/audio/thème
        var pattern2 = @"(?i)\b(official video|official music video|official audio|lyrics|lyric video|audio|visualizer|official|theme song|opening theme|ending theme|opening song|ending song|insert song|character song|full version|full song|original soundtrack|soundtrack|ost|utattemita|歌ってみた)\b";
        output = System.Text.RegularExpressions.Regex.Replace(output, pattern2, "");

        // 3. Suffixes après tiret
        var pattern3 = @"(?i)\s*-\s*(remaster(ed)?(\s*\d{4})?|live(\s+at.*)?|radio edit|single version|album version|bonus track|acoustic|deluxe).*";
        output = System.Text.RegularExpressions.Regex.Replace(output, pattern3, "");

        // 4. Numérotation de piste
        var pattern4 = @"^\d{1,3}\s*[-.]\s*";
        output = System.Text.RegularExpressions.Regex.Replace(output, pattern4, "");

        // 5. Retirer les crochets isolés sans supprimer leur contenu
        output = output.Replace("『", " ").Replace("』", " ")
                       .Replace("「", " ").Replace("」", " ")
                       .Replace("【", " ").Replace("】", " ")
                       .Replace("《", " ").Replace("》", " ")
                       .Replace("〈", " ").Replace("〉", " ");

        output = System.Text.RegularExpressions.Regex.Replace(output, @"\s+", " ");
        return output.Trim(' ', '-', '_');
    }

    // ------------------------------------------------------------------
    //  Nettoyage et validation globale des paroles (toutes sources confondues)
    // ------------------------------------------------------------------

    private static readonly System.Text.RegularExpressions.Regex LrcTimePrefixRegex = new System.Text.RegularExpressions.Regex(
        @"^(\[\d{1,3}:\d{2}([.:]\d{1,3})?([+-]\d+)?\])+", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex LrcMetaLineRegex = new System.Text.RegularExpressions.Regex(
        @"^\[[a-zA-Z]+:.*\]$", System.Text.RegularExpressions.RegexOptions.Compiled);

    // "Artiste - Titre (Romanized) Lyrics", "... Paroles", "... 歌詞" : titre de page recopie par certaines sources.
    private static readonly System.Text.RegularExpressions.Regex HeaderSuffixRegex = new System.Text.RegularExpressions.Regex(
        @"(?i)(?:\s(?:lyrics|paroles|letra|testo|songtext)(?:\s*[\(\[][^\)\]]*[\)\]])?|\s?(?:歌词|歌詞))\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex BoilerplateStartRegex = new System.Text.RegularExpressions.Regex(
        @"(?i)^(?:paroles\s+de\s+la\s+chanson\b|lyrics\s+(?:for|of)\b|\d+\s*contributors?\b|translations?$|you might also like$|(?:lyrics|paroles)$)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex BoilerplateEndRegex = new System.Text.RegularExpressions.Regex(
        @"(?i)^(?:\d*embed|you might also like|lyrics\s+(?:provided|powered)\s+by\b.*|paroles\s+fournies\s+par\b.*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex InstrumentalTitleRegex = new System.Text.RegularExpressions.Regex(
        @"(?i)\b(?:instrumental|instrumentale)\b|\bno vocals?\b|\bsans paroles\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Le titre annonce explicitement un morceau sans chant ("(Instrumental)"...) : inutile de chercher des paroles.</summary>
    public static bool LooksInstrumental(string? title) => !string.IsNullOrWhiteSpace(title) && InstrumentalTitleRegex.IsMatch(title);

    private static bool ContainsNorm(string haystackNorm, string needleNorm) => needleNorm.Length >= 2 && haystackNorm.Contains(needleNorm);

    private static bool IsHeaderLine(string payload, string artistNorm, string titleNorm)
    {
        string p = payload.Trim();
        if (p.Length == 0 || p.Length > 140) return false;
        if (BoilerplateStartRegex.IsMatch(p)) return true;

        string pn = NormalizeForMatching(p);
        bool hasTitle = ContainsNorm(pn, titleNorm);
        bool hasArtist = ContainsNorm(pn, artistNorm);
        bool hasDash = p.Contains(" - ") || p.Contains(" – ") || p.Contains(" — ");

        // "Ado - Show (Romanized) Lyrics"
        if (HeaderSuffixRegex.IsMatch(p) && (hasDash || hasTitle)) return true;
        // Ligne qui ne contient que "Artiste - Titre"
        if (hasTitle && hasArtist && hasDash && pn.Length <= artistNorm.Length + titleNorm.Length + 12) return true;
        return false;
    }

    /// <summary>
    /// Retire le "bruit" que certaines sources ajoutent autour des paroles : titre de page en tete
    /// ("Artiste - Titre Lyrics"), "Paroles de la chanson ...", "N Contributors", et "123Embed" en fin de texte.
    /// Fonctionne sur du texte simple comme sur du LRC synchronise.
    /// </summary>
    public string SanitizeLyricsText(string? lyrics, string? artist, string? title)
    {
        if (string.IsNullOrWhiteSpace(lyrics)) return lyrics ?? string.Empty;
        string artistNorm = NormalizeForMatching(CleanMetadata(artist ?? string.Empty));
        string titleNorm = NormalizeForMatching(CleanMetadata(title ?? string.Empty));
        var lines = lyrics.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

        // En-tete : on n'inspecte que les premieres lignes reelles.
        int inspected = 0;
        int i = 0;
        while (i < lines.Count && inspected < 6)
        {
            string raw = lines[i].Trim();
            if (raw.Length == 0 || LrcMetaLineRegex.IsMatch(raw)) { i++; continue; }
            string payload = LrcTimePrefixRegex.Replace(raw, "").Trim();
            inspected++;
            if (payload.Length > 0 && IsHeaderLine(payload, artistNorm, titleNorm))
            {
                lines.RemoveAt(i);
                continue;
            }
            i++;
        }

        // Pied de page.
        int guard = 0;
        while (lines.Count > 0 && guard++ < 4)
        {
            string rawLast = lines[lines.Count - 1].Trim();
            string last = LrcTimePrefixRegex.Replace(rawLast, "").Trim();
            if (rawLast.Length == 0 || BoilerplateEndRegex.IsMatch(last))
            {
                lines.RemoveAt(lines.Count - 1);
                continue;
            }
            break;
        }

        return string.Join("\n", lines).Trim();
    }

    private static double GetLastTimestampSec(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc)) return 0;
        double max = 0;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(lrc, @"\[(\d{1,3}):(\d{2}(?:[.:]\d{1,3})?)\]"))
        {
            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double min) &&
                double.TryParse(m.Groups[2].Value.Replace(':', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sec))
            {
                max = Math.Max(max, min * 60.0 + sec);
            }
        }
        return max;
    }

    /// <summary>
    /// Controles qui ne dependent d'aucun reseau : assez de contenu pour etre de vraies paroles, et des horodatages
    /// qui ne depassent pas la duree du morceau (sinon ce sont les paroles d'une autre version / d'un autre morceau).
    /// </summary>
    public static bool IsPlausibleForTrack(string? text, bool isSynced, TimeSpan? duration)
    {
        if (!HasActualLyrics(text)) return false;

        int lineCount = 0, charCount = 0;
        foreach (var raw in text!.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string l = raw.Trim();
            if (LrcMetaLineRegex.IsMatch(l)) continue;
            string clean = LrcTimePrefixRegex.Replace(l, "").Trim();
            if (clean.Length == 0) continue;
            lineCount++;
            charCount += clean.Length;
        }
        if (lineCount < 3 || charCount < 40) return false;

        if (isSynced && duration.HasValue && duration.Value.TotalSeconds > 30.0)
        {
            if (GetLastTimestampSec(text) > duration.Value.TotalSeconds + 15.0) return false;
        }
        return true;
    }

    // Un titre trop court / trop generique ("Awaken", "Rain", "Tide") correspond a des dizaines de chansons differentes.
    private static bool IsDistinctiveTitle(string title)
    {
        string n = NormalizeForMatching(title);
        if (n.Length < 7) return false;
        return n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length >= 2 || n.Length >= 9;
    }

    private static readonly HashSet<string> NoiseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "remaster", "remastered", "live", "edit", "radio", "version", "mix",
        "deluxe", "mono", "stereo", "acoustic", "instrumental", "explicit",
        "clean", "cut", "take", "session", "bonus", "track", "remix", "extended", "original"
    };

    private static string NormalizeForMatching(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string formD = input.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char ch in formD)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static string StripThe(string s)
    {
        if (s.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
            return s.Substring(4).Trim();
        return s;
    }

    private static List<string> SplitArtists(string s)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(s, @"\s*(?:feat\.?|featuring|ft\.?|&|and|\/|,|;|x|vs\.?)\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var list = new List<string>();
        foreach (var p in parts)
        {
            string n = StripThe(NormalizeForMatching(p));
            if (!string.IsNullOrWhiteSpace(n)) list.Add(n);
        }
        return list;
    }

    private static bool IsAllNoiseOrYear(IEnumerable<string> words)
    {
        foreach (var w in words)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(w, @"^\d{4}$")) continue;
            if (NoiseWords.Contains(w)) continue;
            return false;
        }
        return true;
    }

    private bool IsArtistMatch(string? qArtist, string? qTitle, string? rArtist)
    {
        var qa = NormalizeForMatching(CleanMetadata(qArtist));
        var ra = NormalizeForMatching(CleanMetadata(rArtist));
        if (string.IsNullOrEmpty(qa) && string.IsNullOrEmpty(ra)) return true;
        if (!string.IsNullOrEmpty(qa) && string.IsNullOrEmpty(ra))
        {
            var qt = NormalizeForMatching(CleanMetadata(qTitle));
            return qt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length >= 2;
        }
        if (string.IsNullOrEmpty(qa) && !string.IsNullOrEmpty(ra)) return true;
        if (qa == ra) return true;
        if (StripThe(qa) == StripThe(ra)) return true;

        var qList = SplitArtists(qa);
        var rList = SplitArtists(ra);
        if (qList.Count > 0 && rList.Count > 0)
        {
            if (qList[0] == rList[0]) return true;
            if (qList.Any(q => rList.Contains(q))) return true;
        }
        return false;
    }

    private bool IsTitleMatch(string? qArtist, string? qTitle, string? rTitle)
    {
        var qt = NormalizeForMatching(CleanMetadata(qTitle));
        var rt = NormalizeForMatching(CleanMetadata(rTitle));
        if (string.IsNullOrEmpty(qt) || string.IsNullOrEmpty(rt)) return false;
        if (qt == rt) return true;

        var qa = NormalizeForMatching(CleanMetadata(qArtist));
        if (!string.IsNullOrEmpty(qa))
        {
            qa = StripThe(qa);
            if (rt.StartsWith(qa + " ", StringComparison.OrdinalIgnoreCase))
            {
                var stripped = rt.Substring(qa.Length + 1).Trim();
                if (stripped == qt) return true;
                rt = stripped;
            }
        }

        var qWords = qt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var rWords = rt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (qWords.Length == 1)
        {
            if (rWords.Length > 1 && string.Equals(rWords[0], qWords[0], StringComparison.OrdinalIgnoreCase))
            {
                return IsAllNoiseOrYear(rWords.Skip(1));
            }
            return qt == rt;
        }

        if (rt.StartsWith(qt + " ", StringComparison.OrdinalIgnoreCase))
        {
            return IsAllNoiseOrYear(rWords.Skip(qWords.Length));
        }

        if (qt.StartsWith(rt + " ", StringComparison.OrdinalIgnoreCase))
        {
            return IsAllNoiseOrYear(qWords.Skip(rWords.Length));
        }

        return false;
    }

    private bool IsMatch(string? qArtist, string? qTitle, string? rArtist, string? rTitle, double? qDur = null, double? rDur = null, bool strict = false)
    {
        // Mode strict (recherche par titre seul) : la duree doit etre connue des deux cotes et quasi identique.
        if (strict)
        {
            if (!qDur.HasValue || qDur.Value <= 10.0 || !rDur.HasValue || rDur.Value <= 10.0) return false;
            if (Math.Abs(qDur.Value - rDur.Value) > 5.0) return false;
        }
        if (qDur.HasValue && qDur.Value > 10.0 && rDur.HasValue && rDur.Value > 10.0)
        {
            // Tolérance adaptative allant jusqu'à 45s pour supporter les vidéos YouTube avec cartes de fin / génériques
            double maxDelta = Math.Max(10.0, Math.Min(45.0, qDur.Value * 0.15));
            if (Math.Abs(qDur.Value - rDur.Value) > maxDelta) return false;
        }
        return IsTitleMatch(qArtist, qTitle, rTitle) && IsArtistMatch(qArtist, qTitle, rArtist);
    }

    private static string ExtractPlainLyricsFromLrc(string lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc)) return string.Empty;
        var lines = lrc.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new System.Text.StringBuilder();
        var timeRegex = new System.Text.RegularExpressions.Regex(@"^(\[\d{1,2}:\d{2}([.:]\d{1,3})?([+-]\d+)?\])+");
        var metadataRegex = new System.Text.RegularExpressions.Regex(@"^\[[a-zA-Z]+:.*\]$");
        var creditRegex = new System.Text.RegularExpressions.Regex(
            @"^(作词|作曲|编曲|制作|监制|混音|母带|吉他|贝斯|鼓|键盘|和声|录音|发行|企划|统筹|文案|OP|SP|T_T|BY)\s*[:：]|^(作词|作曲|编曲|制作人|written by|composed by|lyrics by|produced by)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        TimeSpan maxTime = TimeSpan.Zero;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (metadataRegex.IsMatch(line)) continue;

            var timeMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]");
            if (timeMatch.Success)
            {
                if (int.TryParse(timeMatch.Groups[1].Value, out int min) && int.TryParse(timeMatch.Groups[2].Value, out int sec))
                {
                    var ts = TimeSpan.FromSeconds(min * 60 + sec);
                    if (maxTime > TimeSpan.FromSeconds(20.0) && ts < maxTime - TimeSpan.FromSeconds(15.0))
                    {
                        // Saut temporel en arrière : flux secondaire de traduction bilingue en fin de fichier
                        break;
                    }
                    if (ts > maxTime) maxTime = ts;
                    if (ts.TotalSeconds < 8.0 && creditRegex.IsMatch(timeRegex.Replace(line, "").Trim()))
                    {
                        continue;
                    }
                }
            }

            string cleaned = timeRegex.Replace(line, "").Trim();
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                sb.AppendLine(cleaned);
            }
        }
        return sb.ToString().Trim();
    }

    private async Task<(LyricsResult? result, bool isInstrumental)> TryLrclibGetAsync(string artist, string title, string? album, TimeSpan? duration)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4.5));
            string url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}";
            if (!string.IsNullOrWhiteSpace(album))
                url += $"&album_name={Uri.EscapeDataString(album)}";
            if (duration.HasValue && duration.Value.TotalSeconds > 0)
                url += $"&duration={Math.Round(duration.Value.TotalSeconds)}";

            HttpResponseMessage? resp = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.TryParseAdd("Resona/4.0 (https://github.com/Resona)");
                resp = await _http.SendAsync(req, cts.Token);
                if (resp.IsSuccessStatusCode) break;
                if ((int)resp.StatusCode == 503 || (int)resp.StatusCode == 429)
                {
                    await Task.Delay(350, cts.Token);
                    continue;
                }
                break;
            }

            if (resp != null && resp.IsSuccessStatusCode)
            {
                var r = await resp.Content.ReadFromJsonAsync<LrcLibResponse>(cancellationToken: cts.Token);
                if (r != null)
                {
                    if (r.Instrumental == true)
                    {
                        return (null, true);
                    }

                    double? targetSec = (duration.HasValue && duration.Value.TotalSeconds > 10.0) ? duration.Value.TotalSeconds : null;
                    if (!IsMatch(artist, title, r.ArtistName, r.TrackName, targetSec, r.Duration))
                    {
                        return (null, false);
                    }

                    string? candidateText = r.SyncedLyrics ?? r.PlainLyrics;
                    if (!string.IsNullOrWhiteSpace(candidateText) && !IsCorruptedLyrics(candidateText) && HasActualLyrics(candidateText))
                    {
                        return (new LyricsResult { Source = $"lrclib/get [{r.ArtistName} - {r.TrackName}, {r.Duration:0}s]", PlainLyrics = r.PlainLyrics, SyncedLyrics = r.SyncedLyrics }, false);
                    }
                }
            }
        }
        catch { }
        return (null, false);
    }

    private async Task<(LyricsResult? result, bool isInstrumental)> TryLrclibSearchAsync(string artist, string title, TimeSpan? duration, bool strict = false)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5.0));
            string query = string.IsNullOrWhiteSpace(artist)
                ? $"https://lrclib.net/api/search?q={Uri.EscapeDataString(title)}"
                : $"https://lrclib.net/api/search?q={Uri.EscapeDataString(artist + " " + title)}";

            HttpResponseMessage? resp = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, query);
                req.Headers.UserAgent.TryParseAdd("Resona/4.0 (https://github.com/Resona)");
                resp = await _http.SendAsync(req, cts.Token);
                if (resp.IsSuccessStatusCode) break;
                if ((int)resp.StatusCode == 503 || (int)resp.StatusCode == 429)
                {
                    await Task.Delay(350, cts.Token);
                    continue;
                }
                break;
            }

            if (resp != null && resp.IsSuccessStatusCode)
            {
                var results = await resp.Content.ReadFromJsonAsync<List<LrcLibResponse>>(cancellationToken: cts.Token);
                if (results != null && results.Count > 0)
                {
                    double? targetSec = (duration.HasValue && duration.Value.TotalSeconds > 10.0) ? duration.Value.TotalSeconds : null;
                    bool queryHasCyrillic = HasCyrillic(artist) || HasCyrillic(title);
                    bool queryHasCjk = HasCjk(artist) || HasCjk(title);

                    var matched = results
                        .Where(r => IsMatch(artist, title, r.ArtistName, r.TrackName, targetSec, r.Duration, strict))
                        .Where(r => !IsCorruptedLyrics(r.SyncedLyrics ?? r.PlainLyrics))
                        .Where(r => {
                            if (r.TrackName != null && MisattributedCoverRegex.IsMatch(r.TrackName))
                            {
                                return false;
                            }
                            string? cand = r.SyncedLyrics ?? r.PlainLyrics;
                            if (!queryHasCyrillic && HasCyrillic(cand))
                            {
                                return false;
                            }
                            return true;
                        })
                        .ToList();

                    if (matched.Count > 0)
                    {
                        string normTitle = NormalizeForMatching(CleanMetadata(title));
                        matched = matched
                            .OrderByDescending(r => !string.IsNullOrWhiteSpace(r.SyncedLyrics))
                            .ThenByDescending(r => NormalizeForMatching(CleanMetadata(r.TrackName)) == normTitle)
                            .ThenByDescending(r => queryHasCjk ? HasCjk(r.SyncedLyrics ?? r.PlainLyrics) : false)
                            .ThenBy(r => targetSec.HasValue && r.Duration.HasValue ? (Math.Abs(r.Duration.Value - targetSec.Value) <= 5.0 ? 0 : 1) : 0)
                            .ThenByDescending(r => !string.IsNullOrWhiteSpace(r.SyncedLyrics) && GetFirstTimestampSec(r.SyncedLyrics) <= 12.0)
                            .ThenByDescending(r => GetLineCount(r.SyncedLyrics ?? r.PlainLyrics))
                            .ThenBy(r => targetSec.HasValue && r.Duration.HasValue ? Math.Abs(r.Duration.Value - targetSec.Value) : 999.0)
                            .ToList();

                        var best = matched[0];
                        if (best.Instrumental == true)
                        {
                            return (null, true);
                        }

                        string? candidateText = best.SyncedLyrics ?? best.PlainLyrics;
                        if (!string.IsNullOrWhiteSpace(candidateText) && HasActualLyrics(candidateText))
                        {
                            return (new LyricsResult { Source = $"lrclib/search [{best.ArtistName} - {best.TrackName}, {best.Duration:0}s]", PlainLyrics = best.PlainLyrics, SyncedLyrics = best.SyncedLyrics }, false);
                        }
                    }
                }
            }
        }
        catch { }
        return (null, false);
    }

    private async Task<LyricsResult?> TryLyricsOvhAsync(string artist, string title, TimeSpan? duration = null, bool strict = false)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4.0));

            // 1. Appel direct par artist/title si les deux sont fournis
            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
            {
                string directUrl = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";
                using var directReq = new HttpRequestMessage(HttpMethod.Get, directUrl);
                directReq.Headers.UserAgent.TryParseAdd("Resona/4.0 (https://github.com/Resona)");
                using var directResp = await _http.SendAsync(directReq, cts.Token);

                if (directResp.IsSuccessStatusCode)
                {
                    using var stream = await directResp.Content.ReadAsStreamAsync(cts.Token);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
                    if (doc.RootElement.TryGetProperty("lyrics", out var lyrElem))
                    {
                        string? lyrics = lyrElem.GetString();
                        if (!string.IsNullOrWhiteSpace(lyrics) && !IsCorruptedLyrics(lyrics) && HasActualLyrics(lyrics))
                        {
                            bool isSynced = LocalLyricsService.LooksLikeLrc(lyrics);
                            return new LyricsResult
                            {
                                Source = "lyrics.ovh/direct",
                                PlainLyrics = isSynced ? ExtractPlainLyricsFromLrc(lyrics) : lyrics.Trim(),
                                SyncedLyrics = isSynced ? lyrics : null
                            };
                        }
                    }
                }
            }

            // 2. Si non trouvé directement, recherche de suggestion Deezer (/suggest/)
            string suggestQuery = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
            string suggestUrl = $"https://api.lyrics.ovh/suggest/{Uri.EscapeDataString(suggestQuery)}";
            using var sugReq = new HttpRequestMessage(HttpMethod.Get, suggestUrl);
            sugReq.Headers.UserAgent.TryParseAdd("Resona/4.0 (https://github.com/Resona)");
            using var sugResp = await _http.SendAsync(sugReq, cts.Token);

            if (sugResp.IsSuccessStatusCode)
            {
                using var sugStream = await sugResp.Content.ReadAsStreamAsync(cts.Token);
                using var sugDoc = await JsonDocument.ParseAsync(sugStream, cancellationToken: cts.Token);

                if (sugDoc.RootElement.TryGetProperty("data", out var dataArr) &&
                    dataArr.ValueKind == JsonValueKind.Array)
                {
                    double? targetSec = (duration.HasValue && duration.Value.TotalSeconds > 10.0) ? duration.Value.TotalSeconds : null;

                    foreach (var item in dataArr.EnumerateArray())
                    {
                        string sTitle = item.TryGetProperty("title", out var tElem) ? tElem.GetString() ?? "" : "";
                        string sArtist = "";
                        if (item.TryGetProperty("artist", out var aObj) && aObj.TryGetProperty("name", out var aName))
                        {
                            sArtist = aName.GetString() ?? "";
                        }

                        double? sDuration = null;
                        if (item.TryGetProperty("duration", out var durElem) && durElem.TryGetDouble(out double dVal))
                        {
                            sDuration = dVal;
                        }

                        if (IsMatch(artist, title, sArtist, sTitle, targetSec, sDuration, strict))
                        {
                            string matchUrl = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(sArtist)}/{Uri.EscapeDataString(sTitle)}";
                            using var matchReq = new HttpRequestMessage(HttpMethod.Get, matchUrl);
                            matchReq.Headers.UserAgent.TryParseAdd("Resona/4.0 (https://github.com/Resona)");
                            using var matchResp = await _http.SendAsync(matchReq, cts.Token);
                            if (matchResp.IsSuccessStatusCode)
                            {
                                using var matchStream = await matchResp.Content.ReadAsStreamAsync(cts.Token);
                                using var matchDoc = await JsonDocument.ParseAsync(matchStream, cancellationToken: cts.Token);
                                if (matchDoc.RootElement.TryGetProperty("lyrics", out var mElem))
                                {
                                    string? mLyrics = mElem.GetString();
                                    if (!string.IsNullOrWhiteSpace(mLyrics) && !IsCorruptedLyrics(mLyrics) && HasActualLyrics(mLyrics))
                                    {
                                        bool isSynced = LocalLyricsService.LooksLikeLrc(mLyrics);
                                        return new LyricsResult
                                        {
                                            Source = $"lyrics.ovh/suggest [{sArtist} - {sTitle}]",
                                            PlainLyrics = isSynced ? ExtractPlainLyricsFromLrc(mLyrics) : mLyrics.Trim(),
                                            SyncedLyrics = isSynced ? mLyrics : null
                                        };
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private async Task<LyricsResult> ExecuteSearchCascadeAsync(string artist, string title, string? album, TimeSpan? duration, bool titleOnly = false, bool strict = false)
    {
        LyricsResult? bestPlainFallback = null;

        // 1. LRCLIB Exact
        try
        {
            // En mode "titre seul" (artiste inconnu), l'appel exact par artiste n'a pas de sens.
            var (exactRes, exactInst) = titleOnly ? ((LyricsResult?)null, false) : await TryLrclibGetAsync(artist, title, album, duration);
            if (exactInst) return new LyricsResult();
            if (exactRes != null && exactRes.Found)
            {
                if (!string.IsNullOrWhiteSpace(exactRes.SyncedLyrics))
                {
                    // Si exactRes commence après 15s sur un morceau de plus de 60s, l'intro peut manquer (ex: Show d'Ado).
                    // On le conserve comme fallback et on laisse la recherche explorer les versions complètes dès 00:00.
                    if (duration.HasValue && duration.Value.TotalSeconds > 60.0 && GetFirstTimestampSec(exactRes.SyncedLyrics) > 15.0)
                    {
                        bestPlainFallback ??= exactRes;
                    }
                    else
                    {
                        return exactRes;
                    }
                }
                else
                {
                    bestPlainFallback ??= exactRes;
                }
            }
        }
        catch { }

        // 2. LRCLIB Search
        try
        {
            var (searchRes, searchInst) = await TryLrclibSearchAsync(artist, title, duration, strict);
            if (searchInst) return new LyricsResult();
            if (searchRes != null && searchRes.Found)
            {
                if (!string.IsNullOrWhiteSpace(searchRes.SyncedLyrics)) return searchRes;
                bestPlainFallback ??= searchRes;
            }
        }
        catch { }

        // 3. Lyrics.ovh (Deezer)
        try
        {
            var ovhResult = await TryLyricsOvhAsync(artist, title, duration, strict);
            if (ovhResult != null && ovhResult.Found)
            {
                if (!string.IsNullOrWhiteSpace(ovhResult.SyncedLyrics)) return ovhResult;
                bestPlainFallback ??= ovhResult;
            }
        }
        catch { }

        if (bestPlainFallback != null && bestPlainFallback.Found) return bestPlainFallback;
        return new LyricsResult();
    }

    /// <summary>
    /// Recherche libre sur LRCLIB pour le selecteur manuel : aucun filtre d'artiste ni de titre (c'est l'utilisateur qui
    /// choisit, par exemple les paroles de l'original d'une reprise). Les fiches dont la duree est la plus proche de celle
    /// du morceau passent en premier, puis les paroles synchronisees avant le texte simple.
    /// </summary>
    public async Task<List<LyricsCandidate>> SearchCandidatesAsync(string query, TimeSpan? duration)
    {
        var list = new List<LyricsCandidate>();
        if (string.IsNullOrWhiteSpace(query)) return list;
        double? target = (duration.HasValue && duration.Value.TotalSeconds > 10.0) ? duration.Value.TotalSeconds : null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query.Trim())}");
            req.Headers.UserAgent.TryParseAdd("Resona/4.0 (https://github.com/Resona)");
            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                LogService.Write("Lyrics", $"Selecteur : LRCLIB a repondu {(int)resp.StatusCode} pour \"{query}\"");
                return list;
            }
            var results = await resp.Content.ReadFromJsonAsync<List<LrcLibResponse>>(cancellationToken: cts.Token);
            if (results == null) return list;

            var seen = new HashSet<string>();
            foreach (var r in results)
            {
                if (r.Instrumental == true) continue;
                string? raw = !string.IsNullOrWhiteSpace(r.SyncedLyrics) ? r.SyncedLyrics : r.PlainLyrics;
                if (string.IsNullOrWhiteSpace(raw) || IsCorruptedLyrics(raw)) continue;

                string artist = r.ArtistName ?? string.Empty;
                string title = r.TrackName ?? string.Empty;
                string? synced = string.IsNullOrWhiteSpace(r.SyncedLyrics) ? null : SanitizeLyricsText(r.SyncedLyrics, artist, title);
                string? plain = string.IsNullOrWhiteSpace(r.PlainLyrics) ? null : SanitizeLyricsText(r.PlainLyrics, artist, title);
                string shown = synced ?? plain ?? string.Empty;
                if (!HasActualLyrics(shown)) continue;
                if (!seen.Add($"{artist}|{title}|{Math.Round(r.Duration ?? 0.0)}|{shown.Length}")) continue;

                string preview = string.Join(" / ", shown.Split('\n')
                    .Select(l => LrcTimePrefixRegex.Replace(l.Trim(), "").Trim())
                    .Where(l => l.Length > 0 && !LrcMetaLineRegex.IsMatch(l))
                    .Take(2));
                if (preview.Length > 120) preview = preview.Substring(0, 120) + "...";

                list.Add(new LyricsCandidate
                {
                    Artist = artist,
                    Title = title,
                    DurationSec = r.Duration ?? 0.0,
                    Synced = synced,
                    Plain = plain,
                    Preview = preview
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Write("Lyrics", $"Selecteur : recherche impossible ({ex.Message})");
        }

        return list
            .OrderBy(c => (target.HasValue && c.DurationSec > 0.0) ? Math.Abs(c.DurationSec - target.Value) : 1e6)
            .ThenByDescending(c => c.IsSynced)
            .Take(30)
            .ToList();
    }

    public async Task<LyricsResult> SearchAsync(string artist, string title, string? album = null, TimeSpan? duration = null)
    {
        if (LooksInstrumental(title))
        {
            return new LyricsResult();
        }

        LyricsResult res = await SearchCoreAsync(artist, title, album, duration);
        if (!res.Found)
        {
            return res;
        }

        // Nettoyage commun a toutes les sources (titre de page recopie, "Embed"...) puis controle de coherence avec le morceau.
        string? synced = string.IsNullOrWhiteSpace(res.SyncedLyrics) ? null : SanitizeLyricsText(res.SyncedLyrics, artist, title);
        string? plain = string.IsNullOrWhiteSpace(res.PlainLyrics) ? null : SanitizeLyricsText(res.PlainLyrics, artist, title);
        if (synced != null && !IsPlausibleForTrack(synced, true, duration)) synced = null;
        if (plain != null && !IsPlausibleForTrack(plain, false, duration)) plain = null;

        if (synced == null && plain == null)
        {
            return new LyricsResult();
        }

        return new LyricsResult { Source = res.Source, TitleOnly = res.TitleOnly, SyncedLyrics = synced, PlainLyrics = plain };
    }

    private async Task<LyricsResult> SearchCoreAsync(string artist, string title, string? album = null, TimeSpan? duration = null)
    {
        string cArtist = CleanMetadata(artist);
        string cTitle = CleanMetadata(title);

        if (string.IsNullOrWhiteSpace(cTitle))
            return new LyricsResult();

        // 1. Recherche principale avec métadonnées nettoyées
        var res = await ExecuteSearchCascadeAsync(cArtist, cTitle, album, duration);
        if (res.Found) return res;

        // 2. Fallback 1: Si cArtist contient plusieurs artistes (ex: "2WEI, Kataem & Marvin Brooks"), chercher avec l'artiste principal
        var artistParts = SplitArtists(cArtist);
        if (artistParts.Count > 1 && !string.IsNullOrWhiteSpace(artistParts[0]) && !string.Equals(artistParts[0], cArtist, StringComparison.OrdinalIgnoreCase))
        {
            var fallbackRes = await ExecuteSearchCascadeAsync(artistParts[0], cTitle, album, duration);
            if (fallbackRes.Found) return fallbackRes;
        }

        // 3. Fallback 2: Si le titre contenait un nom entre crochets japonais (ex: "Suzume" dans "Suzume no Tojimari『Suzume』")
        string? innerTitle = ExtractBracketedTitle(title);
        if (!string.IsNullOrWhiteSpace(innerTitle) && !innerTitle.Equals(cTitle, StringComparison.OrdinalIgnoreCase))
        {
            var bracketRes = await ExecuteSearchCascadeAsync(cArtist, innerTitle, album, duration);
            if (bracketRes.Found) return bracketRes;

            if (artistParts.Count > 0 && !string.IsNullOrWhiteSpace(artistParts[0]))
            {
                var bracketArtistRes = await ExecuteSearchCascadeAsync(artistParts[0], innerTitle, album, duration);
                if (bracketArtistRes.Found) return bracketArtistRes;
            }
        }

        // 4. Fallback 3: Si l'artiste ressemble à un nom de chaîne YouTube (ex: "AniMelody - Anime Music", "Topic", "Vevo")
        // ou si la recherche avec artiste n'a rien donné : tenter avec le titre seul (avec contrôle de durée et langue)
        string targetTitle = !string.IsNullOrWhiteSpace(innerTitle) ? innerTitle : cTitle;
        bool isChannelArtist = System.Text.RegularExpressions.Regex.IsMatch(cArtist, @"(?i)(anime music|music channel|records|channel|vevo|topic|entertainment)");
        // Recherche par titre seul : uniquement quand l'artiste est inconnu (ou n'est qu'un nom de chaine), avec un titre
        // assez distinctif et une duree quasi identique. Sinon un titre generique ("Awaken", "Villain"...) retombe sur un
        // tout autre morceau, d'ou les paroles fausses ou les paroles trouvees pour des morceaux instrumentaux.
        string targetNorm = NormalizeForMatching(targetTitle);
        int targetWords = targetNorm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        // Titre fiable : extrait d'un crochet (『Suzume』) ou compose de plusieurs mots -> tolerance de duree habituelle.
        // Mot unique : il doit etre assez long ET la duree quasi identique (mode strict), sinon on ne cherche pas.
        bool bracketTitle = !string.IsNullOrWhiteSpace(innerTitle) && targetNorm.Length >= 5 && !NoiseWords.Contains(targetNorm);
        bool reliableTitle = bracketTitle || targetWords >= 2;
        bool titleOnlyAllowed = reliableTitle ? targetNorm.Length >= 4 : targetNorm.Length >= 7;
        if ((isChannelArtist || string.IsNullOrWhiteSpace(cArtist))
            && duration.HasValue && duration.Value.TotalSeconds > 10.0
            && titleOnlyAllowed)
        {
            var titleOnlyFallback = await ExecuteSearchCascadeAsync("", targetTitle, null, duration, titleOnly: true, strict: !reliableTitle);
            if (titleOnlyFallback.Found)
            {
                titleOnlyFallback.TitleOnly = true; // resultat approximatif : l'appelant peut le recouper avec un tag fiable
                return titleOnlyFallback;
            }
        }

        return new LyricsResult();
    }
}

public static class LocalLyricsService
{
    private static readonly string[] LyricsSubfolderNames = { "Lyrics", "lyrics" };

    public class LocalLyricsResult
    {
        public string Text { get; set; } = "";
        public bool IsSynced { get; set; }
    }

    public static LocalLyricsResult? FindLocalLyrics(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            string? embedded = file.Tag.Lyrics;
            if (!string.IsNullOrWhiteSpace(embedded))
            {
                return new LocalLyricsResult { Text = embedded, IsSynced = LooksLikeLrc(embedded) };
            }
        }
        catch { }

        string? dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir)) return null;
        string baseName = Path.GetFileNameWithoutExtension(filePath);

        var sidecarResult = TryReadLyricsPair(dir, baseName);
        if (sidecarResult != null) return sidecarResult;

        foreach (string sub in LyricsSubfolderNames)
        {
            string subDir = Path.Combine(dir, sub);
            var subResult = TryReadLyricsPair(subDir, baseName);
            if (subResult != null) return subResult;
        }

        return null;
    }

    private static LocalLyricsResult? TryReadLyricsPair(string dir, string baseName)
    {
        try
        {
            if (!Directory.Exists(dir)) return null;

            string lrcPath = Path.Combine(dir, baseName + ".lrc");
            if (System.IO.File.Exists(lrcPath))
            {
                string text = System.IO.File.ReadAllText(lrcPath);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return new LocalLyricsResult { Text = text, IsSynced = true };
                }
            }

            string txtPath = Path.Combine(dir, baseName + ".txt");
            if (System.IO.File.Exists(txtPath))
            {
                string text = System.IO.File.ReadAllText(txtPath);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return new LocalLyricsResult { Text = text, IsSynced = LooksLikeLrc(text) };
                }
            }
        }
        catch { }
        return null;
    }

    public static bool LooksLikeLrc(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"\[\d{1,2}:\d{2}([.:]\d{1,3})?\]");
    }
}

public class PlaylistM3uService
{
    public async Task<(List<string> resolvedPaths, List<string> missing, string? coverPath)> ImportAsync(string playlistFilePath, IEnumerable<Track>? library = null)
	{
        string ext = Path.GetExtension(playlistFilePath).ToLowerInvariant();
        if (ext is ".csv" or ".tsv" or ".txt")
        {
            var lib = library ?? (App.Cache != null ? await App.Cache.LoadAllTracksAsync() : new List<Track>());
            return await PlaylistImportService.ImportExportFileAsync(playlistFilePath, lib);
        }

        var resolved = new List<string>();
        var missing = new List<string>();
		string? coverPath = null;
        string baseDir = Path.GetDirectoryName(playlistFilePath) ?? string.Empty;
        var lines = await System.IO.File.ReadAllLinesAsync(playlistFilePath, DetectEncoding(playlistFilePath));

        FuzzyTrackMatcher? fallbackMatcher = null;
        string? lastExtInfTitle = null;
        string? lastExtInfArtist = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
			if (line.StartsWith("#EXTIMG:"))
            {
                string cPath = line.Substring(8).Trim();
                string fullCPath = Path.IsPathRooted(cPath) ? cPath : Path.GetFullPath(Path.Combine(baseDir, cPath));
                if (System.IO.File.Exists(fullCPath)) coverPath = fullCPath;
                continue;
            }
            if (line.StartsWith("#EXTINF:"))
            {
                int commaIdx = line.IndexOf(',');
                if (commaIdx >= 0 && commaIdx + 1 < line.Length)
                {
                    string info = line.Substring(commaIdx + 1).Trim();
                    int dashIdx = info.IndexOf(" - ");
                    if (dashIdx >= 0)
                    {
                        lastExtInfArtist = info.Substring(0, dashIdx).Trim();
                        lastExtInfTitle = info.Substring(dashIdx + 3).Trim();
                    }
                    else
                    {
                        lastExtInfTitle = info;
                        lastExtInfArtist = null;
                    }
                }
                continue;
            }
			if (line.StartsWith("#")) continue;

            string fullPath = Path.IsPathRooted(line)
                ? line
                : Path.GetFullPath(Path.Combine(baseDir, line));

            if (System.IO.File.Exists(fullPath))
            {
                resolved.Add(fullPath);
            }
            else
            {
                bool matched = false;
                if (library != null)
                {
                    fallbackMatcher ??= new FuzzyTrackMatcher(library);
                    string guessTitle = lastExtInfTitle ?? Path.GetFileNameWithoutExtension(line);
                    string guessArtist = lastExtInfArtist ?? string.Empty;
                    var match = fallbackMatcher.Match(new PlaylistImportEntry
                    {
                        Title = guessTitle,
                        Artist = guessArtist,
                        RawLine = line
                    });
                    if (match.matchedTrack != null && !string.IsNullOrWhiteSpace(match.matchedTrack.FilePath))
                    {
                        resolved.Add(match.matchedTrack.FilePath);
                        matched = true;
                    }
                }

                if (!matched)
                {
                    missing.Add(!string.IsNullOrWhiteSpace(lastExtInfTitle) ? $"\"{lastExtInfTitle}\"{(string.IsNullOrWhiteSpace(lastExtInfArtist) ? "" : $" - {lastExtInfArtist}")}" : line);
                }
            }

            lastExtInfTitle = null;
            lastExtInfArtist = null;
        }

        if (string.IsNullOrEmpty(coverPath))
        {
            string name = Path.GetFileNameWithoutExtension(playlistFilePath);
            foreach (var imgExt in new[] { ".jpg", ".jpeg", ".png", ".webp" })
            {
                string p = Path.Combine(baseDir, name + imgExt);
                if (System.IO.File.Exists(p)) { coverPath = p; break; }
            }
        }

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
    public static bool IsSupportedAudio(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private static readonly string[] SupportedExtensions =
    {
        ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma", ".alac", ".aiff", ".aif",
        ".opus", ".ape", ".mp4", ".m4b", ".dsf", ".dff", ".wv", ".tta", ".spx"
    };

    public IEnumerable<string> EnumerateAudioFiles(string rootFolder)
    {
        if (!Directory.Exists(rootFolder)) yield break;
        // Copie locale de la liste d'exclusion (fichiers retirés de l'app par l'utilisateur)
        var excluded = new HashSet<string>(App.Settings?.Current?.ExcludedFilePaths ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
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
                else if (SupportedExtensions.Contains(Path.GetExtension(entry).ToLowerInvariant()) && !excluded.Contains(entry))
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

        // Migrate existing databases for IsSystem (playlist protegee, ex. Favoris)
        try
        {
            var alterCmd2 = conn.CreateCommand();
            alterCmd2.CommandText = "ALTER TABLE Playlists ADD COLUMN IsSystem INTEGER NOT NULL DEFAULT 0;";
            await alterCmd2.ExecuteNonQueryAsync();
        }
        catch { /* Column probably already exists */ }

        // Migrate existing databases for SmartRulesJson (playlists intelligentes ; NULL = playlist classique)
        try
        {
            var alterCmd3 = conn.CreateCommand();
            alterCmd3.CommandText = "ALTER TABLE Playlists ADD COLUMN SmartRulesJson TEXT;";
            await alterCmd3.ExecuteNonQueryAsync();
        }
        catch { /* Column probably already exists */ }

        // Cree la playlist systeme Favoris si elle n'existe pas encore, toujours en base des
        // le premier lancement qui touche cette methode, pour qu'elle soit disponible partout.
        try
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Playlists WHERE Id = $id";
            checkCmd.Parameters.AddWithValue("$id", Models.Playlist.FavoritesId);
            long count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);
            if (count == 0)
            {
                var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = """
                    INSERT INTO Playlists (Id, Name, TrackIdsJson, CoverImagePath, DateCreated, DateModified, IsSystem)
                    VALUES ($id, $name, '[]', NULL, $created, $created, 1);
                    """;
                insertCmd.Parameters.AddWithValue("$id", Models.Playlist.FavoritesId);
                insertCmd.Parameters.AddWithValue("$name", Models.Strings.Current.IsFr ? "Favoris" : "Favorites");
                insertCmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
        catch { /* best effort */ }
    }

        public async Task ClearVisuallyModifiedTagsAsync()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Tracks SET Title = NULL, Artist = NULL, Album = NULL, AlbumArtist = NULL, Genre = NULL, Year = NULL, TrackNumber = NULL";
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
                // IsAnalyzed = 3 : analyse faite avec la normalisation LUFS (loudness) + plafond anti-saturation. Les anciennes analyses
                // (1 = niveau moyen, 2 = pics) sont ignorees : le son est reanalyse automatiquement.
                NormalizationGainDb = SafeGetInt32(reader, "IsAnalyzed") == 3 ? SafeGetDouble(reader, "NormalizationGainDb") : 0.0,
                IsAnalyzed          = SafeGetInt32(reader, "IsAnalyzed") == 3,
                DateAdded           = SafeGetDateTime(reader, "DateAdded"),
                LastModified        = SafeGetDateTime(reader, "LastModified")
            });
        }
        return tracks;
    }

    public async Task UpsertTrackAsync(Track track)
    {
        if (track.IsExternal) return;   // son ouvert depuis Windows : jamais ajoute a la bibliotheque
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
        cmd.Parameters.AddWithValue("$analyzed",    track.IsAnalyzed ? 3 : 0);
        cmd.Parameters.AddWithValue("$added",       track.DateAdded.ToString("o"));
        cmd.Parameters.AddWithValue("$modified",    track.LastModified.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertTracksBatchedAsync(IEnumerable<Track> tracks)
    {
        tracks = tracks.Where(t => !t.IsExternal);
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
            pAnalyzed.Value = track.IsAnalyzed ? 3 : 0;
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
        cmd.Parameters.AddWithValue("$analyzed",    track.IsAnalyzed ? 3 : 0);
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

    public async Task DeleteTrackByPathAsync(string filePath)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tracks WHERE FilePath = $path";
        cmd.Parameters.AddWithValue("$path", filePath);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTracksByPathsAsync(IEnumerable<string> filePaths)
    {
        var list = filePaths.ToList();
        if (list.Count == 0) return;
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        var cmd = conn.CreateCommand();
        cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = "DELETE FROM Tracks WHERE FilePath = $path";
        var p = cmd.Parameters.Add("$path", SqliteType.Text);
        foreach (var path in list)
        {
            p.Value = path;
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task<List<Models.Playlist>> LoadAllPlaylistsAsync()
    {
        var list = new List<Models.Playlist>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, TrackIdsJson, CoverImagePath, DateCreated, DateModified, IsSystem, SmartRulesJson FROM Playlists ORDER BY DateCreated DESC";
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
                DateModified = DateTime.Parse(reader.GetString(5)),
                IsSystem     = !reader.IsDBNull(6) && reader.GetInt32(6) == 1,
                SmartRulesJson = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        // La playlist systeme (Favoris) est toujours affichee en premier, quelle que soit sa
        // date de creation.
        list.Sort((a, b) =>
        {
            if (a.IsSystem != b.IsSystem) return a.IsSystem ? -1 : 1;
            return b.DateCreated.CompareTo(a.DateCreated);
        });
        return list;
    }

    public async Task UpsertPlaylistAsync(Models.Playlist playlist)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Playlists (Id, Name, TrackIdsJson, CoverImagePath, DateCreated, DateModified, IsSystem, SmartRulesJson)
            VALUES ($id, $name, $json, $cover, $created, $modified, $isSystem, $smart)
            ON CONFLICT(Id) DO UPDATE SET
                Name=CASE WHEN Playlists.IsSystem = 1 THEN Playlists.Name ELSE excluded.Name END,
                SmartRulesJson=excluded.SmartRulesJson,
                TrackIdsJson=excluded.TrackIdsJson,
                CoverImagePath=CASE WHEN Playlists.IsSystem = 1 THEN Playlists.CoverImagePath ELSE excluded.CoverImagePath END,
                DateModified=excluded.DateModified;
            """;
        cmd.Parameters.AddWithValue("$id",       playlist.Id);
        cmd.Parameters.AddWithValue("$name",     playlist.Name);
        cmd.Parameters.AddWithValue("$json",     JsonSerializer.Serialize(playlist.TrackIds));
        cmd.Parameters.AddWithValue("$cover",    string.IsNullOrEmpty(playlist.CoverImagePath) ? (object)DBNull.Value : playlist.CoverImagePath);
        cmd.Parameters.AddWithValue("$created",  playlist.DateCreated.ToString("o"));
        cmd.Parameters.AddWithValue("$modified", playlist.DateModified.ToString("o"));
        cmd.Parameters.AddWithValue("$isSystem", playlist.IsSystem ? 1 : 0);
        cmd.Parameters.AddWithValue("$smart",    string.IsNullOrWhiteSpace(playlist.SmartRulesJson) ? (object)DBNull.Value : playlist.SmartRulesJson);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeletePlaylistAsync(string playlistId)
    {
        // La playlist systeme (Favoris) ne peut jamais etre supprimee, meme si un appelant
        // oublie de verifier IsSystem avant d'appeler cette methode.
        if (playlistId == Models.Playlist.FavoritesId) return;
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

    private SilenceTrimSampleProvider? _silenceTrimmer;

    public event EventHandler? PlaybackStopped;

    // Moteur DJ du "Mode soiree" : cree a la demande, totalement separe du moteur normal (voir DjMixerEngine.cs).
    private DjMixerEngine? _djEngine;
    private bool DjActive => _djEngine?.IsActive == true;
    private DjMixerEngine Dj
    {
        get
        {
            if (_djEngine == null)
            {
                var e = new DjMixerEngine();
                e.PlaybackEnded += (s, a) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
                _djEngine = e;
            }
            return _djEngine;
        }
    }

    /// <summary>Fin "utile" du son en cours : la fin du fichier, ou l'instant du dernier son audible quand l'option "Ignorer les silences" est active.</summary>
    public TimeSpan EffectiveEnd
    {
        get
        {
            if (DjActive) return _djEngine!.TotalDuration;
            TimeSpan total = TotalDuration;
            TimeSpan? limit = _silenceTrimmer?.EndLimit;
            return (limit.HasValue && limit.Value > TimeSpan.Zero && (total <= TimeSpan.Zero || limit.Value < total)) ? limit.Value : total;
        }
    }

    public float CurrentRmsLevel => DjActive ? _djEngine!.RmsLevel : (_rmsCapture?.CurrentRms ?? 0f);
    public float CurrentBassLevel => DjActive ? _djEngine!.BassLevel : (_rmsCapture?.CurrentBass ?? 0f);

    public bool          IsExclusiveMode  { get; private set; }
    public PlaybackState State            => DjActive ? _djEngine!.State : (_output?.PlaybackState ?? PlaybackState.Stopped);
    public TimeSpan      CurrentPosition  => DjActive ? _djEngine!.CurrentPosition : (_fileReader?.CurrentTime ?? _opusReader?.CurrentTime ?? TimeSpan.Zero);
    public TimeSpan      TotalDuration    => DjActive ? _djEngine!.TotalDuration : (_fileReader?.TotalTime   ?? _opusReader?.TotalTime   ?? TimeSpan.Zero);
    public Track?        CurrentTrack     { get; private set; }

    private float  _userVolume         = 1.0f;
    private double _normalizationGainDb = 0;

    private readonly SemaphoreSlim _playLock = new(1, 1);
    // Compteur de demandes de lecture : si l'utilisateur enchaîne les sons, les demandes
    // devenues obsolètes (dépassées par une plus récente) sont ignorées au lieu d'être toutes exécutées.
    private int _playRequestCounter;

    private record PrewarmedReader(NAudio.Wave.WaveStream? FileReader, OpusSampleProvider? OpusReader, ISampleProvider? SampleProvider);
    private Task<PrewarmedReader?>? _prewarmTask;
    private string?                   _prewarmPath;
    private DateTime                  _lastPrewarmUtc = DateTime.MinValue;
    private const double PrewarmDebounceMs = 300;

    public async Task PlayAsync(Track track, bool preferExclusive = false, double initialGainDb = 0, Action<string>? onDownloadProgress = null, int crossfadeMs = 0, TimeSpan? startAt = null, bool startPaused = false, bool beatMix = true)
    {
        int myPlayRequest = System.Threading.Interlocked.Increment(ref _playRequestCounter);
        await _playLock.WaitAsync();
        if (myPlayRequest != System.Threading.Volatile.Read(ref _playRequestCounter))
        {
            _playLock.Release();
            return;
        }
        try
        {
            // Mode soiree : la lecture est confiee au moteur DJ (une seule sortie + melangeur), sauf en mode exclusif.
            if (App.Settings.Current.PartyModeEnabled && !preferExclusive)
            {
                if (_output != null || _fileReader != null || _opusReader != null) StopBase();
                CurrentTrack = track;
                _normalizationGainDb = initialGainDb;
                IsExclusiveMode = false;
                Dj.SetUserVolume(_userVolume);
                await Dj.PlayAsync(track, initialGainDb, crossfadeMs, startAt, startPaused, onDownloadProgress, beatMix);
                return;
            }
            if (_djEngine != null && _djEngine.IsActive) _djEngine.Stop();   // mode soiree coupe : retour au moteur normal

            if (crossfadeMs > 0 && !IsExclusiveMode)
                DetachAndFadeOut(crossfadeMs);
            else
                Stop();
            CurrentTrack = track;

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
                        var msg = Resona.Models.Strings.Current.IsFr ? $"Format audio non pris en charge ou illisible (le fichier est peut-être corrompu ou c'est un fichier WebM/Opus déguisé en MP3).\nFichier : {track.FilePath}" : $"Unsupported or unreadable audio format (the file might be corrupted or it's a WebM/Opus file disguised as MP3).\nFile : {track.FilePath}";
                        throw new NotSupportedException(msg, ex);
                    }
                }
            }

            if (sampleProvider == null) { var msg2 = Resona.Models.Strings.Current.IsFr ? $"Impossible de créer un lecteur.\nFichier : {track.FilePath}" : $"Cannot create audio reader.\nFile : {track.FilePath}"; throw new InvalidOperationException(msg2); }

            // Option "Ignorer les silences au debut et a la fin" : on mesure l'instant du dernier son audible AVANT de se replacer
            // au debut (ou a startAt), puis on enveloppe le flux dans SilenceTrimSampleProvider.
            _silenceTrimmer = null;
            DuckingService.EnsureStarted();
            if (App.Settings.Current.SkipSilenceEnabled || App.Settings.Current.PartyModeEnabled)
            {
                var trimFile = _fileReader;
                var trimOpus = _opusReader;
                string trimPath = track.FilePath;
                TimeSpan trimKnownDuration = track.Duration;
                Func<TimeSpan> trimGetPos = () => trimFile?.CurrentTime ?? trimOpus?.CurrentTime ?? TimeSpan.Zero;
                Action<TimeSpan> trimSetPos = t =>
                {
                    if (trimFile != null) trimFile.CurrentTime = t;
                    if (trimOpus != null) trimOpus.CurrentTime = t;
                };
                // Mesure sur un lecteur SEPARE : deplacer le lecteur en cours faisait sauter la barre de lecture vers la fin.
                TimeSpan? trimEnd = await Task.Run(() => ScanAudibleEnd(trimPath, trimKnownDuration));
                LogService.Write("SilenceTrim", $"Fin utile : {(trimEnd.HasValue ? trimEnd.Value.TotalSeconds.ToString("0.00") + " s" : "non mesuree / audio jusqu'a la fin")} pour {Path.GetFileName(trimPath)}");
                bool trimLeading = !(startAt.HasValue && startAt.Value > TimeSpan.Zero);
                _silenceTrimmer = new SilenceTrimSampleProvider(sampleProvider, trimGetPos, trimLeading, trimEnd);
                sampleProvider = _silenceTrimmer;
            }

            // Reprise apres liberation temporaire du fichier (RunWithFileReleasedAsync) : on se replace avant le demarrage, sans artefact audio.
            if (startAt.HasValue && startAt.Value > TimeSpan.Zero)
            {
                try
                {
                    if (_fileReader != null) _fileReader.CurrentTime = startAt.Value;
                    if (_opusReader != null) _opusReader.CurrentTime = startAt.Value;
                }
                catch { }
            }

            _rmsCapture     = new RmsCaptureSampleProvider(sampleProvider);
            _volumeProvider = new VolumeSampleProvider(_rmsCapture) { Volume = ComputeLinearGain() };
            _equalizer      = new EqualizerSampleProvider(new DuckingSampleProvider(_volumeProvider));
            _equalizer.SetEnabled(App.Settings.Current.EqualizerEnabled);
            if (App.Settings.Current.EqualizerEnabled)
            {
                var bands = App.Settings.Current.EqualizerBands;
                for (int i = 0; i < bands.Length && i < 10; i++)
                    _equalizer.SetBand(i, (float)bands[i]);
            }
            _finalProvider  = new SafetyLimiterSampleProvider(_equalizer);

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
                HookPlaybackStopped(output);
                if (startPaused)
                {
                    // Reprise "en pause" : flux demarre a volume nul puis mis aussitot en pause (rien d'audible), volume restaure ensuite.
                    var pausedVolume = _volumeProvider;
                    if (pausedVolume != null) pausedVolume.Volume = 0f;
                    output.Play();
                    output.Pause();
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(250);
                        try { if (pausedVolume != null) pausedVolume.Volume = ComputeLinearGain(); } catch { }
                    });
                }
                else
                {
                    output.Play();
                }
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

    private static TimeSpan? ScanAudibleEnd(string path, TimeSpan knownDuration)
    {
        try
        {
            string ext = Path.GetExtension(LibraryScannerService.ResolveRealExtension(path)).ToLowerInvariant();
            if (ext is ".opus" or ".ogg")
            {
                using var opus = new OpusSampleProvider(path, knownDuration);
                return SilenceTrimSampleProvider.FindAudibleEnd(opus, () => opus.CurrentTime, t => opus.CurrentTime = t, opus.TotalTime);
            }
            try
            {
                using var reader = new AudioFileReader(path);
                return SilenceTrimSampleProvider.FindAudibleEnd(reader, () => reader.CurrentTime, t => reader.CurrentTime = t, reader.TotalTime);
            }
            catch
            {
                using var mf = SafeAudioReader.Create(path);
                return SilenceTrimSampleProvider.FindAudibleEnd(mf.ToSampleProvider(), () => mf.CurrentTime, t => mf.CurrentTime = t, mf.TotalTime);
            }
        }
        catch { return null; }
    }

    private static bool SamePath(string? a, string? b)
        => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Windows interdit d'ecrire dans un fichier deja ouvert par le lecteur. Execute `action` (ex. ecrire un tag) pendant que le
    /// fichier est libere, puis reprend la lecture au meme endroit et dans le meme etat (lecture ou pause).
    /// Pour un autre fichier que celui en cours, l'action est simplement executee (apres abandon du prechargement s'il le tenait ouvert).
    /// </summary>
    public async Task RunWithFileReleasedAsync(string filePath, Action action)
    {
        var track = CurrentTrack;
        bool isCurrent = track != null && (_fileReader != null || _opusReader != null || DjActive) && SamePath(track.FilePath, filePath);

        if (!isCurrent)
        {
            if (SamePath(_prewarmPath, filePath))
            {
                var task = _prewarmTask;
                _prewarmTask = null;
                _prewarmPath = null;
                if (task != null)
                {
                    try
                    {
                        var prewarmed = await task.WaitAsync(TimeSpan.FromSeconds(5));
                        prewarmed?.FileReader?.Dispose();
                        prewarmed?.OpusReader?.Dispose();
                    }
                    catch { }
                }
            }
            await Task.Run(action);
            return;
        }

        bool wasPlaying = State == PlaybackState.Playing;
        TimeSpan position = CurrentPosition;
        double gainDb = _normalizationGainDb;
        bool exclusive = IsExclusiveMode;
        int request = System.Threading.Volatile.Read(ref _playRequestCounter);

        Stop();
        Exception? error = null;
        try { await Task.Run(action); }
        catch (Exception ex) { error = ex; }

        // Un autre son a ete lance entre-temps (clic sur suivant...) : on ne reprend pas l'ancien.
        if (request == System.Threading.Volatile.Read(ref _playRequestCounter))
        {
            try { await PlayAsync(track!, exclusive, gainDb, null, 0, position, startPaused: !wasPlaying); }
            catch (Exception ex)
            {
                LogService.Write("Playback", ex, "Reprise de la lecture apres ecriture du fichier");
                error ??= ex;
            }
        }
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
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

    // ------------------------------------------------------------------
    //  Suivi du peripherique de sortie Windows
    //  Un flux WASAPI reste lie au peripherique sur lequel il a ete ouvert : quand l'utilisateur change de
    //  peripherique par defaut dans Windows, on recree la sortie sur le nouveau, a la meme position et dans
    //  le meme etat (lecture / pause).
    // ------------------------------------------------------------------

    private string?                  _currentDeviceId;
    private MMDeviceEnumerator?      _deviceWatcher;
    private DeviceChangeClient?      _deviceClient;
    private int                      _deviceChangeToken;
    // Sorties volontairement arretees par l'app (changement de peripherique) : leur PlaybackStopped ne doit pas passer au son suivant.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<WasapiOut, object> _silencedOutputs = new();

    private sealed class DeviceChangeClient : NAudio.CoreAudioApi.Interfaces.IMMNotificationClient
    {
        private readonly AudioEngineService _owner;
        public DeviceChangeClient(AudioEngineService owner) => _owner = owner;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            // Peripherique desactive / debranche : si c'etait le notre, on bascule.
            if (newState != DeviceState.Active) _owner.ScheduleDeviceRebuild(force: false);
        }
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) => _owner.ScheduleDeviceRebuild(force: false);
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && (role == Role.Multimedia || role == Role.Console))
                _owner.ScheduleDeviceRebuild(force: false);
        }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }

    private void EnsureDeviceWatcher()
    {
        if (_deviceWatcher != null) return;
        try
        {
            _deviceWatcher = new MMDeviceEnumerator();
            _deviceClient  = new DeviceChangeClient(this);
            _deviceWatcher.RegisterEndpointNotificationCallback(_deviceClient);
        }
        catch
        {
            _deviceWatcher = null;
            _deviceClient  = null;
        }
    }

    private void StopDeviceWatcher()
    {
        try { if (_deviceWatcher != null && _deviceClient != null) _deviceWatcher.UnregisterEndpointNotificationCallback(_deviceClient); } catch { }
        try { _deviceWatcher?.Dispose(); } catch { }
        _deviceWatcher = null;
        _deviceClient  = null;
    }

    private void ScheduleDeviceRebuild(bool force)
    {
        // Windows envoie plusieurs notifications d'affilee (Console, Multimedia, Communications...) : on regroupe.
        int token = Interlocked.Increment(ref _deviceChangeToken);
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            if (token != Volatile.Read(ref _deviceChangeToken)) return;
            await RebuildOutputOnDeviceChangeAsync(force);
        });
    }

    private void HookPlaybackStopped(WasapiOut output)
    {
        output.PlaybackStopped += (s, e) =>
        {
            if (_silencedOutputs.TryGetValue(output, out _)) return;

            // AUDCLNT_E_DEVICE_INVALIDATED : le peripherique a ete debranche / desactive en pleine lecture.
            // On bascule sur le nouveau peripherique au lieu de passer au son suivant.
            if (e.Exception is System.Runtime.InteropServices.COMException ce
                && unchecked((uint)ce.HResult) == 0x88890004u
                && ReferenceEquals(output, _output))
            {
                ScheduleDeviceRebuild(force: true);
                return;
            }

            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        };
    }

    private async Task RebuildOutputOnDeviceChangeAsync(bool force)
    {
        await _playLock.WaitAsync();
        try
        {
            var oldOut   = _output;
            var provider = _finalProvider;
            // Rien ne joue : la prochaine lecture ouvrira directement le bon peripherique.
            if (oldOut == null || provider == null) return;

            string? newId = null;
            try
            {
                using var en = new MMDeviceEnumerator();
                using var d  = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                newId = d.ID;
            }
            catch { }
            if (!force && newId != null && newId == _currentDeviceId) return;

            var  state        = oldOut.PlaybackState;
            bool wasExclusive = IsExclusiveMode;

            _silencedOutputs.AddOrUpdate(oldOut, new object());
            _output = null;
            try { oldOut.Stop(); } catch { }
            try { oldOut.Dispose(); } catch { }

            await InitOutputAsync(wasExclusive);
            var output = _output;
            if (output == null) return;

            output.Init(provider);
            HookPlaybackStopped(output);

            if (state == PlaybackState.Playing)
            {
                output.Play();
            }
            else if (state == PlaybackState.Paused)
            {
                // Meme astuce que la reprise en pause : demarrage a volume nul puis pause immediate.
                var vol = _volumeProvider;
                if (vol != null) vol.Volume = 0f;
                output.Play();
                output.Pause();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(250);
                    try { if (vol != null) vol.Volume = ComputeLinearGain(); } catch { }
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Write("AudioEngine", ex, "Changement de peripherique de sortie");
        }
        finally
        {
            _playLock.Release();
        }
    }

    private async Task InitOutputAsync(bool preferExclusive)
    {
        EnsureDeviceWatcher();
        await Task.Run(() =>
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _currentDeviceId = device.ID;

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
            // Peripherique explicite (et non le constructeur sans peripherique) : on est sur d'ouvrir le defaut ACTUEL.
            _output         = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
            IsExclusiveMode = false;
        });
    }

    private float ComputeLinearGain()
    {
        // Plafond a +30 dB : la normalisation des pics peut devoir remonter beaucoup un son tres faible.
        double linear = Math.Pow(10, _normalizationGainDb / 20.0) * _userVolume;
        return (float)Math.Clamp(linear, 0.0, 32.0);
    }

    public void SetUserVolume(float volume01)
    {
        _userVolume = Math.Clamp(volume01, 0f, 1f);
        _djEngine?.SetUserVolume(_userVolume);
        if (_volumeProvider != null) _volumeProvider.Volume = ComputeLinearGain();
    }

    public void SetNormalizationGain(double gainDb)
    {
        _normalizationGainDb = gainDb;
        _djEngine?.SetNormalizationGain(gainDb);
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
        _djEngine?.SetEqualizerEnabled(enabled);
    }

    public void SetEqualizerBand(int index, float gainDb)
    {
        _equalizer?.SetBand(index, gainDb);
        _djEngine?.SetEqualizerBand(index, gainDb);
    }

    public void Pause()
    {
        if (DjActive) { _djEngine!.Pause(); return; }
        if (_output == null || _volumeProvider == null || _output.PlaybackState != PlaybackState.Playing) return;
        
        if (!App.Settings.Current.EnableCrossfade)
        {
            _output?.Pause();
            return;
        }

        System.Threading.Tasks.Task.Run(async () => {
            float startVol = _volumeProvider.Volume;
            for(int i=0; i<10; i++) {
                await System.Threading.Tasks.Task.Delay(15);
                try { _volumeProvider.Volume = startVol * (1f - (i+1)/10f); } catch { }
            }
            try { _output?.Pause(); } catch { }
            try { _volumeProvider.Volume = ComputeLinearGain(); } catch { }
        });
    }

    public void Resume()
    {
        if (DjActive) { _djEngine!.Resume(); return; }
        if (_output == null || _volumeProvider == null) return;
        
        if (!App.Settings.Current.EnableCrossfade)
        {
            _output?.Play();
            return;
        }

        float targetVol = ComputeLinearGain();
        _volumeProvider.Volume = 0f;
        _output?.Play();
        
        System.Threading.Tasks.Task.Run(async () => {
            for(int i=0; i<10; i++) {
                await System.Threading.Tasks.Task.Delay(15);
                try { _volumeProvider.Volume = targetVol * ((i+1)/10f); } catch { }
            }
            try { _volumeProvider.Volume = targetVol; } catch { }
        });
    }

    public void Seek(TimeSpan position)
    {
        if (DjActive) { _djEngine!.Seek(position); return; }
        TimeSpan total = TotalDuration;
        if (total > TimeSpan.Zero)
        {
            TimeSpan maxSeekable = total - TimeSpan.FromMilliseconds(200);
            if (maxSeekable < TimeSpan.Zero) maxSeekable = TimeSpan.Zero;
            if (position > maxSeekable) position = maxSeekable;
        }
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        _silenceTrimmer?.NotifySeek();
        try
        {
            if (_fileReader != null) _fileReader.CurrentTime = position;
            if (_opusReader != null) _opusReader.CurrentTime = position;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Certains décodeurs refusent un seek trop proche de la fin réelle du flux
            // (durée déclarée légèrement supérieure à la durée décodable). On retente
            // avec une marge de sécurité plus large plutôt que de laisser planter la lecture.
            try
            {
                TimeSpan safePosition = position - TimeSpan.FromSeconds(1);
                if (safePosition < TimeSpan.Zero) safePosition = TimeSpan.Zero;
                if (_fileReader != null) _fileReader.CurrentTime = safePosition;
                if (_opusReader != null) _opusReader.CurrentTime = safePosition;
            }
            catch { }
        }
    }

    public void Stop()
    {
        StopBase();
        _djEngine?.Stop();
    }

    private void StopBase()
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
        _silenceTrimmer = null;

        DiscardPrewarmLocked();
    }

    // Crossfade support
    private System.Threading.Timer? _fadeTimer;
    private float _fadeStartVolume;
    private int _fadeStepsRemaining;
    private const int FadeTotalSteps = 20;

    public bool IsFadingOut { get; private set; }

    public void StartFadeOut(int durationMs)
    {
        if (IsFadingOut || _volumeProvider == null) return;
        IsFadingOut = true;
        _fadeStartVolume = _volumeProvider.Volume;
        _fadeStepsRemaining = FadeTotalSteps;
        int interval = durationMs / FadeTotalSteps;

        _fadeTimer = new System.Threading.Timer(_ =>
        {
            _fadeStepsRemaining--;
            if (_fadeStepsRemaining <= 0)
            {
                if (_volumeProvider != null) _volumeProvider.Volume = 0;
                _fadeTimer?.Dispose();
                _fadeTimer = null;
                IsFadingOut = false;
                return;
            }
            if (_volumeProvider != null)
            {
                _volumeProvider.Volume = _fadeStartVolume * ((float)_fadeStepsRemaining / FadeTotalSteps);
            }
        }, null, interval, interval);
    }

        public void DetachAndFadeOut(int durationMs)
    {
        if (_output == null || _volumeProvider == null || IsExclusiveMode) 
        {
            Stop();
            return;
        }
        
        var oldOut = _output;
        var oldVol = _volumeProvider;
        var oldReader1 = _fileReader;
        var oldReader2 = _opusReader;
        
        _output = null;
        _volumeProvider = null;
        _fileReader = null;
        _opusReader = null;
        _rmsCapture = null;
        _equalizer = null;
        _finalProvider = null;
        
        System.Threading.Tasks.Task.Run(async () => {
            int steps = 20;
            int delay = durationMs / steps;
            float startVol = oldVol.Volume;
            for(int i=0; i<steps; i++) {
                await System.Threading.Tasks.Task.Delay(delay);
                try { oldVol.Volume = startVol * (1f - (i + 1f)/steps); } catch { }
            }
            try { oldOut.Stop(); } catch { }
            try { oldOut.Dispose(); } catch { }
            try { oldReader1?.Dispose(); } catch { }
            try { oldReader2?.Dispose(); } catch { }
        });
    }

    public void CancelFade()
    {
        _fadeTimer?.Dispose();
        _fadeTimer = null;
        IsFadingOut = false;
        if (_volumeProvider != null) _volumeProvider.Volume = ComputeLinearGain();
    }

    public void Dispose()
    {
        StopDeviceWatcher();
        Stop();
    }
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
    public float      CurrentBass { get; private set; }

    private float _lowPassState;
    private float _alpha;
    private int   _lastSampleRate;

    public RmsCaptureSampleProvider(ISampleProvider source)
    {
        _source = source;
        UpdateFilter(source.WaveFormat?.SampleRate ?? 44100);
    }

    private void UpdateFilter(int sampleRate)
    {
        if (sampleRate <= 0) sampleRate = 44100;
        _lastSampleRate = sampleRate;
        float dt = 1.0f / sampleRate;
        float rc = 1.0f / (2.0f * (float)Math.PI * 160f); // Cutoff basses ~160 Hz
        _alpha = dt / (rc + dt);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read;
        try
        {
            read = _source.Read(buffer, offset, count);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Le décodeur sous-jacent peut refuser de lire après un seek en fin de flux
            // (durée déclarée vs durée réelle). On retourne un buffer silencieux plutôt
            // que de laisser l'exception remonter et faire planter la lecture.
            Array.Clear(buffer, offset, count);
            CurrentRms = 0f;
            CurrentBass = 0f;
            return count;
        }
        if (read > 0)
        {
            int sr = WaveFormat?.SampleRate ?? 44100;
            if (sr != _lastSampleRate) UpdateFilter(sr);

            double sum = 0;
            double bassSum = 0;
            float lp = _lowPassState;
            float a = _alpha;

            for (int i = offset; i < offset + read; i++)
            {
                float s = buffer[i];
                sum += s * s;
                lp += a * (s - lp);
                bassSum += lp * lp;
            }
            _lowPassState = float.IsNaN(lp) || float.IsInfinity(lp) ? 0f : lp;
            CurrentRms = (float)Math.Sqrt(sum / read);
            CurrentBass = (float)Math.Sqrt(bassSum / read);
        }
        return read;
    }
}

public class NormalizationService
{
    // Normalisation par LOUDNESS (LUFS, norme EBU R128 / ITU BS.1770) : chaque son est amene au meme volume RESSENTI.
    // Garde-fou anti-saturation : on ne remonte jamais un son au-dela du plafond de vrai pic (TruePeakCeilingDb).
    // Rien n'est modifie dans les fichiers : le gain est seulement applique a la lecture (et memorise dans le cache de l'app).
    private const double TargetLufs         = -14.0;  // volume cible (LUFS integres), standard des plateformes de streaming
    private const double TruePeakCeilingDb  = -1.0;   // plafond de vrai pic (dBTP) a ne pas depasser quand on remonte un son
    private const double PeakMaxGainDb      = 30.0;   // gain maximum accepte pour remonter un son tres faible
    private const double SilenceLufs        = -70.0;  // en dessous : quasi silence, rien a normaliser

    /// <summary>Gain (dB) a appliquer pour amener le son a TargetLufs sans depasser le plafond de vrai pic quand on le remonte.</summary>
    private static double ComputeGainDb(double loudnessLufs, double truePeakDb, double maxGainSetting)
    {
        if (double.IsNaN(loudnessLufs) || double.IsInfinity(loudnessLufs) || loudnessLufs <= SilenceLufs) return 0.0;
        double maxGain = Math.Max(maxGainSetting, PeakMaxGainDb);
        double gain = TargetLufs - loudnessLufs;
        if (gain > 0)
        {
            // Anti-saturation : le pic du son apres gain ne doit pas depasser le plafond.
            double headroom = TruePeakCeilingDb - truePeakDb;
            gain = Math.Min(gain, Math.Max(0.0, headroom));
        }
        return Math.Clamp(gain, -30.0, maxGain);
    }

    // Analyses en cours, par fichier : si le meme son est demande deux fois (analyse de fond + lecture), on partage la meme analyse.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<double>> _inflight = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Analyse un son et renvoie le gain (dB) de normalisation. highPriority : priorite normale au lieu de "en dessous de la normale"
    /// (utilise quand l'utilisateur attend le resultat, par ex. pour lancer un son pas encore analyse).</summary>
    public Task<double> AnalyzeAsync(string filePath, bool highPriority = false)
    {
        var task = _inflight.GetOrAdd(filePath, p => Task.Run(() => { try { return AnalyzeInternal(p, highPriority); } catch { return 0.0; } }));
        _ = task.ContinueWith(_ => _inflight.TryRemove(new KeyValuePair<string, Task<double>>(filePath, task)), TaskScheduler.Default);
        return task;
    }

    private double AnalyzeInternal(string filePath, bool highPriority)
    {
        double maxGainSetting = App.Settings.Current.NormalizationMaxGain;

        // NB : les tags ReplayGain ne sont volontairement pas utilises : on mesure nous-memes pour que tous les sons soient comparables.

        if (DownloadService.IsFfmpegPresent)
        {
            try
            {
                string exe = File.Exists(DownloadService.FfmpegPath) ? DownloadService.FfmpegPath : "ffmpeg";
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    // -vn/-sn/-dn : on ne decode que l'audio (sans ca, la pochette integree des mp3/flac est decodee pour rien).
                    // -threads 1 : plusieurs analyses tournent deja en parallele, un seul thread par fichier est plus efficace.
                    Arguments = $"-hide_banner -nostats -threads 1 -i \"{filePath}\" -vn -sn -dn -af ebur128=peak=sample:framelog=quiet -f null -",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    try { proc.PriorityClass = highPriority ? ProcessPriorityClass.Normal : ProcessPriorityClass.BelowNormal; } catch { }
                    string output = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    // Resume ebur128 : "I: -9.6 LUFS" (loudness integre) puis "Peak: 1.9 dBFS" (vrai pic). On prend la derniere occurrence.
                    var mI = System.Text.RegularExpressions.Regex.Matches(output, @"\bI:\s*(-?inf|[-+]?[0-9]+(?:\.[0-9]+)?)\s*LUFS");
                    var mP = System.Text.RegularExpressions.Regex.Matches(output, @"\bPeak:\s*(-?inf|[-+]?[0-9]+(?:\.[0-9]+)?)\s*dBFS");

                    if (mI.Count > 0)
                    {
                        string sI = mI[mI.Count - 1].Groups[1].Value;
                        double lufs = sI.Contains("inf") ? -120.0
                            : double.Parse(sI, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

                        double truePeak = 0.0;   // si le pic n'est pas lu : hypothese prudente (0 dBFS)
                        if (mP.Count > 0)
                        {
                            string sP = mP[mP.Count - 1].Groups[1].Value;
                            truePeak = sP.Contains("inf") ? -120.0
                                : double.Parse(sP, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        // Pic d'echantillon (2x plus rapide que le vrai pic) + 0,5 dB de marge pour les pics inter-echantillons ; le limiteur de fin de chaine rattrape le reste.
                        return ComputeGainDb(lufs, truePeak + 0.5, maxGainSetting);
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
            // Repli sans ffmpeg : mesure LUFS integree (BS.1770) + pic d'echantillon (+0,5 dB de marge pour les pics inter-echantillons).
            var (lufs, samplePeakDb) = LoudnessMeter.Measure(sampleProvider);
            if (double.IsNaN(lufs) || double.IsInfinity(lufs)) return 0.0;
            return ComputeGainDb(lufs, samplePeakDb + 0.5, maxGainSetting);
        }
        finally { reader?.Dispose(); }
    }
}

/// <summary>Mesure du loudness integre (LUFS) selon ITU-R BS.1770-4 / EBU R128 : ponderation K, blocs de 400 ms, double seuil (-70 LUFS puis -10 LU).</summary>
internal static class LoudnessMeter
{
    private struct Biquad
    {
        public double B0, B1, B2, A1, A2, Z1, Z2;
        public double Process(double x)
        {
            double y = B0 * x + Z1;
            Z1 = B1 * x - A1 * y + Z2;
            Z2 = B2 * x - A2 * y;
            return y;
        }
    }

    private static Biquad HighShelf(double fs)
    {
        double f0 = 1681.974450955533, g = 3.999843853973347, q = 0.7071752369554196;
        double k  = Math.Tan(Math.PI * f0 / fs);
        double vh = Math.Pow(10.0, g / 20.0);
        double vb = Math.Pow(vh, 0.4996667741545416);
        double a0 = 1.0 + k / q + k * k;
        return new Biquad
        {
            B0 = (vh + vb * k / q + k * k) / a0,
            B1 = 2.0 * (k * k - vh) / a0,
            B2 = (vh - vb * k / q + k * k) / a0,
            A1 = 2.0 * (k * k - 1.0) / a0,
            A2 = (1.0 - k / q + k * k) / a0
        };
    }

    private static Biquad HighPass(double fs)
    {
        double f0 = 38.13547087602444, q = 0.5003270373238773;
        double k  = Math.Tan(Math.PI * f0 / fs);
        double a0 = 1.0 + k / q + k * k;
        return new Biquad
        {
            B0 = 1.0, B1 = -2.0, B2 = 1.0,
            A1 = 2.0 * (k * k - 1.0) / a0,
            A2 = (1.0 - k / q + k * k) / a0
        };
    }

    private static double Weight(int channel, int channels)
    {
        if (channels >= 6) return channel == 3 ? 0.0 : (channel >= 4 ? 1.41 : 1.0);   // 5.1 : LFE ignore, surrounds +1,5 dB
        return 1.0;
    }

    /// <summary>Renvoie (loudness integre en LUFS, pic d'echantillon en dBFS). LUFS = -infini si le son est silencieux.</summary>
    public static (double Lufs, double PeakDb) Measure(ISampleProvider source)
    {
        int channels = Math.Max(1, source.WaveFormat.Channels);
        double fs    = source.WaveFormat.SampleRate;
        int subSize  = Math.Max(1, (int)Math.Round(fs * 0.1));   // sous-blocs de 100 ms (blocs de 400 ms = 4 sous-blocs, recouvrement 75 %)

        var shelf = new Biquad[channels];
        var hp    = new Biquad[channels];
        var weights = new double[channels];
        for (int c = 0; c < channels; c++) { shelf[c] = HighShelf(fs); hp[c] = HighPass(fs); weights[c] = Weight(c, channels); }

        var subEnergy = new List<double>();
        double acc = 0; int frames = 0, ch = 0;
        double peak = 0;
        var buffer = new float[8192];
        int read;

        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                double x = buffer[i];
                double ax = Math.Abs(x);
                if (ax > peak) peak = ax;

                double y = hp[ch].Process(shelf[ch].Process(x));
                acc += weights[ch] * y * y;

                if (++ch == channels)
                {
                    ch = 0;
                    if (++frames == subSize)
                    {
                        subEnergy.Add(acc / subSize);
                        acc = 0; frames = 0;
                    }
                }
            }
        }

        double peakDb = peak > 0 ? 20.0 * Math.Log10(peak) : -120.0;
        int n = subEnergy.Count - 3;
        if (n <= 0) return (double.NegativeInfinity, peakDb);

        var blocks = new double[n];
        for (int i = 0; i < n; i++)
            blocks[i] = (subEnergy[i] + subEnergy[i + 1] + subEnergy[i + 2] + subEnergy[i + 3]) / 4.0;

        static double ToLufs(double z) => -0.691 + 10.0 * Math.Log10(z);

        // Seuil absolu (-70 LUFS)
        double sum = 0; int count = 0;
        foreach (var z in blocks) if (z > 0 && ToLufs(z) > -70.0) { sum += z; count++; }
        if (count == 0) return (double.NegativeInfinity, peakDb);

        // Seuil relatif (-10 LU sous la moyenne des blocs retenus)
        double relThreshold = ToLufs(sum / count) - 10.0;
        sum = 0; count = 0;
        foreach (var z in blocks) if (z > 0 && ToLufs(z) > relThreshold) { sum += z; count++; }
        if (count == 0) return (double.NegativeInfinity, peakDb);

        return (ToLufs(sum / count), peakDb);
    }
}

/// <summary>Filet de securite en fin de chaine : empeche tout depassement de 0 dBFS (donc la saturation / l'ecretage) dû au gain de
/// normalisation ou a l'egaliseur. Attaque instantanee, relachement doux. Sans effet tant que le signal reste sous 0 dBFS.</summary>
public class SafetyLimiterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int   _channels;
    private readonly float _releaseCoef;
    private float _env = 1f;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public SafetyLimiterSampleProvider(ISampleProvider source)
    {
        _source   = source;
        _channels = Math.Max(1, source.WaveFormat.Channels);
        _releaseCoef = (float)(1.0 - Math.Exp(-1.0 / (0.150 * source.WaveFormat.SampleRate)));   // relachement ~150 ms
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int i = 0; i + _channels <= read; i += _channels)
        {
            float peak = 0f;
            for (int c = 0; c < _channels; c++)
            {
                float a = Math.Abs(buffer[offset + i + c]);
                if (a > peak) peak = a;
            }

            float needed = peak > 1f ? 1f / peak : 1f;
            if (needed < _env) _env = needed;                         // attaque instantanee
            else               _env += (needed - _env) * _releaseCoef; // relachement progressif
            if (_env > 0.9999f) _env = 1f;

            if (_env < 1f)
            {
                for (int c = 0; c < _channels; c++)
                {
                    float v = buffer[offset + i + c] * _env;
                    buffer[offset + i + c] = v > 1f ? 1f : (v < -1f ? -1f : v);
                }
            }
        }
        return read;
    }
}

public class PlayStatsService
{
    private readonly string _filePath;
    private Dictionary<string, int> _playCounts = new();
    private TimeSpan _totalListenTime = TimeSpan.Zero;

    public long     TotalPlays       => _playCounts.Values.Sum(v => (long)v);
    public TimeSpan TotalListenTime  => _totalListenTime;

    // Acces pour la sauvegarde (import / export des statistiques)
    public Dictionary<string, int> SnapshotCounts() => new(_playCounts);
    public long ListenSeconds => (long)_totalListenTime.TotalSeconds;

    /// <summary>Fusionne des statistiques importees : pour chaque titre on garde la valeur la plus elevee (pas de doublon si on reimporte).
    /// Ne sauvegarde pas : l'appelant appelle SaveAsync ensuite. Renvoie le nombre de titres mis a jour.</summary>
    public int MergeCounts(IEnumerable<KeyValuePair<string, int>> counts, long listenSeconds)
    {
        int updated = 0;
        foreach (var kv in counts)
        {
            if (kv.Value > GetPlayCount(kv.Key)) { _playCounts[kv.Key] = kv.Value; updated++; }
        }
        if (listenSeconds > (long)_totalListenTime.TotalSeconds) _totalListenTime = TimeSpan.FromSeconds(listenSeconds);
        return updated;
    }

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
        var dir = AppPaths.AppDataDir;
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

// Playlist (YouTube / SoundCloud) listee par yt-dlp, sans rien telecharger.
public sealed record YtPlaylistEntry(int Index, string Title, string Duration, string Url, bool Available);
public sealed record YtPlaylistInfo(string Title, List<YtPlaylistEntry> Entries);
// Resultat d'une recherche de playlists.
public sealed record YtPlaylistHit(string Title, string Info, string Url);

public class DownloadOptions
{
    /// <summary>Indices des sons de la playlist a telecharger, au format yt-dlp (ex. "1-3,7"). Vide = pas de selection.</summary>
    public string?         PlaylistItems   { get; set; }
    /// <summary>Vrai = ne telecharger que la video meme si l'URL contient aussi une playlist / un mix.</summary>
    public bool            NoPlaylist      { get; set; }
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
        onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "Téléchargement de ffmpeg..." : "Downloading ffmpeg...");
        const string zipUrl  = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        string tmpDir  = Path.Combine(Path.GetTempPath(), $"Resona_ffmpeg_{Guid.NewGuid():N}");
        string zipPath = Path.Combine(tmpDir, "ffmpeg.zip");
        try
        {
            Directory.CreateDirectory(tmpDir);

            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Resona");
            http.Timeout = TimeSpan.FromMinutes(10);
            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "Téléchargement de ffmpeg (~160 Mo)..." : "Downloading ffmpeg (~160 MB)...");

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
                onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "ffmpeg installé." : "ffmpeg installed.");
            }
            else
            {
                onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? "❌ ffmpeg.exe introuvable dans l'archive." : "❌ ffmpeg.exe not found in archive.");
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
        onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? $"Téléchargement de {name}..." : $"Downloading {name}...");
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Resona");
            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, bytes);
            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? $"{name} installé." : $"{name} installed.");
        }
        catch (Exception ex)
        {
            onProgress?.Invoke(Resona.Models.Strings.Current.IsFr ? $"Erreur lors du téléchargement de {name} : {ex.Message}" : $"Error downloading {name}: {ex.Message}");
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

    public enum YtDlpUpdateStatus { NotInstalled, Busy, UpToDate, Updated, Failed }
    public sealed record YtDlpUpdateResult(YtDlpUpdateStatus Status, string Version);

    private static int _activeDownloads;
    private static readonly SemaphoreSlim YtDlpUpdateLock = new(1, 1);
    public static bool IsDownloading => Volatile.Read(ref _activeDownloads) > 0;
    /// <summary>Vrai si yt-dlp.exe est le fichier local de l'app (c'est le seul qu'on met a jour).</summary>
    public static bool IsYtDlpLocal => File.Exists(YtDlpPath);

    private static async Task<string> ReadLocalYtDlpVersionAsync()
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = YtDlpPath, Arguments = "--version",
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                }
            };
            proc.Start();
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Trim();
        }
        catch { return string.Empty; }
    }

    /// <summary>Met a jour yt-dlp.exe avec sa commande integree (-U). Jamais pendant un telechargement.</summary>
    public static async Task<YtDlpUpdateResult> UpdateYtDlpAsync()
    {
        if (!File.Exists(YtDlpPath)) return new YtDlpUpdateResult(YtDlpUpdateStatus.NotInstalled, "");
        if (IsDownloading) return new YtDlpUpdateResult(YtDlpUpdateStatus.Busy, "");
        if (!await YtDlpUpdateLock.WaitAsync(0)) return new YtDlpUpdateResult(YtDlpUpdateStatus.Busy, "");
        try
        {
            string before = await ReadLocalYtDlpVersionAsync();
            int exitCode;
            using (var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = YtDlpPath, Arguments = "-U",
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                    WorkingDirectory = BinDir,
                }
            })
            {
                proc.Start();
                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try { await proc.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(true); } catch { }
                    return new YtDlpUpdateResult(YtDlpUpdateStatus.Failed, before);
                }
                await Task.WhenAll(outTask, errTask);
                exitCode = proc.ExitCode;
            }

            string after = await ReadLocalYtDlpVersionAsync();
            if (!string.IsNullOrEmpty(after) && after != before) return new YtDlpUpdateResult(YtDlpUpdateStatus.Updated, after);
            if (exitCode == 0) return new YtDlpUpdateResult(YtDlpUpdateStatus.UpToDate, after);
            return new YtDlpUpdateResult(YtDlpUpdateStatus.Failed, after);
        }
        catch { return new YtDlpUpdateResult(YtDlpUpdateStatus.Failed, ""); }
        finally { YtDlpUpdateLock.Release(); }
    }

    /// <summary>Mise a jour automatique (si activee dans les parametres) : au plus une verification reussie par 24 h.</summary>
    public static async Task AutoUpdateYtDlpIfDueAsync()
    {
        var s = App.Settings?.Current;
        if (s == null || !s.AutoUpdateYtDlp || !File.Exists(YtDlpPath)) return;
        if (DateTime.TryParse(s.LastYtDlpUpdateCheckUtc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var last)
            && (DateTime.UtcNow - last) < TimeSpan.FromHours(24)) return;

        var result = await UpdateYtDlpAsync();
        if (result.Status is YtDlpUpdateStatus.UpToDate or YtDlpUpdateStatus.Updated)
        {
            // SaveSync declenche SettingsChanged : on repasse par le thread UI.
            App.MainWindowInstance?.DispatcherQueue?.TryEnqueue(() =>
            {
                s.LastYtDlpUpdateCheckUtc = DateTime.UtcNow.ToString("o");
                App.Settings.SaveSync();
            });
        }
    }

    // ------------------------------------------------------------------
    //  Playlists : listage et recherche (aucun telechargement)
    // ------------------------------------------------------------------

    private static async Task<List<string>> RunYtDlpLinesAsync(string arguments, CancellationToken ct)
    {
        await EnsureBinariesAsync();
        string ytDlp = FindYtDlp();
        var lines = new List<string>();
        var errors = new List<string>();

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ytDlp,
                Arguments = "--encoding utf-8 --no-warnings " + arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        proc.OutputDataReceived += (s, e) => { if (e.Data != null) lock (lines) lines.Add(e.Data); };
        proc.ErrorDataReceived  += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (errors) errors.Add(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        try { await proc.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { proc.Kill(true); } catch { } throw; }

        if (lines.Count == 0 && proc.ExitCode != 0)
            throw new Exception(errors.Count > 0 ? errors[^1] : $"yt-dlp (code {proc.ExitCode}).");
        return lines;
    }

    private static string Clean(string? s) => string.IsNullOrWhiteSpace(s) || s.Trim() == "NA" ? string.Empty : s.Trim();

    /// <summary>Liste les sons d'une playlist (titres, durees, liens) sans les telecharger.</summary>
    public static async Task<YtPlaylistInfo> ListPlaylistAsync(string url, CancellationToken ct = default)
    {
        string safe = url.Trim().Replace("\"", "");
        var lines = await RunYtDlpLinesAsync(
            "--flat-playlist --yes-playlist --print \"%(playlist_title)s|||%(playlist_index)s|||%(title)s|||%(duration_string)s|||%(webpage_url)s|||%(url)s\" \"" + safe + "\"", ct);

        string title = string.Empty;
        var entries = new List<YtPlaylistEntry>();
        int n = 0;
        foreach (string line in lines)
        {
            string[] p = line.Split("|||");
            if (p.Length < 6) continue;
            n++;
            if (title.Length == 0) title = Clean(p[0]);
            int index = int.TryParse(p[1].Trim(), out int i) ? i : n;
            string t = Clean(p[2]);
            string link = Clean(p[4]).StartsWith("http") ? Clean(p[4]) : Clean(p[5]);
            bool available = link.StartsWith("http")
                && !t.StartsWith("[Private video]", StringComparison.OrdinalIgnoreCase)
                && !t.StartsWith("[Deleted video]", StringComparison.OrdinalIgnoreCase);
            if (t.Length == 0) t = Resona.Models.Strings.Current.IsFr ? "(sans titre)" : "(untitled)";
            entries.Add(new YtPlaylistEntry(index, t, Clean(p[3]), link, available));
        }
        return new YtPlaylistInfo(title, entries);
    }

    /// <summary>Recherche de playlists YouTube (filtre « Playlist » de la recherche YouTube).</summary>
    public static async Task<List<YtPlaylistHit>> SearchPlaylistsAsync(string query, CancellationToken ct = default)
    {
        string q = Uri.EscapeDataString(query.Replace("\"", ""));
        string url = "https://www.youtube.com/results?search_query=" + q + "&sp=EgIQAw%3D%3D";
        var lines = await RunYtDlpLinesAsync(
            "--flat-playlist --playlist-end 10 --print \"%(title)s|||%(playlist_count)s|||%(uploader)s|||%(url)s\" \"" + url + "\"", ct);

        var hits = new List<YtPlaylistHit>();
        foreach (string line in lines)
        {
            string[] p = line.Split("|||");
            if (p.Length < 4) continue;
            string link = p[3].Trim();
            if (!link.StartsWith("http") || !(link.Contains("list=") || link.Contains("/playlist"))) continue;

            var info = new List<string>();
            string count = Clean(p[1]);
            if (count.Length > 0) info.Add(count + (Resona.Models.Strings.Current.IsFr ? " sons" : " tracks"));
            string uploader = Clean(p[2]);
            if (uploader.Length > 0) info.Add(uploader);
            hits.Add(new YtPlaylistHit(Clean(p[0]), string.Join(" • ", info), link));
        }
        return hits;
    }

    public async Task DownloadAsync(string url, DownloadOptions opts, Action<string>? onProgress = null)
    {
        Interlocked.Increment(ref _activeDownloads);
        try { await DownloadCoreAsync(url, opts, onProgress); }
        finally { Interlocked.Decrement(ref _activeDownloads); }
    }

    private async Task DownloadCoreAsync(string url, DownloadOptions opts, Action<string>? onProgress)
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

        // Selection de sons d'une playlist (indices numeriques uniquement : rien d'autre n'est passe a la ligne de commande).
        string playlistArg = !string.IsNullOrWhiteSpace(opts.PlaylistItems) && System.Text.RegularExpressions.Regex.IsMatch(opts.PlaylistItems, @"^[0-9,\-]+$")
            ? $"--yes-playlist --playlist-items {opts.PlaylistItems} "
            : (opts.NoPlaylist ? "--no-playlist " : string.Empty);

        return $"--extractor-args \"youtube:player_client=android\" " +
               $"-x --audio-format {formatExt} {bitrateArg} " +
               $"--embed-thumbnail --embed-metadata " +
               $"--parse-metadata \"%(upload_date>%Y)s:%(meta_date)s\" " +
               $"--no-mtime --output \"{output}\" " +
               $"{playlistArg}" +
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
    // Paroles a ecrire dans le tag du fichier (texte brut, ou LRC si synchronisees). null = ne pas toucher.
    public string? Lyrics { get; set; }
    public double Score { get; set; }
}

public static class AutoTagService
{
    private static readonly HttpClient _http = new();
    private const string UserAgent = "Resona/2.4 (https://github.com/Resona)";

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

        // Enlever les mots-clés parasites (sans enlever cover, remix, etc. qui font partie du vrai titre)
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
        
        // Custom permissive subset check for "rosé apt" -> "ROSÉ & Bruno Mars APT."
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

            if (data.Title != null) { file.Tag.Title = data.Title.Length == 0 ? null : data.Title; changed = true; }
            if (data.Artist != null) { file.Tag.Performers = data.Artist.Length == 0 ? new string[0] : new[] { data.Artist }; changed = true; }
            if (data.Album != null) { file.Tag.Album = data.Album.Length == 0 ? null : data.Album; changed = true; }
            if (data.Genre != null) { file.Tag.Genres = data.Genre.Length == 0 ? new string[0] : new[] { data.Genre }; changed = true; }
            if (data.Year.HasValue) { file.Tag.Year = (uint)data.Year.Value; changed = true; }
            if (data.TrackNumber.HasValue) { file.Tag.Track = (uint)data.TrackNumber.Value; changed = true; }
            if (data.TrackCount.HasValue) { file.Tag.TrackCount = (uint)data.TrackCount.Value; changed = true; }
            if (data.AlbumArtist != null) { file.Tag.AlbumArtists = new[] { data.AlbumArtist }; changed = true; }

            if (!string.IsNullOrWhiteSpace(data.Lyrics) && !string.Equals(file.Tag.Lyrics, data.Lyrics, StringComparison.Ordinal))
            {
                file.Tag.Lyrics = data.Lyrics;
                changed = true;
            }

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
    private static readonly string FpcalcDir = AppPaths.FpcalcDir;
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








