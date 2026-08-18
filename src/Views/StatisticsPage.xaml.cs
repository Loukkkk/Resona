using System;

using System.CodeDom.Compiler;

using System.Collections.Generic;

using System.Diagnostics;

using System.Linq;

using System.Numerics;

using Resona.Models;

using Resona.Services;

using Microsoft.UI.Text;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Controls.Primitives;

using Microsoft.UI.Xaml.Markup;

using Microsoft.UI.Xaml.Media;

using Microsoft.UI.Xaml.Shapes;

using WinRT;



namespace Resona.Views;



public sealed partial class StatisticsPage : Page

{



	public StatisticsPage()

	{

		InitializeComponent();

	}



	public void LoadData(List<Track> library)

	{

		if (library.Count == 0)

		{

			BuildEmpty();

		}

		else

		{

			BuildUI(library);

		}

	}



	private void BuildEmpty()

	{

		ContentPanel.Children.Clear();

		ContentPanel.Children.Add(new TextBlock

		{

			Text = Resona.Models.Strings.Current.CS_Stats_NoTracks,

			Opacity = 0.6,

			HorizontalAlignment = HorizontalAlignment.Center,

			VerticalAlignment = VerticalAlignment.Center,

			FontSize = 16.0,

			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

		});

	}



	private void BuildUI(List<Track> library)

	{

		ContentPanel.Children.Clear();

		TimeSpan timeSpan = TimeSpan.FromTicks(library.Sum((Track t) => t.Duration.Ticks));

		string item = ((timeSpan.TotalHours >= 1.0) ? $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}min" : $"{timeSpan.Minutes}min");

		int num = library.Select((Track t) => t.Artist).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count();

		int num2 = library.Select((Track t) => t.Album).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count();

		PlayStatsService playStats = App.PlayStats;

		long totalPlays = playStats.TotalPlays;

		TimeSpan totalListenTime = playStats.TotalListenTime;

		string value = ((totalListenTime.TotalHours >= 1.0) ? $"{(int)totalListenTime.TotalHours}h {totalListenTime.Minutes}min" : $"{(int)totalListenTime.TotalMinutes}min");

		Grid grid = new Grid

		{

			ColumnSpacing = 12.0

		};

		for (int num3 = 0; num3 < 4; num3++)

		{

			grid.ColumnDefinitions.Add(new ColumnDefinition

			{

				Width = new GridLength(1.0, GridUnitType.Star)

			});

		}

		(string, string, string)[] array = new(string, string, string)[4]

		{

			("\ue8d6", library.Count.ToString(), Resona.Models.Strings.Current.CS_Stats_Tracks),

			("\ue77b", num.ToString(), Resona.Models.Strings.Current.CS_Stats_Artists),

			("\ue93c", num2.ToString(), Resona.Models.Strings.Current.CS_Stats_Albums),

			("\ue916", item, Resona.Models.Strings.Current.CS_Stats_TotalDuration)

		};

		for (int num4 = 0; num4 < array.Length; num4++)

		{

			Grid grid2 = BuildMetricCard(array[num4].Item1, array[num4].Item2, array[num4].Item3);

			Grid.SetColumn(grid2, num4);

			grid.Children.Add(grid2);

		}

		ContentPanel.Children.Add(grid);

		if (totalPlays > 0 || totalListenTime.TotalSeconds > 0.0)

		{

			AddSection(Resona.Models.Strings.Current.CS_Stats_ListeningHeader);

			Grid grid3 = new Grid

			{

				ColumnSpacing = 12.0

			};

			grid3.ColumnDefinitions.Add(new ColumnDefinition

			{

				Width = new GridLength(1.0, GridUnitType.Star)

			});

			grid3.ColumnDefinitions.Add(new ColumnDefinition

			{

				Width = new GridLength(1.0, GridUnitType.Star)

			});

			Track track = playStats.MostPlayedTrack(library);

			Grid grid4 = BuildMetricCard("\ue768", totalPlays.ToString(), Resona.Models.Strings.Current.CS_Stats_Plays);

			Grid grid5 = BuildMetricCard("\ue916", value, Resona.Models.Strings.Current.CS_Stats_ListeningTime);

			Grid.SetColumn(grid4, 0);

			Grid.SetColumn(grid5, 1);

			grid3.Children.Add(grid4);

			grid3.Children.Add(grid5);

			ContentPanel.Children.Add(grid3);

			if (track != null)

			{

				AddSection(Resona.Models.Strings.Current.CS_Stats_MostPlayedTrack);

				Grid grid6 = new Grid

				{

					ColumnSpacing = 12.0,

					Margin = new Thickness(0.0, 4.0, 0.0, 0.0)

				};

				grid6.ColumnDefinitions.Add(new ColumnDefinition

				{

					Width = GridLength.Auto

				});

				grid6.ColumnDefinitions.Add(new ColumnDefinition

				{

					Width = new GridLength(1.0, GridUnitType.Star)

				});

				FontIcon fontIcon = new FontIcon

				{

					Glyph = "\ue734",

					FontSize = 20.0,

					Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],

					VerticalAlignment = VerticalAlignment.Center

				};

				StackPanel stackPanel = new StackPanel

				{

					VerticalAlignment = VerticalAlignment.Center

				};

				stackPanel.Children.Add(new TextBlock

				{

					Text = track.Title,

					FontWeight = FontWeights.SemiBold,

					Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

				});

				TextBlock artistText = new TextBlock 
				{ 
					Text = string.Format(Resona.Models.Strings.Current.CS_Stats_ArtistPlaysFormat, track.Artist, playStats.GetPlayCount(track.Id)), 
					FontSize = 12.0, 
					Opacity = 0.7, 
					TextTrimming = TextTrimming.CharacterEllipsis 
				};

				stackPanel.Children.Add(artistText);

				Grid.SetColumn(fontIcon, 0);

				Grid.SetColumn(stackPanel, 1);

				grid6.Children.Add(fontIcon);

				grid6.Children.Add(stackPanel);

				ContentPanel.Children.Add(grid6);

			}

		}

