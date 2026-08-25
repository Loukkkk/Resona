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







public sealed partial class ArtistsPage : Page



{



	private List<Track> _library = new List<Track>();
	private string _currentSort = "name_asc";

	private void SortArtistsMenu_Click(object sender, RoutedEventArgs e)
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







	private readonly Dictionary<string, Border> _artistBadges = new Dictionary<string, Border>();







	private static readonly Dictionary<int, HashSet<string>> _validCoverCache = new Dictionary<int, HashSet<string>>();







	private static ArtistsPage? _instance;







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



		return CoverCacheService.GetBitmap(path, 80);



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







	public ArtistsPage()



	{



		InitializeComponent();



		_instance = this;



        base.Loaded += delegate { if (AICleanupBtn != null) AICleanupBtn.Visibility = App.Settings.Current.AIEnabled ? Visibility.Visible : Visibility.Collapsed; };



	}







	public void LoadData(List<Track> library)



	{



		int newHash = ComputeHash(library);



		if (newHash == _builtLibraryHash && ArtistsGrid.Children.Count > 0)



		{



			return;



		}



		_library = library;



		_builtLibraryHash = newHash;



		_currentPage = 0;



		ArtistsGrid.Children.Clear();



		BuildValidCoverSetAsync(library, newHash, delegate



		{



			PreloadBitmapsInBackground(library, newHash, delegate



			{



				ArtistsGrid.Children.Clear();



				BuildUIBatched();



			});



		});



	}







	private async Task BuildValidCoverSetAsync(List<Track> library, int hash, Action? onCompleted)



	{



		if (_validCoverCache.ContainsKey(hash))



		{



			base.DispatcherQueue.TryEnqueue(delegate



			{



				onCompleted?.Invoke();



			});



			return;



		}



		HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);



		await Task.Run(delegate



		{



			foreach (string item in (from t in library



				where !string.IsNullOrEmpty(t.CoverArtPath)



				select t.CoverArtPath).Distinct<string>(StringComparer.OrdinalIgnoreCase))



			{



				try



				{



					if (File.Exists(item))



					{



						set.Add(item);



					}



				}



				catch



				{



				}



			}



		});



		_validCoverCache[hash] = set;



