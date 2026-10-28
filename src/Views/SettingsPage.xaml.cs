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
        SetupCollapsibleSections();
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
		WeightedShuffleSwitch.IsOn = current.WeightedShuffleEnabled;
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
		WatchFoldersSwitch.IsOn = current.WatchFoldersEnabled;
		InitCategoryReorder();
		SyncCategoryOrderFromSettings();

		UpdateTranslateVisibility();

		// Themes saisonniers : l'option n'est proposee que pendant sa periode (Halloween = octobre, Noel = decembre).
		foreach (RadioButton seasonal in BackdropChoice.Items.Cast<RadioButton>().ToList())
		{
			if (Enum.TryParse<AppBackdropStyle>(seasonal.Tag?.ToString(), out var seasonalStyle) && !SeasonalThemes.IsAvailable(seasonalStyle))
				BackdropChoice.Items.Remove(seasonal);
		}

		foreach (RadioButton item in BackdropChoice.Items.Cast<RadioButton>())
		{
			if (item.Tag?.ToString() == current.Backdrop.ToString())
			{
				BackdropChoice.SelectedItem = item;
				break;
			}
		}
		CustomGradientSwitch.IsOn = current.CustomGradientEnabled;
		BuildGradientStopsList();
		GrainSwitch.IsOn = current.GrainEnabled;
		GrainAmountSlider.Value = Math.Clamp(current.GrainAmount, 5, 100);
		GrainAmountPanel.Visibility = current.GrainEnabled ? Visibility.Visible : Visibility.Collapsed;
		UpdatePresetsPanelVisibility(current.Backdrop);
		GradientOverflowSwitch.IsOn = current.PlayerGradientOverflowEnabled;
		PlayerGradientSwitch.IsOn = current.PlayerGradientEnabled;
		AudioReactiveThemeSwitch.IsOn = current.AudioReactiveThemeEnabled;
		NowPlayingEqualizerSwitch.IsOn = current.NowPlayingEqualizerEnabled;
		UpdateGradientOverflowAvailability();
		MinimizeToTraySwitch.IsOn = current.MinimizeToTrayOnClose;
		SaveWindowPositionSwitch.IsOn = current.SaveWindowPosition;
		SaveWindowSizeSwitch.IsOn = current.SaveWindowSize;
		StartWithWindowsSwitch.IsOn = current.StartWithWindows;
		InitWindowsIntegrationUi(current);
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
        SkipSilenceSwitch.IsOn = current.SkipSilenceEnabled;
        PartyModeSwitch.IsOn = current.PartyModeEnabled;
        UpdatePartyModeUi();
        InitDuckingUi();

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
				BorderThickness = new Thickness((!App.Settings.Current.UseCustomTheme && index == App.Settings.Current.ThemePresetIndex) ? 3 : 0),
				BorderBrush = new SolidColorBrush(Colors.White)
			};
			// Nom du preset affiché dans une infobulle au survol : on force son fond/texte pour
			// qu'elle reste lisible quel que soit le thème (fond foncé, texte blanc), plutot que
			// de dépendre des ressources ToolTip par défaut qui suivent le thème clair/sombre.
			ToolTip presetTooltip = new ToolTip
			{
				Content = Models.Strings.Current.ThemePresetName(themePreset.Name),
				Background = new SolidColorBrush(Color.FromArgb(255, 0x2A, 0x2A, 0x2E)),
				Foreground = new SolidColorBrush(Colors.White)
			};
			ToolTipService.SetToolTip(button, presetTooltip);
			button.Tapped += async delegate
			{
				_customApplyTimer?.Stop();
				App.Settings.Current.UseCustomTheme = false;
				App.Settings.Current.ThemePresetIndex = index;
				await App.Settings.SaveAsync();
				App.ApplyThemeResources();
				App.MainWindowInstance?.RefreshThemeDependentUI();
				// Pas de reconstruction de la liste ici : on ne detruit pas la pastille dont le clic est en cours de traitement.
				UpdatePresetSelectionBorders();
				UpdatePresetsPanelVisibility(App.Settings.Current.Backdrop);
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
		// Derniere pastille : couleur personnalisee (ouvre un selecteur de couleur).
		PresetsList.Items.Add(BuildCustomSwatch());
	}

	// ------------------------------------------------------------------
	//  Theme de couleur personnalise : selecteur de couleur + fond en degrade
	// ------------------------------------------------------------------

	private SolidColorBrush? _customSwatchBrush;
	private FontIcon? _customSwatchIcon;
	private Border? _customSwatch;
	private DispatcherQueueTimer? _customApplyTimer;

	private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

	private static Color ReadableOn(Color c)
		=> (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 150 ? Color.FromArgb(255, 0x1A, 0x1A, 0x1A) : Colors.White;

	private static ToolTip MakeSwatchTip(string text) => new ToolTip
	{
		Content = text,
		Background = new SolidColorBrush(Color.FromArgb(255, 0x2A, 0x2A, 0x2E)),
		Foreground = new SolidColorBrush(Colors.White)
	};

	private static Color AccentOrDefault(string? hex)
		=> CustomTheme.IsValidHex(hex) ? ColorFromHex(hex!) : ColorFromHex("#7C5CFF");

	private static ColorPicker MakeColorPicker(Color initial)
	{
		// MinSaturation coupe le spectre lui-meme : on ne PEUT plus atteindre la zone "trop blanche" (le bas du graphique).
		// Avant, je laissais le curseur y aller puis je le replacais : colle au bord, la teinte etait perdue (blanc pur = pas de teinte)
		// et le curseur ne reagissait plus aux deplacements horizontaux.
		// Libelles du selecteur et noms de couleurs des infobulles : fournis par le framework, donc alignes sur la langue de l'app.
		App.ApplyFrameworkLanguage();
		var picker = new ColorPicker
		{
			Language = Models.Strings.Current.IsFr ? "fr-FR" : "en-US",
			IsAlphaEnabled = false,
			IsMoreButtonVisible = true,
			IsHexInputVisible = true,
			MinSaturation = (int)Math.Round(CustomTheme.MinPickerSaturation * 100.0)
		};
		// Couleur de depart (ex. ancien blanc pur enregistre) ramenee dans la plage autorisee, sinon le selecteur peut refuser.
		var start = CustomTheme.LimitWhiteness(ToHex(initial), CustomTheme.HueOf(ToHex(initial)));
		picker.Color = start.Changed ? ColorFromHex(start.Hex) : initial;
		LocalizeColorNameTooltip(picker);
		return picker;
	}

	// Infobulle du nom de couleur (celle qui suit le curseur sur le spectre) : le ColorSpectrum WinUI la remplit avec
	// ColorHelper.ToDisplayName, qui suit la langue de WINDOWS et ignore Language / PrimaryLanguageOverride.
	// En version anglaise on remplace donc le texte par le nom CSS anglais le plus proche (ColorNames.English),
	// a chaque fois que le spectrum reecrit le contenu de l'infobulle. En francais on ne touche a rien (nom Windows deja en francais).
	private static void LocalizeColorNameTooltip(ColorPicker picker)
	{
		if (Models.Strings.Current.IsFr) return;

		bool TryHook()
		{
			var spectrum = FindAllOfType<ColorSpectrum>(picker).FirstOrDefault();
			if (spectrum == null) return false;
			// Dans le template WinUI, l'infobulle "ColorNameToolTip" est posee sur l'ellipse de selection (pas sur le spectrum lui-meme).
			var ellipse = FindAllOfType<Ellipse>(spectrum).FirstOrDefault(el => el.Name == "SelectionEllipse");
			if (ellipse == null) return false;
			if (ToolTipService.GetToolTip(ellipse) is not ToolTip tip) return false;
			if ((tip.Tag as string) == "ResonaColorNameLocalized") return true;   // deja branche
			tip.Tag = "ResonaColorNameLocalized";

			bool fixing = false;
			void Fix()
			{
				if (fixing) return;   // evite la boucle : on reecrit nous-memes le contenu ci-dessous
				fixing = true;
				try
				{
					Color c = spectrum.Color;
					string english = ColorNames.English(c.R, c.G, c.B);
					if (!string.Equals(tip.Content as string, english, StringComparison.Ordinal)) tip.Content = english;
				}
				finally { fixing = false; }
			}

			tip.RegisterPropertyChangedCallback(ContentControl.ContentProperty, (s, dp) => Fix());
			Fix();
			return true;
		}

		// Le template du selecteur (donc le ColorSpectrum et son infobulle) n'existe qu'une fois le controle charge.
		picker.Loaded += (s, e) =>
		{
			if (!TryHook()) picker.DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Low, () => TryHook());
		};
	}

	private Border BuildCustomSwatch()
	{
		var cur = App.Settings.Current;
		Color color = AccentOrDefault(cur.CustomAccentHex);
		_customSwatchBrush = new SolidColorBrush(color);
		_customSwatchIcon = new FontIcon { Glyph = "\uE790", FontSize = 18, Foreground = new SolidColorBrush(ReadableOn(color)) };
		var swatch = new Border
		{
			Width = 40.0,
			Height = 40.0,
			CornerRadius = new CornerRadius(20.0),
			UseLayoutRounding = true,
			Background = _customSwatchBrush,
			BorderThickness = new Thickness(cur.UseCustomTheme ? 3 : 0),
			BorderBrush = new SolidColorBrush(Colors.White),
			Child = _customSwatchIcon
		};
		ToolTipService.SetToolTip(swatch, MakeSwatchTip(Models.Strings.Current.SettingsPage_Text_CustomColor));
		swatch.Tapped += (s, e) => OnCustomSwatchTapped();
		_customSwatch = swatch;
		return swatch;
	}

	private void OnCustomSwatchTapped()
	{
		var cur = App.Settings.Current;
		if (!cur.UseCustomTheme)
		{
			cur.UseCustomTheme = true;
			// Index remis a 0 : evite les cas speciaux Noir Absolu / Blanc Pur (7 / 8) tant que le theme perso est actif.
			cur.ThemePresetIndex = 0;
			_ = App.Settings.SaveAsync();
			App.ApplyThemeResources();
			App.MainWindowInstance?.RefreshThemeDependentUI();   // rafraichissement complet, une seule fois (sortie eventuelle du theme blanc)
			UpdatePresetSelectionBorders();
			UpdatePresetsPanelVisibility(cur.Backdrop);
			// Le selecteur s'ouvre APRES le retablissement differe du theme (ForceUIRepaint), pas pendant : ouvrir un flyout
			// pendant que le theme est en train de basculer est risque. La pastille n'est plus reconstruite, donc elle est deja en place.
			DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
			{
				if (_customSwatch != null) ShowAccentFlyout(_customSwatch);
			});
			return;
		}
		if (_customSwatch != null) ShowAccentFlyout(_customSwatch);
	}

	// Met a jour la bordure blanche de selection des pastilles SANS reconstruire la liste.
	private void UpdatePresetSelectionBorders()
	{
		var cur = App.Settings.Current;
		int i = 0;
		foreach (var item in PresetsList.Items)
		{
			if (item is Border b)
			{
				bool selected = ReferenceEquals(b, _customSwatch)
					? cur.UseCustomTheme
					: (!cur.UseCustomTheme && i == cur.ThemePresetIndex);
				b.BorderThickness = new Thickness(selected ? 3 : 0);
			}
			i++;
		}
	}

	// Validation finale (selecteur ferme, ajout/suppression de couleur...) : sauvegarde + application complete UNE fois.
	private void CommitCustomTheme()
	{
		_customApplyTimer?.Stop();
		_ = App.Settings.SaveAsync();
		App.ApplyThemeResources();
		App.MainWindowInstance?.RefreshCustomThemeLight(updatePlayerBar: true);
	}

	private void ShowAccentFlyout(FrameworkElement anchor)
	{
		var picker = MakeColorPicker(AccentOrDefault(App.Settings.Current.CustomAccentHex));
		double lastHue = CustomTheme.HueOf(App.Settings.Current.CustomAccentHex);
		bool adjusting = false;
		picker.ColorChanged += (s, e) =>
		{
			if (adjusting) return;
			// Couleur trop blanche : on la ramene a la limite autorisee et on replace le curseur du selecteur dessus.
			var limited = CustomTheme.LimitWhiteness(ToHex(e.NewColor), lastHue);
			lastHue = limited.Hue;
			Color c = e.NewColor;
			if (limited.Changed)
			{
				c = ColorFromHex(limited.Hex);
				adjusting = true;
				try { picker.Color = c; } finally { adjusting = false; }
			}
			App.Settings.Current.CustomAccentHex = ToHex(c);
			if (_customSwatchBrush != null) _customSwatchBrush.Color = c;
			if (_customSwatchIcon != null) _customSwatchIcon.Foreground = new SolidColorBrush(ReadableOn(c));
			ScheduleCustomThemeApply();
		};
		var flyout = new Flyout { Content = picker, Placement = FlyoutPlacementMode.Bottom };
		flyout.Closed += (s, e) => CommitCustomTheme();
		flyout.ShowAt(anchor);
	}

	// Apercu EN DIRECT pendant le glisser : limite a ~6 mises a jour par seconde et uniquement les couleurs (pas de
	// ForceUIRepaint, pas de ThemeApplied, pas de sauvegarde disque). La validation complete se fait a la fermeture.
	private void ScheduleCustomThemeApply()
	{
		if (_customApplyTimer == null)
		{
			_customApplyTimer = DispatcherQueue.CreateTimer();
			_customApplyTimer.Interval = TimeSpan.FromMilliseconds(160);
			_customApplyTimer.IsRepeating = false;
			_customApplyTimer.Tick += (s, e) =>
			{
				App.ApplyThemeResources(notify: false);
				App.MainWindowInstance?.RefreshCustomThemeLight(updatePlayerBar: false);
			};
		}
		if (!_customApplyTimer.IsRunning) _customApplyTimer.Start();   // pas de Stop/Start : sinon aucun apercu tant que ca bouge
	}

	private void GrainSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoading) return;
		App.Settings.Current.GrainEnabled = GrainSwitch.IsOn;
		GrainAmountPanel.Visibility = GrainSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
		_ = App.Settings.SaveAsync();
		App.MainWindowInstance?.ApplyGrain();
	}

	private void GrainAmountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
	{
		// IsLoaded : le slider notifie aussi pendant InitializeComponent (valeur 0 -> minimum), il ne faut pas ecraser le reglage.
		if (_isLoading || !IsLoaded) return;
		App.Settings.Current.GrainAmount = (int)e.NewValue;
		_ = App.Settings.SaveAsync();
		App.MainWindowInstance?.ApplyGrain();   // ne change que l'opacite de la couche : instantane
	}

	private void CustomGradientSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoading) return;
		var cur = App.Settings.Current;
		cur.CustomGradientEnabled = CustomGradientSwitch.IsOn;
		if (cur.CustomGradientEnabled && cur.CustomGradientColors.Count < CustomTheme.MinGradientColors)
		{
			string first = CustomTheme.IsValidHex(cur.CustomAccentHex) ? cur.CustomAccentHex : "#7C5CFF";
			cur.CustomGradientColors = new List<string> { first, CustomTheme.ShiftHue(first, 50) };
		}
		BuildGradientStopsList();
		UpdatePresetsPanelVisibility(cur.Backdrop);
		CommitCustomTheme();
	}

	private void BuildGradientStopsList()
	{
		GradientStopsList.Items.Clear();
		var cur = App.Settings.Current;
		for (int i = 0; i < cur.CustomGradientColors.Count; i++)
		{
			int index = i;
			var brush = new SolidColorBrush(AccentOrDefault(cur.CustomGradientColors[i]));
			var swatch = new Border
			{
				Width = 36.0,
				Height = 36.0,
				CornerRadius = new CornerRadius(18.0),
				UseLayoutRounding = true,
				Background = brush,
				BorderThickness = new Thickness(1.5),
				BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255))
			};
			swatch.Tapped += (s, e) => ShowGradientStopFlyout(swatch, brush, index);
			GradientStopsList.Items.Add(swatch);
		}
		if (cur.CustomGradientColors.Count < CustomTheme.MaxGradientColors)
		{
			var add = new Border
			{
				Width = 36.0,
				Height = 36.0,
				CornerRadius = new CornerRadius(18.0),
				UseLayoutRounding = true,
				Background = new SolidColorBrush(Colors.Transparent),   // fond non nul : sinon le clic ne passe pas dans le vide
				BorderThickness = new Thickness(1.5),
				BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
				Child = new FontIcon { Glyph = "\uE710", FontSize = 14, Foreground = new SolidColorBrush(Colors.White) }
			};
			ToolTipService.SetToolTip(add, MakeSwatchTip(Models.Strings.Current.SettingsPage_Text_AddGradientColor));
			add.Tapped += (s, e) =>
			{
				var c = App.Settings.Current;
				if (c.CustomGradientColors.Count >= CustomTheme.MaxGradientColors) return;
				string last = c.CustomGradientColors.Count > 0 ? c.CustomGradientColors[c.CustomGradientColors.Count - 1] : c.CustomAccentHex;
				c.CustomGradientColors.Add(CustomTheme.ShiftHue(last, 40));
				BuildGradientStopsList();
				CommitCustomTheme();
			};
			GradientStopsList.Items.Add(add);
		}
	}

	private void ShowGradientStopFlyout(FrameworkElement anchor, SolidColorBrush swatchBrush, int index)
	{
		var cur = App.Settings.Current;
		if (index < 0 || index >= cur.CustomGradientColors.Count) return;
		var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
		var picker = MakeColorPicker(AccentOrDefault(cur.CustomGradientColors[index]));
		double lastHue = CustomTheme.HueOf(cur.CustomGradientColors[index]);
		bool adjusting = false;
		picker.ColorChanged += (s, e) =>
		{
			if (adjusting) return;
			var c = App.Settings.Current;
			if (index >= c.CustomGradientColors.Count) return;
			// Meme limite "pas trop blanc" que pour la couleur principale.
			var limited = CustomTheme.LimitWhiteness(ToHex(e.NewColor), lastHue);
			lastHue = limited.Hue;
			Color chosen = e.NewColor;
			if (limited.Changed)
			{
				chosen = ColorFromHex(limited.Hex);
				adjusting = true;
				try { picker.Color = chosen; } finally { adjusting = false; }
			}
			c.CustomGradientColors[index] = ToHex(chosen);
			swatchBrush.Color = chosen;
			ScheduleCustomThemeApply();
		};
		var panel = new StackPanel { Spacing = 12 };
		panel.Children.Add(picker);
		if (cur.CustomGradientColors.Count > CustomTheme.MinGradientColors)
		{
			var remove = new Button
			{
				Content = Models.Strings.Current.SettingsPage_Content_RemoveGradientColor,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
			};
			remove.Click += (s, e) =>
			{
				var c = App.Settings.Current;
				if (index < c.CustomGradientColors.Count && c.CustomGradientColors.Count > CustomTheme.MinGradientColors)
				{
					c.CustomGradientColors.RemoveAt(index);
					BuildGradientStopsList();
				}
				flyout.Hide();   // la fermeture declenche la validation (CommitCustomTheme)
			};
			panel.Children.Add(remove);
		}
		flyout.Content = panel;
		flyout.Closed += (s, e) => CommitCustomTheme();
		flyout.ShowAt(anchor);
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
				App.FolderWatcher?.RefreshWatchedFolders();
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
			App.FolderWatcher?.RefreshWatchedFolders();
			App.MainWindowInstance?.TriggerLibraryRescan();
		}
	}

	private async void WatchFolders_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.WatchFoldersEnabled = WatchFoldersSwitch.IsOn;
			await App.Settings.SaveAsync();
			if (WatchFoldersSwitch.IsOn)
			{
				App.FolderWatcher?.Start();
			}
			else
			{
				App.FolderWatcher?.Stop();
			}
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

	private async void WeightedShuffleSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.WeightedShuffleEnabled = WeightedShuffleSwitch.IsOn;
			await App.Settings.SaveAsync();
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
			App.FolderWatcher?.RefreshWatchedFolders();
			
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
			UpdatePresetsPanelVisibility(style);
		}
	}

	// Solid : palette + reglages du gradient. Halloween : seulement les reglages du gradient (la palette est
	// remplacee par le preset Halloween). Mica / Acrylic : rien (accent neutre, flou systeme).
	private void UpdatePresetsPanelVisibility(AppBackdropStyle style)
	{
		ColorPresetsPanel.Visibility = style.IsSolidLike() ? Visibility.Visible : Visibility.Collapsed;
		Visibility palette = (style == AppBackdropStyle.Solid) ? Visibility.Visible : Visibility.Collapsed;
		PresetsTopSeparator.Visibility = palette;
		PresetsTitleText.Visibility = palette;
		PresetsList.Visibility = palette;
		bool solid = style == AppBackdropStyle.Solid;
		bool customActive = solid && App.Settings.Current.UseCustomTheme;
		// Hors theme personnalise : l'option reste VISIBLE mais grisee (meme rendu que les autres interrupteurs indisponibles).
		CustomGradientPanel.Visibility = solid ? Visibility.Visible : Visibility.Collapsed;
		SetToggleAvailable(CustomGradientSwitch, customActive);
		GradientStopsList.Visibility = (customActive && App.Settings.Current.CustomGradientEnabled) ? Visibility.Visible : Visibility.Collapsed;
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

	private void NowPlayingEqualizerSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.NowPlayingEqualizerEnabled = NowPlayingEqualizerSwitch.IsOn;
			_ = App.Settings.SaveAsync();
			App.MainWindowInstance?.ApplyNowPlayingEqualizerSetting();
		}
	}

	private void AudioReactiveThemeSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.AudioReactiveThemeEnabled = AudioReactiveThemeSwitch.IsOn;
			_ = App.Settings.SaveAsync();
			App.MainWindowInstance?.UpdateAudioReactiveThemeState();
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
		fileOpenPicker.FileTypeFilter.Add(".csv");
		fileOpenPicker.FileTypeFilter.Add(".tsv");
		fileOpenPicker.FileTypeFilter.Add(".txt");
		StorageFile storageFile = await fileOpenPicker.PickSingleFileAsync();
		if (storageFile != null)
		{
			var allTracks = await App.Cache.LoadAllTracksAsync();
			var tuple = await App.PlaylistIO.ImportAsync(storageFile.Path, allTracks);
			var resolvedPaths = tuple.Item1;
			var missing = tuple.Item2;
			var byPath = allTracks.ToDictionary(t => t.FilePath, StringComparer.OrdinalIgnoreCase);

			Playlist playlist = new Playlist { Name = System.IO.Path.GetFileNameWithoutExtension(storageFile.Name) };
			if (!string.IsNullOrEmpty(tuple.Item3))
			{
				string appData = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
				string coversDir = System.IO.Path.Combine(appData, "Covers");
				System.IO.Directory.CreateDirectory(coversDir);
				string dest = System.IO.Path.Combine(coversDir, System.IO.Path.GetFileName(tuple.Item3));
				try { System.IO.File.Copy(tuple.Item3, dest, true); playlist.CoverImagePath = dest; } catch { }
			}

			int importedCount = 0;
			foreach (string path in resolvedPaths)
			{
				if (byPath.TryGetValue(path, out Track track))
				{
					playlist.TrackIds.Add(track.Id);
					importedCount++;
				}
			}
			await App.Cache.UpsertPlaylistAsync(playlist);

			await new ContentDialog
			{
				Title = Models.Strings.Current.IsFr ? "Import terminé" : "Import complete",
				Content = Models.Strings.Current.IsFr
					? $"Playlist \"{playlist.Name}\" importée avec {importedCount} piste(s)." + ((missing.Count > 0) ? $"\n{missing.Count} piste(s) introuvable(s) dans la bibliothèque locale." : "")
					: $"Playlist \"{playlist.Name}\" imported with {importedCount} track(s)." + ((missing.Count > 0) ? $"\n{missing.Count} track(s) not found in local library." : ""),
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

    private async void ViewLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var s = Resona.Models.Strings.Current;
        string text = LogService.ReadAll();
        bool hasLog = !string.IsNullOrWhiteSpace(text);

        var box = new TextBox
        {
            Text = hasLog ? text : s.Log_Empty,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            MinWidth = 600,
            Height = 340
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);

        // On ouvre le journal sur les erreurs les plus recentes (fin du fichier).
        box.Loaded += (_, _) =>
        {
            box.UpdateLayout();
            var sv = FindScrollViewer(box);
            sv?.ChangeView(null, sv.ScrollableHeight, null, true);
        };

        var copyButton = new Button { Content = s.Log_Copy };
        copyButton.Click += (_, _) =>
        {
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
            data.SetText(box.Text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
        };

        var folderButton = new Button { Content = s.Log_OpenFolder };
        folderButton.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(LogService.LogDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{LogService.LogDirectory}\"") { UseShellExecute = true });
            }
            catch { }
        };

        var clearButton = new Button { Content = s.Log_Clear };
        clearButton.Click += (_, _) =>
        {
            LogService.Clear();
            box.Text = s.Log_Empty;
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(copyButton);
        buttons.Children.Add(folderButton);
        buttons.Children.Add(clearButton);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(box);
        panel.Children.Add(buttons);

        var dialog = new ContentDialog
        {
            Title = s.SettingsPage_Text_Logs,
            Content = panel,
            CloseButtonText = s.Log_Close,
            CenterCloseButton = true,
            XamlRoot = this.XamlRoot
        };
        dialog.Resources["ContentDialogMaxWidth"] = 760.0;
        await dialog.ShowAsync();
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found != null) return found;
        }
        return null;
    }




    
    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var cbCovers = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheDialogCovers, IsChecked = false };
        var cbLyrics = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheDialogLyrics, IsChecked = false };
        var cbCustomLyrics = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheCustomLyrics, IsChecked = false };
        var cbNorm = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheNormalization, IsChecked = false };
        var cbVis = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheVisuallyModified, IsChecked = false };
        var cbScan = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheScannedSounds, IsChecked = false };
        var cbSettings = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheAppSettings, IsChecked = false };
        var cbAll = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheAll, IsChecked = false, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed) };

        cbAll.Checked += (s, ev) => { cbCovers.IsEnabled = cbLyrics.IsEnabled = cbCustomLyrics.IsEnabled = cbNorm.IsEnabled = cbVis.IsEnabled = cbScan.IsEnabled = cbSettings.IsEnabled = false; };
        cbAll.Unchecked += (s, ev) => { cbCovers.IsEnabled = cbLyrics.IsEnabled = cbCustomLyrics.IsEnabled = cbNorm.IsEnabled = cbVis.IsEnabled = cbScan.IsEnabled = cbSettings.IsEnabled = true; };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(cbCovers);
        panel.Children.Add(cbLyrics);
        panel.Children.Add(cbCustomLyrics);
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
                LyricsOverrideService.ClearAll();
                App.MainWindowInstance?.ClearAllLyricsOffsets();
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
                if (cbLyrics.IsChecked == true)
                {
                    await App.Cache.ClearLyricsCacheAsync();
                    App.MainWindowInstance?.ClearCachedLyricsOffsets();
                }
                if (cbCustomLyrics.IsChecked == true) App.MainWindowInstance?.ClearCustomLyrics();
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
        var cbTags = new CheckBox { Content = Resona.Models.Strings.Current.CS_ApplyMetadataDialogTags, IsChecked = false };
        var cbCovers = new CheckBox { Content = Resona.Models.Strings.Current.CS_ClearCacheDialogCovers, IsChecked = false };
        bool fr = Resona.Models.Strings.Current.IsFr;
        // Paroles : decochees par defaut (ecriture d'un gros texte dans chaque fichier, a demander explicitement).
        var cbLyricsFound = new CheckBox
        {
            Content = fr ? "Paroles trouvées (ouverture de la page des paroles)" : "Found lyrics (from opening the lyrics page)",
            IsChecked = false
        };
        var cbLyricsCustom = new CheckBox
        {
            Content = fr ? "Paroles personnalisées (choisies ou modifiées à la main)" : "Custom lyrics (picked or edited by hand)",
            IsChecked = false
        };
        
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(cbTags);
        panel.Children.Add(cbCovers);
        panel.Children.Add(cbLyricsFound);
        panel.Children.Add(cbLyricsCustom);

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
            bool applyLyricsFound = cbLyricsFound.IsChecked == true;
            bool applyLyricsCustom = cbLyricsCustom.IsChecked == true;
            if (!applyTags && !applyCovers && !applyLyricsFound && !applyLyricsCustom) return;

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
                        // Valeur inconnue (placeholder de l'app) : on ecrit un champ vide dans le fichier.
                        static string Blank(string? v, params string[] placeholders)
                            => string.IsNullOrWhiteSpace(v) || placeholders.Contains(v, StringComparer.OrdinalIgnoreCase) ? string.Empty : v;
                        var st = Resona.Models.Strings.Current;
                        data.Title = Blank(track.Title, "Titre inconnu", "Unknown title");
                        data.Artist = Blank(track.Artist, st.CS_ArtisteInconnu, "Artiste inconnu", "Unknown artist");
                        data.Album = Blank(track.Album, st.CS_AlbumInconnu, "Album inconnu", "Unknown album");
                        data.Genre = Blank(track.Genre, "Genre inconnu", "Unknown genre", "Inconnu", "Unknown");
                        data.Year = track.Year > 0 ? track.Year : null;
                        data.TrackNumber = track.TrackNumber > 0 ? track.TrackNumber : null;
                    }
                    if (applyCovers && !string.IsNullOrEmpty(track.CoverArtPath) && System.IO.File.Exists(track.CoverArtPath))
                    {
                        data.CoverPath = track.CoverArtPath;
                    }

                    // Paroles : les personnalisees l'emportent sur les trouvees quand les deux options sont cochees
                    // (c'est ce que l'app affiche pour ce son).
                    if (applyLyricsCustom && Resona.Services.LyricsOverrideService.TryGet(track.FilePath, out var customLyrics))
                        data.Lyrics = customLyrics.Lyrics;
                    else if (applyLyricsFound && !string.IsNullOrWhiteSpace(track.Lyrics))
                        data.Lyrics = track.Lyrics;

                    if (applyTags || (applyCovers && data.CoverPath != null) || data.Lyrics != null)
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
		App.Settings.Current.TrackGenreMappings.Clear();
		await App.Settings.SaveAsync();

		// Les pages Albums / Artistes / Genres ne reconstruisent leur grille que si la bibliothèque a changé : on invalide leur cache puis on les recharge.
		AlbumsPage.ResetSessionCaches();
		ArtistsPage.ResetSessionCaches();
		GenresPage.ResetSessionCaches();
		App.MainWindowInstance?.RefreshAllLibraryViews();

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
	// ------------------------------------------------------------------
	//  Audio ducking (Traitement audio)
	// ------------------------------------------------------------------
	private bool _duckingUiUpdating;
	private bool _duckingStringsHooked;

	private void InitDuckingUi()
	{
		var cur = App.Settings.Current;
		_duckingUiUpdating = true;
		try
		{
			DuckingSwitch.IsOn = cur.DuckingEnabled;
			DuckingPanel.Visibility = cur.DuckingEnabled ? Visibility.Visible : Visibility.Collapsed;
			DuckingReductionSlider.Value = Math.Clamp(cur.DuckingAmountDb, 3, 40);
			DuckingThresholdSlider.Value = Math.Clamp(cur.DuckingThresholdPercent, 0.5, 20);
			DuckingAttackSlider.Value = Math.Clamp(cur.DuckingAttackMs, 20, 1000);
			DuckingHoldSlider.Value = Math.Clamp(cur.DuckingHoldMs, 0, 3000);
			DuckingReleaseSlider.Value = Math.Clamp(cur.DuckingReleaseMs, 100, 5000);
			DuckingCurveCombo.SelectedIndex = Math.Clamp(cur.DuckingCurve, 0, 2);
		}
		finally { _duckingUiUpdating = false; }

		UpdateDuckingLabels();
		DuckingReductionSlider.ThumbToolTipValueConverter = new DuckingReductionTooltipConverter();
		FillDuckingApps(new List<string>());
		if (!_duckingStringsHooked)
		{
			_duckingStringsHooked = true;
			Resona.Models.Strings.Current.PropertyChanged += (s, e) => DispatcherQueue?.TryEnqueue(UpdateDuckingLabels);
		}
		if (cur.DuckingEnabled) _ = RefreshDuckingAppsAsync();
	}

	private void UpdateDuckingLabels()
	{
		var s = Resona.Models.Strings.Current;
		var inv = System.Globalization.CultureInfo.InvariantCulture;
		DuckingReductionLabel.Text = DuckingReductionSlider.Value >= DuckingService.MuteThresholdDb
			? $"{s.SettingsPage_Text_DuckingReduction} : {s.SettingsPage_Text_DuckingMute}"
			: $"{s.SettingsPage_Text_DuckingReduction} : -{DuckingReductionSlider.Value:0} dB";
		DuckingSensitivityLabel.Text = $"{s.SettingsPage_Text_DuckingSensitivity} : {DuckingThresholdSlider.Value.ToString("0.0", inv)} %";
		DuckingAttackLabel.Text = $"{s.SettingsPage_Text_DuckingAttack} : {DuckingAttackSlider.Value:0} ms";
		DuckingHoldLabel.Text = $"{s.SettingsPage_Text_DuckingHold} : {DuckingHoldSlider.Value:0} ms";
		DuckingReleaseLabel.Text = $"{s.SettingsPage_Text_DuckingRelease} : {DuckingReleaseSlider.Value:0} ms";
	}

	private void FillDuckingApps(IEnumerable<string> running)
	{
		string saved = App.Settings.Current.DuckingProcessName ?? "";
		var names = running
			.Concat(string.IsNullOrWhiteSpace(saved) ? Array.Empty<string>() : new[] { saved })
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
			.ToList();
		_duckingUiUpdating = true;
		try
		{
			DuckingAppCombo.Items.Clear();
			foreach (string n in names) DuckingAppCombo.Items.Add(n);
			DuckingAppCombo.SelectedItem = names.FirstOrDefault(n => string.Equals(n, saved, StringComparison.OrdinalIgnoreCase));
		}
		finally { _duckingUiUpdating = false; }
	}

	private async Task RefreshDuckingAppsAsync()
	{
		var apps = await Task.Run(() => DuckingService.GetAudioApps());
		FillDuckingApps(apps);
	}

	private async void DuckingRefresh_Click(object sender, RoutedEventArgs e)
	{
		await RefreshDuckingAppsAsync();
	}

	private void DuckingSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoading || _duckingUiUpdating) return;
		App.Settings.Current.DuckingEnabled = DuckingSwitch.IsOn;
		DuckingPanel.Visibility = DuckingSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
		_ = App.Settings.SaveAsync();
		DuckingService.EnsureStarted();
		if (DuckingSwitch.IsOn) _ = RefreshDuckingAppsAsync();
	}

	private void DuckingAppCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isLoading || _duckingUiUpdating) return;
		if (DuckingAppCombo.SelectedItem is not string name) return;
		App.Settings.Current.DuckingProcessName = name;
		_ = App.Settings.SaveAsync();
	}

	private void DuckingSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		// IsLoaded : les sliders notifient aussi pendant InitializeComponent, il ne faut pas ecraser les reglages.
		if (_isLoading || _duckingUiUpdating || !IsLoaded) return;
		var cur = App.Settings.Current;
		cur.DuckingAmountDb = DuckingReductionSlider.Value;
		cur.DuckingThresholdPercent = DuckingThresholdSlider.Value;
		cur.DuckingAttackMs = (int)DuckingAttackSlider.Value;
		cur.DuckingHoldMs = (int)DuckingHoldSlider.Value;
		cur.DuckingReleaseMs = (int)DuckingReleaseSlider.Value;
		UpdateDuckingLabels();
		_ = App.Settings.SaveAsync();
	}

	private void DuckingCurveCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isLoading || _duckingUiUpdating || DuckingCurveCombo.SelectedIndex < 0) return;
		App.Settings.Current.DuckingCurve = DuckingCurveCombo.SelectedIndex;
		_ = App.Settings.SaveAsync();
	}

	private async void SkipSilenceSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoading)
		{
			App.Settings.Current.SkipSilenceEnabled = SkipSilenceSwitch.IsOn;
			await App.Settings.SaveAsync();
			// Applique tout de suite au son en cours : relance le flux a la meme position et dans le meme etat (lecture / pause).
			try
			{
				var engine = App.AudioEngine;
				var playing = engine?.CurrentTrack;
				if (engine != null && playing != null && engine.State != NAudio.Wave.PlaybackState.Stopped)
					await engine.RunWithFileReleasedAsync(playing.FilePath, () => { });
			}
			catch { }
		}
	}
	/// <summary>Mode soiree : fondu enchaine et saut des silences sont actifs d'office -> affiches actives et grises. Les valeurs enregistrees de l'utilisateur ne sont pas modifiees.</summary>
	private void UpdatePartyModeUi()
	{
		var cur = App.Settings.Current;
		bool party = cur.PartyModeEnabled;
		bool prevLoading = _isLoading;
		_isLoading = true;   // evite que les Toggled ecrivent dans les reglages
		try
		{
			CrossfadeSwitch.IsOn = party || cur.EnableCrossfade;
			SkipSilenceSwitch.IsOn = party || cur.SkipSilenceEnabled;
		}
		finally { _isLoading = prevLoading; }
		CrossfadeSwitch.IsEnabled = !party;
		SkipSilenceSwitch.IsEnabled = !party;
		CrossfadePanel.Visibility = (!party && cur.EnableCrossfade) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;   // mode soiree : duree fixe, curseur masque
	}

	private async void PartyModeSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoading) return;
		var cur = App.Settings.Current;
		cur.PartyModeEnabled = PartyModeSwitch.IsOn;
		await App.Settings.SaveAsync();
		UpdatePartyModeUi();
		// Applique tout de suite au son en cours (saut des silences) : relance le flux a la meme position et dans le meme etat.
		try
		{
			var engine = App.AudioEngine;
			var playing = engine?.CurrentTrack;
			if (engine != null && playing != null && engine.State != NAudio.Wave.PlaybackState.Stopped)
				await engine.RunWithFileReleasedAsync(playing.FilePath, () => { });
		}
		catch { }
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
