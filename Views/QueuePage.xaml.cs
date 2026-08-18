using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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

public sealed partial class QueuePage : Page
{

	

	private Grid? _activeIndicatorGrid;

	private DispatcherTimer? _rmsTimer;

	private float _smoothedRms;

	private List<Track> _queue = new List<Track>();

	public ObservableCollection<Track> DisplayedTracks { get; } = new ObservableCollection<Track>();

	public QueuePage()
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

	private void SyncNowPlayingId()
	{
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);
		SyncNowPlayingId();
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

	private void UpdateDisplayedTracks(List<Track> newItems)
	{
		for (int i = 0; i < newItems.Count; i++)
		{
			if (i < DisplayedTracks.Count) DisplayedTracks[i] = newItems[i];
			else DisplayedTracks.Add(newItems[i]);
		}
		while (DisplayedTracks.Count > newItems.Count) DisplayedTracks.RemoveAt(DisplayedTracks.Count - 1);
	}

	public void SetQueue(List<Track> queue)
	{
		_queue = queue;
		foreach (Track item in _queue)
		{
			item.IsPlaying = !string.IsNullOrEmpty(App.NowPlayingFilePath) && string.Equals(item.FilePath, App.NowPlayingFilePath, StringComparison.OrdinalIgnoreCase);
		}
		
		UpdateDisplayedTracks(_queue);
	}

	private void UpdateCoverIndicator(Grid coverGrid, bool isPlaying, bool isHovered)
	{
		Border border = FindChildByName<Border>(coverGrid, "PlayOverlay");
		FontIcon fontIcon = FindChildByName<FontIcon>(coverGrid, "OverlayIcon");
		if (border == null)
		{
			return;
		}
		bool flag = isPlaying && App.AudioEngine.State == PlaybackState.Playing;
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

	private void PlayOverlay_Tapped(object sender, TappedRoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: Track dataContext } frameworkElement)
		{
			if (dataContext.IsPlaying)
			{
				App.MainWindowInstance?.TogglePlayPause();
			}
			else
			{
				App.MainWindowInstance?.PlayTrack(dataContext, _queue);
			}
			if (frameworkElement is Border { Parent: Grid parent })
			{
				UpdateCoverIndicator(parent, dataContext.IsPlaying, isHovered: true);
			}
		}
	}

	private void TrackListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if ((e.OriginalSource as FrameworkElement)?.DataContext is Track track)
		{
			App.MainWindowInstance?.PlayTrack(track, _queue);
			TrackListView?.SelectedItems.Clear();
		}
	}

	private void TrackListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		Track track = (e.OriginalSource as FrameworkElement)?.DataContext as Track;
		if (track == null)
		{
			return;
		}
		e.Handled = true;
		List<Track> selected = TrackListView.SelectedItems.Cast<Track>().ToList();
		MenuFlyout menuFlyout = App.MainWindowInstance?.BuildTrackMenu(track, _queue, selected);
		if (!(menuFlyout != null))
		{
			return;
		}
		if (menuFlyout.Items.Count >= 2)
		{
			string text = ((selected != null && selected.Count > 1 && selected.Contains(track)) ? $"Supprimer {selected.Count} titres de la file d'attente" : "Supprimer de la file d'attente");
			MenuFlyoutItem menuFlyoutItem = new MenuFlyoutItem
			{
				Text = text,
				Icon = new FontIcon
				{
					Glyph = "\ue74d"
				}
			};
			menuFlyoutItem.Click += delegate
			{
				foreach (Track item in (selected != null && selected.Count > 1 && selected.Contains(track)) ? selected : new List<Track> { track })
				{
					App.MainWindowInstance?.RemoveFromQueue(item);
				}
			};
			menuFlyout.Items.Add(new MenuFlyoutSeparator());
			menuFlyout.Items.Add(menuFlyoutItem);
		}
		menuFlyout.ShowAt(TrackListView, e.GetPosition(TrackListView));
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





