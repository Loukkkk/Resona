using Microsoft.UI.Text;





using Microsoft.UI.Input;





using Windows.UI.Core;





using Microsoft.UI.Xaml.Shapes;





using System.Runtime.CompilerServices;





using System.Numerics;





using Windows.Foundation;





using Microsoft.UI.Xaml.Media.Imaging;





using System;





using System.Collections.Generic;





using System.Collections.ObjectModel;





using System.Linq;





using System.Threading.Tasks;





using Resona.Models;





using Resona.Services;





using Microsoft.UI;





using Microsoft.UI.Xaml;





using Microsoft.UI.Xaml.Controls;





using Microsoft.UI.Xaml.Input;





using Microsoft.UI.Xaml.Media;





using Microsoft.UI.Xaml.Markup;





using Microsoft.UI.Xaml.Navigation;





using Windows.Storage;





using Windows.Storage.Pickers;





using Windows.System;





using NAudio.Wave;





using WinRT;





using WinRT.Interop;











namespace Resona.Views;











public sealed partial class PlaylistsPage : Page





{





	private List<Track> _librarySnapshot = new List<Track>();











	private List<Playlist> _playlists = new List<Playlist>();











	private Playlist? _openPlaylist;











	private List<Track> _opentracks = new List<Track>();











	private Grid? _activeIndicatorGrid;











	private DispatcherTimer? _rmsTimer;











	private float _smoothedRms;





	public ObservableCollection<Track> DisplayedTracks { get; } = new ObservableCollection<Track>();











	public PlaylistsPage()





	{





		InitializeComponent();





		StartRmstimer();





		base.Loaded += delegate





		{





			MainWindow.GlobalClickOutside += OnGlobalClickOutside;





			if (AIPanel != null)





			{





				AIPanel.Visibility = ((!App.Settings.Current.AIEnabled) ? Visibility.Collapsed : Visibility.Visible);





			}





		};





		base.Unloaded += delegate





		{





			MainWindow.GlobalClickOutside -= OnGlobalClickOutside;





		};





	}











	private void OnGlobalClickOutside()





	{





		DispatcherQueue?.TryEnqueue(() =>





		{





			if (PlaylistsGrid != null)





			{





				PlaylistsGrid.SelectedItems.Clear();





				foreach (var item in PlaylistsGrid.Items)





				{





					if (item is Border b && b.Tag is UIElement overlay)





					{





						overlay.Visibility = Visibility.Collapsed;





					}





				}





			}





		});





	}











	private void StartRmstimer()





	{





		_rmsTimer = new DispatcherTimer





		{





			Interval = TimeSpan.FromMilliseconds(50.0)





		};





		_rmsTimer.Tick += delegate





		{





			UpdateRmsBars();





		};





		_rmsTimer.Start();





	}











	private void UpdateRmsBars()





	{





		if (App.AudioEngine.State != NAudio.Wave.PlaybackState.Playing && _smoothedRms < 0.01f) { _smoothedRms = 0; return; }





		if (!(_activeIndicatorGrid == null))





		{





			float currentRmsLevel = App.AudioEngine.State == NAudio.Wave.PlaybackState.Playing ? App.AudioEngine.CurrentRmsLevel : 0f;





			_smoothedRms = _smoothedRms * 0.85f + currentRmsLevel * 0.15f;





			float num = Math.Min(1f, _smoothedRms * 8f);





			double num2 = (double)DateTime.UtcNow.Ticks / 10000.0;





			float num3 = Math.Max(0.1f, num * (float)(0.6 + 0.4 * Math.Sin(num2 / 180.0)));





			float num4 = Math.Max(0.1f, num * (float)(0.6 + 0.4 * Math.Sin(num2 / 220.0 + 1.2)));





			float num5 = Math.Max(0.1f, num * (float)(0.6 + 0.4 * Math.Sin(num2 / 160.0 + 2.4)));





			if (FindChildByName<Border>(_activeIndicatorGrid, "Bar1")?.RenderTransform is ScaleTransform scaleTransform)





			{





				scaleTransform.ScaleY = num3;





			}





			if (FindChildByName<Border>(_activeIndicatorGrid, "Bar2")?.RenderTransform is ScaleTransform scaleTransform2)





			{





				scaleTransform2.ScaleY = num4;





			}





			if (FindChildByName<Border>(_activeIndicatorGrid, "Bar3")?.RenderTransform is ScaleTransform scaleTransform3)





			{





				scaleTransform3.ScaleY = num5;





			}





		}





	}











	public void SetNowPlayingId(string? trackId, string? trackFilePath = null)





	{





	}











	private void SyncNowPlayingId()





	{





	}











	protected override void OnNavigatedTo(NavigationEventArgs e)





	{





		base.OnNavigatedTo(e);





		SyncNowPlayingId();





		RefreshAsync();





	}











	protected override void OnNavigatedFrom(NavigationEventArgs e)





	{





		base.OnNavigatedFrom(e);





	}











	public async Task RefreshAsync()





