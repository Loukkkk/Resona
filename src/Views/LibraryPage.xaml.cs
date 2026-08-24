using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Resona.Models;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NAudio.Wave;
using WinRT;
using Windows.System;
using Windows.UI.Core;

namespace Resona.Views;

public sealed partial class LibraryPage : Page
{
	private List<Track> _allTracks = new List<Track>();

	private bool _isLoadingCombo = true;

	private string _currentSort = "artist_asc";

	private string _collectionTitle = Resona.Models.Strings.Current.LibraryPage_Text_BIBLIOTHQUE;

	private string? _collectionSubtitle;

	private int _currentPage;

	private int _totalPages = 1;

	private DispatcherTimer? _rmsTimer;

	private Grid? _activeIndicatorGrid;

	private float _smoothedRms;

	private List<Track> _currentFilteredList = new List<Track>();

	public ObservableCollection<Track> FilteredTracks { get; } = new ObservableCollection<Track>();

	public LibraryPage()
	{
		InitializeComponent();
		_currentSort = App.Settings.Current.LibrarySort;
		RefreshHeaderText(); UpdateSortButtonText();
		LoadDisplayLimitSelection();
		StartRmsTimer();
		if (TrackListView != null)
		{
			TrackListView.PointerPressed += delegate(object s, PointerRoutedEventArgs e)
			{
				if (e.OriginalSource is FrameworkElement frameworkElement && !(frameworkElement.DataContext is Track))
				{
					TrackListView.SelectedItems.Clear();
				}
			};
		}
		base.Loaded += delegate
		{
			MainWindow.GlobalClickOutside += OnGlobalClickOutside;
		};
		base.Unloaded += delegate
		{
			MainWindow.GlobalClickOutside -= OnGlobalClickOutside;
		};

		// Refresh ComboBox displayed text when language changes
		Resona.Models.Strings.Current.PropertyChanged += (s, e) =>
		{
			DispatcherQueue.TryEnqueue(() =>
			{
				if (DisplayLimitCombo.SelectedItem != null)
				{
					var sel = DisplayLimitCombo.SelectedItem;
					DisplayLimitCombo.SelectedItem = null;
					DisplayLimitCombo.SelectedItem = sel;
				}
				UpdateTotalTracksHint();
			});
		};
	}

	private void OnGlobalClickOutside()
	{
		base.DispatcherQueue.TryEnqueue(delegate
		{
			TrackListView?.SelectedItems.Clear();
		});
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
		bool num = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
		bool flag = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
		if (!num && !flag && listView.SelectedItems.Count == 1)
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

	private void SyncNowPlayingId()
	{
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);
		SyncNowPlayingId();
		if (e.Parameter is ValueTuple<string, string, List<Track>> tuple)
		{
			_collectionTitle = tuple.Item1;
			_collectionSubtitle = tuple.Item2;
			BackButton.Visibility = !string.IsNullOrEmpty(_collectionSubtitle) ? Visibility.Visible : Visibility.Collapsed;
			RefreshHeaderText(); UpdateSortButtonText();
			SetTracks(tuple.Item3);
		}
		else if (_allTracks.Count > 0 && FilteredTracks.Count == 0)
		{
			_currentPage = 0;
			ApplyFilter(SearchBox?.Text ?? string.Empty);
			UpdateTotalTracksHint();
		}
	}

