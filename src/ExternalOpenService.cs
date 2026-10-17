using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Resona.Services;

namespace Resona;

/// <summary>
/// Sons / dossiers ouverts depuis Windows (double-clic sur un son, "Ouvrir avec Resona", glisser sur l'icone...).
/// Les chemins arrivent soit au demarrage (ligne de commande), soit depuis une 2e instance redirigee vers l'instance deja ouverte.
/// Quand plusieurs sons sont selectionnes, l'Explorateur peut lancer une instance par fichier : on regroupe donc les demandes
/// recues a quelques centaines de millisecondes d'ecart en UNE seule lecture (file d'attente = tous les sons selectionnes).
/// Ces sons sont lus uniquement : ils ne sont jamais ajoutes a la bibliotheque (voir Track.IsExternal).
/// </summary>
public static class ExternalOpenService
{
    // Meme tri que l'Explorateur de Windows (ordre "naturel" : piste 2 avant piste 10).
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string x, string y);

    private const int BatchDelayMs = 900;
    private const int MaxFlushAttempts = 20;

    private static readonly object Gate = new();
    private static readonly List<string> Pending = new();
    private static Timer? _timer;
    private static int _attempts;

    // ------------------------------------------------------------------
    //  Entrees
    // ------------------------------------------------------------------

    /// <summary>Ligne de commande (ou arguments d'activation) : seuls les sons pris en charge et les dossiers existants sont retenus.</summary>
    public static void SubmitFromCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return;
        SubmitPaths(SplitCommandLine(commandLine));
    }

    public static void SubmitPaths(IEnumerable<string> candidates)
    {
        var paths = candidates.Where(IsOpenable).ToList();
        if (paths.Count == 0) return;
        lock (Gate)
        {
            Pending.AddRange(paths);
            _attempts = 0;
            Arm();
        }
    }

    // ------------------------------------------------------------------
    //  Regroupement + envoi a la fenetre principale
    // ------------------------------------------------------------------

    private static void Arm()
    {
        _timer?.Dispose();
        _timer = new Timer(_ => Flush(), null, BatchDelayMs, Timeout.Infinite);
    }

    private static void Flush()
    {
        List<string> batch;
        lock (Gate) { batch = Pending.ToList(); }
        if (batch.Count == 0) return;

        bool sent = false;
        var window = App.MainWindowInstance;
        if (window != null)
        {
            try { sent = window.DispatcherQueue.TryEnqueue(() => window.PlayExternalPaths(batch)); }
            catch (Exception ex) { LogService.Write("ExternalOpen", ex, "Envoi a la fenetre"); }
        }

        lock (Gate)
        {
            if (sent) Pending.RemoveRange(0, Math.Min(batch.Count, Pending.Count));
            else if (++_attempts < MaxFlushAttempts) Arm();   // fenetre pas encore prete : on reessaie
        }
    }

    // ------------------------------------------------------------------
    //  Outils
    // ------------------------------------------------------------------

    private static bool IsOpenable(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (File.Exists(path)) return LibraryScannerService.IsSupportedAudio(path);
            return Directory.Exists(path);
        }
        catch { return false; }
    }

    /// <summary>Decoupe une ligne de commande en gardant les chemins entre guillemets d'un seul bloc
    /// (pas de gestion de l'echappement par antislash : un dossier racine comme "D:\" reste valide).</summary>
    public static List<string> SplitCommandLine(string commandLine)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false, hasToken = false;
        foreach (char c in commandLine)
        {
            if (c == '"') { inQuotes = !inQuotes; hasToken = true; }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken) { result.Add(sb.ToString()); sb.Clear(); hasToken = false; }
            }
            else { sb.Append(c); hasToken = true; }
        }
        if (hasToken) result.Add(sb.ToString());
        return result;
    }

    /// <summary>Liste des sons a lire : les fichiers dans l'ordre recu, les dossiers parcourus recursivement (tries comme l'Explorateur).</summary>
    public static List<string> CollectAudioFiles(IEnumerable<string> paths)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };

        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    if (LibraryScannerService.IsSupportedAudio(path) && seen.Add(path)) result.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    var inFolder = Directory.EnumerateFiles(path, "*", options)
                                            .Where(LibraryScannerService.IsSupportedAudio)
                                            .ToList();
                    inFolder.Sort(StrCmpLogicalW);
                    foreach (string f in inFolder)
                        if (seen.Add(f)) result.Add(f);
                }
            }
            catch (Exception ex) { LogService.Write("ExternalOpen", ex, "Lecture de " + path); }
        }
        return result;
    }
}
