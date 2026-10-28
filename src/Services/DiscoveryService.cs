using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Resona.Models;

namespace Resona.Services;

/// <summary>Artiste renvoyé par Deezer.</summary>
public sealed class RelatedArtist
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
}

public sealed class RelatedArtistsResult
{
    /// <summary>Faux si l'artiste n'a pas été trouvé sur Deezer.</summary>
    public bool ArtistFound { get; set; }
    public long ArtistId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public List<RelatedArtist> Related { get; set; } = new();
}

/// <summary>Un artiste suggéré, avec ses titres présents dans la bibliothèque (vide = pas dans la bibliothèque).</summary>
public sealed class SimilarArtistResult
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public List<Track> LibraryTracks { get; } = new();
}

/// <summary>
/// Mode Discovery : artistes similaires via l'API publique de Deezer (sans clé ni compte).
/// Les résultats sont gardés 7 jours dans un petit fichier JSON (un artiste introuvable : 1 jour), donc hors connexion
/// ou à la 2e demande aucun appel réseau n'est fait. Seul le NOM de l'artiste est envoyé à Deezer.
/// </summary>
public static class DiscoveryService
{
    private const string Api = "https://api.deezer.com";
    private const int MaxCacheEntries = 500;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan NotFoundTtl = TimeSpan.FromDays(1);

    private static readonly HttpClient Http = CreateHttp();

    private static readonly string CachePath = Path.Combine(AppPaths.AppDataDir, "similar_artists_cache.json");

    private sealed class CacheEntry
    {
        public DateTime FetchedUtc { get; set; }
        public long ArtistId { get; set; }              // 0 = artiste introuvable sur Deezer
        public string ArtistName { get; set; } = string.Empty;
        public List<RelatedArtist> Related { get; set; } = new();
    }

    private static readonly object CacheLock = new();
    private static readonly SemaphoreSlim SaveLock = new(1, 1);
    private static Dictionary<string, CacheEntry>? _cache;

