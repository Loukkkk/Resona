using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Resona.Models;

namespace Resona.Services;

/// <summary>
/// Moteur audio DJ (mode soirée), TOTALEMENT SEPARE du moteur normal d'AudioEngineService.
/// Deux platines + un mixeur : quand une piste suivante est demandee pendant la lecture, on analyse les deux pistes
/// (BPM + phase des temps calée sur les basses), on cale le tempo de la suivante par etirement temporel (WSOLA,
/// quelques % max) puis on enchaine sur un TEMPS de la piste en cours, avec un fondu equal-power + echange des basses.
/// Aucun silence : la piste suivante demarre pile sur un temps, le debut avant ce temps est simplement coupe.
/// Si l'analyse echoue, si le rythme n'est pas assez net ou si les tempos sont trop differents, on retombe sur un fondu enchaine simple.
///
/// Etat expose a l'interface (IsActive, State, CurrentPosition, TotalDuration) : copie "instantanee" publiee SANS verrou, mise a jour par
/// PublishLocked() a chaque changement d'etat et a chaque bloc audio. Le minuteur de l'interface ne peut donc jamais rester bloque
/// derriere le thread audio (decodage, recherche dans un fichier...).
/// </summary>
public sealed class DjMixerEngine : IDisposable
{
    private const int Rate = DjSource.Rate;

    private readonly object _gate = new();
    private readonly Source _mixSource;
    private readonly RmsCaptureSampleProvider _rms;
    private readonly VolumeSampleProvider _volume;
    private readonly EqualizerSampleProvider _eq;
    private readonly SafetyLimiterSampleProvider _final;
    private WasapiOut? _wasapi;
    private readonly object _outLock = new();

    private DjDeck? _a;            // piste en cours (sortante pendant un mix)
    private DjDeck? _b;            // piste entrante (en attente ou en cours de mix)
    private bool _mixActive;
    private double _trigger;       // position (s) dans _a a laquelle _b demarre
    private int _mixFrames, _mixPos;
    private bool _paused;
    private float _pauseGain = 1f;
    private int _fadeIn;
    private bool _endRaised;
    private int _recoverTries;     // reouvertures de la platine apres une fin anormale (voir MixRead)
    private PlaybackState _state = PlaybackState.Stopped;
    private int _req;
    // Piste demandee mais pas encore audible : tant qu'elle n'est pas en place, la position affichee reste a 0 et la
    // duree affichee est la sienne (sinon la barre de lecture melange la position de l'ancienne piste et la duree de la nouvelle).
    private Track? _dispTrack;
    private TimeSpan _dispTotal;
    private TimeSpan? _seekPending;   // clic sur la barre pendant qu'une piste est en cours de chargement / analyse
    private float _userVolume = 1f;
    // Mix cale sur les temps : nombre de temps du mix, duree d'un temps (frames de sortie). false = repli en fondu equal-power simple.
    private bool _beatMode;
    private int _kBeats = 8;
    private double _beatFrames = Rate / 2.0;

    // Copie instantanee lisible sans verrou (ecrite uniquement par PublishLocked, sous _gate)
    private volatile bool _pActive;
    private int _pState = (int)PlaybackState.Stopped;
    private double _pPos, _pTotal;

    public event EventHandler? PlaybackEnded;

    public DjMixerEngine()
    {
        _mixSource = new Source(this);
        _rms = new RmsCaptureSampleProvider(_mixSource);
        _volume = new VolumeSampleProvider(_rms) { Volume = 1f };
        _eq = new EqualizerSampleProvider(new DuckingSampleProvider(_volume));
        _eq.SetEnabled(App.Settings.Current.EqualizerEnabled);
        if (App.Settings.Current.EqualizerEnabled)
        {
            var bands = App.Settings.Current.EqualizerBands;
            for (int i = 0; i < bands.Length && i < 10; i++) _eq.SetBand(i, (float)bands[i]);
        }
        _final = new SafetyLimiterSampleProvider(_eq);
    }

    // ───────── Etat expose a AudioEngineService (sans verrou) ─────────

    public bool IsActive => _pActive;
    public PlaybackState State => (PlaybackState)Volatile.Read(ref _pState);
    public TimeSpan TotalDuration => TimeSpan.FromSeconds(Volatile.Read(ref _pTotal));
    public TimeSpan CurrentPosition => TimeSpan.FromSeconds(Volatile.Read(ref _pPos));
    public float RmsLevel => _rms.CurrentRms;
    public float BassLevel => _rms.CurrentBass;

