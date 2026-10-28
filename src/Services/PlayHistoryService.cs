// ============================================================
//  PlayHistoryService.cs  —  Resona
//  Historique DATE des ecoutes (alimente le "Year in review").
//  Les compteurs de PlayStatsService (playstats.json) n'ont aucune date : ce service s'ajoute, il ne les remplace pas.
//
//  Fichier : %LOCALAPPDATA%\Resona\playhistory.jsonl  (une ligne JSON par enregistrement, ajout uniquement)
//    - 1re ligne  : {"start":"<date UTC>"}  = moment ou l'historique a commence (sert a dire "depuis le ..." dans le bilan)
//    - ensuite    : un evenement d'ecoute. Un meme evenement (meme "e") peut apparaitre plusieurs fois (points de controle
//                   du temps ecoute) : au chargement, la DERNIERE ligne gagne. Le fichier est compacte quand il grossit trop.
//  Les titres / artistes / albums / genres sont copies dans l'evenement : un son supprime de la bibliotheque compte toujours.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Resona.Models;

namespace Resona.Services;

public sealed class PlayEvent
{
    [JsonPropertyName("e")]  public string EventId { get; set; } = "";
    [JsonPropertyName("t")]  public DateTime StartedUtc { get; set; }
    [JsonPropertyName("id")] public string TrackId { get; set; } = "";
    [JsonPropertyName("ti")] public string Title { get; set; } = "";
    [JsonPropertyName("ar")] public string Artist { get; set; } = "";   // "" = artiste inconnu
    [JsonPropertyName("al")] public string Album { get; set; } = "";    // "" = album inconnu
    [JsonPropertyName("ge")] public string Genre { get; set; } = "";    // "" = genre inconnu
    [JsonPropertyName("d")]  public double DurationSeconds { get; set; }
    [JsonPropertyName("s")]  public double ListenedSeconds { get; set; }

    [JsonIgnore] public DateTime LocalStart => StartedUtc.ToLocalTime();

    /// <summary>Une ecoute compte pour le bilan a partir de 10 s reellement ecoutees (les passages "skip" sont ignores).</summary>
    [JsonIgnore] public bool Counts => ListenedSeconds >= 10.0;
}

