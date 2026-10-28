using System;
using NAudio.Wave;

namespace Resona.Services;

/// <summary>
/// Etirement temporel (WSOLA) pour le moteur DJ : change la VITESSE de lecture sans changer la hauteur.
/// Implementation autonome (aucune dependance NuGet) : fenetre de Hann 2048 frames, recouvrement 50 %,
/// recherche de la meilleure position par correlation (+-10 ms) pour rester en phase avec le signal precedent.
/// Rate &gt; 1 = plus rapide. Le flux source doit etre en float stereo.
/// </summary>
internal sealed class DjTimeStretcher : ISampleProvider
{
    private const int Ch = 2;
    private const int N = 2048;
    private const int Hs = N / 2;
    private const int Tol = 480;

    private readonly ISampleProvider _src;
    private readonly float[] _win = new float[N];
    private float[] _buf = new float[(N * 4) * Ch];
    private long _bufStart;
    private int _bufFrames;
    private bool _srcEnd;

    private double _nom;
    private long _prev = -1;
    private readonly float[] _tail = new float[Hs * Ch];
    private readonly float[] _out = new float[Hs * Ch];
    private int _outPos, _outFrames;
    private bool _finished;
    private long _curC;                 // debut (frames, domaine du flux) du segment dont on rend actuellement la premiere moitie
    private readonly float[] _ref = new float[Hs / 2];

    public WaveFormat WaveFormat => _src.WaveFormat;

    /// <summary>Vitesse de lecture (1.0 = normale). Modifiable a chaud.</summary>
    public double Rate { get; set; } = 1.0;

    /// <summary>Position dans le flux SOURCE (en secondes) correspondant a ce qui vient d'etre rendu (position reellement choisie, pas la nominale).</summary>
    public double SourceSeconds => Math.Max(0, _curC + _outPos) / (double)WaveFormat.SampleRate;

    public DjTimeStretcher(ISampleProvider src)
    {
        if (src.WaveFormat.Channels != Ch) throw new ArgumentException("Stereo requis");
        _src = src;
        for (int i = 0; i < N; i++) _win[i] = 0.5f - 0.5f * (float)Math.Cos(2 * Math.PI * i / N);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int written = 0;
        while (written < count)
        {
            if (_outPos >= _outFrames)
            {
                _outPos = _outFrames = 0;
                if (!Step()) break;
            }
            int n = Math.Min(count - written, (_outFrames - _outPos) * Ch);
            Array.Copy(_out, _outPos * Ch, buffer, offset + written, n);
            written += n;
            _outPos += n / Ch;
        }
        return written;
    }

    private bool EnsureFrames(long absEnd)
    {
        while (_bufStart + _bufFrames < absEnd && !_srcEnd)
        {
            int need = (int)Math.Min(absEnd - (_bufStart + _bufFrames), 16384);
            int required = (_bufFrames + need) * Ch;
            if (required > _buf.Length) Array.Resize(ref _buf, Math.Max(required, _buf.Length * 2));
            int got = _src.Read(_buf, _bufFrames * Ch, need * Ch);
            if (got <= 0) { _srcEnd = true; break; }
            _bufFrames += got / Ch;
        }
        return _bufStart + _bufFrames >= absEnd;
    }

    private void Drop(long keepFrom)
    {
        long d = keepFrom - _bufStart;
        if (d <= 0) return;
        if (d > _bufFrames) d = _bufFrames;
        Buffer.BlockCopy(_buf, (int)d * Ch * 4, _buf, 0, (_bufFrames - (int)d) * Ch * 4);
        _bufStart += d;
        _bufFrames -= (int)d;
    }

    private float Get(long abs, int ch)
    {
        long i = abs - _bufStart;
        if (i < 0 || i >= _bufFrames) return 0f;
        return _buf[i * Ch + ch];
    }

    private float Mono(long abs) => Get(abs, 0) + Get(abs, 1);

    /// <summary>
    /// Cherche, autour de la position nominale, le debut de segment dont le son ressemble le plus a la suite naturelle du segment precedent
    /// (somme des ecarts absolus, 1 frame sur 2 des Hs premieres). Leger avantage a la position nominale : a vitesse 1.0 la sortie est identique a l'entree.
    /// </summary>
    private long Best(long center, long refStart)
    {
        const int M = Hs / 2;
        long end = _bufStart + _bufFrames;
        long lo = Math.Max(center - Tol, _bufStart);
        long hi = Math.Min(center + Tol, end - Hs);
        if (hi < lo) return Math.Clamp(center, _bufStart, Math.Max(_bufStart, end - 1));

        for (int k = 0; k < M; k++) _ref[k] = Mono(refStart + k * 2);

        long best = Math.Clamp(center, lo, hi);
        float bestCost = float.MaxValue;
        for (long c = lo; c <= hi; c++)
        {
            float cost = Math.Abs(c - center) * 0.02f;
            long baseIdx = (c - _bufStart) * Ch;
            for (int k = 0; k < M; k++)
            {
                long idx = baseIdx + (long)k * 2 * Ch;
                cost += Math.Abs(_buf[idx] + _buf[idx + 1] - _ref[k]);
                if (cost >= bestCost) break;
            }
            if (cost < bestCost) { bestCost = cost; best = c; }
        }
        return best;
    }

    private bool Step()
    {
        if (_finished) return false;
        double rate = Math.Clamp(Rate, 0.5, 2.0);
        long c;
        if (_prev < 0)
        {
            c = 0;
            _nom = 0;
            EnsureFrames(N);
            if (_bufFrames == 0) { _finished = true; return false; }
        }
        else
        {
            double nextNom = _nom + Hs * rate;
            long center = (long)Math.Round(nextNom);
            Drop(Math.Min(_prev, center - Tol));
            EnsureFrames(center + Tol + N);
            long total = _bufStart + _bufFrames;
            if (_srcEnd && center >= total)
            {
                // Fin du flux : on rend le dernier recouvrement restant, puis on s'arrete.
                Array.Copy(_tail, _out, _tail.Length);
                _outFrames = Hs; _outPos = 0;
                _finished = true;
                _nom = nextNom;
                return true;
            }
            c = Best(center, _prev + Hs);
            _nom = nextNom;
            EnsureFrames(c + N);
        }

        bool first = _prev < 0;
        for (int i = 0; i < Hs; i++)
        {
            for (int ch = 0; ch < Ch; ch++)
            {
                float seg = Get(c + i, ch);
                _out[i * Ch + ch] = first ? seg : _tail[i * Ch + ch] + seg * _win[i];
                _tail[i * Ch + ch] = Get(c + Hs + i, ch) * _win[Hs + i];
            }
        }
        _prev = c;
        _curC = c;
        _outFrames = Hs;
        _outPos = 0;
        return true;
    }
}
