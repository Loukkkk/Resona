// ============================================================
//  SmartPlaylist.cs  —  Resona
//  Playlists intelligentes : une playlist definie par des REGLES dynamiques (et non par une liste fixe de titres).
//  Le contenu est recalcule a chaque ouverture a partir de la bibliotheque, de l'historique d'ecoute et des favoris.
//
//  Stockage : la definition est serialisee en JSON dans la colonne Playlists.SmartRulesJson (colonne AJOUTEE, nullable) :
//  les anciennes bases restent valides et une playlist classique garde SmartRulesJson = NULL (aucune migration de donnees).
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Resona.Services;

namespace Resona.Models;

public enum SmartField
{
    DateAdded,      // date d'ajout a la bibliotheque
    LastPlayed,     // derniere ecoute
    PlayCount,      // nombre d'ecoutes
    Genre,
    Year,
    Artist,
    Album,
    AlbumArtist,
    Title,
    DurationSeconds,
    TrackNumber,
    HasLyrics,
    HasCover,
    IsFavorite,
    Format,         // extension du fichier (mp3, flac...)
    FilePath,       // chemin / dossier
    // --- Analyse d'ambiance (BPM / energie / vibe). Ajoutes A LA FIN : les regles deja enregistrees memorisent le champ sous forme de nombre. ---
    Bpm,            // tempo (battements par minute)
    Energy,         // energie 0-100
    Vibe,           // ambiance (calm, chill, upbeat, energetic...)
    MoodAnalyzed,   // le titre a-t-il deja ete analyse ?
}

public enum SmartOp
{
    Is, IsNot, Contains, NotContains, StartsWith, EndsWith,
    Gt, Gte, Lt, Lte, Between,
    InLastDays,         // dates : dans les N derniers jours
    NotInLastDays,      // dates : pas dans les N derniers jours (ou jamais)
    ThisMonth, ThisYear, // dates : sans valeur
    Never,              // LastPlayed : jamais ecoute
    IsTrue, IsFalse,    // booleens
    IsEmpty, IsNotEmpty // textes
}

public class SmartRule
{
    public SmartField Field { get; set; } = SmartField.Genre;
    public SmartOp Op { get; set; } = SmartOp.Is;
    public string Value { get; set; } = string.Empty;
    public string Value2 { get; set; } = string.Empty;   // borne haute de "Between"
}

public class SmartPlaylistDefinition
{
    public bool MatchAll { get; set; } = true;   // vrai = toutes les regles (ET), faux = au moins une (OU)
    public List<SmartRule> Rules { get; set; } = new();
    // Tri : added_desc, added_asc, played_desc, lastplayed_desc, lastplayed_asc, title_asc, artist_asc, year_desc, year_asc, random
    public string Sort { get; set; } = "added_desc";
    public int Limit { get; set; } = 0;           // 0 = pas de limite

    public string ToJson() => JsonSerializer.Serialize(this);

    public static SmartPlaylistDefinition? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<SmartPlaylistDefinition>(json); }
        catch { return null; }
    }
}

