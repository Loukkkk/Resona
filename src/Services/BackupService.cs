using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Resona.Models;

namespace Resona.Services;

public static class BackupService
{
    private static readonly string BackupFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "ai_backup.json");

    public class TrackBackup
    {
        public string FilePath { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string AlbumArtist { get; set; } = "";
        public string Genre { get; set; } = "";
        public uint Year { get; set; }
    }

    public static async Task SaveBackupAsync(List<Track> tracks)
    {
        try
        {
            var backups = new List<TrackBackup>();
            foreach (var t in tracks)
            {
                backups.Add(new TrackBackup
                {
                    FilePath = t.FilePath,
                    Title = t.Title ?? "",
                    Artist = t.Artist ?? "",
                    Album = t.Album ?? "",
                    AlbumArtist = t.AlbumArtist ?? "",
                    Genre = t.Genre ?? "",
                    Year = (uint)t.Year
                });
            }

            string dir = Path.GetDirectoryName(BackupFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(backups);
            await File.WriteAllTextAsync(BackupFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Backup Error] {ex.Message}");
        }
    }

    public static async Task<bool> RestoreBackupAsync()
    {
        try
        {
            if (!File.Exists(BackupFilePath)) return false;

            string json = await File.ReadAllTextAsync(BackupFilePath);
            var backups = JsonSerializer.Deserialize<List<TrackBackup>>(json);
            if (backups == null || backups.Count == 0) return false;

            var allTracks = await App.Cache.LoadAllTracksAsync();
            var tracksDict = allTracks.GroupBy(t => t.FilePath).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            bool anyRestored = false;

            foreach (var b in backups)
            {
                if (File.Exists(b.FilePath))
                {
                    try
                    {
                        var file = TagLib.File.Create(b.FilePath);
                        file.Tag.Title = b.Title;
                        file.Tag.Performers = new[] { b.Artist };
                        file.Tag.Album = b.Album;
                        file.Tag.AlbumArtists = new[] { b.AlbumArtist };
                        file.Tag.Genres = new[] { b.Genre };
                        file.Tag.Year = b.Year;
                        file.Save();

                        if (tracksDict.TryGetValue(b.FilePath, out var track))
                        {
                            track.Title = b.Title;
                            track.Artist = b.Artist;
                            track.Album = b.Album;
                            track.AlbumArtist = b.AlbumArtist;
                            track.Genre = b.Genre;
                            track.Year = (int)b.Year;
                            await App.Cache.UpsertTrackAsync(track);
                        }
                        anyRestored = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Restore Error Item] {b.FilePath} : {ex.Message}");
                    }
                }
            }

            if (anyRestored)
            {
                File.Delete(BackupFilePath);
            }
            return anyRestored;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Restore Error] {ex.Message}");
            return false;
        }
    }

    public static bool HasBackup() => File.Exists(BackupFilePath);
}