	protected override void OnNavigatedFrom(NavigationEventArgs e)
	{
		base.OnNavigatedFrom(e);
		FilteredTracks.Clear();
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

	private void LoadDisplayLimitSelection()
	{
		int libraryDisplayLimit = App.Settings.Current.LibraryDisplayLimit;
		foreach (ComboBoxItem item in DisplayLimitCombo.Items)
		{
			if (item.Tag?.ToString() == libraryDisplayLimit.ToString())
			{
				DisplayLimitCombo.SelectedItem = item;
				break;
			}
		}
		if (DisplayLimitCombo.SelectedItem == null)
		{
			DisplayLimitCombo.SelectedIndex = 0;
		}
		_isLoadingCombo = false;
	}

	public void ResetToLibrary(List<Track> fullLibrary)
	{
		_collectionTitle = Resona.Models.Strings.Current.LibraryPage_Text_BIBLIOTHQUE;
		_collectionSubtitle = "";
		BackButton.Visibility = Visibility.Collapsed;
		RefreshHeaderText();
		SetTracks(fullLibrary);
	}

	public void ShowCollection(string title, string? subtitle, List<Track> tracks)
	{
		_collectionTitle = title;
		_collectionSubtitle = subtitle ?? "";
		BackButton.Visibility = Visibility.Visible;
		RefreshHeaderText();
		SetTracks(tracks);
	}

	public void SetTracks(List<Track> tracks)
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		_allTracks = tracks.Where((Track t) => seen.Add(t.FilePath)).ToList();
		foreach (Track allTrack in _allTracks)
		{
			allTrack.IsPlaying = !string.IsNullOrEmpty(App.NowPlayingFilePath) && string.Equals(allTrack.FilePath, App.NowPlayingFilePath, StringComparison.OrdinalIgnoreCase);
		}
		_currentPage = 0;
		ApplyFilter(SearchBox?.Text ?? string.Empty);
		UpdateTotalTracksHint();
	}

	public void SetCollectionContext(string title, List<Track> tracks, string? subtitle = null)
	{
		_collectionTitle = title;
		_collectionSubtitle = subtitle;
		RefreshHeaderText(); UpdateSortButtonText();
		SetTracks(tracks);
	}

	
	private void UpdateSortButtonText()
	{
		string sortName = _currentSort switch
		{
			"title_asc" => Resona.Models.Strings.Current.LibraryPage_Text_TitreAgtZ,
			"artist_asc" => Resona.Models.Strings.Current.LibraryPage_Text_ArtisteAgtZ,
			"album_asc" => Resona.Models.Strings.Current.LibraryPage_Text_AlbumAgtZ,
			"duration_asc" => Resona.Models.Strings.Current.LibraryPage_Text_Durecroissant,
			"duration_desc" => Resona.Models.Strings.Current.LibraryPage_Text_Duredcroissant,
			"added_desc" => Resona.Models.Strings.Current.LibraryPage_Text_Ajoutrcentdabord,
			_ => Resona.Models.Strings.Current.LibraryPage_Text_ArtisteAgtZ
		};
		SortButtonLabel.Text = (Resona.Models.Strings.Current.IsFr ? "Trier : " : "Sort: ") + sortName;
	}
	private void RefreshHeaderText()
	{
		if (PageTitleText != null)
		{
			PageTitleText.Text = _collectionTitle;
		}
		if (PageSubtitleText != null)
		{
			PageSubtitleText.Text = (string.IsNullOrWhiteSpace(_collectionSubtitle) ? string.Empty : _collectionSubtitle);
			PageSubtitleText.Visibility = (string.IsNullOrWhiteSpace(_collectionSubtitle) ? Visibility.Collapsed : Visibility.Visible);
		}
		UpdateTotalTracksHint();
	}

	public void AppendTracks(List<Track> newTracks)
	{
		HashSet<string> existingPaths = new HashSet<string>(_allTracks.Select((Track t) => t.FilePath), StringComparer.OrdinalIgnoreCase);
		List<Track> list = newTracks.Where((Track t) => !existingPaths.Contains(t.FilePath)).ToList();
		if (list.Count != 0)
		{
			_allTracks.AddRange(list);
			ApplyFilter(SearchBox?.Text ?? string.Empty);
			UpdateTotalTracksHint();
		}
	}

