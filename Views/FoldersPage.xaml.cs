using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

public sealed partial class FoldersPage : Page
{
	private List<Track> _library = new List<Track>();

	private int _builtLibraryHash;

	private int _currentPage;

	private int _totalPages = 1;

	private const int PageSize = 20;

	private const string UnknownFolder = "Dossier inconnu";

	private Border? _activePlayingBadge;

	private readonly Dictionary<string, Border> _folderBadges = new Dictionary<string, Border>();

	private static FoldersPage? _instance;

	public static void ResetSessionCaches()
	{
		if (_instance != null)
		{
			_instance._builtLibraryHash = 0;
		}
	}

	public FoldersPage()
	{
		InitializeComponent();
		_instance = this;
	}

	public void LoadData(List<Track> library)
	{
		int num = ComputeHash(library);
		if (num != _builtLibraryHash || FoldersGrid.Children.Count <= 0)
		{
			_library = library;
			_builtLibraryHash = num;
			_currentPage = 0;
			FoldersGrid.Children.Clear();
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
			string key = FolderKey(track);
			if (_folderBadges.TryGetValue(key, out Border value))
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
			hashCode.Add(item.FilePath);
		}
		return hashCode.ToHashCode();
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);
		if (FoldersGrid.Children.Count == 0 && _library.Count > 0)
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
			await Helpers.AnimationHelper.PlayFadeOutAsync(FoldersGrid, 150);
			_currentPage--;
			BuildUIBatched(SearchBox.Text);
			Helpers.AnimationHelper.PlayFadeIn(FoldersGrid, 150);
		}
	}

	private async void NextPage_Click(object sender, RoutedEventArgs e)
	{
		if (_currentPage < _totalPages - 1)
		{
			await Helpers.AnimationHelper.PlayFadeOutAsync(FoldersGrid, 150);
			_currentPage++;
			BuildUIBatched(SearchBox.Text);
			Helpers.AnimationHelper.PlayFadeIn(FoldersGrid, 150);
		}
	}

	private static string FolderKey(Track t)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(t.FilePath);
			if (string.IsNullOrEmpty(directoryName))
			{
				return "Dossier inconnu";
			}
			return directoryName;
		}
		catch
		{
			return "Dossier inconnu";
		}
	}

	private void BuildUIBatched(string filter = "")
	{
		FoldersGrid.Children.Clear();
		_folderBadges.Clear();
		List<(string, List<Track>)> list = (from g in (from g in _library.GroupBy<Track, string>(FolderKey, StringComparer.OrdinalIgnoreCase)
				where string.IsNullOrWhiteSpace(filter) || g.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
				select g).OrderBy<IGrouping<string, Track>, string>((IGrouping<string, Track> g) => g.Key, StringComparer.OrdinalIgnoreCase)
			select (Path: g.Key, Tracks: (from t in g
				orderby t.Artist, t.Album, t.TrackNumber
				select t).ToList())).ToList();
		_totalPages = (int)Math.Ceiling((double)list.Count / 20.0);
		_currentPage = Math.Clamp(_currentPage, 0, Math.Max(1, _totalPages) - 1);
		CountHint.Text = ((list.Count > 0) ? string.Format(Resona.Models.Strings.Current.CS_FoldersCount, list.Count) + $" ({Resona.Models.Strings.Current.CS_PagePrefix} {_currentPage + 1}/{Math.Max(1, _totalPages)})" : " ");
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
				PageIndicator.Text = $"Page {_currentPage + 1} / {Math.Max(1, _totalPages)}";
			}
		}
		List<(string Path, List<Track> Tracks)> pageFolders = list.Skip(_currentPage * 20).Take(20).ToList();
		int index = 0;
		EnqueueNextBatch();
		void EnqueueNextBatch()
		{
			if (index < pageFolders.Count)
			{
				int num = Math.Min(index + 20, pageFolders.Count);
				for (int i = index; i < num; i++)
				{
					FoldersGrid.Children.Add(BuildFolderRow(pageFolders[i].Path, pageFolders[i].Tracks));
				}
				index = num;
				if (index < pageFolders.Count)
				{
					base.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, EnqueueNextBatch);
				}
			}
		}
	}

	private Grid BuildFolderRow(string folderPath, List<Track> tracks)
	{
		string displayName = ((folderPath == "Dossier inconnu") ? "Dossier inconnu" : Path.GetFileName(folderPath.TrimEnd('\\', '/')));
		if (string.IsNullOrWhiteSpace(displayName))
		{
			displayName = folderPath;
		}
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
			Glyph = "\ue8b7",
			FontSize = 34.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = displayName,
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
		_folderBadges[folderPath] = border;
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
			App.MainWindowInstance?.ShowTrackCollection(displayName, tracks);
		};
		ToolTipService.SetToolTip(card, Resona.Models.Strings.Current.CS_Tooltip_TracksFolder);
		return card;
	}
}



