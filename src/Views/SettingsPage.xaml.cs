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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Resona.Models;
using Resona.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT;
using WinRT.Interop;

namespace Resona.Views
{

public sealed partial class SettingsPage : Page
{
	private bool _isLoading = true;

	private static readonly string[] EqualizerLabels = new string[10] { "31", "62", "125", "250", "500", "1K", "2K", "4K", "8K", "16K" };
	private bool _isInitializingUpdates = true;
    public SettingsPage()
	{
		_isLoading = true;
		InitializeComponent();
        AutoUpdateSwitch.IsOn = App.Settings.Current.AutoUpdateEnabled;
        _isInitializingUpdates = false;
		LoadEssentialSettings();
		base.Loaded += delegate
		{
			base.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, delegate
			{
				BuildPresetsList();
				RefreshFoldersList();
			});
		};
	}

	private void LoadEssentialSettings()
	{
		_isLoading = true;
		AppSettings current = App.Settings.Current;

		var langToSelect = string.IsNullOrEmpty(current.AppLanguage) ? System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : current.AppLanguage;
		LanguageDropDown.Content = langToSelect == "fr" ? Models.Strings.Current.SettingsPage_Content_Franaisfr : Models.Strings.Current.SettingsPage_Content_Englishen;

		NormalizationSwitch.IsOn = current.NormalizationEnabled;
		LyricsSwitch.IsOn = current.LyricsEnabled;
		TranslateLyricsSwitch.IsOn = current.TranslateLyricsEnabled;
		CoverSwitch.IsOn = current.AutoFetchMissingCovers;
		ExclusiveModeSwitch.IsOn = current.ExclusiveAudioMode;
		NowPlayingSwitch.IsOn = current.AutoOpenNowPlaying;
		ShowLibraryCheck.IsChecked = current.ShowLibraryCategory;
		ShowAlbumsCheck.IsChecked = current.ShowAlbumsCategory;
		ShowPlaylistsCheck.IsChecked = current.ShowPlaylistsCategory;
		ShowArtistsCheck.IsChecked = current.ShowArtistsCategory;
		ShowGenresCheck.IsChecked = current.ShowGenresCategory;
		ShowFoldersCheck.IsChecked = current.ShowFoldersCategory;
		ShowStatisticsCheck.IsChecked = current.ShowStatisticsCategory;
		ShowDownloadCheck.IsChecked = current.ShowDownloadCategory;

		UpdateTranslateVisibility();

		foreach (RadioButton item in BackdropChoice.Items.Cast<RadioButton>())
		{
			if (item.Tag?.ToString() == current.Backdrop.ToString())
			{
				BackdropChoice.SelectedItem = item;
				break;
			}
		}
		ColorPresetsPanel.Visibility = ((current.Backdrop != AppBackdropStyle.Solid) ? Visibility.Collapsed : Visibility.Visible);
		GradientOverflowSwitch.IsOn = current.PlayerGradientOverflowEnabled;
		MinimizeToTraySwitch.IsOn = current.MinimizeToTrayOnClose;
		StartWithWindowsSwitch.IsOn = current.StartWithWindows;
		StartMinimizedSwitch.IsOn = current.StartMinimized;
		bool flag = current.MinimizeToTrayOnClose && current.StartWithWindows;
		StartMinimizedSwitch.IsEnabled = flag;
		StartMinimizedHint.Opacity = (flag ? 0.6 : 0.2);
		EqualizerSwitch.IsOn = current.EqualizerEnabled;
		if (EqualizerBandsPanel != null)
		{
			EqualizerBandsPanel.Visibility = ((!current.EqualizerEnabled) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (current.EqualizerEnabled)
		{
			BuildEqualizerSliders();
		}
		DownloadFolderBox.Text = current.DownloadFolder;
		foreach (ComboBoxItem item2 in DownloadFormatCombo.Items.Cast<ComboBoxItem>())
		{
			if (item2.Tag?.ToString() == current.DownloadFormat.ToString())
			{
				DownloadFormatCombo.SelectedItem = item2;
				break;
			}
		}
		if (DownloadFormatCombo.SelectedItem == null)
		{
			DownloadFormatCombo.SelectedIndex = 0;
		}
		DownloadCodecBox.Text = current.DownloadCodec;
		foreach (ComboBoxItem item3 in DownloadBitrateCombo.Items.Cast<ComboBoxItem>())
		{
			if (item3.Tag?.ToString() == current.DownloadBitrate.ToString())
			{
				DownloadBitrateCombo.SelectedItem = item3;
				break;
			}
		}
		if (DownloadBitrateCombo.SelectedItem == null)
		{
			DownloadBitrateCombo.SelectedIndex = 0;
		}
		UpdateFormatHint(current.DownloadFormat);
		AIEnabledSwitch.IsOn = current.AIEnabled;
		_isLoading = false;
	}

	private void NormalizationTargetSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.NormalizationTargetRms = e.NewValue;
			App.Settings.SaveAsync();
			App.MainWindowInstance?.InvalidateNormalizationAndReanalyze();
		}
	}

