// ============================================================
//  PlaylistsPage.Smart.cs  —  Resona
//  Playlists intelligentes : creation, edition des regles, evaluation (contexte d'ecoute / favoris).
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Resona.Models;

namespace Resona.Views;

public sealed partial class PlaylistsPage
{
	// Contexte d'ecoute (compteurs, derniere ecoute, favoris) calcule une seule fois par rafraichissement de la page,
	// et non pour chaque carte de playlist.
	private SmartContext? _smartCtx;

	private SmartContext GetSmartContext()
	{
		_smartCtx ??= SmartContext.Build(_playlists.FirstOrDefault(p => p.Id == Playlist.FavoritesId)?.TrackIds);
		return _smartCtx;
	}

	private void InitSmartUi()
	{
		if (NewSmartPlaylistLabel != null)
			NewSmartPlaylistLabel.Text = Models.Strings.Current.IsFr ? "Playlist intelligente" : "Smart playlist";
	}

	private List<Track> ResolveSmartTracks(Playlist playlist)
	{
		var def = SmartPlaylistDefinition.FromJson(playlist.SmartRulesJson);
		if (def == null) return new List<Track>();
		return SmartPlaylistEngine.Evaluate(def, _librarySnapshot, GetSmartContext());
	}

	private async void NewSmartPlaylist_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (_librarySnapshot.Count == 0) _librarySnapshot = await App.Cache.LoadAllTracksAsync();
			_smartCtx = null;
			var result = await SmartPlaylistEditor.ShowAsync(this.XamlRoot, null, null, _librarySnapshot, GetSmartContext());
			if (result == null) return;
			await App.Cache.UpsertPlaylistAsync(new Playlist { Name = result.Name, SmartRulesJson = result.Definition.ToJson() });
			await RefreshAsync();
		}
		catch (Exception ex)
		{
			Resona.Services.LogService.Write("SmartPlaylist", ex, "Creation");
		}
	}

	private async Task EditSmartPlaylistAsync(Playlist playlist)
	{
		try
		{
			if (!playlist.IsSmart() || playlist.IsSystem) return;
			_smartCtx = null;
			var result = await SmartPlaylistEditor.ShowAsync(this.XamlRoot, playlist.Name, SmartPlaylistDefinition.FromJson(playlist.SmartRulesJson), _librarySnapshot, GetSmartContext());
			if (result == null) return;
			playlist.Name = result.Name;
			playlist.SmartRulesJson = result.Definition.ToJson();
			playlist.DateModified = DateTime.UtcNow;
			await App.Cache.UpsertPlaylistAsync(playlist);
			await RefreshAsync();
		}
		catch (Exception ex)
		{
			Resona.Services.LogService.Write("SmartPlaylist", ex, "Edition");
		}
	}

	/// <summary>Entree de menu "Modifier les regles" (null si la playlist n'est pas intelligente).</summary>
	private MenuFlyoutItem? BuildEditRulesItem(Playlist playlist)
	{
		if (!playlist.IsSmart() || playlist.IsSystem) return null;
		var item = new MenuFlyoutItem
		{
			Text = Models.Strings.Current.IsFr ? "Modifier les règles…" : "Edit rules…",
			Icon = new FontIcon { Glyph = "" }
		};
		item.Click += async (s, args) => await EditSmartPlaylistAsync(playlist);
		return item;
	}
}
