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

	private readonly List<(Resona.Models.EqualizerPreset Preset, Button Button)> _eqPresetButtons = new();
	private bool _eqLookWhite;

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
				HookToggleRestColors(this);
				ApplyPureWhiteControlLook();
			});
		};
		App.ThemeApplied += OnAppThemeApplied;
		base.Unloaded += (s, e) => App.ThemeApplied -= OnAppThemeApplied;
	}

	private void OnAppThemeApplied()
	{
		DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Low, ApplyPureWhiteControlLook);
	}

	// Theme blanc pur uniquement : les boutons de cette page recoivent un style visible (fond gris
	// clair + bordure, comme le bouton de tri de la bibliotheque) avec un survol sans flash blanc.
	// Dans les autres themes on revient au style normal (aucun changement).
	private void ApplyPureWhiteControlLook()
	{
		bool white = App.IsPureWhiteTheme;
		var res = Application.Current.Resources;
		Style whiteStyle = white ? res["PureWhiteButtonStyle"] as Style : null;
		foreach (Button btn in FindAllOfType<Button>(this))
		{
			if (btn is DropDownButton)
			{
				// Selecteur de langue : style dedie (fleche sombre fixe, fond qui ne disparait pas au
				// survol). Dans les autres themes on revient au style normal.
				btn.ClearValue(Control.BackgroundProperty);
				string ddKey = white ? "PureWhiteDropDownButtonStyle" : "ThemedDropDownButtonStyle";
				if (res[ddKey] is Style ddStyle) btn.Style = ddStyle;
				else btn.ClearValue(FrameworkElement.StyleProperty);
				continue;
			}
			if (white && whiteStyle != null) btn.Style = whiteStyle;
			else btn.ClearValue(FrameworkElement.StyleProperty);
		}
		foreach (ToggleSwitch ts in FindAllOfType<ToggleSwitch>(this)) { ApplyToggleDisabledBrushes(ts); RefreshToggleTheme(ts); }
		foreach (var kv in _toggleAvail) ApplyToggleAvailable(kv.Key, kv.Value);
		foreach (ComboBox combo in FindAllOfType<ComboBox>(this))
		{
			if (white) combo.Background = new SolidColorBrush(Color.FromArgb(255, 0xE0, 0xE0, 0xE0));
			else combo.ClearValue(Control.BackgroundProperty);
		}
		ApplyCrossfadeSliderLook(white);
		// Egaliseur : sliders reconstruits quand on entre/sort du theme blanc (leurs couleurs de piste en dependent),
		// sinon on rafraichit juste le style des boutons de presets et la bordure du preset actif.
		if (_eqLookWhite != white && App.Settings.Current.EqualizerEnabled && EqualizerSliders != null) BuildEqualizerSliders();
		else UpdateEqPresetHighlight();
	}

	// Theme blanc uni : barre du fondu enchaine -> noir a gauche du rond (partie remplie), blanc a droite.
	// Slider deja affiche : on pose les ressources (etats survol/pression) ET les fills directement sur les
	// parties du template ; hors theme blanc on retire tout pour retrouver le rendu normal.
	// Fills d'origine du template (captures AVANT toute modification) : ClearValue ferait disparaitre la barre,
	// car le template pose ces fills en valeur locale.
	private Brush? _cfFilledOrig, _cfTrackOrig;
	private bool _cfCaptured;

	private void ApplyCrossfadeSliderLook(bool white)
	{
		if (CrossfadeDurationSlider == null) return;
		var slider = CrossfadeDurationSlider;
		string[] valueKeys = { "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed" };
		string[] trackKeys = { "SliderTrackFill", "SliderTrackFillPointerOver", "SliderTrackFillPressed" };
		var dark = new SolidColorBrush(Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
		var light = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xFF, 0xFF));

		// Capture des fills d'origine AVANT de poser nos ressources locales (sinon on capturerait nos couleurs).
		slider.ApplyTemplate();
		var filled = Resona.Helpers.VisualTreeHelperExtensions.FindVisualChild<Rectangle>(slider, "HorizontalDecreaseRect");
		var track = Resona.Helpers.VisualTreeHelperExtensions.FindVisualChild<Rectangle>(slider, "HorizontalTrackRect");
		if (filled == null || track == null) return;
		if (!_cfCaptured)
		{
			_cfFilledOrig = filled.Fill;
			_cfTrackOrig = track.Fill;
			_cfCaptured = true;
		}

		foreach (string k in valueKeys) { if (white) slider.Resources[k] = dark; else slider.Resources.Remove(k); }
		foreach (string k in trackKeys) { if (white) slider.Resources[k] = light; else slider.Resources.Remove(k); }
		if (white)
		{
			filled.Fill = dark;
			track.Fill = light;
		}
		else
		{
			if (_cfFilledOrig != null) filled.Fill = _cfFilledOrig;
			if (_cfTrackOrig != null) track.Fill = _cfTrackOrig;
		}
	}

	private static IEnumerable<T> FindAllOfType<T>(DependencyObject parent) where T : DependencyObject
	{
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T typed) yield return typed;
			foreach (T d in FindAllOfType<T>(child)) yield return d;
		}
	}

	// Le fond "actif" des ToggleSwitch au repos ne suivait pas nos ressources (seul le survol le
	// faisait). On l'applique donc directement sur le rectangle du template, avec les MEMES objets
	// brush que ceux mutes par ApplyThemeResources : la couleur suit le theme en direct, et le
	// survol/pression (animations du template) revient toujours a cette valeur locale.
	private static void HookToggleRestColors(DependencyObject parent)
	{
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is ToggleSwitch toggle)
			{
				toggle.Loaded += (s, e) => ApplyToggleRestColor((ToggleSwitch)s);
				toggle.IsEnabledChanged += (s, e) =>
				{
					var t = (ToggleSwitch)s;
					ApplyToggleDisabledBrushes(t);
					RefreshToggleTheme(t);
				};
				ApplyToggleRestColor(toggle);
			}
			HookToggleRestColors(child);
		}
	}

	private readonly Dictionary<ToggleSwitch, bool> _toggleAvail = new();

	// Interrupteur disponible / grise. En theme blanc uni, l'etat "Disabled" du template ne dessine plus le rail
	// (seul le rond reste visible) : on garde donc l'etat Normal (rail visible), on bloque seulement
	// l'interaction et on attenue l'opacite. Dans les autres themes : IsEnabled classique.
	private void SetToggleAvailable(ToggleSwitch t, bool available)
	{
		_toggleAvail[t] = available;
		ApplyToggleAvailable(t, available);
	}

	private static void ApplyToggleAvailable(ToggleSwitch t, bool available)
	{
		ApplyToggleDisabledBrushes(t); // met a jour les couleurs "Disabled" selon le theme courant (evite les restes du theme blanc)
		if (true) // tous les themes : etat Normal (rail visible) + interaction bloquee + opacite attenuee
		{
			t.IsEnabled = true;
			t.IsHitTestVisible = available;
			t.IsTabStop = available;
			t.Opacity = available ? 1.0 : 0.6;
		}
		else
		{
			t.IsEnabled = available;
			t.IsHitTestVisible = true;
			t.IsTabStop = true;
			t.Opacity = available ? 1.0 : 0.4;
		}
	}

	private static readonly string[] DisabledToggleKeys =
	{
		"ToggleSwitchFillOffDisabled", "ToggleSwitchStrokeOffDisabled", "ToggleSwitchFillOnDisabled",
		"ToggleSwitchStrokeOnDisabled", "ToggleSwitchKnobFillOffDisabled", "ToggleSwitchKnobFillOnDisabled"
	};

	// Etat GRISE (desactive) : les ressources globales ne sont pas prises en compte par le template
	// (voir ci-dessus). On les pose donc localement sur le ToggleSwitch, avec les memes objets brush que
	// ceux mutes par ApplyThemeResources -> en theme blanc uni le rail/contour reste visible.
	private static void ApplyToggleDisabledBrushes(ToggleSwitch toggle)
	{
		// Theme blanc uni : couleurs explicites (les cles du template vivent dans ThemeDictionaries, donc
		// Application.Resources.TryGetValue ne les trouve pas) -> rail gris visible + rond gris fonce.
		if (App.IsPureWhiteTheme)
		{
			SolidColorBrush B(byte r, byte g, byte b) => new SolidColorBrush(Color.FromArgb(255, r, g, b));
			toggle.Resources["ToggleSwitchFillOffDisabled"] = B(0xEC, 0xEC, 0xEC);
			toggle.Resources["ToggleSwitchStrokeOffDisabled"] = B(0x8A, 0x8A, 0x8A);
			toggle.Resources["ToggleSwitchFillOnDisabled"] = B(0xB4, 0xB4, 0xB4);
			toggle.Resources["ToggleSwitchStrokeOnDisabled"] = B(0x8A, 0x8A, 0x8A);
			toggle.Resources["ToggleSwitchKnobFillOffDisabled"] = B(0x8A, 0x8A, 0x8A);
			toggle.Resources["ToggleSwitchKnobFillOnDisabled"] = B(0xFF, 0xFF, 0xFF);
			return;
		}
		foreach (string key in DisabledToggleKeys) toggle.Resources.Remove(key); // autres themes : rendu WinUI par defaut, aucune surcharge
	}

	private static void ApplyToggleDisabledBrushesLegacy(ToggleSwitch toggle)
	{
		var res = Application.Current.Resources;
		foreach (string key in DisabledToggleKeys)
		{
			if (res.TryGetValue(key, out object b) && b is Brush)
				toggle.Resources[key] = b;
		}
	}

	// Thème blanc uni : l'état "Disabled" du template ne dessine pas le rail (seul le rond est visible, quelles que soient
	// les couleurs). On dessine donc nous-memes un contour de rail, ajoute dans le template juste au-dessus de
	// "OuterBorder" (sous le rond), visible uniquement en theme blanc uni quand l'interrupteur est desactive.
	private static void RefreshToggleTheme(ToggleSwitch toggle)
	{
		toggle.ApplyTemplate();
		var outer = Resona.Helpers.VisualTreeHelperExtensions.FindVisualChild<Rectangle>(toggle, "OuterBorder");
		if (outer == null || VisualTreeHelper.GetParent(outer) is not Grid host) return;

		Rectangle overlay = null;
		int outerIndex = -1;
		for (int i = 0; i < host.Children.Count; i++)
		{
			if (ReferenceEquals(host.Children[i], outer)) outerIndex = i;
			if (host.Children[i] is Rectangle r && (r.Tag as string) == "ResonaDisabledRail") overlay = r;
		}

		bool show = App.IsPureWhiteTheme && !toggle.IsEnabled;
		if (overlay == null)
		{
			if (!show || outerIndex < 0) return;
			overlay = new Rectangle
			{
				Tag = "ResonaDisabledRail",
				IsHitTestVisible = false,
				RadiusX = outer.RadiusX,
				RadiusY = outer.RadiusY,
				Width = outer.Width,
				Height = outer.Height,
				HorizontalAlignment = outer.HorizontalAlignment,
				VerticalAlignment = outer.VerticalAlignment,
				Margin = outer.Margin,
				StrokeThickness = 1.5,
				Stroke = new SolidColorBrush(Color.FromArgb(255, 0x8A, 0x8A, 0x8A)),
				Visibility = Visibility.Collapsed
			};
			Grid.SetRow(overlay, Grid.GetRow(outer));
			Grid.SetColumn(overlay, Grid.GetColumn(outer));
			Grid.SetRowSpan(overlay, Grid.GetRowSpan(outer));
			Grid.SetColumnSpan(overlay, Grid.GetColumnSpan(outer));
			host.Children.Insert(outerIndex + 1, overlay);
		}
		overlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
	}

	private static void ApplyToggleRestColor(ToggleSwitch toggle)
	{
		ApplyToggleDisabledBrushes(toggle);
		RefreshToggleTheme(toggle);
		toggle.ApplyTemplate();
		var bounds = Resona.Helpers.VisualTreeHelperExtensions.FindVisualChild<Rectangle>(toggle, "SwitchKnobBounds");
		if (bounds == null) return;
		var res = Application.Current.Resources;
		if (res.TryGetValue("ToggleSwitchFillOn", out object fill) && fill is Brush fillBrush)
			bounds.Fill = fillBrush;
		if (res.TryGetValue("ToggleSwitchStrokeOn", out object stroke) && stroke is Brush strokeBrush)
			bounds.Stroke = strokeBrush;
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
		PlayerGradientSwitch.IsOn = current.PlayerGradientEnabled;
		UpdateGradientOverflowAvailability();
		MinimizeToTraySwitch.IsOn = current.MinimizeToTrayOnClose;
		SaveWindowPositionSwitch.IsOn = current.SaveWindowPosition;
		SaveWindowSizeSwitch.IsOn = current.SaveWindowSize;
		StartWithWindowsSwitch.IsOn = current.StartWithWindows;
		StartMinimizedSwitch.IsOn = current.StartMinimized;
		bool flag = current.MinimizeToTrayOnClose && current.StartWithWindows;
		UpdateStartMinimizedAvailability(flag);
        ShowMiniPlayerSwitch.IsOn = current.EnableMiniPlayerButton;
        MiniPlayerAlwaysOnTopSwitch.IsOn = current.MiniPlayerAlwaysOnTop;
        UpdateMiniPlayerAlwaysOnTopVisibility();
        ShowFavoriteButtonSwitch.IsOn = current.EnableFavoriteButton;
        ShowEqualizerQuickButtonSwitch.IsOn = current.EnableEqualizerQuickButton;
        DiscordRpcSwitch.IsOn = current.EnableDiscordRichPresence;
        CrossfadeSwitch.IsOn = current.EnableCrossfade;
        CrossfadeDurationSlider.Value = current.CrossfadeDurationSeconds > 0 ? current.CrossfadeDurationSeconds : 500;
        CrossfadePanel.Visibility = current.EnableCrossfade ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

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
		UpNextToggle.IsOn = current.EnableUpNextPanel;
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
			SolidColorBrush presetBrush = new SolidColorBrush(ColorFromHex(themePreset.AccentHex));
			// Utilise un Border (pas un Button) : le Button WinUI applique inconditionnellement
			// son template par defaut (VisualStates PointerOver/Pressed, effet Reveal) qui
			// continuait a masquer/estomper la couleur du preset au survol en theme blanc pur,
			// meme en fixant Background/Opacity a chaque evenement pointeur. Un Border n'a
			// AUCUN template ni VisualState herite : sa couleur ne peut pas etre alteree par
			// autre chose que ce qu'on lui assigne nous-memes ci-dessous.
			Border button = new Border
			{
				Width = 40.0,
				Height = 40.0,
				CornerRadius = new CornerRadius(20.0),
				UseLayoutRounding = true,
				Background = presetBrush,
				BorderThickness = new Thickness((index == App.Settings.Current.ThemePresetIndex) ? 3 : 0),
				BorderBrush = new SolidColorBrush(Colors.White)
			};
			// Nom du preset affiché dans une infobulle au survol : on force son fond/texte pour
			// qu'elle reste lisible quel que soit le thème (fond foncé, texte blanc), plutot que
			// de dépendre des ressources ToolTip par défaut qui suivent le thème clair/sombre.
			ToolTip presetTooltip = new ToolTip
			{
				Content = themePreset.Name,
				Background = new SolidColorBrush(Color.FromArgb(255, 0x2A, 0x2A, 0x2E)),
				Foreground = new SolidColorBrush(Colors.White)
			};
			ToolTipService.SetToolTip(button, presetTooltip);
			button.Tapped += async delegate
			{
				App.Settings.Current.ThemePresetIndex = index;
				await App.Settings.SaveAsync();
				App.ApplyThemeResources();
				App.MainWindowInstance?.RefreshThemeDependentUI();
				BuildPresetsList();
			};
			// Survol : la pastille s'assombrit legerement, puis revient a sa couleur. On modifie la
			// couleur du brush directement (pas de template WinUI), donc elle ne peut jamais disparaitre.
			Color baseColor = ColorFromHex(themePreset.AccentHex);
			Color hoverColor = Color.FromArgb(255, (byte)(baseColor.R * 0.78), (byte)(baseColor.G * 0.78), (byte)(baseColor.B * 0.78));
			button.PointerEntered += (s, e) => presetBrush.Color = hoverColor;
			button.PointerExited += (s, e) => presetBrush.Color = baseColor;
			button.PointerCanceled += (s, e) => presetBrush.Color = baseColor;
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
				Content = Resona.Models.Strings.Current.SettingsPage_Content_RemoveFolder,
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
		ApplyPureWhiteControlLook();
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

	private void RescanFolders_Click(object sender, RoutedEventArgs e)
	{
		App.MainWindowInstance?.TriggerLibraryRescan();
	}

	private void UpdateTranslateVisibility()
    {
        bool lyricsOn = LyricsSwitch.IsOn;
        SetToggleAvailable(TranslateLyricsSwitch, lyricsOn);
        // (opacite geree par SetToggleAvailable)
        TranslateLyricsHintText.Opacity = lyricsOn ? 0.6 : 0.3;
    }

	private void UpdateMiniPlayerAlwaysOnTopVisibility()
    {
        bool isMiniPlayerOn = ShowMiniPlayerSwitch.IsOn;
        if (MiniPlayerAlwaysOnTopSwitch != null)
        {
            SetToggleAvailable(MiniPlayerAlwaysOnTopSwitch, isMiniPlayerOn);
            // (opacite geree par SetToggleAvailable)
        }
        if (MiniPlayerAlwaysOnTopHintText != null)
        {
            MiniPlayerAlwaysOnTopHintText.Opacity = isMiniPlayerOn ? 0.6 : 0.3;
        }
    }

	private void UpdateStartMinimizedAvailability(bool enabled)
    {
        SetToggleAvailable(StartMinimizedSwitch, enabled);
        // (opacite geree par SetToggleAvailable)
        StartMinimizedHint.Opacity = enabled ? 0.6 : 0.3;
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
			App.MainWindowInstance?.RefreshLyrics();
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

	private void PlayerGradientSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		UpdateGradientOverflowAvailability();
		if (!_isLoading)
		{
			App.Settings.Current.PlayerGradientEnabled = PlayerGradientSwitch.IsOn;
			App.Settings.SaveAsync();
			App.MainWindowInstance?.ApplyGradientOverflowSetting();
		}
	}

	// Le gradient qui dépasse n'a de sens que si le gradient du lecteur est actif : sinon on le grise.
	private void UpdateGradientOverflowAvailability()
	{
		bool innerOn = PlayerGradientSwitch.IsOn;
		SetToggleAvailable(GradientOverflowSwitch, innerOn);
		// (opacite geree par SetToggleAvailable)
		GradientOverflowHint.Opacity = innerOn ? 0.6 : 0.3;
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
				Content = Models.Strings.Current.IsFr ? $"{list.Count} piste(s) importée(s)." + ((list2.Count > 0) ? $"\n{list2.Count} piste(s) introuvable(s)." : "") : $"{list.Count} track(s) imported." + ((list2.Count > 0) ? $"\n{list2.Count} track(s) not found." : ""),
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
                Content = Models.Strings.Current.IsFr ? "Aucune playlist à exporter." : "No playlists to export.",
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
			Content = Models.Strings.Current.IsFr ? $"{exported} playlist(s) exportée(s)." + ((skipped > 0) ? $"\n{skipped} playlist(s) vide(s) ignorée(s)." : "") : $"{exported} playlist(s) exported." + ((skipped > 0) ? $"\n{skipped} empty playlist(s) skipped." : ""),
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
			UpdateStartMinimizedAvailability(flag);
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
			UpdateStartMinimizedAvailability(flag);
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
		_eqLookWhite = App.IsPureWhiteTheme;
		StackPanel equalizerPresetsContainer = EqualizerPresetsContainer;
		equalizerPresetsContainer.Children.Clear();
		// Liste des presets factorisee dans Models.EqualizerPresets (partagee avec le flyout
		// rapide de la PlayerBar), pour eviter toute duplication entre les deux emplacements.
		List<Button> list = new List<Button>();
		_eqPresetButtons.Clear();
		foreach (var preset in Resona.Models.EqualizerPresets.All)
		{
			var presetBands = preset.Bands;
			Button presetButton = new Button
			{
				Content = preset.Name,
				UseLayoutRounding = true,
				Margin = new Thickness(0.0)
			};
			presetButton.Click += delegate
			{
				ApplyEqPreset((double[])presetBands.Clone());
			};
			list.Add(presetButton);
			_eqPresetButtons.Add((preset, presetButton));
		}
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
		UpdateEqPresetHighlight();
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
			ApplyEqSliderLook(slider);
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

	private async void ApplyEqPreset(double[] newBands)
	{
		App.Settings.Current.EqualizerBands = newBands;
		if (!App.Settings.Current.EqualizerEnabled)
		{
			App.Settings.Current.EqualizerEnabled = true;
			App.AudioEngine?.SetEqualizerEnabled(true);
			if (EqualizerSwitch != null) EqualizerSwitch.IsOn = true;
			if (EqualizerBandsPanel != null) EqualizerBandsPanel.Visibility = Visibility.Visible;
		}
		await App.Settings.SaveAsync();
		for (int i = 0; i < 10; i++)
		{
			App.AudioEngine?.SetEqualizerBand(i, (float)newBands[i]);
		}
		BuildEqualizerSliders();
	}

	// Appelee par MainWindow quand un preset d'egaliseur est choisi depuis le flyout rapide
	// de la PlayerBar, pour que cette page (instanciee en permanence, meme masquee) reflete
	// l'etat reel : switch active si besoin, et sliders reconstruits sur les nouvelles bandes.
	public void RefreshEqualizerUI()
	{
		var current = App.Settings.Current;
		// Marque temporairement _isLoading pour que EqualizerSwitch_Toggled ne reagisse pas a
		// ce changement programmatique : le moteur audio et les Settings sont deja a jour a ce
		// stade (appele depuis MainWindow.ApplyEqualizerPresetQuick), seul l'affichage doit
		// se synchroniser ici.
		bool wasLoading = _isLoading;
		_isLoading = true;
		if (EqualizerSwitch != null) EqualizerSwitch.IsOn = current.EqualizerEnabled;
		_isLoading = wasLoading;
		if (EqualizerBandsPanel != null)
		{
			EqualizerBandsPanel.Visibility = current.EqualizerEnabled ? Visibility.Visible : Visibility.Collapsed;
		}
		if (current.EqualizerEnabled)
		{
			BuildEqualizerSliders();
		}
	}

	// Indique quel preset correspond aux bandes actuelles : son bouton est entoure (couleur d'accent du theme,
	// texte pour les themes noir / blanc). Reapplique aussi style + couleur de texte des boutons selon le theme.
	private void UpdateEqPresetHighlight()
	{
		var res = Application.Current.Resources;
		var cur = App.Settings.Current;
		var active = Resona.Models.EqualizerPresets.FindMatch(cur.EqualizerBands);
		bool white = App.IsPureWhiteTheme;
		bool blackOrWhite = white || (cur.Backdrop == AppBackdropStyle.Solid && cur.ThemePresetIndex == 7);
		Brush highlight = (Brush)res[blackOrWhite ? "TextFillColorPrimaryBrush" : "AppAccentBrush"];
		Style? whiteStyle = white ? res["PureWhiteButtonStyle"] as Style : null;
		foreach (var (preset, button) in _eqPresetButtons)
		{
			// Style change seulement si necessaire (cette methode est aussi appelee a chaque deplacement de slider).
			if (whiteStyle != null) { if (!ReferenceEquals(button.Style, whiteStyle)) button.Style = whiteStyle; }
			else if (button.Style != null) button.ClearValue(FrameworkElement.StyleProperty);
			// Meme objet brush que le texte de la page : suit le theme en direct (sombre en blanc uni, clair ailleurs).
			button.Foreground = (Brush)res["TextFillColorPrimaryBrush"];

			if (ReferenceEquals(preset, active))
			{
				button.BorderThickness = new Thickness(2.0);
				button.BorderBrush = highlight;
			}
			else
			{
				button.ClearValue(Control.BorderThicknessProperty);
				button.ClearValue(Control.BorderBrushProperty);
			}
		}
	}

	// Theme blanc uni : les sliders verticaux de l'egaliseur affichaient la piste inversee (noir en haut, blanc en bas).
	// On force : partie remplie (en bas, jusqu'au curseur) = noir, reste de la piste (en haut) = blanc.
	// Poses sur le slider AVANT que son template soit applique (d'ou la reconstruction au changement de theme).
	private static void ApplyEqSliderLook(Slider slider)
	{
		if (!App.IsPureWhiteTheme) return;
		var dark = new SolidColorBrush(Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
		var light = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
		foreach (string k in new[] { "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed" })
			slider.Resources[k] = dark;
		foreach (string k in new[] { "SliderTrackFill", "SliderTrackFillPointerOver", "SliderTrackFillPressed" })
			slider.Resources[k] = light;
	}

	private void EqualizerBand_ValueChanged(int bandIndex, double newValue)
	{
		if (!_isLoading)
		{
			App.Settings.Current.EqualizerBands[bandIndex] = newValue;
			App.Settings.SaveAsync();
			App.AudioEngine?.SetEqualizerBand(bandIndex, (float)newValue);
			UpdateEqPresetHighlight();
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

	private async void RestoreAITagsBtn_Click(object sender, RoutedEventArgs e)
	{
		App.Settings.Current.ArtistMappings.Clear();
		App.Settings.Current.AlbumMappings.Clear();
		App.Settings.Current.GenreMappings.Clear();
		
		App.MainWindowInstance?.TriggerLibraryRescan();

		var successDialog = new ContentDialog
		{
			Title = Models.Strings.Current.IsFr ? "Restauration terminée" : "Restoration complete",
			Content = Models.Strings.Current.IsFr ? "L'affichage superficiel de l'IA a été effacé." : "AI superficial display has been cleared.",
			CloseButtonText = "OK",
			XamlRoot = this.XamlRoot
		};
		await successDialog.ShowAsync();
	}
	private async void SaveWindowSize_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.SaveWindowSize = SaveWindowSizeSwitch.IsOn;
			await App.Settings.SaveAsync();
		}
	}

	private async void SaveWindowPosition_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.SaveWindowPosition = SaveWindowPositionSwitch.IsOn;
			await App.Settings.SaveAsync();
		}
	}
	private void UpNextToggle_Toggled(object sender, RoutedEventArgs e)
	{
		App.Settings.Current.EnableUpNextPanel = UpNextToggle.IsOn;
		_ = App.Settings.SaveAsync();
		if (App.MainWindowInstance != null)
		{
			App.MainWindowInstance.UpdateUpNextPanelVisibility();
		}
	}
	private async void ShowMiniPlayerSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.EnableMiniPlayerButton = ShowMiniPlayerSwitch.IsOn;
			UpdateMiniPlayerAlwaysOnTopVisibility();
			await App.Settings.SaveAsync();
			App.MainWindowInstance?.UpdateMiniPlayerButtonVisibility();
		}
	}

	private async void MiniPlayerAlwaysOnTopSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.MiniPlayerAlwaysOnTop = MiniPlayerAlwaysOnTopSwitch.IsOn;
			await App.Settings.SaveAsync();
		}
	}
	private async void ShowFavoriteButtonSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.EnableFavoriteButton = ShowFavoriteButtonSwitch.IsOn;
			await App.Settings.SaveAsync();
			App.MainWindowInstance?.UpdatePlayerBarButtonsVisibility();
		}
	}
	private async void ShowEqualizerQuickButtonSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.EnableEqualizerQuickButton = ShowEqualizerQuickButtonSwitch.IsOn;
			await App.Settings.SaveAsync();
			App.MainWindowInstance?.UpdatePlayerBarButtonsVisibility();
		}
	}
	private async void DiscordRpcSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.EnableDiscordRichPresence = DiscordRpcSwitch.IsOn;
			await App.Settings.SaveAsync();
            if (DiscordRpcSwitch.IsOn) { App.DiscordRpc?.Initialize(); App.DiscordRpc?.UpdatePresence(App.AudioEngine.CurrentTrack, App.AudioEngine.State == NAudio.Wave.PlaybackState.Playing); }
            else { App.DiscordRpc?.Deinitialize(); }
		}
	}
	private async void CrossfadeSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.EnableCrossfade = CrossfadeSwitch.IsOn;
			await App.Settings.SaveAsync();
			CrossfadePanel.Visibility = CrossfadeSwitch.IsOn ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
		}
	}
	private async void CrossfadeDurationSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.CrossfadeDurationSeconds = (int)e.NewValue; // now in ms
			await App.Settings.SaveAsync();
		}
	}

}
}
