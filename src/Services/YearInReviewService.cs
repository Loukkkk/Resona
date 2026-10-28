// ============================================================
//  YearInReviewService.cs  —  Resona
//  Calcule le bilan d'une annee a partir de l'historique date des ecoutes (PlayHistoryService).
//  - S'adapte a la couverture reelle : si l'historique a commence en cours d'annee (nouvel utilisateur, ou fonctionnalite ajoutee
//    apres le 1er janvier), le bilan le dit ("depuis le ...") au lieu de faire croire qu'il couvre toute l'annee.
//  - Version Debug uniquement : GenerateDebugEvents() fabrique de fausses ecoutes pour tester l'ecran sans toucher au vrai historique.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Resona.Models;

namespace Resona.Services;

public sealed class ReviewTrackStat
{
    public string TrackId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public int Plays { get; set; }
    public double Seconds { get; set; }
}

public sealed class ReviewNameStat
{
    public string Name { get; set; } = "";
    public int Plays { get; set; }
    public double Seconds { get; set; }
}

public sealed class YearInReviewData
{
    public int Year { get; set; }

    /// <summary>Vrai si l'historique n'existait pas encore au 1er janvier de cette annee : le bilan ne couvre alors qu'une partie de l'annee.</summary>
    public bool IsPartial { get; set; }
    public DateTime? CoverageStart { get; set; }
    /// <summary>Vrai si l'annee n'est pas terminee (bilan "a ce jour").</summary>
    public bool IsOngoing { get; set; }

    public double TotalSeconds { get; set; }
    public int TotalPlays { get; set; }
    public int DistinctTracks { get; set; }
    public int DistinctArtists { get; set; }
    public int ActiveDays { get; set; }
    public int LongestStreakDays { get; set; }
    public int? PeakHour { get; set; }
    public DateTime? BusiestDay { get; set; }
    public double BusiestDaySeconds { get; set; }

    public List<ReviewTrackStat> TopTracks { get; set; } = new();
    public List<ReviewNameStat> TopArtists { get; set; } = new();
    public List<ReviewNameStat> TopGenres { get; set; } = new();
    public double[] MonthSeconds { get; set; } = new double[12];

    /// <summary>Assez d'ecoutes pour que le bilan ait du sens.</summary>
    public bool HasEnoughData => TotalPlays >= 5;
}

public static class YearInReviewService
{
    /// <summary>
    /// Annee a proposer automatiquement : du 20 decembre au 31 decembre -> l'annee en cours ; en janvier -> l'annee precedente ;
    /// le reste de l'annee -> aucune proposition (le bilan reste accessible depuis la page Statistiques).
    /// </summary>
    public static int? GetPromptYear(DateTime now)
    {
        if (now.Month == 12 && now.Day >= 20) return now.Year;
        if (now.Month == 1) return now.Year - 1;
        return null;
    }

