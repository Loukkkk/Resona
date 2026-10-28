using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Resona.Models;

namespace Resona.Services;

/// <summary>Source audio du moteur DJ : lit n'importe quel fichier et le fournit en float stereo 48 kHz.</summary>
internal sealed class DjSource : IDisposable
{
    public const int Rate = 48000;

    private ISampleProvider _raw = null!;
    private Func<TimeSpan> _getTime = () => TimeSpan.Zero;
    private Action<TimeSpan> _setTime = _ => { };
    private IDisposable? _disposable;

    public ISampleProvider Provider { get; private set; } = null!;
    public TimeSpan Total { get; private set; }

    public static DjSource Open(string path, TimeSpan knownDuration)
    {
        var s = new DjSource();
        string ext = Path.GetExtension(LibraryScannerService.ResolveRealExtension(path)).ToLowerInvariant();

        if (ext is ".opus" or ".ogg")
        {
            try
            {
                var op = new OpusSampleProvider(path, knownDuration);
                s._raw = op; s._disposable = op; s.Total = op.TotalTime;
                s._getTime = () => op.CurrentTime; s._setTime = t => op.CurrentTime = t;
            }
            catch { }
        }

        if (s._raw == null)
        {
            try
            {
                var afr = new AudioFileReader(path);
                s._raw = afr; s._disposable = afr; s.Total = afr.TotalTime;
                s._getTime = () => afr.CurrentTime; s._setTime = t => afr.CurrentTime = t;
            }
            catch { }
        }

        if (s._raw == null && ext == ".mp3")
        {
            // Meme repli que le moteur normal : certains MP3 que AudioFileReader refuse passent par Mp3FileReader.
            try
            {
                var mp3 = new Mp3FileReader(path);
                s._raw = mp3.ToSampleProvider(); s._disposable = mp3; s.Total = mp3.TotalTime;
                s._getTime = () => mp3.CurrentTime; s._setTime = t => mp3.CurrentTime = t;
            }
            catch { }
        }

        if (s._raw == null)
        {
            var ws = SafeAudioReader.Create(path);
            s._raw = ws.ToSampleProvider(); s._disposable = ws; s.Total = ws.TotalTime;
            s._getTime = () => ws.CurrentTime; s._setTime = t => ws.CurrentTime = t;
        }

        if (knownDuration > TimeSpan.Zero && (s.Total <= TimeSpan.Zero || Math.Abs((s.Total - knownDuration).TotalSeconds) > 30))
            s.Total = knownDuration;

        s.BuildChain();
        return s;
    }

    private void BuildChain()
    {
        ISampleProvider p = _raw;
        if (p.WaveFormat.Channels == 1) p = new MonoToStereoSampleProvider(p);
        else if (p.WaveFormat.Channels > 2)
        {
            var mux = new MultiplexingSampleProvider(new[] { p }, 2);
            mux.ConnectInputToOutput(0, 0);
            mux.ConnectInputToOutput(1, 1);
            p = mux;
        }
        if (p.WaveFormat.SampleRate != Rate) p = new WdlResamplingSampleProvider(p, Rate);
        Provider = p;
    }

    /// <summary>
    /// Se replace a t (reconstruit la chaine pour vider les tampons du resampler).
    /// Renvoie la position REELLEMENT atteinte (les decodeurs MP3/Opus tombent sur une frame/page voisine).
    /// </summary>
    public double Seek(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        double actual = t.TotalSeconds;
        try { _setTime(t); actual = _getTime().TotalSeconds; } catch { }
        BuildChain();
        return actual;
    }

    public void Dispose()
    {
        try { _disposable?.Dispose(); } catch { }
    }
}

/// <summary>Un "platine" du moteur DJ : une piste, sa position, son gain et (optionnellement) son etirement temporel.</summary>
internal sealed class DjDeck : IDisposable
{
    public Track Track { get; }
    public DjSource Source { get; }
    public DjTimeStretcher? Stretch { get; private set; }
    public double StartSec { get; private set; }
    public bool Ended { get; private set; }
    public readonly DjCrossover Xo = new(200.0, DjSource.Rate);   // separation graves / aigus (mix cale sur les temps)
    public double Gain = 1.0;          // gain courant (lineaire)
    public double TargetGain = 1.0;    // gain cible (normalisation), rejoint en douceur

