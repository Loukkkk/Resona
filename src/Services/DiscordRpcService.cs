using System;
using DiscordRPC;
using Resona.Models;

namespace Resona.Services
{
    public class DiscordRpcService : IDisposable
    {
        private DiscordRpcClient? _client;
        private readonly string _applicationId = "1546162343791042690";

        public void Initialize()
        {
            if (!App.Settings.Current.EnableDiscordRichPresence) return;
            if (_client != null) return;

            _client = new DiscordRpcClient(_applicationId);
            _client.Initialize();
        }

        public void UpdatePresence(Track? track, bool isPlaying)
        {
            if (_client == null || !App.Settings.Current.EnableDiscordRichPresence) return;

            if (track == null || !isPlaying)
            {
                _client.SetPresence(new RichPresence()
                {
                    Details = Resona.Models.Strings.Current.Discord_Idle,
                    State = Resona.Models.Strings.Current.Discord_Browsing,
                    Assets = new Assets()
                    {
                        LargeImageKey = "logo", // Nom de l'asset sur le portail Discord
                        LargeImageText = "Resona"
                    }
                });
                return;
            }

            _client.SetPresence(new RichPresence()
            {
                Details = track.Title,
                State = string.IsNullOrEmpty(track.Album) ? track.Artist : $"{track.Artist} - {track.Album}",
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    LargeImageText = "Resona"
                }
            });
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
