using System;
using System.Collections.Concurrent;
using System.Text;
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

        // URL de pochette publique par album ("" = aucune trouvée) : une seule recherche par album et par session.
        private readonly ConcurrentDictionary<string, string> _coverUrlCache = new();
        private int _updateToken;

        public void Initialize()
        {
            if (!App.Settings.Current.EnableDiscordRichPresence) return;
            if (_client != null) return;

            _client = new DiscordRpcClient(_applicationId);
            _client.Initialize();
        }

        /// <param name="position">Position de lecture actuelle. Si absente, on lit celle du moteur audio (a eviter juste au demarrage d'un son : passer TimeSpan.Zero).</param>
        public void UpdatePresence(Track? track, bool isPlaying, TimeSpan? position = null)
        {
            var client = _client;
            if (client == null || !App.Settings.Current.EnableDiscordRichPresence) return;

            int token = Interlocked.Increment(ref _updateToken);

            if (track == null)
            {
                SetIdle(client);
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
            bool known = _coverUrlCache.TryGetValue(key, out string? cached);
            Send(client, track, isPlaying, start, end, known && !string.IsNullOrEmpty(cached) ? cached : null);

            // 2) Pochette pas encore cherchee : on la cherche puis on renvoie la presence (memes horodatages, donc la barre ne bouge pas).
            if (!known)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string? url = await ResolveCoverUrlAsync(track, key);
                        if (!string.IsNullOrEmpty(url) && token == Volatile.Read(ref _updateToken) && _client == client)
                        {
                            Send(client, track, isPlaying, start, end, url);
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

        private static string CoverKey(Track track)
            => CoverArtService.GetAlbumCacheKey(track.Artist, track.Album, track.Id);

        private async Task<string?> ResolveCoverUrlAsync(Track track, string key)
        {
            string? url = null;
            try
            {
                string query = App.CoverArt.BuildCoverSearchQuery(track.Title, track.Artist, track.Album);
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var results = await App.CoverArt.SearchAppleMusicImagesAsync(query, 1);
                    // Discord : URL en https, 256 caracteres maximum.
                    if (results != null && results.Count > 0
                        && results[0].StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        && results[0].Length <= 250)
                    {
                        url = results[0];
                    }
                }
            }
            catch { }

            _coverUrlCache[key] = url ?? "";
            return url;
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
