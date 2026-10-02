using System;



using System.CodeDom.Compiler;



using System.Collections.Generic;



using System.Diagnostics;



using System.Linq;



using System.Numerics;



using Resona.Models;



using Microsoft.UI.Dispatching;



using Microsoft.UI.Text;



using Microsoft.UI.Xaml;



using Microsoft.UI.Xaml.Controls;



using Microsoft.UI.Xaml.Controls.Primitives;



using Microsoft.UI.Xaml.Markup;



using Microsoft.UI.Xaml.Media;



using Microsoft.UI.Xaml.Navigation;



using WinRT;



using Windows.UI;







namespace Resona.Views;







public sealed partial class GenresPage : Page



{



	private List<Track> _library = new List<Track>();
	private string _currentSort = "name_asc";

	private int _builtLibraryHash;







	private int _currentPage;







	private int _totalPages = 1;







	private const int PageSize = 24;







	private const string UnknownGenre = "Genre inconnu";







	private Border? _activePlayingBadge;







	private readonly Dictionary<string, Border> _genreBadges = new Dictionary<string, Border>();







	private static GenresPage? _instance;







	public static void ResetSessionCaches()



	{



		if (_instance != null)



		{



			_instance._builtLibraryHash = 0;



		}



	}







	public GenresPage()
	{
		InitializeComponent();
		_currentSort = App.Settings.Current.GenresSort;
		Resona.Helpers.DisplayCountHelper.Setup(DisplayCountCombo, App.Settings.Current.GenresDisplayLimit, v =>
		{
			App.Settings.Current.GenresDisplayLimit = v;
			_ = App.Settings.SaveAsync();
			_currentPage = 0;
			BuildUIBatched(SearchBox.Text);
		});




		_instance = this;
		UpdateSortButton();



        base.Loaded += delegate { if (AICleanupBtn != null) AICleanupBtn.Visibility = App.Settings.Current.AIEnabled ? Visibility.Visible : Visibility.Collapsed; };



	}







	private void UpdateSortButton()
	{
		string[] parts = _currentSort.Split('_');
		string field = parts[0];
		bool isAsc = parts.Length > 1 && parts[1] == "asc";

		string sortName = field switch
		{
			"name" => Resona.Models.Strings.Current.PlaylistsPage_Sort_Name,
			"count" => Resona.Models.Strings.Current.PlaylistsPage_Sort_Count,
			_ => Resona.Models.Strings.Current.PlaylistsPage_Sort_Name
		};
		if (SortButtonLabel != null) SortButtonLabel.Text = (Resona.Models.Strings.Current.IsFr ? "Trier : " : "Sort: ") + sortName;
		if (SortDirectionIcon != null) SortDirectionIcon.Glyph = isAsc ? "\uE74A" : "\uE74B";
	}

