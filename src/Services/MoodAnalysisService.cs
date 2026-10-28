// ============================================================
//  MoodAnalysisService.cs  —  Resona
//  Analyse d'ambiance des titres : BPM (tempo), energie et "vibe", utilises par les playlists intelligentes.
//
//  Source des valeurs, par ordre de priorite :
//    1. METADONNEES du fichier : tag BPM, niveau d'energie (ex. Mixed In Key : EnergyLevel 1-10), tag MOOD.
//    2. CALCUL LOCAL sur ~40 s d'audio (milieu du morceau) : BPM par autocorrelation du flux spectral,
//       energie = volume (RMS) + activite spectrale + brillance + tempo, brillance = centroide spectral.
//  Ce sont des ESTIMATIONS heuristiques (pas de modele ML) : suffisantes pour trier / filtrer, pas pour du mesurage exact.
//
//  Stockage : %LOCALAPPDATA%\Resona\moodanalysis.json  (cache recalculable ; cle = chemin du fichier).
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Dsp;
using NAudio.Wave;
using Resona.Models;

namespace Resona.Services;

public sealed class MoodData
{
    [JsonPropertyName("bpm")] public double Bpm { get; set; }            // 0 = inconnu
    [JsonPropertyName("en")]  public int    Energy { get; set; }         // 0-100
    [JsonPropertyName("br")]  public int    Brightness { get; set; }     // 0-100 (sombre -> brillant)
    [JsonPropertyName("vb")]  public string Vibe { get; set; } = "";     // cle de MoodAnalysisService.VibeKeys
    [JsonPropertyName("src")] public string Source { get; set; } = "";   // "tags", "calc" ou "tags+calc"
    [JsonPropertyName("v")]   public int    Version { get; set; } = MoodAnalysisService.AlgorithmVersion;
}

public sealed class MoodAnalysisService
{
    /// <summary>A incrementer quand l'algorithme change : les titres analyses avec une ancienne version seront recalcules.</summary>
    public const int AlgorithmVersion = 1;

    public static readonly string[] VibeKeys = { "calm", "melancholic", "chill", "dark", "upbeat", "intense", "energetic" };

    public static string VibeLabel(string key, bool fr) => key switch
    {
        "calm"        => fr ? "Calme" : "Calm",
        "melancholic" => fr ? "Mélancolique" : "Melancholic",
        "chill"       => fr ? "Détendu" : "Chill",
        "dark"        => fr ? "Sombre" : "Dark",
        "upbeat"      => fr ? "Joyeux / entraînant" : "Upbeat",
        "intense"     => fr ? "Intense" : "Intense",
        "energetic"   => fr ? "Énergique" : "Energetic",
        _             => key
    };

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly Dictionary<string, MoodData> _data = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsRunning { get; private set; }