public sealed class PlayHistoryService
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly List<PlayEvent> _events = new();
    private PlayEvent? _current;
    private double _unflushedSeconds;
    private int _rawLineCount;

    private const double CheckpointSeconds = 30.0;

    /// <summary>Date (UTC) a laquelle l'historique a commence. Avant cette date, aucune ecoute n'a ete enregistree.</summary>
    public DateTime TrackingStartUtc { get; private set; }

    public PlayHistoryService()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona");
        try { Directory.CreateDirectory(dir); } catch { }
        _filePath = Path.Combine(dir, "playhistory.jsonl");
        Load();
        // Filet de securite : quelle que soit la facon dont l'app se ferme, on enregistre le temps d'ecoute en cours.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
    }

    /// <summary>Nouvelle ecoute (appele quand un son demarre).</summary>
    public void BeginPlay(Track track)
    {
        try
        {
            string unknownArtist = Strings.Current.CS_ArtisteInconnu;
            string unknownAlbum = Strings.Current.CS_AlbumInconnu;
            string artist = track.DisplayArtist ?? "";
            string album = track.DisplayAlbum ?? "";

            var ev = new PlayEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                StartedUtc = DateTime.UtcNow,
                TrackId = track.Id,
                Title = track.Title ?? "",
                Artist = string.Equals(artist, unknownArtist, StringComparison.OrdinalIgnoreCase) ? "" : artist,
                Album = string.Equals(album, unknownAlbum, StringComparison.OrdinalIgnoreCase) ? "" : album,
                Genre = track.DisplayGenre ?? "",
                DurationSeconds = track.Duration.TotalSeconds,
                ListenedSeconds = 0
            };

            lock (_lock)
            {
                FlushLocked();
                _events.Add(ev);
                _current = ev;
                _unflushedSeconds = 0;
                AppendLocked(ev);
            }
        }
        catch { }
    }

    /// <summary>Temps d'ecoute reel du son en cours (appele a chaque tick de lecture, comme PlayStatsService.AddListenTime).</summary>
    public void AddListenTime(double seconds)
    {
        if (seconds <= 0 || seconds >= 60) return;
        lock (_lock)
        {
            if (_current == null) return;
            _current.ListenedSeconds += seconds;
            _unflushedSeconds += seconds;
            if (_unflushedSeconds >= CheckpointSeconds) FlushLocked();
        }
    }

    public void Flush()
    {
        lock (_lock) { FlushLocked(); }
    }

    /// <summary>Copie de tous les evenements (triee par date).</summary>
    public List<PlayEvent> Snapshot()
    {
        lock (_lock) { return _events.OrderBy(e => e.StartedUtc).ToList(); }
    }

    /// <summary>Dictionnaire associant chaque TrackId à la date UTC de sa dernière écoute.</summary>
    public Dictionary<string, DateTime> GetLastPlayedUtcByTrackId()
    {
        lock (_lock)
        {
            var dict = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var ev in _events)
            {
                if (!string.IsNullOrEmpty(ev.TrackId))
                {
                    dict[ev.TrackId] = ev.StartedUtc;
                }
            }
            return dict;
        }
    }

    /// <summary>Annees (heure locale) pour lesquelles au moins une ecoute comptee existe, de la plus recente a la plus ancienne.</summary>
    public List<int> YearsWithData()
    {
        lock (_lock)
        {
            return _events.Where(e => e.Counts).Select(e => e.LocalStart.Year).Distinct().OrderByDescending(y => y).ToList();
        }
    }

    // ------------------------------------------------------------------ import

    /// <summary>
    /// Fusionne un historique importe (sauvegarde) : les ecoutes deja presentes ne sont pas dupliquees (meme identifiant d'evenement,
    /// on garde le temps ecoute le plus eleve). Le debut du suivi devient le plus ancien des deux. Renvoie le nombre d'ecoutes ajoutees.
    /// </summary>
    public int MergeEvents(IEnumerable<PlayEvent> incoming, DateTime? incomingStartUtc)
    {
        int added = 0;
        lock (_lock)
        {
            var byId = _events.ToDictionary(e => e.EventId);
            foreach (var ev in incoming)
            {
                if (ev == null || string.IsNullOrEmpty(ev.EventId)) continue;
                if (byId.TryGetValue(ev.EventId, out var existing))
                {
                    if (ev.ListenedSeconds > existing.ListenedSeconds) existing.ListenedSeconds = ev.ListenedSeconds;
                }
                else
                {
                    ev.StartedUtc = ev.StartedUtc.ToUniversalTime();
                    _events.Add(ev);
                    byId[ev.EventId] = ev;
                    added++;
                }
            }
            _events.Sort((a, b) => a.StartedUtc.CompareTo(b.StartedUtc));

            if (incomingStartUtc.HasValue)
            {
                var start = incomingStartUtc.Value.ToUniversalTime();
                if (start < TrackingStartUtc) TrackingStartUtc = start;
            }
            Rewrite();
        }
        return added;
    }

    // ------------------------------------------------------------------ fichier

    private void FlushLocked()
    {
        if (_current != null && _unflushedSeconds > 0)
        {
            AppendLocked(_current);
            _unflushedSeconds = 0;
        }
    }

    private void AppendLocked(PlayEvent ev)
    {
        try
        {
            File.AppendAllText(_filePath, JsonSerializer.Serialize(ev) + "\n");
            _rawLineCount++;
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            var byId = new Dictionary<string, PlayEvent>();
            bool hasMeta = false;

            if (File.Exists(_filePath))
            {
                foreach (string line in File.ReadLines(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("start", out var start))
                        {
                            if (start.TryGetDateTime(out var dt)) { TrackingStartUtc = dt.ToUniversalTime(); hasMeta = true; }
                            continue;
                        }
                        var ev = JsonSerializer.Deserialize<PlayEvent>(line);
                        if (ev != null && !string.IsNullOrEmpty(ev.EventId))
                        {
                            byId[ev.EventId] = ev;   // la derniere ligne d'un meme evenement gagne
                            _rawLineCount++;
                        }
                    }
                    catch { /* ligne abimee (arret brutal) : on l'ignore */ }
                }
            }

            _events.AddRange(byId.Values.OrderBy(e => e.StartedUtc));

            if (!hasMeta)
            {
                // Premier lancement avec l'historique : c'est ici que le suivi commence.
                TrackingStartUtc = DateTime.UtcNow;
                if (_events.Count > 0) TrackingStartUtc = _events[0].StartedUtc;
                Rewrite();
            }
            else if (_rawLineCount > _events.Count * 2 + 50)
            {
                Rewrite();   // trop de points de controle accumules : on compacte
            }
        }
        catch { }
    }

    private void Rewrite()
    {
        try
        {
            string tmp = _filePath + ".tmp";
            using (var w = new StreamWriter(tmp, false))
            {
                w.WriteLine(JsonSerializer.Serialize(new { start = TrackingStartUtc }));
                foreach (var ev in _events) w.WriteLine(JsonSerializer.Serialize(ev));
            }
            File.Move(tmp, _filePath, true);
            _rawLineCount = _events.Count;
        }
        catch { }
    }
}