	private void SortGenresMenu_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Microsoft.UI.Xaml.Controls.MenuFlyoutItem item && item.Tag is string field)
		{
			string dir = (field == "count") ? "desc" : "asc";
			_currentSort = $"{field}_{dir}";
			App.Settings.Current.GenresSort = _currentSort;
			_ = App.Settings.SaveAsync();
			UpdateSortButton();
			BuildUIBatched(SearchBox.Text);
		}
	}

	private void SortDirection_Click(object sender, RoutedEventArgs e)
	{
		string[] parts = _currentSort.Split('_');
		string field = parts[0];
		string dir = (parts.Length > 1 && parts[1] == "asc") ? "desc" : "asc";
		_currentSort = $"{field}_{dir}";
		App.Settings.Current.GenresSort = _currentSort;
		_ = App.Settings.SaveAsync();
		UpdateSortButton();
		BuildUIBatched(SearchBox.Text);
	}

		private void ShuffleAll_Click(object sender, RoutedEventArgs e)
	{
		var listQuery = from g in _library.GroupBy<Track, string>(t => string.IsNullOrWhiteSpace(t.Genre) ? "Genre inconnu" : t.DisplayGenre, StringComparer.OrdinalIgnoreCase)
						where string.IsNullOrWhiteSpace(SearchBox.Text) || g.Key.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)
						select g;
        var groups = listQuery.ToList();
        if (groups.Count > 0)
        {
            Random random = new Random();
            var randomGroup = groups[random.Next(groups.Count)];
            List<Track> tracks = randomGroup.OrderBy(t => t.Artist).ThenBy(t => t.Album).ThenBy(t => t.TrackNumber).ToList();
            string title = randomGroup.Key;
            App.MainWindowInstance?.ShowTrackCollection(title, tracks, "Genre");
            Track randomTrack = tracks[random.Next(tracks.Count)];
            App.MainWindowInstance?.SetShuffleModeAndPlay(randomTrack, tracks);
        }
	}

	public void LoadData(List<Track> library)

	{

		int num = ComputeHash(library);



		if (num != _builtLibraryHash || GenresGrid.Children.Count <= 0)



		{



			_library = library;



			_builtLibraryHash = num;



			_currentPage = 0;



			GenresGrid.Children.Clear();



			BuildUIBatched();



		}



	}







	public void SetNowPlayingId(string? trackId, string? trackFilePath = null)



	{



		if (_activePlayingBadge != null)



		{



			_activePlayingBadge.Visibility = Visibility.Collapsed;



		}



		_activePlayingBadge = null;



		if (trackId == null)



		{



			return;



		}



		Track track = _library.FirstOrDefault((Track t) => t.Id == trackId) ?? ((!string.IsNullOrEmpty(trackFilePath)) ? _library.FirstOrDefault((Track t) => string.Equals(t.FilePath, trackFilePath, StringComparison.OrdinalIgnoreCase)) : null);



		if (track != null)



		{



			string key = (string.IsNullOrWhiteSpace(track.DisplayGenre) ? "Genre inconnu" : track.DisplayGenre);



			if (_genreBadges.TryGetValue(key, out Border value))



			{



				value.Visibility = Visibility.Visible;



				_activePlayingBadge = value;



			}



		}



	}







	private static int ComputeHash(List<Track> lib)



	{



		HashCode hashCode = default(HashCode);



		foreach (Track item in lib)



		{



			hashCode.Add(item.Id);



			hashCode.Add(item.Genre);



		}



		return hashCode.ToHashCode();



	}







	protected override void OnNavigatedTo(NavigationEventArgs e)



	{



		base.OnNavigatedTo(e);



		if (GenresGrid.Children.Count == 0 && _library.Count > 0)



		{



			BuildUIBatched();



		}



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



			await Helpers.AnimationHelper.PlayFadeOutAsync(GenresGrid, 150);



			_currentPage--;



			BuildUIBatched(SearchBox.Text);



			Helpers.AnimationHelper.PlayFadeIn(GenresGrid, 150);



		}



	}







	private async void NextPage_Click(object sender, RoutedEventArgs e)



	{



		if (_currentPage < _totalPages - 1)



		{



			await Helpers.AnimationHelper.PlayFadeOutAsync(GenresGrid, 150);



			_currentPage++;



			BuildUIBatched(SearchBox.Text);



			Helpers.AnimationHelper.PlayFadeIn(GenresGrid, 150);



		}



	}







	private static string GenreKey(Track t)



	{



		if (!string.IsNullOrWhiteSpace(t.Genre))



		{



			return t.DisplayGenre;



		}



		return "Genre inconnu";



	}







	private void BuildUIBatched(string filter = "")



	{



		GenresGrid.Children.Clear();



		_genreBadges.Clear();



		var listQuery = from g in _library.GroupBy<Track, string>(GenreKey, StringComparer.OrdinalIgnoreCase)
						where string.IsNullOrWhiteSpace(filter) || g.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
						select g;
		
		IEnumerable<IGrouping<string, Track>> listOrdered = _currentSort switch
		{
			"name_asc" => listQuery.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase),
			"name_desc" => listQuery.OrderByDescending(g => g.Key, StringComparer.OrdinalIgnoreCase),
			"count_asc" => listQuery.OrderBy(g => g.Count()),
			"count_desc" => listQuery.OrderByDescending(g => g.Count()),
			_ => listQuery.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
		};

		List<(string, List<Track>)> list = listOrdered.Select(g => (Name: g.Key, Tracks: g.OrderBy(t => t.Artist).ThenBy(t => t.Album).ThenBy(t => t.TrackNumber).ToList())).ToList();



		_totalPages = (int)Math.Ceiling((double)list.Count / (double)Resona.Helpers.DisplayCountHelper.GetEffective(App.Settings.Current.GenresDisplayLimit, list.Count));



		_currentPage = Math.Clamp(_currentPage, 0, Math.Max(1, _totalPages) - 1);



		CountHint.Text = ((list.Count > 0) ? string.Format(Resona.Models.Strings.Current.CS_GenresCount, list.Count) + $" ({Resona.Models.Strings.Current.CS_PagePrefix} {_currentPage + 1}/{Math.Max(1, _totalPages)})" : " ");



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



		int genresPageSize = Resona.Helpers.DisplayCountHelper.GetEffective(App.Settings.Current.GenresDisplayLimit, list.Count);
		List<(string Name, List<Track> Tracks)> pageGenres = list.Skip(_currentPage * genresPageSize).Take(genresPageSize).ToList();



		int index = 0;



		EnqueueNextBatch();



		void EnqueueNextBatch()



		{



			if (index < pageGenres.Count)



			{



				int num = Math.Min(index + 24, pageGenres.Count);



				for (int i = index; i < num; i++)



				{



					GenresGrid.Children.Add(BuildGenreCard(pageGenres[i].Name, pageGenres[i].Tracks));



				}



				index = num;



				if (index < pageGenres.Count)



				{



					base.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, EnqueueNextBatch);



				}



			}



		}



	}







	private Grid BuildGenreCard(string genreName, List<Track> tracks)



	{



		Grid card = new Grid



		{



			CornerRadius = new CornerRadius(14.0),



			Background = new SolidColorBrush(Color.FromArgb(22, byte.MaxValue, byte.MaxValue, byte.MaxValue)),



			Margin = new Thickness(6.0, 6.0, 6.0, 8.0),



			Width = 200.0,



			Height = 140.0,



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



		stackPanel.Children.Add(new FontIcon



		{



			Glyph = "\ue8d6",



			FontSize = 34.0,



			HorizontalAlignment = HorizontalAlignment.Center,



			Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]



		});



		stackPanel.Children.Add(new TextBlock



		{



			Text = genreName,



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



			Text = Resona.Models.Strings.Current.FormatTracksCount(tracks.Count),



			FontSize = 11.0,



			Opacity = 0.45,



			HorizontalAlignment = HorizontalAlignment.Center,



			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]



		});



		card.Children.Add(stackPanel);



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



		card.Children.Add(border);



		_genreBadges[genreName] = border;



		if (App.NowPlayingId != null && (tracks.Any((Track t) => t.Id == App.NowPlayingId) || (!string.IsNullOrEmpty(App.NowPlayingFilePath) && tracks.Any((Track t) => string.Equals(t.FilePath, App.NowPlayingFilePath, StringComparison.OrdinalIgnoreCase)))))



		{



			border.Visibility = Visibility.Visible;



			_activePlayingBadge = border;



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



			App.MainWindowInstance?.ShowTrackCollection(genreName, tracks);



		};



		ToolTipService.SetToolTip(card, Resona.Models.Strings.Current.CS_Tooltip_TracksGenre);



		return card;



	

    

}

}





