    public MoodAnalysisService()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona");
        try { Directory.CreateDirectory(dir); } catch { }
        _filePath = Path.Combine(dir, "moodanalysis.json");
        Load();
    }

    // ------------------------------------------------------------------ acces

    /// <summary>Cle d'un fichier : separateurs unifies (C:/x et C:\x designent le meme fichier), utilisee partout pour l'analyse d'ambiance.</summary>
    public static string Key(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        try { return Path.GetFullPath(path); }
        catch { return path.Replace('/', '\\'); }
    }

    public MoodData? Get(string filePath)
    {
        lock (_lock) { return _data.TryGetValue(Key(filePath), out var d) && d.Version == AlgorithmVersion ? d : null; }
    }

    /// <summary>Copie des donnees (utilisee par SmartContext pour evaluer les regles sans verrou).</summary>
    public Dictionary<string, MoodData> Snapshot()
    {
        lock (_lock)
            return _data.Where(kv => kv.Value.Version == AlgorithmVersion)
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    public List<Track> Pending(IEnumerable<Track> tracks)
    {
        lock (_lock)
            return tracks.Where(t => !t.IsExternal && !string.IsNullOrEmpty(t.FilePath)
                                     && !(_data.TryGetValue(Key(t.FilePath), out var d) && d.Version == AlgorithmVersion))
                        // Meme fichier present plusieurs fois dans la liste (lignes en double de la base) : analyse et compte une seule fois.
                        .GroupBy(t => Key(t.FilePath), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    /// <summary>Supprime toutes les analyses (elles seront recalculees a la demande).</summary>
    public void Clear()
    {
        lock (_lock) { _data.Clear(); }
        Save();
    }

    // ------------------------------------------------------------------ analyse par lot

    /// <summary>Analyse les titres pas encore analyses. Un seul lot a la fois : les appels simultanes s'enchainent.</summary>
    public async Task AnalyzeAsync(IEnumerable<Track> tracks, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var requested = tracks.ToList();
        await _gate.WaitAsync(ct);
        try
        {
            var todo = Pending(requested);   // recalcule apres l'attente : un autre lot a pu en faire une partie
            if (todo.Count == 0) { progress?.Report((0, 0)); return; }

            IsRunning = true;
            int done = 0, sinceSave = 0;
            progress?.Report((0, todo.Count));

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 4, 1, 3),
                CancellationToken = ct
            };
            try
            {
                await Parallel.ForEachAsync(todo, options, async (track, token) =>
                {
                    MoodData? result = await Task.Run(() => AnalyzeTrack(track), token);
                    // Titre illisible : on enregistre un resultat neutre pour ne pas le retenter a chaque ouverture.
                    result ??= new MoodData { Bpm = 0, Energy = 50, Brightness = 50, Vibe = "chill", Source = "none" };
                    bool save = false;
                    lock (_lock)
                    {
                        _data[Key(track.FilePath)] = result;
                        if (++sinceSave >= 50) { sinceSave = 0; save = true; }
                    }
                    if (save) Save();
                    progress?.Report((Interlocked.Increment(ref done), todo.Count));
                });
            }
            finally
            {
                Save();
                IsRunning = false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // ------------------------------------------------------------------ analyse d'un titre

    private static MoodData? AnalyzeTrack(Track track)
    {
        try
        {
            ReadTags(track.FilePath, out double tagBpm, out int tagEnergy, out string tagVibe);
            bool allFromTags = tagBpm > 0 && tagEnergy >= 0 && tagVibe.Length > 0;

            double bpm = tagBpm;
            int energy = tagEnergy;
            int brightness = 50;
            string vibe = tagVibe;
            bool calculated = false;

            if (!allFromTags)
            {
                var calc = ComputeFromAudio(track);
                if (calc != null)
                {
                    calculated = true;
                    if (bpm <= 0) bpm = calc.Value.Bpm;
                    if (energy < 0) energy = calc.Value.Energy;
                    brightness = calc.Value.Brightness;
                }
            }

            if (bpm <= 0 && energy < 0 && vibe.Length == 0) return null;   // rien de fiable
            if (energy < 0) energy = 50;
            if (vibe.Length == 0) vibe = DeriveVibe(energy, brightness);

            bool fromTags = tagBpm > 0 || tagEnergy >= 0 || tagVibe.Length > 0;
            return new MoodData
            {
                Bpm = Math.Round(bpm, 1),
                Energy = Math.Clamp(energy, 0, 100),
                Brightness = Math.Clamp(brightness, 0, 100),
                Vibe = vibe,
                Source = fromTags && calculated ? "tags+calc" : fromTags ? "tags" : "calc"
            };
        }
        catch (Exception ex)
        {
            LogService.Write("MoodAnalysis", ex, track.FilePath);
            return null;
        }
    }

    /// <summary>Vibe estimee a partir de l'energie (calme -> intense) et de la brillance du spectre (sombre -> brillant).</summary>
    public static string DeriveVibe(int energy, int brightness)
    {
        bool dark = brightness < 40;
        if (energy < 30) return dark ? "melancholic" : "calm";
        if (energy < 55) return dark ? "dark" : "chill";
        if (energy < 75) return dark ? "dark" : "upbeat";
        return dark ? "intense" : "energetic";
    }

    // ------------------------------------------------------------------ metadonnees

    private static void ReadTags(string path, out double bpm, out int energy, out string vibe)
    {
        bpm = 0; energy = -1; vibe = "";
        try
        {
            using var f = TagLib.File.Create(path);
            if (f.Tag.BeatsPerMinute > 0 && f.Tag.BeatsPerMinute < 400) bpm = f.Tag.BeatsPerMinute;

            string? energyText = null, moodText = null;

            if (f.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3)
            {
                moodText = id3.GetTextAsString("TMOO");
                foreach (string key in new[] { "EnergyLevel", "ENERGY", "Energy" })
                {
                    energyText = TagLib.Id3v2.UserTextInformationFrame.Get(id3, key, false)?.Text?.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(energyText)) break;
                }
            }
            if (f.GetTag(TagLib.TagTypes.Xiph, false) is TagLib.Ogg.XiphComment xiph)
            {
                moodText ??= xiph.GetFirstField("MOOD");
                foreach (string key in new[] { "ENERGYLEVEL", "ENERGY" })
                {
                    energyText ??= xiph.GetFirstField(key);
                }
            }

            if (double.TryParse((energyText ?? "").Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double e) && e > 0)
            {
                energy = (int)Math.Round(e <= 10 ? e * 10 : e);   // 1-10 (Mixed In Key) -> 0-100
                energy = Math.Clamp(energy, 0, 100);
            }
            vibe = MapMoodText(moodText);
        }
        catch { }
    }

    private static string MapMoodText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string t = text.ToLowerInvariant();
        bool Has(params string[] words) => words.Any(t.Contains);
        if (Has("sad", "melanch", "triste", "mélanch", "blue")) return "melancholic";
        if (Has("dark", "sombre", "gloom", "moody", "brood")) return "dark";
        if (Has("aggress", "angry", "intens", "heavy", "agress")) return "intense";
        if (Has("energ", "énerg", "power", "party", "festif", "pump")) return "energetic";
        if (Has("happy", "joy", "upbeat", "uplift", "heureux", "joyeux", "fun", "feel good")) return "upbeat";
        if (Has("chill", "relax", "mellow", "détend", "laid")) return "chill";
        if (Has("calm", "peace", "serene", "sleep", "ambient", "soft", "doux")) return "calm";
        return "";
    }

    // ------------------------------------------------------------------ calcul local

    private readonly struct Calc
    {
        public double Bpm { get; init; }
        public int Energy { get; init; }
        public int Brightness { get; init; }
    }

    private const double WindowSeconds = 40.0;

    private static Calc? ComputeFromAudio(Track track)
    {
        IDisposable? owner = null;
        try
        {
            ISampleProvider provider;
            Action<TimeSpan>? seek = null;
            string ext = Path.GetExtension(LibraryScannerService.ResolveRealExtension(track.FilePath)).ToLowerInvariant();

            if (ext is ".opus" or ".ogg")
            {
                var opus = new OpusSampleProvider(track.FilePath, track.Duration);
                owner = opus; provider = opus; seek = ts => opus.CurrentTime = ts;
            }
            else
            {
                try
                {
                    var afr = new AudioFileReader(track.FilePath);
                    owner = afr; provider = afr; seek = ts => afr.CurrentTime = ts;
                }
                catch
                {
                    var ws = SafeAudioReader.Create(track.FilePath);
                    owner = ws; provider = ws.ToSampleProvider(); seek = ts => ws.CurrentTime = ts;
                }
            }

            // Fenetre au milieu du morceau (evite intro / outro).
            double total = track.Duration.TotalSeconds;
            if (total > WindowSeconds + 10 && seek != null)
            {
                try { seek(TimeSpan.FromSeconds(Math.Max(0, (total - WindowSeconds) / 2))); } catch { }
            }

            int sr = provider.WaveFormat.SampleRate;
            int ch = Math.Max(1, provider.WaveFormat.Channels);
            int maxMono = (int)(sr * WindowSeconds);
            var mono = new float[maxMono];
            int filled = 0;
            var buf = new float[4096 * ch];
            while (filled < maxMono)
            {
                int read = provider.Read(buf, 0, buf.Length);
                if (read <= 0) break;
                for (int i = 0; i + ch <= read && filled < maxMono; i += ch)
                {
                    float s = 0;
                    for (int c = 0; c < ch; c++) s += buf[i + c];
                    mono[filled++] = s / ch;
                }
            }
            if (filled < sr * 8) return null;   // moins de 8 s : trop court pour une estimation fiable
            return Analyze(mono, filled, sr);
        }
        finally
        {
            try { owner?.Dispose(); } catch { }
        }
    }

    private static Calc? Analyze(float[] x, int n, int sr)
    {
        // Taille de trame ~23 ms (puissance de 2), recouvrement 50 %.
        int m = Math.Clamp((int)Math.Round(Math.Log2(sr * 0.023)), 9, 12);
        int frame = 1 << m, hop = frame / 2, bins = frame / 2;
        int frames = (n - frame) / hop + 1;
        if (frames < 200) return null;

        var hann = new double[frame];
        for (int i = 0; i < frame; i++) hann[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (frame - 1));

        var cbuf = new Complex[frame];
        var prev = new double[bins];
        var mag = new double[bins];
        var env = new double[frames];          // flux spectral (domaine log) -> BPM
        double fluxRatioSum = 0, centroidSum = 0, sumSq = 0;
        int fluxFrames = 0, centroidFrames = 0;

        for (int f = 0; f < frames; f++)
        {
            int off = f * hop;
            double fs = 0;
            for (int i = 0; i < frame; i++)
            {
                float s = x[off + i];
                fs += s * s;
                cbuf[i].X = (float)(s * hann[i]);
                cbuf[i].Y = 0;
            }
            sumSq += fs / frame;
            FastFourierTransform.FFT(true, m, cbuf);

            double magSum = 0, weighted = 0, logFlux = 0, linFlux = 0;
            for (int k = 1; k < bins; k++)
            {
                double a = Math.Sqrt(cbuf[k].X * cbuf[k].X + cbuf[k].Y * cbuf[k].Y);
                mag[k] = a;
                magSum += a;
                weighted += a * k;
                double d = a - prev[k];
                if (d > 0) linFlux += d;
                double lg = Math.Log(1 + 10 * a) - Math.Log(1 + 10 * prev[k]);
                if (lg > 0) logFlux += lg;
            }
            env[f] = f == 0 ? 0 : logFlux;
            if (magSum > 1e-6)
            {
                centroidSum += weighted / magSum * sr / frame;   // Hz
                centroidFrames++;
                if (f > 0) { fluxRatioSum += linFlux / magSum; fluxFrames++; }
            }
            Array.Copy(mag, prev, bins);
        }

        // ---- BPM : autocorrelation de l'enveloppe d'onsets (moyenne locale retiree, rectifiee) ----
        double frameRate = (double)sr / hop;
        int win = Math.Max(3, (int)(frameRate * 0.4));
        var onset = new double[frames];
        for (int i = 0; i < frames; i++)
        {
            int a = Math.Max(0, i - win), b = Math.Min(frames - 1, i + win);
            double mean = 0;
            for (int j = a; j <= b; j++) mean += env[j];
            mean /= (b - a + 1);
            onset[i] = Math.Max(0, env[i] - mean);
        }

        int minLag = Math.Max(2, (int)Math.Floor(frameRate * 60.0 / 200.0));
        int maxLag = Math.Min(frames / 2, (int)Math.Ceiling(frameRate * 60.0 / 60.0));
        double bestScore = 0; int bestLag = 0;
        var acf = new double[maxLag + 2];
        for (int lag = minLag - 1; lag <= maxLag + 1 && lag < frames; lag++)
        {
            double s = 0;
            for (int i = 0; i + lag < frames; i++) s += onset[i] * onset[i + lag];
            acf[lag] = s / (frames - lag);
        }
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double bpmAtLag = 60.0 * frameRate / lag;
            double prior = Math.Exp(-0.5 * Math.Pow(Math.Log2(bpmAtLag / 120.0) / 0.9, 2));   // prefere les tempos "naturels"
            double score = acf[lag] * prior;
            if (score > bestScore) { bestScore = score; bestLag = lag; }
        }
        double bpm = 0;
        if (bestLag > 0 && acf[bestLag] > 0)
        {
            double lagRefined = bestLag;
            double y0 = acf[bestLag - 1], y1 = acf[bestLag], y2 = acf[bestLag + 1];
            double denom = y0 - 2 * y1 + y2;
            if (Math.Abs(denom) > 1e-12) lagRefined += 0.5 * (y0 - y2) / denom;
            bpm = 60.0 * frameRate / lagRefined;
            while (bpm > 0 && bpm < 70) bpm *= 2;
            while (bpm > 180) bpm /= 2;
        }

        // ---- Energie et brillance ----
        double rms = Math.Sqrt(sumSq / frames);
        double rmsDb = 20 * Math.Log10(rms + 1e-9);
        double loud = Math.Clamp((rmsDb + 32) / 24.0, 0, 1);                       // -32 dB -> 0 ; -8 dB -> 1
        double fluxRatio = fluxFrames > 0 ? fluxRatioSum / fluxFrames : 0;
        double activity = Math.Clamp((fluxRatio - 0.08) / (0.35 - 0.08), 0, 1);    // attaques / densite rythmique
        double centroidHz = centroidFrames > 0 ? centroidSum / centroidFrames : 1500;
        double bright = Math.Clamp((centroidHz - 800) / 3200.0, 0, 1);             // 800 Hz -> 0 ; 4 kHz -> 1
        double tempo = bpm > 0 ? Math.Clamp((bpm - 60) / 100.0, 0, 1) : 0.5;

        double energy = 100 * (0.40 * loud + 0.25 * activity + 0.20 * bright + 0.15 * tempo);
        return new Calc { Bpm = bpm, Energy = (int)Math.Round(energy), Brightness = (int)Math.Round(bright * 100) };
    }

    // ------------------------------------------------------------------ fichier

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var dict = JsonSerializer.Deserialize<Dictionary<string, MoodData>>(File.ReadAllText(_filePath));
            if (dict == null) return;
            foreach (var kv in dict) _data[Key(kv.Key)] = kv.Value;   // normalise aussi les anciennes cles (C:/x et C:\x fusionnent)
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            string json;
            lock (_lock) { json = JsonSerializer.Serialize(_data); }
            string tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, true);
        }
        catch { }
    }
}
