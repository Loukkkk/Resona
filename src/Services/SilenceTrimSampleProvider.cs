// ============================================================
//  SilenceTrimSampleProvider.cs  —  Resona
//  Option "Ignorer les silences au debut et a la fin des sons" (Parametres > Traitement audio).
//
//  - Debut : les premiers echantillons sont lus puis jetes tant qu'ils restent sous le seuil de silence
//            (uniquement au lancement du son, jamais apres un deplacement dans la barre de lecture).
//  - Fin   : au lancement, AudioEngineService mesure l'instant du dernier echantillon audible dans les derniers
//            secondes du fichier (FindAudibleEnd). Une fois cet instant depasse, le flux est considere comme
//            termine : le reste du fichier (silence) est lu et jete d'un coup, puis la lecture s'arrete comme
//            a la fin naturelle d'un fichier (meme evenement PlaybackStopped, position = fin du fichier).
//  Les silences au milieu d'un son ne sont jamais touches. Rien n'est modifie dans les fichiers.
// ============================================================

using NAudio.Wave;

namespace Resona.Services;

public sealed class SilenceTrimSampleProvider : ISampleProvider
{
    // Seuil de silence : environ -50 dBFS (0,003 en amplitude lineaire) : les fichiers compresses ont rarement un silence parfait.
    private const float SilenceThreshold = 0.003f;
    // Duree maximale ignoree au debut (securite : un fichier qui commence par plus d'une minute de silence est joue tel quel).
    private const int MaxLeadingSkipSeconds = 60;
    // Marge conservee avant le premier echantillon audible (evite de couper l'attaque d'un son).
    private const double LeadingPadSeconds = 0.005;
    // Marge conservee apres le dernier echantillon audible (queues de reverb tres faibles, imprecision de position des decodeurs).
    private const double TrailingPadSeconds = 0.4;
    // Taille de la zone analysee a la fin du fichier.
    public static readonly TimeSpan TrailingScanWindow = TimeSpan.FromSeconds(25);

    private readonly ISampleProvider _source;
    private readonly Func<TimeSpan> _getPosition;
    private readonly int _channels;
    private readonly int _sampleRate;

    private bool _skipLeading;
    private bool _draining;
    private bool _finished;

    /// <summary>Instant (dans le fichier) au-dela duquel plus rien n'est audible ; null = pas de coupe en fin de son.</summary>
    public TimeSpan? EndLimit { get; }

    public SilenceTrimSampleProvider(ISampleProvider source, Func<TimeSpan> getPosition, bool skipLeading, TimeSpan? endLimit)
    {
        _source = source;
        _getPosition = getPosition;
        _skipLeading = skipLeading;
        EndLimit = endLimit;
        _channels = Math.Max(1, source.WaveFormat.Channels);
        _sampleRate = Math.Max(1, source.WaveFormat.SampleRate);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>A appeler quand l'utilisateur deplace la lecture : on ne saute plus de silence de debut.</summary>
    public void NotifySeek()
    {
        _skipLeading = false;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_finished) return 0;
        if (_draining)
        {
            DrainSource();
            _finished = true;
            return 0;
        }

        int read = _skipLeading ? ReadAfterLeadingSilence(buffer, offset, count) : SafeRead(buffer, offset, count);
        if (read <= 0)
        {
            _finished = true;
            return 0;
        }

        if (EndLimit.HasValue)
        {
            TimeSpan position = SafePosition();
            if (position > EndLimit.Value)
            {
                // Une partie de ce bloc depasse la derniere note audible : on garde seulement ce qui precede.
                int overSamples = (int)((position - EndLimit.Value).TotalSeconds * _sampleRate) * _channels;
                int keep = Math.Max(0, read - overSamples);
                keep -= keep % _channels;
                if (keep <= 0)
                {
                    DrainSource();
                    _finished = true;
                    return 0;
                }
                // Les derniers echantillons audibles sont rendus tout de suite ; le reste du fichier est lu et jete au prochain appel.
                _draining = true;
                return keep;
            }
        }
        return read;
    }

    private int SafeRead(float[] buffer, int offset, int count)
    {
        try { return _source.Read(buffer, offset, count); }
        catch (System.Runtime.InteropServices.COMException) { return 0; }
    }