		base.DispatcherQueue.TryEnqueue(delegate



		{



			onCompleted?.Invoke();



		});



	}







	private bool CoverIsValid(string? path)



	{



		if (!string.IsNullOrEmpty(path) && _validCoverCache.TryGetValue(_builtLibraryHash, out HashSet<string> value))



		{



			return value.Contains(path);



		}



		return false;



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



		if (ArtistsGrid.Children.Count == 0 && _library.Count > 0)



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



			await Helpers.AnimationHelper.PlayFadeOutAsync(ArtistsGrid, 150);



			_currentPage--;



			BuildUIBatched(SearchBox.Text);



			Helpers.AnimationHelper.PlayFadeIn(ArtistsGrid, 150);



		}



	}







	private async void NextPage_Click(object sender, RoutedEventArgs e)



	{



		if (_currentPage < _totalPages - 1)



		{



			await Helpers.AnimationHelper.PlayFadeOutAsync(ArtistsGrid, 150);



			_currentPage++;



			BuildUIBatched(SearchBox.Text);



			Helpers.AnimationHelper.PlayFadeIn(ArtistsGrid, 150);



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



			if (track != null && _artistBadges.TryGetValue(track.Artist, out Border value))



			{



				value.Visibility = Visibility.Visible;



				_activePlayingBadge = value;



			}



		}



	}







	private void BuildUIBatched(string filter = "")



	{



		ArtistsGrid.Children.Clear();



		_artistBadges.Clear();



		var listQuery = from g in _library.GroupBy<Track, string>((Track t) => t.Artist, StringComparer.OrdinalIgnoreCase)
			where !string.IsNullOrWhiteSpace(g.Key) && (string.IsNullOrWhiteSpace(filter) || g.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
			select g;
		
		IEnumerable<IGrouping<string, Track>> listOrdered = _currentSort switch
		{
			"name_desc" => listQuery.OrderByDescending(g => g.Key, StringComparer.OrdinalIgnoreCase),
			"count_desc" => listQuery.OrderByDescending(g => g.Count()),
			_ => listQuery.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
		};

		List<(string, List<Track>, int)> list = listOrdered.Select(delegate(IGrouping<string, Track> g)
		{
			List<Track> item = (from t in g
				orderby t.Album, t.TrackNumber
				select t).ToList();
			int item2 = g.Select((Track t) => t.Album).Where((string a) => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
			return (g.Key, item, item2);
		}).ToList();



		_totalPages = (int)Math.Ceiling((double)list.Count / 24.0);



		_currentPage = Math.Clamp(_currentPage, 0, Math.Max(1, _totalPages) - 1);



		CountHint.Text = ((list.Count > 0) ? string.Format(Resona.Models.Strings.Current.CS_ArtistsCount, list.Count) + $" ({Resona.Models.Strings.Current.CS_PagePrefix} {_currentPage + 1}/{Math.Max(1, _totalPages)})" : " ");



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



				PageIndicator.Text = $"{Resona.Models.Strings.Current.CS_Page} {_currentPage + 1} / {Math.Max(1, _totalPages)}";



			}



		}



		List<(string key, List<Track> tracks, int albumCount)> pageArtists = list.Skip(_currentPage * 24).Take(24).ToList();



		int index = 0;



		EnqueueNextBatch();



		void EnqueueNextBatch()



		{



			if (index < pageArtists.Count)



			{



				int num = Math.Min(index + 24, pageArtists.Count);



				for (int i = index; i < num; i++)



				{



					ArtistsGrid.Children.Add(BuildArtistCard(pageArtists[i].key, pageArtists[i].tracks, pageArtists[i].albumCount));



				}



				index = num;



				if (index < pageArtists.Count)



				{



					base.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, EnqueueNextBatch);



				}



			}



		}



	}







	private Grid BuildArtistCard(string artistName, List<Track> tracks, int albumCount)



	{



		Grid card = new Grid



		{



			CornerRadius = new CornerRadius(14.0),



			Background = new SolidColorBrush(Color.FromArgb(22, byte.MaxValue, byte.MaxValue, byte.MaxValue)),



			Margin = new Thickness(6.0, 6.0, 6.0, 8.0),



			Width = 200.0,



			Height = 190.0,



			Translation = new Vector3(0f, 0f, 8f)



		};



		card.Shadow = new ThemeShadow();



		StackPanel stackPanel = new StackPanel



		{



			HorizontalAlignment = HorizontalAlignment.Center,



			VerticalAlignment = VerticalAlignment.Center,



			Spacing = 10.0,



			Margin = new Thickness(18.0, 0.0, 18.0, 0.0)



		};



		Border border = new Border



		{



			Width = 56.0,



			Height = 56.0,



			CornerRadius = new CornerRadius(28.0),



			Background = (Brush)Application.Current.Resources["AppAccentGradientBrush"],



			HorizontalAlignment = HorizontalAlignment.Center



		};



		Track track = tracks.FirstOrDefault((Track t) => CoverIsValid(t.CoverArtPath));



		BitmapImage bitmapImage = ((track != null) ? GetCoverBitmap(track.CoverArtPath) : null);



		if (bitmapImage != null)



		{



			border.Background = new ImageBrush



			{



				ImageSource = bitmapImage,



				Stretch = Stretch.UniformToFill



			};



		}



		else



		{



			border.Child = new FontIcon



			{



				Glyph = "\ue77b",



				FontSize = 24.0,



				Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],



				HorizontalAlignment = HorizontalAlignment.Center,



				VerticalAlignment = VerticalAlignment.Center



			};



		}



		stackPanel.Children.Add(border);



		stackPanel.Children.Add(new TextBlock



		{



			Text = artistName,



			FontWeight = FontWeights.SemiBold,



			FontSize = 16.0,



			TextWrapping = TextWrapping.Wrap,



			TextTrimming = TextTrimming.CharacterEllipsis,



			MaxLines = 2,



			HorizontalAlignment = HorizontalAlignment.Center,



			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]



		});



		stackPanel.Children.Add(new TextBlock



		{



			Text = $"{Resona.Models.Strings.Current.FormatTracksCount(tracks.Count)} • {Resona.Models.Strings.Current.FormatAlbumsCount(albumCount)}",



			FontSize = 11.0,



			Opacity = 0.45,



			HorizontalAlignment = HorizontalAlignment.Center,



			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]



		});



		card.Children.Add(stackPanel);



		Border border2 = new Border



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



		card.Children.Add(border2);



		_artistBadges[artistName] = border2;



		if (App.NowPlayingId != null && (tracks.Any((Track t) => t.Id == App.NowPlayingId) || (!string.IsNullOrEmpty(App.NowPlayingFilePath) && tracks.Any((Track t) => string.Equals(t.FilePath, App.NowPlayingFilePath, StringComparison.OrdinalIgnoreCase)))))



		{



			border2.Visibility = Visibility.Visible;



			_activePlayingBadge = border2;



		}



		card.PointerEntered += delegate



		{



			card.Background = new SolidColorBrush(Color.FromArgb(34, byte.MaxValue, byte.MaxValue, byte.MaxValue));



		};



		card.PointerExited += delegate



		{



			card.Background = new SolidColorBrush(Color.FromArgb(22, byte.MaxValue, byte.MaxValue, byte.MaxValue));



		};



		card.Tapped += delegate



		{
		if (MainWindow.LastClickWasXButton) { MainWindow.LastClickWasXButton = false; return; }



			App.MainWindowInstance?.ShowTrackCollection(artistName, tracks);



		};



		ToolTipService.SetToolTip(card, Resona.Models.Strings.Current.CS_Tooltip_TracksArtist);



		return card;



	

    

}

}



















