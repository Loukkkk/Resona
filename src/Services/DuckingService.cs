// ============================================================
//  DuckingService.cs  —  Resona
//  Audio ducking (Parametres > Traitement audio) : le volume de Resona baisse automatiquement tant qu'une application
//  choisie (Discord, jeu, visio...) emet du son, puis remonte quand elle se tait.
//
//  Detection : sessions audio Windows (WASAPI) du peripherique de sortie par defaut ; on lit le niveau crete de chaque
//  session appartenant au processus choisi. Un thread dedie (tous les appels COM y restent) calcule le gain :
//    - baisse en "Duree de la baisse" ms, maintien "Delai avant de remonter" ms apres le dernier son detecte,
//      puis remontee en "Duree de la remontee" ms ;
//    - la pente suit la courbe choisie (lineaire / progressive en S / rapide puis douce), interpolee en dB.
//  DuckingSampleProvider applique ce gain dans la chaine audio (avec une rampe a l'interieur de chaque bloc : pas de craquement).
// ============================================================

using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Resona.Services;

public static class DuckingService
{
    /// <summary>Reduction a partir de laquelle (bout du curseur) le ducking coupe totalement le son.</summary>
    public const double MuteThresholdDb = 40.0;

    private static volatile float _gain = 1f;
    private static Thread? _thread;
    private static readonly object StartLock = new();

    /// <summary>Gain lineaire courant du ducking (1 = aucune baisse).</summary>
    public static float CurrentGain => _gain;

    /// <summary>Demarre (une seule fois) le thread de surveillance. Sans effet tant que l'option est desactivee.</summary>
    public static void EnsureStarted()
    {
        lock (StartLock)
        {
            if (_thread != null) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "ResonaDucking" };
            _thread.Start();
        }
    }

    /// <summary>Noms des applications ayant une session audio sur le peripherique de sortie par defaut (hors Resona).</summary>
    public static List<string> GetAudioApps()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                string? name = ProcessNameOf(sessions[i]);
                if (name != null) names.Add(name);
            }
        }
        catch (Exception ex) { LogService.Write("Ducking", ex, "Liste des applications audio"); }
        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? ProcessNameOf(AudioSessionControl session)
    {
        try
        {
            if (session.IsSystemSoundsSession) return null;
            uint pid = session.GetProcessID;
            if (pid == 0 || pid == (uint)Environment.ProcessId) return null;
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch { return null; }
    }

    private static double Shape(double p, int curve) => curve switch
    {
        0 => p,                                   // lineaire
        2 => 1.0 - (1.0 - p) * (1.0 - p),         // rapide puis douce
        _ => p * p * (3.0 - 2.0 * p),             // progressive (en S)
    };

    private static void Loop()
    {
        MMDeviceEnumerator? enumerator = null;
        MMDevice? device = null;
        var meters = new List<AudioMeterInformation>();
        string trackedName = "";
        long lastRefresh = -1_000_000L;
        long lastActive = -1_000_000_000L;
        long last = Environment.TickCount64;
        double progress = 0.0;   // 0 = pas de baisse, 1 = baisse maximale

        while (true)
        {
            try
            {
                Thread.Sleep(25);
                long now = Environment.TickCount64;
                double dt = Math.Max(1, now - last);
                last = now;

                var cfg = App.Settings?.Current;
                if (cfg == null) continue;
                string target = cfg.DuckingProcessName ?? "";
                bool enabled = cfg.DuckingEnabled && !string.IsNullOrWhiteSpace(target);

                bool active = false;
                if (enabled)
                {
                    try
                    {
                        enumerator ??= new MMDeviceEnumerator();
                        // Liste des sessions rafraichie chaque seconde (nouvelle application, changement de peripherique par defaut...).
                        if (device == null || now - lastRefresh > 1000 || !string.Equals(trackedName, target, StringComparison.OrdinalIgnoreCase))
                        {
                            var newDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                            var newMeters = new List<AudioMeterInformation>();
                            var sessions = newDevice.AudioSessionManager.Sessions;
                            for (int i = 0; i < sessions.Count; i++)
                            {
                                var s = sessions[i];
                                string? name = ProcessNameOf(s);
                                if (name != null && string.Equals(name, target, StringComparison.OrdinalIgnoreCase))
                                    newMeters.Add(s.AudioMeterInformation);
                            }
                            var oldDevice = device;
                            device = newDevice;
                            meters = newMeters;
                            trackedName = target;
                            lastRefresh = now;
                            try { oldDevice?.Dispose(); } catch { }
                        }

                        double threshold = Math.Clamp(cfg.DuckingThresholdPercent, 0.1, 100.0) / 100.0;
                        foreach (var meter in meters)
                        {
                            if (meter.MasterPeakValue > threshold) { active = true; break; }
                        }
                    }
                    catch
                    {
                        // Peripherique disparu / session invalide : on repart de zero au prochain tour.
                        try { device?.Dispose(); } catch { }
                        device = null;
                        meters = new List<AudioMeterInformation>();
                        lastRefresh = -1_000_000L;
                    }
                }

                if (active) lastActive = now;
                bool duck = enabled && (active || now - lastActive < Math.Max(0, cfg.DuckingHoldMs));

                if (duck)
                {
                    progress = Math.Min(1.0, progress + dt / Math.Max(10, cfg.DuckingAttackMs));
                }
                else
                {
                    progress = Math.Max(0.0, progress - dt / Math.Max(10, cfg.DuckingReleaseMs));
                    if (!enabled && progress <= 0.0) Thread.Sleep(200);   // rien a surveiller : on economise le processeur
                }

                bool mute = cfg.DuckingAmountDb >= MuteThresholdDb;
                double shaped = Shape(progress, cfg.DuckingCurve);
                // Coupure totale : la courbe descend jusqu'a -80 dB (inaudible) puis le gain passe a 0.
                double db = -(mute ? 80.0 : Math.Clamp(cfg.DuckingAmountDb, 0.0, 60.0)) * shaped;
                _gain = (mute && shaped >= 1.0) ? 0f : (float)Math.Pow(10.0, db / 20.0);
            }
            catch
            {
                Thread.Sleep(250);
            }
        }
    }
}

/// <summary>Applique le gain de ducking (rampe lineaire a l'interieur de chaque bloc pour eviter les craquements).</summary>
public sealed class DuckingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float _lastGain = 1f;

    public DuckingSampleProvider(ISampleProvider source) => _source = source;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read <= 0) return read;

        float target = DuckingService.CurrentGain;
        if (target >= 0.9999f && _lastGain >= 0.9999f)
        {
            _lastGain = 1f;
            return read;
        }

        int channels = Math.Max(1, WaveFormat.Channels);
        int frames = read / channels;
        float start = _lastGain;
        for (int f = 0; f < frames; f++)
        {
            float g = start + (target - start) * (f + 1) / frames;
            int baseIndex = offset + f * channels;
            for (int c = 0; c < channels; c++) buffer[baseIndex + c] *= g;
        }
        _lastGain = target;
        return read;
    }
}