    private TimeSpan SafePosition()
    {
        try { return _getPosition(); }
        catch { return TimeSpan.Zero; }
    }

    private int ReadAfterLeadingSilence(float[] buffer, int offset, int count)
    {
        int maxDiscard = _sampleRate * _channels * MaxLeadingSkipSeconds;
        int discarded = 0;
        while (true)
        {
            int read = SafeRead(buffer, offset, count);
            if (read <= 0)
            {
                _skipLeading = false;
                return 0;   // fichier entierement silencieux
            }

            int first = FirstAudibleIndex(buffer, offset, read);
            if (first >= 0)
            {
                _skipLeading = false;
                int start = Math.Max(0, first - (int)(LeadingPadSeconds * _sampleRate) * _channels);
                start -= start % _channels;
                LogService.Write("SilenceTrim", $"Debut : {(discarded + start) / (double)(_sampleRate * _channels):0.00} s ignores");
                if (start == 0) return read;

                int kept = read - start;
                Array.Copy(buffer, offset + start, buffer, offset, kept);
                // On complete le buffer pour ne pas renvoyer un bloc plus court que demande.
                while (kept < count)
                {
                    int more = SafeRead(buffer, offset + kept, count - kept);
                    if (more <= 0) break;
                    kept += more;
                }
                return kept;
            }

            discarded += read;
            if (discarded >= maxDiscard)
            {
                _skipLeading = false;   // limite atteinte : on joue le fichier tel quel
                return read;
            }
        }
    }

    private void DrainSource()
    {
        var scratch = new float[16384];
        try
        {
            int guard = 0;
            while (guard++ < 20000 && _source.Read(scratch, 0, scratch.Length) > 0) { }
        }
        catch { }
    }

    private int FirstAudibleIndex(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float s = buffer[offset + i];
            if (s > SilenceThreshold || s < -SilenceThreshold) return i;
        }
        return -1;
    }

    private static int LastAudibleIndex(float[] buffer, int count)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            float s = buffer[i];
            if (s > SilenceThreshold || s < -SilenceThreshold) return i;
        }
        return -1;
    }

    /// <summary>
    /// Mesure l'instant du dernier echantillon audible dans les derniers secondes du fichier (+ une petite marge).
    /// Deplace la lecture du decodeur : celle-ci est remise a zero a la fin (l'appelant se replace ensuite si besoin).
    /// Renvoie null si le fichier est trop court, si la mesure echoue, ou si tout le fichier est silencieux.
    /// </summary>
    public static TimeSpan? FindAudibleEnd(ISampleProvider provider, Func<TimeSpan> getPosition, Action<TimeSpan> setPosition, TimeSpan total)
    {
        if (total < TimeSpan.FromSeconds(8)) return null;
        int channels = Math.Max(1, provider.WaveFormat.Channels);
        int sampleRate = Math.Max(1, provider.WaveFormat.SampleRate);
        TimeSpan windowStart = total > TrailingScanWindow ? total - TrailingScanWindow : TimeSpan.Zero;

        try
        {
            setPosition(windowStart);
            var buffer = new float[4096 * channels];
            TimeSpan? lastAudible = null;
            int read;
            int guard = 0;
            while (guard++ < 100000 && (read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                int idx = LastAudibleIndex(buffer, read);
                if (idx < 0) continue;
                TimeSpan blockEnd = getPosition();
                lastAudible = blockEnd - TimeSpan.FromSeconds((double)(read - idx) / (sampleRate * channels));
            }

            if (lastAudible.HasValue)
            {
                TimeSpan limit = lastAudible.Value + TimeSpan.FromSeconds(TrailingPadSeconds);
                return limit < total ? limit : null;   // rien a couper si l'audio va jusqu'a la fin
            }

            // Toute la zone analysee est silencieuse : si elle ne couvre pas tout le fichier, on coupe a son debut.
            return windowStart > TimeSpan.Zero ? windowStart + TimeSpan.FromSeconds(TrailingPadSeconds) : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            try { setPosition(TimeSpan.Zero); } catch { }
        }
    }
}