    private DjDeck? Shown => _b ?? _a;

    /// <summary>Recalcule la copie instantanee. A appeler sous _gate apres tout changement d'etat.</summary>
    private void PublishLocked()
    {
        _pActive = _a != null;
        Volatile.Write(ref _pState, (int)_state);

        TimeSpan total;
        if (_dispTrack != null && _dispTotal > TimeSpan.Zero) total = _dispTotal;
        else total = Shown?.Total ?? TimeSpan.Zero;

        double pos = 0;
        if (_dispTrack == null)                                    // piste demandee, pas encore audible : 0
        {
            var d = Shown;
            if (d != null && !(_b != null && !_mixActive))         // entrante en attente de son temps de depart : 0
                pos = d.Ended ? d.Total.TotalSeconds : d.PositionSec;
        }
        Volatile.Write(ref _pTotal, total.TotalSeconds);
        Volatile.Write(ref _pPos, pos);
    }

    // ───────── Reglages ─────────

    public void SetUserVolume(float volume01)
    {
        _userVolume = Math.Clamp(volume01, 0f, 1f);
        _volume.Volume = _userVolume;
    }

    public void SetNormalizationGain(double gainDb)
    {
        lock (_gate)
        {
            var d = Shown;
            if (d != null) d.TargetGain = DbToLinear(gainDb);
        }
    }

    public void SetEqualizerEnabled(bool enabled) => _eq.SetEnabled(enabled);
    public void SetEqualizerBand(int index, float gainDb) => _eq.SetBand(index, gainDb);

    private static double DbToLinear(double db) => Math.Min(Math.Pow(10, db / 20.0), 31.6);

    // ───────── Controle ─────────

