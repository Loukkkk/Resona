using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using Resona.Models;

namespace Resona.Services
{
    /// <summary>
    /// Discord Rich Presence : "Écoute Resona" avec barre de progression (horodatages début/fin calculés d'après la position réelle),
    /// vraie pochette de l'album (recherchée en ligne, URL publique : Discord ne peut pas afficher un fichier local) et logo Resona en petit badge.
    /// </summary>
    public class DiscordRpcService : IDisposable
    {
        private DiscordRpcClient? _client;
        private readonly string _applicationId = "1546162343791042690";

        // URL de pochette publique par pochette locale (chemin du fichier de cover de Resona ; "" = aucune correspondance sure) :
        // une seule recherche par pochette et par session.
        private readonly ConcurrentDictionary<string, string> _coverUrlCache = new();
        private int _updateToken;

        private static readonly HttpClient _http = CreateHttp();
        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            return c;
        }

        // Ecart maximal (sur 64 bits) entre l'empreinte visuelle de la pochette de Resona et celle d'un candidat pour les considerer identiques.
        private const int MaxHashDistance = 10;

        // --- Envoi des pochettes sur un hebergeur d'images temporaire (quand aucune correspondance en ligne) ---
        internal sealed record UploadEntry(string Url, DateTime ExpiresUtc);
        private readonly ConcurrentDictionary<string, UploadEntry> _uploads = new();
        private static readonly string _uploadsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "discord_covers.json");

        // Un seul envoi a la fois vers Discord, dans l'ordre : empeche une mise a jour tardive (ancien son) d'ecraser la plus recente.
        private readonly object _sendLock = new();

        // Filet de securite : si la presence ne correspond plus au son reellement joue, on la renvoie.
        private Timer? _watchdog;
        private volatile string? _lastTrackId;
        private volatile bool _lastPlaying;
        private int _mismatchTicks;

        public DiscordRpcService()
        {
            LoadUploads();
        }

        public void Initialize()
        {
            if (!App.Settings.Current.EnableDiscordRichPresence) return;
            if (_client != null) return;

            _client = new DiscordRpcClient(_applicationId);
            _client.Initialize();
            _watchdog = new Timer(_ => Watchdog(), null, 8000, 8000);
        }

        private void Watchdog()
        {
            try
            {
                if (_client == null || !App.Settings.Current.EnableDiscordRichPresence) return;
                var cur = App.AudioEngine.CurrentTrack;
                bool playing = (int)App.AudioEngine.State == 1;
                if (cur?.Id == _lastTrackId && (cur == null || playing == _lastPlaying))
                {
                    _mismatchTicks = 0;
                    return;
                }
                // Deux constats d'affilee (un son en cours de chargement ne doit pas declencher un renvoi de l'ancien).
                if (++_mismatchTicks < 2) return;
                _mismatchTicks = 0;
                UpdatePresence(cur, playing);
            }
            catch { }
        }

        /// <param name="position">Position de lecture actuelle. Si absente, on lit celle du moteur audio (a eviter juste au demarrage d'un son : passer TimeSpan.Zero).</param>
        public void UpdatePresence(Track? track, bool isPlaying, TimeSpan? position = null)
        {
            var client = _client;
            if (client == null || !App.Settings.Current.EnableDiscordRichPresence) return;

            int token = Interlocked.Increment(ref _updateToken);
            _lastTrackId = track?.Id;
            _lastPlaying = isPlaying;

            if (track == null)
            {
                lock (_sendLock) { SetIdle(client); }
                return;
            }

            // Barre de progression : debut = maintenant - position ; fin = debut + duree. Pas d'horodatage en pause (le compteur doit s'arreter).
            DateTime? start = null, end = null;
            TimeSpan duration = track.Duration;
            if (isPlaying && duration > TimeSpan.Zero)
            {
                TimeSpan pos = position ?? SafePosition();
                if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
                if (pos > duration) pos = duration;
                start = DateTime.UtcNow - pos;
                end = start + duration;
            }

            // 1) Envoi immediat (avec la pochette si on la connait deja), pour ne jamais faire attendre le changement de son.
            string key = CoverKey(track);
            string? cached = null;
            // Pas de pochette dans Resona (ou fichier absent) : logo Resona, aucune recherche (on n'affiche jamais une pochette "au hasard").
            bool known = key.Length == 0 || _coverUrlCache.TryGetValue(key, out cached);
            lock (_sendLock)
            {
                Send(client, track, isPlaying, start, end, known && !string.IsNullOrEmpty(cached) ? cached : null);
            }

            // 2) Pochette pas encore cherchee : on la cherche puis on renvoie la presence (memes horodatages, donc la barre ne bouge pas).
            if (!known)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string? url = await ResolveCoverUrlAsync(track, key);
                        if (!string.IsNullOrEmpty(url))
                        {
                            lock (_sendLock)
                            {
                                // Verifie sous verrou : si un son plus recent a ete envoye entre-temps, on n'ecrase rien.
                                if (token == Volatile.Read(ref _updateToken) && _client == client)
                                {
                                    Send(client, track, isPlaying, start, end, url);
                                }
                            }
                        }
                    }
                    catch { }
                });
            }
        }

        private static TimeSpan SafePosition()
        {
            try { return App.AudioEngine.CurrentPosition; }
            catch { return TimeSpan.Zero; }
        }

        // Cle de cache = fichier de pochette affiche dans Resona (et non artiste+album : des sons "inconnus" ou d'un meme album
        // ayant des pochettes differentes se retrouvaient avec la pochette d'un autre son).
        private static string CoverKey(Track track)
            => !string.IsNullOrEmpty(track.CoverArtPath) && File.Exists(track.CoverArtPath) ? track.CoverArtPath : "";

        private async Task<string?> ResolveCoverUrlAsync(Track track, string localCoverPath)
        {
            string? url = null;
            bool definitive = true;   // false si un probleme reseau empeche de conclure : on ne memorise pas "aucune pochette" dans ce cas

            try
            {
                ulong? localHash = DHash(await File.ReadAllBytesAsync(localCoverPath));
                if (localHash == null)
                {
                    _coverUrlCache[localCoverPath] = "";
                    return null;
                }

                var seen = new HashSet<string>();
                foreach (var (term, entity) in BuildQueries(track))
                {
                    List<string> thumbs;
                    try { thumbs = await SearchAppleThumbsAsync(term, entity); }
                    catch { definitive = false; continue; }

                    var fresh = new List<string>();
                    foreach (var t in thumbs) if (seen.Add(t)) fresh.Add(t);

                    // Chaque candidat est compare a la VRAIE pochette de Resona (empreinte visuelle) : seule une correspondance est acceptee.
                    var checks = fresh.ConvertAll(async t =>
                    {
                        try { return (thumb: t, hash: DHash(await _http.GetByteArrayAsync(t))); }
                        catch { definitive = false; return (thumb: t, hash: (ulong?)null); }
                    });
                    var results = await Task.WhenAll(checks);

                    int best = int.MaxValue;
                    string? bestThumb = null;
                    foreach (var (thumb, hash) in results)
                    {
                        if (hash == null) continue;
                        int dist = System.Numerics.BitOperations.PopCount(hash.Value ^ localHash.Value);
                        if (dist < best) { best = dist; bestThumb = thumb; }
                    }

                    if (bestThumb != null && best <= MaxHashDistance)
                    {
                        string full = bestThumb.Replace("100x100bb", "600x600bb");
                        // Discord : URL en https, 256 caracteres maximum.
                        if (full.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && full.Length <= 250)
                        {
                            url = full;
                            break;
                        }
                    }
                }
            }
            catch { definitive = false; }

            // Pas de correspondance en ligne : envoi de la vraie pochette sur un hebergeur d'images temporaire (la description de l'option Discord l'explique).
            if (url == null)
            {
                try { url = await UploadCoverAsync(localCoverPath); }
                catch { definitive = false; }   // probleme reseau : on reessaiera au prochain changement de son
            }

            if (url != null || definitive) _coverUrlCache[localCoverPath] = url ?? "";
            return url;
        }

        private static string FileKey(string path)
        {
            var fi = new FileInfo(path);
            return path + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
        }

        // Hebergeurs d'images temporaires essayes dans l'ordre : le premier qui renvoie un vrai lien d'image est utilise.
        private sealed record UploadHost(string Url, string FileField, (string Key, string Value)[] Fields, string HostSuffix, TimeSpan Validity, string? UserAgent = null);
        private static readonly UploadHost[] UploadHosts =
        {
            new("https://uguu.se/upload?output=text", "files[]", Array.Empty<(string, string)>(), "uguu.se", TimeSpan.FromMinutes(170)),
            new("https://litterbox.catbox.moe/resources/internals/api.php", "fileToUpload", new[] { ("reqtype", "fileupload"), ("time", "72h") }, "catbox.moe", TimeSpan.FromHours(70)),
        };

        private async Task<string?> UploadCoverAsync(string localPath)
        {
            string key = FileKey(localPath);
            if (_uploads.TryGetValue(key, out var known) && known.ExpiresUtc > DateTime.UtcNow) return known.Url;

            byte[]? jpeg = ResizeToJpeg(await File.ReadAllBytesAsync(localPath), 512);
            if (jpeg == null) return null;

            foreach (var host in UploadHosts)
            {
                try
                {
                    string? link = await TryUploadAsync(host, jpeg);
                    if (link == null) continue;
                    _uploads[key] = new UploadEntry(link, DateTime.UtcNow + host.Validity);
                    SaveUploads();
                    return link;
                }
                catch { /* hebergeur indisponible : on essaie le suivant */ }
            }
            return null;
        }

        private async Task<string?> TryUploadAsync(UploadHost host, byte[] jpeg)
        {
            using var form = new MultipartFormDataContent();
            foreach (var (k, v) in host.Fields) form.Add(new StringContent(v), k);
            var file = new ByteArrayContent(jpeg);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, host.FileField, Guid.NewGuid().ToString("N") + ".jpg");   // nom aleatoire : rien du fichier d'origine n'est envoye

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var req = new HttpRequestMessage(HttpMethod.Post, host.Url) { Content = form };
            if (host.UserAgent != null) { req.Headers.UserAgent.Clear(); req.Headers.UserAgent.ParseAdd(host.UserAgent); }
            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            string text = (await resp.Content.ReadAsStringAsync(cts.Token)).Trim();
            string link = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];

            // La reponse doit etre un lien https du bon hebergeur (une page d'erreur / de pare-feu n'est PAS acceptee), de taille compatible Discord.
            if (link.Length > 250 || !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
            if (!(uri.Host == host.HostSuffix || uri.Host.EndsWith("." + host.HostSuffix, StringComparison.OrdinalIgnoreCase))) return null;

            // Et ce lien doit vraiment servir une image.
            using var check = new HttpRequestMessage(HttpMethod.Get, uri);
            using var checkResp = await _http.SendAsync(check, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!checkResp.IsSuccessStatusCode) return null;
            string? type = checkResp.Content.Headers.ContentType?.MediaType;
            return type != null && type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? link : null;
        }

        /// <summary>Carre centre 512x512 en JPEG : image legere, sans aucune metadonnee du fichier d'origine.</summary>
        private static byte[]? ResizeToJpeg(byte[] data, int size)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var src = new System.Drawing.Bitmap(ms);
                int side = Math.Min(src.Width, src.Height);
                using var dst = new System.Drawing.Bitmap(size, size);
                using (var g = System.Drawing.Graphics.FromImage(dst))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(src,
                        new System.Drawing.Rectangle(0, 0, size, size),
                        new System.Drawing.Rectangle((src.Width - side) / 2, (src.Height - side) / 2, side, side),
                        System.Drawing.GraphicsUnit.Pixel);
                }
                using var outMs = new MemoryStream();
                dst.Save(outMs, System.Drawing.Imaging.ImageFormat.Jpeg);
                return outMs.ToArray();
            }
            catch { return null; }
        }

        private void LoadUploads()
        {
            try
            {
                if (!File.Exists(_uploadsFile)) return;
                var data = JsonSerializer.Deserialize<Dictionary<string, UploadEntry>>(File.ReadAllText(_uploadsFile));
                if (data == null) return;
                foreach (var kv in data)
                    if (kv.Value.ExpiresUtc > DateTime.UtcNow) _uploads[kv.Key] = kv.Value;
            }
            catch { }
        }

        private void SaveUploads()
        {
            try
            {
                foreach (var kv in _uploads)
                    if (kv.Value.ExpiresUtc <= DateTime.UtcNow) _uploads.TryRemove(kv.Key, out _);
                Directory.CreateDirectory(Path.GetDirectoryName(_uploadsFile)!);
                File.WriteAllText(_uploadsFile, JsonSerializer.Serialize(new Dictionary<string, UploadEntry>(_uploads)));
            }
            catch { }
        }

        private static string Unknown(string? s)
        {
            s = (s ?? "").Trim();
            string l = s.ToLowerInvariant();
            return l.Contains("inconnu") || l.Contains("unknown") ? "" : s;
        }

        private static List<(string term, string entity)> BuildQueries(Track track)
        {
            string title = Unknown(track.Title), artist = Unknown(track.Artist), album = Unknown(track.Album);
            var list = new List<(string, string)>();
            void Add(string term, string entity)
            {
                term = System.Text.RegularExpressions.Regex.Replace(term, @"\s+", " ").Trim();
                if (term.Length > 0 && !list.Contains((term, entity))) list.Add((term, entity));
            }

            if (album.Length > 0) Add(artist + " " + album, "album");
            if (title.Length > 0) Add(title + " " + artist, "song");
            if (title.Length > 0) Add(title + " " + artist, "album");
            if (album.Length > 0) Add(album, "album");
            return list;
        }

        private static async Task<List<string>> SearchAppleThumbsAsync(string term, string entity)
        {
            var result = new List<string>();
            string api = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(term)}&entity={entity}&limit=10";
            string json = await _http.GetStringAsync(api);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("artworkUrl100", out var art))
                    {
                        string? u = art.GetString();
                        if (!string.IsNullOrEmpty(u) && !result.Contains(u)) result.Add(u);
                    }
                }
            }
            return result;
        }

        private static ulong? DHash(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var src = new System.Drawing.Bitmap(ms);
                using var small = new System.Drawing.Bitmap(9, 8);
                using (var g = System.Drawing.Graphics.FromImage(small))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new System.Drawing.Rectangle(0, 0, 9, 8));
                }

                static double Lum(System.Drawing.Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                ulong hash = 0;
                int bit = 0;
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        if (Lum(small.GetPixel(x, y)) > Lum(small.GetPixel(x + 1, y))) hash |= 1UL << bit;
                        bit++;
                    }
                }
                return hash;
            }
            catch { return null; }
        }

        private static void Send(DiscordRpcClient client, Track track, bool isPlaying, DateTime? start, DateTime? end, string? coverUrl)
        {
            try
            {
                bool fr = Strings.Current.IsFr;
                string artist = string.IsNullOrWhiteSpace(track.Artist) ? "" : track.Artist;
                string byArtist = string.IsNullOrEmpty(artist) ? "" : (fr ? "de " : "by ") + artist;

                string state = isPlaying
                    ? byArtist
                    : (fr ? "⏸ En pause" : "⏸ Paused") + (string.IsNullOrEmpty(artist) ? "" : " • " + artist);
                if (string.IsNullOrWhiteSpace(state)) state = "Resona";

                string album = string.IsNullOrWhiteSpace(track.Album) ? "Resona" : track.Album;

                var presence = new RichPresence
                {
                    Type = ActivityType.Listening,
                    Details = Clip(track.Title, 128),
                    State = Clip(state, 128),
                    Assets = new Assets
                    {
                        LargeImageKey = coverUrl ?? "logo",           // vraie pochette (URL) ou logo Resona (asset du portail Discord)
                        LargeImageText = Clip(album, 128),
                        SmallImageKey = coverUrl != null ? "logo" : null,   // petit badge Resona quand la pochette occupe la grande image
                        SmallImageText = coverUrl != null ? "Resona" : null
                    }
                };

                if (start.HasValue && end.HasValue)
                {
                    presence.Timestamps = new Timestamps(start.Value, end.Value);
                }

                client.SetPresence(presence);
            }
            catch { }
        }

        private static void SetIdle(DiscordRpcClient client)
        {
            try
            {
                client.SetPresence(new RichPresence()
                {
                    Details = Resona.Models.Strings.Current.Discord_Idle,
                    State = Resona.Models.Strings.Current.Discord_Browsing,
                    Assets = new Assets()
                    {
                        LargeImageKey = "logo", // Nom de l'asset sur le portail Discord
                        LargeImageText = "Resona"
                    }
                });
            }
            catch { }
        }

        /// <summary>Discord impose 2 a 128 octets (UTF-8) par champ de texte : on tronque proprement et on complete si trop court.</summary>
        private static string Clip(string? text, int maxBytes)
        {
            text = (text ?? "").Trim();
            if (text.Length < 2) text = text.PadRight(2, ' ');

            if (Encoding.UTF8.GetByteCount(text) <= maxBytes) return text;

            var sb = new StringBuilder();
            int bytes = 0;
            foreach (Rune r in text.EnumerateRunes())   // par caractere Unicode complet (pas de paire de substitution coupee en deux)
            {
                int b = r.Utf8SequenceLength;
                if (bytes + b > maxBytes - 3) break;   // 3 octets reserves pour les points de suspension
                sb.Append(r.ToString());
                bytes += b;
            }
            return sb.Append("...").ToString();
        }

        public void Deinitialize()
        {
            _watchdog?.Dispose();
            _watchdog = null;
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public void Dispose()
        {
            Deinitialize();
        }
    }
}
