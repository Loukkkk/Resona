using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Resona.Services;

/// <summary>
/// Paroles choisies a la main par l'utilisateur pour un morceau (selecteur de l'overlay des paroles).
/// Elles passent AVANT toute recherche automatique et tout controle de coherence : c'est l'utilisateur qui a tranche.
/// Stockage : %LOCALAPPDATA%\Resona\lyrics_overrides.json, indexe par chemin du fichier.
/// </summary>
public static class LyricsOverrideService
{
    public class Entry
    {
        public string Lyrics { get; set; } = string.Empty;
        public bool Synced { get; set; }
        /// <summary>"Artiste - Titre" de la fiche choisie (information, pour les logs).</summary>
        public string Label { get; set; } = string.Empty;
    }

    private static readonly object _lock = new();
    private static Dictionary<string, Entry>? _entries;

    private static string FilePath => Path.Combine(AppPaths.AppDataDir, "lyrics_overrides.json");

    private static Dictionary<string, Entry> Entries()
    {
        if (_entries != null) return _entries;
        var loaded = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(FilePath));
                if (data != null)
                {
                    foreach (var kv in data) loaded[kv.Key] = kv.Value;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Write("Lyrics", $"Lecture des paroles choisies impossible : {ex.Message}");
        }
        _entries = loaded;
        return loaded;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Entries(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Write("Lyrics", $"Ecriture des paroles choisies impossible : {ex.Message}");
        }
    }

    public static bool TryGet(string? trackPath, out Entry entry)
    {
        entry = new Entry();
        if (string.IsNullOrWhiteSpace(trackPath)) return false;
        lock (_lock)
        {
            if (Entries().TryGetValue(trackPath, out var e) && !string.IsNullOrWhiteSpace(e.Lyrics))
            {
                entry = e;
                return true;
            }
        }
        return false;
    }

    public static void Set(string trackPath, string lyrics, bool synced, string label)
    {
        if (string.IsNullOrWhiteSpace(trackPath) || string.IsNullOrWhiteSpace(lyrics)) return;
        lock (_lock)
        {
            Entries()[trackPath] = new Entry { Lyrics = lyrics, Synced = synced, Label = label };
            Save();
        }
    }

    /// <summary>Supprime TOUTES les paroles personnalisees (choisies ou modifiees a la main). Renvoie les chemins concernes.</summary>
    public static List<string> ClearAll()
    {
        lock (_lock)
        {
            var paths = new List<string>(Entries().Keys);
            _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            Save();
            return paths;
        }
    }

    public static void Remove(string? trackPath)
    {
        if (string.IsNullOrWhiteSpace(trackPath)) return;
        lock (_lock)
        {
            if (Entries().Remove(trackPath)) Save();
        }
    }
}
