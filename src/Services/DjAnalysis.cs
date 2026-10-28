using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Resona.Services;

/// <summary>
/// Analyse rythmique du moteur DJ : tempo precis (BPM) et phase des temps (position d'un temps), ponderes sur les
/// BASSES (grosse caisse) pour que la grille se cale sur les kicks et non sur les contretemps (charleys).
/// Chaque estimation a un indice de NETTETE (meilleure phase / moyenne des phases) : sous le seuil, le rythme n'est pas fiable
/// (acoustique, live, tempo qui derive...) et le moteur retombe sur un fondu simple au lieu de caler a tort.
/// </summary>
internal static class DjAnalysis
{
    public const int Hop = 256; // frames @48 kHz entre deux points de l'enveloppe (5,33 ms)
    public const double HopSec = Hop / (double)DjSource.Rate;

    /// <summary>Nettete minimale du tempo (grille reguliere ~ 5 a 11, bruit ~ 1,6).</summary>
    public const double MinTempoPeak = 2.6;
    /// <summary>Nettete minimale de la phase (tempo deja connu, fenetre plus courte).</summary>
    public const double MinPhasePeak = 2.2;

    private sealed class Entry
    {
        public double Bpm { get; set; }
        public double Peak { get; set; }
    }

    private static readonly object _lock = new();
    private static Dictionary<string, Entry>? _cache;
    // Nouveau format (BPM + nettete) : nouveau fichier. L'ancien djbpm.json (BPM seuls, sans fiabilite) est simplement ignore : ce n'est qu'un cache, il se reconstruit.
    private static readonly string CachePath = Path.Combine(AppPaths.AppDataDir, "djbpm2.json");

    // ───────── Enveloppe d'onset ─────────

    /// <summary>Lit [startSec, startSec+lenSec] du fichier et renvoie l'enveloppe d'onset (basses ponderees x2).</summary>
    public static float[] Envelope(string path, TimeSpan knownDuration, double startSec, double lenSec, out double actualStart)
    {
        using var src = DjSource.Open(path, knownDuration);
        actualStart = src.Seek(TimeSpan.FromSeconds(Math.Max(0, startSec)));
        var p = src.Provider;

        int maxHops = (int)(lenSec * DjSource.Rate / Hop);
        var onset = new List<float>(maxHops);
        var buf = new float[Hop * 2];
        float lp = 0f;
        float a = (float)(1 - Math.Exp(-2 * Math.PI * 150.0 / DjSource.Rate));
        double prevBass = 0, prevFull = 0;

        for (int h = 0; h < maxHops; h++)
        {
            int got = 0;
            while (got < buf.Length)
            {
                int n = p.Read(buf, got, buf.Length - got);
                if (n <= 0) break;
                got += n;
            }
            if (got < buf.Length) break;

            double bass = 0, full = 0;
            for (int i = 0; i < buf.Length; i += 2)
            {
                float m = 0.5f * (buf[i] + buf[i + 1]);
                lp += a * (m - lp);
                bass += lp * lp;
                full += m * m;
            }
            double b = Math.Sqrt(bass / Hop), f = Math.Sqrt(full / Hop);
            float flux = (float)(2.0 * Math.Max(0, b - prevBass) * 4.0 + Math.Max(0, f - prevFull));
            prevBass = b; prevFull = f;
            onset.Add(flux);
        }
        return onset.ToArray();
    }

    // ───────── Peigne de temps ─────────

    private static double Comb(float[] on, double periodFrames, double phaseFrames)
    {
        double sum = 0; int cnt = 0;
        for (double t = phaseFrames; t < on.Length - 1; t += periodFrames)
        {
            int i = (int)t;
            double fr = t - i;
            sum += on[i] * (1 - fr) + on[i + 1] * fr;
            cnt++;
        }
        return cnt > 0 ? sum / cnt : 0;
    }