    public static YearInReviewData Compute(int year, IEnumerable<PlayEvent> events, DateTime trackingStartUtc)
    {
        var d = new YearInReviewData { Year = year };
        var list = events.Where(e => e.Counts && e.LocalStart.Year == year).ToList();

        DateTime start = trackingStartUtc.ToLocalTime().Date;
        if (start > new DateTime(year, 1, 1))
        {
            d.IsPartial = true;
            d.CoverageStart = start;
        }
        DateTime now = DateTime.Now;
        d.IsOngoing = year == now.Year && now < new DateTime(year, 12, 31);

        if (list.Count == 0) return d;

        d.TotalSeconds = list.Sum(e => e.ListenedSeconds);
        d.TotalPlays = list.Count;

        // --- Titres ---
        string KeyOf(PlayEvent e) => !string.IsNullOrEmpty(e.TrackId) ? e.TrackId : (e.Title + "|" + e.Artist);
        var byTrack = list.GroupBy(KeyOf).Select(g =>
        {
            var last = g.OrderByDescending(x => x.StartedUtc).First();
            return new ReviewTrackStat
            {
                TrackId = last.TrackId, Title = last.Title, Artist = last.Artist,
                Plays = g.Count(), Seconds = g.Sum(x => x.ListenedSeconds)
            };
        }).OrderByDescending(t => t.Plays).ThenByDescending(t => t.Seconds).ToList();
        d.DistinctTracks = byTrack.Count;
        d.TopTracks = byTrack.Take(5).ToList();

        // --- Artistes (classes par temps d'ecoute) ---
        var artists = list.Where(e => !string.IsNullOrWhiteSpace(e.Artist))
            .GroupBy(e => e.Artist, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReviewNameStat { Name = g.First().Artist, Plays = g.Count(), Seconds = g.Sum(x => x.ListenedSeconds) })
            .OrderByDescending(a => a.Seconds).ToList();
        d.DistinctArtists = artists.Count;
        d.TopArtists = artists.Take(5).ToList();

        // --- Genres ---
        d.TopGenres = list.Where(e => !string.IsNullOrWhiteSpace(e.Genre))
            .GroupBy(e => e.Genre, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReviewNameStat { Name = g.First().Genre, Plays = g.Count(), Seconds = g.Sum(x => x.ListenedSeconds) })
            .OrderByDescending(a => a.Seconds).Take(3).ToList();

        // --- Mois, jours, heures ---
        var hours = new double[24];
        foreach (var e in list)
        {
            d.MonthSeconds[e.LocalStart.Month - 1] += e.ListenedSeconds;
            hours[e.LocalStart.Hour] += e.ListenedSeconds;
        }
        int peak = Array.IndexOf(hours, hours.Max());
        d.PeakHour = hours[peak] > 0 ? peak : null;

        var days = list.GroupBy(e => e.LocalStart.Date)
            .Select(g => (Date: g.Key, Seconds: g.Sum(x => x.ListenedSeconds)))
            .OrderBy(x => x.Date).ToList();
        d.ActiveDays = days.Count;
        var busiest = days.OrderByDescending(x => x.Seconds).First();
        d.BusiestDay = busiest.Date;
        d.BusiestDaySeconds = busiest.Seconds;

        int best = 1, cur = 1;
        for (int i = 1; i < days.Count; i++)
        {
            cur = (days[i].Date - days[i - 1].Date).Days == 1 ? cur + 1 : 1;
            if (cur > best) best = cur;
        }
        d.LongestStreakDays = best;

        return d;
    }

#if DEBUG
    /// <summary>
    /// DEBUG uniquement : fausses ecoutes realistes (titres de la bibliotheque, repartition par mois, soirees plus chargees)
    /// pour tester l'ecran du bilan sans toucher au vrai historique.
    /// </summary>
    public static List<PlayEvent> GenerateDebugEvents(int year, IReadOnlyList<Track> library)
    {
        var rnd = new Random(year * 7919 + 13);
        string unknownArtist = Strings.Current.CS_ArtisteInconnu;
        string unknownAlbum = Strings.Current.CS_AlbumInconnu;

        var pool = new List<(string Id, string Title, string Artist, string Album, string Genre, double Dur)>();
        foreach (var t in library.OrderBy(_ => rnd.Next()).Take(80))
        {
            string artist = t.DisplayArtist ?? "";
            string album = t.DisplayAlbum ?? "";
            pool.Add((t.Id, t.Title ?? "",
                string.Equals(artist, unknownArtist, StringComparison.OrdinalIgnoreCase) ? "" : artist,
                string.Equals(album, unknownAlbum, StringComparison.OrdinalIgnoreCase) ? "" : album,
                t.DisplayGenre ?? "",
                t.Duration.TotalSeconds > 20 ? t.Duration.TotalSeconds : 200));
        }
        string[] fakeGenres = { "Rock", "Pop", "Electro", "Hip-Hop", "Jazz" };
        for (int i = pool.Count; i < 12; i++)
            pool.Add(("debug-" + i, "Debug track " + (i + 1), "Debug artist " + (i % 4 + 1), "Debug album", fakeGenres[i % fakeGenres.Length], 150 + rnd.Next(120)));

        // Quelques titres tres ecoutes, une longue queue : poids ~ 1 / rang
        var weights = pool.Select((_, i) => 1.0 / Math.Pow(i + 1, 0.85)).ToArray();
        double totalWeight = weights.Sum();
        int PickTrack()
        {
            double r = rnd.NextDouble() * totalWeight;
            for (int i = 0; i < weights.Length; i++) { r -= weights[i]; if (r <= 0) return i; }
            return weights.Length - 1;
        }

        var monthWeights = Enumerable.Range(0, 12).Select(_ => 0.5 + rnd.NextDouble()).ToArray();
        double monthTotal = monthWeights.Sum();
        int PickMonth()
        {
            double r = rnd.NextDouble() * monthTotal;
            for (int i = 0; i < 12; i++) { r -= monthWeights[i]; if (r <= 0) return i + 1; }
            return 12;
        }

        var events = new List<PlayEvent>();
        for (int i = 0; i < 1800; i++)
        {
            var p = pool[PickTrack()];
            int month = PickMonth();
            int day = rnd.Next(1, DateTime.DaysInMonth(year, month) + 1);
            int hour = rnd.NextDouble() < 0.65 ? rnd.Next(19, 24) : rnd.Next(7, 19);
            var local = new DateTime(year, month, day, hour, rnd.Next(60), rnd.Next(60), DateTimeKind.Local);
            events.Add(new PlayEvent
            {
                EventId = "debug-" + i,
                StartedUtc = local.ToUniversalTime(),
                TrackId = p.Id, Title = p.Title, Artist = p.Artist, Album = p.Album, Genre = p.Genre,
                DurationSeconds = p.Dur,
                ListenedSeconds = p.Dur * (0.55 + rnd.NextDouble() * 0.45)
            });
        }
        return events;
    }
#endif
}