    private ISampleProvider _out;
    private long _frames;
    private int _zeroReads;

    public TimeSpan Total => Source.Total;

    public double PositionSec
    {
        get
        {
            double p = Stretch != null ? StartSec + Stretch.SourceSeconds : StartSec + _frames / (double)DjSource.Rate;
            double tot = Total.TotalSeconds;
            return tot > 0 ? Math.Min(p, tot) : p;
        }
    }

    public DjDeck(Track track, DjSource src, double startSec, bool stretch, double rate, bool exact = false)
    {
        Track = track;
        Source = src;
        _out = src.Provider;
        Rebuild(startSec, stretch, rate, exact);
    }

    /// <summary>
    /// Positionne la platine. exact = true : on vise l'instant a l'echantillon pres (seek un peu avant, puis on jette
    /// les echantillons en trop) - indispensable pour caler un temps sur la grille.
    /// </summary>
    public void Rebuild(double startSec, bool stretch, double rate, bool exact = false)
    {
        double target = Math.Max(0, startSec);
        double seekTo = exact ? Math.Max(0, target - 0.15) : target;
        StartSec = Source.Seek(TimeSpan.FromSeconds(seekTo));
        if (exact && StartSec < target)
        {
            long toSkip = (long)Math.Round((target - StartSec) * DjSource.Rate) * 2;
            var tmp = new float[4096];
            while (toSkip > 0)
            {
                int n = Source.Provider.Read(tmp, 0, (int)Math.Min(tmp.Length, toSkip));
                if (n <= 0) break;
                toSkip -= n;
            }
            StartSec = target;
        }
        _frames = 0;
        _zeroReads = 0;
        Ended = false;
        if (stretch)
        {
            Stretch = new DjTimeStretcher(Source.Provider) { Rate = rate };
            _out = Stretch;
        }
        else
        {
            Stretch = null;
            _out = Source.Provider;
        }
    }

    public void SetRate(double rate)
    {
        if (Stretch != null) Stretch.Rate = rate;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (Ended) return 0;
        int n;
        try { n = _out.Read(buffer, offset, count); }
        catch (Exception ex) { LogService.Write("DJ", ex, "Lecture de la platine " + Track.Title); n = 0; }
        if (n <= 0)
        {
            // Une lecture a 0 loin de la fin du son n'est pas une vraie fin (decodeur qui n'a pas encore rendu de donnees, fichier
            // fraichement ouvert...) : on re-essaie quelques fois avant de conclure. Sinon la piste etait consideree finie et l'app sautait au son suivant.
            double tot = Total.TotalSeconds;
            if (tot > 5.0 && PositionSec < tot - 3.0 && ++_zeroReads <= 8) return 0;
            Ended = true;
            LogService.Write("DJ", $"Fin de lecture de la platine : {PositionSec:0.00}s / {tot:0.00}s ({Track.Title})");
            return 0;
        }
        _zeroReads = 0;
        _frames += n / 2;
        return n;
    }

    /// <summary>Ignore le silence de debut (max maxSec) : evite de "perdre" des secondes muettes avant le morceau.</summary>
    public void SkipLeadingSilence(double maxSec, float threshold = 0.004f)
    {
        var tmp = new float[960 * 2];
        long limit = (long)(maxSec * DjSource.Rate);
        long skipped = 0;
        while (skipped < limit)
        {
            int n = Read(tmp, 0, tmp.Length);
            if (n <= 0) return;
            float peak = 0;
            for (int i = 0; i < n; i++) { float a = Math.Abs(tmp[i]); if (a > peak) peak = a; }
            if (peak >= threshold)
            {
                // On est au debut du son : on revient d'un bloc (20 ms) pour ne pas manger l'attaque.
                double back = Math.Max(0, StartSec + (_frames - n / 2) / (double)DjSource.Rate - 0.02);
                Rebuild(back, Stretch != null, Stretch?.Rate ?? 1.0);
                return;
            }
            skipped += n / 2;
        }
    }

    public void Dispose() => Source.Dispose();
}
