using System;



using System.CodeDom.Compiler;



using System.Collections.Generic;



using System.Diagnostics;



using System.IO;



using System.Linq;



using System.Numerics;



using System.Threading.Tasks;



using Resona.Models;



using Resona.Services;



using Microsoft.UI;



using Microsoft.UI.Dispatching;



using Microsoft.UI.Text;



using Microsoft.UI.Xaml;



using Microsoft.UI.Xaml.Controls;



using Microsoft.UI.Xaml.Controls.Primitives;



using Microsoft.UI.Xaml.Markup;



using Microsoft.UI.Xaml.Media;



using Microsoft.UI.Xaml.Media.Imaging;



using Microsoft.UI.Xaml.Navigation;



using WinRT;



using Windows.UI;







namespace Resona.Views;







public sealed partial class AlbumsPage : Page



{



	private List<Track> _library = new List<Track>();
	private string _currentSort = "name_asc";

	private void SortAlbumsMenu_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Microsoft.UI.Xaml.Controls.MenuFlyoutItem item && item.Tag is string sort)
		{
			_currentSort = sort;
			SortButtonLabel.Text = item.Text;
			BuildUIBatched(SearchBox.Text);
		}
	}







	private int _builtLibraryHash;







	private int _currentPage;







	private int _totalPages = 1;







	private const int PageSize = 24;







	private Border? _activePlayingBadge;







	private readonly Dictionary<string, Border> _albumBadges = new Dictionary<string, Border>();







	private static readonly Dictionary<int, HashSet<string>> _validCoverCache = new Dictionary<int, HashSet<string>>();







	private static AlbumsPage? _instance;







	public static void ResetSessionCaches()



	{



		_validCoverCache.Clear();



		if (_instance != null)



		{



			_instance._builtLibraryHash = 0;



		}



	}







	private static BitmapImage? GetCoverBitmap(string? path)



	{



		return CoverCacheService.GetBitmap(path, 180);



	}







	private static void PreloadBitmapsInBackground(List<Track> library, int hash, Action? onCompleted)



	{



		DispatcherQueue dq = DispatcherQueue.GetForCurrentThread();



		Task.Run(delegate



		{



			if (_validCoverCache.TryGetValue(hash, out HashSet<string> validSet))



			{



				List<string> paths = (from t in library



					where !string.IsNullOrEmpty(t.CoverArtPath) && validSet.Contains(t.CoverArtPath)



					select t.CoverArtPath).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();



				dq?.TryEnqueue(DispatcherQueuePriority.Low, delegate



				{



					foreach (string item in paths)



					{



						GetCoverBitmap(item);



					}



					onCompleted?.Invoke();



				});



			}



		});



	}







	public AlbumsPage()



	{



		InitializeComponent();



		_instance = this;



        base.Loaded += delegate { if (AICleanupBtn != null) AICleanupBtn.Visibility = App.Settings.Current.AIEnabled ? Visibility.Visible : Visibility.Collapsed; };



	}







	public void LoadData(List<Track> library)



	{



		int newHash = ComputeHash(library);



		if (newHash == _builtLibraryHash && AlbumsGrid.Children.Count > 0)



		{



			return;



		}



		_library = library;



		_builtLibraryHash = newHash;



		_currentPage = 0;



		AlbumsGrid.Children.Clear();



		



		// Build UI immediately so cards are present during the page entrance animation



		BuildUIBatched();



	}







	private static int ComputeHash(List<Track> lib)



	{



		HashCode hashCode = default(HashCode);



		foreach (Track item in lib)



		{



			hashCode.Add(item.Id);



			hashCode.Add(item.CoverArtPath);



		}



		return hashCode.ToHashCode();



	}







	protected override void OnNavigatedTo(NavigationEventArgs e)



	{



		base.OnNavigatedTo(e);



		if (AlbumsGrid.Children.Count == 0 && _library.Count > 0)



		{



			BuildUIBatched();



		}



	}







	public void SetSearch(string searchTerm)



	{



		SearchBox.Text = searchTerm;



		_currentPage = 0;



		BuildUIBatched(searchTerm);



	}







	private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)



	{



		_currentPage = 0;



		BuildUIBatched(SearchBox.Text);



	}







	private async void PrevPage_Click(object sender, RoutedEventArgs e)



	{



		if (_currentPage > 0)



		{



			await Helpers.AnimationHelper.PlayFadeOutAsync(AlbumsGrid, 150);



			_currentPage--;



			BuildUIBatched(SearchBox.Text);



			Helpers.AnimationHelper.PlayFadeIn(AlbumsGrid, 150);



		}



	}







	private async void NextPage_Click(object sender, RoutedEventArgs e)



	{



		if (_currentPage < _totalPages - 1)



		{



			await Helpers.AnimationHelper.PlayFadeOutAsync(AlbumsGrid, 150);



			_currentPage++;



			BuildUIBatched(SearchBox.Text);



			Helpers.AnimationHelper.PlayFadeIn(AlbumsGrid, 150);



		}



	}







	public void SetNowPlayingId(string? trackId, string? trackFilePath = null)



	{



		if (_activePlayingBadge != null)



		{



			_activePlayingBadge.Visibility = Visibility.Collapsed;



		}



		_activePlayingBadge = null;



		if (trackId != null)



		{



			Track track = _library.FirstOrDefault((Track t) => t.Id == trackId) ?? ((!string.IsNullOrEmpty(trackFilePath)) ? _library.FirstOrDefault((Track t) => string.Equals(t.FilePath, trackFilePath, StringComparison.OrdinalIgnoreCase)) : null);



			if (track != null && _albumBadges.TryGetValue(track.DisplayAlbum, out Border value))



			{



				value.Visibility = Visibility.Visible;



				_activePlayingBadge = value;



			}



		}



	}







	private void BuildUIBatched(string filter = "")



	{



		AlbumsGrid.Children.Clear();



		_albumBadges.Clear();



		var listQuery = from g in _library.GroupBy<Track, string>((Track t) => t.Album, StringComparer.OrdinalIgnoreCase)
				where !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1 && (string.IsNullOrWhiteSpace(filter) || g.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) || g.First().Artist.Contains(filter, StringComparison.OrdinalIgnoreCase))
				select g;
		
		IEnumerable<IGrouping<string, Track>> listOrdered = _currentSort switch
		{
			"name_desc" => listQuery.OrderByDescending(g => g.Key, StringComparer.OrdinalIgnoreCase),
			"count_desc" => listQuery.OrderByDescending(g => g.Count()),
			_ => listQuery.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
		};

		List<(string, List<Track>)> list = listOrdered.Select(g => (g.Key, g.OrderBy(t => t.TrackNumber).ToList())).ToList();



		_totalPages = (int)Math.Ceiling((double)list.Count / 24.0);



		_currentPage = Math.Clamp(_currentPage, 0, Math.Max(1, _totalPages) - 1);



		CountHint.Text = ((list.Count > 0) ? string.Format(Resona.Models.Strings.Current.CS_AlbumsCount, list.Count) + $" ({Resona.Models.Strings.Current.CS_PagePrefix} {_currentPage + 1}/{_totalPages})" : " ");



		if (PaginationPanel != null)



		{



			PaginationPanel.Visibility = ((_totalPages <= 1) ? Visibility.Collapsed : Visibility.Visible);



			if (PrevPageButton != null)



			{



				PrevPageButton.IsEnabled = _currentPage > 0;



			}



			if (NextPageButton != null)



			{



				NextPageButton.IsEnabled = _currentPage < _totalPages - 1;



			}



			if (PageIndicator != null)



			{



				PageIndicator.Text = $"{Resona.Models.Strings.Current.CS_Page} {_currentPage + 1} / {_totalPages}";



			}



		}



		List<(string name, List<Track> tracks)> pageAlbums = list.Skip(_currentPage * 24).Take(24).ToList();



		int index = 0;



		EnqueueNextBatch();



		void EnqueueNextBatch()



		{



			if (index < pageAlbums.Count)



			{



				int num = Math.Min(index + 24, pageAlbums.Count);



				for (int i = index; i < num; i++)



				{



					AlbumsGrid.Children.Add(BuildAlbumCard(pageAlbums[i].name, pageAlbums[i].tracks));



				}



				index = num;



				if (index < pageAlbums.Count)



				{



					base.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, EnqueueNextBatch);



				}



			}



		}



	}







	private void BuildUI(string filter = "")



	{



		BuildUIBatched(filter);



	}







	private Grid BuildAlbumCard(string albumName, List<Track> tracks)



	{



		Grid obj = new Grid



		{



			Width = 180.0,



			Height = 280.0,



			CornerRadius = new CornerRadius(12.0),



			Background = new SolidColorBrush(Color.FromArgb(20, byte.MaxValue, byte.MaxValue, byte.MaxValue)),



			Margin = new Thickness(8.0, 4.0, 8.0, 16.0),



			Translation = new System.Numerics.Vector3(0f, 0f, 8f),



			RowDefinitions = 



			{



				new RowDefinition



				{



					Height = new GridLength(180.0)



				},



				new RowDefinition



				{



					Height = new GridLength(1.0, GridUnitType.Star)



				}



			}



		};



		obj.Shadow = new ThemeShadow();



		Track track = tracks.FirstOrDefault((Track t) => !string.IsNullOrEmpty(t.CoverArtPath));



		Grid grid = new Grid



		{



			CornerRadius = new CornerRadius(12.0, 12.0, 0.0, 0.0),



			Background = (Brush)Application.Current.Resources["AppAccentGradientBrush"],



			Tag = track?.CoverArtPath



		};



		BitmapImage bitmapImage = ((track != null) ? GetCoverBitmap(track.CoverArtPath) : null);



		if (bitmapImage != null)



		{



			grid.Children.Add(new Grid



			{



				CornerRadius = new CornerRadius(12.0, 12.0, 0.0, 0.0),



				Background = new ImageBrush



				{



					ImageSource = bitmapImage,



					Stretch = Stretch.UniformToFill



				}



			});



		}



		else if (track == null || string.IsNullOrEmpty(track.CoverArtPath))



		{



			grid.Children.Add(new FontIcon



			{



				Glyph = "\ue93c",



				FontSize = 48.0,



				Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],



				HorizontalAlignment = HorizontalAlignment.Center,



				VerticalAlignment = VerticalAlignment.Center



			});



		}



		Grid.SetRow(grid, 0);



		obj.Children.Add(grid);



		Border playOverlay = new Border



		{



			Background = new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)),



			CornerRadius = new CornerRadius(12.0, 12.0, 0.0, 0.0),



			Visibility = Visibility.Collapsed,



			Child = new FontIcon



			{



				Glyph = "\ue768",



				FontSize = 36.0,



				Foreground = new SolidColorBrush(Colors.White),



				HorizontalAlignment = HorizontalAlignment.Center,



				VerticalAlignment = VerticalAlignment.Center



			}



		};



		Grid.SetRow(playOverlay, 0);



		obj.Children.Add(playOverlay);



		Border border = new Border



		{



			Width = 16.0,



			Height = 16.0,



			CornerRadius = new CornerRadius(8.0),



			Background = (Brush)Application.Current.Resources["AppAccentBrush"],



			HorizontalAlignment = HorizontalAlignment.Right,



			VerticalAlignment = VerticalAlignment.Bottom,



			Margin = new Thickness(0.0, 0.0, 6.0, 6.0),



			Visibility = Visibility.Collapsed



		};



		grid.Children.Add(border);



		_albumBadges[albumName] = border;



		if (App.NowPlayingId != null && (tracks.Any((Track t) => t.Id == App.NowPlayingId) || (!string.IsNullOrEmpty(App.NowPlayingFilePath) && tracks.Any((Track t) => string.Equals(t.FilePath, App.NowPlayingFilePath, StringComparison.OrdinalIgnoreCase)))))



		{



			border.Visibility = Visibility.Visible;



			_activePlayingBadge = border;



		}



		Grid grid2 = new Grid



		{



			Padding = new Thickness(12.0, 10.0, 12.0, 10.0)



		};



		grid2.RowDefinitions.Add(new RowDefinition



		{



			Height = GridLength.Auto



		});



		grid2.RowDefinitions.Add(new RowDefinition



		{



			Height = new GridLength(4.0)



		});



		grid2.RowDefinitions.Add(new RowDefinition



		{



			Height = GridLength.Auto



		});



		grid2.RowDefinitions.Add(new RowDefinition



		{



			Height = new GridLength(1.0, GridUnitType.Star)



		});



		grid2.RowDefinitions.Add(new RowDefinition



		{



			Height = GridLength.Auto



		});



		TextBlock textBlock = new TextBlock



		{



			Text = albumName,



			FontWeight = FontWeights.SemiBold,



			FontSize = 14.0,



			TextTrimming = TextTrimming.CharacterEllipsis,



			MaxLines = 2,



			TextWrapping = TextWrapping.Wrap,



			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],



			VerticalAlignment = VerticalAlignment.Top



		};



		Grid.SetRow(textBlock, 0);



		grid2.Children.Add(textBlock);



		TextBlock textBlock2 = new TextBlock



		{



			Text = tracks.First().Artist,



			FontSize = 12.0,



			Opacity = 0.7,



			TextTrimming = TextTrimming.CharacterEllipsis,



			MaxLines = 1,



			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]



		};



		Grid.SetRow(textBlock2, 2);



		grid2.Children.Add(textBlock2);



		TextBlock textBlock3 = new TextBlock



		{



			Text = Resona.Models.Strings.Current.FormatTracksCount(tracks.Count),



			FontSize = 11.0,



			Opacity = 0.45,



			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]



		};



		Grid.SetRow(textBlock3, 4);



		grid2.Children.Add(textBlock3);



		Grid.SetRow(grid2, 1);



		obj.Children.Add(grid2);



		obj.PointerEntered += delegate



		{



			playOverlay.Visibility = Visibility.Visible;



			if (tracks.Count > 0)



			{



				App.AudioEngine.PrewarmOpus(tracks[0].FilePath, tracks[0].Duration);



			}



		};



		obj.PointerExited += delegate



		{



			playOverlay.Visibility = Visibility.Collapsed;



		};



		obj.Tapped += delegate



		{
		if (MainWindow.LastClickWasXButton) { MainWindow.LastClickWasXButton = false; return; }



			if (tracks.Count > 0)



			{



				App.MainWindowInstance?.ShowTrackCollection(albumName, tracks, tracks.First().Artist);



			}



		};



		obj.DoubleTapped += delegate



		{



			if (tracks.Count > 0)



			{



				App.MainWindowInstance?.PlayTrack(tracks[0], tracks);



			}



		};



		return obj;



	

    

}

}



