    public void Pause()
    {
        lock (_gate)
        {
            if (_a == null || _state != PlaybackState.Playing) return;
            _paused = true;
            _state = PlaybackState.Paused;
            PublishLocked();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_a == null || _state != PlaybackState.Paused) return;
            _paused = false;
            _state = PlaybackState.Playing;
            _fadeIn = Math.Max(_fadeIn, 240);
            PublishLocked();
        }
        EnsureOutput();
    }

    public void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            if (_dispTrack != null)
            {
                // Une piste est en cours de chargement / d'analyse : le clic vise CETTE piste, on l'applique des qu'elle est prete.
                _seekPending = position;
                return;
            }
            if (_a == null) return;
            Interlocked.Increment(ref _req);
            // Un mix en attente / en cours est abandonne : la piste affichee (entrante) devient la piste courante.
            // IMPORTANT : que le mix soit en cours OU seulement en attente, la barre de lecture montre la piste ENTRANTE (sa duree, sa position) :
            // le clic vise donc B. Avant, en attente, on appliquait le clic (donne dans la duree de B) a l'ANCIENNE piste, clampe a sa fin :
            // la tete de lecture sautait et on enchainait sur le son suivant.
            bool tookB = false;
            if (_b != null)
            {
                _a.Dispose();
                _a = _b;
                _b = null;
                _mixActive = false;
                tookB = true;
            }
            double t = Math.Max(0, position.TotalSeconds);
            double tot = _a.Total.TotalSeconds;
            if (tot > 0 && t > tot - 0.05) t = Math.Max(0, tot - 0.05);
            if (tookB) _a.Rebuild(t, false, 1.0);          // la piste reprise repart a vitesse normale (plus de mix a caler)
            else _a.Rebuild(t, _a.Stretch != null, _a.Stretch?.Rate ?? 1.0);
            _endRaised = false;
            _recoverTries = 0;
            _fadeIn = 240;
            if (_state == PlaybackState.Stopped) _state = _paused ? PlaybackState.Paused : PlaybackState.Playing;
            PublishLocked();
        }
        EnsureOutput();
    }

    public void Stop()
    {
        Interlocked.Increment(ref _req);
        lock (_gate)
        {
            _a?.Dispose(); _b?.Dispose();
            _a = null; _b = null;
            _dispTrack = null; _seekPending = null;
            _mixActive = false;
            _paused = false;
            _pauseGain = 1f;
            _state = PlaybackState.Stopped;
            PublishLocked();
        }
        WasapiOut? w;
        lock (_outLock) { w = _wasapi; _wasapi = null; }
        try { w?.Stop(); } catch { }
        try { w?.Dispose(); } catch { }
    }

    public void Dispose() => Stop();

    private void EnsureOutput()
    {
        lock (_outLock)
        {
            if (_wasapi != null) return;
            var w = new WasapiOut(AudioClientShareMode.Shared, true, 100);
            w.Init(new SampleToWaveProvider(_final));
            w.Play();
            _wasapi = w;
        }
    }

    // ───────── Lecture d'une piste ─────────

    /// <param name="beatMix">true = enchainement automatique : on tente de caler la transition sur les temps. false = lancement manuel (clic sur un son, suivant/precedent) : simple fondu + saut du silence d'intro.</param>
    public async Task PlayAsync(Track track, double initialGainDb, int crossfadeMs, TimeSpan? startAt, bool startPaused, Action<string>? onDownloadProgress = null, bool beatMix = true)
    {
        int req = Interlocked.Increment(ref _req);
        DuckingService.EnsureStarted();
        double gain = DbToLinear(initialGainDb);
        lock (_gate)
        {
            _dispTrack = track; _dispTotal = track.Duration; _seekPending = null;
            PublishLocked();
        }
        LogService.Write("DJ", $"PlayAsync req={req} : {System.IO.Path.GetFileName(track.FilePath)} (xfade {crossfadeMs} ms, startAt {startAt?.TotalSeconds:0.0})");

        DjSource src;
        try
        {
            // Comme le moteur normal : les .m4a ont besoin de FFmpeg, telecharge au premier usage.
            string ext = System.IO.Path.GetExtension(LibraryScannerService.ResolveRealExtension(track.FilePath)).ToLowerInvariant();
            if (ext == ".m4a") await DownloadService.EnsureBinariesAsync(onDownloadProgress);
            src = await Task.Run(() => DjSource.Open(track.FilePath, track.Duration));
        }
        catch { lock (_gate) { if (req == _req) { _dispTrack = null; PublishLocked(); } } throw; }
        if (req != _req) { src.Dispose(); return; }

        bool canMix;
        lock (_gate)
        {
            canMix = _a != null && !_a.Ended && !_paused && _state == PlaybackState.Playing
                     && !startPaused && !(startAt.HasValue && startAt.Value > TimeSpan.Zero);
        }

        if (!canMix)
        {
            await StartFreshAsync(req, track, src, gain, startAt, startPaused);
            return;
        }

        await TransitionAsync(req, track, src, gain, crossfadeMs, beatMix);
    }

    private async Task StartFreshAsync(int req, Track track, DjSource src, double gain, TimeSpan? startAt, bool startPaused)
    {
        bool resume = startAt.HasValue && startAt.Value > TimeSpan.Zero;
        double resumeAt = resume ? startAt!.Value.TotalSeconds : 0;
        DjDeck deck;
        try
        {
            // Creation de la platine ET saut du silence d'intro hors du thread de l'interface : le decodage d'un fichier "a froid"
            // (premier son apres le demarrage de l'app) ne fige plus la fenetre, et le lecteur est cree et lu depuis les memes threads.
            deck = await Task.Run(() =>
            {
                var d = new DjDeck(track, src, resumeAt, false, 1.0);
                if (!resume) d.SkipLeadingSilence(6);
                return d;
            });
        }
        catch
        {
            src.Dispose();
            lock (_gate) { if (req == _req) { _dispTrack = null; PublishLocked(); } }
            throw;
        }
        deck.Gain = deck.TargetGain = gain;
        LogService.Write("DJ", $"Piste prete : duree decodeur {deck.Total.TotalSeconds:0.0}s, duree bibliotheque {track.Duration.TotalSeconds:0.0}s, depart {deck.PositionSec:0.00}s ({track.Title})");

        lock (_gate)
        {
            if (req != _req) { deck.Dispose(); return; }
            if (_seekPending is TimeSpan sp)
            {
                _seekPending = null;
                deck.Rebuild(Math.Clamp(sp.TotalSeconds, 0, Math.Max(0, deck.Total.TotalSeconds - 0.05)), false, 1.0);
            }
            _dispTrack = null;
            _a?.Dispose(); _b?.Dispose();
            _a = deck; _b = null; _mixActive = false;
            _paused = startPaused;
            _pauseGain = startPaused ? 0f : 1f;
            _fadeIn = startPaused ? 0 : 480;
            _endRaised = false;
            _recoverTries = 0;
            _state = startPaused ? PlaybackState.Paused : PlaybackState.Playing;
            PublishLocked();
        }
        EnsureOutput();

        // Analyse du BPM en tache de fond : sera deja en cache pour la prochaine transition.
        string p = track.FilePath; TimeSpan d2 = deck.Total;
        _ = Task.Run(() => { try { DjAnalysis.GetBpm(p, d2); } catch { } });
    }

    /// <summary>Si l'utilisateur a clique sur la barre pendant la preparation : on abandonne le mix et on joue la nouvelle piste a cet endroit.</summary>
    private bool TrySeekSwapLocked(DjDeck b)
    {
        if (_seekPending is not TimeSpan sp) return false;
        _seekPending = null;
        double t = Math.Clamp(sp.TotalSeconds, 0, Math.Max(0, b.Total.TotalSeconds - 0.05));
        b.Rebuild(t, false, 1.0);
        _a?.Dispose();
        _a = b; _b = null; _mixActive = false;
        _fadeIn = 240; _endRaised = false; _recoverTries = 0; _dispTrack = null;
        PublishLocked();
        return true;
    }

    private sealed class Plan
    {
        public double StartB;
        public double TriggerA;
        public int MixFrames;
        public double RateB;
        public bool Stretch;
        public int Beats;
        public double BeatFrames;
        public double PeriodA;      // duree d'un temps de A en temps SOURCE (pour recaler le declenchement apres l'analyse)
    }

    private async Task TransitionAsync(int req, Track track, DjSource src, double gain, int crossfadeMs, bool beatMix)
    {
        DjDeck a;
        lock (_gate)
        {
            // Une transition deja en attente / en cours est consolidee avant d'en planifier une autre.
            if (_b != null)
            {
                if (_mixActive) { _a!.Dispose(); _a = _b; }
                else _b.Dispose();
                _b = null; _mixActive = false;
                PublishLocked();
            }
            a = _a!;
        }

        Plan? plan = null;
        DjDeck? b = null;
        try
        {
            // Lancement manuel : pas de calage sur les temps (on ne cherche pas a "coller au beat"), on va directement au repli = fondu simple.
            if (beatMix) plan = await Task.Run(() => BuildPlan(a, track, src));
            else LogService.Write("DJ", "Lancement manuel : fondu simple, sans calage sur les temps");
            if (plan != null)
                b = await Task.Run(() => new DjDeck(track, src, plan.StartB, plan.Stretch, plan.RateB, exact: true));
        }
        catch (Exception ex)
        {
            LogService.Write("DJ", "Analyse impossible, fondu simple : " + ex.Message);
            plan = null; b?.Dispose(); b = null;
        }

        if (req != _req)
        {
            if (b != null) b.Dispose(); else src.Dispose();
            return;
        }

        if (plan == null || b == null)
        {
            // Repli : fondu enchaine simple, immediat, piste suivante demarree apres son silence d'intro.
            DjDeck fb;
            try
            {
                fb = await Task.Run(() =>
                {
                    var d = new DjDeck(track, src, 0, false, 1.0);
                    d.SkipLeadingSilence(6);
                    return d;
                });
            }
            catch
            {
                src.Dispose();
                lock (_gate) { if (req == _req) { _dispTrack = null; PublishLocked(); } }
                throw;
            }
            fb.Gain = fb.TargetGain = gain;
            lock (_gate)
            {
                if (req != _req || _a == null)
                {
                    if (req == _req) { _dispTrack = null; PublishLocked(); }
                    fb.Dispose();
                    return;
                }
                if (TrySeekSwapLocked(fb)) return;
                _dispTrack = null;
                double remain = Math.Max(0.05, _a.Total.TotalSeconds - _a.PositionSec - 0.1);
                double sec = Math.Clamp(crossfadeMs / 1000.0, 0.03, Math.Min(8.0, remain));
                _b = fb; _trigger = double.NegativeInfinity; _mixFrames = Math.Max(256, (int)(sec * Rate)); _mixPos = 0; _mixActive = false;
                _beatMode = false;
                PublishLocked();
            }
            return;
        }

        b.Gain = b.TargetGain = gain;
        lock (_gate)
        {
            if (req != _req || _a != a || a.Ended)
            {
                if (req == _req) { _dispTrack = null; PublishLocked(); }
                b.Dispose();
                return;
            }
            if (TrySeekSwapLocked(b)) return;
            _dispTrack = null;
            // L'analyse a pris du temps pendant lequel A a continue de jouer : le temps de declenchement prevu est peut-etre deja PASSE.
            // On le recale sur le prochain temps de A encore a venir (sinon le mix partait tout de suite, n'importe ou dans la mesure = "juste un fondu").
            double trig = plan.TriggerA;
            double minTrig = a.PositionSec + 0.12;
            if (trig < minTrig) trig += Math.Ceiling((minTrig - trig) / plan.PeriodA) * plan.PeriodA;
            int beats = plan.Beats;
            double beatSec = plan.BeatFrames / Rate;
            double remOut = (a.Total.TotalSeconds - trig) / Math.Max(0.5, a.Stretch?.Rate ?? 1.0) - 0.2;
            while (beats > 2 && beats * beatSec > remOut) beats /= 2;   // le recalage a rogne la fin de A : mix plus court plutot que debordant
            LogService.Write("DJ", $"Mix cale : B demarre a {plan.StartB:0.000}s, declenchement A a {trig:0.000}s (prevu {plan.TriggerA:0.000}s, A a {a.PositionSec:0.000}s), {beats} temps = {beats * beatSec:0.0}s, vitesse B x{plan.RateB:0.0000}");
            _b = b;
            _trigger = trig;
            _mixFrames = (int)(beats * plan.BeatFrames);
            _mixPos = 0;
            _mixActive = false;
            _beatMode = true;
            _kBeats = beats;
            _beatFrames = plan.BeatFrames;
            a.Xo.Reset();
            PublishLocked();
        }
    }

    /// <summary>Calcule ou et comment enchainer. Renvoie null si le mix cale n'est pas possible (-> fondu simple).</summary>
    private Plan? BuildPlan(DjDeck a, Track trackB, DjSource srcB)
    {
        // Les analyses (BPM de B, enveloppes de A et de B) sont independantes : on les lance EN PARALLELE. Chaque seconde d'analyse est une
        // seconde pendant laquelle A continue de jouer, donc moins de retard sur le plan = mix plus proche de la demande.
        string pathA = a.Track.FilePath;
        TimeSpan durA = a.Total;
        double totA = durA.TotalSeconds;
        double posA0 = a.PositionSec;
        if (totA <= 0 || totA - posA0 < 4.0) return null;
        double regStart = posA0 + 0.25;
        double regLen = Math.Min(20.0, totA - regStart - 0.3);
        if (regLen < 5.0) return null;
        string pathB = trackB.FilePath;
        TimeSpan durB = srcB.Total;
        var tBpmA = Task.Run(() => DjAnalysis.GetBpm(pathA, durA));
        var tBpmB = Task.Run(() => DjAnalysis.GetBpm(pathB, durB));
        var tEnvA = Task.Run(() => { var ee = DjAnalysis.Envelope(pathA, durA, regStart, regLen, out double ss); return (Env: ee, Start: ss); });
        var tEnvB = Task.Run(() => { var ee = DjAnalysis.Envelope(pathB, durB, 0, 30, out double ss); return (Env: ee, Start: ss); });
        Task.WaitAll(tBpmA, tBpmB, tEnvA, tEnvB);

        // GetBpm renvoie 0 quand le rythme n'est pas assez net (acoustique, live...) : fondu simple plutot qu'un calage a tort.
        double bpmA = tBpmA.Result, bpmB = tBpmB.Result;
        if (bpmA <= 0 || bpmB <= 0) return null;

        double rA = a.Stretch?.Rate ?? 1.0;
        double effA = bpmA * rA;                      // tempo reellement entendu
        double rb = effA / bpmB;
        double best = rb;
        foreach (double c in new[] { rb * 2, rb / 2 })
            if (Math.Abs(c - 1) < Math.Abs(best - 1)) best = c;
        rb = best;
        // Tempos trop eloignes pour etirer sans deformer : au lieu d'un simple fondu, mix COURT (2 temps) "a la coupe", cale sur la grille de A ;
        // B joue a sa vitesse naturelle, la derive sur 1 a 2 s est inaudible.
        bool cut = Math.Abs(rb - 1) > 0.08;
        if (cut) rb = 1.0;
        bool stretch = !cut && Math.Abs(rb - 1) > 0.002;
        if (!stretch) rb = 1.0;

        // Grille de temps de la piste sortante, sur ses prochaines secondes.
        var envA = tEnvA.Result.Env;
        double actA = tEnvA.Result.Start;
        double phA = actA + DjAnalysis.BeatPhaseSeconds(envA, bpmA, out double peakA);
        if (peakA < DjAnalysis.MinPhasePeak) { LogService.Write("DJ", $"Grille de A peu nette ({peakA:0.0}) : fondu simple"); return null; }
        double periodA = 60.0 / bpmA;                 // en temps SOURCE de A

        double nowA = a.PositionSec + 0.40;           // marge pour installer le plan
        double ta = phA + Math.Ceiling((nowA - phA) / periodA) * periodA;

        // Duree du mix : un nombre entier de temps, borne par ce qui reste de la piste sortante.
        double pOut = periodA / rA;
        double remainOut = (totA - ta) / rA - 0.3;
        int beats = 0;
        foreach (int k in cut ? new[] { 2 } : new[] { 4, 2 })      // 4 temps (~2 s a 120 BPM) : aigus qui montent, swap des basses au 3e temps, nerveux ; 2 temps pour le mix a la coupe
            if (k * pOut <= Math.Min(8.0, remainOut)) { beats = k; break; }
        if (beats == 0) return null;

        // Grille de la piste entrante (debut du morceau), calee sur les basses.
        var envB = tEnvB.Result.Env;
        double actB = tEnvB.Result.Start;
        if (envB.Length < 600) return null;
        double periodB = 60.0 / bpmB;
        double phB = actB + DjAnalysis.BeatPhaseSeconds(envB, bpmB, out double peakB);
        if (peakB < DjAnalysis.MinPhasePeak) { LogService.Write("DJ", $"Grille de B peu nette ({peakB:0.0}) : fondu simple"); return null; }
        float max = 0;
        foreach (float v in envB) if (v > max) max = v;
        int first = 0;
        while (first < envB.Length && envB[first] < 0.15f * max) first++;
        double tSound = Math.Max(0, first * DjAnalysis.HopSec + actB - 0.03);
        // Premier temps de la grille de B qui porte un VRAI coup (grosse caisse, attaque) : demarrer sur un temps muet (intro en nappes, silence
        // entre deux coups) donnait l'impression d'un simple fondu, la piste entrante ne "tapait" pas sur le temps.
        double firstK = tSound <= phB ? 0 : Math.Ceiling((tSound - phB) / periodB);
        double tb = -1;
        for (int kk = 0; kk < 24; kk++)
        {
            double cand = phB + (firstK + kk) * periodB;
            if (cand > 12) break;                     // intro trop longue/incertaine : fondu simple
            int ci = (int)Math.Round((cand - actB) / DjAnalysis.HopSec);
            float hit = 0;
            for (int j = Math.Max(0, ci - 8); j <= Math.Min(envB.Length - 1, ci + 8); j++)   // +-43 ms autour du temps
                if (envB[j] > hit) hit = envB[j];
            if (hit >= 0.25f * max) { tb = cand; break; }
        }
        if (tb < 0) return null;

        return new Plan
        {
            StartB = tb,
            TriggerA = ta,
            MixFrames = (int)(beats * pOut * Rate),
            RateB = rb,
            Stretch = stretch,
            Beats = beats,
            BeatFrames = pOut * Rate,
            PeriodA = periodA,
        };
    }

    // ───────── Mixeur (thread audio) ─────────

    private sealed class Source : ISampleProvider
    {
        private readonly DjMixerEngine _e;
        public Source(DjMixerEngine e) { _e = e; }
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);
        public int Read(float[] buffer, int offset, int count)
        {
            try { return _e.MixRead(buffer, offset, count); }
            catch (Exception ex)
            {
                LogService.Write("DJ", ex, "Mixeur");
                Array.Clear(buffer, offset, count);
                return count;
            }
        }
    }

    private static float Smooth(double x)
    {
        x = Math.Clamp(x, 0.0, 1.0);
        return (float)(x * x * (3 - 2 * x));
    }

    /// <summary>Nombre de marches (une par temps entier e dans [from, to]) deja franchies a la position u (en temps). Chaque marche monte en 0,05 temps (~25 ms) pour finir PILE sur le temps, sans clic.</summary>
    private static double Stair(double u, int from, int to)
    {
        const double W = 0.05;
        int last = (int)Math.Floor(u);
        double n = Math.Clamp(last, from - 1, to) - (from - 1);
        int nx = last + 1;
        if (nx >= from && nx <= to && nx - u < W) n += Smooth((u - (nx - W)) / W);
        return n;
    }

    /// <summary>
    /// Gains par bande du mix cale (u = position en temps, K = nombre de temps, K/2 = temps du "drop") :
    /// - 1re moitie : les AIGUS de la piste entrante montent par marches, chacune tombant sur un temps (charleys, caisse claire...), sans basses ; la sortante garde tout (aigus un peu baisses pour eviter l'empilement) ;
    /// - au temps K/2 : echange des basses en ~15 ms (la grosse caisse de la sortante coupe net, celle de l'entrante arrive pleine) ;
    /// - 2e moitie : les aigus de la sortante redescendent par marches sur les temps jusqu'a zero.
    /// </summary>
    private static void BeatGains(double u, int K, out float aLow, out float aHigh, out float bLow, out float bHigh)
    {
        int h = K / 2;
        const double WS = 0.03;
        double bH = Stair(u, 0, h) / (h + 1);
        double bL = Smooth((u - (h - WS)) / WS);
        double down = Stair(u, h, K - 1) / (h + 1);
        double fin = Smooth((u - (K - 0.5)) / 0.5) / (h + 1);
        double aH = (1 - 0.3 * bH) * (1 - down - fin);
        aLow = (float)(1 - bL);
        aHigh = (float)Math.Max(0, aH);
        bLow = (float)bL;
        bHigh = (float)bH;
    }

    private float[] _tmpA = new float[8192];
    private float[] _tmpB = new float[8192];

    private static int ReadDeck(DjDeck d, float[] arr, int frames)
    {
        int need = frames * 2, got = 0;
        while (got < need)
        {
            int n = d.Read(arr, got, need - got);
            if (n <= 0) break;
            got += n;
        }
        if (got < need) Array.Clear(arr, got, need - got);
        return got / 2;
    }

    private int MixRead(float[] buf, int off, int count)
    {
        bool raiseEnd = false;
        lock (_gate)
        {
            int frames = count / 2;
            if (_a == null || (_paused && _pauseGain <= 0.0001f))
            {
                Array.Clear(buf, off, count);
                return count;
            }

            int done = 0;
            while (done < frames)
            {
                var a = _a!;
                int chunk = Math.Min(frames - done, 4096);

                if (_b != null && !_mixActive)
                {
                    double toTrig = _trigger - a.PositionSec;
                    if (a.Ended || toTrig <= 0) { _mixActive = true; _mixPos = 0; }
                    else
                    {
                        int fr = (int)(toTrig * Rate / Math.Max(0.5, a.Stretch?.Rate ?? 1.0));
                        chunk = Math.Min(chunk, Math.Max(64, fr));
                    }
                }
                if (_mixActive) chunk = Math.Min(chunk, Math.Max(1, _mixFrames - _mixPos));

                int gotA = a.Ended ? 0 : ReadDeck(a, _tmpA, chunk);
                bool mixing = _mixActive && _b != null;
                if (mixing) ReadDeck(_b!, _tmpB, chunk);
                else if (a.Ended && gotA == 0 && _b == null)
                {
                    // Fin ANORMALE (loin de la fin reelle du son) : un decodeur qui lache n'est pas une fin de morceau. On rouvre la platine
                    // a la position courante (2 essais) plutot que de sauter au son suivant.
                    double tot = a.Total.TotalSeconds;
                    if (tot > 5.0 && a.PositionSec < tot - 3.0 && _recoverTries < 2)
                    {
                        _recoverTries++;
                        LogService.Write("DJ", $"Fin anormale a {a.PositionSec:0.00}s / {tot:0.00}s : reouverture de la platine (essai {_recoverTries}) - {a.Track.Title}");
                        try { a.Rebuild(a.PositionSec, a.Stretch != null, a.Stretch?.Rate ?? 1.0); }
                        catch (Exception ex) { LogService.Write("DJ", ex, "Reouverture de la platine"); }
                        Array.Clear(buf, off + done * 2, chunk * 2);
                        done += chunk;
                        continue;
                    }

                    // Fin naturelle de la derniere piste.
                    LogService.Write("DJ", $"Fin naturelle : {a.PositionSec:0.00}s / {tot:0.00}s - {a.Track.Title}");
                    Array.Clear(buf, off + done * 2, (frames - done) * 2);
                    if (!_endRaised) { _endRaised = true; _state = PlaybackState.Stopped; raiseEnd = true; }
                    done = frames;
                    break;
                }

                double ga = a.Gain, ta = a.TargetGain;
                double gb = mixing ? _b!.Gain : 0, tb = mixing ? _b!.TargetGain : 0;
                const double k = 0.00004;
                int o = off + done * 2;
                for (int i = 0; i < chunk; i++)
                {
                    ga += (ta - ga) * k;
                    float l = _tmpA[i * 2], r = _tmpA[i * 2 + 1];
                    float outL, outR;
                    if (mixing)
                    {
                        gb += (tb - gb) * k;
                        float bl = _tmpB[i * 2], br = _tmpB[i * 2 + 1];
                        float aOutL, aOutR, bOutL, bOutR;
                        if (_beatMode)
                        {
                            // Mix "DJ" : les gains de chaque bande suivent la grille des temps (voir BeatGains).
                            BeatGains((_mixPos + i) / _beatFrames, _kBeats, out float gaLow, out float gaHigh, out float gbLow, out float gbHigh);
                            a.Xo.Process(l, r, out float alL, out float alR, out float ahL, out float ahR);
                            _b!.Xo.Process(bl, br, out float blL, out float blR, out float bhL, out float bhR);
                            aOutL = alL * gaLow + ahL * gaHigh; aOutR = alR * gaLow + ahR * gaHigh;
                            bOutL = blL * gbLow + bhL * gbHigh; bOutR = blR * gbLow + bhR * gbHigh;
                        }
                        else
                        {
                            // Repli (rythme peu net) : fondu enchaine equal-power classique.
                            double t = (_mixPos + i) / (double)_mixFrames;
                            float eb = (float)Math.Sin(t * Math.PI / 2), ea = (float)Math.Cos(t * Math.PI / 2);
                            aOutL = l * ea; aOutR = r * ea;
                            bOutL = bl * eb; bOutR = br * eb;
                        }
                        outL = (float)(aOutL * ga + bOutL * gb);
                        outR = (float)(aOutR * ga + bOutR * gb);
                    }
                    else
                    {
                        // Piste entrante en attente de son temps de depart : on chauffe le filtre de A pour que le mix demarre sans transitoire.
                        if (_b != null && _beatMode) a.Xo.Prime(l, r);
                        outL = (float)(l * ga); outR = (float)(r * ga);
                    }

                    if (_fadeIn > 0)
                    {
                        float f = 1f - _fadeIn / 480f; if (f < 0) f = 0;
                        outL *= f; outR *= f; _fadeIn--;
                    }
                    buf[o + i * 2] = outL; buf[o + i * 2 + 1] = outR;
                }
                a.Gain = ga;
                if (mixing) _b!.Gain = gb;

                if (mixing)
                {
                    _mixPos += chunk;
                    if (_mixPos >= _mixFrames)
                    {
                        // Mix termine : la piste entrante devient la piste courante.
                        a.Dispose();
                        _a = _b; _b = null; _mixActive = false;
                        _a!.Xo.Reset();
                        _endRaised = false;
                        _recoverTries = 0;
                    }
                }
                else if (_b == null && _a!.Stretch != null)
                {
                    // Apres un mix, la piste etiree revient doucement a sa vitesse normale (~12 s).
                    double r = _a.Stretch.Rate;
                    if (Math.Abs(r - 1.0) > 0.0004)
                    {
                        double step = Math.Abs(r - 1.0) > 0 ? 0.0065 * chunk / Rate : 0;
                        r = r > 1 ? Math.Max(1.0, r - step) : Math.Min(1.0, r + step);
                        _a.SetRate(r);
                    }
                    else _a.SetRate(1.0);
                }
                done += chunk;
            }

            PublishLocked();

            // Pause / reprise en douceur (rampe de 10 ms) : jamais de claquement.
            float target = _paused ? 0f : 1f;
            if (_pauseGain != target || _pauseGain < 1f)
            {
                for (int i = 0; i < frames; i++)
                {
                    if (_pauseGain < target) _pauseGain = Math.Min(target, _pauseGain + 1f / 480f);
                    else if (_pauseGain > target) _pauseGain = Math.Max(target, _pauseGain - 1f / 480f);
                    buf[off + i * 2] *= _pauseGain;
                    buf[off + i * 2 + 1] *= _pauseGain;
                }
            }
        }
        if (raiseEnd)
        {
            var h = PlaybackEnded;
            Task.Run(() => h?.Invoke(this, EventArgs.Empty));
        }
        return count;
    }
}
