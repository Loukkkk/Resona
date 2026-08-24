using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Resona.Models;
using Resona.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WinRT;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;

namespace Resona.Views;

public sealed partial class DownloadPage : Page
{
	private bool _isDownloading;

	public DownloadPage()
	{
		InitializeComponent();
		base.Loaded += async delegate
		{
			await CheckAndInstallBinariesAsync();
		};
	}

	protected override void OnNavigatedFrom(NavigationEventArgs e)
	{
		base.OnNavigatedFrom(e);
		SearchResultsPanel.Children.Clear();
	}

	private void TabUrl_Click(object sender, RoutedEventArgs e)
	{
		PanelUrl.Visibility = Visibility.Visible;
		PanelSearch.Visibility = Visibility.Collapsed;
		SearchResultsPanel.Visibility = Visibility.Collapsed;
		TabUrlButton.Background = (Brush)Application.Current.Resources["AppAccentBrush"];
		TabUrlButton.Foreground = (Brush)Application.Current.Resources["AppAccentForegroundBrush"];
		TabSearchButton.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
		TabSearchButton.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
		DownloadButton.Visibility = Visibility.Visible;
	}

	private void TabSearch_Click(object sender, RoutedEventArgs e)
	{
		PanelUrl.Visibility = Visibility.Collapsed;
		PanelSearch.Visibility = Visibility.Visible;
		TabSearchButton.Background = (Brush)Application.Current.Resources["AppAccentBrush"];
		TabSearchButton.Foreground = (Brush)Application.Current.Resources["AppAccentForegroundBrush"];
		TabUrlButton.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
		TabUrlButton.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
		DownloadButton.Visibility = Visibility.Collapsed;
	}

