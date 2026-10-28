using System.Collections.Generic;
using System.Linq;

namespace Resona.Models;

/// <summary>
/// Categories du volet de gauche : cles, ordre par defaut, ordre choisi par l'utilisateur (AppSettings.NavCategoryOrder)
/// et visibilite.
/// </summary>
public static class NavCategories
{
    public static readonly string[] DefaultOrder =
    {
        "library", "albums", "playlists", "queue", "artists", "genres", "folders", "statistics", "download"
    };

    /// <summary>
    /// Ordre effectif : l'ordre enregistre (cles inconnues et doublons ignores). Une categorie absente de l'ordre enregistre
    /// (ajoutee plus tard, comme la file d'attente) est placee juste apres celle qui la precede par defaut.
    /// </summary>
    public static List<string> ResolveOrder(AppSettings settings)
    {
        var result = new List<string>();
        if (settings.NavCategoryOrder != null)
        {
            foreach (string key in settings.NavCategoryOrder)
            {
                if (DefaultOrder.Contains(key) && !result.Contains(key)) result.Add(key);
            }
        }
        for (int i = 0; i < DefaultOrder.Length; i++)
        {
            string key = DefaultOrder[i];
            if (result.Contains(key)) continue;
            int insertAt = 0;
            for (int j = i - 1; j >= 0; j--)
            {
                int pos = result.IndexOf(DefaultOrder[j]);
                if (pos >= 0) { insertAt = pos + 1; break; }
            }
            result.Insert(insertAt, key);
        }
        return result;
    }

    public static bool IsVisible(AppSettings s, string key) => key switch
    {
        "library" => s.ShowLibraryCategory,
        "albums" => s.ShowAlbumsCategory,
        "playlists" => s.ShowPlaylistsCategory,
        "queue" => s.ShowQueueCategory,
        "artists" => s.ShowArtistsCategory,
        "genres" => s.ShowGenresCategory,
        "folders" => s.ShowFoldersCategory,
        "statistics" => s.ShowStatisticsCategory,
        "download" => s.ShowDownloadCategory,
        _ => true,
    };
}