/// <summary>Donnees d'ecoute / favoris necessaires pour evaluer les regles (calculees une fois par evaluation).</summary>
public sealed class SmartContext
{
    public Dictionary<string, int> PlayCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DateTime> LastPlayedUtc { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FavoriteIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Analyse d'ambiance (BPM / energie / vibe) par chemin de fichier.</summary>
    public Dictionary<string, MoodData> Mood { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime NowUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Copie des donnees d'ecoute / favoris, pour fusionner celles d'un fichier enregistre en double sans modifier l'original.</summary>
    public SmartContext CloneForMerge() => new SmartContext
    {
        PlayCounts = new Dictionary<string, int>(PlayCounts, StringComparer.OrdinalIgnoreCase),
        LastPlayedUtc = new Dictionary<string, DateTime>(LastPlayedUtc, StringComparer.OrdinalIgnoreCase),
        FavoriteIds = new HashSet<string>(FavoriteIds, StringComparer.OrdinalIgnoreCase),
        Mood = Mood,
        NowUtc = NowUtc
    };

    /// <summary>Additionne ecoutes, derniere ecoute et favori de tous les Id d'un meme fichier sur l'Id conserve.</summary>
    public void MergeInto(string keepId, IEnumerable<string> ids)
    {
        int plays = 0;
        DateTime lastPlayed = DateTime.MinValue;
        bool hasLast = false, fav = false;
        foreach (var id in ids)
        {
            if (PlayCounts.TryGetValue(id, out int n)) plays += n;
            if (LastPlayedUtc.TryGetValue(id, out var d) && d > lastPlayed) { lastPlayed = d; hasLast = true; }
            if (FavoriteIds.Contains(id)) fav = true;
        }
        if (plays > 0) PlayCounts[keepId] = plays;
        if (hasLast) LastPlayedUtc[keepId] = lastPlayed;
        if (fav) FavoriteIds.Add(keepId);
    }

    private static Dictionary<string, MoodData> SafeMoodSnapshot()
    {
        try { return App.MoodAnalysis.Snapshot(); }
        catch { return new Dictionary<string, MoodData>(StringComparer.OrdinalIgnoreCase); }
    }

    /// <summary>Construit le contexte a partir de l'historique daté (une ecoute compte a partir de 10 s reellement ecoutees).</summary>
    public static SmartContext Build(IEnumerable<string>? favoriteIds)
    {
        var ctx = new SmartContext
        {
            FavoriteIds = new HashSet<string>(favoriteIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            Mood = SafeMoodSnapshot()
        };
        try
        {
            foreach (var ev in App.PlayHistory.Snapshot())
            {
                if (string.IsNullOrEmpty(ev.TrackId) || !ev.Counts) continue;
                ctx.PlayCounts[ev.TrackId] = ctx.PlayCounts.TryGetValue(ev.TrackId, out int n) ? n + 1 : 1;
                if (!ctx.LastPlayedUtc.TryGetValue(ev.TrackId, out var last) || ev.StartedUtc > last)
                    ctx.LastPlayedUtc[ev.TrackId] = ev.StartedUtc;
            }
        }
        catch { }
        try
        {
            // Compteurs historiques (PlayStatsService) : couvrent aussi les ecoutes anterieures a l'historique date.
            foreach (var kv in App.PlayStats.SnapshotCounts())
                if (kv.Value > 0) ctx.PlayCounts[kv.Key] = Math.Max(ctx.PlayCounts.TryGetValue(kv.Key, out int c) ? c : 0, kv.Value);
        }
        catch { }
        return ctx;
    }
}

public static class SmartPlaylistEngine
{
    public static bool IsSmart(this Playlist p) => !string.IsNullOrWhiteSpace(p.SmartRulesJson);

    /// <summary>La definition utilise-t-elle l'analyse d'ambiance (BPM / energie / vibe), en regle ou en tri ?</summary>
    public static bool UsesMood(SmartPlaylistDefinition def)
        => def.Rules.Any(r => r != null && r.Field is SmartField.Bpm or SmartField.Energy or SmartField.Vibe or SmartField.MoodAnalyzed)
           || (def.Sort?.StartsWith("energy_", StringComparison.Ordinal) ?? false)
           || (def.Sort?.StartsWith("bpm_", StringComparison.Ordinal) ?? false);

    /// <summary>Evalue la definition sur la bibliotheque : filtre, trie puis limite.</summary>
    public static List<Track> Evaluate(SmartPlaylistDefinition def, IEnumerable<Track> library, SmartContext ctx)
    {
        // Garde-fou : un meme fichier peut etre enregistre deux fois sous des ecritures differentes du chemin (C:\x et C:/x).
        // On n'en garde qu'un exemplaire (de preference celui dont le chemin est deja canonique) et on lui additionne ecoutes / favori de ses doublons.
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<string, List<Track>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var unique = new List<Track>();
        foreach (var t in library)
        {
            if (t == null || t.IsExternal) continue;
            if (!string.IsNullOrEmpty(t.Id) && !seenIds.Add(t.Id)) continue;
            string pathKey = string.IsNullOrEmpty(t.FilePath) ? "id:" + t.Id : MoodAnalysisService.Key(t.FilePath);
            if (!groups.TryGetValue(pathKey, out var grp)) { grp = new List<Track>(); groups[pathKey] = grp; order.Add(pathKey); }
            grp.Add(t);
            // (l'exemplaire conserve est choisi apres regroupement, ci-dessous)
        }
        SmartContext? merged = null;
        foreach (var pathKey2 in order)
        {
            var grp2 = groups[pathKey2];
            var keep = grp2.FirstOrDefault(x => x.FilePath == pathKey2) ?? grp2[0];
            unique.Add(keep);
            if (grp2.Count > 1)
            {
                merged ??= ctx.CloneForMerge();
                merged.MergeInto(keep.Id, grp2.Select(x => x.Id));
            }
        }
        if (merged != null) ctx = merged;
        IEnumerable<Track> q = unique;
        var rules = def.Rules.Where(r => r != null).ToList();
        if (rules.Count > 0)
        {
            q = def.MatchAll
                ? q.Where(t => rules.All(r => Matches(r, t, ctx)))
                : q.Where(t => rules.Any(r => Matches(r, t, ctx)));
        }
        var list = Sort(q, def.Sort, ctx).ToList();
        if (def.Limit > 0 && list.Count > def.Limit) list = list.Take(def.Limit).ToList();
        return list;
    }

    private static IEnumerable<Track> Sort(IEnumerable<Track> q, string? sort, SmartContext ctx)
    {
        int Plays(Track t) => ctx.PlayCounts.TryGetValue(t.Id, out int n) ? n : 0;
        DateTime Last(Track t) => ctx.LastPlayedUtc.TryGetValue(t.Id, out var d) ? d : DateTime.MinValue;
        // Titres sans analyse d'ambiance : toujours en fin de liste, quel que soit le sens du tri.
        MoodData? MoodOf(Track t) => ctx.Mood.TryGetValue(MoodAnalysisService.Key(t.FilePath), out var md) ? md : null;
        int Missing(Track t) => MoodOf(t) == null ? 1 : 0;
        return (sort ?? "added_desc") switch
        {
            "added_asc"       => q.OrderBy(t => t.DateAdded),
            "played_desc"     => q.OrderByDescending(Plays).ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase),
            "lastplayed_desc" => q.OrderByDescending(Last),
            "lastplayed_asc"  => q.OrderBy(Last),
            "title_asc"       => q.OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase),
            "artist_asc"      => q.OrderBy(t => t.DisplayArtist, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Album, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.TrackNumber),
            "year_desc"       => q.OrderByDescending(t => t.Year),
            "year_asc"        => q.OrderBy(t => t.Year),
            "energy_desc"     => q.OrderBy(Missing).ThenByDescending(t => MoodOf(t)?.Energy ?? 0),
            "energy_asc"      => q.OrderBy(Missing).ThenBy(t => MoodOf(t)?.Energy ?? 0),
            "bpm_desc"        => q.OrderBy(Missing).ThenByDescending(t => MoodOf(t)?.Bpm ?? 0),
            "bpm_asc"         => q.OrderBy(Missing).ThenBy(t => MoodOf(t)?.Bpm ?? 0),
            "random"          => q.OrderBy(_ => Random.Shared.Next()),
            _                 => q.OrderByDescending(t => t.DateAdded),
        };
    }

    // ---------------------------------------------------------------- evaluation d'une regle

    public static bool Matches(SmartRule r, Track t, SmartContext ctx)
    {
        try
        {
            switch (r.Field)
            {
                case SmartField.Genre:       return MatchText(r, t.DisplayGenre, split: true);
                case SmartField.Artist:      return MatchText(r, t.DisplayArtist, split: true);
                case SmartField.Album:       return MatchText(r, t.DisplayAlbum, split: false);
                case SmartField.AlbumArtist: return MatchText(r, t.AlbumArtist, split: true);
                case SmartField.Title:       return MatchText(r, t.Title, split: false);
                case SmartField.FilePath:    return MatchText(r, t.FilePath, split: false);
                case SmartField.Format:      return MatchText(r, Path.GetExtension(t.FilePath).TrimStart('.'), split: false);

                case SmartField.Year:            return MatchNumber(r, t.Year);
                case SmartField.TrackNumber:     return MatchNumber(r, t.TrackNumber);
                case SmartField.DurationSeconds: return MatchNumber(r, t.Duration.TotalSeconds);
                case SmartField.PlayCount:       return MatchNumber(r, ctx.PlayCounts.TryGetValue(t.Id, out int n) ? n : 0);

                case SmartField.DateAdded:
                    return MatchDate(r, t.DateAdded == DateTime.MinValue ? (DateTime?)null : ToUtc(t.DateAdded), ctx);
                case SmartField.LastPlayed:
                {
                    bool hasDate = ctx.LastPlayedUtc.TryGetValue(t.Id, out var lp);
                    // Ecoute ancienne connue seulement par son compteur (sans date) : le titre n'est pas "jamais ecoute".
                    if (r.Op == SmartOp.Never) return !hasDate && !(ctx.PlayCounts.TryGetValue(t.Id, out int pc) && pc > 0);
                    return MatchDate(r, hasDate ? lp : (DateTime?)null, ctx);
                }

                case SmartField.HasLyrics:   return MatchBool(r, !string.IsNullOrWhiteSpace(t.Lyrics));
                case SmartField.HasCover:    return MatchBool(r, !string.IsNullOrEmpty(t.CoverArtPath));
                case SmartField.IsFavorite:  return MatchBool(r, ctx.FavoriteIds.Contains(t.Id));

                case SmartField.Bpm:
                {
                    bool has = ctx.Mood.TryGetValue(MoodAnalysisService.Key(t.FilePath), out var moodBpm) && moodBpm.Bpm > 0;
                    return has && MatchNumber(r, moodBpm!.Bpm);
                }
                case SmartField.Energy:
                {
                    bool has = ctx.Mood.TryGetValue(MoodAnalysisService.Key(t.FilePath), out var moodEnergy);
                    return has && MatchNumber(r, moodEnergy!.Energy);
                }
                case SmartField.Vibe:
                {
                    if (!ctx.Mood.TryGetValue(MoodAnalysisService.Key(t.FilePath), out var moodVibe)) return false;
                    bool same = string.Equals(moodVibe.Vibe, r.Value, StringComparison.OrdinalIgnoreCase);
                    return r.Op == SmartOp.IsNot ? !same : same;
                }
                case SmartField.MoodAnalyzed:
                    return MatchBool(r, ctx.Mood.ContainsKey(MoodAnalysisService.Key(t.FilePath)));
            }
        }
        catch { }
        return false;
    }

    private static DateTime ToUtc(DateTime d) => d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private static string Norm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        string d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (char c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString().Trim();
    }

    private static bool IsUnknown(string? s)
        => string.IsNullOrWhiteSpace(s) || s == Strings.Current.CS_ArtisteInconnu || s == Strings.Current.CS_AlbumInconnu
           || s is "Unknown genre" or "Genre inconnu" or "Unknown" or "Inconnu";

    /// <param name="split">Vrai pour genre/artiste : "Rock, Pop" correspond a "Rock" comme a "Pop" (egalite sur chaque element).</param>
    private static bool MatchText(SmartRule r, string? raw, bool split)
    {
        string v = Norm(raw);
        string target = Norm(r.Value);
        switch (r.Op)
        {
            case SmartOp.IsEmpty:    return IsUnknown(raw);
            case SmartOp.IsNotEmpty: return !IsUnknown(raw);
        }
        IEnumerable<string> parts = split
            ? v.Split(new[] { ',', ';', '/', '&' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Append(v)
            : new[] { v };
        return r.Op switch
        {
            SmartOp.Is          => parts.Any(p => p == target),
            SmartOp.IsNot       => !parts.Any(p => p == target),
            SmartOp.Contains    => target.Length == 0 || v.Contains(target),
            SmartOp.NotContains => target.Length == 0 || !v.Contains(target),
            SmartOp.StartsWith  => v.StartsWith(target, StringComparison.Ordinal),
            SmartOp.EndsWith    => v.EndsWith(target, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool TryNum(string s, out double d)
        => double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d);

    private static bool MatchNumber(SmartRule r, double value)
    {
        if (!TryNum(r.Value, out double a)) return r.Op == SmartOp.IsNot;
        switch (r.Op)
        {
            case SmartOp.Is:      return Math.Abs(value - a) < 0.0001;
            case SmartOp.IsNot:   return Math.Abs(value - a) >= 0.0001;
            case SmartOp.Gt:      return value > a;
            case SmartOp.Gte:     return value >= a;
            case SmartOp.Lt:      return value < a;
            case SmartOp.Lte:     return value <= a;
            case SmartOp.Between:
                if (!TryNum(r.Value2, out double b)) return false;
                return value >= Math.Min(a, b) && value <= Math.Max(a, b);
        }
        return false;
    }

    private static bool MatchDate(SmartRule r, DateTime? utc, SmartContext ctx)
    {
        DateTime now = ctx.NowUtc;
        DateTime localNow = now.ToLocalTime();
        switch (r.Op)
        {
            case SmartOp.Never:  return utc == null;
            case SmartOp.ThisMonth:
                return utc != null && utc.Value.ToLocalTime() is var m && m.Year == localNow.Year && m.Month == localNow.Month;
            case SmartOp.ThisYear:
                return utc != null && utc.Value.ToLocalTime().Year == localNow.Year;
            case SmartOp.InLastDays:
                return utc != null && TryNum(r.Value, out double d1) && utc.Value >= now.AddDays(-d1) && utc.Value <= now.AddMinutes(5);
            case SmartOp.NotInLastDays:
                return utc == null || (TryNum(r.Value, out double d2) && utc.Value < now.AddDays(-d2));
            case SmartOp.Gt: case SmartOp.Gte: case SmartOp.Lt: case SmartOp.Lte: case SmartOp.Is: case SmartOp.Between:
            {
                if (utc == null) return false;
                if (!DateTime.TryParse(r.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var a)) return false;
                DateTime loc = utc.Value.ToLocalTime();
                switch (r.Op)
                {
                    case SmartOp.Is:  return loc.Date == a.Date;
                    case SmartOp.Gt:  return loc.Date > a.Date;
                    case SmartOp.Gte: return loc.Date >= a.Date;
                    case SmartOp.Lt:  return loc.Date < a.Date;
                    case SmartOp.Lte: return loc.Date <= a.Date;
                    case SmartOp.Between:
                        if (!DateTime.TryParse(r.Value2, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var b)) return false;
                        return loc.Date >= (a < b ? a : b).Date && loc.Date <= (a < b ? b : a).Date;
                }
                return false;
            }
        }
        return false;
    }

    private static bool MatchBool(SmartRule r, bool value) => r.Op == SmartOp.IsFalse ? !value : value;

    // ---------------------------------------------------------------- aide pour l'editeur

    public static SmartOp[] OpsFor(SmartField f) => f switch
    {
        SmartField.Genre or SmartField.Artist or SmartField.AlbumArtist or SmartField.Album or SmartField.Title or SmartField.Format or SmartField.FilePath
            => new[] { SmartOp.Is, SmartOp.IsNot, SmartOp.Contains, SmartOp.NotContains, SmartOp.StartsWith, SmartOp.EndsWith, SmartOp.IsEmpty, SmartOp.IsNotEmpty },
        SmartField.Year or SmartField.TrackNumber or SmartField.DurationSeconds or SmartField.PlayCount
            => new[] { SmartOp.Is, SmartOp.IsNot, SmartOp.Gt, SmartOp.Gte, SmartOp.Lt, SmartOp.Lte, SmartOp.Between },
        SmartField.Bpm or SmartField.Energy
            => new[] { SmartOp.Gt, SmartOp.Gte, SmartOp.Lt, SmartOp.Lte, SmartOp.Between },
        SmartField.Vibe
            => new[] { SmartOp.Is, SmartOp.IsNot },
        SmartField.DateAdded or SmartField.LastPlayed
            => f == SmartField.LastPlayed
                ? new[] { SmartOp.Never, SmartOp.InLastDays, SmartOp.NotInLastDays, SmartOp.ThisMonth, SmartOp.ThisYear, SmartOp.Gte, SmartOp.Lte, SmartOp.Between }
                : new[] { SmartOp.InLastDays, SmartOp.NotInLastDays, SmartOp.ThisMonth, SmartOp.ThisYear, SmartOp.Gte, SmartOp.Lte, SmartOp.Between },
        _ => new[] { SmartOp.IsTrue, SmartOp.IsFalse }
    };

    /// <summary>Nombre de champs de valeur a saisir (0, 1 ou 2) pour un operateur.</summary>
    public static int ValueCount(SmartOp op) => op switch
    {
        SmartOp.Never or SmartOp.ThisMonth or SmartOp.ThisYear or SmartOp.IsTrue or SmartOp.IsFalse or SmartOp.IsEmpty or SmartOp.IsNotEmpty => 0,
        SmartOp.Between => 2,
        _ => 1
    };

    public static string FieldLabel(SmartField f, bool fr) => f switch
    {
        SmartField.DateAdded       => fr ? "Date d'ajout" : "Date added",
        SmartField.LastPlayed      => fr ? "Dernière écoute" : "Last played",
        SmartField.PlayCount       => fr ? "Nombre d'écoutes" : "Play count",
        SmartField.Genre           => fr ? "Genre" : "Genre",
        SmartField.Year            => fr ? "Année" : "Year",
        SmartField.Artist          => fr ? "Artiste" : "Artist",
        SmartField.Album           => "Album",
        SmartField.AlbumArtist     => fr ? "Artiste de l'album" : "Album artist",
        SmartField.Title           => fr ? "Titre" : "Title",
        SmartField.DurationSeconds => fr ? "Durée (secondes)" : "Duration (seconds)",
        SmartField.TrackNumber     => fr ? "N° de piste" : "Track number",
        SmartField.HasLyrics       => fr ? "A des paroles" : "Has lyrics",
        SmartField.HasCover        => fr ? "A une pochette" : "Has cover",
        SmartField.IsFavorite      => fr ? "Dans les favoris" : "Is favorite",
        SmartField.Format          => fr ? "Format de fichier" : "File format",
        SmartField.FilePath        => fr ? "Chemin / dossier" : "Path / folder",
        SmartField.Bpm             => fr ? "Tempo (BPM)" : "Tempo (BPM)",
        SmartField.Energy          => fr ? "Énergie (0–100)" : "Energy (0–100)",
        SmartField.Vibe            => fr ? "Ambiance (vibe)" : "Mood (vibe)",
        SmartField.MoodAnalyzed    => fr ? "Ambiance déjà analysée" : "Mood already analyzed",
        _ => f.ToString()
    };

    public static string OpLabel(SmartOp op, SmartField f, bool fr)
    {
        bool isDate = f == SmartField.DateAdded || f == SmartField.LastPlayed;
        return op switch
        {
            SmartOp.Is          => isDate ? (fr ? "est le" : "is on") : (fr ? "est" : "is"),
            SmartOp.IsNot       => fr ? "n'est pas" : "is not",
            SmartOp.Contains    => fr ? "contient" : "contains",
            SmartOp.NotContains => fr ? "ne contient pas" : "does not contain",
            SmartOp.StartsWith  => fr ? "commence par" : "starts with",
            SmartOp.EndsWith    => fr ? "finit par" : "ends with",
            SmartOp.Gt          => fr ? "supérieur à" : "greater than",
            SmartOp.Gte         => isDate ? (fr ? "depuis le" : "since") : (fr ? "supérieur ou égal à" : "at least"),
            SmartOp.Lt          => fr ? "inférieur à" : "less than",
            SmartOp.Lte         => isDate ? (fr ? "jusqu'au" : "until") : (fr ? "inférieur ou égal à" : "at most"),
            SmartOp.Between     => fr ? "entre" : "between",
            SmartOp.InLastDays  => fr ? "dans les N derniers jours" : "in the last N days",
            SmartOp.NotInLastDays => fr ? "pas dans les N derniers jours" : "not in the last N days",
            SmartOp.ThisMonth   => fr ? "ce mois-ci" : "this month",
            SmartOp.ThisYear    => fr ? "cette année" : "this year",
            SmartOp.Never       => fr ? "jamais" : "never",
            SmartOp.IsTrue      => fr ? "oui" : "yes",
            SmartOp.IsFalse     => fr ? "non" : "no",
            SmartOp.IsEmpty     => fr ? "est vide / inconnu" : "is empty / unknown",
            SmartOp.IsNotEmpty  => fr ? "est renseigné" : "is set",
            _ => op.ToString()
        };
    }

    public static readonly (string Key, string Fr, string En)[] SortOptions =
    {
        ("added_desc",       "Ajoutés récemment",        "Recently added"),
        ("added_asc",        "Ajoutés en premier",       "Oldest added"),
        ("played_desc",      "Les plus écoutés",         "Most played"),
        ("lastplayed_desc",  "Écoutés récemment",        "Recently played"),
        ("lastplayed_asc",   "Écoutés il y a longtemps", "Played longest ago"),
        ("title_asc",        "Titre (A → Z)",            "Title (A → Z)"),
        ("artist_asc",       "Artiste (A → Z)",          "Artist (A → Z)"),
        ("year_desc",        "Année (récente d'abord)",  "Year (newest first)"),
        ("year_asc",         "Année (ancienne d'abord)", "Year (oldest first)"),
        ("random",           "Aléatoire",                "Random"),
        ("energy_desc",      "Énergie (forte d'abord)",  "Energy (highest first)"),
        ("energy_asc",       "Énergie (faible d'abord)", "Energy (lowest first)"),
        ("bpm_desc",         "Tempo (rapide d'abord)",   "Tempo (fastest first)"),
        ("bpm_asc",          "Tempo (lent d'abord)",     "Tempo (slowest first)"),
    };

    /// <summary>Modeles prets a l'emploi proposes a la creation.</summary>
    public static List<(string Fr, string En, SmartPlaylistDefinition Def)> Presets() => new()
    {
        ("Ajoutés ce mois-ci", "Added this month", new SmartPlaylistDefinition { Sort = "added_desc",
            Rules = { new SmartRule { Field = SmartField.DateAdded, Op = SmartOp.ThisMonth } } }),
        ("Jamais écoutés", "Never played", new SmartPlaylistDefinition { Sort = "added_desc",
            Rules = { new SmartRule { Field = SmartField.LastPlayed, Op = SmartOp.Never } } }),
        ("Les plus écoutés", "Most played", new SmartPlaylistDefinition { Sort = "played_desc", Limit = 50,
            Rules = { new SmartRule { Field = SmartField.PlayCount, Op = SmartOp.Gte, Value = "1" } } }),
        ("Écoutés récemment", "Recently played", new SmartPlaylistDefinition { Sort = "lastplayed_desc", Limit = 50,
            Rules = { new SmartRule { Field = SmartField.LastPlayed, Op = SmartOp.InLastDays, Value = "30" } } }),
        ("Oubliés (pas écoutés depuis 6 mois)", "Forgotten (not played in 6 months)", new SmartPlaylistDefinition { Sort = "lastplayed_asc",
            Rules = { new SmartRule { Field = SmartField.LastPlayed, Op = SmartOp.NotInLastDays, Value = "180" } } }),
        ("Genre = Rock et année > 2010", "Genre = Rock and year > 2010", new SmartPlaylistDefinition { Sort = "year_desc",
            Rules = { new SmartRule { Field = SmartField.Genre, Op = SmartOp.Is, Value = "Rock" },
                      new SmartRule { Field = SmartField.Year, Op = SmartOp.Gt, Value = "2010" } } }),
        ("Titres sans paroles", "Tracks without lyrics", new SmartPlaylistDefinition { Sort = "artist_asc",
            Rules = { new SmartRule { Field = SmartField.HasLyrics, Op = SmartOp.IsFalse } } }),
        ("Énergiques", "High energy", new SmartPlaylistDefinition { Sort = "energy_desc",
            Rules = { new SmartRule { Field = SmartField.Energy, Op = SmartOp.Gte, Value = "70" } } }),
        ("Calmes et détendus", "Calm and chill", new SmartPlaylistDefinition { MatchAll = false, Sort = "energy_asc",
            Rules = { new SmartRule { Field = SmartField.Vibe, Op = SmartOp.Is, Value = "calm" },
                      new SmartRule { Field = SmartField.Vibe, Op = SmartOp.Is, Value = "chill" } } }),
        ("Pour courir (150–180 BPM)", "Running (150–180 BPM)", new SmartPlaylistDefinition { Sort = "bpm_desc",
            Rules = { new SmartRule { Field = SmartField.Bpm, Op = SmartOp.Between, Value = "150", Value2 = "180" },
                      new SmartRule { Field = SmartField.Energy, Op = SmartOp.Gte, Value = "60" } } }),
        ("Ambiance sombre ou mélancolique", "Dark or melancholic mood", new SmartPlaylistDefinition { MatchAll = false, Sort = "energy_asc",
            Rules = { new SmartRule { Field = SmartField.Vibe, Op = SmartOp.Is, Value = "dark" },
                      new SmartRule { Field = SmartField.Vibe, Op = SmartOp.Is, Value = "melancholic" } } }),
    };
}
