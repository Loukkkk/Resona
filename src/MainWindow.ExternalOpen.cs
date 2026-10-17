// ============================================================
//  MainWindow.ExternalOpen.cs  —  Resona
//  Lecture de sons / dossiers ouverts depuis Windows (double-clic, "Ouvrir avec Resona").
//  Lecture seule : ces sons ne sont PAS ajoutes a la bibliotheque (Track.IsExternal, ignore par le cache de la bibliotheque).
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Resona.Models;
using Resona.Services;

namespace Resona;

public sealed partial class MainWindow
{
    /// <summary>Lance la lecture des sons (ou des dossiers) donnes ; les sons deja presents dans la bibliotheque sont reutilises tels quels.</summary>
    public async void PlayExternalPaths(IReadOnlyList<string> paths)
    {
        try
        {
            var files = await Task.Run(() => ExternalOpenService.CollectAudioFiles(paths));
            if (files.Count == 0) return;

            // Sons deja connus de la bibliotheque (lu ici, sur le thread de l'interface, puis en lecture seule dans les taches).
            var libLookup = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in _library) libLookup.TryAdd(t.FilePath, t);

            // Le 1er son demarre tout de suite ; le reste de la file est prepare en arriere-plan (un dossier peut contenir beaucoup de sons).
            Track? first = null;
            int next = 0;
            while (next < files.Count && first == null)
            {
                string file = files[next++];
                first = libLookup.TryGetValue(file, out var known) ? known : await Task.Run(() => BuildExternalTrack(file));
            }
            if (first == null) return;

            var queue = new List<Track> { first };
            PlayTrack(first, queue);

            if (next < files.Count)
            {
                var rest = files.Skip(next).ToList();
                var built = await Task.Run(() =>
                {
                    var list = new List<Track>();
                    foreach (string f in rest)
                    {
                        var track = libLookup.TryGetValue(f, out var known) ? known : BuildExternalTrack(f);
                        if (track != null) list.Add(track);
                    }
                    return list;
                });
                if (built.Count > 0)
                {
                    queue.AddRange(built);   // meme liste que la file de lecture : les sons suivants s'enchainent
                    try { UpdateUpNextPanel(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Write("ExternalOpen", ex, "Lecture d'un son ouvert depuis Windows");
        }
    }

    /// <summary>Vrai si les octets forment une image lisible : une pochette integree corrompue est ignoree (= son sans pochette).</summary>
    private static bool IsDecodableImage(byte[] data)
    {
        try
        {
            using var ms = new System.IO.MemoryStream(data);
            using var img = System.Drawing.Image.FromStream(ms);
            return img.Width > 0 && img.Height > 0;
        }
        catch { return false; }
    }

    private static Track? BuildExternalTrack(string file)
    {
        try
        {
            var track = App.Scanner.ExtractMetadata(file, out byte[]? cover);
            if (track == null) return null;
            track.IsExternal = true;

            // Pochette integree : enregistree dans le cache des pochettes (pas dans la bibliotheque).
            // Artiste ET album inconnus : cle propre au son (sinon tous les sons "inconnus" partageraient le meme fichier de pochette).
            if (cover is { Length: > 0 } && IsDecodableImage(cover))
            {
                bool unknownArtist = string.Equals(track.Artist, Strings.Current.CS_ArtisteInconnu, StringComparison.OrdinalIgnoreCase);
                bool unknownAlbum = string.Equals(track.Album, Strings.Current.CS_AlbumInconnu, StringComparison.OrdinalIgnoreCase);
                string key = CoverArtService.GetAlbumCacheKey(unknownArtist ? "" : track.Artist, unknownAlbum ? "" : track.Album, track.Id);
                string? coverPath = App.CoverArt.SaveEmbeddedCoverAsync(key, cover).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(coverPath)) track.CoverArtPath = coverPath;
            }
            return track;
        }
        catch (Exception ex)
        {
            LogService.Write("ExternalOpen", ex, "Lecture des informations de " + file);
            return null;
        }
    }
}