    /// <summary>Meilleure phase pour une periode donnee. peak = score de la meilleure phase / score moyen des phases (nettete de la grille).</summary>
    private static double BestPhase(float[] on, double periodFrames, out double score, double step, out double peak)
    {
        double best = 0, sum = 0; int cnt = 0;
        score = double.MinValue;
        for (double ph = 0; ph < periodFrames; ph += step)
        {
            double s = Comb(on, periodFrames, ph);
            sum += s; cnt++;
            if (s > score) { score = s; best = ph; }
        }
        double mean = cnt > 0 ? sum / cnt : 0;
        peak = mean > 1e-9 ? score / mean : 1.0;
        return best;
    }

    /// <summary>BPM precis (70-180) a partir d'une enveloppe d'onset. Renvoie 0 si inexploitable. peak = nettete de la grille trouvee.</summary>
    public static double EstimateBpm(float[] on, out double peak)
    {
        peak = 1.0;
        if (on.Length < 400) return 0;
        double bestBpm = 0, bestScore = 0;
        for (double bpm = 70; bpm <= 180; bpm += 0.1)
        {
            double period = 60.0 / bpm / HopSec;
            BestPhase(on, period, out double s, 3, out _);
            if (bpm < 80 || bpm > 165) s *= 0.93;
            if (s > bestScore) { bestScore = s; bestBpm = bpm; }
        }
        if (bestBpm <= 0) return 0;

        // Affinage fin autour du meilleur candidat.
        double fine = bestBpm, fineScore = 0, finePeak = 1.0;
        for (double bpm = bestBpm - 0.15; bpm <= bestBpm + 0.15; bpm += 0.01)
        {
            double period = 60.0 / bpm / HopSec;
            BestPhase(on, period, out double s, 1, out double pk);
            if (s > fineScore) { fineScore = s; fine = bpm; finePeak = pk; }
        }
        peak = finePeak;
        return fine;
    }

    public static double EstimateBpm(float[] on) => EstimateBpm(on, out _);

    /// <summary>Instant (secondes, relatif au debut de l'enveloppe) d'un temps de la grille, calé sur les basses. peak = nettete de la phase.</summary>
    public static double BeatPhaseSeconds(float[] on, double bpm, out double peak)
    {
        double period = 60.0 / bpm / HopSec;
        double ph = BestPhase(on, period, out _, 0.5, out peak);
        return ph * HopSec;
    }

    public static double BeatPhaseSeconds(float[] on, double bpm) => BeatPhaseSeconds(on, bpm, out _);

    // ───────── BPM avec cache ─────────

    private static string Key(string path, TimeSpan dur)
    {
        long ticks = 0;
        try { ticks = File.GetLastWriteTimeUtc(path).Ticks; } catch { }
        return $"{path}|{(long)dur.TotalSeconds}|{ticks}";
    }

    private static void LoadCache()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, Entry>();
        try
        {
            if (File.Exists(CachePath))
                _cache = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(CachePath)) ?? _cache;
        }
        catch { }
    }

    private static void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(_cache));
        }
        catch { }
    }

    /// <summary>
    /// BPM du morceau (fenetre centrale de 60 s), ou 0 si inconnu OU PEU FIABLE (grille pas assez nette) : l'appelant retombe alors sur un fondu simple.
    /// Mis en cache ; une analyse qui a echoue (fichier verrouille...) n'est pas memorisee et sera retentee.
    /// </summary>
    public static double GetBpm(string path, TimeSpan dur)
    {
        string key = Key(path, dur);
        lock (_lock)
        {
            LoadCache();
            if (_cache!.TryGetValue(key, out var e)) return e.Peak >= MinTempoPeak ? e.Bpm : 0;
        }
        double bpm, peak;
        try
        {
            double total = dur.TotalSeconds;
            double start = total > 90 ? total / 2 - 30 : 0;
            double len = total > 0 ? Math.Min(60, total - start) : 60;
            var env = Envelope(path, dur, start, len, out _);
            bpm = EstimateBpm(env, out peak);
        }
        catch { return 0; }
        lock (_lock)
        {
            LoadCache();
            _cache![key] = new Entry { Bpm = bpm, Peak = peak };
            SaveCache();
        }
        return peak >= MinTempoPeak ? bpm : 0;
    }
}