	{





		_librarySnapshot = await App.Cache.LoadAllTracksAsync();





		_playlists = await App.Cache.LoadAllPlaylistsAsync();





		PlaylistsGrid.Items.Clear();





		DetailPanel.Visibility = Visibility.Collapsed;





		_openPlaylist = null;





		if (_playlists.Count == 0)





		{





			TextBlock item = new TextBlock





			{





				Opacity = 0.5,





				TextWrapping = TextWrapping.Wrap,





				Margin = new Thickness(0.0, 8.0, 0.0, 0.0),





				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]





			};





			item.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = Models.Strings.Current, Path = new PropertyPath("CS_NoPlaylistInCategory"), Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay });





			PlaylistsGrid.Items.Add(item);





			return;





		}





		foreach (Playlist playlist in _playlists)





		{





			PlaylistsGrid.Items.Add(BuildFolderCard(playlist));





		}





	}











	private UIElement BuildFolderCard(Playlist playlist)





	{





		List<Track> tracks = ResolvePlaylisttracks(playlist);





		Border border = new Border





		{





			DataContext = playlist,





			Width = 160.0,





			CornerRadius = new CornerRadius(10.0),





			Background = (Brush)Application.Current.Resources["AppSurfaceBrush"],





			Translation = new Vector3(0f, 0f, 8f),





			Shadow = new ThemeShadow()





		};





		UIElement uIElement = (border.Child = new Grid());





		Grid grid = (Grid)uIElement;





		StackPanel stackPanel = new StackPanel();





		grid.Children.Add(stackPanel);





		Border border2 = new Border





		{





			Width = 160.0,





			Height = 160.0,





			CornerRadius = new CornerRadius(10.0, 10.0, 0.0, 0.0)





		};





		Grid grid2 = new Grid





		{





			Width = 160.0,





			Height = 160.0,





			Clip = new RectangleGeometry





			{





				Rect = new Rect(0f, 0f, 160f, 160f)





			}





		};





		if (!string.IsNullOrEmpty(playlist.CoverImagePath))





		{





			grid2.Children.Add(new Image
			{
				Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(playlist.CoverImagePath)) { DecodePixelHeight = 320 },
				Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			});





		}





		else





		{





			grid2.Children.Add(BuildCoverMosaic(tracks, 160.0, 160.0));





		}





		border2.Child = grid2;





		stackPanel.Children.Add(border2);





		StackPanel stackPanel2 = new StackPanel





		{





			Padding = new Thickness(8.0, 6.0, 8.0, 8.0),





			Spacing = 1.0,





			HorizontalAlignment = HorizontalAlignment.Stretch





		};





		TextBlock name = new TextBlock





		{





			Text = playlist.Name,





			FontWeight = FontWeights.SemiBold,





			FontSize = 12.0,





			LineHeight = 16.0,





			TextTrimming = TextTrimming.CharacterEllipsis,





			MaxLines = 1,





			TextAlignment = TextAlignment.Center,





			HorizontalAlignment = HorizontalAlignment.Stretch,





			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]





		};





		stackPanel2.Children.Add(name);





		stackPanel2.Children.Add(new TextBlock





		{





			Text = Resona.Models.Strings.Current.FormatTracksCount(tracks.Count),





			Opacity = 0.5,





			FontSize = 10.0,





			LineHeight = 14.0,





			TextAlignment = TextAlignment.Center,





			HorizontalAlignment = HorizontalAlignment.Stretch,





			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]





		});





		stackPanel.Children.Add(stackPanel2);





		Border hoverOverlay = new Border





		{





			Background = new SolidColorBrush(Colors.White),





			CornerRadius = new CornerRadius(10.0),





			Opacity = 0.0,





			IsHitTestVisible = false





		};





		grid.Children.Add(hoverOverlay);











		Border selectionOverlay = new Border





		{





			BorderThickness = new Thickness(3),





			BorderBrush = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"],





			CornerRadius = new CornerRadius(10.0),





			Visibility = Visibility.Collapsed,





			IsHitTestVisible = false





		};





		grid.Children.Add(selectionOverlay);





		border.Tag = selectionOverlay;











		border.PointerEntered += delegate





		{





			hoverOverlay.Opacity = 0.08;





		};





		border.PointerExited += delegate





		{





			hoverOverlay.Opacity = 0.0;





		};





		border.PointerCanceled += delegate





		{





			hoverOverlay.Opacity = 0.0;





		};





		border.PointerCaptureLost += delegate





		{





			hoverOverlay.Opacity = 0.0;





		};





		return border;





	}











	private void PlaylistsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)





	{





		foreach (var item in PlaylistsGrid.Items)





		{





			if (item is Border b && b.Tag is UIElement overlay)





			{





				overlay.Visibility = PlaylistsGrid.SelectedItems.Contains(b) ? Visibility.Visible : Visibility.Collapsed;





			}





		}





	}











	private void PlaylistsGrid_Tapped(object sender, TappedRoutedEventArgs e)





	{





		if (e.OriginalSource is FrameworkElement fe && fe.DataContext is Playlist)





		{





			return; // Clicked on a playlist, handled by ItemClick





		}





		PlaylistsGrid.SelectedItems.Clear();





	}











	private void PlaylistsGrid_ItemClick(object sender, ItemClickEventArgs e)
	{
		if (MainWindow.LastClickWasXButton) { MainWindow.LastClickWasXButton = false; return; }





		var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);





		var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);





		if ((ctrl & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down ||





			(shift & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down)





		{





			return;





		}











		if (e.ClickedItem is Border border && border.Child is Grid grid)





		{





			foreach (var child in grid.Children)





			{





				if (child is Border b && b.Background is SolidColorBrush sb && sb.Color == Colors.White)





				{





					b.Opacity = 0.0;





					break;





				}





			}





		}











		if ((e.ClickedItem as FrameworkElement)?.DataContext is Playlist playlist)





		{





			App.MainWindowInstance?.NavigateToPlaylistDetail(playlist, _librarySnapshot);





		}





	}











	private static Grid BuildCoverMosaic(List<Track> tracks, double w, double h)





	{





		Grid grid = new Grid





		{





			Width = w,





			Height = h





		};





		List<string> list = (from t in tracks





			where !string.IsNullOrEmpty(t.CoverArtPath) && File.Exists(t.CoverArtPath)





			select t.CoverArtPath).Distinct().Take(4).ToList();





		if (list.Count == 0)





		{





			grid.Background = (Brush)Application.Current.Resources["AppAccentBrush"];





			grid.Children.Add(new FontIcon





			{





				Glyph = "\ue8d6",





				FontSize = 36.0,





				Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],





				HorizontalAlignment = HorizontalAlignment.Center,





				VerticalAlignment = VerticalAlignment.Center





			});





			return grid;





		}





		if (list.Count < 4)





		{





			grid.Children.Add(new Image
		{
			Source = SafeImage(list[0]),
			Stretch = Stretch.UniformToFill,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		});





			return grid;





		}





		double num = w / 2.0;





		double num2 = h / 2.0;





		grid.ColumnDefinitions.Add(new ColumnDefinition





		{





			Width = new GridLength(num)





		});





		grid.ColumnDefinitions.Add(new ColumnDefinition





		{





			Width = new GridLength(num)





		});





		grid.RowDefinitions.Add(new RowDefinition





		{





			Height = new GridLength(num2)





		});





		grid.RowDefinitions.Add(new RowDefinition





		{





			Height = new GridLength(num2)





		});





		int[] array = new int[4] { 0, 1, 0, 1 };





		int[] array2 = new int[4] { 0, 0, 1, 1 };





		for (int num3 = 0; num3 < 4; num3++)





		{





			Border cell = new Border
			{
				Width = num,
				Height = num2,
				Child = new Image
				{
					Source = SafeImage(list[num3]),
					Stretch = Stretch.UniformToFill,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				}
			};





			Grid.SetColumn(cell, array[num3]);





			Grid.SetRow(cell, array2[num3]);





			grid.Children.Add(cell);





		}





		return grid;





	}











	private static BitmapImage? SafeImage(string path)





	{





		return CoverCacheService.GetBitmap(path, 80);





	}











	private void UpdateDisplayedTracks(List<Track> newItems)





	{





		for (int i = 0; i < newItems.Count; i++)





		{





			if (i < DisplayedTracks.Count) DisplayedTracks[i] = newItems[i];





			else DisplayedTracks.Add(newItems[i]);





		}





		while (DisplayedTracks.Count > newItems.Count) DisplayedTracks.RemoveAt(DisplayedTracks.Count - 1);





	}











	private void OpenPlaylistDetail(Playlist playlist)





	{





		SyncNowPlayingId();





		_openPlaylist = playlist;





		_opentracks = ResolvePlaylisttracks(playlist);





		foreach (Track opentrack in _opentracks)





		{





			opentrack.IsPlaying = !string.IsNullOrEmpty(App.NowPlayingFilePath) && string.Equals(opentrack.FilePath, App.NowPlayingFilePath, StringComparison.OrdinalIgnoreCase);





		}





		DetailTitle.Text = playlist.Name;





		DetailCount.Text = Resona.Models.Strings.Current.FormatTracksCount(_opentracks.Count);





		





		UpdateDisplayedTracks(_opentracks);





		





		DetailPanel.Visibility = Visibility.Visible;





	}











	private List<Track> ResolvePlaylisttracks(Playlist playlist)





	{





		return playlist.TrackIds.Select(id => _librarySnapshot.FirstOrDefault(t => t.Id == id)).Where(t => t != null).ToList();





	}























    private void TrackTitle_Click(object sender, RoutedEventArgs e)





    {





        if (sender is HyperlinkButton btn && btn.Tag is Resona.Models.Track track)





        {





            App.MainWindowInstance?.ShowTrackInfo(track);





        }





    }











	private void DetailClose_Click(object sender, RoutedEventArgs e)





	{





		DetailPanel.Visibility = Visibility.Collapsed;





		DisplayedTracks.Clear();





		_openPlaylist = null;





		_opentracks.Clear();





	}











	public bool TryGoBack()





	{





		if (DetailPanel.Visibility == Visibility.Visible)





		{





			DetailClose_Click(null, null);





			return true;





		}





		return false;





	}











	private void DetailPlayAll_Click(object sender, RoutedEventArgs e)





	{





		if (_opentracks.Count > 0)





		{





			App.MainWindowInstance?.PlayTrack(_opentracks[0], _opentracks);
			App.MainWindowInstance?.EnableContinuousPlaybackIfOff();
		}





	}











	private async void DetailExport_Click(object sender, RoutedEventArgs e)





	{





		if (_openPlaylist != null)





		{





			await ExportPlaylistAsync(_openPlaylist, _opentracks);





		}





	}











	private async void DetailRename_Click(object sender, RoutedEventArgs e)





	{





		if (_openPlaylist != null)





		{





			await RenamePlaylistAsync(_openPlaylist, null);





		}





	}











	private async void DetailDelete_Click(object sender, RoutedEventArgs e)





	{





		if (_openPlaylist != null)





		{





			await DeletePlaylistAsync(_openPlaylist);





		}





	}











	private async void NewPlaylist_Click(object sender, RoutedEventArgs e)





	{





		TextBox nameBox = new TextBox { PlaceholderText = Models.Strings.Current.IsFr ? "Nom de la playlist" : "Playlist name", Text = Models.Strings.Current.CS_MyPlaylist };





		ContentDialog contentDialog = new ContentDialog





		{





			Title = Models.Strings.Current.CS_NewPlaylist,





			PrimaryButtonText = Models.Strings.Current.IsFr ? "Créer" : "Create",





			CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,





			DefaultButton = ContentDialogButton.Primary,





			Content = nameBox,





			XamlRoot = this.XamlRoot





		};





		contentDialog.Opened += (s, args) =>





		{





			nameBox.Focus(FocusState.Programmatic);





			nameBox.SelectAll();





		};





		if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)





		{





			string text = nameBox.Text.Trim();





			if (string.IsNullOrEmpty(text)) text = Models.Strings.Current.CS_NewPlaylist;





			await App.Cache.UpsertPlaylistAsync(new Playlist { Name = text });





			await RefreshAsync();





		}





	}











	private void SortPlaylistsMenu_Click(object sender, RoutedEventArgs e)





	{





		if (sender is MenuFlyoutItem item && item.Tag is string tag)





		{





			SortButtonLabel.Text = (Models.Strings.Current.IsFr ? "Trier : " : "Sort: ") + item.Text;





			if (tag == "name_asc")





			{





				_playlists = _playlists.OrderBy(p => p.Name).ToList();





			}





			else if (tag == "name_desc")





			{





				_playlists = _playlists.OrderByDescending(p => p.Name).ToList();





			}





			else if (tag == "date_desc")





			{





				_playlists = _playlists.OrderByDescending(p => p.DateCreated).ToList();





			}





			else if (tag == "count_desc")





			{





				_playlists = _playlists.OrderByDescending(p => p.TrackIds?.Count ?? 0).ToList();





			}











			PlaylistsGrid.Items.Clear();





			foreach (Playlist playlist in _playlists)





			{





				PlaylistsGrid.Items.Add(BuildFolderCard(playlist));





			}





		}





	}











	private async void Import_Click(object sender, RoutedEventArgs e)





	{





		try





		{





			Windows.Storage.Pickers.FileOpenPicker picker = new Windows.Storage.Pickers.FileOpenPicker { ViewMode = Windows.Storage.Pickers.PickerViewMode.List };





			var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);





			WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);





			picker.FileTypeFilter.Add(".m3u");





			picker.FileTypeFilter.Add(".m3u8");





			var files = await picker.PickMultipleFilesAsync();





			if (files == null || files.Count == 0) return;





			





			int totalImported = 0;





			int totalMissing = 0;





			List<Track> allNewTracks = new List<Track>();





			var allTracks = await App.Cache.LoadAllTracksAsync();





			Dictionary<string, Track> byPath = allTracks.ToDictionary(t => t.FilePath, StringComparer.OrdinalIgnoreCase);





			





			var loadingDialog = new ContentDialog





			{





				Content = new StackPanel





				{





					HorizontalAlignment = HorizontalAlignment.Stretch,





					VerticalAlignment = VerticalAlignment.Center,





					Margin = new Thickness(0, -30, 0, 0),





					Children = {





						new TextBlock { Text = Models.Strings.Current.CS_Importing, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center },





						new ProgressRing { IsActive = true, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Center },





						new TextBlock { Text = Models.Strings.Current.IsFr ? "Veuillez patienter..." : "Please wait...", HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center }





					}





				},





				XamlRoot = this.XamlRoot





			};





			_ = loadingDialog.ShowAsync();











			List<Playlist> newPlaylists = new List<Playlist>();











			foreach (var file in files)





			{





				var tuple = await App.PlaylistIO.ImportAsync(file.Path);





				List<string> resolvedPaths = tuple.Item1;





				List<string> missingPaths = tuple.Item2;





				string? coverPath = tuple.Item3;





				totalMissing += missingPaths.Count;





				





				Playlist playlist = new Playlist { Name = System.IO.Path.GetFileNameWithoutExtension(file.Name) };





				if (!string.IsNullOrEmpty(coverPath)) { string appData = Windows.Storage.ApplicationData.Current.LocalFolder.Path; string coversDir = System.IO.Path.Combine(appData, "Covers"); System.IO.Directory.CreateDirectory(coversDir); string dest = System.IO.Path.Combine(coversDir, System.IO.Path.GetFileName(coverPath)); try { System.IO.File.Copy(coverPath, dest, true); playlist.CoverImagePath = dest; } catch {} }





				





				var pathsToExtract = resolvedPaths.Where(p => !byPath.ContainsKey(p)).Distinct().ToList();





				var extractedTracks = new System.Collections.Concurrent.ConcurrentBag<Track>();





				





				await Parallel.ForEachAsync(pathsToExtract, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (path, ct) =>





				{





					var track = App.Scanner.ExtractMetadata(path, out byte[] embeddedCoverBytes);





					if (track != null)





					{





						if (embeddedCoverBytes != null && embeddedCoverBytes.Length != 0)





						{





							string coverPath = await App.CoverArt.SaveEmbeddedCoverAsync(track.Id, embeddedCoverBytes);





							if (coverPath != null) track.CoverArtPath = coverPath;





						}





						extractedTracks.Add(track);





					}





				});











				var newTracksBatch = extractedTracks.ToList();





				if (newTracksBatch.Count > 0)





				{





					await App.Cache.UpsertTracksBatchedAsync(newTracksBatch);





					allNewTracks.AddRange(newTracksBatch);





					foreach (var track in newTracksBatch)





					{





						byPath[track.FilePath] = track;





					}





				}











				foreach (string path in resolvedPaths)





				{





					if (byPath.TryGetValue(path, out Track track))





					{





						playlist.TrackIds.Add(track.Id);





						totalImported++;





					}





				}





				newPlaylists.Add(playlist);





			}





			





			foreach(var pl in newPlaylists)





			{





				await App.Cache.UpsertPlaylistAsync(pl);





			}











			loadingDialog.Hide();











			if (allNewTracks.Count > 0)





			{





				App.MainWindowInstance?.FetchCoversForTracks(allNewTracks);





			}





			await RefreshAsync();





			





			string msg = string.Format(Models.Strings.Current.CS_ImportCompleteDesc, files.Count, totalImported);





			if (totalMissing > 0) msg += Models.Strings.Current.IsFr ? $"\n{totalMissing} piste(s) introuvable(s) sur le disque." : $"\n{totalMissing} track(s) missing on disk.";





			





			await new ContentDialog { Title = Models.Strings.Current.CS_ImportComplete, Content = msg, CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





		}





		catch (Exception ex)





		{





			await new ContentDialog { Title = Models.Strings.Current.IsFr ? "Erreur" : "Error", Content = (Models.Strings.Current.IsFr ? "Erreur lors de l'import: " : "Error during import: ") + ex.Message, CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





		}





	}











	private async Task ExportPlaylistAsync(Playlist playlist, List<Track> tracks)





	{





		if (tracks.Count == 0)





		{





			await new ContentDialog { Title = Models.Strings.Current.IsFr ? "Playlist vide" : "Empty playlist", Content = Models.Strings.Current.IsFr ? "Aucune piste à exporter." : "No tracks to export.", CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





			return;





		}





		Windows.Storage.Pickers.FileSavePicker picker = new Windows.Storage.Pickers.FileSavePicker();





		var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);





		WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);





		picker.FileTypeChoices.Add("Playlist M3U8 (UTF-8)", new List<string> { ".m3u8" });





		picker.FileTypeChoices.Add("Playlist M3U (legacy)", new List<string> { ".m3u" });





		picker.SuggestedFileName = playlist.Name;





		var file = await picker.PickSaveFileAsync();





		if (file != null)





		{





			bool legacy = file.FileType.ToLower() == ".m3u";





			await App.PlaylistIO.ExportAsync(file.Path, tracks, legacy, playlist.CoverImagePath);





		}





	}











	private async Task RenamePlaylistAsync(Playlist playlist, TextBlock? nameDisplay)





	{





		TextBox nameBox = new TextBox { Text = playlist.Name };





		ContentDialog contentDialog = new ContentDialog





		{





			Title = Models.Strings.Current.IsFr ? "Renommer" : "Rename",





			PrimaryButtonText = "OK",





			CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,





			DefaultButton = ContentDialogButton.Primary,





			Content = nameBox,





			XamlRoot = this.XamlRoot





		};





		contentDialog.Opened += (s, args) =>





		{





			nameBox.Focus(FocusState.Programmatic);





			nameBox.SelectAll();





		};





		if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)





		{





			playlist.Name = nameBox.Text;





			playlist.DateModified = DateTime.UtcNow;





			await App.Cache.UpsertPlaylistAsync(playlist);





			if (nameDisplay != null) nameDisplay.Text = playlist.Name;





			if (_openPlaylist == playlist && DetailTitle != null) DetailTitle.Text = playlist.Name;





		}





	}











	private async Task DeletePlaylistAsync(Playlist playlist)





	{





		ContentDialog contentDialog = new ContentDialog





		{





			Title = Models.Strings.Current.IsFr ? "Supprimer la playlist" : "Delete playlist",





			Content = Models.Strings.Current.IsFr ? $"Voulez-vous vraiment supprimer la playlist '{playlist.Name}' ?\nLes fichiers audio ne seront pas supprimés." : $"Are you sure you want to delete the playlist '{playlist.Name}'?\nThe audio files will not be deleted.",





			PrimaryButtonText = Models.Strings.Current.CS_Delete,





			CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,





			DefaultButton = ContentDialogButton.Close,





			XamlRoot = this.XamlRoot





		};





		if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)





		{





			await App.Cache.DeletePlaylistAsync(playlist.Id);





			_playlists.Remove(playlist);





			if (_openPlaylist == playlist)





			{





				DetailPanel.Visibility = Visibility.Collapsed;





				_openPlaylist = null;





			}





			await RefreshAsync();





		}





	}











	private async void AIGenerateBtn_Click(object sender, RoutedEventArgs e)





	{





		if (!App.Settings.Current.AIEnabled)





		{





			await new ContentDialog { Title = Models.Strings.Current.IsFr ? "Erreur" : "Error", Content = Models.Strings.Current.IsFr ? "L'IA n'est pas activée. Veuillez l'activer dans les paramètres." : "AI is not enabled. Please enable it in the settings.", CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





			return;





		}





		try





		{





			List<Track> alltracks = await App.Cache.LoadAllTracksAsync();





			var promptParts = AIService.GeneratePlaylistPromptParts("... (VOTRE DEMANDE ICI)", alltracks.ToList());





			string responseJson = await AIHelper.ShowManualAIDialog(xamlRoot: this.XamlRoot, title: "GÃƒÂ©nÃƒÂ©rations de Playlist IA", instructions: promptParts.Instructions, hiddenData: promptParts.JsonData);





			await Task.Delay(800);





			if (string.IsNullOrWhiteSpace(responseJson)) return;





			AIService.AIPlaylistResult result = AIService.ParsePlaylistResponse(responseJson);





			if (result != null && result.TrackIds.Count > 0)





			{





				List<Track> selectedtracks = alltracks.Where(t => result.TrackIds.Contains(t.Id)).ToList();





				string pName = string.IsNullOrWhiteSpace(result.PlaylistName) ? "Playlist IA" : result.PlaylistName;





				if (selectedtracks.Count > 0)





				{





					await createPlaylist(pName, selectedtracks);





					await new ContentDialog { Content = Models.Strings.Current.IsFr ? $"La playlist '{pName}' a été créée avec {selectedtracks.Count} chansons !" : $"Playlist '{pName}' was created with {selectedtracks.Count} songs !", CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





				}





				else





				{





					await new ContentDialog { Title = Models.Strings.Current.IsFr ? "Erreur" : "Error", Content = Models.Strings.Current.IsFr ? "L'IA a généré une playlist mais aucune chanson correspondante n'a été trouvée." : "The AI generated a playlist but no corresponding songs were found.", CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





				}





			}





			else





			{





				await new ContentDialog { Title = Models.Strings.Current.IsFr ? "Erreur" : "Error", Content = Models.Strings.Current.IsFr ? "La réponse de l'IA ne contient aucune chanson valide." : "The AI response does not contain any valid songs.", CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





			}





		}





		catch (Exception ex)





		{





			await new ContentDialog { Title = Models.Strings.Current.IsFr ? "Erreur IA" : "AI Error", Content = ex.Message, CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





		}





	}











	private void PlaylistsGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)





	{





		if ((e.OriginalSource as FrameworkElement)?.DataContext is Playlist playlist)





		{





			e.Handled = true;





			List<Playlist> selectedPlaylists = PlaylistsGrid.SelectedItems





				.Select(item => (item as FrameworkElement)?.DataContext as Playlist)





				.Where(p => p != null)





				.ToList();











			if (!selectedPlaylists.Contains(playlist))





			{





				PlaylistsGrid.SelectedItems.Clear();





				var container = PlaylistsGrid.Items.Cast<FrameworkElement>().FirstOrDefault(x => x.DataContext == playlist);





				if (container != null) PlaylistsGrid.SelectedItems.Add(container);





				selectedPlaylists = new List<Playlist> { playlist };





			}











			MenuFlyout menuFlyout = new MenuFlyout();











			if (selectedPlaylists.Count <= 1)





			{





				var playItem = new MenuFlyoutItem { Text = Models.Strings.Current.PlaylistDetailPage_Text_Toutlire, Icon = new FontIcon { Glyph = "\ue768" } };





				playItem.Click += (s, args) =>





				{





					List<Track> tracks = ResolvePlaylisttracks(playlist);





					if (tracks.Count > 0)





						App.MainWindowInstance?.PlayTrack(tracks[0], tracks);





				};











				var renameItem = new MenuFlyoutItem { Text = Models.Strings.Current.CS_Rename, Icon = new FontIcon { Glyph = "\ue70f" } };





				renameItem.Click += async (s, args) => await RenamePlaylistAsync(playlist, null); // passing null will require slight modify if name is needed











				var chooseCoverItem = new MenuFlyoutItem { Text = Models.Strings.Current.IsFr ? "Choisir une pochette locale..." : "Choose local cover...", Icon = new FontIcon { Glyph = "\ue8b9" } };





				chooseCoverItem.Click += async (s, args) => await ChooseLocalCoverAsync(playlist);











				var searchCoverItem = new MenuFlyoutItem { Text = Resona.Models.Strings.Current.CS_Rechercherunepochett, Icon = new FontIcon { Glyph = "\ue721" } };





				searchCoverItem.Click += async (s, args) => await SearchOnlineCoverAsync(playlist);











				menuFlyout.Items.Add(playItem);





				menuFlyout.Items.Add(renameItem);





				menuFlyout.Items.Add(new MenuFlyoutSeparator());





				menuFlyout.Items.Add(chooseCoverItem);





				menuFlyout.Items.Add(searchCoverItem);





				menuFlyout.Items.Add(new MenuFlyoutSeparator());





			}











			var deleteItem = new MenuFlyoutItem { Text = Models.Strings.Current.CS_Delete, Icon = new FontIcon { Glyph = "\ue74d" } };





			deleteItem.Click += async (s, args) =>





			{





				if (selectedPlaylists.Count > 1)





				{





					ContentDialog contentDialog = new ContentDialog





					{





						Title = Models.Strings.Current.IsFr ? "Supprimer les playlists" : "Delete playlists",





						Content = Models.Strings.Current.IsFr ? $"Voulez-vous vraiment supprimer les {selectedPlaylists.Count} playlists sélectionnées ?\nLes fichiers audio ne seront pas supprimés." : $"Are you sure you want to delete the {selectedPlaylists.Count} selected playlists?\nThe audio files will not be deleted.",





						PrimaryButtonText = Models.Strings.Current.CS_Delete,





						CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,





						DefaultButton = ContentDialogButton.Close,





						XamlRoot = this.XamlRoot





					};





					if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)





					{





						foreach (var pl in selectedPlaylists)





						{





							await App.Cache.DeletePlaylistAsync(pl.Id);





							_playlists.Remove(pl);





							if (_openPlaylist == pl)





							{





								DetailPanel.Visibility = Visibility.Collapsed;





								_openPlaylist = null;





							}





						}





						await RefreshAsync();





					}





				}





				else if (selectedPlaylists.Count == 1)





				{





					await DeletePlaylistAsync(selectedPlaylists[0]);





				}





			};











			menuFlyout.Items.Add(deleteItem);











			menuFlyout.ShowAt(PlaylistsGrid, e.GetPosition(PlaylistsGrid));





		}





	}











	private async Task ChooseLocalCoverAsync(Playlist playlist)





	{





		var picker = new Windows.Storage.Pickers.FileOpenPicker { ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail };





		var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);





		WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);





		picker.FileTypeFilter.Add(".jpg");





		picker.FileTypeFilter.Add(".png");





		picker.FileTypeFilter.Add(".jpeg");











		var file = await picker.PickSingleFileAsync();





		if (file != null)





		{





			string appFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona");





			string destFolder = System.IO.Path.Combine(appFolder, "PlaylistCovers");





			System.IO.Directory.CreateDirectory(destFolder);





			





			string ext = System.IO.Path.GetExtension(file.Path);





			string destPath = System.IO.Path.Combine(destFolder, $"{playlist.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}");





			





			System.IO.File.Copy(file.Path, destPath, true);





			





			if (!string.IsNullOrEmpty(playlist.CoverImagePath) && System.IO.File.Exists(playlist.CoverImagePath))





			{





				try { System.IO.File.Delete(playlist.CoverImagePath); } catch { }





			}











			playlist.CoverImagePath = destPath;





			await App.Cache.UpsertPlaylistAsync(playlist);





			await RefreshAsync();





		}





	}











	private async Task SearchOnlineCoverAsync(Playlist playlist)





	{





		var textBox = new TextBox { PlaceholderText = "Terme de recherche (ex: Nom de l'album)", Text = playlist.Name };





		var dialog = new ContentDialog





		{





			Title = Models.Strings.Current.IsFr ? "Rechercher une pochette" : "Search cover",





			Content = textBox,





			PrimaryButtonText = Models.Strings.Current.IsFr ? "Rechercher" : "Search",





			CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,





			DefaultButton = ContentDialogButton.Primary,





			XamlRoot = this.XamlRoot





		};











		if (await dialog.ShowAsync() == ContentDialogResult.Primary)





		{





			string query = textBox.Text.Trim();





			if (string.IsNullOrEmpty(query)) return;





			





			var loadingDialog = new ContentDialog





			{





				Title = Models.Strings.Current.IsFr ? "Recherche en cours" : "Searching",





				Content = new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center },





				XamlRoot = this.XamlRoot





			};





			_ = loadingDialog.ShowAsync();











			List<string> options = new();





			try





			{





				options = await App.CoverArt.SearchGoogleImagesAsync(query + (query.EndsWith(" cover", StringComparison.OrdinalIgnoreCase) ? "" : " cover"), 12);





			}





			catch { }





			





			loadingDialog.Hide();





			





			if (options.Count > 0)





			{





				GridView imageGrid = new GridView





				{





					SelectionMode = ListViewSelectionMode.Single,





					MaxHeight = 400





				};





				foreach (var url in options)





				{





					imageGrid.Items.Add(new Image { Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(url)), Width=150, Height=150, Margin=new Thickness(5) });





				}





				imageGrid.SelectedIndex = 0;











				var applyDialog = new ContentDialog





				{





					Title = Resona.Models.Strings.Current.CS_Choisirunepochette,





					Content = imageGrid,





					PrimaryButtonText = Resona.Models.Strings.Current.CS_Appliquer,





					CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,





					XamlRoot = this.XamlRoot





				};











				if (await applyDialog.ShowAsync() == ContentDialogResult.Primary)





				{





					try





					{





						var selectedImg = imageGrid.SelectedItem as Image;





						if (selectedImg != null)





						{





							string artworkUrl = ((Microsoft.UI.Xaml.Media.Imaging.BitmapImage)selectedImg.Source).UriSource.ToString();





							var http = new System.Net.Http.HttpClient();





							byte[] data = await http.GetByteArrayAsync(artworkUrl);





							string appFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona");





							string destFolder = System.IO.Path.Combine(appFolder, "PlaylistCovers");





							System.IO.Directory.CreateDirectory(destFolder);





							string destPath = System.IO.Path.Combine(destFolder, $"{playlist.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg");





							System.IO.File.WriteAllBytes(destPath, data);











							if (!string.IsNullOrEmpty(playlist.CoverImagePath) && System.IO.File.Exists(playlist.CoverImagePath))





							{





								try { System.IO.File.Delete(playlist.CoverImagePath); } catch { }





							}











							playlist.CoverImagePath = destPath;





							await App.Cache.UpsertPlaylistAsync(playlist);





							await RefreshAsync();





						}





					}





					catch { }





				}





			}





			else





			{





				await new ContentDialog { Title = Resona.Models.Strings.Current.CS_Aucunrsultat, Content = Models.Strings.Current.IsFr ? "Aucune pochette n'a \u00E9t\u00E9 trouv\u00E9e pour ce terme." : "No cover found for this term.", CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();





			}





		}





	}











	private async Task createPlaylist(string name, List<Track> tracks)





	{





		Playlist playlist = new Playlist





		{





			Name = name,





			TrackIds = tracks.Select(t => t.Id).ToList()





		};





		await App.Cache.UpsertPlaylistAsync(playlist);





		_playlists.Add(playlist);





		await RefreshAsync();





	}











	private void CoverGrid_PointerEntered(object sender, PointerRoutedEventArgs e)





	{





		if (sender is Grid coverGrid)





		{





			Helpers.AnimationHelper.ApplyBouncyScale(coverGrid, 1.05f);





			UpdateCoverIndicator(coverGrid, true, true);





		}





	}











	private void CoverGrid_PointerExited(object sender, PointerRoutedEventArgs e)





	{





		if (sender is Grid coverGrid)





		{





			Helpers.AnimationHelper.ApplyBouncyScale(coverGrid, 1.0f);





			UpdateCoverIndicator(coverGrid, false, false);





		}





	}











	private void StartBarsAnimation(Grid coverGrid)





	{





		_activeIndicatorGrid = coverGrid;





	}











	private void UpdateCoverIndicator(Grid coverGrid, bool isPlaying, bool isHovered)





	{





		var border = FindChildByName<Border>(coverGrid, "PlayOverlay");





		var fontIcon = FindChildByName<FontIcon>(coverGrid, "OverlayIcon");





		if (border == null) return;





		bool flag = isPlaying && App.AudioEngine.State != NAudio.Wave.PlaybackState.Paused;





		if (isHovered)





		{





			border.Visibility = Visibility.Visible;





			if (fontIcon != null) fontIcon.Glyph = flag ? "\ue769" : "\ue768";





		}





		else





		{





			border.Visibility = Visibility.Collapsed;





			if (flag) StartBarsAnimation(coverGrid);





		}





	}











	private void TrackListView_SelectionChanged(object sender, SelectionChangedEventArgs e)





	{





		if (sender is ListView listView)





		{





			foreach (var removedItem in e.RemovedItems)





			{





				if (listView.ContainerFromItem(removedItem) is ListViewItem parent)





				{





					var border = FindChildByName<Border>(parent, "SelectionBg");





					if (border != null) border.Opacity = 0.0;





					var border2 = FindChildByName<Border>(parent, "SelectionOverlay");





					if (border2 != null) border2.Opacity = 0.0;





				}





			}





			bool ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;





			bool shift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;





			if (!ctrl && !shift && listView.SelectedItems.Count == 1)





			{





				base.DispatcherQueue.TryEnqueue(() =>





				{





					if (listView.SelectedItems.Count == 1) listView.SelectedItems.Clear();





				});





				return;





			}





			bool flag = listView.SelectedItems.Count > 1;





			foreach (var selectedItem in listView.SelectedItems)





			{





				if (listView.ContainerFromItem(selectedItem) is ListViewItem parent2)





				{





					var border3 = FindChildByName<Border>(parent2, "SelectionBg");





					if (border3 != null) border3.Opacity = 0.15;





					var border4 = FindChildByName<Border>(parent2, "SelectionOverlay");





					if (border4 != null) border4.Opacity = 1.0;





				}





			}





		}





	}











	private void TrackRow_PointerExited(object sender, PointerRoutedEventArgs e)





	{





		if (sender is FrameworkElement frameworkElement)





		{





			if (frameworkElement.FindName("HoverBg") is Border border)





			{





				border.Opacity = 0.0;





			}





			if (frameworkElement.FindName("HoverAccentBar") is Microsoft.UI.Xaml.Shapes.Rectangle rectangle)





			{





				rectangle.Opacity = 0.0;





			}





		}





	}











	private void TrackListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)





	{





		if ((e.OriginalSource as FrameworkElement)?.DataContext is Track track)





		{





			App.MainWindowInstance?.PlayTrack(track, _opentracks);





			TrackListView?.SelectedItems.Clear();





		}





	}











	private void TrackListView_RightTapped(object sender, RightTappedRoutedEventArgs e)





	{





		if ((e.OriginalSource as FrameworkElement)?.DataContext is Track track)





		{





			e.Handled = true;





			List<Track> selectedTracks = TrackListView.SelectedItems.Cast<Track>().ToList();





			MenuFlyout menuFlyout = App.MainWindowInstance?.BuildTrackMenu(track, _opentracks.ToList(), selectedTracks);





			if (menuFlyout != null)





			{





				MenuFlyoutItem removeFileItem = new MenuFlyoutItem { Text = Models.Strings.Current.IsFr ? "Supprimer de la playlist" : "Remove from playlist", Icon = new FontIcon { Glyph = "\ue107" } };





				removeFileItem.Click += async (s, args) => 





				{





					if (_openPlaylist != null)





					{





						_openPlaylist.TrackIds.Remove(track.Id);





						await App.Cache.UpsertPlaylistAsync(_openPlaylist);





						_opentracks.Remove(track);





						DisplayedTracks.Remove(track);





						DetailCount.Text = Resona.Models.Strings.Current.FormatTracksCount(_opentracks.Count);





					}





				};





				menuFlyout.Items.Add(new MenuFlyoutSeparator());





				menuFlyout.Items.Add(removeFileItem);





				menuFlyout.ShowAt(TrackListView, e.GetPosition(TrackListView));





			}





		}





	}











	private void TrackListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)





	{





		if (args.InRecycleQueue) return;





		if (args.ItemContainer is ListViewItem item)





		{





			Track track = args.Item as Track;





			bool isSelected = sender.SelectedItems.Contains(track);





			bool isHovered = false; // Simplified





			Border selectionBg = FindChildByName<Border>(item, "SelectionBg");





			Border selectionOverlay = FindChildByName<Border>(item, "SelectionOverlay");





			if (selectionBg != null) selectionBg.Opacity = isSelected ? 0.15 : 0.0;





			if (selectionOverlay != null) selectionOverlay.Opacity = isSelected ? 1.0 : 0.0;





		}





	}











	private void TrackRow_PointerEntered(object sender, PointerRoutedEventArgs e)





	{





		if (sender is FrameworkElement frameworkElement)





		{





			if (frameworkElement.FindName("HoverBg") is Border border) border.Opacity = 0.05;





			if (frameworkElement.FindName("HoverAccentBar") is Microsoft.UI.Xaml.Shapes.Rectangle rectangle) rectangle.Opacity = 1.0;





		}





	}











	private void PlayOverlay_Tapped(object sender, TappedRoutedEventArgs e)





	{





		if (sender is Border border && border.DataContext is Track track)





		{





			e.Handled = true;





			App.MainWindowInstance?.PlayTrack(track, _opentracks);





		}





	}











	private static T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement





	{





		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);





		for (int i = 0; i < childrenCount; i++)





		{





			DependencyObject child = VisualTreeHelper.GetChild(parent, i);





			if (child is T val && val.Name == name)





			{





				return val;





			}





			T val2 = FindChildByName<T>(child, name);





			if (val2 != null)





			{





				return val2;





			}





		}





		return null;





	


    


}


}



























































































































































