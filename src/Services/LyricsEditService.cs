using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Resona.Services;

/// <summary>
/// Edition des paroles d'un morceau : retrouve OU elles sont stockees (meme ordre de recherche que
/// LocalLyricsService.FindLocalLyrics : tag du fichier audio, puis .lrc / .txt a cote du fichier, puis sous-dossier Lyrics)
/// et les enregistre de facon permanente a cet endroit.
/// Rien ici n'est appele sans que l'utilisateur ait coche "enregistrer dans le fichier" : sans cette case, les paroles
/// modifiees restent seulement dans Resona (LyricsOverrideService) et aucun fichier n'est touche.
/// </summary>
public static class LyricsEditService
{
    public enum SourceKind { None, EmbeddedTag, Sidecar }

    /// <param name="Kind">Ou sont les paroles locales (None = aucune : elles viennent d'internet ou n'existent pas).</param>
    /// <param name="FilePath">Chemin du .lrc / .txt quand Kind = Sidecar.</param>
    public sealed record LyricsSource(SourceKind Kind, string? FilePath);

    public sealed class SaveResult
    {
        public bool Success { get; init; }
        /// <summary>Ou les paroles ont ete ecrites (EmbeddedTag ou Sidecar). Pour un echec : la destination visee.</summary>
        public SourceKind Target { get; init; }
        public string? TargetPath { get; init; }
        public string Error { get; init; } = string.Empty;
    }

    // Meme liste que LocalLyricsService (les deux ecritures Lyrics / lyrics visent le meme dossier sous Windows).
    private static readonly string[] LyricsSubfolders = { "Lyrics", "lyrics" };

    // ------------------------------------------------------------------
    //  Ou sont les paroles locales ?
    // ------------------------------------------------------------------

    /// <summary>
    /// Premiere source locale NON VIDE, dans l'ordre de la detection locale : tag integre, fichier voisin (.lrc puis .txt),
    /// sous-dossier Lyrics. None si rien en local : on integrera alors les paroles au tag du fichier audio.
    /// </summary>
    public static LyricsSource LocateSource(string audioPath)
    {
        try
        {
            using var file = TagLib.File.Create(audioPath);
            if (!string.IsNullOrWhiteSpace(file.Tag.Lyrics)) return new LyricsSource(SourceKind.EmbeddedTag, null);
        }
        catch { }

        string? dir = Path.GetDirectoryName(audioPath);
        if (!string.IsNullOrEmpty(dir))
        {
            string baseName = Path.GetFileNameWithoutExtension(audioPath);
            string? found = FirstNonEmptySidecar(dir, baseName);
            if (found == null)
            {
                foreach (string sub in LyricsSubfolders)
                {
                    found = FirstNonEmptySidecar(Path.Combine(dir, sub), baseName);
                    if (found != null) break;
                }
            }
            if (found != null) return new LyricsSource(SourceKind.Sidecar, found);
        }

        return new LyricsSource(SourceKind.None, null);
    }

    private static string? FirstNonEmptySidecar(string folder, string baseName)
    {
        try
        {
            if (!Directory.Exists(folder)) return null;
            foreach (string ext in new[] { ".lrc", ".txt" })
            {
                string path = Path.Combine(folder, baseName + ext);
                if (File.Exists(path) && !string.IsNullOrWhiteSpace(File.ReadAllText(path))) return path;
            }
        }
        catch { }
        return null;
    }

    // ------------------------------------------------------------------
    //  Enregistrement permanent
    // ------------------------------------------------------------------

    /// <summary>
    /// Ecrase la source locale des paroles (tag ou fichier .lrc / .txt) ou, s'il n'y en a pas, integre les paroles au tag
    /// du fichier audio. Ne leve jamais d'exception : l'echec est decrit dans le resultat.
    /// </summary>
    public static async Task<SaveResult> SaveToFileAsync(string audioPath, string text)
    {
        LyricsSource source = await Task.Run(() => LocateSource(audioPath));
        try
        {
            if (source.Kind == SourceKind.Sidecar && source.FilePath != null)
            {
                string target = source.FilePath;
                await Task.Run(() => WriteSidecar(target, text));
                return new SaveResult { Success = true, Target = SourceKind.Sidecar, TargetPath = target };
            }

            await WriteEmbeddedAsync(audioPath, text);
            return new SaveResult { Success = true, Target = SourceKind.EmbeddedTag };
        }
        catch (Exception ex)
        {
            LogService.Write("Lyrics", ex, $"Enregistrement des paroles impossible ({audioPath})");
            return new SaveResult { Success = false, Target = source.Kind, TargetPath = source.FilePath, Error = ex.Message };
        }
    }