    // Un artiste écrit « A feat. B », « A & B », « A, B », « A; B », « A x B » : on essaie aussi le premier nom.
    private static readonly Regex FirstArtist = new(
        @"^(?<first>.+?)(?:\s*[;,&/]\s*|\s+(?:feat\.?|ft\.?|featuring|with|x|vs\.?)\s+).+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Resona/1.0");
        return http;
    }

    // ------------------------------------------------------------------
    //  Artistes similaires
    // ------------------------------------------------------------------

    /// <summary>
    /// Artistes similaires à l'artiste donné. Lève une exception en cas de problème réseau ou de quota Deezer
    /// (rien n'est alors mis en cache) ; un artiste introuvable renvoie ArtistFound = false.
    /// </summary>
    public static async Task<RelatedArtistsResult> GetRelatedAsync(string artist, CancellationToken ct = default)
    {
        string cleaned = (artist ?? string.Empty).Trim();
        string key = FuzzyTrackMatcher.NormalizeText(cleaned);
        if (key.Length == 0) return new RelatedArtistsResult();

        if (TryGetCached(key, out CacheEntry? cached) && cached != null) return ToResult(cached);

        RelatedArtist? found = null;
        foreach (string query in QueryCandidates(cleaned))
        {
            found = await FindArtistAsync(query, ct);
            if (found != null) break;
        }

        var entry = new CacheEntry { FetchedUtc = DateTime.UtcNow };
        if (found != null)
        {
            entry.ArtistId = found.Id;
            entry.ArtistName = found.Name;
            entry.Related = await GetRelatedByIdAsync(found.Id, ct);
        }

        Store(key, entry);
        return ToResult(entry);
    }

    private static IEnumerable<string> QueryCandidates(string raw)
    {
        yield return raw;
        Match m = FirstArtist.Match(raw);
        if (m.Success)
        {
            string first = m.Groups["first"].Value.Trim();
            if (first.Length > 0 && !string.Equals(first, raw, StringComparison.OrdinalIgnoreCase)) yield return first;
        }
    }

    private static async Task<RelatedArtist?> FindArtistAsync(string query, CancellationToken ct)
    {
        var results = await GetArtistsAsync($"{Api}/search/artist?q={Uri.EscapeDataString(query)}&limit=10", ct);

        string nq = FuzzyTrackMatcher.NormalizeText(query);
        RelatedArtist? best = null;
        double bestScore = 0;
        foreach (RelatedArtist r in results)
        {
            string nr = FuzzyTrackMatcher.NormalizeText(r.Name);
            double score = nr == nq || nr.Replace(" ", string.Empty) == nq.Replace(" ", string.Empty)
                ? 1.0
                : FuzzyTrackMatcher.LevenshteinSimilarity(nr, nq);
            if (score > bestScore)   // à égalité on garde le premier (le plus populaire pour Deezer)
            {
                bestScore = score;
                best = r;
            }
        }
        return bestScore >= 0.88 ? best : null;
    }

    private static Task<List<RelatedArtist>> GetRelatedByIdAsync(long artistId, CancellationToken ct)
        => GetArtistsAsync($"{Api}/artist/{artistId}/related?limit=40", ct);

    private static async Task<List<RelatedArtist>> GetArtistsAsync(string url, CancellationToken ct)
    {
        using HttpResponseMessage response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(ct);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        // Deezer répond parfois 200 avec { "error": { "message": ... } } (quota dépassé...).
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out JsonElement error))
        {
            string message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out JsonElement m) ? m.GetString() ?? "erreur" : "erreur";
            throw new HttpRequestException("Deezer : " + message);
        }

        var list = new List<RelatedArtist>();
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
            return list;

        foreach (JsonElement el in data.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            if (!el.TryGetProperty("id", out JsonElement idEl) || !idEl.TryGetInt64(out long id)) continue;
            string name = el.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
            if (name.Length == 0) continue;

            string? picture = null;
            if (el.TryGetProperty("picture_medium", out JsonElement pic) && pic.ValueKind == JsonValueKind.String) picture = pic.GetString();
            else if (el.TryGetProperty("picture", out JsonElement pic2) && pic2.ValueKind == JsonValueKind.String) picture = pic2.GetString();

            list.Add(new RelatedArtist { Id = id, Name = name, Picture = picture });
        }
        return list;
    }

    private static RelatedArtistsResult ToResult(CacheEntry e) => new()
    {
        ArtistFound = e.ArtistId != 0,
        ArtistId = e.ArtistId,
        ArtistName = e.ArtistName,
        Related = e.Related.ToList()
    };

    // ------------------------------------------------------------------
    //  Correspondance avec la bibliothèque
    // ------------------------------------------------------------------

    // « The Beatles » = « Beatles » = « beatles » ; « AC/DC » = « ACDC ».
    private static string ArtistKey(string? name)
    {
        string n = FuzzyTrackMatcher.NormalizeText(name);
        if (n.StartsWith("the ", StringComparison.Ordinal)) n = n.Substring(4);
        return n.Replace(" ", string.Empty);
    }

    /// <summary>Associe chaque artiste suggéré aux titres de la bibliothèque (nom identique, sans flou : mieux vaut manquer un artiste que se tromper).</summary>
    public static List<SimilarArtistResult> Annotate(IEnumerable<RelatedArtist> related, IEnumerable<Track> library)
    {
        // artiste (nom complet ou chaque artiste d'un « A feat. B ») -> titres
        var index = new Dictionary<string, List<Track>>(StringComparer.Ordinal);
        void Add(string key, Track t)
        {
            if (key.Length < 2) return;
            if (!index.TryGetValue(key, out var list)) index[key] = list = new List<Track>();
            list.Add(t);
        }

        foreach (Track t in library)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.FilePath) || string.IsNullOrWhiteSpace(t.Artist)) continue;
            Add(ArtistKey(t.Artist), t);
            foreach (string part in FuzzyTrackMatcher.SplitArtists(t.Artist)) Add(ArtistKey(part), t);
        }

        var results = new List<SimilarArtistResult>();
        var seenIds = new HashSet<long>();
        foreach (RelatedArtist r in related)
        {
            if (!seenIds.Add(r.Id)) continue;
            var item = new SimilarArtistResult { Id = r.Id, Name = r.Name, PictureUrl = r.Picture };
            if (index.TryGetValue(ArtistKey(r.Name), out var tracks))
            {
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Track t in tracks)
                    if (seenPaths.Add(t.FilePath)) item.LibraryTracks.Add(t);
            }
            results.Add(item);
        }
        return results;
    }

    // ------------------------------------------------------------------
    //  Cache disque
    // ------------------------------------------------------------------

    private static Dictionary<string, CacheEntry> LoadCacheLocked()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(CachePath))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(CachePath));
                if (loaded != null)
                {
                    _cache = new Dictionary<string, CacheEntry>(loaded, StringComparer.Ordinal);
                    return _cache;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Write("Discovery", ex, "Lecture du cache des artistes similaires");
        }
        _cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        return _cache;
    }

    private static bool TryGetCached(string key, out CacheEntry? entry)
    {
        lock (CacheLock)
        {
            var cache = LoadCacheLocked();
            if (cache.TryGetValue(key, out CacheEntry? e) && e != null)
            {
                TimeSpan ttl = e.ArtistId == 0 ? NotFoundTtl : CacheTtl;
                if (DateTime.UtcNow - e.FetchedUtc < ttl)
                {
                    entry = e;
                    return true;
                }
            }
        }
        entry = null;
        return false;
    }

    private static void Store(string key, CacheEntry entry)
    {
        string json;
        lock (CacheLock)
        {
            var cache = LoadCacheLocked();
            cache[key] = entry;
            if (cache.Count > MaxCacheEntries)
            {
                foreach (string oldKey in cache.OrderBy(kv => kv.Value.FetchedUtc).Take(cache.Count - MaxCacheEntries).Select(kv => kv.Key).ToList())
                    cache.Remove(oldKey);
            }
            json = JsonSerializer.Serialize(cache);
        }
        _ = Task.Run(async () =>
        {
            await SaveLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                string tmp = CachePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, CachePath, overwrite: true);
            }
            catch (Exception ex)
            {
                LogService.Write("Discovery", ex, "Ecriture du cache des artistes similaires");
            }
            finally { SaveLock.Release(); }
        });
    }
}
