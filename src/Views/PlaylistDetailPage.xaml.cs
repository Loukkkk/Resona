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

public sealed partial class PlaylistDetailPage : Page
{
	private Playlist _playlist;

	private List<Track> _tracks = new List<Track>();

	private List<Track> _librarySnapshot = new List<Track>();

	private Grid? _activeIndicatorGrid;

	private DispatcherTimer? _rmsTimer;

	private float _smoothedRms;
	public ObservableCollection<Track> DisplayedTracks { get; } = new ObservableCollection<Track>();

	public PlaylistDetailPage()
	{
		InitializeComponent();
		StartRmsTimer();
		base.Loaded += delegate
		{
			MainWindow.GlobalClickOutside += OnGlobalClickOutside;
		};
		base.Unloaded += delegate
		{
			MainWindow.GlobalClickOutside -= OnGlobalClickOutside;
		};
	}

	private void OnGlobalClickOutside()
	{
		base.DispatcherQueue.TryEnqueue(delegate
		{
			TrackListView?.SelectedItems.Clear();
		});
	}

	private void StartRmsTimer()
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
		BackButton.Visibility = Visibility.Visible;
		if (!(e.Parameter is ITuple { Length: 2 } tuple) || !(tuple[0] is Playlist playlist))
		{
			return;
		}
		object obj = tuple[1];
		List<Track> lib = obj as List<Track>;
		if (lib == null)
		{
			return;
		}
		_playlist = playlist;
		_librarySnapshot = lib;
		_tracks = (from id in playlist.TrackIds
			select lib.FirstOrDefault((Track t) => t.Id == id) into t
			where t != null
			select t).Cast<Track>().ToList();
		BuildUI();
	}

	protected override void OnNavigatedFrom(NavigationEventArgs e)
	{
		base.OnNavigatedFrom(e);
		_rmsTimer?.Stop();
		_rmsTimer = null;
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

	public void SetPlaylistContext(Playlist playlist, List<Track> tracks)
	{
		_playlist = playlist;
		_tracks = tracks;
		PlaylistName.Text = _playlist.Name;
		PlaylistCount.Text = ((_tracks.Count == 0) ? "" : $"♪ {Resona.Models.Strings.Current.FormatTracksCount(_tracks.Count)}");
		Grid child = BuildCoverMosaic(_tracks, 120.0, 120.0);
		HeaderCover.Child = child;
		
		UpdateDisplayedTracks(_tracks);
	}

	private void BuildUI()
	{
		PlaylistName.Text = _playlist.Name;
		PlaylistCount.Text = ((_tracks.Count == 0) ? "" : $"♪ {Resona.Models.Strings.Current.FormatTracksCount(_tracks.Count)}");
		Grid child = BuildCoverMosaic(_tracks, 120.0, 120.0);
		HeaderCover.Child = child;
		DisplayedTracks.Clear();
		if (_tracks.Count <= 0)
		{
			return;
		}
		foreach (Track track in _tracks)
		{
			DisplayedTracks.Add(track);
		}
	}

	private void PlayAllBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_tracks.Count > 0)
		{
			App.MainWindowInstance?.PlayTrack(_tracks[0], _tracks);
			App.MainWindowInstance?.EnableContinuousPlaybackIfOff();
		}
	}

	private async void ExportBtn_Click(object sender, RoutedEventArgs e)
	{
		await ExportAsync();
	}

	private async void RenameBtn_Click(object sender, RoutedEventArgs e)
	{
		await RenameAsync();
	}

	private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
	{
		await DeleteAsync();
	}

	private void UpdateCoverIndicator(Grid coverGrid, bool isPlaying, bool isHovered)
	{
		Border border = FindChildByName<Border>(coverGrid, "PlayOverlay");
		FontIcon fontIcon = FindChildByName<FontIcon>(coverGrid, "OverlayIcon");
		if (border == null)
		{
			return;
		}
		bool flag = isPlaying && App.AudioEngine.State != NAudio.Wave.PlaybackState.Paused;
		if (isHovered)
		{
			border.Visibility = Visibility.Visible;
			if (fontIcon != null)
			{
				fontIcon.Glyph = (flag ? "\ue769" : "\ue768");
			}
		}
		else
		{
			border.Visibility = Visibility.Collapsed;
			if (flag)
			{
				StartBarsAnimation(coverGrid);
			}
		}
	}

	private void StartBarsAnimation(Grid coverGrid)
	{
		_activeIndicatorGrid = coverGrid;
	}

	private void CoverGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Grid grid)
		{
			Track track = grid.DataContext as Track;
			UpdateCoverIndicator(grid, track?.IsPlaying ?? false, isHovered: true);
			if (track != null)
			{
				App.AudioEngine.PrewarmOpus(track.FilePath, track.Duration);
			}
		}
	}

	private void CoverGrid_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Grid grid)
		{
			UpdateCoverIndicator(grid, (grid.DataContext as Track)?.IsPlaying ?? false, isHovered: false);
		}
	}

	private void PlayOverlay_Tapped(object sender, TappedRoutedEventArgs e)
	{
		if (MainWindow.LastClickWasXButton) { MainWindow.LastClickWasXButton = false; return; }
		if (sender is FrameworkElement { DataContext: Track dataContext } frameworkElement)
		{
			if (dataContext.IsPlaying)
			{
				App.MainWindowInstance?.TogglePlayPause();
			}
			else
			{
				App.MainWindowInstance?.PlayTrack(dataContext, _tracks);
			}
			if (frameworkElement is Border && frameworkElement.Parent is Grid coverGrid)
			{
				UpdateCoverIndicator(coverGrid, dataContext.IsPlaying, isHovered: true);
			}
		}
	}

	private void TrackListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if ((e.OriginalSource as FrameworkElement)?.DataContext is Track track)
		{
			App.MainWindowInstance?.PlayTrack(track, _tracks);
			TrackListView?.SelectedItems.Clear();
		}
	}

	private void TrackListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		if ((e.OriginalSource as FrameworkElement)?.DataContext is Track track)
		{
			e.Handled = true;
			List<Track> selectedTracks = TrackListView.SelectedItems.Cast<Track>().ToList();
			MenuFlyout menuFlyout = App.MainWindowInstance?.BuildTrackMenu(track, _tracks, selectedTracks);
			if (menuFlyout != null)
			{
				menuFlyout.ShowAt(TrackListView, e.GetPosition(TrackListView));
			}
		}
	}

	private void TrackRow_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		TrackListView?.Focus(FocusState.Pointer);
	}

	private void TrackRow_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (!(sender is FrameworkElement frameworkElement))
		{
			return;
		}
		Track track = frameworkElement.DataContext as Track;
		if (track == null || !track.IsPlaying)
		{
			if (frameworkElement.FindName("HoverBg") is Border border)
			{
				border.Opacity = 0.25;
			}
			if (frameworkElement.FindName("HoverAccentBar") is Rectangle rectangle)
			{
				rectangle.Opacity = 1.0;
			}
		}
		if (track != null)
		{
			App.AudioEngine.PrewarmOpus(track.FilePath, track.Duration);
		}
	}

	private void TrackListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
	{
		if (args.InRecycleQueue)
		{
			return;
		}
		ListViewItem listViewItem = args.ItemContainer as ListViewItem;
		if (listViewItem == null)
		{
			return;
		}
		Track track = args.Item as Track;
		bool flag = ((ListView)sender).SelectedItems.Count > 1;
		Border border = FindChildByName<Border>(listViewItem, "SelectionBg");
		if ((object)border != null)
		{
			border.Opacity = ((listViewItem.IsSelected && flag) ? 0.15 : 0.0);
		}
		Border border2 = FindChildByName<Border>(listViewItem, "SelectionOverlay");
		if ((object)border2 != null)
		{
			border2.Opacity = ((listViewItem.IsSelected && flag) ? 1.0 : 0.0);
		}
		if (track != null && track.IsPlaying)
		{
			Grid grid = FindChildByName<Grid>(listViewItem, "ItemRootGrid")?.Children.FirstOrDefault() as Grid;
			if (grid != null)
			{
				_activeIndicatorGrid = grid;
			}
		}
	}

	private void TrackListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		ListView listView = sender as ListView;
		if ((object)listView == null)
		{
			return;
		}
		foreach (object removedItem in e.RemovedItems)
		{
			if (listView.ContainerFromItem(removedItem) is ListViewItem parent)
			{
				Border border = FindChildByName<Border>(parent, "SelectionBg");
				if ((object)border != null)
				{
					border.Opacity = 0.0;
				}
				Border border2 = FindChildByName<Border>(parent, "SelectionOverlay");
				if ((object)border2 != null)
				{
					border2.Opacity = 0.0;
				}
			}
		}
		bool flag = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
		bool flag2 = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
		if (!flag && !flag2 && listView.SelectedItems.Count == 1)
		{
			base.DispatcherQueue.TryEnqueue(delegate
			{
				if (listView.SelectedItems.Count == 1)
				{
					listView.SelectedItems.Clear();
				}
			});
			return;
		}
		foreach (object selectedItem in listView.SelectedItems)
		{
			if (listView.ContainerFromItem(selectedItem) is ListViewItem parent2)
			{
				Border border3 = FindChildByName<Border>(parent2, "SelectionBg");
				if ((object)border3 != null)
				{
					border3.Opacity = 0.15;
				}
				Border border4 = FindChildByName<Border>(parent2, "SelectionOverlay");
				if ((object)border4 != null)
				{
					border4.Opacity = 1.0;
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
			if (frameworkElement.FindName("HoverAccentBar") is Rectangle rectangle)
			{
				rectangle.Opacity = 0.0;
			}
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
			BitmapImage bitmap = CoverCacheService.GetBitmap(list[0], (int)w);
			grid.Children.Add(new Image
			{
				Source = bitmap,
				Stretch = Stretch.UniformToFill,
				Width = w,
				Height = h
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
			BitmapImage bitmap2 = CoverCacheService.GetBitmap(list[num3], (int)num);
			Image image = new Image
			{
				Source = bitmap2,
				Stretch = Stretch.UniformToFill,
				Width = num,
				Height = num2
			};
			Grid.SetColumn(image, array[num3]);
			Grid.SetRow(image, array2[num3]);
			grid.Children.Add(image);
		}
		return grid;
	}

	private async Task ExportAsync()
	{
		if (_tracks.Count == 0)
		{
			await new ContentDialog
			{
				Title = "Playlist vide",
				Content = Resona.Models.Strings.Current.IsFr ? "Aucune piste à exporter." : "No tracks to export.",
				CloseButtonText = "OK",
				XamlRoot = base.XamlRoot
			}.ShowAsync();
			return;
		}
		FileSavePicker fileSavePicker = new FileSavePicker();
		InitializeWithWindow.Initialize(fileSavePicker, WindowNative.GetWindowHandle(App.MainWindowInstance));
		fileSavePicker.FileTypeChoices.Add("Playlist M3U8 (UTF-8)", new List<string> { ".m3u8" });
		fileSavePicker.SuggestedFileName = _playlist.Name;
		StorageFile storageFile = await fileSavePicker.PickSaveFileAsync();
		if (storageFile != null)
		{
			await App.PlaylistIO.ExportAsync(storageFile.Path, _tracks, useRelativePaths: false, _playlist.CoverImagePath);
		}
	}

	private async Task RenameAsync()
	{
		TextBox nameBox = new TextBox
		{
			Text = _playlist.Name
		};
		ContentDialog contentDialog = new ContentDialog();
		contentDialog.Title = Models.Strings.Current.IsFr ? "Renommer" : "Rename";
		contentDialog.PrimaryButtonText = "OK";
		contentDialog.CloseButtonText = Resona.Models.Strings.Current.CS_Annuler;
		contentDialog.DefaultButton = ContentDialogButton.Primary;
		contentDialog.Content = nameBox;
		contentDialog.XamlRoot = base.XamlRoot;
		contentDialog.Opened += delegate
		{
			nameBox.Focus(FocusState.Programmatic);
			nameBox.SelectAll();
		};
		if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
		{
			string text = nameBox.Text.Trim();
			if (!string.IsNullOrEmpty(text))
			{
				_playlist.Name = text;
				_playlist.DateModified = DateTime.UtcNow;
				await App.Cache.UpsertPlaylistAsync(_playlist);
				BuildUI();
			}
		}
	}

			private async Task DeleteAsync()
	{
		if (await new ContentDialog
		{
			Title = Models.Strings.Current.IsFr ? "Supprimer la playlist" : "Delete playlist",
			Content = Models.Strings.Current.IsFr ? "Supprimer \u00AB " + _playlist.Name + " \u00BB ? Les fichiers audio ne sont pas supprim\u00E9s." : "Delete \u00AB " + _playlist.Name + " \u00BB? Audio files will not be deleted.",
			PrimaryButtonText = Models.Strings.Current.IsFr ? "Supprimer" : "Delete",
			CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,
			DefaultButton = ContentDialogButton.Close,
			XamlRoot = base.XamlRoot
		}.ShowAsync() == ContentDialogResult.Primary)
		{
			await App.Cache.DeletePlaylistAsync(_playlist.Id);
			App.MainWindowInstance?.RestoreSidebarSelection();
		}
	}

	private void BackButton_Click(object sender, RoutedEventArgs e)
	{
		App.MainWindowInstance?.RestoreSidebarSelection();
	}



    private void TrackTitle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.Tag is Resona.Models.Track track)
        {
            App.MainWindowInstance?.ShowTrackInfo(track);
        }
    }

    private void Artist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.Tag is string artist && !string.IsNullOrWhiteSpace(artist))
        {
            App.MainWindowInstance?.NavigateToArtist(artist);
        }
    }

    private void Album_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.Tag is string album && !string.IsNullOrWhiteSpace(album))
        {
            App.MainWindowInstance?.NavigateToAlbum(album);
        }
    }
}