	private void NormalizationGainSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.NormalizationMaxGain = e.NewValue;
			App.Settings.SaveAsync();
			App.MainWindowInstance?.InvalidateNormalizationAndReanalyze();
		}
	}

		private static string FormatHint(DownloadFormat fmt)
	{
		string result = fmt switch
		{
			DownloadFormat.Opus => Models.Strings.Current.IsFr ? "Codec libopus — Meilleure qualité par kbit/s disponible — Recommandé." : "libopus codec — Best quality per kbit/s available — Recommended.", 
			DownloadFormat.Mp3 => Models.Strings.Current.IsFr ? "Codec libmp3lame — Universel, compatible partout." : "libmp3lame codec — Universal, widely compatible.", 
			DownloadFormat.Flac => Models.Strings.Current.IsFr ? "Sans perte — Fichiers très lourds — Codec : flac." : "Lossless — Very large files — Codec: flac.", 
			DownloadFormat.M4a => Models.Strings.Current.IsFr ? "Codec AAC — Bonne qualité, compatible Apple." : "AAC codec — Good quality, Apple compatible.", 
			DownloadFormat.Vorbis => Models.Strings.Current.IsFr ? "Codec libvorbis — Open source, OGG." : "libvorbis codec — Open source, OGG.", 
			DownloadFormat.Wav => Models.Strings.Current.IsFr ? "PCM non compressé — Archivage uniquement — Très lourd." : "Uncompressed PCM — Archival only — Very large.", 
			_ => string.Empty, 
		};
		return result;
	}

	private void UpdateFormatHint(DownloadFormat fmt)
	{
		if (FormatHintText != null)
		{
			FormatHintText.Text = FormatHint(fmt);
		}
	}

	private void BuildPresetsList()
	{
		PresetsList.Items.Clear();
		for (int i = 0; i < ThemePresets.All.Length; i++)
		{
			ThemePreset themePreset = ThemePresets.All[i];
			int index = i;
			Button button = new Button
			{
				Width = 40.0,
				Height = 40.0,
				CornerRadius = new CornerRadius(20.0),
				Padding = new Thickness(0.0),
				UseLayoutRounding = true,
				Background = new SolidColorBrush(ColorFromHex(themePreset.AccentHex)),
				BorderThickness = new Thickness((index == App.Settings.Current.ThemePresetIndex) ? 3 : 0),
				BorderBrush = new SolidColorBrush(Colors.White)
			};
			ToolTipService.SetToolTip(button, themePreset.Name);
			button.Click += async delegate
			{
				App.Settings.Current.ThemePresetIndex = index;
				await App.Settings.SaveAsync();
				App.ApplyThemeResources();
				App.MainWindowInstance?.RefreshThemeDependentUI();
				BuildPresetsList();
			};
			PresetsList.Items.Add(button);
		}
	}

	private static Color ColorFromHex(string hex)
	{
		hex = hex.TrimStart('#');
		return Color.FromArgb(byte.MaxValue, Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
	}

	public void RefreshFoldersList()
	{
		FoldersList.Items.Clear();
		foreach (string folder in App.Settings.Current.MusicFolders)
		{
			Grid grid = new Grid
			{
				ColumnSpacing = 8.0
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			TextBlock textBlock = new TextBlock
			{
				Text = folder,
				VerticalAlignment = VerticalAlignment.Center,
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
			};
			Grid.SetColumn(textBlock, 0);
			Button button = new Button
			{
				Content = "Retirer",
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
				UseLayoutRounding = true
			};
			Grid.SetColumn(button, 1);
			button.Click += async delegate
			{
				App.Settings.Current.MusicFolders.Remove(folder);
				await App.Settings.SaveAsync();
				RefreshFoldersList();
			};
			grid.Children.Add(textBlock);
			grid.Children.Add(button);
			FoldersList.Items.Add(grid);
		}
	}

	private void LanguageFlyoutItem_Click(object sender, RoutedEventArgs e)
	{
		if (_isLoading || sender is not Microsoft.UI.Xaml.Controls.MenuFlyoutItem item) return;
		
		string lang = item.Tag?.ToString() ?? "fr";
		if (App.Settings.Current.AppLanguage != lang)
		{
			App.Settings.Current.AppLanguage = lang;
			App.Settings.SaveSync();
			Resona.Models.Strings.Current.NotifyLanguageChanged();

			LanguageDropDown.Content = lang == "fr" ? Models.Strings.Current.SettingsPage_Content_Franaisfr : Models.Strings.Current.SettingsPage_Content_Englishen;

			// Force ComboBox text refresh: re-select current items so the closed-state text updates
			RefreshComboBoxSelection(DownloadFormatCombo);
			RefreshComboBoxSelection(DownloadBitrateCombo);
		}
	}

	private static void RefreshComboBoxSelection(ComboBox combo)
	{
		if (combo.SelectedItem != null)
		{
			var sel = combo.SelectedItem;
			combo.SelectedItem = null;
			combo.SelectedItem = sel;
		}
	}

	private async void AddFolder_Click(object sender, RoutedEventArgs e)
	{
		FolderPicker folderPicker = new FolderPicker();
		InitializeWithWindow.Initialize(folderPicker, WindowNative.GetWindowHandle(App.MainWindowInstance));
		folderPicker.FileTypeFilter.Add("*");
		StorageFolder storageFolder = await folderPicker.PickSingleFolderAsync();
		if (!(storageFolder == null) && !App.Settings.Current.MusicFolders.Contains(storageFolder.Path))
		{
			App.Settings.Current.MusicFolders.Add(storageFolder.Path);
			await App.Settings.SaveAsync();
			RefreshFoldersList();
			App.MainWindowInstance?.TriggerLibraryRescan();
		}
	}

	private void UpdateTranslateVisibility()
    {
        var vis = LyricsSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        TranslateLyricsSwitch.Visibility = vis;
        TranslateLyricsSeparator.Visibility = vis;
        TranslateLyricsHintText.Visibility = vis;
    }

	private async void NormalizationSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.NormalizationEnabled = NormalizationSwitch.IsOn;
			await App.Settings.SaveAsync();
			App.MainWindowInstance?.ApplyNormalizationSetting();
			if (NormalizationSwitch.IsOn)
			{
				App.MainWindowInstance?.AnalyzeWholeLibraryInBackground();
			}
		}
	}

	private async void LyricsSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.LyricsEnabled = LyricsSwitch.IsOn;
			await App.Settings.SaveAsync();
			App.MainWindowInstance?.ApplyLyricsButtonVisibility();
            UpdateTranslateVisibility();
		}
	}

	private async void TranslateLyricsSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.TranslateLyricsEnabled = TranslateLyricsSwitch.IsOn;
			await App.Settings.SaveAsync();
		}
	}

	private async void CoverSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.AutoFetchMissingCovers = CoverSwitch.IsOn;
			await App.Settings.SaveAsync();
			if (CoverSwitch.IsOn)
			{
				App.MainWindowInstance?.FetchCoversForTracks(App.MainWindowInstance.Library);
			}
		}
	}

	private async void ExclusiveModeSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.ExclusiveAudioMode = ExclusiveModeSwitch.IsOn;
			await App.Settings.SaveAsync();
		}
	}

	private async void NowPlayingSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.AutoOpenNowPlaying = NowPlayingSwitch.IsOn;
			await App.Settings.SaveAsync();
		}
	}

	private async void CategoryCheck_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			AppSettings current = App.Settings.Current;
			current.ShowLibraryCategory = ShowLibraryCheck.IsChecked == true;
			current.ShowAlbumsCategory = ShowAlbumsCheck.IsChecked == true;
			current.ShowPlaylistsCategory = ShowPlaylistsCheck.IsChecked == true;
			current.ShowArtistsCategory = ShowArtistsCheck.IsChecked == true;
			current.ShowGenresCategory = ShowGenresCheck.IsChecked == true;
			current.ShowFoldersCategory = ShowFoldersCheck.IsChecked == true;
			current.ShowStatisticsCategory = ShowStatisticsCheck.IsChecked == true;
			current.ShowDownloadCategory = ShowDownloadCheck.IsChecked == true;
			await App.Settings.SaveAsync();
			App.MainWindowInstance?.RefreshNavCategories();
		}
	}

	private async void ChooseDownloadFolder_Click(object sender, RoutedEventArgs e)
	{
		FolderPicker folderPicker = new FolderPicker();
		InitializeWithWindow.Initialize(folderPicker, WindowNative.GetWindowHandle(App.MainWindowInstance));
		folderPicker.FileTypeFilter.Add("*");
		StorageFolder storageFolder = await folderPicker.PickSingleFolderAsync();
		if (!(storageFolder == null))
		{
			App.Settings.Current.DownloadFolder = storageFolder.Path;
			DownloadFolderBox.Text = storageFolder.Path;
			
			if (!App.Settings.Current.MusicFolders.Contains(storageFolder.Path))
			{
				App.Settings.Current.MusicFolders.Add(storageFolder.Path);
				RefreshFoldersList();
				App.MainWindowInstance?.TriggerLibraryRescan();
			}
			
			await App.Settings.SaveAsync();
		}
	}

	private async void DownloadFormatCombo_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (!_isLoading && DownloadFormatCombo.SelectedItem is ComboBoxItem { Tag: var tag } && Enum.TryParse<DownloadFormat>(tag?.ToString(), out var fmt))
		{
			App.Settings.Current.DownloadFormat = fmt;
			await App.Settings.SaveAsync();
			UpdateFormatHint(fmt);
		}
	}

	private async void DownloadCodecBox_Changed(object sender, TextChangedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.DownloadCodec = DownloadCodecBox.Text.Trim();
			await App.Settings.SaveAsync();
		}
	}

	private async void DownloadBitrateCombo_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (!_isLoading && DownloadBitrateCombo.SelectedItem is ComboBoxItem { Tag: var tag } && Enum.TryParse<DownloadBitrate>(tag?.ToString(), out var result))
		{
			App.Settings.Current.DownloadBitrate = result;
			await App.Settings.SaveAsync();
		}
	}

	private async void BackdropChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isLoading && BackdropChoice.SelectedItem is RadioButton { Tag: var tag } && Enum.TryParse<AppBackdropStyle>(tag?.ToString(), out var style))
		{
			App.Settings.Current.Backdrop = style;
			await App.Settings.SaveAsync();
			App.MainWindowInstance?.ApplyBackdrop();
			App.ApplyThemeResources();
			App.MainWindowInstance?.RefreshThemeDependentUI();
			ColorPresetsPanel.Visibility = ((style != AppBackdropStyle.Solid) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void GradientOverflowSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.PlayerGradientOverflowEnabled = GradientOverflowSwitch.IsOn;
			App.Settings.SaveAsync();
			App.MainWindowInstance?.ApplyGradientOverflowSetting();
		}
	}

	private async void ImportPlaylist_Click(object sender, RoutedEventArgs e)
	{
		FileOpenPicker fileOpenPicker = new FileOpenPicker();
		InitializeWithWindow.Initialize(fileOpenPicker, WindowNative.GetWindowHandle(App.MainWindowInstance));
		fileOpenPicker.FileTypeFilter.Add(".m3u");
		fileOpenPicker.FileTypeFilter.Add(".m3u8");
		StorageFile storageFile = await fileOpenPicker.PickSingleFileAsync();
		if (!(storageFile == null))
		{
			List<string> list;
			List<string> list2;
			var tuple = await App.PlaylistIO.ImportAsync(storageFile.Path); list = tuple.Item1; list2 = tuple.Item2;
			await new ContentDialog
			{
				Title = Models.Strings.Current.IsFr ? "Import termin\u00E9" : "Import complete",
				Content = Models.Strings.Current.IsFr ? $"{list.Count} piste(s) importÃ©e(s)." + ((list2.Count > 0) ? $"\n{list2.Count} piste(s) introuvable(s)." : "") : $"{list.Count} track(s) imported." + ((list2.Count > 0) ? $"\n{list2.Count} track(s) not found." : ""),
				CloseButtonText = "OK",
				XamlRoot = base.XamlRoot
			}.ShowAsync();
		}
	}

	private async void ExportPlaylist_Click(object sender, RoutedEventArgs e)
	{
        List<Playlist> playlists = await App.Cache.LoadAllPlaylistsAsync();
        if (playlists.Count == 0)
        {
            await new ContentDialog
            {
                Title = Models.Strings.Current.IsFr ? "Aucune playlist" : "No playlists",
                Content = Models.Strings.Current.IsFr ? "Aucune playlist Ã  exporter." : "No playlists to export.",
                CloseButtonText = "OK",
                XamlRoot = base.XamlRoot
            }.ShowAsync();
            return;
        }
		FolderPicker folderPicker = new FolderPicker();
		InitializeWithWindow.Initialize(folderPicker, WindowNative.GetWindowHandle(App.MainWindowInstance));
		folderPicker.FileTypeFilter.Add("*");
		StorageFolder folder = await folderPicker.PickSingleFolderAsync();
		if (folder == null)
		{
			return;
		}
		Dictionary<string, Track> byId = (await App.Cache.LoadAllTracksAsync()).ToDictionary((Track t) => t.Id);
		int exported = 0;
		int skipped = 0;
		foreach (Playlist item in playlists)
		{
			List<Track> list = (from id in item.TrackIds
				where byId.ContainsKey(id)
				select byId[id]).ToList();
			if (list.Count == 0)
			{
				skipped++;
				continue;
			}
			string outputPath = System.IO.Path.Combine(path2: string.Concat(string.Concat(item.Name.Split(System.IO.Path.GetInvalidFileNameChars())), ".m3u8"), path1: folder.Path);
			await App.PlaylistIO.ExportAsync(outputPath, list, useRelativePaths: false);
			exported++;
		}
		await new ContentDialog
		{
			Title = Models.Strings.Current.IsFr ? "Export termin\u00E9" : "Export complete",
			Content = Models.Strings.Current.IsFr ? $"{exported} playlist(s) exportÃ©e(s)." + ((skipped > 0) ? $"\n{skipped} playlist(s) vide(s) ignorÃ©e(s)." : "") : $"{exported} playlist(s) exported." + ((skipped > 0) ? $"\n{skipped} empty playlist(s) skipped." : ""),
			CloseButtonText = "OK",
			XamlRoot = base.XamlRoot
		}.ShowAsync();
	}

	private async void MinimizeToTray_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.MinimizeToTrayOnClose = MinimizeToTraySwitch.IsOn;
			await App.Settings.SaveAsync();
			bool flag = MinimizeToTraySwitch.IsOn && StartWithWindowsSwitch.IsOn;
			StartMinimizedSwitch.IsEnabled = flag;
			StartMinimizedHint.Opacity = (flag ? 0.6 : 0.2);
			if (!MinimizeToTraySwitch.IsOn)
			{
				App.Settings.Current.StartMinimized = false;
				StartMinimizedSwitch.IsOn = false;
				await App.Settings.SaveAsync();
			}
		}
	}

	private async void StartWithWindows_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.StartWithWindows = StartWithWindowsSwitch.IsOn;
			await App.Settings.SaveAsync();
			bool flag = MinimizeToTraySwitch.IsOn && StartWithWindowsSwitch.IsOn;
			StartMinimizedSwitch.IsEnabled = flag;
			StartMinimizedHint.Opacity = (flag ? 0.6 : 0.2);
			if (!StartWithWindowsSwitch.IsOn)
			{
				App.Settings.Current.StartMinimized = false;
				StartMinimizedSwitch.IsOn = false;
				await App.Settings.SaveAsync();
			}
			else
			{
				try
				{
					var dialog = new ContentDialog
					{
						Title = Resona.Models.Strings.Current.Dialog_StartWithWindowsTitle,
						XamlRoot = this.XamlRoot,
						RequestedTheme = ActualTheme
					};

					var contentStack = new StackPanel { Spacing = 24, Margin = new Thickness(0, 10, 0, 0) };
					contentStack.Children.Add(new TextBlock
					{
						Text = Resona.Models.Strings.Current.Dialog_StartWithWindowsContent,
						TextWrapping = TextWrapping.Wrap
					});

					var okBtn = new Button
					{
						Content = "OK",
						HorizontalAlignment = HorizontalAlignment.Center,
						Padding = new Thickness(40, 8, 40, 8)
					};
					okBtn.Click += (s, ev) => dialog.Hide();
					
					contentStack.Children.Add(okBtn);
					dialog.Content = contentStack;

					await dialog.ShowAsync();
				}
				catch { }
			}
			App.ApplyStartWithWindowsSetting();
		}
	}

	private async void StartMinimized_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.StartMinimized = StartMinimizedSwitch.IsOn;
			await App.Settings.SaveAsync();
		}
	}

	private void BuildEqualizerSliders()
	{
		EqualizerSliders.Items.Clear();
		StackPanel equalizerPresetsContainer = EqualizerPresetsContainer;
		equalizerPresetsContainer.Children.Clear();
		Button button = new Button
		{
			Content = "Flat",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button.Click += delegate
		{
			ApplyEqPreset(new double[10]);
		};
		Button button2 = new Button
		{
			Content = "Bass Boost",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button2.Click += delegate
		{
			ApplyEqPreset(new double[10] { 6.0, 5.0, 4.0, 2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 });
		};
		Button button3 = new Button
		{
			Content = "Rock",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button3.Click += delegate
		{
			ApplyEqPreset(new double[10] { 5.0, 4.0, 3.0, -1.0, -2.0, -1.0, 2.0, 3.0, 4.0, 4.0 });
		};
		Button button4 = new Button
		{
			Content = "Pop",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button4.Click += delegate
		{
			ApplyEqPreset(new double[10] { -1.0, -1.0, 0.0, 2.0, 4.0, 4.0, 2.0, 0.0, -1.0, -2.0 });
		};
		Button button5 = new Button
		{
			Content = "Electro",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button5.Click += delegate
		{
			ApplyEqPreset(new double[10] { 5.0, 4.0, 1.0, 0.0, -2.0, 0.0, 1.0, 3.0, 4.0, 5.0 });
		};
		Button button6 = new Button
		{
			Content = "Classical",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button6.Click += delegate
		{
			ApplyEqPreset(new double[10] { 3.0, 2.0, 1.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0 });
		};
		Button button7 = new Button
		{
			Content = "Acoustic",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button7.Click += delegate
		{
			ApplyEqPreset(new double[10] { 2.0, 1.0, 0.0, 1.0, 2.0, 2.0, 3.0, 2.0, 1.0, 0.0 });
		};
		Button button8 = new Button
		{
			Content = "Dance",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button8.Click += delegate
		{
			ApplyEqPreset(new double[10] { 4.0, 3.0, 2.0, 0.0, -1.0, 0.0, 2.0, 3.0, 4.0, 4.0 });
		};
		Button button9 = new Button
		{
			Content = "Hip-Hop",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button9.Click += delegate
		{
			ApplyEqPreset(new double[10] { 5.0, 4.0, 1.0, 0.0, -1.0, -1.0, 1.0, 2.0, 3.0, 4.0 });
		};
		Button button10 = new Button
		{
			Content = "Jazz",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button10.Click += delegate
		{
			ApplyEqPreset(new double[10] { 3.0, 2.0, 0.0, 1.0, 2.0, 2.0, 1.0, 0.0, 1.0, 2.0 });
		};
		Button button11 = new Button
		{
			Content = "Vocal",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button11.Click += delegate
		{
			ApplyEqPreset(new double[10] { -2.0, -1.0, 0.0, 1.0, 3.0, 4.0, 3.0, 1.0, 0.0, -1.0 });
		};
		Button button12 = new Button
		{
			Content = "Treble",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button12.Click += delegate
		{
			ApplyEqPreset(new double[10] { -1.0, -1.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0, 5.0 });
		};
		Button button13 = new Button
		{
			Content = "Metal",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button13.Click += delegate
		{
			ApplyEqPreset(new double[10] { 4.0, 3.0, 0.0, -2.0, -3.0, -2.0, 0.0, 2.0, 4.0, 5.0 });
		};
		Button button14 = new Button
		{
			Content = "Party",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button14.Click += delegate
		{
			ApplyEqPreset(new double[10] { 5.0, 5.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 5.0, 5.0 });
		};
		Button button15 = new Button
		{
			Content = "R&B",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button15.Click += delegate
		{
			ApplyEqPreset(new double[10] { 3.0, 5.0, 4.0, 1.0, -1.0, -1.0, 1.0, 2.0, 3.0, 4.0 });
		};
		Button button16 = new Button
		{
			Content = "Spoken Word",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button16.Click += delegate
		{
			ApplyEqPreset(new double[10] { -4.0, -2.0, 0.0, 2.0, 4.0, 5.0, 4.0, 2.0, 0.0, -2.0 });
		};
		Button button17 = new Button
		{
			Content = "Piano",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button17.Click += delegate
		{
			ApplyEqPreset(new double[10] { 2.0, 1.0, 0.0, 1.0, 2.0, 3.0, 4.0, 3.0, 2.0, 1.0 });
		};
		Button button18 = new Button
		{
			Content = "Lofi",
			UseLayoutRounding = true,
			Margin = new Thickness(0.0)
		};
		button18.Click += delegate
		{
			ApplyEqPreset(new double[10] { 4.0, 5.0, 3.0, 0.0, -2.0, -4.0, -5.0, -5.0, -4.0, -3.0 });
		};
		List<Button> list = new List<Button>
		{
			button, button2, button3, button4, button5, button6, button7, button8, button9, button10,
			button11, button12, button13, button14, button15, button16, button17, button18
		};
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			ColumnSpacing = 8.0,
			RowSpacing = 8.0
		};
		int num = 6;
		for (int num2 = 0; num2 < num; num2++)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		int num3 = (int)Math.Ceiling((double)list.Count / (double)num);
		for (int num4 = 0; num4 < num3; num4++)
		{
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
		}
		for (int num5 = 0; num5 < list.Count; num5++)
		{
			list[num5].HorizontalAlignment = HorizontalAlignment.Stretch;
			Grid.SetRow(list[num5], num5 / num);
			Grid.SetColumn(list[num5], num5 % num);
			grid.Children.Add(list[num5]);
		}
		equalizerPresetsContainer.Children.Add(grid);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8.0,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		double[] equalizerBands = App.Settings.Current.EqualizerBands;
		for (int num6 = 0; num6 < 10; num6++)
		{
			int idx = num6;
			StackPanel stackPanel2 = new StackPanel
			{
				Spacing = 4.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				Width = 48.0
			};
			Slider slider = new Slider
			{
				Minimum = -12.0,
				Maximum = 12.0,
				Value = equalizerBands[num6],
				StepFrequency = 1.0,
				Orientation = Orientation.Vertical,
				Height = 160.0,
				Width = 36.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				UseLayoutRounding = true
			};
			slider.ValueChanged += delegate(object s, RangeBaseValueChangedEventArgs args)
			{
				EqualizerBand_ValueChanged(idx, args.NewValue);
			};
			stackPanel2.Children.Add(slider);
			TextBlock item = new TextBlock
			{
				Text = EqualizerLabels[num6],
				FontSize = 10.0,
				Opacity = 0.6,
				HorizontalAlignment = HorizontalAlignment.Center,
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
			};
			stackPanel2.Children.Add(item);
			TextBlock item2 = new TextBlock
			{
				Text = $"{equalizerBands[num6]:+0;-0;0} dB",
				FontSize = 9.0,
				Opacity = 0.5,
				HorizontalAlignment = HorizontalAlignment.Center,
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
			};
			stackPanel2.Children.Add(item2);
			stackPanel.Children.Add(stackPanel2);
		}
		EqualizerSliders.Items.Add(stackPanel);
	}

	private void ApplyEqPreset(double[] newBands)
	{
		App.Settings.Current.EqualizerBands = newBands;
		App.Settings.SaveAsync();
		for (int i = 0; i < 10; i++)
		{
			App.AudioEngine?.SetEqualizerBand(i, (float)newBands[i]);
		}
		BuildEqualizerSliders();
	}

	private void EqualizerBand_ValueChanged(int bandIndex, double newValue)
	{
		if (!_isLoading)
		{
			App.Settings.Current.EqualizerBands[bandIndex] = newValue;
			App.Settings.SaveAsync();
			App.AudioEngine?.SetEqualizerBand(bandIndex, (float)newValue);
		}
	}

	private async void EqualizerSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			bool enabled = EqualizerSwitch.IsOn;
			App.Settings.Current.EqualizerEnabled = enabled;
			await App.Settings.SaveAsync();
			if (enabled)
			{
				BuildEqualizerSliders();
				EqualizerBandsPanel.Visibility = Visibility.Visible;
			}
			else
			{
				EqualizerSliders.Items.Clear();
				EqualizerBandsPanel.Visibility = Visibility.Collapsed;
			}
			App.AudioEngine?.SetEqualizerEnabled(enabled);
		}
	}

	private void AIEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		App.Settings.Current.AIEnabled = AIEnabledSwitch.IsOn;
		App.Settings.SaveAsync();
	}

    
	private void AutoUpdateSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializingUpdates) return;
        App.Settings.Current.AutoUpdateEnabled = AutoUpdateSwitch.IsOn;
        App.Settings.SaveSync();
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await UpdateManager.CheckForUpdatesAsync(this.Content.XamlRoot, true);
    }




    
    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var cbCovers = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheDialogCovers, IsChecked = false };
        var cbLyrics = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheDialogLyrics, IsChecked = false };
        var cbNorm = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheNormalization, IsChecked = false };
        var cbVis = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheVisuallyModified, IsChecked = false };
        var cbScan = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheScannedSounds, IsChecked = false };
        var cbSettings = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheAppSettings, IsChecked = false };
        var cbAll = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheAll, IsChecked = false, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed) };

        cbAll.Checked += (s, ev) => { cbCovers.IsEnabled = cbLyrics.IsEnabled = cbNorm.IsEnabled = cbVis.IsEnabled = cbScan.IsEnabled = cbSettings.IsEnabled = false; };
        cbAll.Unchecked += (s, ev) => { cbCovers.IsEnabled = cbLyrics.IsEnabled = cbNorm.IsEnabled = cbVis.IsEnabled = cbScan.IsEnabled = cbSettings.IsEnabled = true; };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(cbCovers);
        panel.Children.Add(cbLyrics);
        panel.Children.Add(cbNorm);
        panel.Children.Add(cbVis);
        panel.Children.Add(cbScan);
        panel.Children.Add(cbSettings);
        panel.Children.Add(new MenuFlyoutSeparator { Margin = new Thickness(0, 8, 0, 8) });
        panel.Children.Add(cbAll);

        var dialog = new ContentDialog
        {
            Title = Resona.Models.Strings.Current.CS_ClearCacheDialogTitle,
            Content = panel,
            PrimaryButtonText = Resona.Models.Strings.Current.CS_Delete,
            CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            bool restart = false;
            string restartTitle = Resona.Models.Strings.Current.CS_RestartRequiredTitle;
            string restartBody = Resona.Models.Strings.Current.CS_RestartRequiredBody;
            string restartLanguage = App.Settings.Current.AppLanguage;
            if (cbAll.IsChecked == true)
            {
                await App.Cache.ClearAllDataAsync();
                App.Settings.Current = new Resona.Models.AppSettings();
                App.Settings.SaveSync();
                try {
                    string coversDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "Covers");
                    if (System.IO.Directory.Exists(coversDir)) System.IO.Directory.Delete(coversDir, true);
                } catch {}
                restart = true;
            }
            else
            {
                if (cbLyrics.IsChecked == true) await App.Cache.ClearLyricsCacheAsync();
                if (cbNorm.IsChecked == true) await App.Cache.ClearAnalysisAsync();
                if (cbVis.IsChecked == true) await App.Cache.ClearVisuallyModifiedTagsAsync();
                if (cbScan.IsChecked == true)
                {
                    await App.Cache.ClearAllDataAsync();
                    App.Settings.Current.MusicFolders.Clear();
                    App.Settings.SaveSync();
                    restart = true;
                }
                if (cbSettings.IsChecked == true)
                {
                    App.Settings.Current = new Resona.Models.AppSettings();
                    App.Settings.SaveSync();
                    restart = true;
                }
                if (cbCovers.IsChecked == true)
                {
                    await App.Cache.ClearCoversCacheAsync();
                    try {
                        string coversDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "Covers");
                        if (System.IO.Directory.Exists(coversDir)) {
                            foreach (var file in System.IO.Directory.GetFiles(coversDir)) System.IO.File.Delete(file);
                        }
                    } catch {}
                }
            }

            if (restart)
            {
                if (App.Settings.Current.AppLanguage == null && restartLanguage != null) {
                    App.Settings.Current.AppLanguage = restartLanguage;
                    App.Settings.SaveSync();
                }
                var rDialog = new ContentDialog
                {
                    Title = restartTitle,
                    Content = restartBody,
                    PrimaryButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await rDialog.ShowAsync();
                Application.Current.Exit();
            }
        }
    }


    private async void ApplyMetadata_Click(object sender, RoutedEventArgs e)
    {
        var cbTags = new CheckBox { Content = Resona.Models.Strings.Current.CS_ApplyMetadataDialogTags, IsChecked = true };
        var cbCovers = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheDialogCovers, IsChecked = true };
        
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(cbTags);
        panel.Children.Add(cbCovers);

        var dialog = new ContentDialog
        {
            Title = Resona.Models.Strings.Current.CS_ApplyMetadataDialogTitle,
            Content = panel,
            PrimaryButtonText = Resona.Models.Strings.Current.SettingsPage_Text_ApplyMetadataButton,
            CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            bool applyTags = cbTags.IsChecked == true;
            bool applyCovers = cbCovers.IsChecked == true;
            if (!applyTags && !applyCovers) return;

            var progressDialog = new ContentDialog
            {
                Title = Resona.Models.Strings.Current.CS_ApplyMetadataDialogProgress,
                Content = new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center },
                XamlRoot = this.XamlRoot
            };
            _ = progressDialog.ShowAsync();

            var tracks = await App.Cache.LoadAllTracksAsync();
            await Task.Run(() => {
                foreach (var track in tracks)
                {
                    var data = new Resona.Services.AutoTagResult();
                    if (applyTags)
                    {
                        data.Title = track.Title;
                        data.Artist = track.Artist;
                        data.Album = track.Album;
                        data.Genre = track.Genre;
                        data.Year = track.Year > 0 ? track.Year : null;
                        data.TrackNumber = track.TrackNumber > 0 ? track.TrackNumber : null;
                    }
                    if (applyCovers && !string.IsNullOrEmpty(track.CoverArtPath) && System.IO.File.Exists(track.CoverArtPath))
                    {
                        data.CoverPath = track.CoverArtPath;
                    }

                    if (applyTags || (applyCovers && data.CoverPath != null))
                    {
                        Resona.Services.AutoTagService.WriteMetadata(track.FilePath, data);
                    }
                }
            });

            progressDialog.Hide();
        }
    }
}

}
