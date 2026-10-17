using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Resona;

/// <summary>
/// Integration a Windows, sans droits administrateur (tout est ecrit dans HKEY_CURRENT_USER) :
///  - Resona apparait dans "Ouvrir avec" et dans Parametres Windows > Applications par defaut (types de fichiers audio) ;
///  - menu "Ouvrir avec Resona" de l'Explorateur pour un dossier ou une selection de sons (reglage activable / desactivable).
/// Windows interdit a une application de se definir elle-meme comme application par defaut : l'utilisateur choisit Resona
/// dans la page de parametres Windows que le bouton des Parametres de Resona ouvre.
/// Appelee a chaque lancement : ne reecrit que ce qui a change (par ex. si Resona a ete deplace ou la langue changee).
/// </summary>
public static class WindowsIntegration
{
    public const string ProgId = "Resona.AudioFile";
    private const string RegisteredAppName = "Resona";
    private const string MenuKeyName = "ResonaOpen";

    // Types de fichiers proposes. .mp4 est volontairement absent : c'est surtout un format video, Resona ne doit pas s'y associer.
    public static readonly string[] AudioExtensions =
    {
        ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma", ".alac", ".aiff", ".aif",
        ".opus", ".ape", ".m4b", ".dsf", ".dff", ".wv", ".tta", ".spx"
    };

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>Met le registre a jour d'apres les reglages actuels (types de fichiers + menu de l'Explorateur).</summary>
    public static void Apply()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;

        bool fr = Models.Strings.Current.IsFr;
        string command = $"\"{exe}\" \"%1\"";

        bool changed = RegisterFileTypes(exe, command, fr);
        changed |= ApplyContextMenu(exe, command, fr);

        // Previent l'Explorateur seulement si quelque chose a change (evite de rafraichir les icones a chaque lancement).
        if (changed) SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);
    }

    // ------------------------------------------------------------------
    //  Types de fichiers (Ouvrir avec / Applications par defaut)
    // ------------------------------------------------------------------

    private static bool RegisterFileTypes(string exe, string command, bool fr)
    {
        string progKey = $@"Software\Classes\{ProgId}";
        bool changed = false;

        changed |= SetString(progKey, null, fr ? "Fichier audio Resona" : "Resona audio file");
        changed |= SetString(progKey + @"\DefaultIcon", null, $"\"{exe}\",0");
        changed |= SetString(progKey + @"\shell\open\command", null, command);

        string exeName = Path.GetFileName(exe);
        changed |= SetString($@"Software\Classes\Applications\{exeName}", "FriendlyAppName", "Resona");
        changed |= SetString($@"Software\Classes\Applications\{exeName}\shell\open\command", null, command);

        // Capacites de l'application : c'est ce qui la rend visible dans Parametres Windows > Applications par defaut.
        const string capKey = @"Software\Resona\Capabilities";
        changed |= SetString(capKey, "ApplicationName", "Resona");
        changed |= SetString(capKey, "ApplicationDescription",
            fr ? "Lecteur audio moderne pour Windows" : "Modern audio player for Windows");
        foreach (string ext in AudioExtensions)
        {
            changed |= SetString(capKey + @"\FileAssociations", ext, ProgId);

            using var withProgIds = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids", true);
            if (withProgIds != null && withProgIds.GetValue(ProgId) == null)
            {
                withProgIds.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
                changed = true;
            }
        }
        changed |= SetString(@"Software\RegisteredApplications", RegisteredAppName, capKey);

        return changed;
    }

    // ------------------------------------------------------------------
    //  Menu "Ouvrir avec Resona" de l'Explorateur
    // ------------------------------------------------------------------

    private static bool ApplyContextMenu(string exe, string command, bool fr)
    {
        bool enabled = App.Settings?.Current?.ExplorerContextMenuEnabled ?? true;
        string label = fr ? "Ouvrir avec Resona" : "Open with Resona";
        bool changed = false;

        // Dossier (ou plusieurs dossiers selectionnes) + chaque type de son (un ou plusieurs sons selectionnes).
        var menuPaths = new System.Collections.Generic.List<string> { $@"Software\Classes\Directory\shell\{MenuKeyName}" };
        foreach (string ext in AudioExtensions)
            menuPaths.Add($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuKeyName}");

        foreach (string path in menuPaths)
        {
            if (enabled)
            {
                changed |= SetString(path, null, label);
                changed |= SetString(path, "Icon", $"\"{exe}\",0");
                // "Player" : meme avec une selection de plusieurs sons, Explorateur les envoie a la meme application
                // (et les demandes sont de toute facon regroupees par ExternalOpenService).
                changed |= SetString(path, "MultiSelectModel", "Player");
                changed |= SetString(path + @"\command", null, command);
            }
            else
            {
                using var existing = Registry.CurrentUser.OpenSubKey(path);
                if (existing != null)
                {
                    existing.Dispose();
                    Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                    changed = true;
                }
            }
        }
        return changed;
    }

    // ------------------------------------------------------------------
    //  Outils
    // ------------------------------------------------------------------

    /// <summary>Ecrit une valeur texte (name = null : valeur par defaut de la cle) ; renvoie vrai si elle a change.</summary>
    private static bool SetString(string subKey, string? name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey, true);
        if (key == null) return false;
        string valueName = name ?? "";
        if (key.GetValue(valueName) is string current && current == value) return false;
        key.SetValue(valueName, value, RegistryValueKind.String);
        return true;
    }
}
