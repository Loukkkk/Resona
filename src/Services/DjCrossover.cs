using System;

namespace Resona.Services;

/// <summary>
/// Filtre separateur graves / aigus de type Linkwitz-Riley d'ordre 4 (deux Butterworth d'ordre 2 en cascade), stereo.
/// LP + HP redonne le signal d'origine (a la phase pres, module plat) : aucune coloration quand les deux bandes sont a 1.
/// Pente de 24 dB/octave : quand le mixeur coupe les basses de la piste sortante, la grosse caisse disparait vraiment
/// (l'ancien filtre du 1er ordre en laissait passer une grande partie, d'ou l'effet "simple fondu").
/// </summary>
internal sealed class DjCrossover
{
    private struct Bq { public double Z1, Z2; }

    private readonly double _lb0, _lb1, _lb2, _hb0, _hb1, _hb2, _a1, _a2;
    private Bq _lp1L, _lp2L, _hp1L, _hp2L, _lp1R, _lp2R, _hp1R, _hp2R;

    public DjCrossover(double cutoffHz, int sampleRate)
    {
        double w0 = 2 * Math.PI * cutoffHz / sampleRate;
        double c = Math.Cos(w0), s = Math.Sin(w0);
        double alpha = s / (2 * (1 / Math.Sqrt(2)));   // Q = 1/sqrt(2) (Butterworth)
        double a0 = 1 + alpha;
        _a1 = -2 * c / a0;
        _a2 = (1 - alpha) / a0;
        _lb0 = (1 - c) / 2 / a0; _lb1 = (1 - c) / a0; _lb2 = _lb0;
        _hb0 = (1 + c) / 2 / a0; _hb1 = -(1 + c) / a0; _hb2 = _hb0;
    }

    private static double Step(ref Bq s, double x, double b0, double b1, double b2, double a1, double a2)
    {
        double y = b0 * x + s.Z1;
        s.Z1 = b1 * x - a1 * y + s.Z2;
        s.Z2 = b2 * x - a2 * y;
        return y;
    }

    /// <summary>Separe un echantillon stereo en graves et aigus.</summary>
    public void Process(float l, float r, out float lowL, out float lowR, out float highL, out float highR)
    {
        lowL = (float)Step(ref _lp2L, Step(ref _lp1L, l, _lb0, _lb1, _lb2, _a1, _a2), _lb0, _lb1, _lb2, _a1, _a2);
        highL = (float)Step(ref _hp2L, Step(ref _hp1L, l, _hb0, _hb1, _hb2, _a1, _a2), _hb0, _hb1, _hb2, _a1, _a2);
        lowR = (float)Step(ref _lp2R, Step(ref _lp1R, r, _lb0, _lb1, _lb2, _a1, _a2), _lb0, _lb1, _lb2, _a1, _a2);
        highR = (float)Step(ref _hp2R, Step(ref _hp1R, r, _hb0, _hb1, _hb2, _a1, _a2), _hb0, _hb1, _hb2, _a1, _a2);
    }

    /// <summary>Fait tourner le filtre sans utiliser le resultat : l'etat est deja "chaud" quand le mix demarre (pas de transitoire).</summary>
    public void Prime(float l, float r) => Process(l, r, out _, out _, out _, out _);

    public void Reset()
    {
        _lp1L = _lp2L = _hp1L = _hp2L = _lp1R = _lp2R = _hp1R = _hp2R = default;
    }
}
