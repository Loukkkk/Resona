using System;
using System.IO;
using System.Text;

namespace Resona.Services;

/// <summary>
/// Journal d'erreurs de l'application.
/// Ecrit dans %LOCALAPPDATA%\Resona\logs\resona.log (dossier toujours accessible en ecriture,
/// contrairement au dossier de l'executable qui peut etre protege). Ne leve jamais d'exception.
/// </summary>
public static class LogService
{
    private const long MaxFileBytes = 1024 * 1024; // 1 Mo, puis rotation vers resona.old.log
    private static readonly object _lock = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Resona", "logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "resona.log");
    private static string OldLogFilePath => Path.Combine(LogDirectory, "resona.old.log");

    public static void Write(string source, string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Le journal ne doit jamais faire planter l'app.
        }
    }

    public static void Write(string source, Exception ex, string? context = null)
    {
        string message = string.IsNullOrEmpty(context) ? ex.ToString() : context + Environment.NewLine + ex;
        Write(source, message);
    }

    /// <summary>Contenu complet du journal courant (chaine vide s'il n'y en a pas).</summary>
    public static string ReadAll()
    {
        try
        {
            lock (_lock)
            {
                if (!File.Exists(LogFilePath)) return "";
                using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }
        catch
        {
            return "";
        }
    }

    public static void Clear()
    {
        try
        {
            lock (_lock)
            {
                if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
                if (File.Exists(OldLogFilePath)) File.Delete(OldLogFilePath);
            }
        }
        catch
        {
        }
    }

    private static void RotateIfNeeded()
    {
        var info = new FileInfo(LogFilePath);
        if (!info.Exists || info.Length < MaxFileBytes) return;
        if (File.Exists(OldLogFilePath)) File.Delete(OldLogFilePath);
        File.Move(LogFilePath, OldLogFilePath);
    }
}