	public void SetNowPlayingId(string? trackId, string? trackFilePath = null)
	{
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

	private void UpdateFilteredTracks(List<Track> newItems)
	{
		for (int i = 0; i < newItems.Count; i++)
		{
			if (i < FilteredTracks.Count) FilteredTracks[i] = newItems[i];
			else FilteredTracks.Add(newItems[i]);
		}
		while (FilteredTracks.Count > newItems.Count) FilteredTracks.RemoveAt(FilteredTracks.Count - 1);
	}

	private void ApplyFilter(string query)
	{
		int libraryDisplayLimit = App.Settings.Current.LibraryDisplayLimit;
		bool noFilter = string.IsNullOrWhiteSpace(query);
		IEnumerable<Track> enumerable = _allTracks.Where((Track t) => noFilter || t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || t.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) || t.Album.Contains(query, StringComparison.OrdinalIgnoreCase));
		List<Track> list = (_currentSort switch
		{
			"title_asc" => enumerable.OrderBy<Track, string>((Track t) => t.Title, StringComparer.OrdinalIgnoreCase), 
			"artist_asc" => enumerable.OrderBy<Track, string>((Track t) => t.Artist, StringComparer.OrdinalIgnoreCase).ThenBy((Track t) => t.Album).ThenBy((Track t) => t.TrackNumber), 
			"album_asc" => enumerable.OrderBy<Track, string>((Track t) => t.Album, StringComparer.OrdinalIgnoreCase).ThenBy((Track t) => t.TrackNumber), 
			"duration_asc" => enumerable.OrderBy((Track t) => t.Duration), 
			"duration_desc" => enumerable.OrderByDescending((Track t) => t.Duration), 
			"added_desc" => enumerable.OrderByDescending((Track t) => t.DateAdded), 
			_ => enumerable, 
		}).ToList();
		_currentFilteredList = list;
		if (libraryDisplayLimit > 0 && list.Count > libraryDisplayLimit)
		{
			_totalPages = (int)Math.Ceiling((double)list.Count / (double)libraryDisplayLimit);
			_currentPage = Math.Clamp(_currentPage, 0, _totalPages - 1);
			UpdateFilteredTracks(list.Skip(_currentPage * libraryDisplayLimit).Take(libraryDisplayLimit).ToList());
			PaginationPanel.Visibility = Visibility.Visible;
			PageIndicator.Text = $"{Resona.Models.Strings.Current.CS_Page} {_currentPage + 1} / {_totalPages}";
			PrevPageButton.IsEnabled = _currentPage > 0;
			NextPageButton.IsEnabled = _currentPage < _totalPages - 1;
			return;
		}
		_totalPages = 1;
		_currentPage = 0;
		UpdateFilteredTracks(list);
		PaginationPanel.Visibility = Visibility.Collapsed;
	}

	private void UpdateTotalTracksHint()
	{
		if (PageTitleText != null)
		{
			PageTitleText.Text = _collectionTitle;
		}
		TotalTracksHint.Text = ((_allTracks.Count > 0) ? Resona.Models.Strings.Current.FormatTracksCount(_allTracks.Count) : " ");
		if (PageSubtitleText != null)
		{
			PageSubtitleText.Text = (string.IsNullOrWhiteSpace(_collectionSubtitle) ? string.Empty : _collectionSubtitle);
			PageSubtitleText.Visibility = (string.IsNullOrWhiteSpace(_collectionSubtitle) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private async void PrevPage_Click(object sender, RoutedEventArgs e)
	{
		if (_currentPage > 0)
		{
			await Helpers.AnimationHelper.PlayFadeOutAsync(TrackListView, 150);
			_currentPage--;
			ApplyFilter(SearchBox.Text);
			Helpers.AnimationHelper.PlayFadeIn(TrackListView, 150);
		}
	}

	private async void NextPage_Click(object sender, RoutedEventArgs e)
	{
		if (_currentPage < _totalPages - 1)
		{
			await Helpers.AnimationHelper.PlayFadeOutAsync(TrackListView, 150);
			_currentPage++;
			ApplyFilter(SearchBox.Text);
			Helpers.AnimationHelper.PlayFadeIn(TrackListView, 150);
		}
	}

	private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		_currentPage = 0;
		ApplyFilter(SearchBox.Text);
	}

	private async void DisplayLimitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isLoadingCombo && DisplayLimitCombo.SelectedItem is ComboBoxItem { Tag: var tag } && int.TryParse(tag?.ToString(), out var result))
		{
			App.Settings.Current.LibraryDisplayLimit = result;
			await App.Settings.SaveAsync();
			_currentPage = 0;
			ApplyFilter(SearchBox.Text);
		}
	}

