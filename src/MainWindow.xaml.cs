using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.Foundation;
using Resona.Models;
using Resona.Services;
using Resona.Views;
using WinRT.Interop;

namespace Resona;

public sealed partial class MainWindow : Window
{
    private List<Track> _library = new();
    private LibraryPage? _libraryPageInstance;
    private AlbumsPage? _albumsPageInstance;
    private PlaylistsPage? _playlistsPageInstance;
    private ArtistsPage? _artistsPageInstance;
    private GenresPage? _genresPageInstance;
    private FoldersPage? _foldersPageInstance;
    private StatisticsPage? _statisticsPageInstance;
    private DownloadPage? _downloadPageInstance;
    private QueuePage? _queuePageInstance;
    private int _currentIndex = -1;
    private string? _nowPlayingId;
    private string? _nowPlayingFilePath;

    private List<Track> _queue = new();
    private int _queueIndex = -1;
    private readonly List<Track> _manualQueue = new();

    private static readonly Dictionary<string, Windows.UI.Color?> _coverColorCache = new();

    private enum PlaybackMode { Off, RepeatAll, RepeatOne, Shuffle }
    private PlaybackMode _playbackMode = PlaybackMode.Off;
    private readonly Random _random = new();
    private DispatcherTimer? _positionTimer;
    private bool _isSliderDragging;

    private record LrcLine(TimeSpan Time, string Text);
    private List<LrcLine> _lrcLines = new();
    private int _lrcCurrentIndex = -1;
    private bool _lyricsOverlayOpen = false;
    private Grid? _lyricsOverlay;
    private TextBlock? _lyricsLinePrev;
    private TextBlock? _lyricsLineCurrent;
    private TextBlock? _lyricsLineNext;
    private TextBlock? _lyricsTrackTitle;
    private TextBlock? _lyricsTrackArtist;
    private ScrollViewer? _lyricsPlainScroll;
    private TextBlock? _lyricsPlainText;
    private HyperlinkButton? _lyricsGoogleBtn;
    private StackPanel? _lyricsSyncedPanel;

    private Grid? _navMainContentGrid;
    private UIElement? _navShadowCaster;

    public static event Action GlobalClickOutside;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Resona";
        AppWindow.SetIcon("icon.ico");
        
        PointerEventHandler globalClickHandler = new PointerEventHandler((s, e) =>
        {
            var el = e.OriginalSource as FrameworkElement;
            if (el != null && el.DataContext is not Track && el.DataContext is not Resona.Models.Playlist)
            {
                GlobalClickOutside?.Invoke();
            }
        });

        this.Content.AddHandler(UIElement.PointerPressedEvent, globalClickHandler, true);
        RootNav.AddHandler(UIElement.PointerPressedEvent, globalClickHandler, true);
        RootNav.ItemInvoked += (s, e) => { GlobalClickOutside?.Invoke(); };

        SetupMinSizeViaWin32();
        ApplyBackdrop();
        ApplyTitleBarTheme();
        RefreshNavCategories();
        ApplyLyricsButtonVisibility();
        
        // Charger le volume par dÃƒÂ©faut depuis les paramÃƒÂ¨tres
        VolumeSlider.Value = App.Settings.Current.Volume;
        App.AudioEngine.SetUserVolume((float)(VolumeSlider.Value / 100.0));
        
        _playbackMode = (PlaybackMode)Math.Clamp(App.Settings.Current.SavedPlaybackMode, 0, 3);

        AlbumsPage.ResetSessionCaches();
        ArtistsPage.ResetSessionCaches();
        GenresPage.ResetSessionCaches();
        FoldersPage.ResetSessionCaches();

        UpdateRepeatButtonVisual();

        RootNav.SizeChanged += (_, _) => UpdateGradientOverflowLayout();
        RootNav.DisplayModeChanged += (_, _) => UpdateGradientOverflowLayout();
        RootNav.Loaded += (_, _) => { ResolveNavigationViewChromeElements(); UpdateGradientOverflowLayout(); };
        PlayerBar.SizeChanged += (_, _) => UpdateGradientOverflowLayout();
        this.SizeChanged += (_, _) => UpdateGradientOverflowLayout();