    /// <summary>Fichier .lrc / .txt : on garde l'encodage (BOM) et le type de fin de ligne du fichier remplace, ecriture atomique.</summary>
    private static void WriteSidecar(string path, string text)
    {
        bool bom = false;
        bool crlf = true;
        try
        {
            if (File.Exists(path))
            {
                byte[] head = new byte[3];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int n = fs.Read(head, 0, 3);
                    bom = n == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
                }
                string existing = File.ReadAllText(path);
                crlf = existing.Contains("\r\n") || !existing.Contains('\n');
            }
        }
        catch { }

        string content = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (crlf) content = content.Replace("\n", "\r\n");

        string tmp = path + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        try
        {
            File.WriteAllText(tmp, content, new UTF8Encoding(bom));
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Tag du fichier audio. Si le fichier est celui en cours de lecture (ou le suivant, precharge), le lecteur le tient ouvert
    /// et Windows interdit d'y ecrire : on le libere le temps de l'ecriture puis la lecture reprend au meme endroit.
    /// </summary>
    private static async Task WriteEmbeddedAsync(string audioPath, string text)
    {
        EnsureTagWritable(audioPath);
        Action write = () => WriteTagWithRetry(audioPath, text);

        var engine = App.AudioEngine;
        if (engine != null) await engine.RunWithFileReleasedAsync(audioPath, write);
        else await Task.Run(write);
    }

    private static void EnsureTagWritable(string audioPath)
    {
        if (!File.Exists(audioPath)) throw new FileNotFoundException("Le fichier audio est introuvable.", audioPath);

        // Certains fichiers ont une extension qui ne correspond pas a leur contenu (ex. un .mp3 qui est en fait un m4a) : l'app sait
        // les lire, mais TagLib les analyserait selon l'extension. On refuse plutot que de risquer d'abimer le fichier.
        string ext = Path.GetExtension(audioPath);
        string realExt = Path.GetExtension(LibraryScannerService.ResolveRealExtension(audioPath));
        if (!string.Equals(ExtensionFamily(ext), ExtensionFamily(realExt), StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Le contenu du fichier ({realExt}) ne correspond pas a son extension ({ext}) : ecriture refusee pour ne pas l'abimer.");
        }
    }

    private static string ExtensionFamily(string ext) => ext.ToLowerInvariant() switch
    {
        ".m4a" or ".mp4" or ".m4b" or ".alac" => "mp4",
        ".ogg" or ".opus" or ".oga" or ".spx" => "ogg",
        string other => other
    };

    // Le fichier peut etre tenu un instant par une autre lecture (analyse de volume en arriere-plan...) : quelques essais.
    private static void WriteTagWithRetry(string audioPath, string text)
    {
        const int attempts = 3;
        for (int i = 1; ; i++)
        {
            try
            {
                WriteTag(audioPath, text);
                return;
            }
            catch (IOException) when (i < attempts)
            {
                System.Threading.Thread.Sleep(300);
            }
        }
    }

    private static void WriteTag(string audioPath, string text)
    {
        using (var file = TagLib.File.Create(audioPath))
        {
            file.Tag.Lyrics = text;
            file.Save();
        }

        // Verification : certains formats ne conservent pas ce champ. Mieux vaut le savoir que croire que c'est enregistre.
        using (var check = TagLib.File.Create(audioPath))
        {
            if (!SameText(check.Tag.Lyrics, text))
            {
                throw new NotSupportedException("Ce format de fichier ne conserve pas les paroles dans ses tags.");
            }
        }
    }

    private static bool SameText(string? a, string? b) => Normalize(a) == Normalize(b);

    private static string Normalize(string? s) => (s ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
}
