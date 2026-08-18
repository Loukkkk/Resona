using NAudio.Dsp;
using NAudio.Wave;

namespace Resona.Services;

public class EqualizerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _sampleRate;
    private readonly BiQuadFilter[] _filters;
    private bool _enabled;

    private static readonly double[] FreqBands =
        { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    public EqualizerSampleProvider(ISampleProvider source)
    {
        _source = source;
        _sampleRate = source.WaveFormat.SampleRate;
        _filters = new BiQuadFilter[FreqBands.Length];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samples = _source.Read(buffer, offset, count);
        if (!_enabled) return samples;

        for (int i = 0; i < samples; i++)
        {
            float sample = buffer[offset + i];
            foreach (var filter in _filters)
            {
                if (filter != null)
                    sample = filter.Transform(sample);
            }
            buffer[offset + i] = sample;
        }
        return samples;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled) return;
        for (int i = 0; i < _filters.Length; i++)
        {
            if (_filters[i] == null)
                _filters[i] = CreateFilter(i, 0);
        }
    }

    public void SetBand(int index, float gainDb)
    {
        if (index < 0 || index >= _filters.Length) return;
        _filters[index] = CreateFilter(index, gainDb);
    }

    private BiQuadFilter CreateFilter(int index, float gainDb)
    {
        double freq = FreqBands[index];
        double q = freq <= 250 ? 1.0 :
                   freq <= 2000 ? 1.41 :
                   freq <= 8000 ? 1.41 : 1.0;
        return BiQuadFilter.PeakingEQ(_sampleRate, (float)freq, (float)q, gainDb);
    }
}