		else

		{

			ContentPanel.Children.Add(new TextBlock

			{

				Text = Resona.Models.Strings.Current.CS_Stats_NoPlays,

				Opacity = 0.5,

				FontSize = 13.0,

				Margin = new Thickness(0.0, 12.0, 0.0, 0.0),

				TextWrapping = TextWrapping.Wrap,

				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

			});

		}

		List<IGrouping<string, Track>> list = (from g in library.GroupBy<Track, string>((Track t) => t.Artist, StringComparer.OrdinalIgnoreCase)

			orderby g.Count() descending

			select g).Take(10).ToList();

		AddSection(Resona.Models.Strings.Current.CS_Stats_TopArtists);

		int num5 = list[0].Count();

		foreach (IGrouping<string, Track> item2 in list)

		{

			double ratio = (double)item2.Count() / (double)num5;

			ContentPanel.Children.Add(BuildBarRow(item2.Key, item2.Count(), ratio, string.Format(Resona.Models.Strings.Current.CS_Stats_TracksSuffix, item2.Count())));

		}

		List<IGrouping<string, Track>> list2 = (from g in library.GroupBy<Track, string>((Track t) => t.Album, StringComparer.OrdinalIgnoreCase)

			orderby g.Count() descending

			select g).Take(10).ToList();

		AddSection(Resona.Models.Strings.Current.CS_Stats_TopAlbums);

		int num6 = list2[0].Count();

		foreach (IGrouping<string, Track> item3 in list2)

		{

			double ratio2 = (double)item3.Count() / (double)num6;

			string artist = item3.First().Artist;

			ContentPanel.Children.Add(BuildBarRow(item3.Key, item3.Count(), ratio2, artist + " - " + string.Format(Resona.Models.Strings.Current.CS_Stats_TracksSuffix, item3.Count())));

		}

		List<IGrouping<string, Track>> list3 = (from g in library.Where((Track t) => !string.IsNullOrWhiteSpace(t.Genre)).GroupBy<Track, string>((Track t) => t.Genre, StringComparer.OrdinalIgnoreCase)

			orderby g.Count() descending

			select g).Take(8).ToList();

