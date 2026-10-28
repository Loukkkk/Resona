// ============================================================
//  PlaylistDetailPage.Smart.cs  —  Resona
//  Playlists intelligentes : le contenu est recalcule a chaque ouverture de la page de detail.
// ============================================================

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Resona.Models;

namespace Resona.Views;

public sealed partial class PlaylistDetailPage
{
	private async Task LoadSmartTracksAsync()
	{
		var playlist = _playlist;
		var def = SmartPlaylistDefinition.FromJson(playlist.SmartRulesJson);
		if (def == null) return;

		IEnumerable<string> favoriteIds = new List<string>();
		if (App.MainWindowInstance != null)
		{
			var fav = await App.MainWindowInstance.GetFavoriteTrackIdsAsync();
			if (fav != null) favoriteIds = fav.Cast<string>().ToList();
		}

		// L'utilisateur est peut-etre deja parti vers une autre playlist pendant le calcul.
		if (!ReferenceEquals(playlist, _playlist)) return;

		var ctx = SmartContext.Build(favoriteIds);
		_tracks = SmartPlaylistEngine.Evaluate(def, _librarySnapshot, ctx);
		BuildUI();
	}
}