	private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Enter)
		{
			SearchButton_Click(sender, e);
		}
	}

	private async void SearchButton_Click(object sender, RoutedEventArgs e)
	{
		string text = SearchBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		SearchResultsPanel.Children.Clear();
		SearchResultsPanel.Visibility = Visibility.Visible;
		StatusText.Text = Models.Strings.Current.IsFr ? "Recherche en cours..." : "Searching...";
		ProgressRing.IsActive = true;
		try
		{
			List<(string, string, string)> list = await SearchYouTubeAsync(text);
			SearchResultsPanel.Children.Clear();
			if (list.Count == 0)
			{
				SearchResultsPanel.Children.Add(new TextBlock
				{
					Text = Resona.Models.Strings.Current.CS_Aucunrsultat,
					Opacity = 0.6,
					FontSize = 13.0,
					Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
				});
				StatusText.Text = string.Empty;
				return;
			}
			foreach (var item4 in list)
			{
				string item = item4.Item1;
				string item2 = item4.Item2;
				string item3 = item4.Item3;
				Grid grid = new Grid
				{
					ColumnSpacing = 12.0,
					Margin = new Thickness(0.0, 2.0, 0.0, 2.0),
					Padding = new Thickness(12.0, 8.0, 12.0, 8.0),
					Background = new SolidColorBrush(Color.FromArgb(16, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
					CornerRadius = new CornerRadius(8.0)
				};
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				TextBlock textBlock = new TextBlock
				{
					Text = item,
					VerticalAlignment = VerticalAlignment.Center,
					TextTrimming = TextTrimming.CharacterEllipsis,
					MaxLines = 1,
					Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
				};
				TextBlock textBlock2 = new TextBlock
				{
					Text = item2,
					Opacity = 0.5,
					FontSize = 12.0,
					VerticalAlignment = VerticalAlignment.Center,
					Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
				};
				Button button = new Button
				{
					Content = new FontIcon
					{
						Glyph = "\ue896",
						FontSize = 13.0
					},
					Background = (Brush)Application.Current.Resources["AppAccentBrush"],
					Foreground = (Brush)Application.Current.Resources["TextFillColorInverseBrush"],
					BorderThickness = new Thickness(0.0),
					CornerRadius = new CornerRadius(6.0),
					Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
					VerticalAlignment = VerticalAlignment.Center
				};
				ToolTipService.SetToolTip(button, "TÃƒÆ’Ã‚Â©lÃƒÆ’Ã‚Â©charger ce morceau");
				string capturedUrl = item3;
				button.Click += delegate(object s2, RoutedEventArgs e2)
				{
					UrlBox.Text = capturedUrl;
					PanelUrl.Visibility = Visibility.Visible;
					PanelSearch.Visibility = Visibility.Collapsed;
					SearchResultsPanel.Visibility = Visibility.Collapsed;
					TabUrl_Click(s2, e2);
					StartDownloadAsync(capturedUrl);
				};
				Grid.SetColumn(textBlock, 0);
				Grid.SetColumn(textBlock2, 1);
				Grid.SetColumn(button, 2);
				grid.Children.Add(textBlock);
				grid.Children.Add(textBlock2);
				grid.Children.Add(button);
				SearchResultsPanel.Children.Add(grid);
			}
			StatusText.Text = $"{list.Count} rÃƒÆ’Ã‚Â©sultats";
		}
		catch (Exception ex)
		{
			StatusText.Text = Models.Strings.Current.IsFr ? "\u274C " + ex.Message : "\u274C " + ex.Message;
		}
		finally
		{
			ProgressRing.IsActive = false;
		}
	}

	private async Task<List<(string title, string duration, string url)>> SearchYouTubeAsync(string query)
	{
		List<(string, string, string)> results = new List<(string, string, string)>();
		List<string> lines = new List<string>();
		string text = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
		if (!File.Exists(text))
		{
			text = "yt-dlp";
		}
		using Process proc = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = text,
				Arguments = "\"ytsearch10:" + query.Replace("\"", "") + "\" --flat-playlist --no-warnings --print %(title)s|||%(duration_string)s|||%(webpage_url)s",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			}
		};
		proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				lines.Add(e.Data);
			}
		};
		proc.Start();
		proc.BeginOutputReadLine();
		await proc.WaitForExitAsync();
		foreach (string item in lines)
		{
			string[] array = item.Split("|||");
			if (array.Length >= 3 && !string.IsNullOrWhiteSpace(array[2]) && array[2].StartsWith("http"))
			{
				results.Add((array[0].Trim(), array[1].Trim(), array[2].Trim()));
			}
		}
		return results;
	}

	private async Task CheckAndInstallBinariesAsync()
	{
		DownloadService svc = new DownloadService();
		if (DownloadService.IsYtDlpPresent && DownloadService.IsFfmpegPresent)
		{
			StatusText.Text = string.Empty;
			return;
		}
		StatusText.Text = Models.Strings.Current.IsFr ? "Installation de yt-dlp et ffmpeg..." : "Installing yt-dlp and ffmpeg...";
		ProgressRing.IsActive = true;
		await DownloadService.EnsureBinariesAsync(delegate(string line)
		{
			base.DispatcherQueue.TryEnqueue(delegate
			{
				StatusText.Text = line;
			});
		});
		ProgressRing.IsActive = false;
		StatusText.Text = (DownloadService.IsYtDlpPresent ? string.Empty : "ÃƒÆ’Ã‚Â¢Ãƒâ€¦Ã‚Â¡Ãƒâ€šÃ‚Â \ufe0f Installation ÃƒÆ’Ã‚Â©chouÃƒÆ’Ã‚Â©e ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â vÃƒÆ’Ã‚Â©rifiez votre connexion.");
	}

	private async void DownloadButton_Click(object sender, RoutedEventArgs e)
	{
		string url = UrlBox.Text.Trim();
		await StartDownloadAsync(url);
	}

	private async Task StartDownloadAsync(string url)
	{
		if (_isDownloading)
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(url))
		{
			await ShowDialog("URL manquante", "Colle l'URL d'une vidÃƒÆ’Ã‚Â©o ou d'une playlist.");
			return;
		}
		AppSettings current = App.Settings.Current;
		string outputDir = current.DownloadFolder;
		if (!string.IsNullOrWhiteSpace(outputDir) && Directory.Exists(outputDir))
		{
			if (!App.Settings.Current.MusicFolders.Contains(outputDir))
			{
				App.Settings.Current.MusicFolders.Add(outputDir);
				await App.Settings.SaveAsync();
				App.MainWindowInstance?.TriggerLibraryRescan();
			}
			
			_isDownloading = true;
			DownloadButton.IsEnabled = false;
			ProgressRing.IsActive = true;
			StatusText.Text = Models.Strings.Current.IsFr ? "PrÃƒÂ©paration..." : "Preparing...";
			LogBox.Text = string.Empty;
			DownloadOptions opts = new DownloadOptions
			{
				OutputDirectory = outputDir,
				Format = current.DownloadFormat,
				Codec = current.DownloadCodec,
				Bitrate = current.DownloadBitrate
			};
			try
			{
				await new DownloadService().DownloadAsync(url, opts, delegate(string line)
				{
					base.DispatcherQueue.TryEnqueue(delegate
					{
						StatusText.Text = ((line.Length > 90) ? (line.Substring(0, 90) + "...") : line);
						LogBox.Text = line + "\n" + LogBox.Text;
					});
				});
				StatusText.Text = Models.Strings.Current.IsFr ? "\u2705 T\u00E9l\u00E9chargement termin\u00E9 !" : "\u2705 Download complete!";
				await Task.Delay(1000);
				DateTime cutoff = DateTime.UtcNow.AddSeconds(-30.0);
				List<string> list = (from f in Directory.GetFiles(outputDir)
					where File.GetLastWriteTimeUtc(f) >= cutoff
					where f.EndsWith(".mp3") || f.EndsWith(".flac") || f.EndsWith(".opus") || f.EndsWith(".m4a") || f.EndsWith(".wav")
					select f).ToList();
					
				App.MainWindowInstance?.StartScanAnimation();
				foreach (string item in list)
				{
					Track track = App.Scanner.ExtractMetadata(item) ?? new Track
					{
						FilePath = item,
						Title = Path.GetFileNameWithoutExtension(item)
					};
					
					bool isSavedByAutoTag = false;
					if (AutoTagCheckBox.IsChecked == true)
					{
					    isSavedByAutoTag = await App.MainWindowInstance.ShowAutoTagDialogAsync(track);
					}
					
					if (!isSavedByAutoTag)
					{
					    await App.Cache.UpsertTrackAsync(track);
					    App.MainWindowInstance?.AddTrackToLibrary(track);
					}
				}
				App.MainWindowInstance?.TriggerLibraryRescan();
				return;
			}
			catch (Exception ex)
			{
				StatusText.Text = Models.Strings.Current.IsFr ? "\u274C " + ex.Message : "\u274C " + ex.Message;
				return;
			}
			finally
			{
				_isDownloading = false;
				DownloadButton.IsEnabled = true;
				ProgressRing.IsActive = false;
			}
		}
		await ShowDialog("Dossier introuvable", "Configure d'abord le dossier dans ParamÃƒÆ’Ã‚Â¨tres ? TÃƒÆ’Ã‚Â©lÃƒÆ’Ã‚Â©chargement.");
	}

	private async void PasteButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			DataPackageView content = Clipboard.GetContent();
			if (content.Contains(StandardDataFormats.Text))
			{
				TextBox urlBox = UrlBox;
				urlBox.Text = await content.GetTextAsync();
			}
		}
		catch
		{
		}
	}

	private async Task ShowDialog(string title, string message)
	{
		await new ContentDialog
		{
			Title = title,
			Content = message,
			CloseButtonText = "OK",
			XamlRoot = base.XamlRoot
		}.ShowAsync();
	}
}