		if (list3.Count <= 0)

		{

			return;

		}

		AddSection("Par genre");

		int num7 = list3[0].Count();

		foreach (IGrouping<string, Track> item4 in list3)

		{

			ContentPanel.Children.Add(BuildBarRow(item4.Key, item4.Count(), (double)item4.Count() / (double)num7, string.Format(Resona.Models.Strings.Current.CS_Stats_TracksSuffix, item4.Count())));

		}

	}



	private void AddSection(string title)

	{

		ContentPanel.Children.Add(new TextBlock

		{

			Text = title,

			FontSize = 18.0,

			FontWeight = FontWeights.SemiBold,

			Margin = new Thickness(0.0, 24.0, 0.0, 8.0),

			Opacity = 0.9,

			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

		});

	}



	private static Grid BuildMetricCard(string glyph, string value, string label)

	{

		Grid grid = new Grid();

		grid.Padding = new Thickness(12.0);

		grid.CornerRadius = new CornerRadius(12.0);

		grid.Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"];

		grid.Translation = new Vector3(0f, 0f, 4f);

		grid.Shadow = new ThemeShadow();

		grid.Padding = new Thickness(16.0);

		StackPanel stackPanel = new StackPanel

		{

			Spacing = 4.0,

			HorizontalAlignment = HorizontalAlignment.Center

		};

		stackPanel.Children.Add(new FontIcon

		{

			Glyph = glyph,

			FontSize = 28.0,

			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

		});

		stackPanel.Children.Add(new TextBlock

		{

			Text = value,

			FontSize = 24.0,

			FontWeight = FontWeights.Bold,

			HorizontalAlignment = HorizontalAlignment.Center,

			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

		});

		stackPanel.Children.Add(new TextBlock

		{

			Text = label,

			FontSize = 12.0,

			Opacity = 0.6,

			HorizontalAlignment = HorizontalAlignment.Center,

			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

		});

		grid.Children.Add(stackPanel);

		return grid;

	}



	private static Grid BuildBarRow(string name, int count, double ratio, string hint)

	{

		Grid obj = new Grid

		{

			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),

			ColumnSpacing = 12.0,

			ColumnDefinitions = 

			{

				new ColumnDefinition

				{

					Width = new GridLength(180.0)

				},

				new ColumnDefinition

				{

					Width = new GridLength(1.0, GridUnitType.Star)

				},

				new ColumnDefinition

				{

					Width = GridLength.Auto

				}

			}

		};

		TextBlock textBlock = new TextBlock

		{

			Text = name,

			VerticalAlignment = VerticalAlignment.Center,

			TextTrimming = TextTrimming.CharacterEllipsis,

			MaxLines = 1,

			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

		};

		Grid.SetColumn(textBlock, 0);

		Grid barBg = new Grid

		{

			Height = 6.0,

			VerticalAlignment = VerticalAlignment.Center

		};

		Rectangle item = new Rectangle

		{

			Fill = (Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],

			RadiusX = 3.0,

			RadiusY = 3.0

		};

		Rectangle barFill = new Rectangle

		{

			Fill = (Brush)Application.Current.Resources["AppAccentBrush"],

			RadiusX = 3.0,

			RadiusY = 3.0,

			HorizontalAlignment = HorizontalAlignment.Left

		};

		barBg.Children.Add(item);

		barBg.Children.Add(barFill);

		barBg.SizeChanged += delegate

		{

			barFill.Width = barBg.ActualWidth * ratio;

		};

		Grid.SetColumn(barBg, 1);

		TextBlock textBlock2 = new TextBlock

		{

			Text = hint,

			FontSize = 12.0,

			Opacity = 0.6,

			VerticalAlignment = VerticalAlignment.Center,

			Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]

		};

		Grid.SetColumn(textBlock2, 2);

		obj.Children.Add(textBlock);

		obj.Children.Add(barBg);

		obj.Children.Add(textBlock2);

		return obj;

	}

}