        SetupPositionTimer();
        ProgressSlider.ThumbToolTipValueConverter = new Resona.Converters.SecondsToTimeStringConverter();
                PlayPauseButton.PointerEntered += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PlayPauseButton, 1.05f);
        PlayPauseButton.PointerExited += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PlayPauseButton, 1.0f);
        PlayPauseButton.PointerPressed += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PlayPauseButton, 0.95f);
                PlayPauseButton.PointerReleased += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PlayPauseButton, 1.05f);

        NowPlayingCoverBorder.PointerEntered += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NowPlayingCoverBorder, 1.05f);
        NowPlayingCoverBorder.PointerExited += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NowPlayingCoverBorder, 1.0f);
        NowPlayingCoverBorder.PointerPressed += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NowPlayingCoverBorder, 0.95f);
        NowPlayingCoverBorder.PointerReleased += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NowPlayingCoverBorder, 1.05f);

        PrevButton.PointerEntered += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PrevButton, 1.05f);
        PrevButton.PointerExited += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PrevButton, 1.0f);
        PrevButton.PointerPressed += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PrevButton, 0.95f);
        PrevButton.PointerReleased += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(PrevButton, 1.05f);

        NextButton.PointerEntered += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NextButton, 1.05f);
        NextButton.PointerExited += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NextButton, 1.0f);
        NextButton.PointerPressed += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NextButton, 0.95f);
        NextButton.PointerReleased += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(NextButton, 1.05f);


        RepeatButton.PointerEntered += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(RepeatButton, 1.05f);
        RepeatButton.PointerExited += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(RepeatButton, 1.0f);
        RepeatButton.PointerPressed += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(RepeatButton, 0.95f);
        RepeatButton.PointerReleased += (s, e) => Resona.Helpers.AnimationHelper.ApplyBouncyScale(RepeatButton, 1.05f);
        PlayPauseButton.PointerExited += (s, e) => PlayPauseButton.Opacity = 1.0;
        PlayPauseButton.PointerPressed += (s, e) => PlayPauseButton.Opacity = 0.7;
        PlayPauseButton.PointerReleased += (s, e) => PlayPauseButton.Opacity = 0.85;
        ProgressSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ProgressSlider_PointerPressed), true);
        ProgressSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ProgressSlider_PointerReleased), true);
        App.AudioEngine.PlaybackStopped += AudioEngine_PlaybackStopped;

        if (!App.Settings.Current.HasCompletedOnboarding)
        {
            ShowOnboarding();
        }
        else
        {
            RootNav.Loaded += OnRootNavFirstLoaded;
            RootNav.SelectedItem = NavLibrary;
            _libraryPageInstance = new LibraryPage();
            ContentFrame.Content = _libraryPageInstance;
            _ = LoadLibraryThenPreloadSettings();
        }
    }

    private void OnRootNavFirstLoaded(object sender, RoutedEventArgs e)
    {
        RootNav.Loaded -= OnRootNavFirstLoaded;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            RootNav.Opacity = 1;
            var sb = new Storyboard();
            var anim = new DoubleAnimation
            {
                From = 0, To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(anim, RootGrid);
            Storyboard.SetTargetProperty(anim, "Opacity");
            sb.Children.Add(anim);
            sb.Begin();
        });
    }

    private async Task LoadLibraryThenPreloadSettings()
    {
        await LoadLibraryFromCacheAsync();
        PreloadSettingsPageInBackground();
    }

    private void PreloadSettingsPageInBackground()
    {
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            SettingsContainer.Opacity = 0;
            SettingsContainer.Visibility = Visibility.Visible;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                SettingsContainer.Visibility = Visibility.Collapsed;
                SettingsContainer.Opacity = 1;
            });
        });
    }

    // ===================== ONBOARDING =====================

    private void ShowOnboarding()
    {
        ContentFrame.Visibility = Visibility.Collapsed;
        OnboardingFrame.Visibility = Visibility.Visible;
        OnboardingFrame.Opacity = 0;
        OnboardingFrame.Navigate(typeof(OnboardingPage));
        RootGrid.Opacity = 1;

        var sb = new Storyboard();
        var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(fadeIn, OnboardingFrame);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        sb.Children.Add(fadeIn);
        sb.Begin();

        if (OnboardingFrame.Content is OnboardingPage page)
        {
            page.OnboardingCompleted += async () =>
            {
                var sbOut = new Storyboard();
                var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                Storyboard.SetTarget(fadeOut, OnboardingFrame);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                sbOut.Children.Add(fadeOut);

                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                sbOut.Completed += (s, e) => tcs.SetResult(true);
                sbOut.Begin();
                await tcs.Task;

                OnboardingFrame.Visibility = Visibility.Collapsed;
                RootNav.Opacity = 1;

                ContentFrame.Opacity = 0;
                ContentFrame.Visibility = Visibility.Visible;
                RootNav.SelectedItem = NavLibrary;
                _libraryPageInstance = new LibraryPage();
                ContentFrame.Content = _libraryPageInstance;

                var sbIn = new Storyboard();
                var fadeInMain = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                Storyboard.SetTarget(fadeInMain, ContentFrame);
                Storyboard.SetTargetProperty(fadeInMain, "Opacity");
                sbIn.Children.Add(fadeInMain);
                sbIn.Begin();

                await LoadLibraryFromCacheAsync();
                PreloadSettingsPageInBackground();
            };
        }
    }

    // ===================== BARRE DE TITRE =====================

    private void ApplyTitleBarTheme()
    {
        try
        {
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            ApplyNativeWindowBackgroundFix();
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;

    // ===================== TAILLE MINIMALE =====================

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _originalWndProc;

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr newProc);
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_GETMINMAXINFO = 0x0024;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public System.Drawing.Point ptReserved;
        public System.Drawing.Point ptMaxSize;
        public System.Drawing.Point ptMaxPosition;
        public System.Drawing.Point ptMinTrackSize;
        public System.Drawing.Point ptMaxTrackSize;
    }

    private const int MinWidthPx  = 1300;
    private const int MinHeightPx = 600;
    private const double GradientFadeHeight = 600;
    private const double ColorLayerExtraPad   = 0;

    private Windows.UI.Color? _gradientStartColor;
    private Windows.UI.Color? _gradientEndColor;

    private static readonly Thickness NavContentBorderDefault = new(1, 1, 0, 0);
    private static readonly Thickness NavContentBorderMinimal = new(0, 1, 0, 0);
    private static readonly Thickness NavContentBorderHidden = new(0);

    private void SetupMinSizeViaWin32()
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            _wndProcDelegate = CustomWndProc;
            _originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        }
        catch { }
    }

    private IntPtr CustomWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMinTrackSize = new System.Drawing.Point(MinWidthPx, MinHeightPx);
            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, false);
        }
        return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }

    private void ApplyNativeWindowBackgroundFix()
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            var preset = ThemePresets.All[Math.Clamp(App.Settings.Current.ThemePresetIndex, 0, ThemePresets.All.Length - 1)];
            string hex = preset.BackgroundHex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            int colorRef = (b << 16) | (g << 8) | r;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref colorRef, sizeof(int));
        }
        catch { }
    }

    // ===================== BACKDROP =====================

    public void ApplyBackdrop()
    {
        var style = App.Settings.Current.Backdrop;
        try
        {
            this.SystemBackdrop = style switch
            {
                AppBackdropStyle.Mica    => new MicaBackdrop { Kind = MicaKind.Base },
                AppBackdropStyle.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                AppBackdropStyle.Acrylic => new DesktopAcrylicBackdrop(),
                _                        => null
            };
        }
        catch { this.SystemBackdrop = null; style = AppBackdropStyle.Solid; }

        bool isSolid = style == AppBackdropStyle.Solid;
        var transparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        RootGrid.Background = isSolid
            ? (Brush)Application.Current.Resources["AppDeepBackgroundBrush"]
            : transparentBrush;

        if (isSolid)
        {
            if (_currentIndex >= 0)
                UpdatePlayerBarColorAsync(_library[_currentIndex]);
            else
                PlayerBar.Background = (Brush)Application.Current.Resources["AppSurfaceBrush"];
        }
        else
        {
            PlayerBar.Background = transparentBrush;
            PlayerGradientOverflow.Visibility = Visibility.Collapsed;
            PlayerGradientFadeLayer.Visibility = Visibility.Collapsed;
            ApplyGradientOverflowChrome(false);
        }

        ApplyTitleBarButtonColors(isSolid);
    }

    private void ApplyTitleBarButtonColors(bool isSolid)
    {
        try
        {
            var titleBar = this.AppWindow?.TitleBar;
            if (titleBar == null) return;
            var transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);

            var preset = ThemePresets.All[Math.Clamp(App.Settings.Current.ThemePresetIndex, 0, ThemePresets.All.Length - 1)];
            var bg = ParseHexColor(preset.BackgroundHex);
            bool isPureWhite = isSolid && (bg.R >= 220 && bg.G >= 220 && bg.B >= 220);
            var fg = isPureWhite ? Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A)
                             : Windows.UI.Color.FromArgb(255, 255, 255, 255);
            var fgInactive = isPureWhite ? Windows.UI.Color.FromArgb(150, 0x1A, 0x1A, 0x1A)
                                     : Windows.UI.Color.FromArgb(150, 255, 255, 255);

            titleBar.BackgroundColor = transparent;
            titleBar.InactiveBackgroundColor = transparent;
            titleBar.ForegroundColor = fg;
            titleBar.InactiveForegroundColor = fgInactive;
            titleBar.ButtonBackgroundColor = transparent;
            titleBar.ButtonInactiveBackgroundColor = transparent;
            titleBar.ButtonForegroundColor = fg;
            titleBar.ButtonInactiveForegroundColor = fgInactive;

            if (isSolid)
            {
                if (isPureWhite)
                {
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 0xD6, 0xD6, 0xD6);
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 0xC4, 0xC4, 0xC4);
                    titleBar.ButtonHoverForegroundColor = fg;
                    titleBar.ButtonPressedForegroundColor = fg;
                }
                else
                {
                    var hoverBg = Windows.UI.Color.FromArgb(255, 
                        (byte)Math.Min(255, bg.R + 40),
                        (byte)Math.Min(255, bg.G + 40),
                        (byte)Math.Min(255, bg.B + 40));
                    var pressedBg = Windows.UI.Color.FromArgb(255, 
                        (byte)Math.Max(0, bg.R - 30),
                        (byte)Math.Max(0, bg.G - 30),
                        (byte)Math.Max(0, bg.B - 30));
                    titleBar.ButtonHoverBackgroundColor = hoverBg;
                    titleBar.ButtonPressedBackgroundColor = pressedBg;
                    titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                    titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                }
            }
            else
            {
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(70, 255, 255, 255);
                titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            }
        }
        catch { }
    }

    // ===================== COULEUR DU PLAYER =====================

    private static Windows.UI.Color? GetAverageColorCpu(string imagePath)
    {
        try
        {
            byte[] fileBytes = System.IO.File.ReadAllBytes(imagePath);
            using var ms = new System.IO.MemoryStream(fileBytes);
            using var bmp = System.Drawing.Image.FromStream(ms, false, false);
            using var small = new System.Drawing.Bitmap(8, 8);
            using var g = System.Drawing.Graphics.FromImage(small);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            g.DrawImage(bmp, 0, 0, 8, 8);
            long sumR = 0, sumG = 0, sumB = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    var px = small.GetPixel(x, y);
                    sumR += px.R; sumG += px.G; sumB += px.B;
                }
            return Windows.UI.Color.FromArgb(255, (byte)(sumR / 64), (byte)(sumG / 64), (byte)(sumB / 64));
        }
        catch { return null; }
    }

    private static Windows.UI.Color Darken(Windows.UI.Color c, double factor)
        => Windows.UI.Color.FromArgb(255, (byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));

    private void UpdatePlayerBarColorAsync(Track track)
    {
        if (App.Settings.Current.Backdrop != AppBackdropStyle.Solid) return;

        var preset = ThemePresets.All[Math.Clamp(App.Settings.Current.ThemePresetIndex, 0, ThemePresets.All.Length - 1)];
        var themeSurface = ParseHexColor(preset.SurfaceHex);

        if (_coverColorCache.TryGetValue(track.Id, out var cachedColor))
        {
            ApplyPlayerBarColor(track.Id, cachedColor, themeSurface);
            return;
        }

        ApplyPlayerBarColor(track.Id, null, themeSurface);

        if (string.IsNullOrEmpty(track.CoverArtPath))
        {
            _coverColorCache[track.Id] = null;
            return;
        }

        var capturedId   = track.Id;
        var capturedPath = track.CoverArtPath;
        _ = Task.Run(() => GetAverageColorCpu(capturedPath)).ContinueWith(t =>
        {
            var avg = t.Result;
            _coverColorCache[capturedId] = avg;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (App.Settings.Current.Backdrop != AppBackdropStyle.Solid) return;
                if (_nowPlayingId != capturedId) return;
                ApplyPlayerBarColor(capturedId, avg, themeSurface);
            });
        }, TaskScheduler.Default);
    }

    private void ApplyPlayerBarColor(string trackId, Windows.UI.Color? avg, Windows.UI.Color themeSurface)
    {
        var baseColor = avg ?? Windows.UI.Color.FromArgb(255, 30, 30, 30);
        var darkened = Darken(baseColor, 0.4);
        var transparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        gradient.GradientStops.Add(new GradientStop { Color = darkened, Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = themeSurface, Offset = 1 });

        bool isSolid = App.Settings.Current.Backdrop == AppBackdropStyle.Solid;
        if (App.Settings.Current.PlayerGradientOverflowEnabled && isSolid)
        {
            _gradientStartColor = darkened;
            _gradientEndColor = themeSurface;

            var overflowGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            overflowGradient.GradientStops.Add(new GradientStop { Color = darkened, Offset = 0 });
            overflowGradient.GradientStops.Add(new GradientStop { Color = themeSurface, Offset = 0.5 });
            overflowGradient.GradientStops.Add(new GradientStop { Color = themeSurface, Offset = 1 });
            PlayerGradientColorLayer.Background = overflowGradient;

            var bgColor = ((SolidColorBrush)Application.Current.Resources["AppDeepBackgroundBrush"]).Color;
            PlayerGradientFadeLayer.Background = BuildBackgroundFadeBrush(bgColor, Bounds.Height);

            PlayerBar.Background = transparentBrush;
            PlayerBar.BorderThickness = new Thickness(0);
            PlayerBar.Margin = new Thickness(0);
            PlayerGradientOverflow.Visibility = Visibility.Visible;
            PlayerGradientFadeLayer.Visibility = Visibility.Visible;
            ApplyGradientOverflowChrome(true);
            UpdateGradientOverflowLayout();
        }
        else
        {
            PlayerBar.Background = isSolid ? gradient : transparentBrush;
            PlayerBar.Margin = new Thickness(0);
            PlayerGradientOverflow.Visibility = Visibility.Collapsed;
            PlayerGradientFadeLayer.Visibility = Visibility.Collapsed;
            ApplyGradientOverflowChrome(false);
        }
    }

    private static LinearGradientBrush BuildBackgroundFadeBrush(Windows.UI.Color bgColor, double containerHeight)
    {
        var fade = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        fade.GradientStops.Add(new GradientStop { Color = bgColor, Offset = 0 });
        fade.GradientStops.Add(new GradientStop { Color = bgColor, Offset = 0.2 });
        fade.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, bgColor.R, bgColor.G, bgColor.B), Offset = 0.6 });
        fade.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, bgColor.R, bgColor.G, bgColor.B), Offset = 1 });
        return fade;
    }

    private static void AddSmoothHorizontalStops(LinearGradientBrush brush, Windows.UI.Color startColor, Windows.UI.Color endColor, double windowWidth)
    {
        brush.GradientStops.Add(new GradientStop { Color = startColor, Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = endColor, Offset = 0.5 });
        brush.GradientStops.Add(new GradientStop { Color = endColor, Offset = 1 });
    }

    private static void AddSmoothVerticalStops(LinearGradientBrush brush, Windows.UI.Color rgb,
        double opaqueUntil, double fadePower)
    {
        const int steps = 48;
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            double alpha = t <= opaqueUntil ? 1.0
                : Math.Pow(1.0 - ((t - opaqueUntil) / (1.0 - opaqueUntil)), fadePower);
            brush.GradientStops.Add(new GradientStop
            {
                Color = Windows.UI.Color.FromArgb((byte)(alpha * 255), rgb.R, rgb.G, rgb.B),
                Offset = t
            });
        }
    }

    private static void AddSmoothVerticalStopsFixed(LinearGradientBrush brush, Windows.UI.Color rgb,
        double opaqueHeightPx, double containerHeightPx, double fadePower)
    {
        const int steps = 48;
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            double pixelOffset = t * containerHeightPx;
            double alpha = pixelOffset <= opaqueHeightPx ? 1.0
                : Math.Pow(1.0 - ((pixelOffset - opaqueHeightPx) / (containerHeightPx - opaqueHeightPx)), fadePower);
            brush.GradientStops.Add(new GradientStop
            {
                Color = Windows.UI.Color.FromArgb((byte)(alpha * 255), rgb.R, rgb.G, rgb.B),
                Offset = t
            });
        }
    }

    private void UpdateGradientOverflowLayout()
    {
        if (PlayerGradientOverflow.Visibility != Visibility.Visible) return;
        double playerH = PlayerBar.ActualHeight > 0 ? PlayerBar.ActualHeight : 88;
        
        double windowHeight = Bounds.Height;
        double fadeHeight = Math.Min(windowHeight * 0.6, 800);
        PlayerGradientColorLayer.Height = fadeHeight + playerH;
        PlayerGradientFadeLayer.Height = fadeHeight;

        if (_gradientStartColor.HasValue && _gradientEndColor.HasValue)
        {
            var overflowGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            overflowGradient.GradientStops.Add(new GradientStop { Color = _gradientStartColor.Value, Offset = 0 });
            overflowGradient.GradientStops.Add(new GradientStop { Color = _gradientEndColor.Value, Offset = 0.5 });
            overflowGradient.GradientStops.Add(new GradientStop { Color = _gradientEndColor.Value, Offset = 1 });
            PlayerGradientColorLayer.Background = overflowGradient;
        }

        var bgColor = ((SolidColorBrush)Application.Current.Resources["AppDeepBackgroundBrush"]).Color;
        PlayerGradientFadeLayer.Background = BuildBackgroundFadeBrush(bgColor, fadeHeight);
    }

    private void ResolveNavigationViewChromeElements()
    {
        if (_navMainContentGrid != null) return;

        Grid? largest = null;
        double largestArea = 0;
        foreach (var fe in WalkVisualTree(RootNav))
        {
            if (fe is not Grid grid || grid.Name != "ContentGrid") continue;
            double area = grid.ActualWidth * grid.ActualHeight;
            if (area > largestArea) { largestArea = area; largest = grid; }
        }
        _navMainContentGrid = largest;

        foreach (var fe in WalkVisualTree(RootNav))
        {
            if (fe is Grid g && g.Name == "ShadowCaster")
            {
                _navShadowCaster = g;
                break;
            }
        }
    }

    private static IEnumerable<FrameworkElement> WalkVisualTree(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe)
            {
                yield return fe;
                foreach (var nested in WalkVisualTree(fe))
                    yield return nested;
            }
            else
            {
                foreach (var nested in WalkVisualTree(child))
                    yield return nested;
            }
        }
    }

    private void ApplyNavigationViewChrome(bool enabled)
    {
        ResolveNavigationViewChromeElements();

        if (_navMainContentGrid != null)
        {
            _navMainContentGrid.BorderThickness = enabled ? NavContentBorderHidden : NavContentBorderDefault;
            _navMainContentGrid.BorderBrush = enabled
                ? transparentBrush
                : Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush ?? transparentBrush;
        }

        if (_navShadowCaster != null)
            _navShadowCaster.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
    }

    private static readonly SolidColorBrush transparentBrush = new(Microsoft.UI.Colors.Transparent);

    private void ApplyGradientOverflowChrome(bool enabled)
    {
        ApplyNavigationViewChrome(true);
        RootNav.Resources["NavigationViewContentGridBorderThickness"] = NavContentBorderHidden;
        RootNav.Resources["NavigationViewMinimalContentGridBorderThickness"] = NavContentBorderHidden;
        RootNav.Resources["NavigationViewContentGridBorderBrush"] = transparentBrush;
        RootNav.Resources["NavigationViewContentGridCornerRadius"] = new CornerRadius(0);
        if (!enabled) PlayerBar.Margin = new Thickness(0);
    }

    private double GetNavPaneWidth()
    {
        return RootNav.DisplayMode switch
        {
            NavigationViewDisplayMode.Expanded => RootNav.OpenPaneLength,
            NavigationViewDisplayMode.Compact  => RootNav.CompactPaneLength,
            _                                => 0
        };
    }

    public void ApplyGradientOverflowSetting()
    {
        if (App.Settings.Current.Backdrop != AppBackdropStyle.Solid) return;
        if (_nowPlayingId == null) return;
        var track = _queue.FirstOrDefault(t => t.Id == _nowPlayingId)
                 ?? _library.FirstOrDefault(t => t.Id == _nowPlayingId);
        if (track == null) return;
        UpdatePlayerBarColorAsync(track);
    }

    private static Windows.UI.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    public void RefreshThemeDependentUI()
    {
        _coverColorCache.Clear();
        if (_currentIndex >= 0)
            UpdatePlayerBarColorAsync(_library[_currentIndex]);
        else if (App.Settings.Current.Backdrop == AppBackdropStyle.Solid)
            PlayerBar.Background = (Brush)Application.Current.Resources["AppSurfaceBrush"];
        ApplyNativeWindowBackgroundFix();
        ForceUIRepaint();
    }

    private void ForceUIRepaint()
    {
        var currentTheme = RootGrid.RequestedTheme;
        RootGrid.RequestedTheme = currentTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => 
        {
            RootGrid.RequestedTheme = currentTheme;
        });
    }

        // ===================== CATÃƒâ€°GORIES DE NAVIGATION =====================
    public void RefreshNavCategories()
    {
        var s = App.Settings.Current;
        var allItems = new List<NavigationViewItem> { NavLibrary, NavAlbums, NavPlaylists, NavQueue, NavArtists, NavGenres, NavFolders, NavStatistics, NavDownload };
        var targetVisibility = new List<bool> { s.ShowLibraryCategory, s.ShowAlbumsCategory, s.ShowPlaylistsCategory, true, s.ShowArtistsCategory, s.ShowGenresCategory, s.ShowFoldersCategory, s.ShowStatisticsCategory, s.ShowDownloadCategory };

        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];
            bool shouldShow = targetVisibility[i];
            if (shouldShow && !RootNav.MenuItems.Contains(item))
            {
                int insertIndex = 0;
                for (int j = 0; j < i; j++)
                {
                    if (targetVisibility[j] && RootNav.MenuItems.Contains(allItems[j]))
                    {
                        insertIndex = RootNav.MenuItems.IndexOf(allItems[j]) + 1;
                    }
                }
                RootNav.MenuItems.Insert(insertIndex, item);

                // Animation apparition plus visible
                var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                item.Opacity = 0;
                item.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { X = -20 };
                var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(300) };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fade, item);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fade, "Opacity");

                var slide = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = -20, To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slide, item.RenderTransform);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slide, "X");

                sb.Children.Add(fade);
                sb.Children.Add(slide);
                sb.Begin();
            }
            else if (!shouldShow && RootNav.MenuItems.Contains(item))
            {
                // Animation disparition plus visible
                var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                if (item.RenderTransform is not Microsoft.UI.Xaml.Media.TranslateTransform)
                    item.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();
                
                var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = item.Opacity, To = 0, Duration = TimeSpan.FromMilliseconds(200) };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fade, item);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fade, "Opacity");
                var slide = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = 0, To = -20, Duration = TimeSpan.FromMilliseconds(200) };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slide, item.RenderTransform);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slide, "X");

                sb.Children.Add(fade);
                sb.Children.Add(slide);
                sb.Completed += (_, _) => {
                    if (RootNav.MenuItems.Contains(item)) RootNav.MenuItems.Remove(item);
                    item.RenderTransform = null;
                };
                sb.Begin();
            }
        }
    }

        // ===================== BIBLIOTHÃƒË†QUE =====================

    public async Task ReloadLibraryFromCacheAsync()
    {
        await LoadLibraryFromCacheAsync();
        RestoreSidebarSelection();
    }

    private async Task LoadLibraryFromCacheAsync()
    {
        await App.Cache.InitializeAsync();
        _library = await App.Cache.LoadAllTracksAsync();
        _libraryPageInstance?.SetTracks(_library);
        if (ContentFrame.Content is GenresPage genresPage) genresPage.LoadData(_library);
        if (ContentFrame.Content is FoldersPage foldersPage) foldersPage.LoadData(_library);
        _ = FetchMissingCoversInBackgroundAsync(_library.ToList());
        _ = BackgroundRescanAsync();
        if (App.Settings.Current.AutoFetchMissingCovers)
        {
            StartBackgroundCoverFetch();
        }
    }

    private async Task FetchMissingCoversInBackgroundAsync(List<Track> tracks)
    {
        foreach (var track in tracks)
        {
            if (!string.IsNullOrEmpty(track.CoverArtPath)) continue;
            var embedded = App.Scanner.ExtractEmbeddedCover(track.FilePath);
            if (embedded != null && embedded.Length > 0)
            {
                var localPath = await App.CoverArt.SaveEmbeddedCoverAsync(track.Id, embedded);
                if (localPath != null) { track.CoverArtPath = localPath; await App.Cache.UpdateTrackAsync(track); continue; }
            }
            if (!App.Settings.Current.AutoFetchMissingCovers) continue;
            var path = await App.CoverArt.FindAndCacheCoverAsync(track.Id, track.Artist, track.Album);
            if (path != null) { track.CoverArtPath = path; await App.Cache.UpdateTrackAsync(track); }
            await Task.Delay(150);
        }
    }

    public void FetchCoversForTracks(List<Track> tracks) => _ = FetchMissingCoversInBackgroundAsync(tracks);
    public void TriggerLibraryRescan() => _ = RunRescanAsync();
    
    public void RefreshSettingsFolders()
    {
        SettingsPageInstance?.RefreshFoldersList();
    }

    public List<Track> Library => _library;

    private bool _scanInProgress = false;
    private bool _rescanPending = false;
    private readonly HashSet<string> _dirtyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _dirtyLock = new();

    public void MarkPathDirty(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
            lock (_dirtyLock) { _dirtyPaths.Add(filePath); }
    }

    private async Task RunRescanAsync()
    {
        if (_scanInProgress) { _rescanPending = true; return; }
        _scanInProgress = true;
        _rescanPending = false;
        try
        {
            await BackgroundRescanAsync();
        }
        finally
        {
            _scanInProgress = false;
            if (_rescanPending)
                _ = RunRescanAsync();
        }
    }

    private async Task BackgroundRescanAsync()
    {
        await App.Cache.InitializeAsync();
            var knownPaths = await App.Cache.GetCachedFilePathsAsync();
            foreach (var t in _library) knownPaths.Add(t.FilePath);
            var folders = App.Settings.Current.MusicFolders.ToList();
            if (folders.Count == 0) return;

            var dlFolder = App.Settings.Current.DownloadFolder;
            if (!string.IsNullOrWhiteSpace(dlFolder) && Directory.Exists(dlFolder) && !folders.Contains(dlFolder, StringComparer.OrdinalIgnoreCase))
                folders.Add(dlFolder);
            bool scanStarted = false;
            var allNew = new List<Track>();
            var allCurrentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string coverDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Resona", "Covers");
            Directory.CreateDirectory(coverDir);

            await Task.Run(() =>
            {
                int countSinceCollect = 0;
                foreach (var musicFolder in folders)
                    foreach (var filePath in App.Scanner.EnumerateAudioFiles(musicFolder))
                    {
                        if (!scanStarted) { scanStarted = true; DispatcherQueue.TryEnqueue(() => StartScanAnimation()); }
                        allCurrentPaths.Add(filePath);
                        bool isDirty;
                        lock (_dirtyLock) { isDirty = _dirtyPaths.Remove(filePath); }
                        if (knownPaths.Contains(filePath) && !isDirty) continue;
                        knownPaths.Add(filePath);
                        var track = App.Scanner.ExtractMetadata(filePath, out var embeddedCover);
                        if (track == null) continue;
                        if (embeddedCover != null && embeddedCover.Length > 0)
                        {
                            string coverPath = Path.Combine(coverDir, $"{track.Id}.jpg");
                            File.WriteAllBytes(coverPath, embeddedCover);
                            track.CoverArtPath = coverPath;
                        }
                        embeddedCover = null;
                        allNew.Add(track);
                        countSinceCollect++;
                        if (countSinceCollect % 50 == 0) GC.Collect();
                    }
            });

            var deletedPaths = knownPaths.Where(p => !allCurrentPaths.Contains(p)).ToList();
            var deletedTracks = _library.Where(t => deletedPaths.Contains(t.FilePath, StringComparer.OrdinalIgnoreCase)).ToList();

            await Task.Run(async () =>
            {
                int count = 0;
                foreach (var track in allNew)
                {
                    await App.Cache.UpsertTrackAsync(track);
                    count++;
                    if (count % 50 == 0) GC.Collect();
                }
            });

            DispatcherQueue.TryEnqueue(() =>
            {
                bool libraryChanged = false;
                
                if (deletedTracks.Count > 0)
                {
                    _library.RemoveAll(t => deletedPaths.Contains(t.FilePath, StringComparer.OrdinalIgnoreCase));
                    libraryChanged = true;
                }

                if (allNew.Count > 0)
                {
                    var existingPaths = new HashSet<string>(_library.Select(t => t.FilePath), StringComparer.OrdinalIgnoreCase);
                    var reallyNew = allNew.Where(t => !existingPaths.Contains(t.FilePath)).ToList();
                    if (reallyNew.Count > 0)
                    {
                        _library.AddRange(reallyNew);
                        libraryChanged = true;
                        _ = FetchMissingCoversInBackgroundAsync(reallyNew.ToList());
                    }
                }
                
                StopScanAnimation();
                if (!libraryChanged) return;
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _library = _library.Where(t => seen.Add(t.FilePath)).ToList();
                    _libraryPageInstance?.SetTracks(_library);
                });
            });
    }
    
    private Storyboard? _scanStoryboard;

    private void StartScanAnimation()
    {
        ScanProgressRow.Visibility = Visibility.Visible;
        double totalWidth = AppWindow.Size.Width + 160;
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var anim = new DoubleAnimation { From = -160, To = totalWidth, Duration = TimeSpan.FromMilliseconds(1600),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        Storyboard.SetTarget(anim, ScanAnimTranslate);
        Storyboard.SetTargetProperty(anim, "X");
        sb.Children.Add(anim);
        sb.Begin();
        _scanStoryboard = sb;
    }

    private void StopScanAnimation()
    {
        _scanStoryboard?.Stop();
        _scanStoryboard = null;
        ScanProgressRow.Visibility = Visibility.Collapsed;
    }

    private void SetupPositionTimer()
    {
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _positionTimer.Tick += (s, e) =>
        {
            if (_isSliderDragging) return;

            var enginePos = App.AudioEngine.CurrentPosition;
            var total = App.AudioEngine.TotalDuration;
            if (total.TotalSeconds <= 0 && enginePos.TotalSeconds > 0) total = enginePos;
            if (total.TotalSeconds <= 0) return;

            if (enginePos.TotalSeconds > ProgressSlider.Maximum)
            {
                ProgressSlider.Maximum = enginePos.TotalSeconds;
                TotalTimeText.Text = FormatTime(TimeSpan.FromSeconds(ProgressSlider.Maximum));
            }

            if (Math.Abs(ProgressSlider.Value - enginePos.TotalSeconds) > 0.05)
                ProgressSlider.Value = enginePos.TotalSeconds;
            
            string newTime = FormatTime(enginePos);
            if (CurrentTimeText.Text != newTime)
                CurrentTimeText.Text = newTime;

            if (_queueIndex >= 0 && _queueIndex < _queue.Count)
            {
                var track = _queue[_queueIndex];
                if (track.Duration.TotalSeconds <= 0)
                {
                    track.Duration = total;
                    _ = App.Cache.UpsertTrackAsync(track);
                }
            }
            
            TickSyncedLyrics(enginePos);
            
            if (App.AudioEngine.State == NAudio.Wave.PlaybackState.Playing)
                App.PlayStats.AddListenTime(TimeSpan.FromMilliseconds(33));
        };
        _positionTimer.Start();
    }

    private static string FormatTime(TimeSpan t)
        => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    private void UpdateIsPlayingGlobally(string filePath, bool isPlaying)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        foreach (var t in _library.Where(x => x.FilePath == filePath)) t.IsPlaying = isPlaying;
        foreach (var t in _queue.Where(x => x.FilePath == filePath)) t.IsPlaying = isPlaying;
        if (_libraryPageInstance != null)
            foreach (var t in _libraryPageInstance.FilteredTracks.Where(x => x.FilePath == filePath)) t.IsPlaying = isPlaying;
        if (_queuePageInstance != null)
            foreach (var t in _queuePageInstance.DisplayedTracks.Where(x => x.FilePath == filePath)) t.IsPlaying = isPlaying;
        if (_playlistsPageInstance != null)
            foreach (var t in _playlistsPageInstance.DisplayedTracks.Where(x => x.FilePath == filePath)) t.IsPlaying = isPlaying;
    }

    public async void PlayTrack(Track track, List<Track>? queue = null)
    {
        var old = _queue.FirstOrDefault(t => t.Id == _nowPlayingId);
        if (App.NowPlayingFilePath != null)
            UpdateIsPlayingGlobally(App.NowPlayingFilePath, false);

        _queue = queue ?? _library;
        _queueIndex = _queue.FindIndex(t => t.Id == track.Id);
        _currentIndex = _library.FindIndex(t => t.Id == track.Id);
        var settings = App.Settings.Current;

        _nowPlayingId = track.Id;
        _nowPlayingFilePath = track.FilePath;
        App.NowPlayingId = track.Id;
        App.NowPlayingFilePath = track.FilePath;
        UpdateIsPlayingGlobally(track.FilePath, true);
        NowPlayingTitle.Content = track.Title;
        NowPlayingArtist.Content = track.Artist;
        NowPlayingAlbum.Content = track.Album;
        PlayPauseIcon.Glyph = "\uE769"; PlayPauseIcon.Margin = new Thickness(0);
        ProgressSlider.Maximum = Math.Max(track.Duration.TotalSeconds, 1);
        ProgressSlider.Value = 0;
        TotalTimeText.Text = FormatTime(track.Duration);
        CurrentTimeText.Text = "0:00";
        ShowPlayerBar();

        _libraryPageInstance?.SetNowPlayingId(track.Id, track.FilePath);
        if (ContentFrame.Content is PlaylistDetailPage pdp) pdp.SetNowPlayingId(track.Id, track.FilePath);
        if (ContentFrame.Content is PlaylistsPage pp) pp.SetNowPlayingId(track.Id, track.FilePath);
        if (ContentFrame.Content is QueuePage qp) qp.SetNowPlayingId(track.Id, track.FilePath);
        if (ContentFrame.Content is AlbumsPage ap) ap.SetNowPlayingId(track.Id, track.FilePath);
        if (ContentFrame.Content is ArtistsPage arp) arp.SetNowPlayingId(track.Id, track.FilePath);
        if (ContentFrame.Content is GenresPage gp) gp.SetNowPlayingId(track.Id, track.FilePath);
        if (ContentFrame.Content is FoldersPage fp) fp.SetNowPlayingId(track.Id, track.FilePath);

        if (!string.IsNullOrEmpty(track.CoverArtPath))
            SetPlayerCover(track.CoverArtPath);
        else
        {
            ClearPlayerCover();
            if (settings.AutoFetchMissingCovers)
                _ = FindMissingCoverAsync(track);
        }

        UpdatePlayerBarColorAsync(track);

        _lrcLines.Clear();
        _lrcCurrentIndex = -1;
        UpdateLyricsOverlayTrackInfo(track);
        if (settings.LyricsEnabled)
        {
            if (!string.IsNullOrEmpty(track.Lyrics)) LoadLyricsAsync(track.Lyrics, track.LyricsSynced);
            else _ = FindMissingLyricsAsync(track);
        }

        try
        {
            double gainToApply = settings.NormalizationEnabled ? track.NormalizationGainDb : 0;
            Microsoft.UI.Xaml.Controls.ContentDialog? downloadDialog = null;
            Microsoft.UI.Xaml.Controls.TextBlock? statusText = null;

            await App.AudioEngine.PlayAsync(track, preferExclusive: settings.ExclusiveAudioMode, initialGainDb: gainToApply,
                onDownloadProgress: line => {
                    DispatcherQueue.TryEnqueue(() => {
                        if (downloadDialog == null) {
                            statusText = new Microsoft.UI.Xaml.Controls.TextBlock { Text = line, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap };
                            var panel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 10 };
                            panel.Children.Add(new Microsoft.UI.Xaml.Controls.ProgressRing { IsActive = true, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center });
                            panel.Children.Add(statusText);
                            downloadDialog = new Microsoft.UI.Xaml.Controls.ContentDialog {
                                Title = "Installation des codecs audio (FFmpeg)...",
                                Content = panel,
                                XamlRoot = ContentFrame.XamlRoot
                            };
                            _ = downloadDialog.ShowAsync();
                        } else {
                            statusText!.Text = line;
                        }
                    });
                });
            if (downloadDialog != null) {
                downloadDialog.Hide();
            }

            // Mettre ÃƒÂ  jour la durÃƒÂ©e si le dÃƒÂ©codage a rÃƒÂ©vÃƒÂ©lÃƒÂ© une durÃƒÂ©e diffÃƒÂ©rente (ex: FFMpeg)
            var actualDuration = App.AudioEngine.TotalDuration;
            if (actualDuration.TotalSeconds > 0 && Math.Abs((actualDuration - track.Duration).TotalSeconds) > 1)
            {
                track.Duration = actualDuration;
                ProgressSlider.Maximum = Math.Max(actualDuration.TotalSeconds, 1);
                TotalTimeText.Text = FormatTime(actualDuration);
                _ = App.Cache.UpsertTrackAsync(track);
            }
        }
        catch (Exception ex)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Lecture impossible",
                Content = $"Impossible de lire ce fichier : {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = ContentFrame.XamlRoot
            };
            _ = errorDialog.ShowAsync();
            return;
        }

        App.PlayStats.RecordPlay(track.Id);

        if (_queueIndex >= 0 && _queueIndex + 1 < _queue.Count)
        {
            var next = _queue[_queueIndex + 1];
            App.AudioEngine.PrewarmOpus(next.FilePath, next.Duration);
        }

        if (settings.NormalizationEnabled && !track.IsAnalyzed)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    double gain = await App.Normalization.AnalyzeAsync(track.FilePath);
                    track.NormalizationGainDb = gain;
                    track.IsAnalyzed = true;
                    _ = App.Cache.UpsertTrackAsync(track);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_nowPlayingId == track.Id)
                            App.AudioEngine.SetNormalizationGain(gain);
                    });
                }
                catch { track.NormalizationGainDb = 0; }
            });
        }
    }

    private void SetPlayerCover(string path)
    {
        try
        {
            var bmp = CoverCacheService.GetBitmap(path, 140);
            if (bmp != null)
            {
                NowPlayingCoverBorder.Background = new Microsoft.UI.Xaml.Media.ImageBrush
                {
                    ImageSource = bmp,
                    Stretch     = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                };
            }
            else
            {
                NowPlayingCoverBorder.Background = new Microsoft.UI.Xaml.Media.ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(path)),
                    Stretch     = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                };
            }
            NowPlayingPlaceholderIcon.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            NowPlayingCover.Source = null;
        }
        catch { }
    }

    private void ClearPlayerCover()
    {
        NowPlayingCoverBorder.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
        NowPlayingPlaceholderIcon.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        NowPlayingCover.Source = null;
    }

    private async Task FindMissingCoverAsync(Track track)
    {
        var path = await App.CoverArt.FindAndCacheCoverAsync(track.Id, track.Artist, track.Album);
        if (path != null)
        {
            track.CoverArtPath = path;
            await App.Cache.UpdateTrackAsync(track);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_currentIndex >= 0 && _library[_currentIndex].Id == track.Id)
                    SetPlayerCover(path);
            });
            UpdatePlayerBarColorAsync(track);
        }
    }

    private bool _isFetchingCovers = false;
    private HashSet<string> _failedCoverSearches = new HashSet<string>();
    public async void StartBackgroundCoverFetch()
    {
        if (_isFetchingCovers || !App.Settings.Current.AutoFetchMissingCovers) return;
        _isFetchingCovers = true;
        try
        {
            var missingCovers = _library.Where(t => string.IsNullOrEmpty(t.CoverArtPath) && !_failedCoverSearches.Contains(t.Id)).ToList();
            foreach (var track in missingCovers)
            {
                if (!App.Settings.Current.AutoFetchMissingCovers) break;
                if (string.IsNullOrWhiteSpace(track.Artist) && string.IsNullOrWhiteSpace(track.Album)) continue;

                var path = await App.CoverArt.FindAndCacheCoverAsync(track.Id, track.Artist, track.Album);
                if (path != null)
                {
                    track.CoverArtPath = path;
                    await App.Cache.UpdateTrackAsync(track);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_currentIndex >= 0 && _library[_currentIndex].Id == track.Id)
                            SetPlayerCover(path);
                    });
                }
                else
                {
                    _failedCoverSearches.Add(track.Id);
                }
                
                await Task.Delay(2000);
            }
        }
        catch { }
        finally
        {
            _isFetchingCovers = false;
        }
    }

    private async Task FindMissingLyricsAsync(Track track)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var lyricsTask = App.Lyrics.SearchAsync(track.Artist, track.Title, track.Album, track.Duration);
        var autoTagTask = AutoTagService.LookupAsync(track.Artist, track.Title, track.Duration, track.FilePath);

        var result = new LyricsResult();
        try { result = await lyricsTask.WaitAsync(cts.Token); }
        catch { result = new LyricsResult(); }

        AutoTagResult? autoTag = null;
        try { autoTag = await autoTagTask.WaitAsync(cts.Token); }
        catch { }

        if (!result.Found && autoTag != null)
        {
            string altArtist = autoTag.Artist ?? track.Artist;
            string altTitle = autoTag.Title ?? track.Title;
            cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try { result = await App.Lyrics.SearchAsync(altArtist, altTitle, autoTag.Album ?? track.Album, track.Duration).WaitAsync(cts.Token); }
            catch { }
            if (!result.Found && autoTag.Album != null)
            {
                cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                try { result = await App.Lyrics.SearchAsync(altArtist, altTitle, autoTag.Album, track.Duration).WaitAsync(cts.Token); }
                catch { }
            }
        }

        track.Lyrics = result.Found ? (result.SyncedLyrics ?? result.PlainLyrics) : null;
        track.LyricsSynced = result.SyncedLyrics != null;
        if (result.Found) await App.Cache.UpdateTrackAsync(track);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_currentIndex < 0 || _library[_currentIndex].Id != track.Id) return;
            if (result.Found) LoadLyricsAsync(track.Lyrics!, track.LyricsSynced);
            else if (_lyricsOverlayOpen) ShowPlainLyrics("Aucune parole trouvÃƒÂ©e pour ce morceau.");
        });
    }

    public void ApplyNormalizationSetting()
    {
        if (_currentIndex < 0) return;
        var track = _library[_currentIndex];
        App.AudioEngine.SetNormalizationGain(App.Settings.Current.NormalizationEnabled ? track.NormalizationGainDb : 0);
    }

    private CancellationTokenSource? _reanalyzeCts;
    public void InvalidateNormalizationAndReanalyze()
    {
        _reanalyzeCts?.Cancel();
        _reanalyzeCts = new CancellationTokenSource();
        var token = _reanalyzeCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(1500, token);
            if (token.IsCancellationRequested) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                AnalyzeWholeLibraryInBackground();
            });
        });
    }

    public void AnalyzeWholeLibraryInBackground()
    {
        _ = Task.Run(async () =>
        {
            var tracksToAnalyze = _library.Where(t => !t.IsAnalyzed).ToList();
            if (tracksToAnalyze.Count == 0) return;

            // Prioritize currently playing track so the user hears the effect immediately
            var current = tracksToAnalyze.FirstOrDefault(t => t.Id == _nowPlayingId);
            if (current != null)
            {
                tracksToAnalyze.Remove(current);
                tracksToAnalyze.Insert(0, current);
            }

            Microsoft.UI.Xaml.Controls.ContentDialog? loadingDialog = null;
            Microsoft.UI.Xaml.Controls.TextBlock? statusText = null;

            if (tracksToAnalyze.Count > 10)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusText = new Microsoft.UI.Xaml.Controls.TextBlock { Text = $"Analyse de {tracksToAnalyze.Count} titres...", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center };
                    var panel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 10 };
                    panel.Children.Add(new Microsoft.UI.Xaml.Controls.ProgressRing { IsActive = true, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center });
                    panel.Children.Add(statusText);
                    loadingDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = "Normalisation Audio",
                        Content = panel,
                        XamlRoot = ContentFrame.XamlRoot
                    };
                    _ = loadingDialog.ShowAsync();
                });
            }

            int processed = 0;
            using SemaphoreSlim dbLock = new SemaphoreSlim(1, 1);

            await Parallel.ForEachAsync(tracksToAnalyze, new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 3) }, async (track, ct) =>
            {
                try
                {
                    double gain = await App.Normalization.AnalyzeAsync(track.FilePath);
                    track.NormalizationGainDb = gain;
                    track.IsAnalyzed = true;

                    await dbLock.WaitAsync(ct);
                    try
                    {
                        await App.Cache.UpdateTrackAsync(track);
                    }
                    finally
                    {
                        dbLock.Release();
                    }
                    
                    int currentProcessed = Interlocked.Increment(ref processed);
                    if (statusText != null)
                    {
                        DispatcherQueue.TryEnqueue(() => statusText.Text = $"Analyse... {currentProcessed}/{tracksToAnalyze.Count}");
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (App.Settings.Current.NormalizationEnabled && _nowPlayingId == track.Id)
                        {
                            App.AudioEngine.SetNormalizationGain(gain);
                        }
                    });
                }
                catch
                {
                    Interlocked.Increment(ref processed);
                }
            });

            if (loadingDialog != null)
            {
                DispatcherQueue.TryEnqueue(() => loadingDialog.Hide());
            }
        });
    }

    public void ApplyLyricsButtonVisibility()
    {
        LyricsButton.Visibility = App.Settings.Current.LyricsEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (!App.Settings.Current.LyricsEnabled) CloseLyricsOverlay();
    }

    private async void LoadLyricsAsync(string lyrics, bool isSynced)
    {
        _lrcLines.Clear();
        _lrcCurrentIndex = -1;
        if (isSynced)
        {
            var rawLines = new List<LrcLine>();
            foreach (var rawLine in lyrics.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length < 7 || line[0] != '[') continue;
                int close = line.IndexOf(']');
                if (close < 0) continue;
                var timePart = line.Substring(1, close - 1);
                var text = line.Substring(close + 1).Trim();
                if (TryParseLrcTime(timePart, out var ts)) rawLines.Add(new LrcLine(ts, text));
            }
            rawLines.Sort((a, b) => a.Time.CompareTo(b.Time));
            
            if (App.Settings.Current.TranslateLyricsEnabled)
            {
                string combined = string.Join(" \n ", rawLines.Select(l => l.Text));
                string translated = await Resona.Services.LyricsTranslatorService.TranslateTextAsync(combined, "fr");
                var translatedLines = translated.Split(new[] { " \n ", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < rawLines.Count && i < translatedLines.Length; i++)
                {
                    string orig = rawLines[i].Text;
                    string trans = translatedLines[i].Trim();
                    _lrcLines.Add(new LrcLine(rawLines[i].Time, string.IsNullOrWhiteSpace(trans) || orig == trans ? orig : $"{orig}\n\u2014 {trans}"));
                }
            }
            else
            {
                _lrcLines.AddRange(rawLines);
            }

            if (_lrcLines.Count > 0) { ShowSyncedLyricsUI(); return; }
        }
        else
        {
            if (App.Settings.Current.TranslateLyricsEnabled)
            {
                string translated = await Resona.Services.LyricsTranslatorService.TranslateTextAsync(lyrics, "fr");
                var origLines = lyrics.Split('\n');
                var transLines = translated.Split('\n');
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < origLines.Length; i++)
                {
                    string orig = origLines[i].TrimEnd();
                    string trans = i < transLines.Length ? transLines[i].TrimEnd() : "";
                    if (string.IsNullOrWhiteSpace(orig)) sb.AppendLine();
                    else sb.AppendLine(string.IsNullOrWhiteSpace(trans) || orig == trans ? orig : $"{orig}\n\u2014 {trans}");
                }
                lyrics = sb.ToString();
            }
            ShowPlainLyrics(lyrics);
        }
    }

    private static bool TryParseLrcTime(string s, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        var colon = s.IndexOf(':');
        if (colon < 0) return false;
        if (!int.TryParse(s.Substring(0, colon), out int minutes)) return false;
        string rest = s.Substring(colon + 1).Replace(':', '.');
        if (!double.TryParse(rest, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double secs)) return false;
        result = TimeSpan.FromSeconds(minutes * 60 + secs);
        return true;
    }

    private void EnsureLyricsOverlayCreated()
    {
        if (_lyricsOverlay != null) return;
        var backdrop = new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xEA, 0x09, 0x09, 0x0D)),
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

        _lyricsTrackTitle = new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.5, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
        _lyricsTrackArtist = new TextBlock { FontSize = 11, Opacity = 0.3,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1, HorizontalAlignment = HorizontalAlignment.Center };

        _lyricsLinePrev = new TextBlock { FontSize = 17, Opacity = 0.22, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 540, Margin = new Thickness(0, 0, 0, 18), IsHitTestVisible = false };
        _lyricsLineCurrent = new TextBlock { FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 540, Margin = new Thickness(0, 0, 0, 18),
            Foreground = (Brush)Application.Current.Resources["AppAccentBrush"], IsHitTestVisible = false };
        _lyricsLineNext = new TextBlock { FontSize = 17, Opacity = 0.22, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 540, IsHitTestVisible = false };

        _lyricsSyncedPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 620,
            Padding = new Thickness(32, 0, 32, 0), Visibility = Visibility.Visible };
        _lyricsSyncedPanel.Children.Add(_lyricsLinePrev);
        _lyricsSyncedPanel.Children.Add(_lyricsLineCurrent);
        _lyricsSyncedPanel.Children.Add(_lyricsLineNext);

        _lyricsPlainText = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 16, LineHeight = 28,
            TextAlignment = TextAlignment.Center, Opacity = 0.75, HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 540, IsHitTestVisible = false };

        _lyricsGoogleBtn = new HyperlinkButton { Content = "Chercher sur Google", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0), Visibility = Visibility.Collapsed };
        _lyricsGoogleBtn.Click += (s, e) => {
            if (_currentIndex >= 0 && _currentIndex < _library.Count)
            {
                var t = _library[_currentIndex];
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(t.Artist + " " + t.Title + " paroles")}"));
            }
        };

        var plainContainer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        plainContainer.Children.Add(_lyricsPlainText);
        plainContainer.Children.Add(_lyricsGoogleBtn);

        _lyricsPlainScroll = new ScrollViewer { Content = plainContainer, Padding = new Thickness(32, 24, 32, 40),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed,
            IsHitTestVisible = true };

        var headerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 32, 0, 12) };
        headerPanel.Children.Add(_lyricsTrackTitle);
        headerPanel.Children.Add(_lyricsTrackArtist);

        var contentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        contentPanel.Children.Add(_lyricsSyncedPanel);
        contentPanel.Children.Add(_lyricsPlainScroll);

        var rootGrid = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(headerPanel, 0);
        Grid.SetRow(contentPanel, 1);
        rootGrid.Children.Add(headerPanel);
        rootGrid.Children.Add(contentPanel);

        _lyricsOverlay = new Grid { Visibility = Visibility.Collapsed, Opacity = 0 };
        Grid.SetRow(_lyricsOverlay, 0); Grid.SetRowSpan(_lyricsOverlay, 3);
        Canvas.SetZIndex(_lyricsOverlay, 90);
        _lyricsOverlay.Tapped += (s, e) => CloseLyricsOverlay();
        _lyricsOverlay.Children.Add(backdrop);
        _lyricsOverlay.Children.Add(rootGrid);
        RootGrid.Children.Add(_lyricsOverlay);
    }

    private void ShowSyncedLyricsUI()
    {
        EnsureLyricsOverlayCreated();
        if (_lyricsPlainScroll != null) _lyricsPlainScroll.Visibility = Visibility.Collapsed;
        if (_lyricsSyncedPanel != null) _lyricsSyncedPanel.Visibility = Visibility.Visible;
        UpdateSyncedLyricsDisplay(-1);
    }

    private void ShowPlainLyrics(string text)
    {
        EnsureLyricsOverlayCreated();
        if (_lyricsPlainText != null) _lyricsPlainText.Text = text;
        if (_lyricsLinePrev != null) _lyricsLinePrev.Text = "";
        if (_lyricsLineCurrent != null) _lyricsLineCurrent.Text = "";
        if (_lyricsLineNext != null) _lyricsLineNext.Text = "";
        if (_lyricsPlainScroll != null) _lyricsPlainScroll.Visibility = Visibility.Visible;
        if (_lyricsSyncedPanel != null) _lyricsSyncedPanel.Visibility = Visibility.Collapsed;
        if (_lyricsGoogleBtn != null) _lyricsGoogleBtn.Visibility = text.StartsWith("Aucune parole") ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateLyricsOverlayTrackInfo(Track track)
    {
        EnsureLyricsOverlayCreated();
        if (_lyricsTrackTitle != null) _lyricsTrackTitle.Text = track.Title;
        if (_lyricsTrackArtist != null) _lyricsTrackArtist.Text = track.Artist;
        ShowPlainLyrics("Recherche des paroles...");
    }

    private void UpdateSyncedLyricsDisplay(int newIndex)
    {
        if (_lyricsLineCurrent == null) return;
        string prev = newIndex > 0 ? _lrcLines[newIndex - 1].Text : "";
        string curr = newIndex >= 0 && newIndex < _lrcLines.Count ? _lrcLines[newIndex].Text : "";
        string next = newIndex + 1 < _lrcLines.Count ? _lrcLines[newIndex + 1].Text : "";

        if (_lyricsLineCurrent.Text == curr)
        { _lyricsLinePrev!.Text = prev; _lyricsLineNext!.Text = next; return; }

        var sbOut = new Storyboard();
        var fo = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(fo, _lyricsLineCurrent); Storyboard.SetTargetProperty(fo, "Opacity");
        sbOut.Children.Add(fo);
        sbOut.Completed += (_, __) =>
        {
            _lyricsLinePrev!.Text = prev; _lyricsLineCurrent.Text = curr; _lyricsLineNext!.Text = next;
            
            if (_lyricsSyncedPanel.RenderTransform is not TranslateTransform)
                _lyricsSyncedPanel.RenderTransform = new TranslateTransform();
                
            var sbIn = new Storyboard();
            
            var fi = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fi, _lyricsLineCurrent); Storyboard.SetTargetProperty(fi, "Opacity"); sbIn.Children.Add(fi);
            
            var fip = new DoubleAnimation { To = 0.22, Duration = TimeSpan.FromMilliseconds(300) };
            Storyboard.SetTarget(fip, _lyricsLinePrev); Storyboard.SetTargetProperty(fip, "Opacity"); sbIn.Children.Add(fip);
            
            var fin = new DoubleAnimation { To = 0.22, Duration = TimeSpan.FromMilliseconds(300) };
            Storyboard.SetTarget(fin, _lyricsLineNext!); Storyboard.SetTargetProperty(fin, "Opacity"); sbIn.Children.Add(fin);
            
            var slide = new DoubleAnimation { From = 20, To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slide, _lyricsSyncedPanel); Storyboard.SetTargetProperty(slide, "(UIElement.RenderTransform).(TranslateTransform.Y)"); sbIn.Children.Add(slide);
            
            sbIn.Begin();
        };
        sbOut.Begin();
    }

    public void TickSyncedLyrics(TimeSpan position)
    {
        if (_lrcLines.Count == 0 || !_lyricsOverlayOpen) return;
        int idx = -1;
        for (int i = 0; i < _lrcLines.Count; i++)
        { if (_lrcLines[i].Time <= position) idx = i; else break; }
        if (idx != _lrcCurrentIndex) { _lrcCurrentIndex = idx; UpdateSyncedLyricsDisplay(idx); }
    }

    private void OpenLyricsOverlay()
    {
        EnsureLyricsOverlayCreated();
        _lyricsOverlayOpen = true;
        _lyricsOverlay!.Visibility = Visibility.Visible;
        
        var sb = new Storyboard();
        var fi = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(fi, _lyricsOverlay); Storyboard.SetTargetProperty(fi, "Opacity");
        sb.Children.Add(fi);
        
        if (_lyricsOverlay.RenderTransform is not CompositeTransform) _lyricsOverlay.RenderTransform = new CompositeTransform();
        _lyricsOverlay.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        var transform = (CompositeTransform)_lyricsOverlay.RenderTransform;
        transform.ScaleX = 0.95; transform.ScaleY = 0.95; transform.TranslateY = 20;
        
        var sx = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 } };
        var sy = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 } };
        var ty = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 } };
        Storyboard.SetTarget(sx, _lyricsOverlay); Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
        Storyboard.SetTarget(sy, _lyricsOverlay); Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
        Storyboard.SetTarget(ty, _lyricsOverlay); Storyboard.SetTargetProperty(ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
        sb.Children.Add(sx); sb.Children.Add(sy); sb.Children.Add(ty);
        
        sb.Begin();
    }

    private void CloseLyricsOverlay()
    {
        if (_lyricsOverlay == null) return;
        _lyricsOverlayOpen = false;
        var sb = new Storyboard();
        var fo = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var ty = new DoubleAnimation { To = 10, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var sx = new DoubleAnimation { To = 0.98, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var sy = new DoubleAnimation { To = 0.98, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        Storyboard.SetTarget(fo, _lyricsOverlay); Storyboard.SetTargetProperty(fo, "Opacity");
        Storyboard.SetTarget(ty, _lyricsOverlay); Storyboard.SetTargetProperty(ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
        Storyboard.SetTarget(sx, _lyricsOverlay); Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
        Storyboard.SetTarget(sy, _lyricsOverlay); Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
        sb.Children.Add(fo); sb.Children.Add(ty); sb.Children.Add(sx); sb.Children.Add(sy);
        sb.Completed += (_, __) => { if (!_lyricsOverlayOpen) _lyricsOverlay.Visibility = Visibility.Collapsed; };
        sb.Begin();
    }

    // ===================== TRACK INFO OVERLAY =====================

    private Grid? _infoOverlay;
    private bool _infoOverlayOpen = false;
    private TextBlock? _infoTrackTitle;
    private TextBlock? _infoTrackArtist;
    private TextBlock? _infoContent;

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (App.AudioEngine != null)
        {
            App.AudioEngine.SetUserVolume((float)(VolumeSlider.Value / 100.0));
            if (App.Settings != null && App.Settings.Current != null)
            {
                App.Settings.Current.Volume = VolumeSlider.Value;
                _ = App.Settings.SaveAsync();
            }
        }
    }

    private void LyricsButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        HideInfoOverlay();
        if (_lyricsOverlayOpen) CloseLyricsOverlay();
        else OpenLyricsOverlay();
        LyricsButtonBg.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
    }

    private void LyricsButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Resona.Helpers.AnimationHelper.ApplyBouncyScale(LyricsButton, 1.0f);
        LyricsButtonBg.Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void LyricsButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        LyricsButtonBg.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255));
    }

    private void LyricsButton_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        LyricsButtonBg.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
    }

    public void RefreshPlaylistsPage() => _playlistsPageInstance?.RefreshAsync();

    private async Task<List<string>> SearchCoversOnlineAsync(string query)
    {
        var urls = new List<string>();
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.TryParseAdd("Resona/1.0");

            // Pour trouver de bonnes pochettes, on enlÃƒÂ¨ve "cover" pour les API musicales qui ÃƒÂ©choueraient, 
            // mais on fait plusieurs requÃƒÂªtes en parallÃƒÂ¨le (album, chanson) pour maximiser les rÃƒÂ©sultats.
            string apiQuery = System.Text.RegularExpressions.Regex.Replace(query, @"(?i)\b(cover|album art)\b", "").Trim();
            if (string.IsNullOrWhiteSpace(apiQuery)) apiQuery = query;

            string term = Uri.EscapeDataString(apiQuery);

            var tasks = new List<Task>();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(await http.GetByteArrayAsync($"https://itunes.apple.com/search?term={term}&entity=album&limit=10"));
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("results", out var results))
                    {
                        foreach (var r in results.EnumerateArray())
                        {
                            if (r.TryGetProperty("artworkUrl100", out var art))
                                urls.Add((art.GetString() ?? "").Replace("100x100bb", "600x600bb"));
                        }
                    }
                }
                catch { }
            }));

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(await http.GetByteArrayAsync($"https://itunes.apple.com/search?term={term}&entity=song&limit=10"));
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("results", out var results))
                    {
                        foreach (var r in results.EnumerateArray())
                        {
                            if (r.TryGetProperty("artworkUrl100", out var art))
                                urls.Add((art.GetString() ?? "").Replace("100x100bb", "600x600bb"));
                        }
                    }
                }
                catch { }
            }));

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(await http.GetByteArrayAsync($"https://api.deezer.com/search?q={term}&limit=10"));
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data))
                    {
                        foreach (var track in data.EnumerateArray())
                        {
                            if (track.TryGetProperty("album", out var album))
                            {
                                if (album.TryGetProperty("cover_xl", out var cover)) urls.Add(cover.GetString() ?? "");
                                else if (album.TryGetProperty("cover_big", out var coverBig)) urls.Add(coverBig.GetString() ?? "");
                            }
                        }
                    }
                }
                catch { }
            }));

            await Task.WhenAll(tasks);
        }
        catch { }
        return urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
    }







	public void RestoreSidebarSelection()
	{
		if (RootNav.SelectedItem is NavigationViewItem navigationViewItem)
		{
			bool isSettings = RootNav.SettingsItem as NavigationViewItem == navigationViewItem;
			NavigateToSidebarItem(navigationViewItem.Tag?.ToString(), isSettings);
		}
	}

	public void NavigateToArtist(string artist)
	{
		List<Track> list = _library.Where((Track t) => string.Equals(t.Artist, artist, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list.Count > 0)
		{
			RootNav.SelectedItem = null;
			ShowTrackCollection(artist, list, Strings.Current.CS_Artiste);
		}
	}

	public void NavigateToAlbum(string album)
	{
		List<Track> list = _library.Where((Track t) => string.Equals(t.Album, album, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list.Count > 0)
		{
			RootNav.SelectedItem = null;
			ShowTrackCollection(album, list, Strings.Current.CS_Album);
		}
	}

	private async void NavigateToSidebarItem(string? tag, bool isSettings, object? parameter = null)
	{
		_pendingNavTag = tag;
		_pendingNavIsSettings = isSettings;
		_pendingNavParameter = parameter;
		if (_isNavigating)
		{
			return;
		}
		_isNavigating = true;
		try
		{
			while (true)
			{
				string currentTag = _pendingNavTag;
				bool currentSettings = _pendingNavIsSettings;
				object currentParameter = _pendingNavParameter;
				if (ContentFrame.Visibility == Visibility.Visible && ContentFrame.Content != null)
				{
					await Resona.Helpers.AnimationHelper.PlayExitAnimationAsync(ContentFrame, -20f);
				}
				else if (SettingsContainer.Visibility == Visibility.Visible)
				{
					await Resona.Helpers.AnimationHelper.PlayExitAnimationAsync(SettingsContainer, -20f);
				}
				if (_pendingNavTag != currentTag || _pendingNavIsSettings != currentSettings)
				{
					continue;
				}
				if (currentSettings)
				{
					ContentFrame.Visibility = Visibility.Collapsed;
					SettingsContainer.Visibility = Visibility.Visible;
					Resona.Helpers.AnimationHelper.PlayEntranceAnimation(SettingsContainer);
				}
				else
				{
					SettingsContainer.Visibility = Visibility.Collapsed;
					ContentFrame.Visibility = Visibility.Visible;
					ContentFrame.BackStack.Clear();
					if (!string.IsNullOrEmpty(currentTag))
					{
						switch (currentTag)
						{
						case "library":
							ContentFrame.Content = _libraryPageInstance;
							_libraryPageInstance?.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							if (_libraryPageInstance != null)
							{
								_libraryPageInstance.ResetToLibrary(_library);
							}
							break;
						case "albums":
							if (_albumsPageInstance == null)
							{
								_albumsPageInstance = new AlbumsPage();
							}
							ContentFrame.Content = _albumsPageInstance;
							_albumsPageInstance.LoadData(_library);
							_albumsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							if (currentParameter is string albumName)
							{
								_albumsPageInstance.SetSearch(albumName);
							}
							break;
						case "playlists":
							if (_playlistsPageInstance == null)
							{
								_playlistsPageInstance = new PlaylistsPage();
								ContentFrame.Content = _playlistsPageInstance;
								_playlistsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								_playlistsPageInstance.RefreshAsync();
							}
							else
							{
								ContentFrame.Content = _playlistsPageInstance;
								_playlistsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							}
							break;
						case "artists":
							if (_artistsPageInstance == null)
							{
								_artistsPageInstance = new ArtistsPage();
							}
							ContentFrame.Content = _artistsPageInstance;
							_artistsPageInstance.LoadData(_library);
							_artistsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							if (currentParameter is string artistName)
							{
								_artistsPageInstance.SetSearch(artistName);
							}
							break;
						case "genres":
							if (_genresPageInstance == null)
							{
								_genresPageInstance = new GenresPage();
							}
							ContentFrame.Content = _genresPageInstance;
							_genresPageInstance.LoadData(_library);
							_genresPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							break;
						case "folders":
							if (_foldersPageInstance == null)
							{
								_foldersPageInstance = new FoldersPage();
							}
							ContentFrame.Content = _foldersPageInstance;
							_foldersPageInstance.LoadData(_library);
							_foldersPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							break;
						case "statistics":
							if (_statisticsPageInstance == null)
							{
								_statisticsPageInstance = new StatisticsPage();
							}
							ContentFrame.Content = _statisticsPageInstance;
							_statisticsPageInstance.LoadData(_library);
							break;
						case "queue":
							if (_queuePageInstance == null)
							{
								_queuePageInstance = new QueuePage();
							}
							ContentFrame.Content = _queuePageInstance;
							_queuePageInstance.SetQueue(_manualQueue);
							_queuePageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							break;
						case "download":
							if (_downloadPageInstance == null)
							{
								_downloadPageInstance = new DownloadPage();
							}
							ContentFrame.Content = _downloadPageInstance;
							break;
						}
					}
					Resona.Helpers.AnimationHelper.PlayEntranceAnimation(ContentFrame);
				}
				if (!(_pendingNavTag == currentTag) || _pendingNavIsSettings != currentSettings)
				{
					continue;
				}
				break;
			}
		}
		finally
		{
			_isNavigating = false;
		}
	}

	public void AddToQueue(Track track)
	{
		_manualQueue.Add(track);
		_queuePageInstance?.SetQueue(_manualQueue);
	}

	public void RemoveFromQueue(Track track)
	{
		_manualQueue.Remove(track);
		_queuePageInstance?.SetQueue(_manualQueue);
	}

	public MenuFlyout BuildTrackMenu(Track track, List<Track>? trackList = null, List<Track>? selectedTracks = null)
	{
		MenuFlyout menuFlyout = new MenuFlyout();
		if (selectedTracks != null && selectedTracks.Count > 1 && selectedTracks.Contains(track))
		{
			MenuFlyoutItem item = new MenuFlyoutItem
			{
				Text = $"SÃ©lection de {selectedTracks.Count} titres",
				IsEnabled = false
			};
			menuFlyout.Items.Add(item);
			menuFlyout.Items.Add(new MenuFlyoutSeparator());
			MenuFlyoutItem menuFlyoutItem = new MenuFlyoutItem
			{
				Text = "Ajouter Ã  la file d'attente",
				Icon = new FontIcon
				{
					Glyph = "\ue81e"
				}
			};
			menuFlyoutItem.Click += delegate
			{
				foreach (Track selectedTrack in selectedTracks)
				{
					AddToQueue(selectedTrack);
				}
			};
			menuFlyout.Items.Add(menuFlyoutItem);
			MenuFlyoutSubItem menuFlyoutSubItem = new MenuFlyoutSubItem
			{
				Text = Strings.Current.CS_Ajouteruneplaylist,
				Icon = new FontIcon
				{
					Glyph = "\ue90b"
				}
			};
			menuFlyout.Items.Add(menuFlyoutSubItem);
			LoadPlaylistSubItemsAsync(menuFlyoutSubItem, selectedTracks);
			return menuFlyout;
		}
		MenuFlyoutItem menuFlyoutItem2 = new MenuFlyoutItem
		{
			Text = "Lire",
			Icon = new FontIcon
			{
				Glyph = "\ue768"
			}
		};
		menuFlyoutItem2.Click += delegate
		{
			PlayTrack(track, trackList ?? _library);
		};
		menuFlyout.Items.Add(menuFlyoutItem2);
		MenuFlyoutItem menuFlyoutItem3 = new MenuFlyoutItem
		{
			Text = "Ajouter Ã  la file d'attente",
			Icon = new FontIcon
			{
				Glyph = "\ue81e"
			}
		};
		menuFlyoutItem3.Click += delegate
		{
			AddToQueue(track);
		};
		menuFlyout.Items.Add(menuFlyoutItem3);
		MenuFlyoutSubItem menuFlyoutSubItem2 = new MenuFlyoutSubItem
		{
			Text = Strings.Current.CS_Ajouteruneplaylist,
			Icon = new FontIcon
			{
				Glyph = "\ue90b"
			}
		};
		menuFlyout.Items.Add(menuFlyoutSubItem2);
		LoadPlaylistSubItemsAsync(menuFlyoutSubItem2, new List<Track> { track });
		MenuFlyoutItem menuFlyoutItem4 = new MenuFlyoutItem
		{
			Text = Strings.Current.CS_Autotag,
			Icon = new FontIcon
			{
				Glyph = "\ue943"
			}
		};
		menuFlyoutItem4.Click += delegate
		{
			ShowAutoTagDialogAsync(track);
		};
		menuFlyout.Items.Add(menuFlyoutItem4);
		return menuFlyout;
	}

	private async Task LoadPlaylistSubItemsAsync(MenuFlyoutSubItem parent, List<Track> tracksToAdd)
	{
		try
		{
			foreach (Playlist pl in await App.Cache.LoadAllPlaylistsAsync())
			{
				MenuFlyoutItem item = new MenuFlyoutItem
				{
					Text = pl.Name
				};
				Playlist captured = pl;
				item.Click += async delegate
				{
					bool modified = false;
					foreach (Track track in tracksToAdd)
					{
						if (!captured.TrackIds.Contains(track.Id))
						{
							captured.TrackIds.Add(track.Id);
							modified = true;
						}
					}
					if (modified)
					{
						captured.DateModified = DateTime.UtcNow;
						await App.Cache.UpsertPlaylistAsync(captured);
						_playlistsPageInstance?.RefreshAsync();
					}
				};
				parent.Items.Add(item);
			}
			if (parent.Items.Count == 0)
			{
				parent.Items.Add(new MenuFlyoutItem
				{
					Text = "Aucune playlist",
					IsEnabled = false
				});
			}
		}
		catch
		{
		}
	}

	private static StackPanel MakeField(string label, FrameworkElement input)
	{
		FontIcon item = new FontIcon
		{
			Glyph = "\ue8f1",
			FontSize = 10.0,
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
			Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
		};
		TextBlock item2 = new TextBlock
		{
			Text = label,
			FontSize = 11.0,
			Opacity = 0.7,
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
		};
		StackPanel item3 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Children = 
			{
				(UIElement)item,
				(UIElement)item2
			}
		};
		return new StackPanel
		{
			Children = 
			{
				(UIElement)item3,
				(UIElement)input
			},
			Spacing = 2.0
		};
	}

	public async Task<bool> ShowAutoTagDialogAsync(Track track)
	{
		ContentDialog loadingDialog = new ContentDialog
		{
			Title = Strings.Current.CS_RechercheAutotag,
			Content = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				Children = 
				{
					(UIElement)new ProgressRing
					{
						IsActive = true,
						Width = 24.0,
						Height = 24.0,
						Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
						HorizontalAlignment = HorizontalAlignment.Center
					},
					(UIElement)new TextBlock
					{
						Text = "Analyse de Â« " + track.Title + " Â» â€” " + track.Artist,
						TextWrapping = TextWrapping.Wrap,
						TextAlignment = TextAlignment.Center
					}
				}
			},
			CloseButtonText = Strings.Current.CS_Annuler,
			XamlRoot = ContentFrame.XamlRoot
		};
		loadingDialog.ShowAsync();
		AutoTagResult result = await AutoTagService.LookupAsync(track.Artist, track.Title, track.Duration, track.FilePath);
		loadingDialog.Hide();
		string coverPath = track.CoverArtPath;
		Microsoft.UI.Xaml.Controls.Image coverImage = new Microsoft.UI.Xaml.Controls.Image
		{
			Width = 140.0,
			Height = 140.0,
			Stretch = Stretch.UniformToFill,
			Source = ((!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath)) ? CoverCacheService.GetBitmap(coverPath, 140) : null)
		};
		Border coverPlaceholder = new Border
		{
			Width = 140.0,
			Height = 140.0,
			CornerRadius = new CornerRadius(8.0),
			Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppSurfaceBrush"],
			Child = new FontIcon
			{
				Glyph = "\ue93c",
				FontSize = 40.0,
				Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		if (coverImage.Source != null)
		{
			coverPlaceholder.Child = coverImage;
		}
		Button changeCoverBtn = new Button
		{
			Content = Strings.Current.CS_Changerlapochette,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
			UseLayoutRounding = true,
			Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
			BorderThickness = new Thickness(1.0),
			BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 4.0, 10.0, 4.0)
		};
		changeCoverBtn.Click += async delegate
		{
			Windows.Storage.Pickers.FileOpenPicker picker = new Windows.Storage.Pickers.FileOpenPicker
			{
				ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
				FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" }
			};
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
			if (file != null)
			{
				coverPath = file.Path;
				BitmapImage bmp2 = CoverCacheService.GetBitmap(coverPath, 140);
				if (bmp2 != null)
				{
					coverImage.Source = bmp2;
					coverPlaceholder.Child = coverImage;
				}
			}
		};
		TextBox titleBox = new TextBox
		{
			Text = (result?.Title ?? track.Title),
			PlaceholderText = Strings.Current.CS_Titre
		};
		TextBox artistBox = new TextBox
		{
			Text = (result?.Artist ?? track.Artist),
			PlaceholderText = Strings.Current.CS_Artiste
		};
		TextBox albumBox = new TextBox
		{
			Text = (result?.Album ?? track.Album),
			PlaceholderText = Strings.Current.CS_Album
		};
		Button searchCoverBtn = new Button
		{
			Content = Strings.Current.CS_Chercherenligne,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
			UseLayoutRounding = true,
			Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
			BorderThickness = new Thickness(1.0),
			BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 4.0, 10.0, 4.0)
		};
		ContentDialog editDialog = null;
		searchCoverBtn.Click += async delegate
		{
			string term = (string.IsNullOrWhiteSpace(artistBox.Text) ? "" : (artistBox.Text + " ")) + track.Title + " cover";
			editDialog?.Hide();
			TextBox searchTermBox = new TextBox
			{
				Text = term,
				PlaceholderText = Strings.Current.CS_Termederecherche
			};
			ContentDialog searchTermDialog = new ContentDialog
			{
				Title = Strings.Current.CS_Rechercherunepochett,
				Content = searchTermBox,
				PrimaryButtonText = "Rechercher",
				CloseButtonText = Strings.Current.CS_Annuler,
				XamlRoot = ContentFrame.XamlRoot
			};
			if (await searchTermDialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(searchTermBox.Text))
			{
				editDialog.ShowAsync();
			}
			else
			{
				term = searchTermBox.Text;
				ContentDialog searchLoadingDialog = new ContentDialog
				{
					Title = Strings.Current.CS_Rechercheencours,
					Content = new StackPanel
					{
						HorizontalAlignment = HorizontalAlignment.Center,
						Children = 
						{
							(UIElement)new ProgressRing
							{
								IsActive = true,
								Width = 24.0,
								Height = 24.0,
								Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
								HorizontalAlignment = HorizontalAlignment.Center
							},
							(UIElement)new TextBlock
							{
								Text = "Recherche de pochettes pour Â« " + term + " Â»...",
								TextWrapping = TextWrapping.Wrap,
								TextAlignment = TextAlignment.Center
							}
						}
					},
					CloseButtonText = Strings.Current.CS_Annuler,
					XamlRoot = ContentFrame.XamlRoot
				};
				searchLoadingDialog.ShowAsync();
				List<string> results = (await SearchCoversOnlineAsync(term)).Distinct().Take(10).ToList();
				searchLoadingDialog.Hide();
				if (results.Count == 0)
				{
					HyperlinkButton googleBtn = new HyperlinkButton
					{
						Content = Strings.Current.CS_CherchersurGoogleIma,
						NavigateUri = new Uri("https://www.google.com/search?tbm=isch&q=" + Uri.EscapeDataString(term))
					};
					await new ContentDialog
					{
						Title = Strings.Current.CS_Aucunrsultat,
						Content = new StackPanel
						{
							Children = 
							{
								(UIElement)new TextBlock
								{
									Text = "Aucune pochette trouvÃ©e pour Â« " + term + " Â»."
								},
								(UIElement)googleBtn
							}
						},
						CloseButtonText = "OK",
						XamlRoot = ContentFrame.XamlRoot
					}.ShowAsync();
					editDialog.ShowAsync();
				}
				else
				{
					GridView imageGrid = new GridView
					{
						SelectionMode = ListViewSelectionMode.Single,
						MaxHeight = 400.0
					};
					foreach (string url in results)
					{
						try
						{
							imageGrid.Items.Add(new Microsoft.UI.Xaml.Controls.Image
							{
								Source = new BitmapImage(new Uri(url)),
								Width = 150.0,
								Height = 150.0,
								Stretch = Stretch.UniformToFill,
								Margin = new Thickness(5.0)
							});
						}
						catch
						{
						}
					}
					if (imageGrid.Items.Count > 0)
					{
						imageGrid.SelectedIndex = 0;
					}
					ContentDialog applyDialog = new ContentDialog
					{
						Title = Strings.Current.CS_Choisirunepochette,
						Content = imageGrid,
						PrimaryButtonText = Strings.Current.CS_Appliquer,
						CloseButtonText = Strings.Current.CS_Annuler,
						XamlRoot = ContentFrame.XamlRoot
					};
					if (await applyDialog.ShowAsync() == ContentDialogResult.Primary)
					{
						Microsoft.UI.Xaml.Controls.Image selectedImg = imageGrid.SelectedItem as Microsoft.UI.Xaml.Controls.Image;
						ImageSource imageSource = selectedImg?.Source;
						if (imageSource is BitmapImage bmp2)
						{
							coverPath = bmp2.UriSource.ToString();
							coverImage.Source = selectedImg.Source;
							coverPlaceholder.Child = coverImage;
						}
					}
					editDialog.ShowAsync();
				}
			}
		};
		StackPanel coverPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 0.0, 16.0, 0.0)
		};
		coverPanel.Children.Add(coverPlaceholder);
		coverPanel.Children.Add(changeCoverBtn);
		coverPanel.Children.Add(searchCoverBtn);
		TextBox genreBox = new TextBox
		{
			Text = (result?.Genre ?? track.Genre ?? ""),
			PlaceholderText = Strings.Current.CS_Genre
		};
		TextBox yearBox = new TextBox
		{
			Text = (result?.Year?.ToString() ?? ""),
			PlaceholderText = Strings.Current.CS_Anne
		};
		TextBox trackNumBox = new TextBox
		{
			Text = (result?.TrackNumber?.ToString() ?? ((track.TrackNumber > 0) ? track.TrackNumber.ToString() : ""))
		};
		StackPanel form = new StackPanel
		{
			Spacing = 6.0
		};
		form.Children.Add(MakeField(Strings.Current.CS_Titre, titleBox));
		form.Children.Add(MakeField(Strings.Current.CS_Artiste, artistBox));
		form.Children.Add(MakeField(Strings.Current.CS_Album, albumBox));
		form.Children.Add(MakeField(Strings.Current.CS_Genre, genreBox));
		form.Children.Add(MakeField(Strings.Current.CS_Anne, yearBox));
		form.Children.Add(MakeField(Strings.Current.CS_Piste, trackNumBox));
		form.Children.Add(new TextBlock
		{
			Text = ((result != null) ? "âœ… MÃ©tadonnÃ©es trouvÃ©es et prÃ©-remplies." : "âš \ufe0f Aucune donnÃ©e trouvÃ©e â€” remplis manuellement."),
			FontSize = 11.0,
			Opacity = 0.6,
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
			TextWrapping = TextWrapping.Wrap
		});
		Grid bodyGrid = new Grid
		{
			ColumnSpacing = 0.0,
			Width = 520.0
		};
		bodyGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		bodyGrid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Grid.SetColumn(coverPanel, 0);
		Grid.SetColumn(form, 1);
		bodyGrid.Children.Add(coverPanel);
		bodyGrid.Children.Add(form);
		CheckBox writeToFileCheckBox = new CheckBox
		{
			Content = Strings.Current.CS_Enregistrerlesmodifi,
			IsChecked = App.Settings.Current.AutoTagWriteToFile,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0),
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
		};
		StackPanel dialogContentPanel = new StackPanel
		{
			Children = 
			{
				(UIElement)bodyGrid,
				(UIElement)writeToFileCheckBox
			}
		};
		editDialog = new ContentDialog
		{
			Title = Strings.Current.CS_Autotagdition,
			Content = dialogContentPanel,
			PrimaryButtonText = Strings.Current.CS_Sauvegarder,
			CloseButtonText = Strings.Current.CS_Annuler,
			DefaultButton = ContentDialogButton.Primary,
			XamlRoot = ContentFrame.XamlRoot
		};
		if (await editDialog.ShowAsync() == ContentDialogResult.Primary)
		{
			AutoTagResult data = new AutoTagResult
			{
				Title = titleBox.Text,
				Artist = artistBox.Text,
				Album = albumBox.Text,
				Genre = genreBox.Text,
				Year = (int.TryParse(yearBox.Text, out var y) ? new int?(y) : ((int?)null)),
				TrackNumber = (int.TryParse(trackNumBox.Text, out var tn) ? new int?(tn) : ((int?)null)),
				CoverPath = coverPath
			};
			bool saved = false;
			if (titleBox.Text != track.Title || artistBox.Text != track.Artist || albumBox.Text != track.Album || genreBox.Text != (track.Genre ?? "") || yearBox.Text != "" || trackNumBox.Text != "")
			{
				saved = true;
				track.Title = data.Title ?? track.Title;
				track.Artist = data.Artist ?? track.Artist;
				track.Album = data.Album ?? track.Album;
				track.Genre = data.Genre ?? track.Genre ?? "";
				if (data.Year.HasValue)
				{
					track.Year = data.Year.Value;
				}
				if (data.TrackNumber.HasValue)
				{
					track.TrackNumber = data.TrackNumber.Value;
				}
			}
			if (coverPath != track.CoverArtPath && !string.IsNullOrWhiteSpace(coverPath))
			{
				string coverDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "Covers");
				string newCoverName = $"{track.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";
				string coverDest = System.IO.Path.Combine(coverDir, newCoverName);
				try
				{
					string[] oldCovers = Directory.GetFiles(coverDir, track.Id + "*.jpg");
					string[] array = oldCovers;
					foreach (string old in array)
					{
						try
						{
							File.Delete(old);
						}
						catch
						{
						}
					}
					if (coverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
					{
						using HttpClient hc = new HttpClient();
						await File.WriteAllBytesAsync(coverDest, await hc.GetByteArrayAsync(coverPath));
						track.CoverArtPath = coverDest;
						saved = true;
					}
					else if (File.Exists(coverPath))
					{
						File.Copy(coverPath, coverDest, overwrite: true);
						track.CoverArtPath = coverDest;
						saved = true;
					}
				}
				catch
				{
				}
			}
			if (saved && writeToFileCheckBox.IsChecked == true)
			{
				data.CoverPath = track.CoverArtPath;
				AutoTagService.WriteMetadata(track.FilePath, data);
				App.Settings.Current.AutoTagWriteToFile = true;
				App.Settings.SaveSync();
			}
			else if (writeToFileCheckBox.IsChecked == false && App.Settings.Current.AutoTagWriteToFile)
			{
				App.Settings.Current.AutoTagWriteToFile = false;
				App.Settings.SaveSync();
			}
			if (saved)
			{
				await App.Cache.UpdateTrackAsync(track);
				if (App.NowPlayingId == track.Id)
				{
					NowPlayingTitle.Content = track.Title;
					NowPlayingArtist.Content = track.Artist;
					NowPlayingAlbum.Content = track.Album;
					if (!string.IsNullOrEmpty(track.CoverArtPath))
					{
						BitmapImage bmp = CoverCacheService.GetBitmap(track.CoverArtPath, 140);
						if (bmp != null)
						{
							NowPlayingCover.Source = bmp;
							NowPlayingPlaceholderIcon.Visibility = Visibility.Collapsed;
						}
					}
				}
			}
			return true;
		}
		return false;
	}

	private void AudioEngine_PlaybackStopped(object? sender, EventArgs e)
	{
		base.DispatcherQueue.TryEnqueue(delegate
		{
			TimeSpan currentPosition = App.AudioEngine.CurrentPosition;
			TimeSpan totalDuration = App.AudioEngine.TotalDuration;
			if (totalDuration.TotalSeconds > 0.0 && currentPosition.TotalSeconds >= totalDuration.TotalSeconds - 0.5 && _queue.Count != 0 && _queueIndex >= 0)
			{
				switch (_playbackMode)
				{
				case PlaybackMode.RepeatOne:
					PlayTrack(_queue[_queueIndex], _queue);
					break;
				case PlaybackMode.Shuffle:
					PlayTrack(PickRandomTrack(), _queue);
					break;
				case PlaybackMode.RepeatAll:
					PlayTrack(_queue[(_queueIndex + 1) % _queue.Count], _queue);
					break;
				case PlaybackMode.Off:
					PlayPauseIcon.Glyph = "\ue768";
					PlayPauseIcon.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
					break;
				}
			}
		});
	}

	private void ShowPlayerBar()
	{
		if (PlayerBar.Visibility != Visibility.Visible)
		{
			PlayerBar.Visibility = Visibility.Visible;
			PlayerBar.Opacity = 0.0;
			PlayerBar.Height = 0.0;
			if (App.Settings.Current.PlayerGradientOverflowEnabled && App.Settings.Current.Backdrop == AppBackdropStyle.Solid)
			{
				PlayerGradientOverflow.Visibility = Visibility.Visible;
				PlayerGradientFadeLayer.Visibility = Visibility.Visible;
			}
			TranslateTransform translateTransform = new TranslateTransform
			{
				Y = 15.0
			};
			PlayerBar.RenderTransform = translateTransform;
			Storyboard storyboard = new Storyboard();
			DoubleAnimation doubleAnimation = new DoubleAnimation
			{
				From = 0.0,
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(250.0),
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			Storyboard.SetTarget(doubleAnimation, PlayerBar);
			Storyboard.SetTargetProperty(doubleAnimation, "Opacity");
			DoubleAnimation doubleAnimation2 = new DoubleAnimation
			{
				From = 15.0,
				To = 0.0,
				Duration = TimeSpan.FromMilliseconds(300.0),
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			Storyboard.SetTarget(doubleAnimation2, translateTransform);
			Storyboard.SetTargetProperty(doubleAnimation2, "Y");
			DoubleAnimation doubleAnimation3 = new DoubleAnimation
			{
				From = 0.0,
				To = 98.0,
				Duration = TimeSpan.FromMilliseconds(300.0),
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				},
				EnableDependentAnimation = true
			};
			Storyboard.SetTarget(doubleAnimation3, PlayerBar);
			Storyboard.SetTargetProperty(doubleAnimation3, "Height");
			storyboard.Children.Add(doubleAnimation);
			storyboard.Children.Add(doubleAnimation2);
			storyboard.Children.Add(doubleAnimation3);
			storyboard.Completed += delegate
			{
				PlayerBar.ClearValue(FrameworkElement.HeightProperty);
				PlayerBar.RenderTransform = new TranslateTransform();
			};
			storyboard.Begin();
		}
	}

	private void NowPlayingArtist_Click(object sender, RoutedEventArgs e)
	{
		if (NowPlayingArtist.Content is string text && !string.IsNullOrWhiteSpace(text))
		{
			NavigateToArtist(text);
		}
	}

	private void NowPlayingAlbum_Click(object sender, RoutedEventArgs e)
	{
		if (NowPlayingAlbum.Content is string text && !string.IsNullOrWhiteSpace(text))
		{
			NavigateToAlbum(text);
		}
	}

	public void TogglePlayPause()
	{
		PlayPauseButton_Click(this, new RoutedEventArgs());
	}

	private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
	{
		switch (App.AudioEngine.State)
		{
		case NAudio.Wave.PlaybackState.Playing:
			App.AudioEngine.Pause();
			PlayPauseIcon.Glyph = "\ue768";
			PlayPauseIcon.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
			return;
		case NAudio.Wave.PlaybackState.Stopped:
		{
			TimeSpan totalDuration = App.AudioEngine.TotalDuration;
			TimeSpan currentPosition = App.AudioEngine.CurrentPosition;
			if (totalDuration.TotalSeconds > 0.0 && currentPosition.TotalSeconds >= totalDuration.TotalSeconds - 0.5)
			{
				App.AudioEngine.Seek(TimeSpan.Zero);
			}
			break;
		}
		}
		App.AudioEngine.Resume();
		PlayPauseIcon.Glyph = "\ue769";
		PlayPauseIcon.Margin = new Thickness(0.0);
	}

	public void SetShuffleModeAndPlay(Track track, List<Track> queue)
	{
		_playbackMode = PlaybackMode.Shuffle;
		UpdateRepeatButtonVisual();
		PlayTrack(track, queue);
	}

	public void NavigateToPlaylistDetail(Playlist playlist, List<Track> librarySnapshot)
	{
		ContentFrame.Navigate(typeof(PlaylistDetailPage), (playlist, librarySnapshot), NoTransition);
		base.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, delegate
		{
			if (ContentFrame.Content is PlaylistDetailPage playlistDetailPage)
			{
				playlistDetailPage.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
			}
		});
	}

	public async void ShowTrackCollection(string title, List<Track> tracks, string? subtitle = null)
	{
		if (ContentFrame.Content != null && ContentFrame.Visibility == Visibility.Visible)
		{
			await Resona.Helpers.AnimationHelper.PlayExitAnimationAsync(ContentFrame, -20f);
		}
		if (_libraryPageInstance == null)
		{
			_libraryPageInstance = new LibraryPage();
		}
		_libraryPageInstance.ShowCollection(title, subtitle, tracks);
		ContentFrame.Content = _libraryPageInstance;
		Resona.Helpers.AnimationHelper.PlayEntranceAnimation(ContentFrame);
	}

	private void UpdateRepeatButtonVisual()
	{
		RepeatOneBadge.Visibility = Visibility.Collapsed;
		switch (_playbackMode)
		{
		case PlaybackMode.Off:
			RepeatIcon.Glyph = "\ue8ee";
			RepeatIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
			ToolTipService.SetToolTip(RepeatButton, "Lecture simple");
			break;
		case PlaybackMode.RepeatAll:
			RepeatIcon.Glyph = "\ue8ee";
			RepeatIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
			ToolTipService.SetToolTip(RepeatButton, "RÃ©pÃ©ter la liste");
			break;
		case PlaybackMode.RepeatOne:
			RepeatIcon.Glyph = "\ue8ee";
			RepeatIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
			RepeatOneBadge.Visibility = Visibility.Visible;
			ToolTipService.SetToolTip(RepeatButton, "RÃ©pÃ©ter ce morceau");
			break;
		case PlaybackMode.Shuffle:
			RepeatIcon.Glyph = "\ue8b1";
			RepeatIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
			ToolTipService.SetToolTip(RepeatButton, "Lecture alÃ©atoire");
			break;
		}
	}

	private void RepeatButton_Click(object sender, RoutedEventArgs e)
	{
		PlaybackMode playbackMode = _playbackMode;
		if (1 == 0)
		{
		}
		PlaybackMode playbackMode2 = playbackMode switch
		{
			PlaybackMode.Off => PlaybackMode.RepeatAll, 
			PlaybackMode.RepeatAll => PlaybackMode.RepeatOne, 
			PlaybackMode.RepeatOne => PlaybackMode.Shuffle, 
			_ => PlaybackMode.Off, 
		};
		if (1 == 0)
		{
		}
		_playbackMode = playbackMode2;
		App.Settings.Current.SavedPlaybackMode = (int)_playbackMode;
		App.Settings.SaveAsync();
		UpdateRepeatButtonVisual();
	}

	private void NextButton_Click(object sender, RoutedEventArgs e)
	{
		if (_queue.Count != 0)
		{
			if (_playbackMode == PlaybackMode.Shuffle)
			{
				PlayTrack(PickRandomTrack(), _queue);
			}
			else if (_queueIndex < _queue.Count - 1)
			{
				PlayTrack(_queue[_queueIndex + 1], _queue);
			}
			else if (_playbackMode == PlaybackMode.RepeatAll)
			{
				PlayTrack(_queue[0], _queue);
			}
		}
	}

	private void PrevButton_Click(object sender, RoutedEventArgs e)
	{
		if (_queue.Count != 0)
		{
			if (_playbackMode == PlaybackMode.Shuffle)
			{
				PlayTrack(PickRandomTrack(), _queue);
			}
			else if (_queueIndex > 0)
			{
				PlayTrack(_queue[_queueIndex - 1], _queue);
			}
			else if (_playbackMode == PlaybackMode.RepeatAll)
			{
				PlayTrack(_queue[_queue.Count - 1], _queue);
			}
		}
	}

	private Track PickRandomTrack()
	{
		if (_queue.Count == 1)
		{
			return _queue[0];
		}
		int num;
		do
		{
			num = _random.Next(_queue.Count);
		}
		while (num == _queueIndex);
		return _queue[num];
	}

	private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		_isSliderDragging = true;
	}

	private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		TimeSpan totalDuration = App.AudioEngine.TotalDuration;
		if (totalDuration.TotalSeconds > 0.0)
		{
			App.AudioEngine.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
			if (App.AudioEngine.State == NAudio.Wave.PlaybackState.Stopped && ProgressSlider.Value < totalDuration.TotalSeconds)
			{
				App.AudioEngine.Resume();
				PlayPauseIcon.Glyph = "\ue769";
				PlayPauseIcon.Margin = new Thickness(0.0);
			}
		}
		_isSliderDragging = false;
	}

	private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdateCustomProgress();
	}

	private void ProgressSlider_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		UpdateCustomProgress();
	

    }
    private void UpdateCustomProgress()
    {
        if (ProgressSlider == null || CustomProgressFill == null || CustomProgressThumb == null || CustomProgressTrack == null) return;
        double max = Math.Max(0.001, ProgressSlider.Maximum);
        double percentage = ProgressSlider.Value / max;
        double actualWidth = CustomProgressTrack.ActualWidth;
        if (actualWidth > 0)
        {
            double width = actualWidth * percentage;
            CustomProgressFill.Width = width;
            CustomProgressThumb.Margin = new Microsoft.UI.Xaml.Thickness(width, 5, 0, 0);
        }
    

    }
    public async void ShowTrackInfo(Resona.Models.Track track)
    {
        if (track == null) return;
        EnsureInfoOverlayCreated();
        if (_infoTrackTitle != null) _infoTrackTitle.Text = track.Title;
        if (_infoTrackArtist != null) _infoTrackArtist.Text = track.Artist;
        if (_infoContent != null) _infoContent.Text = Resona.Models.Strings.Current.TrackInfo_Loading;
        
        if (_infoOverlay != null) _infoOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        _infoOverlayOpen = true;
        
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var fi = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fi, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fi, "Opacity");
        sb.Children.Add(fi);
        
        if (_infoOverlay.RenderTransform is not Microsoft.UI.Xaml.Media.CompositeTransform) _infoOverlay.RenderTransform = new Microsoft.UI.Xaml.Media.CompositeTransform();
        _infoOverlay.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        var transform = (Microsoft.UI.Xaml.Media.CompositeTransform)_infoOverlay.RenderTransform;
        transform.ScaleX = 0.95; transform.ScaleY = 0.95; transform.TranslateY = 20;
        
        var sx = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ExponentialEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut, Exponent = 4 } };
        var sy = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ExponentialEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut, Exponent = 4 } };
        var ty = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ExponentialEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut, Exponent = 4 } };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(sx, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(sy, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(ty, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
        sb.Children.Add(sx); sb.Children.Add(sy); sb.Children.Add(ty);
        
        sb.Begin();

        if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.Equals("Inconnu", StringComparison.OrdinalIgnoreCase) || track.Artist.Equals("Unknown", StringComparison.OrdinalIgnoreCase) || track.Artist.Equals("Unknown Artist", StringComparison.OrdinalIgnoreCase) || track.Artist.Equals("Artiste inconnu", StringComparison.OrdinalIgnoreCase))
        {
            if (_infoContent != null) _infoContent.Text = Resona.Models.Strings.Current.TrackInfo_NoBio;
            return;
        }

        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Resona/1.0");
            string artistEncoded = Uri.EscapeDataString(track.Artist);
            string lang = Resona.Models.Strings.Current.IsFr ? "fr" : "en";
            string url = $"https://{lang}.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch={artistEncoded}&gsrlimit=1&prop=extracts&exintro&explaintext&format=json";
            
            var response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            
            var root = System.Text.Json.JsonDocument.Parse(json);
            var pages = root.RootElement.GetProperty("query").GetProperty("pages");
            
            string infoText = Resona.Models.Strings.Current.TrackInfo_NoBio;
            bool isDisambiguation = false;
            
            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("extract", out var extractElement))
                {
                    string extract = extractElement.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(extract))
                    {
                        infoText = extract;
                        if (extract.Contains("may refer to:", StringComparison.OrdinalIgnoreCase) || 
                            extract.Contains("peut faire référence à", StringComparison.OrdinalIgnoreCase) || 
                            extract.Contains("est une page d'homonymie", StringComparison.OrdinalIgnoreCase) || 
                            extract.Contains("may also refer to:", StringComparison.OrdinalIgnoreCase))
                        {
                            isDisambiguation = true;
                        }
                        break;
                    }
                }
            }
            
            if (isDisambiguation)
            {
                string searchSuffix = Resona.Models.Strings.Current.IsFr ? " musique" : " musician";
                string artistEncodedWithSuffix = Uri.EscapeDataString(track.Artist + searchSuffix);
                url = $"https://{lang}.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch={artistEncodedWithSuffix}&gsrlimit=1&prop=extracts&exintro&explaintext&format=json";
                
                response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
                root = System.Text.Json.JsonDocument.Parse(json);
                if (root.RootElement.GetProperty("query").TryGetProperty("pages", out pages))
                {
                    foreach (var page in pages.EnumerateObject())
                    {
                        if (page.Value.TryGetProperty("extract", out var extractElement))
                        {
                            string extract = extractElement.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(extract))
                            {
                                infoText = extract;
                                break;
                            }
                        }
                    }
                }
            }
            
            if (_infoContent != null) _infoContent.Text = infoText;
        }
        catch (Exception ex)
        {
            if (_infoContent != null) _infoContent.Text = string.Format(Resona.Models.Strings.Current.TrackInfo_Error, ex.Message);
        }
    

    }
    private string? _pendingNavTag;
    private bool _pendingNavIsSettings;
    private object? _pendingNavParameter;
    private bool _isNavigating = false;
    private static readonly Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo NoTransition = new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo();

    private void EnsureInfoOverlayCreated()
    {
        if (_infoOverlay != null) return;
        var mainPanel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 20, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center, MaxWidth = 800 };
        _infoTrackTitle = new Microsoft.UI.Xaml.Controls.TextBlock { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Bold, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap, TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center };
        _infoTrackArtist = new Microsoft.UI.Xaml.Controls.TextBlock { FontSize = 20, Opacity = 0.8, Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 20), TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap, TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center };
        _infoContent = new Microsoft.UI.Xaml.Controls.TextBlock { FontSize = 16, Opacity = 0.9, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap, IsTextSelectionEnabled = true };
        mainPanel.Children.Add(_infoTrackTitle); mainPanel.Children.Add(_infoTrackArtist); mainPanel.Children.Add(_infoContent);
        mainPanel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        mainPanel.PointerPressed += (s, e) => { e.Handled = true; };
        
        var scrollViewer = new Microsoft.UI.Xaml.Controls.ScrollViewer { Content = mainPanel, Padding = new Microsoft.UI.Xaml.Thickness(32, 24, 32, 40), VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        
        var bg = new Microsoft.UI.Xaml.Shapes.Rectangle { Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(220, 0, 0, 0)) };
        _infoOverlay = new Microsoft.UI.Xaml.Controls.Grid { Visibility = Microsoft.UI.Xaml.Visibility.Collapsed, Opacity = 0, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch };
        Microsoft.UI.Xaml.Controls.Grid.SetRowSpan(_infoOverlay, 2);
        Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(_infoOverlay, 100);
        _infoOverlay.Children.Add(bg); _infoOverlay.Children.Add(scrollViewer);
        _infoOverlay.PointerPressed += (s, e) => { HideInfoOverlay(); e.Handled = true; };
        
        RootGrid.Children.Add(_infoOverlay);
    }

    private void HideInfoOverlay()
    {
        if (!_infoOverlayOpen || _infoOverlay == null) return;
        _infoOverlayOpen = false;
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var fo = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn } };
        var ty = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 10, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn } };
        var sx = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0.98, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn } };
        var sy = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0.98, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn } };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fo, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fo, "Opacity");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(ty, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(sx, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(sy, _infoOverlay); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
        sb.Children.Add(fo); sb.Children.Add(ty); sb.Children.Add(sx); sb.Children.Add(sy);
        sb.Completed += (_, __) => { if (!_infoOverlayOpen) _infoOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed; };
        sb.Begin();
    }
    private void RootGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        HideInfoOverlay();
    }
    
    private void RootNav_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
    {
        string tag = args.SelectedItemContainer?.Tag?.ToString() ?? "";
        NavigateToSidebarItem(tag, args.IsSettingsSelected, null);
    }
    
    private void LyricsButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Resona.Helpers.AnimationHelper.ApplyBouncyScale(LyricsButton, 1.05f);
        LyricsButtonBg.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 255, 255, 255));
    }
    
    private void NowPlayingTitle_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_currentIndex >= 0 && _currentIndex < _library.Count)
            ShowTrackInfo(_library[_currentIndex]);
    }

}


