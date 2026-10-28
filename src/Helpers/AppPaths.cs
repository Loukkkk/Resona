using System;
using System.IO;

namespace Resona;

/// <summary>
/// Centralise les chemins d'accès aux données de l'application.
/// En mode DEBUG : utilise %LOCALAPPDATA%\Resona-Dev pour isoler complètement l'environnement de dev.
/// En mode RELEASE : utilise %LOCALAPPDATA%\Resona (données de production de l'utilisateur final).
/// </summary>
public static class AppPaths
{
#if DEBUG
    public const string AppDataFolderName = "Resona-Dev";
#else
    public const string AppDataFolderName = "Resona";
#endif

    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppDataFolderName);

    public static string CoversDir => Path.Combine(AppDataDir, "Covers");
    public static string PlaylistCoversDir => Path.Combine(AppDataDir, "PlaylistCovers");
    public static string LogsDir => Path.Combine(AppDataDir, "logs");
    public static string FpcalcDir => Path.Combine(AppDataDir, "fpcalc");
}