	private async void SortMenu_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuFlyoutItem)
		{
			_currentSort = menuFlyoutItem.Tag?.ToString() ?? "artist_asc";
			App.Settings.Current.LibrarySort = _currentSort; await App.Settings.SaveAsync(); UpdateSortButtonText();
			_currentPage = 0;
			ApplyFilter(SearchBox.Text);
		}
	}

	private void ShuffleAll_Click(object sender, RoutedEventArgs e)
	{
		if (_currentFilteredList.Count != 0)
		{
			Random random = new Random();
			Track track = _currentFilteredList[random.Next(_currentFilteredList.Count)];
			App.MainWindowInstance?.SetShuffleModeAndPlay(track, _currentFilteredList);
		}
	}

	private void TrackListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (!((e.OriginalSource as FrameworkElement)?.DataContext is Track track))
		{
			return;
		}
		ListViewItem listViewItem = TrackListView.ContainerFromItem(track) as ListViewItem;
		if (listViewItem != null)
		{
			Grid grid = FindChildByName<Grid>(listViewItem, "ItemRootGrid")?.Children[0] as Grid;
			if (grid != null)
			{
				_activeIndicatorGrid = grid;
				UpdateCoverIndicator(grid, isPlaying: true, isHovered: false);
			}
		}
		App.MainWindowInstance?.PlayTrack(track, _currentFilteredList);
		TrackListView?.SelectedItems.Clear();
	}

	private void TrackListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		if ((e.OriginalSource as FrameworkElement)?.DataContext is Track track)
		{
			e.Handled = true;
			List<Track> selectedTracks = TrackListView.SelectedItems.Cast<Track>().ToList();
			MenuFlyout menuFlyout = App.MainWindowInstance?.BuildTrackMenu(track, _currentFilteredList, selectedTracks);
			if (menuFlyout != null)
			{
				menuFlyout.ShowAt(TrackListView, e.GetPosition(TrackListView));
			}
		}
	}

	private async Task LoadMultiPlaylistSubItemsAsync(MenuFlyoutSubItem parent, List<Track> tracks)
	{
		try
		{
			foreach (Playlist item in await App.Cache.LoadAllPlaylistsAsync())
			{
				MenuFlyoutItem menuFlyoutItem = new MenuFlyoutItem
				{
					Text = item.Name
				};
				Playlist captured = item;
				List<string> ids = tracks.Select((Track t) => t.Id).ToList();
				menuFlyoutItem.Click += async delegate
				{
					foreach (string item2 in ids)
					{
						if (!captured.TrackIds.Contains(item2))
						{
							captured.TrackIds.Add(item2);
						}
					}
					captured.DateModified = DateTime.UtcNow;
					await App.Cache.UpsertPlaylistAsync(captured);
					App.MainWindowInstance?.RefreshPlaylistsPage();
				};
				parent.Items.Add(menuFlyoutItem);
			}
			if (parent.Items.Count == 0)
			{
				parent.Items.Add(new MenuFlyoutItem
				{
					Text = Resona.Models.Strings.Current.CS_NoPlaylist,
					IsEnabled = false
				});
			}
		}
		catch
		{
		}
	}

	private void CoverGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Grid grid)
		{
			Helpers.AnimationHelper.ApplyBouncyScale(grid, 1.05f);
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
			Helpers.AnimationHelper.ApplyBouncyScale(grid, 1.0f);
			UpdateCoverIndicator(grid, (grid.DataContext as Track)?.IsPlaying ?? false, isHovered: false);
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

	private void PlayOverlay_Tapped(object sender, TappedRoutedEventArgs e)
	{
		if (MainWindow.LastClickWasXButton) { MainWindow.LastClickWasXButton = false; return; }
		if (!(sender is Border border))
		{
			return;
		}
		Grid grid = border.Parent as Grid;
		if (!(grid?.DataContext is Track track))
		{
			return;
		}
		if (track.IsPlaying)
		{
			App.MainWindowInstance?.TogglePlayPause();
		}
		else
		{
			if (grid != null)
			{
				_activeIndicatorGrid = grid;
				UpdateCoverIndicator(grid, isPlaying: true, isHovered: false);
			}
			App.MainWindowInstance?.PlayTrack(track, _currentFilteredList);
		}
		if (grid != null)
		{
			UpdateCoverIndicator(grid, track.IsPlaying, isHovered: true);
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

	public bool TryGoBack() { BackButton_Click(null, null); return true; }

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









