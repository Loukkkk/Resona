using System.Text.Json;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Globalization;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using Windows.Graphics;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI;
using Windows.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Text;
using System.Text.Json.Serialization;
using Windows.Storage;
using Windows.Media;
using Windows.Media.Playback;
using Microsoft.UI.Dispatching;
using DispatcherQueueHandler = Microsoft.UI.Dispatching.DispatcherQueueHandler;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueuePriority = Microsoft.UI.Dispatching.DispatcherQueuePriority;
using Path = System.IO.Path;
using NAudio.Wave;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.CodeDom.Compiler;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Windows.Foundation;
using Resona.Models;
using Resona.Services;
using Resona.Views;
using Resona.Helpers;
using Resona.Converters;
using WinRT.Interop;

namespace Resona;

	public sealed partial class MainWindow : Window
	{
		[ComImport]
		[Guid("00021401-0000-0000-C000-000000000046")]
		internal class ShellLink
		{
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("000214F9-0000-0000-C000-000000000046")]
		internal interface IShellLink
		{
			void GetPath([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out nint pfd, int fFlags);

			void GetIDList(out nint ppidl);

			void SetIDList(nint pidl);

			void GetDescription([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);

			void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

			void GetWorkingDirectory([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);

			void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

			void GetArguments([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);

			void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

			void GetHotkey(out short pwHotkey);

			void SetHotkey(short wHotkey);

			void GetShowCmd(out int piShowCmd);

			void SetShowCmd(int iShowCmd);

			void GetIconLocation([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);

			void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

			void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);

			void Resolve(nint hwnd, int fFlags);

			void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
		internal interface IPropertyStore
		{
			void GetCount(out uint cProps);

			void GetAt(uint iProp, out object pkey);

			void GetValue(ref PropertyKey key, out object pv);

			void SetValue(ref PropertyKey key, ref PropVariant propvar);

			void Commit();
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("0000010b-0000-0000-C000-000000000046")]
		internal interface IPersistFile
		{
			void GetClassID(out Guid pClassID);

			[PreserveSig]
			int IsDirty();

			void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

			void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);

			void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

			void GetCurFile([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
		}

		internal struct PropertyKey
		{
			public Guid fmtid;

			public uint pid;
		}

		[StructLayout(LayoutKind.Explicit)]
		internal struct PropVariant
		{
			[FieldOffset(0)]
			public ushort vt;

			[FieldOffset(8)]
			public nint pwszVal;
		}

		private enum PlaybackMode
		{
			Off,
			RepeatAll,
			RepeatOne,
			Shuffle
		}

		private record LrcLine(TimeSpan Time, string Text);

		private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int X;

			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MINMAXINFO
		{
			public POINT ptReserved;

			public POINT ptMaxSize;

			public POINT ptMaxPosition;

			public POINT ptMinTrackSize;

			public POINT ptMaxTrackSize;
		}

		[ComImport]
		[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface ITaskbarList3
		{
			void HrInit();

			void AddTab(nint hwnd);

			void DeleteTab(nint hwnd);

			void ActivateTab(nint hwnd);

			void SetActiveAlt(nint hwnd);

			void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

			void SetProgressValue(nint hwnd, ulong ullCompleted, ulong ullTotal);

			void SetProgressState(nint hwnd, int tbpFlags);

			void RegisterTab(nint hwndTab, nint hwndMDI);

			void UnregisterTab(nint hwndTab);

			void SetTabOrder(nint hwndTab, nint hwndInsertBefore);

			void SetTabActive(nint hwndTab, nint hwndMDI, uint dwReserved);

			void ThumbBarAddButtons(nint hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray)] THUMBBUTTON[] pButton);

			void ThumbBarUpdateButtons(nint hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray)] THUMBBUTTON[] pButton);

			void ThumbBarSetImageList(nint hwnd, nint himl);

			void SetOverlayIcon(nint hwnd, nint hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);

			void SetThumbnailTooltip(nint hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);

			void SetThumbnailClip(nint hwnd, ref RECT prcClip);
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct THUMBBUTTON
		{
			public uint dwMask;

			public uint iId;

			public uint iBitmap;

			public nint hIcon;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szTip;

			public uint dwFlags;
		}

		private struct RECT
		{
			public int left;

			public int top;

			public int right;

			public int bottom;
		}

		[ComImport]
		[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
		[ClassInterface(ClassInterfaceType.None)]
		private class TaskbarInstance
		{
		}


		private string? _pendingNavTag;

		private bool _pendingNavIsSettings;

		private object? _pendingNavParameter;

		private bool _isNavigating;

		public static bool LastClickWasXButton;

		private ITaskbarList3 _taskbar;

		private THUMBBUTTON _thumbPlay;

		private THUMBBUTTON _thumbPrev;

		private THUMBBUTTON _thumbNext;

		private nint _hIconPlay;

		private nint _hIconPause;

		private MediaPlayer _smtcPlayer;

		private SystemMediaTransportControls _smtc;

		private object? _lastNavSelectedItem;

		private List<Track> _library;

		private LibraryPage? _libraryPageInstance;

		private AlbumsPage? _albumsPageInstance;

		private PlaylistsPage? _playlistsPageInstance;

		private ArtistsPage? _artistsPageInstance;

		private GenresPage? _genresPageInstance;

		private FoldersPage? _foldersPageInstance;

		private StatisticsPage? _statisticsPageInstance;

		private DownloadPage? _downloadPageInstance;

		private QueuePage? _queuePageInstance;

		private int _currentIndex;

		private string? _nowPlayingId;

		private string? _nowPlayingFilePath;

		private List<Track> _queue;

		private int _queueIndex;

		private Stack<Track> _playbackHistory;

		private Stack<Track> _playbackFuture;

		private readonly List<Track> _manualQueue;

		private string? _queueSourceName;

		private bool _isCurrentlyPlayingManualQueue;

		private bool _isUpNextPanelOpen;

		private static readonly Dictionary<string, Color?> _coverColorCache = new Dictionary<string, Color?>();

		private PlaybackMode _playbackMode;

		private readonly Random _random;

		private List<Track> _shuffleUpcoming;

		private DispatcherTimer? _positionTimer;

		private bool _isSliderDragging;

		private bool _crossfadeTriggered;

		private List<LrcLine> _lrcLines;

		private int _lrcCurrentIndex;

		private bool _lyricsOverlayOpen;

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

		private const int DWMWA_BORDER_COLOR = 34;

		private const int DWMWA_CAPTION_COLOR = 35;

		private WndProcDelegate? _wndProcDelegate;

		private nint _originalWndProc;

		private const int GWLP_WNDPROC = -4;

		private const uint WM_GETMINMAXINFO = 36u;

		private const uint WM_COMMAND = 273u;

		private const uint THBN_CLICKED = 6144u;

		private const int MinWidthPx = 1200;

		private const int MinHeightPx = 600;

		private const double GradientFadeHeight = 600.0;

		private const double ColorLayerExtraPad = 0.0;

		private Color? _gradientStartColor;

		private Color? _gradientEndColor;

		private static readonly Thickness NavContentBorderDefault = new Thickness(0.0, 1.0, 0.0, 0.0);

		private static readonly Thickness NavContentBorderMinimal = new Thickness(0.0, 1.0, 0.0, 0.0);

		private static readonly Thickness NavContentBorderHidden = new Thickness(0.0);

		private bool _isNowPlayingModeActive;

		private Color _lastComputedAvgColor;

		private bool _isManuallyClosingPane;

		private static readonly SolidColorBrush transparentBrush = new SolidColorBrush(Colors.Transparent);

		private bool _isFetchingCovers;

		private HashSet<string> _failedCoverSearches;

		private bool _scanInProgress;

		private bool _rescanPending;

		private readonly HashSet<string> _dirtyPaths;

		private readonly object _dirtyLock;

		private Storyboard? _scanStoryboard;

		private CancellationTokenSource? _reanalyzeCts;

		private const double LyricsButtonApproxWidthPx = 92.0;

		private const double UpNextButtonApproxWidthPx = 30.0;

		private const double MiniPlayerButtonApproxWidthPx = 30.0;

		private const double PlaybackControlsWideWidthPx = 1900.0;

		private Grid? _infoOverlay;

		private bool _infoOverlayOpen;

		private TextBlock? _infoTrackTitle;

		private TextBlock? _infoTrackArtist;

		private TextBlock? _infoContent;

		private double _previousVolume;

		private List<Track> _upNextDisplayList;

		private ObservableCollection<Track> _upNextObsList;

		private bool _isMiniPlayerMode;

		private int _savedMainWindowWidth;

		private int _savedMainWindowHeight;

		private int _savedMainWindowX;

		private int _savedMainWindowY;





























































































		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2608")]
		public List<Track> Library => _library;

		public static event Action GlobalClickOutside;

		private void CreateStartMenuShortcut()
		{
			try
			{
				string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
				string pszFileName = Path.Combine(folderPath, "Resona.lnk");
				IShellLink shellLink = (IShellLink)new ShellLink();
				string processPath = Environment.ProcessPath;
				shellLink.SetPath(processPath);
				shellLink.SetWorkingDirectory(Path.GetDirectoryName(processPath));
				shellLink.SetDescription("Resona");
				IPropertyStore propertyStore = (IPropertyStore)shellLink;
				PropertyKey key = new PropertyKey
				{
					fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
					pid = 5u
				};
				PropVariant propvar = new PropVariant
				{
					vt = 31,
					pwszVal = Marshal.StringToCoTaskMemUni("Resona")
				};
				propertyStore.SetValue(ref key, ref propvar);
				propertyStore.Commit();
				IPersistFile persistFile = (IPersistFile)shellLink;
				persistFile.Save(pszFileName, fRemember: false);
				Marshal.FreeCoTaskMem(propvar.pwszVal);
			}
			catch
			{
			}
		}

		private void InitTaskbarThumbnailButtons()
		{
			try
			{
				nint windowHandle = WindowNative.GetWindowHandle((object)this);
				ITaskbarList3 taskbarList = (ITaskbarList3)new TaskbarInstance();
				taskbarList.HrInit();
				nint iconForText = GetIconForText("\ue76b");
				nint iconForText2 = GetIconForText("\ue768");
				nint iconForText3 = GetIconForText("\ue769");
				nint iconForText4 = GetIconForText("\ue76c");
				_hIconPlay = iconForText2;
				_hIconPause = iconForText3;
				_thumbPrev = new THUMBBUTTON
				{
					dwMask = 15u,
					iId = 101u,
					hIcon = iconForText,
					szTip = "PrÃƒÆ’Ã‚Â©cÃƒÆ’Ã‚Â©dent",
					dwFlags = 0u
				};
				_thumbPlay = new THUMBBUTTON
				{
					dwMask = 15u,
					iId = 100u,
					hIcon = iconForText2,
					szTip = "Lecture/Pause",
					dwFlags = 0u
				};
				_thumbNext = new THUMBBUTTON
				{
					dwMask = 15u,
					iId = 102u,
					hIcon = iconForText4,
					szTip = "Suivant",
					dwFlags = 0u
				};
				taskbarList.ThumbBarAddButtons(windowHandle, 3u, new THUMBBUTTON[3] { _thumbPrev, _thumbPlay, _thumbNext });
				_taskbar = taskbarList;
			}
			catch
			{
			}
		}

		private nint GetIconForText(string text)
		{
			try
			{
				using System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(32, 32);
				using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);
				graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
				using (System.Drawing.Font font = new System.Drawing.Font("Segoe MDL2 Assets", 16f))
				{
					using System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
					System.Drawing.SizeF sizeF = graphics.MeasureString(text, font);
					graphics.DrawString(text, font, brush, (32f - sizeF.Width) / 2f, (32f - sizeF.Height) / 2f);
				}
				return bitmap.GetHicon();
			}
			catch
			{
				return IntPtr.Zero;
			}
		}

		[DllImport("combase.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
		private static extern void RoGetActivationFactory([MarshalAs(UnmanagedType.HString)] string activatableClassId, [In] ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);

		private void InitSMTC()
		{
			try
			{
				_smtcPlayer = new MediaPlayer();
				_smtcPlayer.CommandManager.IsEnabled = true;
				_smtcPlayer.CommandManager.PlayReceived += delegate(MediaPlaybackCommandManager s, MediaPlaybackCommandManagerPlayReceivedEventArgs e)
				{
					e.Handled = true;
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						PlayPauseButton_Click(null, null);
					});
				};
				_smtcPlayer.CommandManager.PauseReceived += delegate(MediaPlaybackCommandManager s, MediaPlaybackCommandManagerPauseReceivedEventArgs e)
				{
					e.Handled = true;
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						PlayPauseButton_Click(null, null);
					});
				};
				_smtcPlayer.CommandManager.NextReceived += delegate(MediaPlaybackCommandManager s, MediaPlaybackCommandManagerNextReceivedEventArgs e)
				{
					e.Handled = true;
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						NextButton_Click(null, null);
					});
				};
				_smtcPlayer.CommandManager.PreviousReceived += delegate(MediaPlaybackCommandManager s, MediaPlaybackCommandManagerPreviousReceivedEventArgs e)
				{
					e.Handled = true;
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						PrevButton_Click(null, null);
					});
				};
				_smtc = _smtcPlayer.SystemMediaTransportControls;
				_smtc.IsEnabled = true;
				_smtc.IsPlayEnabled = true;
				_smtc.IsPauseEnabled = true;
				_smtc.IsNextEnabled = true;
				_smtc.IsPreviousEnabled = true;
				InitTaskbarThumbnailButtons();
			}
			catch (Exception ex)
			{
				File.WriteAllText("smtc_error.txt", ex.ToString());
			}
		}

		private void UpdateTaskbarPlayPauseIcon(bool? forcePlaying = null)
		{
			try
			{
				if (_taskbar != null)
				{
					nint windowHandle = WindowNative.GetWindowHandle((object)this);
					if (forcePlaying ?? ((int)App.AudioEngine.State == 1))
					{
						_thumbPlay.hIcon = _hIconPause;
						_thumbPlay.szTip = "Pause";
					}
					else
					{
						_thumbPlay.hIcon = _hIconPlay;
						_thumbPlay.szTip = "Lecture";
					}
					_taskbar.ThumbBarUpdateButtons(windowHandle, 1u, new THUMBBUTTON[1] { _thumbPlay });
				}
			}
			catch (Exception ex)
			{
				File.WriteAllText(Path.Combine(Path.GetTempPath(), "resona_err.log"), ex.ToString());
			}
		}

		public void SetNowPlayingMode(bool isActive, string? trackId)
		{
			bool isNowPlayingModeActive = _isNowPlayingModeActive;
			_isNowPlayingModeActive = isActive;
			if (isActive)
			{
				Track track = _library.FirstOrDefault((Track t) => t.Id == trackId) ?? _queue.FirstOrDefault((Track t) => t.Id == trackId);
				if (track != null)
				{
					UpdateNowPlayingBackground(track.CoverArtPath);
				}
				if (isNowPlayingModeActive)
				{
					UpdatePlayerButtonsColor();
					return;
				}
				((UIElement)NowPlayingBlurredBackground).Visibility = Visibility.Visible;
				((UIElement)NowPlayingDimOverlay).Visibility = Visibility.Visible;
				((UIElement)NowPlayingCenterCoverBorder).IsHitTestVisible = true;
				((UIElement)PlayerGradientFadeLayer).Opacity = 1.0;
				((UIElement)PlayerGradientColorLayer).Opacity = 1.0;
				((UIElement)PlayerGradientOverflow).Opacity = 0.0;
				Storyboard val = new Storyboard();
				DoubleAnimation val2 = new DoubleAnimation
				{
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(400.0)
				};
				Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)NowPlayingBlurredBackground);
				Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
				val.Children.Add((Timeline)(object)val2);
				DoubleAnimation val3 = new DoubleAnimation
				{
					To = 0.6,
					Duration = TimeSpan.FromMilliseconds(400.0)
				};
				Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)NowPlayingDimOverlay);
				Storyboard.SetTargetProperty((Timeline)(object)val3, "Opacity");
				val.Children.Add((Timeline)(object)val3);
				DoubleAnimation val4 = new DoubleAnimation
				{
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(400.0)
				};
				Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)NowPlayingCenterCoverBorder);
				Storyboard.SetTargetProperty((Timeline)(object)val4, "Opacity");
				val.Children.Add((Timeline)(object)val4);
				NowPlayingCenterCoverTransform.X = 0.0;
				DoubleAnimation val5 = new DoubleAnimation
				{
					To = -5.0,
					Duration = TimeSpan.FromMilliseconds(400.0)
				};
				Storyboard.SetTarget((Timeline)(object)val5, (DependencyObject)NowPlayingCenterCoverTransform);
				Storyboard.SetTargetProperty((Timeline)(object)val5, "Y");
				val.Children.Add((Timeline)(object)val5);
				DoubleAnimation val6 = new DoubleAnimation
				{
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(150.0)
				};
				Storyboard.SetTarget((Timeline)(object)val6, (DependencyObject)NowPlayingCoverBorder);
				Storyboard.SetTargetProperty((Timeline)(object)val6, "Opacity");
				val.Children.Add((Timeline)(object)val6);
				DoubleAnimation val7 = new DoubleAnimation
				{
					To = 20.0,
					Duration = TimeSpan.FromMilliseconds(200.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val7, (DependencyObject)NowPlayingCoverTransform);
				Storyboard.SetTargetProperty((Timeline)(object)val7, "Y");
				val.Children.Add((Timeline)(object)val7);
				DoubleAnimation val8 = new DoubleAnimation
				{
					To = -58.0,
					Duration = TimeSpan.FromMilliseconds(150.0),
					BeginTime = TimeSpan.FromMilliseconds(80.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val8, (DependencyObject)NowPlayingTextTransform);
				Storyboard.SetTargetProperty((Timeline)(object)val8, "X");
				val.Children.Add((Timeline)(object)val8);
				UpdateNowPlayingTextMaxWidth();
				val.Begin();
				if (trackId != null && trackId == _nowPlayingId)
				{
					UpdatePlayerButtonsColor();
				}
			}
			else
			{
				Storyboard val9 = new Storyboard();
				DoubleAnimation val10 = new DoubleAnimation
				{
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(300.0)
				};
				Storyboard.SetTarget((Timeline)(object)val10, (DependencyObject)PlayerGradientOverflow);
				Storyboard.SetTargetProperty((Timeline)(object)val10, "Opacity");
				val9.Children.Add((Timeline)(object)val10);
				DoubleAnimation val11 = new DoubleAnimation
				{
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(300.0)
				};
				Storyboard.SetTarget((Timeline)(object)val11, (DependencyObject)NowPlayingBlurredBackground);
				Storyboard.SetTargetProperty((Timeline)(object)val11, "Opacity");
				val9.Children.Add((Timeline)(object)val11);
				DoubleAnimation val12 = new DoubleAnimation
				{
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(300.0)
				};
				Storyboard.SetTarget((Timeline)(object)val12, (DependencyObject)NowPlayingDimOverlay);
				Storyboard.SetTargetProperty((Timeline)(object)val12, "Opacity");
				val9.Children.Add((Timeline)(object)val12);
				DoubleAnimation val13 = new DoubleAnimation
				{
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(150.0)
				};
				Storyboard.SetTarget((Timeline)(object)val13, (DependencyObject)NowPlayingCenterCoverBorder);
				Storyboard.SetTargetProperty((Timeline)(object)val13, "Opacity");
				val9.Children.Add((Timeline)(object)val13);
				DoubleAnimation val14 = new DoubleAnimation
				{
					To = -5.0,
					Duration = TimeSpan.FromMilliseconds(150.0)
				};
				Storyboard.SetTarget((Timeline)(object)val14, (DependencyObject)NowPlayingCenterCoverTransform);
				Storyboard.SetTargetProperty((Timeline)(object)val14, "Y");
				val9.Children.Add((Timeline)(object)val14);
				((UIElement)NowPlayingCenterCoverBorder).IsHitTestVisible = false;
				DoubleAnimation val15 = new DoubleAnimation
				{
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(150.0),
					BeginTime = TimeSpan.FromMilliseconds(100.0)
				};
				Storyboard.SetTarget((Timeline)(object)val15, (DependencyObject)NowPlayingCoverBorder);
				Storyboard.SetTargetProperty((Timeline)(object)val15, "Opacity");
				val9.Children.Add((Timeline)(object)val15);
				DoubleAnimation val16 = new DoubleAnimation
				{
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(200.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val16, (DependencyObject)NowPlayingCoverTransform);
				Storyboard.SetTargetProperty((Timeline)(object)val16, "Y");
				val9.Children.Add((Timeline)(object)val16);
				DoubleAnimation val17 = new DoubleAnimation
				{
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(200.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val17, (DependencyObject)NowPlayingTextTransform);
				Storyboard.SetTargetProperty((Timeline)(object)val17, "X");
				val9.Children.Add((Timeline)(object)val17);
				UpdateNowPlayingTextMaxWidth();
				((Timeline)val9).Completed += delegate
				{
					if (!(((ContentControl)ContentFrame).Content is NowPlayingPage))
					{
						((UIElement)NowPlayingBlurredBackground).Visibility = Visibility.Collapsed;
						((UIElement)NowPlayingDimOverlay).Visibility = Visibility.Collapsed;
						NowPlayingBlurredBackground.Source = null;
					}
				};
				val9.Begin();
			}
			UpdatePlayerButtonsColor();
		}

		public async void UpdateNowPlayingBackground(string? coverPath)
		{
			if (string.IsNullOrEmpty(coverPath))
			{
				NowPlayingBlurredBackground.Source = null;
				NowPlayingCenterCoverImage.Source = null;
				((UIElement)NowPlayingCenterPlaceholder).Visibility = Visibility.Visible;
				return;
			}
			try
			{
				NowPlayingCenterCoverImage.Source = (ImageSource)new BitmapImage(new Uri(coverPath));
				((UIElement)NowPlayingCenterPlaceholder).Visibility = Visibility.Collapsed;
				byte[] bytes = await Task.Run(delegate
				{
					byte[] array = File.ReadAllBytes(coverPath);
					using MemoryStream stream = new MemoryStream(array);
					using System.Drawing.Image original = System.Drawing.Image.FromStream(stream);
					using System.Drawing.Bitmap image = new System.Drawing.Bitmap(original, new System.Drawing.Size(32, 32));
					System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(512, 512);
					using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
					{
						graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
						graphics.DrawImage(image, 0, 0, 512, 512);
					}
					using MemoryStream memoryStream = new MemoryStream();
					try
					{
						bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
					}
					catch
					{
						return array;
					}
					return memoryStream.ToArray();
				});
				InMemoryRandomAccessStream ras = new InMemoryRandomAccessStream();
				try
				{
					DataWriter dw = new DataWriter(ras.GetOutputStreamAt(0uL));
					try
					{
						dw.WriteBytes(bytes);
						await dw.StoreAsync();
						ras.Seek(0uL);
						BitmapImage bmp = new BitmapImage();
						await bmp.SetSourceAsync(ras);
						NowPlayingBlurredBackground.Source = (ImageSource)bmp;
					}
					finally
					{
						((IDisposable)dw)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)ras)?.Dispose();
				}
			}
			catch
			{
			}
		}

		private void AnimateTrackChange(bool isGoingBack)
		{
			Storyboard val = new Storyboard();
			double x = (isGoingBack ? (-100) : 100);
			NowPlayingCenterCoverTransform.X = x;
			((UIElement)NowPlayingCenterCoverBorder).Opacity = 0.0;
			((UIElement)NowPlayingBlurredBackground).Opacity = 0.0;
			DoubleAnimation val2 = new DoubleAnimation
			{
				To = 0.0,
				Duration = TimeSpan.FromMilliseconds(400.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)NowPlayingCenterCoverTransform);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "X");
			DoubleAnimation val3 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(400.0)
			};
			Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)NowPlayingCenterCoverBorder);
			Storyboard.SetTargetProperty((Timeline)(object)val3, "Opacity");
			DoubleAnimation val4 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(600.0)
			};
			Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)NowPlayingBlurredBackground);
			Storyboard.SetTargetProperty((Timeline)(object)val4, "Opacity");
			val.Children.Add((Timeline)(object)val2);
			val.Children.Add((Timeline)(object)val3);
			val.Children.Add((Timeline)(object)val4);
			val.Begin();
		}

		public void NavigateToNowPlaying(Track track, bool isGoingBack = false)
		{
			Transform renderTransform = ((UIElement)NowPlayingCenterCoverBorder).RenderTransform;
			CompositeTransform val = renderTransform as CompositeTransform;
			if (val != null)
			{
				val.ScaleX = 1.0;
				val.ScaleY = 1.0;
			}
			((UIElement)SettingsContainer).Visibility = Visibility.Collapsed;
			((UIElement)ContentFrame).Visibility = Visibility.Visible;
			if (((ContentControl)ContentFrame).Content is NowPlayingPage nowPlayingPage)
			{
				nowPlayingPage.UpdateTrackInfo(track);
				SetNowPlayingMode(isActive: true, track.Id);
				AnimateTrackChange(isGoingBack);
				return;
			}
			NowPlayingPage nowPlayingPage2 = new NowPlayingPage();
			((ContentControl)ContentFrame).Content = nowPlayingPage2;
			nowPlayingPage2.UpdateTrackInfo(track);
			if (RootNav.SelectedItem != null)
			{
				_lastNavSelectedItem = RootNav.SelectedItem;
			}
			RootNav.SelectedItem = null;
		}

		private void NowPlayingCover_Tapped(object sender, TappedRoutedEventArgs e)
		{
			AnimationHelper.ApplyBouncyScale((UIElement)NowPlayingCoverBorder, 1f);
			if (_currentIndex >= 0 && _currentIndex < _library.Count)
			{
				NavigateToNowPlaying(_library[_currentIndex]);
			}
		}

		private void NowPlayingCenterCover_Tapped(object sender, TappedRoutedEventArgs e)
		{
			if (_lastNavSelectedItem != null)
			{
				RootNav.SelectedItem = _lastNavSelectedItem;
			}
			else
			{
				NavigateToSidebarItem("library", isSettings: false);
			}
		}

		private void NowPlayingCenterCover_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			Storyboard val = new Storyboard();
			DoubleAnimation val2 = new DoubleAnimation
			{
				To = 1.03,
				Duration = TimeSpan.FromMilliseconds(250.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			DoubleAnimation val3 = new DoubleAnimation
			{
				To = 1.03,
				Duration = TimeSpan.FromMilliseconds(250.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			if (!(((UIElement)NowPlayingCenterCoverBorder).RenderTransform is CompositeTransform))
			{
				CompositeTransform val4 = new CompositeTransform();
				val4.TranslateY = -5.0;
				((UIElement)NowPlayingCenterCoverBorder).RenderTransform = (Transform)val4;
			}
			((UIElement)NowPlayingCenterCoverBorder).RenderTransformOrigin = new Point(0.5, 0.5);
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)NowPlayingCenterCoverBorder);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
			Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)NowPlayingCenterCoverBorder);
			Storyboard.SetTargetProperty((Timeline)(object)val3, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
			val.Children.Add((Timeline)(object)val2);
			val.Children.Add((Timeline)(object)val3);
			val.Begin();
		}

		private void NowPlayingCenterCover_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			Storyboard val = new Storyboard();
			DoubleAnimation val2 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(250.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			DoubleAnimation val3 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(250.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			if (!(((UIElement)NowPlayingCenterCoverBorder).RenderTransform is CompositeTransform))
			{
				CompositeTransform val4 = new CompositeTransform();
				val4.TranslateY = -5.0;
				((UIElement)NowPlayingCenterCoverBorder).RenderTransform = (Transform)val4;
			}
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)NowPlayingCenterCoverBorder);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
			Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)NowPlayingCenterCoverBorder);
			Storyboard.SetTargetProperty((Timeline)(object)val3, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
			val.Children.Add((Timeline)(object)val2);
			val.Children.Add((Timeline)(object)val3);
			val.Begin();
		}

		private async void UpdateSMTCInfo(Track track, bool isPlaying)
		{
			try
			{
				if (_smtc == (SystemMediaTransportControls)null)
				{
					return;
				}
				SystemMediaTransportControls smtc = _smtc;
				smtc.PlaybackStatus = (MediaPlaybackStatus)(isPlaying ? 3 : 4);
				SystemMediaTransportControlsDisplayUpdater updater = smtc.DisplayUpdater;
				updater.Type = MediaPlaybackType.Music;
				updater.MusicProperties.Title = (string.IsNullOrEmpty(track.Title) ? "Unknown" : track.Title);
				updater.MusicProperties.Artist = (string.IsNullOrEmpty(track.Artist) ? "Unknown" : track.Artist);
				if (!string.IsNullOrEmpty(track.CoverArtPath) && File.Exists(track.CoverArtPath))
				{
					try
					{
						StorageFile sf = await StorageFile.GetFileFromPathAsync(track.CoverArtPath);
						updater.Thumbnail = RandomAccessStreamReference.CreateFromFile((IStorageFile)sf);
					}
					catch
					{
						updater.Thumbnail = null;
					}
				}
				else
				{
					updater.Thumbnail = null;
				}
				updater.Update();
			}
			catch
			{
			}
		}

		private void RegenerateShuffleUpcoming(bool includeCurrent = false)
		{
			_shuffleUpcoming.Clear();
			if (_queue == null || _queue.Count == 0)
			{
				return;
			}
			List<Track> list = _queue.ToList();
			Random random = new Random();
			for (int num = list.Count - 1; num > 0; num--)
			{
				int num2 = random.Next(num + 1);
				List<Track> list2 = list;
				int index = num;
				int index2 = num2;
				Track value = list[num2];
				Track value2 = list[num];
				list2[index] = value;
				list[index2] = value2;
			}
			if (list.Count > 1 && list[0].Id == _nowPlayingId)
			{
				Track value3 = list[0];
				list[0] = list[list.Count - 1];
				list[list.Count - 1] = value3;
			}
			_shuffleUpcoming.AddRange(list);
			if (!includeCurrent)
			{
				_shuffleUpcoming.RemoveAll((Track t) => t.Id == _nowPlayingId);
			}
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

		public MainWindow()
		{
			_library = new List<Track>();
			_currentIndex = -1;
			_queue = new List<Track>();
			_queueIndex = -1;
			_playbackHistory = new Stack<Track>();
			_playbackFuture = new Stack<Track>();
			_manualQueue = new List<Track>();
			_queueSourceName = null;
			_isCurrentlyPlayingManualQueue = false;
			_isUpNextPanelOpen = false;
			_playbackMode = PlaybackMode.Off;
			_random = new Random();
			_shuffleUpcoming = new List<Track>();
			_lrcLines = new List<LrcLine>();
			_lrcCurrentIndex = -1;
			_lyricsOverlayOpen = false;
			_isNowPlayingModeActive = false;
			_lastComputedAvgColor = Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			_isManuallyClosingPane = false;
			_isFetchingCovers = false;
			_failedCoverSearches = new HashSet<string>();
			_scanInProgress = false;
			_rescanPending = false;
			_dirtyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			_dirtyLock = new object();
			_infoOverlayOpen = false;
			_previousVolume = 50.0;
			_upNextDisplayList = new List<Track>();
			_upNextObsList = new ObservableCollection<Track>();
			_isMiniPlayerMode = false;
			try
			{
				SetCurrentProcessExplicitAppUserModelID("Resona");
				CreateStartMenuShortcut();
			}
			catch (Exception ex)
			{
				File.WriteAllText(Path.Combine(Path.GetTempPath(), "resona_err.log"), ex.ToString());
			}
			InitSMTC();
			InitializeComponent();
			((Window)this).AppWindow.Closing += AppWindow_Closing;
			int windowWidth = App.Settings.Current.WindowWidth;
			int windowHeight = App.Settings.Current.WindowHeight;
			int windowX = App.Settings.Current.WindowX;
			int windowY = App.Settings.Current.WindowY;
			bool flag = App.Settings.Current.SaveWindowSize && windowWidth > 0;
			bool flag2 = App.Settings.Current.SaveWindowPosition && windowX != -1;
			if (flag & flag2)
			{
				((Window)this).AppWindow.MoveAndResize(new RectInt32(windowX, windowY, windowWidth, windowHeight));
			}
			else if (flag)
			{
				((Window)this).AppWindow.Resize(new SizeInt32(windowWidth, windowHeight));
			}
			else if (flag2)
			{
				((Window)this).AppWindow.Move(new PointInt32(windowX, windowY));
			}
			((FrameworkElement)RootGrid).Loaded += (RoutedEventHandler)delegate
			{
				if (App.Settings.Current.AutoUpdateEnabled)
				{
					UpdateManager.CheckForUpdatesAsync(((Window)this).Content.XamlRoot, manualCheck: false);
				}
			};
			((UIElement)RootGrid).AddHandler(UIElement.PointerReleasedEvent, (object)new PointerEventHandler(RootGrid_PointerReleased), true);
			((UIElement)RootGrid).AddHandler(UIElement.PointerPressedEvent, (object)new PointerEventHandler(RootGrid_PointerPressed), true);
			((UIElement)RootGrid).AddHandler(UIElement.PointerPressedEvent, (object)(PointerEventHandler)delegate(object s, PointerRoutedEventArgs e)
			{
				if (_isUpNextPanelOpen)
				{
					object originalSource = ((RoutedEventArgs)e).OriginalSource;
					DependencyObject val3 = originalSource as DependencyObject;
					bool flag3 = false;
					while (val3 != (DependencyObject)null)
					{
						if (val3 == (DependencyObject)UpNextPanel || val3 == (DependencyObject)UpNextToggleBtn)
						{
							flag3 = true;
							break;
						}
						val3 = VisualTreeHelper.GetParent(val3);
					}
					if (!flag3)
					{
						_isUpNextPanelOpen = false;
						UpNextTransform.Y = 520.0;
						((UIElement)UpNextPanel).Visibility = Visibility.Collapsed;
					}
				}
			}, true);
			((UIElement)RootGrid).PreviewKeyDown += (KeyEventHandler)delegate(object s, KeyRoutedEventArgs e)
			{
				if ((int)e.Key == 32)
				{
					object focusedElement = FocusManager.GetFocusedElement(((Window)this).Content.XamlRoot);
					if (!(focusedElement is TextBox) && !(focusedElement is PasswordBox) && !(focusedElement is AutoSuggestBox) && !(focusedElement is RichEditBox))
					{
						e.Handled = true;
						PlayPauseButton_Click(this, new RoutedEventArgs());
					}
				}
			};
			((FrameworkElement)RootNav).Loaded += (RoutedEventHandler)delegate
			{
				InitTaskbarThumbnailButtons();
			};
			Strings.Current.PropertyChanged += delegate
			{
				object settingsItem = RootNav.SettingsItem;
				NavigationViewItem val3 = settingsItem as NavigationViewItem;
				if (val3 != null)
				{
					((ContentControl)val3).Content = Strings.Current.CS_Settings;
				}
			};
			((Window)this).Title = "Resona";
			try
			{
				string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icon.ico");
				if (File.Exists(iconPath))
				{
					((Window)this).AppWindow.SetIcon(iconPath);
				}
			}
			catch (Exception ex)
			{
				File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "resona_err.log"), ex.ToString());
			}
			PointerEventHandler val2 = delegate(object s, PointerRoutedEventArgs e)
			{
				object originalSource = ((RoutedEventArgs)e).OriginalSource;
				FrameworkElement val3 = originalSource as FrameworkElement;
				if (val3 != (FrameworkElement)null && !(val3.DataContext is Track) && !(val3.DataContext is Playlist))
				{
					GlobalClickOutside?.Invoke();
				}
			};
			((Window)this).Content.AddHandler(UIElement.PointerPressedEvent, (object)val2, true);
			((UIElement)RootNav).AddHandler(UIElement.PointerPressedEvent, (object)val2, true);
			RootNav.ItemInvoked += delegate
			{
				GlobalClickOutside?.Invoke();
			};
			SetupMinSizeViaWin32();
			ApplyBackdrop();
			ApplyTitleBarTheme();
			RefreshNavCategories();
			ApplyLyricsButtonVisibility();
			UpdatePlaybackControlsCentering();
			((RangeBase)VolumeSlider).Value = App.Settings.Current.Volume;
			App.AudioEngine.SetUserVolume((float)(((RangeBase)VolumeSlider).Value / 100.0));
			_playbackMode = (PlaybackMode)Math.Clamp(App.Settings.Current.SavedPlaybackMode, 0, 3);
			AlbumsPage.ResetSessionCaches();
			ArtistsPage.ResetSessionCaches();
			GenresPage.ResetSessionCaches();
			FoldersPage.ResetSessionCaches();
			UpdateRepeatButtonVisual();
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				ApplyLyricsButtonVisibility();
				UpdateUpNextPanelVisibility();
				UpdateMiniPlayerButtonVisibility();
				UpdatePlayerBarButtonsVisibility();
			});
			((FrameworkElement)RootNav).SizeChanged += (SizeChangedEventHandler)delegate
			{
				UpdateGradientOverflowLayout();
			};
			RootNav.DisplayModeChanged += delegate
			{
				UpdateGradientOverflowLayout();
			};
			((FrameworkElement)RootNav).Loaded += (RoutedEventHandler)delegate
			{
				ResolveNavigationViewChromeElements();
				object settingsItem = RootNav.SettingsItem;
				NavigationViewItem val3 = settingsItem as NavigationViewItem;
				if (val3 != null)
				{
					((ContentControl)val3).Content = Strings.Current.CS_Settings;
				}
				UpdateGradientOverflowLayout();
				AnimateNavItemsText(RootNav.IsPaneOpen);
			};
			Strings.Current.PropertyChanged += delegate(object? _, PropertyChangedEventArgs e)
			{
				if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "IsFr")
				{
					RootNav.OpenPaneLength = (Strings.Current.IsFr ? 210 : 160);
				}
			};
			((FrameworkElement)PlayerBar).SizeChanged += (SizeChangedEventHandler)delegate
			{
				UpdateGradientOverflowLayout();
				UpdateNowPlayingTextMaxWidth();
			};
			((Window)this).SizeChanged += delegate
			{
				UpdateGradientOverflowLayout();
				UpdateNowPlayingTextMaxWidth();
				UpdateVolumeSliderWidth();
				UpdatePlaybackControlsCentering();
			};
			SetupPositionTimer();
			ProgressSlider.ThumbToolTipValueConverter = (IValueConverter)new SecondsToTimeStringConverter();
			if (MiniProgressSlider != (Slider)null)
			{
				MiniProgressSlider.ThumbToolTipValueConverter = (IValueConverter)new SecondsToTimeStringConverter();
			}
			((UIElement)ProgressSlider).PointerEntered += (PointerEventHandler)delegate
			{
				((UIElement)CustomProgressThumb).RenderTransformOrigin = new Point(0.5, 0.5);
				AnimationHelper.ApplyBouncyScale((UIElement)CustomProgressThumb, 1.25f);
				Brush background = ((Panel)CustomProgressFill).Background;
				SolidColorBrush val3 = background as SolidColorBrush;
				if (val3 != null)
				{
					Color color = val3.Color;
					((Panel)CustomProgressFill).Background = (Brush)new SolidColorBrush(Color.FromArgb(color.A, (byte)Math.Min(255, color.R + 40), (byte)Math.Min(255, color.G + 40), (byte)Math.Min(255, color.B + 40)));
				}
			};
			((UIElement)ProgressSlider).PointerExited += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)CustomProgressThumb, 1f);
				UpdatePlayerButtonsColor();
			};
			((UIElement)PlayPauseButton).PointerEntered += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PlayPauseButton, 1.05f);
			};
			((UIElement)PlayPauseButton).PointerExited += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PlayPauseButton, 1f);
			};
			((UIElement)PlayPauseButton).PointerPressed += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PlayPauseButton, 0.95f);
			};
			((UIElement)PlayPauseButton).PointerReleased += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PlayPauseButton, 1.05f);
			};
			((UIElement)NowPlayingCoverBorder).PointerEntered += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NowPlayingCoverBorder, 1.05f);
			};
			((UIElement)NowPlayingCoverBorder).PointerExited += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NowPlayingCoverBorder, 1f);
			};
			((UIElement)NowPlayingCoverBorder).PointerPressed += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NowPlayingCoverBorder, 0.95f);
			};
			((UIElement)NowPlayingCoverBorder).PointerReleased += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NowPlayingCoverBorder, 1.05f);
			};
			((UIElement)PrevButton).PointerEntered += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PrevButton, 1.05f);
			};
			((UIElement)PrevButton).PointerExited += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PrevButton, 1f);
			};
			((UIElement)PrevButton).PointerPressed += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PrevButton, 0.95f);
			};
			((UIElement)PrevButton).PointerReleased += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)PrevButton, 1.05f);
			};
			((UIElement)NextButton).PointerEntered += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NextButton, 1.05f);
			};
			((UIElement)NextButton).PointerExited += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NextButton, 1f);
			};
			((UIElement)NextButton).PointerPressed += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NextButton, 0.95f);
			};
			((UIElement)NextButton).PointerReleased += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)NextButton, 1.05f);
			};
			((UIElement)RepeatButton).PointerEntered += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)RepeatButton, 1.05f);
			};
			((UIElement)RepeatButton).PointerExited += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)RepeatButton, 1f);
			};
			((UIElement)RepeatButton).PointerPressed += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)RepeatButton, 0.95f);
			};
			((UIElement)RepeatButton).PointerReleased += (PointerEventHandler)delegate
			{
				AnimationHelper.ApplyBouncyScale((UIElement)RepeatButton, 1.05f);
			};
			((UIElement)PlayPauseButton).PointerEntered += (PointerEventHandler)delegate
			{
				((UIElement)PlayPauseButton).Opacity = 0.85;
			};
			((UIElement)PlayPauseButton).PointerExited += (PointerEventHandler)delegate
			{
				((UIElement)PlayPauseButton).Opacity = 1.0;
			};
			((UIElement)PlayPauseButton).PointerPressed += (PointerEventHandler)delegate
			{
				((UIElement)PlayPauseButton).Opacity = 0.7;
			};
			((UIElement)PlayPauseButton).PointerReleased += (PointerEventHandler)delegate
			{
				((UIElement)PlayPauseButton).Opacity = 0.85;
			};
			((UIElement)ProgressSlider).AddHandler(UIElement.PointerPressedEvent, (object)new PointerEventHandler(ProgressSlider_PointerPressed), true);
			((UIElement)ProgressSlider).AddHandler(UIElement.PointerReleasedEvent, (object)new PointerEventHandler(ProgressSlider_PointerReleased), true);
			if (MiniProgressSlider != (Slider)null)
			{
				((UIElement)MiniProgressSlider).AddHandler(UIElement.PointerPressedEvent, (object)new PointerEventHandler(ProgressSlider_PointerPressed), true);
				((UIElement)MiniProgressSlider).AddHandler(UIElement.PointerReleasedEvent, (object)new PointerEventHandler(ProgressSlider_PointerReleased), true);
			}
			App.AudioEngine.PlaybackStopped += AudioEngine_PlaybackStopped;
			if (!App.Settings.Current.HasCompletedOnboarding)
			{
				ShowOnboarding();
				return;
			}
			((FrameworkElement)RootNav).Loaded += new RoutedEventHandler(OnRootNavFirstLoaded);
			RootNav.SelectedItem = NavLibrary;
			_libraryPageInstance = new LibraryPage();
			((ContentControl)ContentFrame).Content = _libraryPageInstance;
			LoadLibraryThenPreloadSettings();
		}

		private void OnRootNavFirstLoaded(object sender, RoutedEventArgs e)
		{
			((FrameworkElement)RootNav).Loaded -= new RoutedEventHandler(OnRootNavFirstLoaded);
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueuePriority)(-10), (DispatcherQueueHandler)delegate
			{
				((UIElement)RootNav).Opacity = 1.0;
				Storyboard val = new Storyboard();
				DoubleAnimation val2 = new DoubleAnimation
				{
					From = 0.0,
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(200.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)RootGrid);
				Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
				val.Children.Add((Timeline)(object)val2);
				val.Begin();
			});
		}

		private async Task LoadLibraryThenPreloadSettings()
		{
			await LoadLibraryFromCacheAsync();
			PreloadSettingsPageInBackground();
		}

		private void PreloadSettingsPageInBackground()
		{
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueuePriority)(-10), (DispatcherQueueHandler)delegate
			{
				((UIElement)SettingsContainer).Opacity = 0.0;
				((UIElement)SettingsContainer).Visibility = Visibility.Visible;
				((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueuePriority)(-10), (DispatcherQueueHandler)delegate
				{
					((UIElement)SettingsContainer).Visibility = Visibility.Collapsed;
					((UIElement)SettingsContainer).Opacity = 1.0;
				});
			});
		}

		private void ShowOnboarding()
		{
			((UIElement)ContentFrame).Visibility = Visibility.Collapsed;
			((UIElement)OnboardingFrame).Visibility = Visibility.Visible;
			((UIElement)OnboardingFrame).Opacity = 0.0;
			OnboardingFrame.Navigate(typeof(OnboardingPage));
			((UIElement)RootGrid).Opacity = 1.0;
			Storyboard val = new Storyboard();
			DoubleAnimation val2 = new DoubleAnimation
			{
				From = 0.0,
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(400.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)OnboardingFrame);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
			val.Children.Add((Timeline)(object)val2);
			val.Begin();
			if (!(((ContentControl)OnboardingFrame).Content is OnboardingPage onboardingPage))
			{
				return;
			}
			onboardingPage.OnboardingCompleted += async delegate
			{
				Storyboard sbOut = new Storyboard();
				DoubleAnimation fadeOut = new DoubleAnimation
				{
					From = 1.0,
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(300.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseIn
					}
				};
				Storyboard.SetTarget((Timeline)(object)fadeOut, (DependencyObject)OnboardingFrame);
				Storyboard.SetTargetProperty((Timeline)(object)fadeOut, "Opacity");
				sbOut.Children.Add((Timeline)(object)fadeOut);
				TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
				((Timeline)sbOut).Completed += delegate
				{
					tcs.SetResult(result: true);
				};
				sbOut.Begin();
				await tcs.Task;
				((UIElement)OnboardingFrame).Visibility = Visibility.Collapsed;
				((UIElement)RootNav).Opacity = 1.0;
				((UIElement)ContentFrame).Opacity = 0.0;
				((UIElement)ContentFrame).Visibility = Visibility.Visible;
				RootNav.SelectedItem = NavLibrary;
				_libraryPageInstance = new LibraryPage();
				((ContentControl)ContentFrame).Content = _libraryPageInstance;
				Storyboard sbIn = new Storyboard();
				DoubleAnimation fadeInMain = new DoubleAnimation
				{
					From = 0.0,
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(400.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)fadeInMain, (DependencyObject)ContentFrame);
				Storyboard.SetTargetProperty((Timeline)(object)fadeInMain, "Opacity");
				sbIn.Children.Add((Timeline)(object)fadeInMain);
				sbIn.Begin();
				await LoadLibraryFromCacheAsync();
				PreloadSettingsPageInBackground();
			};
		}

		private void ApplyTitleBarTheme()
		{
			try
			{
				((Window)this).ExtendsContentIntoTitleBar = true;
				((Window)this).SetTitleBar((UIElement)AppTitleBar);
				ApplyNativeWindowBackgroundFix();
			}
			catch
			{
			}
		}

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

		[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
		private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

		[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
		private static extern nint SetWindowLong32(nint hWnd, int nIndex, nint dwNewLong);

		private static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
		{
			if (IntPtr.Size == 8)
			{
				return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
			}
			return SetWindowLong32(hWnd, nIndex, dwNewLong);
		}

		[DllImport("user32.dll")]
		private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hwnd, uint msg, nint wParam, nint lParam);

		private void SetupMinSizeViaWin32()
		{
			try
			{
				nint windowHandle = WindowNative.GetWindowHandle((object)this);
				_wndProcDelegate = CustomWndProc;
				_originalWndProc = SetWindowLongPtr(windowHandle, -4, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
			}
			catch
			{
			}
		}

		private nint CustomWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
		{
			switch (msg)
			{
			case 36u:
			{
				MINMAXINFO structure = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
				if (_isMiniPlayerMode)
				{
					structure.ptMinTrackSize.X = 340;
					structure.ptMinTrackSize.Y = 600;
					structure.ptMaxTrackSize.X = 340;
					structure.ptMaxTrackSize.Y = 600;
				}
				else
				{
					structure.ptMinTrackSize.X = 1200;
					structure.ptMinTrackSize.Y = 600;
				}
				Marshal.StructureToPtr(structure, lParam, fDeleteOld: true);
				break;
			}
			case 273u:
			{
				long num = ((IntPtr)wParam).ToInt64();
				uint num2 = (uint)((num >> 16) & 0xFFFF);
				uint num3 = (uint)(num & 0xFFFF);
				if (num2 != 6144)
				{
					break;
				}
				switch (num3)
				{
				case 100u:
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						PlayPauseButton_Click(null, null);
					});
					return IntPtr.Zero;
				case 101u:
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						PrevButton_Click(null, null);
					});
					return IntPtr.Zero;
				case 102u:
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						NextButton_Click(null, null);
					});
					return IntPtr.Zero;
				}
				break;
			}
			}
			return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
		}

		private void ApplyNativeWindowBackgroundFix()
		{
			try
			{
				nint windowHandle = WindowNative.GetWindowHandle((object)this);
				ThemePreset themePreset = ThemePresets.All[Math.Clamp(App.Settings.Current.ThemePresetIndex, 0, ThemePresets.All.Length - 1)];
				string text = themePreset.BackgroundHex.TrimStart('#');
				byte b = Convert.ToByte(text.Substring(0, 2), 16);
				byte b2 = Convert.ToByte(text.Substring(2, 2), 16);
				byte b3 = Convert.ToByte(text.Substring(4, 2), 16);
				int value = (b3 << 16) | (b2 << 8) | b;
				DwmSetWindowAttribute(windowHandle, 34, ref value, 4);
				DwmSetWindowAttribute(windowHandle, 35, ref value, 4);
			}
			catch
			{
			}
		}

		public void ApplyBackdrop()
		{
			AppBackdropStyle appBackdropStyle = App.Settings.Current.Backdrop;
			try
			{
				((Window)this).SystemBackdrop = (SystemBackdrop)(appBackdropStyle switch
				{
					AppBackdropStyle.Mica => (object)new MicaBackdrop
					{
						Kind = MicaKind.BaseAlt
					}, 
					AppBackdropStyle.MicaAlt => (object)new MicaBackdrop
					{
						Kind = MicaKind.Base
					}, 
					AppBackdropStyle.Acrylic => (object)new DesktopAcrylicBackdrop(), 
					_ => null, 
				});
			}
			catch
			{
				((Window)this).SystemBackdrop = null;
				appBackdropStyle = AppBackdropStyle.Solid;
			}
			bool flag = appBackdropStyle == AppBackdropStyle.Solid;
			SolidColorBrush val = new SolidColorBrush(Colors.Transparent);
			((Panel)RootGrid).Background = (Brush)((!flag) ? ((object)val) : ((Brush)Application.Current.Resources["AppDeepBackgroundBrush"]));
			if (flag)
			{
				if (_currentIndex >= 0)
				{
					UpdatePlayerBarColorAsync(_library[_currentIndex]);
				}
				else
				{
					((Panel)PlayerBar).Background = (Brush)Application.Current.Resources["AppSurfaceBrush"];
				}
			}
			else
			{
				((Panel)PlayerBar).Background = (Brush)val;
				((UIElement)PlayerGradientOverflow).Visibility = Visibility.Collapsed;
				((UIElement)PlayerGradientFadeLayer).Visibility = Visibility.Collapsed;
				ApplyGradientOverflowChrome(enabled: false);
			}
			ApplyTitleBarButtonColors(flag);
		}

		private void ApplyTitleBarButtonColors(bool isSolid)
		{
			try
			{
				AppWindow appWindow = ((Window)this).AppWindow;
				AppWindowTitleBar val = ((appWindow != null) ? appWindow.TitleBar : null);
				if (val == (AppWindowTitleBar)null)
				{
					return;
				}
				Color value = Color.FromArgb((byte)0, (byte)0, (byte)0, (byte)0);
				ThemePreset themePreset = ThemePresets.All[Math.Clamp(App.Settings.Current.ThemePresetIndex, 0, ThemePresets.All.Length - 1)];
				Color val2 = ParseHexColor(themePreset.BackgroundHex);
				bool flag = isSolid && val2.R >= 220 && val2.G >= 220 && val2.B >= 220;
				Color value2 = (flag ? Color.FromArgb(byte.MaxValue, (byte)26, (byte)26, (byte)26) : Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				Color value3 = (flag ? Color.FromArgb((byte)150, (byte)26, (byte)26, (byte)26) : Color.FromArgb((byte)150, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				val.BackgroundColor = value;
				val.InactiveBackgroundColor = value;
				val.ForegroundColor = value2;
				val.InactiveForegroundColor = value3;
				val.ButtonBackgroundColor = value;
				val.ButtonInactiveBackgroundColor = value;
				val.ButtonForegroundColor = value2;
				val.ButtonInactiveForegroundColor = value3;
				if (isSolid)
				{
					if (flag)
					{
						val.ButtonHoverBackgroundColor = Color.FromArgb(byte.MaxValue, (byte)214, (byte)214, (byte)214);
						val.ButtonPressedBackgroundColor = Color.FromArgb(byte.MaxValue, (byte)196, (byte)196, (byte)196);
						val.ButtonHoverForegroundColor = value2;
						val.ButtonPressedForegroundColor = value2;
					}
					else
					{
						Color value4 = Color.FromArgb(byte.MaxValue, (byte)Math.Min(255, val2.R + 40), (byte)Math.Min(255, val2.G + 40), (byte)Math.Min(255, val2.B + 40));
						Color value5 = Color.FromArgb(byte.MaxValue, (byte)Math.Max(0, val2.R - 30), (byte)Math.Max(0, val2.G - 30), (byte)Math.Max(0, val2.B - 30));
						val.ButtonHoverBackgroundColor = value4;
						val.ButtonPressedBackgroundColor = value5;
						val.ButtonHoverForegroundColor = Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
						val.ButtonPressedForegroundColor = Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
					}
				}
				else
				{
					val.ButtonHoverBackgroundColor = Color.FromArgb((byte)40, byte.MaxValue, byte.MaxValue, byte.MaxValue);
					val.ButtonPressedBackgroundColor = Color.FromArgb((byte)70, byte.MaxValue, byte.MaxValue, byte.MaxValue);
					val.ButtonHoverForegroundColor = Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
					val.ButtonPressedForegroundColor = Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
				}
			}
			catch
			{
			}
		}

		private static Color? GetAverageColorCpu(string imagePath)
		{
			try
			{
				byte[] buffer = File.ReadAllBytes(imagePath);
				using MemoryStream stream = new MemoryStream(buffer);
				using System.Drawing.Image image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
				using System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(8, 8);
				using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);
				graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
				graphics.DrawImage(image, 0, 0, 8, 8);
				long num = 0L;
				long num2 = 0L;
				long num3 = 0L;
				for (int i = 0; i < 8; i++)
				{
					for (int j = 0; j < 8; j++)
					{
						System.Drawing.Color pixel = bitmap.GetPixel(j, i);
						num += pixel.R;
						num2 += pixel.G;
						num3 += pixel.B;
					}
				}
				return Color.FromArgb(byte.MaxValue, (byte)(num / 64), (byte)(num2 / 64), (byte)(num3 / 64));
			}
			catch
			{
				return null;
			}
		}

		private static Color Darken(Color c, double factor)
		{
			return Color.FromArgb(byte.MaxValue, (byte)((double)(int)c.R * factor), (byte)((double)(int)c.G * factor), (byte)((double)(int)c.B * factor));
		}

		private Color BrightenIfNeeded(Color c)
		{
			double num = (0.299 * (double)(int)c.R + 0.587 * (double)(int)c.G + 0.114 * (double)(int)c.B) / 255.0;
			if (num < 0.45)
			{
				return Color.FromArgb(byte.MaxValue, (byte)Math.Min(255, c.R + 80), (byte)Math.Min(255, c.G + 80), (byte)Math.Min(255, c.B + 80));
			}
			return c;
		}

		private void UpdatePlayerButtonsColor()
		{
			if (_isNowPlayingModeActive)
			{
				Color val = BrightenIfNeeded(_lastComputedAvgColor);
				SolidColorBrush val2 = new SolidColorBrush(val);
				Color val3 = Color.FromArgb(byte.MaxValue, (byte)Math.Min(255, val.R + 30), (byte)Math.Min(255, val.G + 30), (byte)Math.Min(255, val.B + 30));
				SolidColorBrush val4 = new SolidColorBrush(val3);
				double num = (0.299 * (double)(int)val.R + 0.587 * (double)(int)val.G + 0.114 * (double)(int)val.B) / 255.0;
				((Control)PrevButton).Foreground = (Brush)val2;
				((Control)NextButton).Foreground = (Brush)val2;
				((Control)NowPlayingTitle).Foreground = (Brush)val2;
				((Control)NowPlayingArtist).Foreground = (Brush)val2;
				((Control)RepeatButton).Foreground = (Brush)val2;
				((IconElement)RepeatIcon).Foreground = (Brush)val2;
				((UIElement)RepeatIcon).Opacity = ((_playbackMode == PlaybackMode.Off) ? 0.5 : 1.0);
				if (MiniRepeatIcon != (FontIcon)null)
				{
					((UIElement)MiniRepeatIcon).Opacity = ((_playbackMode == PlaybackMode.Off) ? 0.5 : 1.0);
				}
				RepeatOneBadge.Foreground = (Brush)val2;
				((Control)PlayPauseButton).Background = (Brush)val2;
				((FrameworkElement)PlayPauseButton).Resources["ButtonBackgroundPointerOver"] = val4;
				((FrameworkElement)PlayPauseButton).Resources["ButtonBackgroundPressed"] = val4;
				ElementTheme requestedTheme = ((FrameworkElement)PlayPauseButton).RequestedTheme;
				((FrameworkElement)PlayPauseButton).RequestedTheme = ElementTheme.Dark;
				((FrameworkElement)PlayPauseButton).RequestedTheme = ElementTheme.Light;
				((FrameworkElement)PlayPauseButton).RequestedTheme = requestedTheme;
				((IconElement)PlayPauseIcon).Foreground = (Brush)((num > 0.6) ? new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0)) : new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
				CurrentTimeText.Foreground = (Brush)val2;
				TotalTimeText.Foreground = (Brush)val2;
				((Panel)CustomProgressFill).Background = (Brush)val2;
				((Microsoft.UI.Xaml.Shapes.Shape)CustomProgressThumbInner).Fill = (Brush)val2;
				((IconElement)LyricsIcon).Foreground = (Brush)val2;
				
				LyricsButton.BorderBrush = (Brush)val2;
				((IconElement)VolumeIcon).Foreground = (Brush)val2;
				((Control)VolumeSlider).Foreground = (Brush)val2;
				((FrameworkElement)VolumeSlider).Resources["SliderThumbBackground"] = val2;
				((FrameworkElement)VolumeSlider).Resources["SliderThumbBackgroundPointerOver"] = val4;
				((FrameworkElement)VolumeSlider).Resources["SliderThumbBackgroundPressed"] = val4;
				((FrameworkElement)VolumeSlider).Resources["SliderTrackValueFillPointerOver"] = val4;
				((FrameworkElement)VolumeSlider).Resources["SliderTrackValueFillPressed"] = val4;
				ElementTheme requestedTheme2 = ((FrameworkElement)VolumeSlider).RequestedTheme;
				((FrameworkElement)VolumeSlider).RequestedTheme = ElementTheme.Dark;
				((FrameworkElement)VolumeSlider).RequestedTheme = ElementTheme.Light;
				((FrameworkElement)VolumeSlider).RequestedTheme = requestedTheme2;
				((Microsoft.UI.Xaml.Shapes.Shape)CustomProgressThumbOuter).Fill = (Brush)val2;
				return;
			}
			SolidColorBrush val5 = new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			Brush val6 = (Brush)Application.Current.Resources["AppAccentBrush"];
			((Control)PrevButton).Foreground = (Brush)val5;
			((Control)NextButton).Foreground = (Brush)val5;
			((Control)NowPlayingTitle).Foreground = (Brush)val5;
			((Control)NowPlayingArtist).Foreground = (Brush)new SolidColorBrush(Color.FromArgb((byte)165, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			((Control)RepeatButton).Foreground = (Brush)val5;
			((IconElement)RepeatIcon).Foreground = ((_playbackMode == PlaybackMode.Off) ? ((Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]) : ((Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"]));
			((UIElement)RepeatIcon).Opacity = 1.0;
			RepeatOneBadge.Foreground = val6;
			((Control)PlayPauseButton).Background = val6;
			((IconElement)PlayPauseIcon).Foreground = (Brush)val5;
			((FrameworkElement)PlayPauseButton).Resources.Remove("ButtonBackgroundPointerOver");
			((FrameworkElement)PlayPauseButton).Resources.Remove("ButtonBackgroundPressed");
			if (App.Settings.Current.Backdrop == AppBackdropStyle.Solid)
			{
				if (App.Settings.Current.ThemePresetIndex == 7)
				{
					((Control)PlayPauseButton).Background = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)40, (byte)40, (byte)40));
					((IconElement)PlayPauseIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
					if (_playbackMode != PlaybackMode.Off)
					{
						((IconElement)RepeatIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
						RepeatOneBadge.Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
					}
				}
				else if (App.Settings.Current.ThemePresetIndex == 8)
				{
					((Control)PlayPauseButton).Background = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)220, (byte)220, (byte)220));
					((IconElement)PlayPauseIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0));
					if (_playbackMode != PlaybackMode.Off)
					{
						((IconElement)RepeatIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0));
						RepeatOneBadge.Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0));
					}
				}
			}
			ElementTheme requestedTheme3 = ((FrameworkElement)PlayPauseButton).RequestedTheme;
			((FrameworkElement)PlayPauseButton).RequestedTheme = ElementTheme.Dark;
			((FrameworkElement)PlayPauseButton).RequestedTheme = ElementTheme.Light;
			((FrameworkElement)PlayPauseButton).RequestedTheme = requestedTheme3;
			CurrentTimeText.Foreground = (Brush)val5;
			TotalTimeText.Foreground = (Brush)val5;
			Brush val7 = val6;
			SolidColorBrush val8 = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0));
			if (App.Settings.Current.Backdrop == AppBackdropStyle.Solid)
			{
				if (App.Settings.Current.ThemePresetIndex == 7)
				{
					val7 = (Brush)val5;
					((FrameworkElement)PlayPauseButton).Resources["ButtonBackgroundPointerOver"] = (object)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)60, (byte)60, (byte)60));
					((FrameworkElement)PlayPauseButton).Resources["ButtonBackgroundPressed"] = (object)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)80, (byte)80, (byte)80));
				}
				else if (App.Settings.Current.ThemePresetIndex == 8)
				{
					val7 = (Brush)val8;
					((FrameworkElement)PlayPauseButton).Resources["ButtonBackgroundPointerOver"] = (object)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)220, (byte)220, (byte)220));
					((FrameworkElement)PlayPauseButton).Resources["ButtonBackgroundPressed"] = (object)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)200, (byte)200, (byte)200));
					((Control)PlayPauseButton).Background = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)240, (byte)240, (byte)240));
					((IconElement)PlayPauseIcon).Foreground = (Brush)val8;
					((Control)PrevButton).Foreground = (Brush)val8;
					((Control)NextButton).Foreground = (Brush)val8;
				}
			}
			((Panel)CustomProgressFill).Background = val7;
			((Microsoft.UI.Xaml.Shapes.Shape)CustomProgressThumbInner).Fill = val7;
			((Microsoft.UI.Xaml.Shapes.Shape)CustomProgressThumbOuter).Fill = val7;
			((IconElement)LyricsIcon).Foreground = (Brush)val5;
			
			LyricsButton.BorderBrush = val6;
			if (App.Settings.Current.Backdrop == AppBackdropStyle.Solid && App.Settings.Current.ThemePresetIndex == 8)
			{
				SolidColorBrush val9 = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)60, (byte)60, (byte)60));
				((IconElement)VolumeIcon).Foreground = (Brush)val8;
				((Control)VolumeSlider).Foreground = (Brush)val8;
				((FrameworkElement)VolumeSlider).Resources["SliderThumbBackground"] = val8;
				((FrameworkElement)VolumeSlider).Resources["SliderThumbBackgroundPointerOver"] = val9;
				((FrameworkElement)VolumeSlider).Resources["SliderThumbBackgroundPressed"] = val9;
				((FrameworkElement)VolumeSlider).Resources["SliderTrackValueFillPointerOver"] = val9;
				((FrameworkElement)VolumeSlider).Resources["SliderTrackValueFillPressed"] = val9;
			}
			else
			{
				((IconElement)VolumeIcon).Foreground = (Brush)val5;
				((DependencyObject)VolumeSlider).ClearValue(Control.ForegroundProperty);
				((FrameworkElement)VolumeSlider).Resources.Remove("SliderThumbBackground");
				((FrameworkElement)VolumeSlider).Resources.Remove("SliderThumbBackgroundPointerOver");
				((FrameworkElement)VolumeSlider).Resources.Remove("SliderThumbBackgroundPressed");
				((FrameworkElement)VolumeSlider).Resources.Remove("SliderTrackValueFillPointerOver");
				((FrameworkElement)VolumeSlider).Resources.Remove("SliderTrackValueFillPressed");
			}
			ElementTheme requestedTheme4 = ((FrameworkElement)VolumeSlider).RequestedTheme;
			((FrameworkElement)VolumeSlider).RequestedTheme = ElementTheme.Dark;
			((FrameworkElement)VolumeSlider).RequestedTheme = ElementTheme.Light;
			((FrameworkElement)VolumeSlider).RequestedTheme = requestedTheme4;
		}

		private void UpdatePlayerBarColorAsync(Track track)
		{
			ThemePreset themePreset = ThemePresets.All[Math.Clamp(App.Settings.Current.ThemePresetIndex, 0, ThemePresets.All.Length - 1)];
			Color themeSurface = ParseHexColor(themePreset.SurfaceHex);
			Task.Run(delegate
			{
				Color? c = GetAverageColorCpu(track.CoverArtPath ?? "");
				((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
				{
					ApplyPlayerBarColor(track.Id, c, themeSurface);
				});
			});
		}

		private void ApplyPlayerBarColor(string trackId, Color? avg, Color themeSurface)
		{
			Color c = (_lastComputedAvgColor = (avg ?? Color.FromArgb(byte.MaxValue, (byte)30, (byte)30, (byte)30)));
			UpdatePlayerButtonsColor();
			Color val = Darken(c, 0.4);
			SolidColorBrush val2 = new SolidColorBrush(Color.FromArgb((byte)0, (byte)0, (byte)0, (byte)0));
			LinearGradientBrush val3 = new LinearGradientBrush
			{
				StartPoint = new Point(0f, 0f),
				EndPoint = new Point(1f, 0f)
			};
			((GradientBrush)val3).GradientStops.Add(new GradientStop
			{
				Color = val,
				Offset = 0.0
			});
			((GradientBrush)val3).GradientStops.Add(new GradientStop
			{
				Color = themeSurface,
				Offset = 1.0
			});
			bool flag = App.Settings.Current.Backdrop == AppBackdropStyle.Solid;
			if (App.Settings.Current.PlayerGradientOverflowEnabled & flag)
			{
				_gradientStartColor = val;
				_gradientEndColor = themeSurface;
				LinearGradientBrush val4 = new LinearGradientBrush
				{
					StartPoint = new Point(0f, 0f),
					EndPoint = new Point(1f, 0f)
				};
				((GradientBrush)val4).GradientStops.Add(new GradientStop
				{
					Color = val,
					Offset = 0.0
				});
				((GradientBrush)val4).GradientStops.Add(new GradientStop
				{
					Color = themeSurface,
					Offset = 0.5
				});
				((GradientBrush)val4).GradientStops.Add(new GradientStop
				{
					Color = themeSurface,
					Offset = 1.0
				});
				PlayerGradientColorLayer.Background = (Brush)val4;
				Color color = ((SolidColorBrush)Application.Current.Resources["AppDeepBackgroundBrush"]).Color;
				Border playerGradientFadeLayer = PlayerGradientFadeLayer;
				Rect bounds = ((Window)this).Bounds;
				double height = bounds.Height;
				bounds = ((Window)this).Bounds;
				playerGradientFadeLayer.Background = (Brush)BuildBackgroundFadeBrush(color, height, Math.Min(bounds.Height * 0.6, 800.0), (((FrameworkElement)PlayerBar).ActualHeight > 0.0) ? ((FrameworkElement)PlayerBar).ActualHeight : 88.0);
				((Panel)PlayerBar).Background = (Brush)val2;
				PlayerBar.BorderThickness = new Thickness(0.0);
				((FrameworkElement)PlayerBar).Margin = new Thickness(0.0);
				((UIElement)PlayerGradientOverflow).Visibility = Visibility.Visible;
				((UIElement)PlayerGradientFadeLayer).Visibility = Visibility.Visible;
				ApplyGradientOverflowChrome(enabled: true);
				UpdateGradientOverflowLayout();
			}
			else
			{
				((Panel)PlayerBar).Background = (Brush)(flag ? ((object)val3) : ((object)val2));
				((FrameworkElement)PlayerBar).Margin = new Thickness(0.0);
				((UIElement)PlayerGradientOverflow).Visibility = Visibility.Collapsed;
				((UIElement)PlayerGradientFadeLayer).Visibility = Visibility.Collapsed;
				ApplyGradientOverflowChrome(enabled: false);
			}
		}

		private static LinearGradientBrush BuildBackgroundFadeBrush(Color bgColor, double windowHeight, double fadeHeight, double playerH)
		{
			LinearGradientBrush val = new LinearGradientBrush
			{
				StartPoint = new Point(0.5, 0.0),
				EndPoint = new Point(0.5, 1.0)
			};
			double num = windowHeight - playerH - fadeHeight;
			double num2 = Math.Max(0.0, num - 150.0);
			double num3 = num + fadeHeight * 0.4;
			Color color = Color.FromArgb((byte)0, bgColor.R, bgColor.G, bgColor.B);
			((GradientBrush)val).GradientStops.Add(new GradientStop
			{
				Color = color,
				Offset = 0.0
			});
			((GradientBrush)val).GradientStops.Add(new GradientStop
			{
				Color = color,
				Offset = Math.Clamp(num2 / windowHeight, 0.0, 1.0)
			});
			((GradientBrush)val).GradientStops.Add(new GradientStop
			{
				Color = bgColor,
				Offset = Math.Clamp(num / windowHeight, 0.0, 1.0)
			});
			((GradientBrush)val).GradientStops.Add(new GradientStop
			{
				Color = color,
				Offset = Math.Clamp(num3 / windowHeight, 0.0, 1.0)
			});
			((GradientBrush)val).GradientStops.Add(new GradientStop
			{
				Color = color,
				Offset = 1.0
			});
			return val;
		}

		private static void AddSmoothHorizontalStops(LinearGradientBrush brush, Color startColor, Color endColor, double windowWidth)
		{
			((GradientBrush)brush).GradientStops.Add(new GradientStop
			{
				Color = startColor,
				Offset = 0.0
			});
			((GradientBrush)brush).GradientStops.Add(new GradientStop
			{
				Color = endColor,
				Offset = 0.5
			});
			((GradientBrush)brush).GradientStops.Add(new GradientStop
			{
				Color = endColor,
				Offset = 1.0
			});
		}

		private static void AddSmoothVerticalStops(LinearGradientBrush brush, Color rgb, double opaqueUntil, double fadePower)
		{
			for (int i = 0; i <= 48; i++)
			{
				double num = (double)i / 48.0;
				double num2 = ((num <= opaqueUntil) ? 1.0 : Math.Pow(1.0 - (num - opaqueUntil) / (1.0 - opaqueUntil), fadePower));
				((GradientBrush)brush).GradientStops.Add(new GradientStop
				{
					Color = Color.FromArgb((byte)(num2 * 255.0), rgb.R, rgb.G, rgb.B),
					Offset = num
				});
			}
		}

		private static void AddSmoothVerticalStopsFixed(LinearGradientBrush brush, Color rgb, double opaqueHeightPx, double containerHeightPx, double fadePower)
		{
			for (int i = 0; i <= 48; i++)
			{
				double num = (double)i / 48.0;
				double num2 = num * containerHeightPx;
				double num3 = ((num2 <= opaqueHeightPx) ? 1.0 : Math.Pow(1.0 - (num2 - opaqueHeightPx) / (containerHeightPx - opaqueHeightPx), fadePower));
				((GradientBrush)brush).GradientStops.Add(new GradientStop
				{
					Color = Color.FromArgb((byte)(num3 * 255.0), rgb.R, rgb.G, rgb.B),
					Offset = num
				});
			}
		}

		private void UpdateGradientOverflowLayout()
		{
			if ((int)((UIElement)PlayerGradientOverflow).Visibility <= 0)
			{
				double num = ((((FrameworkElement)PlayerBar).ActualHeight > 0.0) ? ((FrameworkElement)PlayerBar).ActualHeight : 88.0);
				double num2;
				if (!(((FrameworkElement)RootGrid).ActualHeight > 0.0))
				{
					Rect bounds = ((Window)this).Bounds;
					num2 = bounds.Height;
				}
				else
				{
					num2 = ((FrameworkElement)RootGrid).ActualHeight;
				}
				double num3 = num2;
				double num4 = Math.Min(num3 * 0.6, 800.0);
				((FrameworkElement)PlayerGradientColorLayer).Height = num4 + num;
				((FrameworkElement)PlayerGradientFadeLayer).Height = double.NaN;
				((FrameworkElement)PlayerGradientFadeLayer).VerticalAlignment = VerticalAlignment.Stretch;
				((FrameworkElement)PlayerGradientFadeLayer).Margin = new Thickness(0.0);
				if (_gradientStartColor.HasValue && _gradientEndColor.HasValue)
				{
					LinearGradientBrush val = new LinearGradientBrush
					{
						StartPoint = new Point(0f, 0f),
						EndPoint = new Point(1f, 0f)
					};
					((GradientBrush)val).GradientStops.Add(new GradientStop
					{
						Color = _gradientStartColor.Value,
						Offset = 0.0
					});
					((GradientBrush)val).GradientStops.Add(new GradientStop
					{
						Color = _gradientEndColor.Value,
						Offset = 0.5
					});
					((GradientBrush)val).GradientStops.Add(new GradientStop
					{
						Color = _gradientEndColor.Value,
						Offset = 1.0
					});
					PlayerGradientColorLayer.Background = (Brush)val;
				}
				Color color = ((SolidColorBrush)Application.Current.Resources["AppDeepBackgroundBrush"]).Color;
				PlayerGradientFadeLayer.Background = (Brush)BuildBackgroundFadeBrush(color, num3, num4, num);
			}
		}

		private void UpdateNowPlayingTextMaxWidth()
		{
			if (PlayerBar == (Grid)null || NowPlayingTextContainer == (StackPanel)null || PlayerBar.ColumnDefinitions.Count == 0)
			{
				return;
			}
			bool flag = ((ContentControl)ContentFrame).Content is NowPlayingPage;
			if (flag)
			{
				GridLength width = PlayerBar.ColumnDefinitions[0].Width;
				if (width.IsAuto)
				{
					PlayerBar.ColumnDefinitions[0].Width = new GridLength(PlayerBar.ColumnDefinitions[0].ActualWidth);
				}
			}
			else
			{
				PlayerBar.ColumnDefinitions[0].Width = new GridLength(1.0, GridUnitType.Auto);
			}
			double actualWidth = PlayerBar.ColumnDefinitions[0].ActualWidth;
			if (!(actualWidth <= 0.0))
			{
				double num = (flag ? 10 : 68);
				double val = actualWidth - num;
				double val2 = (flag ? 458 : 400);
				val = Math.Max(180.0, Math.Min(val, val2));
				((FrameworkElement)NowPlayingTextContainer).MaxWidth = val;
			}
		}

		private void ResolveNavigationViewChromeElements()
		{
			RootNav.OpenPaneLength = (Strings.Current.IsFr ? 210 : 160);
			if (_navMainContentGrid != (Grid)null)
			{
				return;
			}
			Grid navMainContentGrid = null;
			double num = 0.0;
			foreach (FrameworkElement item in WalkVisualTree((DependencyObject)RootNav))
			{
				Grid val = item as Grid;
				if (val != null && !(((FrameworkElement)val).Name != "ContentGrid"))
				{
					double num2 = ((FrameworkElement)val).ActualWidth * ((FrameworkElement)val).ActualHeight;
					if (num2 > num)
					{
						num = num2;
						navMainContentGrid = val;
					}
				}
			}
			_navMainContentGrid = navMainContentGrid;
			foreach (FrameworkElement item2 in WalkVisualTree((DependencyObject)RootNav))
			{
				Grid val2 = item2 as Grid;
				if (val2 != null && ((FrameworkElement)val2).Name == "ShadowCaster")
				{
					_navShadowCaster = (UIElement?)(object)val2;
					break;
				}
			}
		}

		private static IEnumerable<FrameworkElement> WalkVisualTree(DependencyObject root)
		{
			int count = VisualTreeHelper.GetChildrenCount(root);
			for (int i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				FrameworkElement fe = child as FrameworkElement;
				if (fe != null)
				{
					yield return fe;
					foreach (FrameworkElement item in WalkVisualTree((DependencyObject)fe))
					{
						yield return item;
					}
					continue;
				}
				foreach (FrameworkElement item2 in WalkVisualTree(child))
				{
					yield return item2;
				}
			}
		}

		private void AnimateNavItemsText(bool opening)
		{
			IEnumerable<NavigationViewItem> enumerable = RootNav.MenuItems.OfType<NavigationViewItem>().Concat(RootNav.FooterMenuItems.OfType<NavigationViewItem>());
			object settingsItem = RootNav.SettingsItem;
			NavigationViewItem val = settingsItem as NavigationViewItem;
			if (val != null)
			{
				enumerable = enumerable.Append(val);
			}
			foreach (NavigationViewItem item in enumerable)
			{
				ContentPresenter val2 = WalkVisualTree((DependencyObject)item).OfType<ContentPresenter>().FirstOrDefault((ContentPresenter c) => ((FrameworkElement)c).Name == "ContentPresenter");
				if (val2 != (ContentPresenter)null)
				{
					Storyboard val3 = new Storyboard();
					DoubleAnimation val4 = new DoubleAnimation
					{
						To = (opening ? 1 : 0),
						Duration = TimeSpan.FromMilliseconds(opening ? 300 : 150)
					};
					Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)val2);
					Storyboard.SetTargetProperty((Timeline)(object)val4, "Opacity");
					val3.Children.Add((Timeline)(object)val4);
					val3.Begin();
				}
			}
		}

		private void RootNav_PaneOpening(NavigationView sender, object args)
		{
			AnimateNavItemsText(opening: true);
		}

		private async void RootNav_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
		{
			if (_isManuallyClosingPane)
			{
				AnimateNavItemsText(opening: false);
				return;
			}
			args.Cancel = true;
			_isManuallyClosingPane = true;
			((UIElement)RootNav).UpdateLayout();
			AnimateNavItemsText(opening: false);
			await Task.Delay(150);
			RootNav.IsPaneOpen = false;
			_isManuallyClosingPane = false;
		}

		private void ApplyNavigationViewChrome(bool enabled)
		{
			ResolveNavigationViewChromeElements();
			object settingsItem = RootNav.SettingsItem;
			NavigationViewItem val = settingsItem as NavigationViewItem;
			if (val != null)
			{
				((ContentControl)val).Content = Strings.Current.CS_Settings;
			}
			if (_navMainContentGrid != (Grid)null)
			{
				_navMainContentGrid.BorderThickness = (enabled ? NavContentBorderHidden : NavContentBorderDefault);
				Grid? navMainContentGrid = _navMainContentGrid;
				object borderBrush;
				if (!enabled)
				{
					object obj = Application.Current.Resources["CardStrokeColorDefaultBrush"];
					borderBrush = ((obj is Brush) ? obj : null) ?? transparentBrush;
				}
				else
				{
					borderBrush = transparentBrush;
				}
				navMainContentGrid.BorderBrush = (Brush)borderBrush;
			}
			if (_navShadowCaster != (UIElement)null)
			{
				_navShadowCaster.Visibility = (Visibility)(enabled ? 1 : 0);
			}
		}

		private void ApplyGradientOverflowChrome(bool enabled)
		{
			ApplyNavigationViewChrome(enabled: true);
			((FrameworkElement)RootNav).Resources["NavigationViewContentGridBorderThickness"] = NavContentBorderHidden;
			((FrameworkElement)RootNav).Resources["NavigationViewMinimalContentGridBorderThickness"] = NavContentBorderHidden;
			((FrameworkElement)RootNav).Resources["NavigationViewContentGridBorderBrush"] = transparentBrush;
			((FrameworkElement)RootNav).Resources["NavigationViewContentGridCornerRadius"] = (object)new CornerRadius(0.0);
			if (!enabled)
			{
				((FrameworkElement)PlayerBar).Margin = new Thickness(0.0);
			}
		}

		private double GetNavPaneWidth()
		{
			NavigationViewDisplayMode displayMode = RootNav.DisplayMode;
			if ((int)displayMode != 1)
			{
				if ((int)displayMode == 2)
				{
					return RootNav.OpenPaneLength;
				}
				return 0.0;
			}
			return RootNav.CompactPaneLength;
		}

		public void ApplyGradientOverflowSetting()
		{
			if (_nowPlayingId != null)
			{
				Track track = _queue.FirstOrDefault((Track t) => t.Id == _nowPlayingId) ?? _library.FirstOrDefault((Track t) => t.Id == _nowPlayingId);
				if (track != null)
				{
					UpdatePlayerBarColorAsync(track);
				}
			}
		}

		private static Color ParseHexColor(string hex)
		{
			hex = hex.TrimStart('#');
			return Color.FromArgb(byte.MaxValue, Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
		}

		public void RefreshThemeDependentUI()
		{
			_coverColorCache.Clear();
			if (_currentIndex >= 0)
			{
				UpdatePlayerBarColorAsync(_library[_currentIndex]);
			}
			else if (App.Settings.Current.Backdrop == AppBackdropStyle.Solid)
			{
				((Panel)PlayerBar).Background = (Brush)Application.Current.Resources["AppSurfaceBrush"];
			}
			ApplyNativeWindowBackgroundFix();
			ForceUIRepaint();
		}

		private void ForceUIRepaint()
		{
			ElementTheme currentTheme = ((FrameworkElement)RootGrid).RequestedTheme;
			((FrameworkElement)RootGrid).RequestedTheme = (ElementTheme)(((int)currentTheme != 1) ? 1 : 2);
			((Window)this).DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, (DispatcherQueueHandler)delegate
			{
				((FrameworkElement)RootGrid).RequestedTheme = currentTheme;
			});
		}

		public void RefreshNavCategories()
		{
			AppSettings current = App.Settings.Current;
			List<NavigationViewItem> list = new List<NavigationViewItem> { NavLibrary, NavAlbums, NavPlaylists, NavQueue, NavArtists, NavGenres, NavFolders, NavStatistics, NavDownload };
			List<bool> list2 = new List<bool> { current.ShowLibraryCategory, current.ShowAlbumsCategory, current.ShowPlaylistsCategory, true, current.ShowArtistsCategory, current.ShowGenresCategory, current.ShowFoldersCategory, current.ShowStatisticsCategory, current.ShowDownloadCategory };
			for (int i = 0; i < list.Count; i++)
			{
				NavigationViewItem item = list[i];
				bool flag = list2[i];
				if (flag && !RootNav.MenuItems.Contains(item))
				{
					int index = 0;
					for (int j = 0; j < i; j++)
					{
						if (list2[j] && RootNav.MenuItems.Contains(list[j]))
						{
							index = RootNav.MenuItems.IndexOf(list[j]) + 1;
						}
					}
					RootNav.MenuItems.Insert(index, item);
					Storyboard val = new Storyboard();
					((UIElement)item).Opacity = 0.0;
					((UIElement)item).RenderTransform = (Transform)new TranslateTransform
					{
						X = -20.0
					};
					DoubleAnimation val2 = new DoubleAnimation
					{
						From = 0.0,
						To = 1.0,
						Duration = TimeSpan.FromMilliseconds(300.0)
					};
					Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)item);
					Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
					DoubleAnimation val3 = new DoubleAnimation
					{
						From = -20.0,
						To = 0.0,
						Duration = TimeSpan.FromMilliseconds(300.0),
						EasingFunction = (EasingFunctionBase)new CubicEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)((UIElement)item).RenderTransform);
					Storyboard.SetTargetProperty((Timeline)(object)val3, "X");
					val.Children.Add((Timeline)(object)val2);
					val.Children.Add((Timeline)(object)val3);
					val.Begin();
				}
				else
				{
					if (flag || !RootNav.MenuItems.Contains(item))
					{
						continue;
					}
					Storyboard val4 = new Storyboard();
					if (!(((UIElement)item).RenderTransform is TranslateTransform))
					{
						((UIElement)item).RenderTransform = (Transform)new TranslateTransform();
					}
					DoubleAnimation val5 = new DoubleAnimation
					{
						From = ((UIElement)item).Opacity,
						To = 0.0,
						Duration = TimeSpan.FromMilliseconds(200.0)
					};
					Storyboard.SetTarget((Timeline)(object)val5, (DependencyObject)item);
					Storyboard.SetTargetProperty((Timeline)(object)val5, "Opacity");
					DoubleAnimation val6 = new DoubleAnimation
					{
						From = 0.0,
						To = -20.0,
						Duration = TimeSpan.FromMilliseconds(200.0)
					};
					Storyboard.SetTarget((Timeline)(object)val6, (DependencyObject)((UIElement)item).RenderTransform);
					Storyboard.SetTargetProperty((Timeline)(object)val6, "X");
					val4.Children.Add((Timeline)(object)val5);
					val4.Children.Add((Timeline)(object)val6);
					((Timeline)val4).Completed += delegate
					{
						if (RootNav.MenuItems.Contains(item))
						{
							RootNav.MenuItems.Remove(item);
						}
						((UIElement)item).RenderTransform = null;
					};
					val4.Begin();
				}
			}
		}

		public async Task ReloadLibraryFromCacheAsync()
		{
			await LoadLibraryFromCacheAsync();
			RestoreSidebarSelection();
		}

		public void AddTrackToLibrary(Track track)
		{
			Track track2 = _library.FirstOrDefault((Track t) => string.Equals(t.FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase));
			if (track2 != null)
			{
				int index = _library.IndexOf(track2);
				_library[index] = track;
			}
			else
			{
				_library.Add(track);
			}
			_libraryPageInstance?.SetTracks(_library);
			_albumsPageInstance?.LoadData(_library);
			_artistsPageInstance?.LoadData(_library);
			_genresPageInstance?.LoadData(_library);
			_foldersPageInstance?.LoadData(_library);
		}

		private async Task LoadLibraryFromCacheAsync()
		{
			await App.Cache.InitializeAsync();
			_library = await App.Cache.LoadAllTracksAsync();
			RestoreManualQueue();
			_libraryPageInstance?.SetTracks(_library);
			_albumsPageInstance?.LoadData(_library);
			_artistsPageInstance?.LoadData(_library);
			_genresPageInstance?.LoadData(_library);
			_foldersPageInstance?.LoadData(_library);
			FetchMissingCoversInBackgroundAsync(_library.ToList());
			BackgroundRescanAsync();
		}

		private async Task FetchMissingCoversInBackgroundAsync(List<Track> tracks)
		{
			if (_isFetchingCovers)
			{
				return;
			}
			_isFetchingCovers = true;
			try
			{
				List<Track> missing = tracks.Where((Track t) => string.IsNullOrEmpty(t.CoverArtPath)).ToList();
				List<Track> stillMissing = new List<Track>();
				// Regroupe les tracks par album (même clé stable que la recherche web plus bas) afin
				// de n'extraire/écrire l'image embarquée qu'une seule fois par album, au lieu d'un
				// fichier JPEG quasi-identique par track (ex. 430 fichiers pour 29 albums).
				List<IGrouping<string, Track>> embeddedGroups = missing.GroupBy(delegate (Track t)
				{
					string a = t.Artist ?? "";
					string b = t.Album ?? "";
					if (b.ToLowerInvariant().Contains("inconnu") || b.ToLowerInvariant().Contains("unknown")) b = "";
					if (a.ToLowerInvariant().Contains("inconnu") || a.ToLowerInvariant().Contains("unknown")) a = "";
					return (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)) ? (a + "|" + b) : t.Id;
				}).ToList();
				foreach (IGrouping<string, Track> group in embeddedGroups)
				{
					Track firstTrack = group.First();
					string cacheKey = Resona.Services.CoverArtService.GetAlbumCacheKey(firstTrack.Artist, firstTrack.Album, firstTrack.Id);
					byte[] embedded = App.Scanner.ExtractEmbeddedCover(firstTrack.FilePath);
					string localPath = null;
					if (embedded != null && embedded.Length != 0)
					{
						localPath = await App.CoverArt.SaveEmbeddedCoverAsync(cacheKey, embedded);
					}
					if (localPath != null)
					{
						foreach (Track track in group)
						{
							track.CoverArtPath = localPath;
							await App.Cache.UpdateTrackAsync(track);
						}
						continue;
					}
					// Pas de cover embarquée sur le premier track du groupe : on retente sur les
					// autres tracks du même album (des tags ID3 incohérents entre pistes existent),
					// sinon tout le groupe part en recherche web plus bas.
					bool found = false;
					foreach (Track track in group.Skip(1))
					{
						byte[] embedded2 = App.Scanner.ExtractEmbeddedCover(track.FilePath);
						if (embedded2 != null && embedded2.Length != 0)
						{
							string localPath2 = await App.CoverArt.SaveEmbeddedCoverAsync(cacheKey, embedded2);
							if (localPath2 != null)
							{
								found = true;
								foreach (Track t2 in group)
								{
									t2.CoverArtPath = localPath2;
									await App.Cache.UpdateTrackAsync(t2);
								}
								break;
							}
						}
					}
					if (!found)
					{
						stillMissing.AddRange(group);
					}
				}
				if (!App.Settings.Current.AutoFetchMissingCovers)
				{
					return;
				}
				List<IGrouping<string, Track>> grouped = stillMissing.Where((Track t) => !_failedCoverSearches.Contains(t.Id)).GroupBy(delegate(Track t)
				{
					string text = t.Artist ?? "";
					string text2 = t.Album ?? "";
					if (text2.ToLowerInvariant().Contains("inconnu") || text2.ToLowerInvariant().Contains("unknown"))
					{
						text2 = "";
					}
					if (text.ToLowerInvariant().Contains("inconnu") || text.ToLowerInvariant().Contains("unknown"))
					{
						text = "";
					}
					return (!string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text)) ? (text + "|" + text2) : t.Id;
				}).ToList();
				foreach (IGrouping<string, Track> group in grouped)
				{
					if (!App.Settings.Current.AutoFetchMissingCovers)
					{
						break;
					}
					Track firstTrack = group.First();
					if (string.IsNullOrWhiteSpace(firstTrack.Artist) && string.IsNullOrWhiteSpace(firstTrack.Album) && string.IsNullOrWhiteSpace(firstTrack.Title))
					{
						continue;
					}
					string path = await App.CoverArt.FindAndCacheCoverAsync(firstTrack.Id, firstTrack.Artist, firstTrack.Album, firstTrack.Title);
					if (path != null)
					{
						foreach (Track track2 in group)
						{
							track2.CoverArtPath = path;
							await App.Cache.UpdateTrackAsync(track2);
						}
						((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
						{
							if (_currentIndex >= 0 && group.Any((Track t) => t.Id == _library[_currentIndex].Id))
							{
								SetPlayerCover(path);
							}
						});
					}
					else
					{
						foreach (Track track3 in group)
						{
							_failedCoverSearches.Add(track3.Id);
						}
					}
					await Task.Delay(800);
				}
			}
			catch
			{
			}
			finally
			{
				_isFetchingCovers = false;
			}
		}

		public void FetchCoversForTracks(List<Track> tracks)
		{
			FetchMissingCoversInBackgroundAsync(tracks);
		}

		public void TriggerLibraryRescan()
		{
			RunRescanAsync();
		}

		public void RefreshSettingsFolders()
		{
			SettingsPageInstance?.RefreshFoldersList();
		}

		public void MarkPathDirty(string filePath)
		{
			if (!string.IsNullOrWhiteSpace(filePath))
			{
				lock (_dirtyLock)
				{
					_dirtyPaths.Add(filePath);
				}
			}
		}

		private async Task RunRescanAsync()
		{
			if (_scanInProgress)
			{
				_rescanPending = true;
				return;
			}
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
				{
					RunRescanAsync();
				}
			}
		}

		private async Task BackgroundRescanAsync()
		{
			await App.Cache.InitializeAsync();
			HashSet<string> knownPaths = await App.Cache.GetCachedFilePathsAsync();
			foreach (Track t in _library)
			{
				knownPaths.Add(t.FilePath);
			}
			List<string> folders = App.Settings.Current.MusicFolders.ToList();
			if (folders.Count == 0)
			{
				return;
			}
			string dlFolder = App.Settings.Current.DownloadFolder;
			if (!string.IsNullOrWhiteSpace(dlFolder) && Directory.Exists(dlFolder) && !folders.Contains<string>(dlFolder, StringComparer.OrdinalIgnoreCase))
			{
				folders.Add(dlFolder);
			}
			bool scanStarted = false;
			List<Track> allNew = new List<Track>();
			HashSet<string> allCurrentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string coverDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "Covers");
			Directory.CreateDirectory(coverDir);
			DispatcherQueueHandler val = default(DispatcherQueueHandler);
			await Task.Run(delegate
			{
				try
				{
				int num = 0;
				foreach (string item in folders)
				{
					IEnumerable<string> files;
					try
					{
						files = App.Scanner.EnumerateAudioFiles(item).ToList();
					}
					catch (Exception)
					{
						continue;
					}
					foreach (string item2 in files)
					{
						if (!scanStarted)
						{
							scanStarted = true;
							DispatcherQueue dispatcherQueue = ((Window)this).DispatcherQueue;
							DispatcherQueueHandler obj = val;
							if (obj == null)
							{
								DispatcherQueueHandler val2 = delegate
								{
									StartScanAnimation();
								};
								DispatcherQueueHandler val3 = val2;
								val = val2;
								obj = val3;
							}
							dispatcherQueue.TryEnqueue(obj);
						}
						allCurrentPaths.Add(item2);
						bool flag;
						lock (_dirtyLock)
						{
							flag = _dirtyPaths.Remove(item2);
						}
						if (!knownPaths.Contains(item2) || flag)
						{
							knownPaths.Add(item2);
							Track track = null;
							try
							{
								track = App.Scanner.ExtractMetadata(item2, out byte[] embeddedCoverBytes2);
								if (track != null && embeddedCoverBytes2 != null && embeddedCoverBytes2.Length != 0)
								{
                                    string cacheKey = Resona.Services.CoverArtService.GetAlbumCacheKey(track.AlbumArtist != string.Empty ? track.AlbumArtist : track.Artist, track.Album, track.Id);
									string text = Path.Combine(coverDir, cacheKey + ".jpg");
									if (!System.IO.File.Exists(text))
                                    {
                                        File.WriteAllBytes(text, embeddedCoverBytes2);
                                    }
									track.CoverArtPath = text;
								}
							}
							catch (Exception)
							{
								track = null;
							}
							if (track != null)
							{
								allNew.Add(track);
								num++;
								if (num % 50 == 0)
								{
									GC.Collect();
								}
							}
						}
					}
				}
				}
				catch (Exception)
				{
				}
			});
			List<string> deletedPaths = knownPaths.Where((string p) => !allCurrentPaths.Contains(p)).ToList();
			List<Track> deletedTracks = _library.Where((Track track) => deletedPaths.Contains<string>(track.FilePath, StringComparer.OrdinalIgnoreCase)).ToList();
			DispatcherQueueHandler val4 = default(DispatcherQueueHandler);
			try
			{
				await Task.Run(async delegate
				{
					int count = 0;
					foreach (Track track in allNew)
					{
						try
						{
							await App.Cache.UpsertTrackAsync(track);
						}
						catch (Exception)
						{
						}
						count++;
						if (count % 50 == 0)
						{
							GC.Collect();
						}
					}
				});
			}
			catch (Exception)
			{
			}
			finally
			{
				if (scanStarted)
				{
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						StopScanAnimation();
					});
				}
			}
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				bool flag = false;
				if (deletedTracks.Count > 0)
				{
					_library.RemoveAll((Track track2) => deletedPaths.Contains<string>(track2.FilePath, StringComparer.OrdinalIgnoreCase));
					flag = true;
				}
				if (allNew.Count > 0)
				{
					HashSet<string> hashSet = new HashSet<string>(_library.Select((Track track2) => track2.FilePath), StringComparer.OrdinalIgnoreCase);
					List<Track> list = new List<Track>();
					foreach (Track track in allNew)
					{
						if (hashSet.Contains(track.FilePath))
						{
							int num = _library.FindIndex((Track track2) => string.Equals(track2.FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase));
							if (num >= 0)
							{
								_library[num] = track;
								flag = true;
							}
						}
						else
						{
							list.Add(track);
						}
					}
					if (list.Count > 0)
					{
						_library.AddRange(list);
						flag = true;
						FetchMissingCoversInBackgroundAsync(list.ToList());
					}
				}
				if (flag)
				{
					DispatcherQueue dispatcherQueue2 = ((Window)this).DispatcherQueue;
					DispatcherQueueHandler obj2 = val4;
					if (obj2 == null)
					{
						DispatcherQueueHandler val5 = delegate
						{
							HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
							_library = _library.Where((Track track2) => seen.Add(track2.FilePath)).ToList();
							_libraryPageInstance?.SetTracks(_library);
							_albumsPageInstance?.LoadData(_library);
							_artistsPageInstance?.LoadData(_library);
							_genresPageInstance?.LoadData(_library);
							_foldersPageInstance?.LoadData(_library);
						};
						DispatcherQueueHandler val3 = val5;
						val4 = val5;
						obj2 = val3;
					}
					dispatcherQueue2.TryEnqueue((DispatcherQueuePriority)(-10), obj2);
				}
			});
		}

		private int _activeScanCount;

		public void StartScanAnimation()
		{
			_activeScanCount++;
			if (_scanStoryboard != null)
			{
				return;
			}
			((UIElement)ScanProgressRow).Visibility = Visibility.Visible;
			double value = ((Window)this).AppWindow.Size.Width + 160;
			Storyboard val = new Storyboard
			{
				RepeatBehavior = RepeatBehavior.Forever
			};
			DoubleAnimation val2 = new DoubleAnimation
			{
				From = -160.0,
				To = value,
				Duration = TimeSpan.FromMilliseconds(1600.0),
				EasingFunction = (EasingFunctionBase)new SineEase
				{
					EasingMode = EasingMode.EaseInOut
				}
			};
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)ScanAnimTranslate);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "X");
			val.Children.Add((Timeline)(object)val2);
			val.Begin();
			_scanStoryboard = val;
		}

		public void StopScanAnimation()
		{
			_activeScanCount--;
			if (_activeScanCount > 0)
			{
				return;
			}
			_activeScanCount = 0;
			Storyboard? scanStoryboard = _scanStoryboard;
			if (scanStoryboard != null)
			{
				scanStoryboard.Stop();
			}
			_scanStoryboard = null;
			((UIElement)ScanProgressRow).Visibility = Visibility.Collapsed;
		}

		private void SetupPositionTimer()
		{
			_positionTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(33.0)
			};
			_positionTimer.Tick += delegate
			{
				if (!_isSliderDragging)
				{
					TimeSpan currentPosition = App.AudioEngine.CurrentPosition;
					TimeSpan duration = App.AudioEngine.TotalDuration;
					if (duration.TotalSeconds <= 0.0 && currentPosition.TotalSeconds > 0.0)
					{
						duration = currentPosition;
					}
					if (!(duration.TotalSeconds <= 0.0))
					{
						if (currentPosition.TotalSeconds > ((RangeBase)ProgressSlider).Maximum)
						{
							((RangeBase)ProgressSlider).Maximum = currentPosition.TotalSeconds;
							TotalTimeText.Text = FormatTime(TimeSpan.FromSeconds(((RangeBase)ProgressSlider).Maximum));
						}
						if (MiniProgressSlider != (Slider)null && ((RangeBase)MiniProgressSlider).Maximum != ((RangeBase)ProgressSlider).Maximum)
						{
							((RangeBase)MiniProgressSlider).Maximum = ((RangeBase)ProgressSlider).Maximum;
						}
						if (Math.Abs(((RangeBase)ProgressSlider).Value - currentPosition.TotalSeconds) > 0.05)
						{
							((RangeBase)ProgressSlider).Value = currentPosition.TotalSeconds;
							if (MiniProgressSlider != (Slider)null)
							{
								((RangeBase)MiniProgressSlider).Value = currentPosition.TotalSeconds;
							}
						}
						string text = FormatTime(currentPosition);
						if (CurrentTimeText.Text != text)
						{
							CurrentTimeText.Text = text;
						}
						if (_queueIndex >= 0 && _queueIndex < _queue.Count)
						{
							Track track = _queue[_queueIndex];
							if (track.Duration.TotalSeconds <= 0.0)
							{
								track.Duration = duration;
								App.Cache.UpsertTrackAsync(track);
							}
						}
						TickSyncedLyrics(currentPosition);
						if (App.Settings.Current.EnableCrossfade && !_crossfadeTriggered && !App.AudioEngine.IsFadingOut && (int)App.AudioEngine.State == 1 && duration.TotalSeconds > 0.0 && _queue.Count > 0 && _queueIndex >= 0)
						{
							double num = App.Settings.Current.CrossfadeDurationSeconds;
							double num2 = num / 1000.0;
							double num3 = duration.TotalSeconds - currentPosition.TotalSeconds;
							if (num3 <= num2 && num3 > 0.2)
							{
								_crossfadeTriggered = true;
								((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
								{
									NextButton_Click(null, null);
								});
							}
						}
						if ((int)App.AudioEngine.State == 1)
						{
							App.PlayStats.AddListenTime(TimeSpan.FromMilliseconds(33.0));
						}
					}
				}
			};
			_positionTimer.Start();
		}

		private static string FormatTime(TimeSpan t)
		{
			return (t.TotalHours >= 1.0) ? t.ToString("h\\:mm\\:ss") : t.ToString("m\\:ss");
		}

		private void UpdateIsPlayingGlobally(string filePath, bool isPlaying)
		{
			Track track = _queue.FirstOrDefault((Track x) => x.FilePath == filePath);
			if (track != null)
			{
				UpdateSMTCInfo(track, isPlaying);
			}
			if (string.IsNullOrEmpty(filePath))
			{
				return;
			}
			foreach (Track item in _library.Where((Track x) => x.FilePath == filePath))
			{
				item.IsPlaying = isPlaying;
			}
			foreach (Track item2 in _queue.Where((Track x) => x.FilePath == filePath))
			{
				item2.IsPlaying = isPlaying;
			}
			if ((Page)_libraryPageInstance != (Page)null)
			{
				foreach (Track item3 in _libraryPageInstance.FilteredTracks.Where((Track x) => x.FilePath == filePath))
				{
					item3.IsPlaying = isPlaying;
				}
			}
			if ((Page)_queuePageInstance != (Page)null)
			{
				foreach (Track item4 in _queuePageInstance.DisplayedTracks.Where((Track x) => x.FilePath == filePath))
				{
					item4.IsPlaying = isPlaying;
				}
			}
			if (!((Page)_playlistsPageInstance != (Page)null))
			{
				return;
			}
			foreach (Track item5 in _playlistsPageInstance.DisplayedTracks.Where((Track x) => x.FilePath == filePath))
			{
				item5.IsPlaying = isPlaying;
			}
		}

		public async void PlayTrack(Track track, List<Track>? queue = null, bool isGoingBack = false, bool isGoingForward = false, string? sourceName = null, bool fromManualQueue = false)
		{
			Track old = _queue.FirstOrDefault((Track t) => t.Id == _nowPlayingId);
			if (old != null)
			{
				if (isGoingBack)
				{
					_playbackFuture.Push(old);
				}
				else if (isGoingForward)
				{
					_playbackHistory.Push(old);
				}
				else
				{
					_playbackHistory.Push(old);
					_playbackFuture.Clear();
				}
			}
			if (App.NowPlayingFilePath != null)
			{
				UpdateIsPlayingGlobally(App.NowPlayingFilePath, isPlaying: false);
			}
			bool queueChanged = false;
			if (queue != null && !fromManualQueue)
			{
				if (_queue != queue)
				{
					queueChanged = true;
				}
				_queue = queue;
			}
			if (!fromManualQueue && _playbackMode == PlaybackMode.Shuffle)
			{
				if (queueChanged)
				{
					RegenerateShuffleUpcoming();
				}
				else if (!isGoingForward && !isGoingBack)
				{
					int idx = _shuffleUpcoming.FindIndex((Track t) => t.Id == track.Id);
					if (idx >= 0)
					{
						_shuffleUpcoming.RemoveRange(0, idx + 1);
					}
					else
					{
						_shuffleUpcoming.RemoveAll((Track t) => t.Id == track.Id);
					}
					_playbackFuture.Clear();
				}
			}
			else if (_queue == null)
			{
				_queue = _library;
			}
			_isCurrentlyPlayingManualQueue = fromManualQueue;
			if (!fromManualQueue)
			{
				_queueIndex = _queue.FindIndex((Track t) => t.Id == track.Id);
				if (sourceName != null)
				{
					_queueSourceName = sourceName;
				}
			}
			_currentIndex = _library.FindIndex((Track t) => t.Id == track.Id);
			AppSettings settings = App.Settings.Current;
			_nowPlayingId = track.Id;
			_nowPlayingFilePath = track.FilePath;
			_crossfadeTriggered = false;
			App.AudioEngine.CancelFade();
			App.NowPlayingId = track.Id;
			App.NowPlayingFilePath = track.FilePath;
			UpdateIsPlayingGlobally(track.FilePath, isPlaying: true);
			((ContentControl)NowPlayingTitle).Content = track.Title;
			UpdateMiniPlayerUI(track);
			App.DiscordRpc?.UpdatePresence(track, isPlaying: true);
			((ContentControl)NowPlayingArtist).Content = track.Artist;
			((ContentControl)NowPlayingAlbum).Content = track.Album;
			_ = UpdateNowPlayingFavoriteIconAsync(track.Id);
			PlayPauseIcon.Glyph = "\ue769";
			((FrameworkElement)PlayPauseIcon).Margin = new Thickness(0.0);
			if (MiniPlayPauseIcon != (FontIcon)null)
			{
				((FrameworkElement)MiniPlayPauseIcon).Margin = new Thickness(0.0);
			}
			if (MiniPlayPauseIcon != (FontIcon)null)
			{
				MiniPlayPauseIcon.Glyph = "\ue769";
			}
			UpdateTaskbarPlayPauseIcon(true);
			((RangeBase)ProgressSlider).Maximum = Math.Max(track.Duration.TotalSeconds, 1.0);
			((RangeBase)ProgressSlider).Value = 0.0;
			if (MiniProgressSlider != (Slider)null)
			{
				((RangeBase)MiniProgressSlider).Maximum = ((RangeBase)ProgressSlider).Maximum;
				((RangeBase)MiniProgressSlider).Value = 0.0;
			}
			TotalTimeText.Text = FormatTime(track.Duration);
			CurrentTimeText.Text = "0:00";
			ShowPlayerBar();
			_libraryPageInstance?.SetNowPlayingId(track.Id, track.FilePath);
			object content = ((ContentControl)ContentFrame).Content;
			if (content is PlaylistDetailPage pdp)
			{
				pdp.SetNowPlayingId(track.Id, track.FilePath);
			}
			content = ((ContentControl)ContentFrame).Content;
			if (content is PlaylistsPage pp)
			{
				pp.SetNowPlayingId(track.Id, track.FilePath);
			}
			content = ((ContentControl)ContentFrame).Content;
			if (content is QueuePage qp)
			{
				qp.SetNowPlayingId(track.Id, track.FilePath);
			}
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				UpdateUpNextPanel();
			});
			content = ((ContentControl)ContentFrame).Content;
			if (content is AlbumsPage ap)
			{
				ap.SetNowPlayingId(track.Id, track.FilePath);
			}
			content = ((ContentControl)ContentFrame).Content;
			if (content is ArtistsPage arp)
			{
				arp.SetNowPlayingId(track.Id, track.FilePath);
			}
			content = ((ContentControl)ContentFrame).Content;
			if (content is GenresPage gp)
			{
				gp.SetNowPlayingId(track.Id, track.FilePath);
			}
			content = ((ContentControl)ContentFrame).Content;
			if (content is FoldersPage fp)
			{
				fp.SetNowPlayingId(track.Id, track.FilePath);
			}
			content = ((ContentControl)ContentFrame).Content;
			if (content is NowPlayingPage npp)
			{
				npp.UpdateTrackInfo(track);
			}
			if (App.Settings.Current.AutoOpenNowPlaying)
			{
				NavigateToNowPlaying(track, isGoingBack);
			}
			if (!string.IsNullOrEmpty(track.CoverArtPath))
			{
				SetPlayerCover(track.CoverArtPath);
			}
			else
			{
				ClearPlayerCover();
				if (settings.AutoFetchMissingCovers)
				{
					FindMissingCoverAsync(track);
				}
			}
			UpdatePlayerBarColorAsync(track);
			_lrcLines.Clear();
			_lrcCurrentIndex = -1;
			UpdateLyricsOverlayTrackInfo(track);
			if (settings.LyricsEnabled)
			{
				if (!string.IsNullOrEmpty(track.Lyrics))
				{
					LoadLyricsAsync(track.Lyrics, track.LyricsSynced);
				}
				else
				{
					FindMissingLyricsAsync(track);
				}
			}
			try
			{
				double gainToApply = (settings.NormalizationEnabled ? track.NormalizationGainDb : 0.0);
				ContentDialog downloadDialog = null;
				TextBlock statusText = null;
				int crossfadeMs = 0;
				if (App.Settings.Current.EnableCrossfade && (int)App.AudioEngine.State == 1)
				{
					crossfadeMs = App.Settings.Current.CrossfadeDurationSeconds;
				}
				await App.AudioEngine.PlayAsync(track, settings.ExclusiveAudioMode, gainToApply, delegate(string line)
				{
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						if (downloadDialog == (ContentDialog)null)
						{
							statusText = new TextBlock
							{
								Text = line,
								TextWrapping = TextWrapping.Wrap
							};
							StackPanel val = new StackPanel
							{
								Spacing = 10.0
							};
							((Panel)val).Children.Add((UIElement)new ProgressRing
							{
								IsActive = true,
								HorizontalAlignment = HorizontalAlignment.Center
							});
							((Panel)val).Children.Add((UIElement)statusText);
							downloadDialog = new ContentDialog
							{
								Title = "Installation des codecs audio (FFmpeg)...",
								Content = val,
								XamlRoot = ((UIElement)ContentFrame).XamlRoot
							};
							downloadDialog.ShowAsync();
						}
						else
						{
							statusText.Text = line;
						}
					});
				}, crossfadeMs);
				if (downloadDialog != (ContentDialog)null)
				{
					downloadDialog.Hide();
				}
				TimeSpan actualDuration = App.AudioEngine.TotalDuration;
				if (actualDuration.TotalSeconds > 0.0 && Math.Abs((actualDuration - track.Duration).TotalSeconds) > 1.0)
				{
					track.Duration = actualDuration;
					((RangeBase)ProgressSlider).Maximum = Math.Max(actualDuration.TotalSeconds, 1.0);
					if (MiniProgressSlider != (Slider)null)
					{
						((RangeBase)MiniProgressSlider).Maximum = ((RangeBase)ProgressSlider).Maximum;
					}
					TotalTimeText.Text = FormatTime(actualDuration);
					App.Cache.UpsertTrackAsync(track);
				}
			}
			catch (Exception ex)
			{
				ContentDialog errorDialog = new ContentDialog
				{
					Title = "Lecture impossible",
					Content = "Impossible de lire ce fichier : " + ex.Message,
					CloseButtonText = "OK",
					XamlRoot = ((UIElement)ContentFrame).XamlRoot
				};
				errorDialog.ShowAsync();
				return;
			}
			App.PlayStats.RecordPlay(track.Id);
			if (_queueIndex >= 0 && _queueIndex + 1 < _queue.Count)
			{
				Track next = _queue[_queueIndex + 1];
				App.AudioEngine.PrewarmOpus(next.FilePath, next.Duration);
			}
			if (!settings.NormalizationEnabled || track.IsAnalyzed)
			{
				return;
			}
			Task.Run(async delegate
			{
				try
				{
					double gain = await App.Normalization.AnalyzeAsync(track.FilePath);
					track.NormalizationGainDb = gain;
					track.IsAnalyzed = true;
					App.Cache.UpsertTrackAsync(track);
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						if (_nowPlayingId == track.Id)
						{
							App.AudioEngine.SetNormalizationGain(gain);
						}
					});
				}
				catch
				{
					track.NormalizationGainDb = 0.0;
				}
			});
		}

		private void SetPlayerCover(string path)
		{
			try
			{
				BitmapImage bitmap = CoverCacheService.GetBitmap(path, 140);
				if (bitmap != (BitmapImage)null)
				{
					NowPlayingCoverBorder.Background = (Brush)new ImageBrush
					{
						ImageSource = (ImageSource)bitmap,
						Stretch = Stretch.UniformToFill
					};
				}
				else
				{
					NowPlayingCoverBorder.Background = (Brush)new ImageBrush
					{
						ImageSource = (ImageSource)new BitmapImage(new Uri(path)),
						Stretch = Stretch.UniformToFill
					};
				}
				((UIElement)NowPlayingPlaceholderIcon).Visibility = Visibility.Collapsed;
				NowPlayingCover.Source = null;
			}
			catch
			{
			}
		}

		private void ClearPlayerCover()
		{
			NowPlayingCoverBorder.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
			((UIElement)NowPlayingPlaceholderIcon).Visibility = Visibility.Visible;
			NowPlayingCover.Source = null;
		}

		private string SanitizeUnknown(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				return "";
			}
			string text = input.ToLowerInvariant();
			if (text.Contains("inconnu") || text.Contains("unknown"))
			{
				return "";
			}
			return input;
		}

		private async Task FindMissingCoverAsync(Track track)
		{
			string sArtist = SanitizeUnknown(track.Artist);
			string sAlbum = SanitizeUnknown(track.Album);
			string sTitle = SanitizeUnknown(track.Title);
			if (string.IsNullOrEmpty(sArtist) && string.IsNullOrEmpty(sAlbum) && string.IsNullOrEmpty(sTitle))
			{
				return;
			}
			string path = await App.CoverArt.FindAndCacheCoverAsync(track.Id, sArtist, sAlbum, track.Title, track.FilePath);
			if (path == null)
			{
				return;
			}
			track.CoverArtPath = path;
			await App.Cache.UpdateTrackAsync(track);
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				if (_currentIndex >= 0 && _library[_currentIndex].Id == track.Id)
				{
					SetPlayerCover(path);
				}
			});
			UpdatePlayerBarColorAsync(track);
		}

		private async Task FindMissingLyricsAsync(Track track)
		{
			// Paroles locales d'abord (tags embarqués, sidecar .lrc/.txt, sous-dossier Lyrics),
			// avant tout appel réseau : évite une recherche web inutile quand l'utilisateur gère
			// déjà ses paroles localement.
			var local = await Task.Run(() => Resona.Services.LocalLyricsService.FindLocalLyrics(track.FilePath));
			if (local != null && !string.IsNullOrWhiteSpace(local.Text))
			{
				track.Lyrics = local.Text;
				track.LyricsSynced = local.IsSynced;
				await App.Cache.UpdateTrackAsync(track);
				((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
				{
					LoadLyricsAsync(track.Lyrics, track.LyricsSynced);
				});
				return;
			}

			string sArtist = SanitizeUnknown(track.Artist);
			string sTitle = SanitizeUnknown(track.Title);
			string sAlbum = SanitizeUnknown(track.Album);
			if (string.IsNullOrEmpty(sArtist) && string.IsNullOrEmpty(sTitle))
			{
				((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
				{
					ShowPlainLyrics(Strings.Current.IsFr ? "Paroles introuvables." : "Lyrics not found.");
				});
				return;
			}
			CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
			Task<LyricsResult> lyricsTask = App.Lyrics.SearchAsync(sArtist, sTitle, sAlbum, track.Duration);
			Task<AutoTagResult?> autoTagTask = AutoTagService.LookupAsync(sArtist, sTitle, track.Duration, track.FilePath);
			LyricsResult result = new LyricsResult();
			try
			{
				result = await lyricsTask.WaitAsync(cts.Token);
			}
			catch
			{
				result = new LyricsResult();
			}
			AutoTagResult autoTag = null;
			try
			{
				autoTag = await autoTagTask.WaitAsync(cts.Token);
			}
			catch
			{
			}
			if (!result.Found && autoTag != null)
			{
				string altArtist = autoTag.Artist ?? track.Artist;
				string altTitle = autoTag.Title ?? track.Title;
				cts = new CancellationTokenSource(TimeSpan.FromSeconds(8.0));
				try
				{
					result = await App.Lyrics.SearchAsync(altArtist, altTitle, autoTag.Album ?? track.Album, track.Duration).WaitAsync(cts.Token);
				}
				catch
				{
				}
				if (!result.Found && autoTag.Album != null)
				{
					cts = new CancellationTokenSource(TimeSpan.FromSeconds(8.0));
					try
					{
						result = await App.Lyrics.SearchAsync(altArtist, altTitle, autoTag.Album, track.Duration).WaitAsync(cts.Token);
					}
					catch
					{
					}
				}
			}
			track.Lyrics = (result.Found ? (result.SyncedLyrics ?? result.PlainLyrics) : null);
			track.LyricsSynced = result.SyncedLyrics != null;
			if (result.Found)
			{
				await App.Cache.UpdateTrackAsync(track);
			}
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				if (_currentIndex >= 0 && !(_library[_currentIndex].Id != track.Id))
				{
					if (result.Found)
					{
						LoadLyricsAsync(track.Lyrics, track.LyricsSynced);
					}
					else
					{
						ShowPlainLyrics(Strings.Current.IsFr ? "Aucune parole trouvée pour ce morceau." : "No lyrics found for this track.");
					}
				}
			});
		}

		public void ApplyNormalizationSetting()
		{
			if (_currentIndex >= 0)
			{
				Track track = _library[_currentIndex];
				App.AudioEngine.SetNormalizationGain(App.Settings.Current.NormalizationEnabled ? track.NormalizationGainDb : 0.0);
			}
		}

		public void InvalidateNormalizationAndReanalyze()
		{
			_reanalyzeCts?.Cancel();
			_reanalyzeCts = new CancellationTokenSource();
			CancellationToken token = _reanalyzeCts.Token;
			DispatcherQueueHandler val = default(DispatcherQueueHandler);
			Task.Run(async delegate
			{
				await Task.Delay(1500, token);
				if (!token.IsCancellationRequested)
				{
					DispatcherQueue dispatcherQueue = ((Window)this).DispatcherQueue;
					DispatcherQueueHandler obj = val;
					if (obj == null)
					{
						DispatcherQueueHandler val2 = delegate
						{
							AnalyzeWholeLibraryInBackground();
						};
						DispatcherQueueHandler val3 = val2;
						val = val2;
						obj = val3;
					}
					dispatcherQueue.TryEnqueue(obj);
				}
			});
		}

		public void AnalyzeWholeLibraryInBackground()
		{
			Task.Run(async delegate
			{
				List<Track> tracksToAnalyze = _library.Where((Track t) => !t.IsAnalyzed).ToList();
				if (tracksToAnalyze.Count == 0)
				{
					return;
				}
				Track current = tracksToAnalyze.FirstOrDefault((Track t) => t.Id == _nowPlayingId);
				if (current != null)
				{
					tracksToAnalyze.Remove(current);
					tracksToAnalyze.Insert(0, current);
				}
				ContentDialog loadingDialog = null;
				TextBlock statusText = null;
				if (tracksToAnalyze.Count > 10)
				{
					((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
					{
						statusText = new TextBlock
						{
							Text = (Strings.Current.IsFr ? $"Analyse de {tracksToAnalyze.Count} titres..." : $"Analyzing {tracksToAnalyze.Count} tracks..."),
							HorizontalAlignment = HorizontalAlignment.Center
						};
						StackPanel val = new StackPanel
						{
							Spacing = 10.0
						};
						((Panel)val).Children.Add((UIElement)new ProgressRing
						{
							IsActive = true,
							HorizontalAlignment = HorizontalAlignment.Center
						});
						((Panel)val).Children.Add((UIElement)statusText);
						loadingDialog = new ContentDialog
						{
							Title = (Strings.Current.IsFr ? "Normalisation Audio" : "Audio Normalization"),
							Content = val,
							XamlRoot = ((UIElement)ContentFrame).XamlRoot
						};
						loadingDialog.ShowAsync();
					});
				}
				int processed = 0;
				SemaphoreSlim dbLock = new SemaphoreSlim(1, 1);
				try
				{
					await Parallel.ForEachAsync(tracksToAnalyze, new ParallelOptions
					{
						MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 3)
					}, async delegate(Track track, CancellationToken ct)
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
							if (statusText != (TextBlock)null)
							{
								((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
								{
									statusText.Text = (Strings.Current.IsFr ? "Analyse... " : "Analyzing... ") + $"{currentProcessed}/{tracksToAnalyze.Count}";
								});
							}
							((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
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
					if (loadingDialog != (ContentDialog)null)
					{
						((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
						{
							loadingDialog.Hide();
						});
					}
				}
				finally
				{
					if (dbLock != null)
					{
						((IDisposable)dbLock).Dispose();
					}
				}
			});
		}

		private double GetBaseVolumeSliderWidth()
		{
			bool lyricsEnabled = App.Settings.Current.LyricsEnabled;
			bool enableUpNextPanel = App.Settings.Current.EnableUpNextPanel;
			if (lyricsEnabled)
			{
				return enableUpNextPanel ? 100 : 130;
			}
			return enableUpNextPanel ? 130 : 160;
		}

		private void UpdateVolumeSliderWidth()
		{
			if (!(VolumeSlider == (Slider)null))
			{
				double baseVolumeSliderWidth = GetBaseVolumeSliderWidth();
				double num = Math.Clamp((double)((Window)this).AppWindow.Size.Width * 0.1, 80.0, 150.0);
				((FrameworkElement)VolumeSlider).Width = Math.Max(baseVolumeSliderWidth, num + 30.0);
			}
		}

		private void UpdatePlaybackControlsCentering()
		{
			if (!(PlaybackControlsPanel == (StackPanel)null))
			{
				double num = 0.0;
				if (App.Settings.Current.LyricsEnabled)
				{
					num += 42.0;
				}
				if (App.Settings.Current.EnableUpNextPanel)
				{
					num += 42.0;
				}
				if (App.Settings.Current.EnableMiniPlayerButton)
				{
					num += 42.0;
				}
                if (App.Settings.Current.EnableFavoriteButton)
                {
                    num += 42.0;
                }
                if (App.Settings.Current.EnableEqualizerQuickButton)
                {
                    num += 42.0;
                }
				double num2 = num / 2.0;
				double num3 = ((Window)this).AppWindow.Size.Width;
				double num4 = Math.Max(1.0, 700.0);
				double num5 = Math.Clamp((num3 - 1200.0) / num4, 0.0, 1.0);
				double num6 = num2 * (1.0 - num5);
				((FrameworkElement)PlaybackControlsPanel).Margin = new Thickness(0.0, 0.0, num6, 0.0);
			}
		}

		public void ApplyLyricsButtonVisibility()
		{
			bool lyricsEnabled = App.Settings.Current.LyricsEnabled;
			bool enableUpNextPanel = App.Settings.Current.EnableUpNextPanel;
			((UIElement)LyricsButton).Visibility = (lyricsEnabled ? Visibility.Visible : Visibility.Collapsed);
			((UIElement)UpNextToggleBtn).Visibility = (enableUpNextPanel ? Visibility.Visible : Visibility.Collapsed);
			UpdateVolumeSliderWidth();
			UpdatePlaybackControlsCentering();
			if (!lyricsEnabled)
			{
				CloseLyricsOverlay();
			}
		}

		private async void LoadLyricsAsync(string lyrics, bool isSynced)
		{
			_lrcLines.Clear();
			_lrcCurrentIndex = -1;
			if (isSynced)
			{
				List<LrcLine> rawLines = new List<LrcLine>();
				string[] array = lyrics.Split('\n');
				foreach (string rawLine in array)
				{
					string line = rawLine.Trim();
					if (line.Length < 7 || line[0] != '[')
					{
						continue;
					}
					int close = line.IndexOf(']');
					if (close >= 0)
					{
						string timePart = line.Substring(1, close - 1);
						string text = line.Substring(close + 1).Trim();
						if (TryParseLrcTime(timePart, out var ts))
						{
							rawLines.Add(new LrcLine(ts, text));
						}
					}
				}
				rawLines.Sort((LrcLine a, LrcLine b) => a.Time.CompareTo(b.Time));
				if (App.Settings.Current.TranslateLyricsEnabled)
				{
					string combined = string.Join(" \n ", rawLines.Select((LrcLine l) => l.Text));
					string[] translatedLines = (await LyricsTranslatorService.TranslateTextAsync(combined)).Split(new string[2] { " \n ", "\n" }, StringSplitOptions.None);
					for (int i2 = 0; i2 < rawLines.Count && i2 < translatedLines.Length; i2++)
					{
						string orig = rawLines[i2].Text;
						string trans = translatedLines[i2].Trim();
						_lrcLines.Add(new LrcLine(rawLines[i2].Time, (string.IsNullOrWhiteSpace(trans) || orig == trans) ? orig : (orig + "\n— " + trans)));
					}
				}
				else
				{
					_lrcLines.AddRange(rawLines);
				}
				if (_lrcLines.Count > 0)
				{
					ShowSyncedLyricsUI();
				}
				else
				{
					ShowPlainLyrics(lyrics);
				}
				return;
			}
			if (App.Settings.Current.TranslateLyricsEnabled)
			{
				string translated = await LyricsTranslatorService.TranslateTextAsync(lyrics);
				string[] origLines = lyrics.Split('\n');
				string[] transLines = translated.Split('\n');
				StringBuilder sb = new StringBuilder();
				for (int i3 = 0; i3 < origLines.Length; i3++)
				{
					string orig2 = origLines[i3].TrimEnd();
					string trans2 = ((i3 < transLines.Length) ? transLines[i3].TrimEnd() : "");
					if (string.IsNullOrWhiteSpace(orig2))
					{
						sb.AppendLine();
					}
					else
					{
						sb.AppendLine((string.IsNullOrWhiteSpace(trans2) || orig2 == trans2) ? orig2 : (orig2 + "\n— " + trans2));
					}
				}
				lyrics = sb.ToString();
			}
			ShowPlainLyrics(lyrics);
		}

		private static bool TryParseLrcTime(string s, out TimeSpan result)
		{
			result = TimeSpan.Zero;
			int num = s.IndexOf(':');
			if (num < 0)
			{
				return false;
			}
			if (!int.TryParse(s.Substring(0, num), out var result2))
			{
				return false;
			}
			string s2 = s.Substring(num + 1).Replace(':', '.');
			if (!double.TryParse(s2, NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
			{
				return false;
			}
			result = TimeSpan.FromSeconds((double)(result2 * 60) + result3);
			return true;
		}

		private void EnsureLyricsOverlayCreated()
		{
			if (_lyricsOverlay != (Grid)null)
			{
				return;
			}
			Border val = new Border
			{
				Background = (Brush)new SolidColorBrush(Color.FromArgb((byte)234, (byte)9, (byte)9, (byte)13)),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch
			};
			_lyricsTrackTitle = new TextBlock
			{
				FontSize = 13.0,
				FontWeight = FontWeights.SemiBold,
				Opacity = 0.5,
				TextTrimming = TextTrimming.CharacterEllipsis,
				MaxLines = 1,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 0.0, 2.0)
			};
			_lyricsTrackArtist = new TextBlock
			{
				FontSize = 11.0,
				Opacity = 0.3,
				TextTrimming = TextTrimming.CharacterEllipsis,
				MaxLines = 1,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_lyricsLinePrev = new TextBlock
			{
				FontSize = 17.0,
				Opacity = 0.22,
				TextWrapping = TextWrapping.Wrap,
				TextAlignment = TextAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
				MaxWidth = 540.0,
				Margin = new Thickness(0.0, 0.0, 0.0, 18.0),
				IsHitTestVisible = false
			};
			_lyricsLineCurrent = new TextBlock
			{
				FontSize = 28.0,
				FontWeight = FontWeights.Bold,
				TextWrapping = TextWrapping.Wrap,
				TextAlignment = TextAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
				MaxWidth = 540.0,
				Margin = new Thickness(0.0, 0.0, 0.0, 18.0),
				Foreground = (Brush)Application.Current.Resources["AppAccentBrush"],
				IsHitTestVisible = false
			};
			_lyricsLineNext = new TextBlock
			{
				FontSize = 17.0,
				Opacity = 0.22,
				TextWrapping = TextWrapping.Wrap,
				TextAlignment = TextAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
				MaxWidth = 540.0,
				IsHitTestVisible = false
			};
			_lyricsSyncedPanel = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				MaxWidth = 620.0,
				Padding = new Thickness(32.0, 0.0, 32.0, 0.0),
				Visibility = Visibility.Visible
			};
			((Panel)_lyricsSyncedPanel).Children.Add((UIElement)_lyricsLinePrev);
			((Panel)_lyricsSyncedPanel).Children.Add((UIElement)_lyricsLineCurrent);
			((Panel)_lyricsSyncedPanel).Children.Add((UIElement)_lyricsLineNext);
			_lyricsPlainText = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				FontSize = 16.0,
				LineHeight = 28.0,
				TextAlignment = TextAlignment.Center,
				Opacity = 0.75,
				HorizontalAlignment = HorizontalAlignment.Center,
				MaxWidth = 540.0,
				IsHitTestVisible = false
			};
			_lyricsGoogleBtn = new HyperlinkButton
			{
				Content = "Chercher sur Google",
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 16.0, 0.0, 0.0),
				Visibility = Visibility.Collapsed
			};
			((ButtonBase)_lyricsGoogleBtn).Click += (RoutedEventHandler)delegate
			{
				if (_currentIndex >= 0 && _currentIndex < _library.Count)
				{
					Track track = _library[_currentIndex];
					Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.google.com/search?q=" + Uri.EscapeDataString(track.Artist + " " + track.Title + (Strings.Current.IsFr ? " paroles" : " lyrics"))));
				}
			};
			StackPanel val2 = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			((Panel)val2).Children.Add((UIElement)_lyricsPlainText);
			((Panel)val2).Children.Add((UIElement)_lyricsGoogleBtn);
			_lyricsPlainScroll = new ScrollViewer
			{
				Content = val2,
				Padding = new Thickness(32.0, 24.0, 32.0, 40.0),
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Visibility = Visibility.Collapsed,
				IsHitTestVisible = true
			};
			StackPanel val3 = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 32.0, 0.0, 12.0)
			};
			((Panel)val3).Children.Add((UIElement)_lyricsTrackTitle);
			((Panel)val3).Children.Add((UIElement)_lyricsTrackArtist);
			StackPanel val4 = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			((Panel)val4).Children.Add((UIElement)_lyricsSyncedPanel);
			((Panel)val4).Children.Add((UIElement)_lyricsPlainScroll);
			Grid val5 = new Grid
			{
				VerticalAlignment = VerticalAlignment.Stretch
			};
			val5.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			val5.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			Grid.SetRow((FrameworkElement)val3, 0);
			Grid.SetRow((FrameworkElement)val4, 1);
			((Panel)val5).Children.Add((UIElement)val3);
			((Panel)val5).Children.Add((UIElement)val4);
			_lyricsOverlay = new Grid
			{
				Visibility = Visibility.Collapsed,
				Opacity = 0.0
			};
			Grid.SetRow((FrameworkElement)_lyricsOverlay, 0);
			Grid.SetRowSpan((FrameworkElement)_lyricsOverlay, 3);
			Canvas.SetZIndex((UIElement)_lyricsOverlay, 90);
			((UIElement)_lyricsOverlay).Tapped += (TappedEventHandler)delegate
			{
				CloseLyricsOverlay();
			};
			((Panel)_lyricsOverlay).Children.Add((UIElement)val);
			((Panel)_lyricsOverlay).Children.Add((UIElement)val5);
			((Panel)RootGrid).Children.Add((UIElement)_lyricsOverlay);
		}

		private void ShowSyncedLyricsUI()
		{
			EnsureLyricsOverlayCreated();
			if (_lyricsPlainScroll != (ScrollViewer)null)
			{
				((UIElement)_lyricsPlainScroll).Visibility = Visibility.Collapsed;
			}
			if (_lyricsSyncedPanel != (StackPanel)null)
			{
				((UIElement)_lyricsSyncedPanel).Visibility = Visibility.Visible;
			}
			UpdateSyncedLyricsDisplay(-1);
		}

		private void ShowPlainLyrics(string text)
		{
			EnsureLyricsOverlayCreated();
			if (_lyricsPlainText != (TextBlock)null)
			{
				_lyricsPlainText.Text = text;
			}
			if (_lyricsLinePrev != (TextBlock)null)
			{
				_lyricsLinePrev.Text = "";
			}
			if (_lyricsLineCurrent != (TextBlock)null)
			{
				_lyricsLineCurrent.Text = "";
			}
			if (_lyricsLineNext != (TextBlock)null)
			{
				_lyricsLineNext.Text = "";
			}
			if (_lyricsPlainScroll != (ScrollViewer)null)
			{
				((UIElement)_lyricsPlainScroll).Visibility = Visibility.Visible;
			}
			if (_lyricsSyncedPanel != (StackPanel)null)
			{
				((UIElement)_lyricsSyncedPanel).Visibility = Visibility.Collapsed;
			}
			if (_lyricsGoogleBtn != (HyperlinkButton)null)
			{
				((UIElement)_lyricsGoogleBtn).Visibility = (Visibility)((!text.StartsWith("Aucune parole") && !text.StartsWith("No lyrics")) ? 1 : 0);
			}
		}

		private void UpdateLyricsOverlayTrackInfo(Track track)
		{
			EnsureLyricsOverlayCreated();
			if (_lyricsTrackTitle != (TextBlock)null)
			{
				_lyricsTrackTitle.Text = track.Title;
			}
			if (_lyricsTrackArtist != (TextBlock)null)
			{
				_lyricsTrackArtist.Text = track.Artist;
			}
			ShowPlainLyrics(Strings.Current.IsFr ? "Recherche des paroles..." : "Searching for lyrics...");
		}

		private void UpdateSyncedLyricsDisplay(int newIndex)
		{
			if (_lyricsLineCurrent == (TextBlock)null)
			{
				return;
			}
			string prev = ((newIndex > 0) ? _lrcLines[newIndex - 1].Text : "");
			string curr = ((newIndex >= 0 && newIndex < _lrcLines.Count) ? _lrcLines[newIndex].Text : "");
			string next = ((newIndex + 1 < _lrcLines.Count) ? _lrcLines[newIndex + 1].Text : "");
			if (_lyricsLineCurrent.Text == curr)
			{
				_lyricsLinePrev.Text = prev;
				_lyricsLineNext.Text = next;
				return;
			}
			Storyboard val = new Storyboard();
			DoubleAnimation val2 = new DoubleAnimation
			{
				To = 0.0,
				Duration = TimeSpan.FromMilliseconds(120.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)_lyricsLineCurrent);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
			val.Children.Add((Timeline)(object)val2);
			((Timeline)val).Completed += delegate
			{
				_lyricsLinePrev.Text = prev;
				_lyricsLineCurrent.Text = curr;
				_lyricsLineNext.Text = next;
				if (!(((UIElement)_lyricsSyncedPanel).RenderTransform is TranslateTransform))
				{
					((UIElement)_lyricsSyncedPanel).RenderTransform = (Transform)new TranslateTransform();
				}
				Storyboard val3 = new Storyboard();
				DoubleAnimation val4 = new DoubleAnimation
				{
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(300.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)_lyricsLineCurrent);
				Storyboard.SetTargetProperty((Timeline)(object)val4, "Opacity");
				val3.Children.Add((Timeline)(object)val4);
				DoubleAnimation val5 = new DoubleAnimation
				{
					To = 0.22,
					Duration = TimeSpan.FromMilliseconds(300.0)
				};
				Storyboard.SetTarget((Timeline)(object)val5, (DependencyObject)_lyricsLinePrev);
				Storyboard.SetTargetProperty((Timeline)(object)val5, "Opacity");
				val3.Children.Add((Timeline)(object)val5);
				DoubleAnimation val6 = new DoubleAnimation
				{
					To = 0.22,
					Duration = TimeSpan.FromMilliseconds(300.0)
				};
				Storyboard.SetTarget((Timeline)(object)val6, (DependencyObject)_lyricsLineNext);
				Storyboard.SetTargetProperty((Timeline)(object)val6, "Opacity");
				val3.Children.Add((Timeline)(object)val6);
				DoubleAnimation val7 = new DoubleAnimation
				{
					From = 20.0,
					To = 0.0,
					Duration = TimeSpan.FromMilliseconds(300.0),
					EasingFunction = (EasingFunctionBase)new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val7, (DependencyObject)_lyricsSyncedPanel);
				Storyboard.SetTargetProperty((Timeline)(object)val7, "(UIElement.RenderTransform).(TranslateTransform.Y)");
				val3.Children.Add((Timeline)(object)val7);
				val3.Begin();
			};
			val.Begin();
		}

		public void TickSyncedLyrics(TimeSpan position)
		{
			if (_lrcLines.Count != 0 && _lyricsOverlayOpen)
			{
				int num = -1;
				for (int i = 0; i < _lrcLines.Count && _lrcLines[i].Time <= position; i++)
				{
					num = i;
				}
				if (num != _lrcCurrentIndex)
				{
					_lrcCurrentIndex = num;
					UpdateSyncedLyricsDisplay(num);
				}
			}
		}

		private void OpenLyricsOverlay()
		{
			EnsureLyricsOverlayCreated();
			_lyricsOverlayOpen = true;
			((UIElement)_lyricsOverlay).Visibility = Visibility.Visible;
			Storyboard val = new Storyboard();
			DoubleAnimation val2 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(200.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
			val.Children.Add((Timeline)(object)val2);
			if (!(((UIElement)_lyricsOverlay).RenderTransform is CompositeTransform))
			{
				((UIElement)_lyricsOverlay).RenderTransform = (Transform)new CompositeTransform();
			}
			((UIElement)_lyricsOverlay).RenderTransformOrigin = new Point(0.5, 0.5);
			CompositeTransform val3 = (CompositeTransform)((UIElement)_lyricsOverlay).RenderTransform;
			val3.ScaleX = 0.95;
			val3.ScaleY = 0.95;
			val3.TranslateY = 20.0;
			DoubleAnimation val4 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(400.0),
				EasingFunction = (EasingFunctionBase)new ExponentialEase
				{
					EasingMode = EasingMode.EaseOut,
					Exponent = 4.0
				}
			};
			DoubleAnimation val5 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(400.0),
				EasingFunction = (EasingFunctionBase)new ExponentialEase
				{
					EasingMode = EasingMode.EaseOut,
					Exponent = 4.0
				}
			};
			DoubleAnimation val6 = new DoubleAnimation
			{
				To = 0.0,
				Duration = TimeSpan.FromMilliseconds(400.0),
				EasingFunction = (EasingFunctionBase)new ExponentialEase
				{
					EasingMode = EasingMode.EaseOut,
					Exponent = 4.0
				}
			};
			Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val4, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
			Storyboard.SetTarget((Timeline)(object)val5, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val5, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
			Storyboard.SetTarget((Timeline)(object)val6, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val6, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
			val.Children.Add((Timeline)(object)val4);
			val.Children.Add((Timeline)(object)val5);
			val.Children.Add((Timeline)(object)val6);
			val.Begin();
		}

		private void CloseLyricsOverlay()
		{
			if (_lyricsOverlay == (Grid)null)
			{
				return;
			}
			if (!(((UIElement)_lyricsOverlay).RenderTransform is CompositeTransform))
			{
				((UIElement)_lyricsOverlay).RenderTransform = (Transform)new CompositeTransform();
			}
			_lyricsOverlayOpen = false;
			Storyboard val = new Storyboard();
			DoubleAnimation val2 = new DoubleAnimation
			{
				To = 0.0,
				Duration = TimeSpan.FromMilliseconds(150.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			DoubleAnimation val3 = new DoubleAnimation
			{
				To = 10.0,
				Duration = TimeSpan.FromMilliseconds(150.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			DoubleAnimation val4 = new DoubleAnimation
			{
				To = 0.98,
				Duration = TimeSpan.FromMilliseconds(150.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			DoubleAnimation val5 = new DoubleAnimation
			{
				To = 0.98,
				Duration = TimeSpan.FromMilliseconds(150.0),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
			Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val3, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
			Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val4, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
			Storyboard.SetTarget((Timeline)(object)val5, (DependencyObject)_lyricsOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val5, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
			val.Children.Add((Timeline)(object)val2);
			val.Children.Add((Timeline)(object)val3);
			val.Children.Add((Timeline)(object)val4);
			val.Children.Add((Timeline)(object)val5);
			((Timeline)val).Completed += delegate
			{
				if (!_lyricsOverlayOpen)
				{
					((UIElement)_lyricsOverlay).Visibility = Visibility.Collapsed;
				}
			};
			val.Begin();
		}

		private void VolumeMuteButton_Click(object sender, RoutedEventArgs e)
		{
			if (((RangeBase)VolumeSlider).Value > 0.0)
			{
				_previousVolume = ((RangeBase)VolumeSlider).Value;
				((RangeBase)VolumeSlider).Value = 0.0;
			}
			else
			{
				((RangeBase)VolumeSlider).Value = ((_previousVolume > 0.0) ? _previousVolume : 50.0);
			}
		}

		private void VolumeSlider_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
		{
			int mouseWheelDelta = e.GetCurrentPoint((UIElement)VolumeSlider).Properties.MouseWheelDelta;
			double num = ((mouseWheelDelta > 0) ? 5 : (-5));
			double val = ((RangeBase)VolumeSlider).Value + num;
			((RangeBase)VolumeSlider).Value = Math.Max(0.0, Math.Min(100.0, val));
			e.Handled = true;
		}

		private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			if (sender == MiniVolumeSlider && VolumeSlider != (Slider)null)
			{
				((RangeBase)VolumeSlider).Value = e.NewValue;
			}
			if (sender == VolumeSlider && MiniVolumeSlider != (Slider)null)
			{
				((RangeBase)MiniVolumeSlider).Value = e.NewValue;
			}
			if (VolumeIcon != (FontIcon)null)
			{
				if (e.NewValue <= 0.0)
				{
					VolumeIcon.Glyph = "\ue74f";
				}
				else if (e.NewValue < 33.0)
				{
					VolumeIcon.Glyph = "\ue992";
				}
				else if (e.NewValue < 66.0)
				{
					VolumeIcon.Glyph = "\ue993";
				}
				else
				{
					VolumeIcon.Glyph = "\ue767";
				}
				if (MiniVolumeIcon != (FontIcon)null)
				{
					MiniVolumeIcon.Glyph = VolumeIcon.Glyph;
				}
			}
			if (App.AudioEngine != null)
			{
				App.AudioEngine.SetUserVolume((float)(((RangeBase)VolumeSlider).Value / 100.0));
				if (App.Settings != null && App.Settings.Current != null)
				{
					App.Settings.Current.Volume = ((RangeBase)VolumeSlider).Value;
					App.Settings.SaveAsync();
				}
			}
		}

		private void LyricsButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			HideInfoOverlay();
			if (_lyricsOverlayOpen)
			{
				CloseLyricsOverlay();
			}
			else
			{
				OpenLyricsOverlay();
			}
			LyricsButton.Background = (Brush)new SolidColorBrush(Color.FromArgb((byte)30, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}

		private void LyricsButton_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			AnimationHelper.ApplyBouncyScale((UIElement)LyricsButton, 1f);
			LyricsButton.Background = (Brush)new SolidColorBrush(Colors.Transparent);
		}

		private void LyricsButton_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			LyricsButton.Background = (Brush)new SolidColorBrush(Color.FromArgb((byte)15, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}

		private void LyricsButton_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			LyricsButton.Background = (Brush)new SolidColorBrush(Color.FromArgb((byte)30, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}

		public void RefreshPlaylistsPage()
		{
			_playlistsPageInstance?.RefreshAsync();
		}

		public MenuFlyout BuildTrackMenu(Track track, List<Track>? trackList = null, List<Track>? selectedTracks = null)
		{
			MenuFlyout val = new MenuFlyout();
			if (selectedTracks != null && selectedTracks.Count > 1 && selectedTracks.Contains(track))
			{
				MenuFlyoutItem item = new MenuFlyoutItem
				{
					Text = $"{(Strings.Current.IsFr ? "Sélection de" : "Selection of")} {selectedTracks.Count} {(Strings.Current.IsFr ? "titres" : "tracks")}",
					IsEnabled = false
				};
				val.Items.Add((MenuFlyoutItemBase)item);
				val.Items.Add((MenuFlyoutItemBase)new MenuFlyoutSeparator());
				MenuFlyoutItem val2 = new MenuFlyoutItem
				{
					Text = Strings.Current.CS_Ajouterlafiledatten,
					Icon = (IconElement)new FontIcon
					{
						Glyph = "\ue81e"
					}
				};
				val2.Click += (RoutedEventHandler)delegate
				{
					foreach (Track selectedTrack in selectedTracks)
					{
						AddToQueue(selectedTrack);
					}
				};
				val.Items.Add((MenuFlyoutItemBase)val2);
				MenuFlyoutSubItem val3 = new MenuFlyoutSubItem
				{
					Text = Strings.Current.CS_Ajouteruneplaylist,
					Icon = (IconElement)new FontIcon
					{
						Glyph = "\ue90b"
					}
				};
				val.Items.Add((MenuFlyoutItemBase)val3);
				LoadPlaylistSubItemsAsync(val3, selectedTracks);
				MenuFlyoutItem favMulti = new MenuFlyoutItem
				{
					Text = Strings.Current.CS_AddToFavorites,
					Icon = (IconElement)new FontIcon { Glyph = "\ue006" }
				};
				favMulti.Click += (RoutedEventHandler)async delegate
				{
					try
					{
						var playlists = await App.Cache.LoadAllPlaylistsAsync();
						var favorites = playlists.FirstOrDefault(p => p.Id == Playlist.FavoritesId)
							?? new Playlist { Id = Playlist.FavoritesId, Name = Strings.Current.IsFr ? "Favoris" : "Favorites", IsSystem = true };
						bool modified = false;
						foreach (Track selectedTrack in selectedTracks)
						{
							if (!favorites.TrackIds.Contains(selectedTrack.Id))
							{
								favorites.TrackIds.Add(selectedTrack.Id);
								modified = true;
							}
						}
						if (modified)
						{
							favorites.DateModified = DateTime.UtcNow;
							await App.Cache.UpsertPlaylistAsync(favorites);
							_playlistsPageInstance?.RefreshAsync();
							if (selectedTracks.Any(t => t.Id == App.NowPlayingId))
							{
								UpdateNowPlayingFavoriteIconVisual(true);
							}
						}
					}
					catch { }
				};
				val.Items.Add((MenuFlyoutItemBase)favMulti);
				val.Items.Add((MenuFlyoutItemBase)new MenuFlyoutSeparator());
				MenuFlyoutItem val4 = new MenuFlyoutItem
				{
					Text = Strings.Current.CS_Delete,
					Icon = (IconElement)new FontIcon
					{
						Glyph = "\ue74d"
					},
					Foreground = (Brush)new SolidColorBrush(Colors.IndianRed)
				};
				val4.Click += (RoutedEventHandler)async delegate
				{
					await ShowDeleteTracksDialogAsync(selectedTracks);
				};
				val.Items.Add((MenuFlyoutItemBase)val4);
				return val;
			}
			MenuFlyoutItem val5 = new MenuFlyoutItem
			{
				Text = Strings.Current.CS_Play,
				Icon = (IconElement)new FontIcon
				{
					Glyph = "\ue768"
				}
			};
			val5.Click += (RoutedEventHandler)delegate
			{
				PlayTrack(track, trackList ?? _library);
			};
			val.Items.Add((MenuFlyoutItemBase)val5);
			MenuFlyoutItem val6 = new MenuFlyoutItem
			{
				Text = Strings.Current.CS_Ajouterlafiledatten,
				Icon = (IconElement)new FontIcon
				{
					Glyph = "\ue81e"
				}
			};
			val6.Click += (RoutedEventHandler)delegate
			{
				AddToQueue(track);
			};
			val.Items.Add((MenuFlyoutItemBase)val6);
			MenuFlyoutSubItem val7 = new MenuFlyoutSubItem
			{
				Text = Strings.Current.CS_Ajouteruneplaylist,
				Icon = (IconElement)new FontIcon
				{
					Glyph = "\ue90b"
				}
			};
			val.Items.Add((MenuFlyoutItemBase)val7);
			LoadPlaylistSubItemsAsync(val7, new List<Track> { track });
			MenuFlyoutItem favSingle = new MenuFlyoutItem();
			favSingle.Click += (RoutedEventHandler)async delegate
			{
				await ToggleFavoriteAsync(track);
			};
			PopulateFavoriteMenuItemAsync(favSingle, track.Id);
			val.Items.Add((MenuFlyoutItemBase)favSingle);
			MenuFlyoutItem val8 = new MenuFlyoutItem
			{
				Text = "Autotag",
				Icon = (IconElement)new FontIcon
				{
					Glyph = "\ue943"
				}
			};
			val8.Click += (RoutedEventHandler)delegate
			{
				ShowAutoTagDialogAsync(track);
			};
			val.Items.Add((MenuFlyoutItemBase)val8);
			val.Items.Add((MenuFlyoutItemBase)new MenuFlyoutSeparator());
			MenuFlyoutItem val9 = new MenuFlyoutItem
			{
				Text = Strings.Current.CS_Delete,
				Icon = (IconElement)new FontIcon
				{
					Glyph = "\ue74d"
				},
				Foreground = (Brush)new SolidColorBrush(Colors.IndianRed)
			};
			val9.Click += (RoutedEventHandler)async delegate
			{
				await ShowDeleteTracksDialogAsync(new List<Track> { track });
			};
			val.Items.Add((MenuFlyoutItemBase)val9);
			return val;
		}

		public async Task ShowDeleteTracksDialogAsync(List<Track> tracks)
		{
			if (tracks == null || tracks.Count == 0)
			{
				return;
			}
			bool isMulti = tracks.Count > 1;
			string title = (isMulti ? Strings.Current.CS_DeleteTracksTitle : Strings.Current.CS_DeleteTrackTitle);
			string body = (isMulti ? string.Format(Strings.Current.CS_DeleteTracksBody, tracks.Count) : Strings.Current.CS_DeleteTrackBody);
			Button removeBtn = new Button
			{
				Content = Strings.Current.CS_RemoveFromApp,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			};
			Button deleteBtn = new Button
			{
				Content = Strings.Current.CS_DeleteFilePermanently,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
				Background = (Brush)new SolidColorBrush(Color.FromArgb((byte)40, (byte)220, (byte)50, (byte)50)),
				Foreground = (Brush)new SolidColorBrush(Colors.IndianRed)
			};
			Button cancelBtn = new Button
			{
				Content = Strings.Current.CS_Annuler,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			StackPanel panel = new StackPanel
			{
				Spacing = 4.0
			};
			((Panel)panel).Children.Add((UIElement)new TextBlock
			{
				Text = body,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 0.0, 0.0, 16.0)
			});
			((Panel)panel).Children.Add((UIElement)removeBtn);
			((Panel)panel).Children.Add((UIElement)deleteBtn);
			((Panel)panel).Children.Add((UIElement)cancelBtn);
			ContentDialog dialog = new ContentDialog
			{
				Title = title,
				Content = panel,
				XamlRoot = ((UIElement)ContentFrame).XamlRoot
			};
			DispatcherQueueHandler val = default(DispatcherQueueHandler);
			((ButtonBase)removeBtn).Click += (RoutedEventHandler)async delegate
			{
				dialog.Hide();
				bool wasPlaying = false;
				foreach (Track track in tracks)
				{
					await App.Cache.DeleteTrackAsync(track.Id);
					_library.Remove(track);
					if (App.NowPlayingId == track.Id)
					{
						wasPlaying = true;
					}
				}
				if (wasPlaying)
				{
					if ((int)App.AudioEngine.State == 1)
					{
						DispatcherQueue dispatcherQueue = ((Window)this).DispatcherQueue;
						DispatcherQueueHandler obj = val;
						if (obj == null)
						{
							DispatcherQueueHandler val2 = delegate
							{
								NextButton_Click(null, null);
							};
							DispatcherQueueHandler val3 = val2;
							val = val2;
							obj = val3;
						}
						dispatcherQueue.TryEnqueue(obj);
					}
					else
					{
						App.AudioEngine.Stop();
						App.NowPlayingId = null;
					}
				}
				_libraryPageInstance?.SetTracks(_library);
			};
			((ButtonBase)deleteBtn).Click += (RoutedEventHandler)async delegate
			{
				dialog.Hide();
				bool wasPlaying = false;
				foreach (Track track in tracks)
				{
					await App.Cache.DeleteTrackAsync(track.Id);
					_library.Remove(track);
					if (App.NowPlayingId == track.Id)
					{
						wasPlaying = true;
					}
					try
					{
						if (File.Exists(track.FilePath))
						{
							File.Delete(track.FilePath);
						}
					}
					catch
					{
					}
				}
				if (wasPlaying)
				{
					if ((int)App.AudioEngine.State == 1)
					{
						DispatcherQueue dispatcherQueue = ((Window)this).DispatcherQueue;
						DispatcherQueueHandler obj2 = val;
						if (obj2 == null)
						{
							DispatcherQueueHandler val2 = delegate
							{
								NextButton_Click(null, null);
							};
							DispatcherQueueHandler val3 = val2;
							val = val2;
							obj2 = val3;
						}
						dispatcherQueue.TryEnqueue(obj2);
					}
					else
					{
						App.AudioEngine.Stop();
						App.NowPlayingId = null;
					}
				}
				_libraryPageInstance?.SetTracks(_library);
			};
			((ButtonBase)cancelBtn).Click += (RoutedEventHandler)delegate
			{
				dialog.Hide();
			};
			await dialog.ShowAsync();
		}

		// --------------------------------------------------------------
		//  Favoris (playlist systeme protegee)
		// --------------------------------------------------------------

		public async Task<HashSet<string>> GetFavoriteTrackIdsAsync()
		{
			try
			{
				var playlists = await App.Cache.LoadAllPlaylistsAsync();
				var favorites = playlists.FirstOrDefault(p => p.Id == Playlist.FavoritesId);
				return favorites != null ? new HashSet<string>(favorites.TrackIds) : new HashSet<string>();
			}
			catch { return new HashSet<string>(); }
		}

		public async Task<bool> IsFavoriteAsync(string trackId)
		{
			if (string.IsNullOrEmpty(trackId)) return false;
			try
			{
				var playlists = await App.Cache.LoadAllPlaylistsAsync();
				var favorites = playlists.FirstOrDefault(p => p.Id == Playlist.FavoritesId);
				return favorites != null && favorites.TrackIds.Contains(trackId);
			}
			catch { return false; }
		}

		public async Task<bool> ToggleFavoriteAsync(Track track)
		{
			if (track == null) return false;
			try
			{
				var playlists = await App.Cache.LoadAllPlaylistsAsync();
				var favorites = playlists.FirstOrDefault(p => p.Id == Playlist.FavoritesId);
				if (favorites == null)
				{
					// Filet de securite : la playlist systeme est normalement deja creee par
					// LibraryCacheService.InitializeAsync, mais on la recree si besoin.
					favorites = new Playlist { Id = Playlist.FavoritesId, Name = Strings.Current.IsFr ? "Favoris" : "Favorites", IsSystem = true };
				}
				bool nowFavorite;
				if (favorites.TrackIds.Contains(track.Id))
				{
					favorites.TrackIds.Remove(track.Id);
					nowFavorite = false;
				}
				else
				{
					favorites.TrackIds.Add(track.Id);
					nowFavorite = true;
				}
				favorites.DateModified = DateTime.UtcNow;
				await App.Cache.UpsertPlaylistAsync(favorites);
				_playlistsPageInstance?.RefreshAsync();
				if (App.NowPlayingId == track.Id)
				{
					UpdateNowPlayingFavoriteIconVisual(nowFavorite);
				}
				return nowFavorite;
			}
			catch { return false; }
		}

		private void UpdateNowPlayingFavoriteIconVisual(bool isFavorite)
		{
			if (NowPlayingFavoriteIcon == null) return;
			NowPlayingFavoriteIcon.Glyph = isFavorite ? "\ue00b" : "\ue006";
			((IconElement)NowPlayingFavoriteIcon).Foreground = isFavorite
				? (Brush)Application.Current.Resources["AppAccentBrush"]
				: (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
		}

		private async Task UpdateNowPlayingFavoriteIconAsync(string trackId)
		{
			bool isFavorite = await IsFavoriteAsync(trackId);
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				UpdateNowPlayingFavoriteIconVisual(isFavorite);
			});
		}

		private async void NowPlayingFavoriteButton_Click(object sender, RoutedEventArgs e)
		{
			Track track = _library.FirstOrDefault((Track t) => t.Id == App.NowPlayingId);
			if (track != null)
			{
				await ToggleFavoriteAsync(track);
			}
		}

		// Remplit apres coup le texte/icone d'un MenuFlyoutItem "Ajouter/Retirer des favoris"
		// selon l'etat actuel (necessite une requete async, donc pas disponible au moment de
		// la construction synchrone du menu).
		private async void PopulateFavoriteMenuItemAsync(MenuFlyoutItem item, string trackId)
		{
			bool isFavorite = await IsFavoriteAsync(trackId);
			item.Text = isFavorite ? Strings.Current.CS_RemoveFromFavorites : Strings.Current.CS_AddToFavorites;
			item.Icon = (IconElement)new FontIcon { Glyph = isFavorite ? "\ue00b" : "\ue006" };
		}

		// --------------------------------------------------------------
		//  Presets d'egaliseur : flyout rapide depuis la PlayerBar
		// --------------------------------------------------------------

		private void EqualizerQuickButton_Click(object sender, RoutedEventArgs e)
		{
			MenuFlyout flyout = new MenuFlyout();
			MenuFlyoutItem header = new MenuFlyoutItem
			{
				Text = Strings.Current.CS_EqualizerPresetsTitle,
				IsEnabled = false
			};
			flyout.Items.Add((MenuFlyoutItemBase)header);
			flyout.Items.Add((MenuFlyoutItemBase)new MenuFlyoutSeparator());
			foreach (var preset in Resona.Models.EqualizerPresets.All)
			{
				var presetBands = preset.Bands;
				MenuFlyoutItem item = new MenuFlyoutItem { Text = preset.Name };
				item.Click += (RoutedEventHandler)delegate
				{
					ApplyEqualizerPresetQuick((double[])presetBands.Clone());
				};
				flyout.Items.Add((MenuFlyoutItemBase)item);
			}
			flyout.Items.Add((MenuFlyoutItemBase)new MenuFlyoutSeparator());
			MenuFlyoutItem openFull = new MenuFlyoutItem
			{
				Text = Strings.Current.CS_OpenFullEqualizer,
				Icon = (IconElement)new FontIcon { Glyph = "\ue713" }
			};
			openFull.Click += (RoutedEventHandler)delegate
			{
				NavigateToSidebarItem(null, true);
			};
			flyout.Items.Add((MenuFlyoutItemBase)openFull);
			flyout.ShowAt((FrameworkElement)EqualizerQuickButton);
		}

		private void ApplyEqualizerPresetQuick(double[] newBands)
		{
			App.Settings.Current.EqualizerBands = newBands;
			if (!App.Settings.Current.EqualizerEnabled)
			{
				App.Settings.Current.EqualizerEnabled = true;
				App.AudioEngine?.SetEqualizerEnabled(true);
			}
			App.Settings.SaveAsync();
			for (int i = 0; i < 10; i++)
			{
				App.AudioEngine?.SetEqualizerBand(i, (float)newBands[i]);
			}
			// SettingsPageInstance est instanciee de facon permanente en XAML (pas a la
			// demande) : si l'utilisateur a deja ouvert les Parametres, son UI (switch +
			// sliders) doit refleter le preset choisi depuis le lecteur.
			SettingsPageInstance?.RefreshEqualizerUI();
		}

		private async Task LoadPlaylistSubItemsAsync(MenuFlyoutSubItem parent, List<Track> tracksToAdd)
		{
			try
			{
				foreach (Playlist pl in await App.Cache.LoadAllPlaylistsAsync())
				{
					// La playlist systeme (Favoris) n'apparait pas dans les ajouts de playlists
					// classiques : le bouton coeur dedie sert deja a cet usage.
					if (pl.IsSystem) continue;
					MenuFlyoutItem item = new MenuFlyoutItem
					{
						Text = pl.Name
					};
					Playlist captured = pl;
					item.Click += (RoutedEventHandler)async delegate
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
					parent.Items.Add((MenuFlyoutItemBase)item);
				}
				if (parent.Items.Count == 0)
				{
					parent.Items.Add((MenuFlyoutItemBase)new MenuFlyoutItem
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
			FontIcon val = new FontIcon
			{
				Glyph = "\ue8f1",
				FontSize = 10.0,
				Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
				Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
			};
			TextBlock val2 = new TextBlock
			{
				Text = label,
				FontSize = 11.0,
				Opacity = 0.7,
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
			};
			StackPanel val3 = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			((Panel)val3).Children.Add((UIElement)val);
			((Panel)val3).Children.Add((UIElement)val2);
			StackPanel val4 = val3;
			StackPanel val5 = new StackPanel();
			((Panel)val5).Children.Add((UIElement)val4);
			((Panel)val5).Children.Add((UIElement)input);
			val5.Spacing = 2.0;
			return val5;
		}

		public void AddToQueue(Track track)
		{
			_manualQueue.Add(track);
			_queuePageInstance?.SetQueue(_manualQueue);
			SaveManualQueue();
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				UpdateUpNextPanel();
			});
		}

		public void RemoveFromQueue(Track track)
		{
			_manualQueue.Remove(track);
			_queuePageInstance?.SetQueue(_manualQueue);
			SaveManualQueue();
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				UpdateUpNextPanel();
			});
		}

		public void EnableContinuousPlaybackIfOff()
		{
			if (_playbackMode == PlaybackMode.Off)
			{
				_playbackMode = PlaybackMode.RepeatAll;
				App.Settings.Current.SavedPlaybackMode = (int)_playbackMode;
				RepeatIcon.Glyph = "\ue8ee";
				((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
				ToolTipService.SetToolTip((DependencyObject)RepeatButton, (object)(Strings.Current.IsFr ? "Répéter la liste" : "Repeat all"));
			}
		}

		private void RestoreManualQueue()
		{
			_manualQueue.Clear();
			if (App.Settings.Current.SavedQueueIds != null && _library != null)
			{
				foreach (string id in App.Settings.Current.SavedQueueIds)
				{
					Track track = _library.FirstOrDefault((Track t) => t.Id == id);
					if (track != null)
					{
						_manualQueue.Add(track);
					}
				}
			}
			_queuePageInstance?.SetQueue(_manualQueue);
		}

		private void SaveManualQueue()
		{
			App.Settings.Current.SavedQueueIds = _manualQueue.Select((Track t) => t.Id).ToList();
			App.Settings.SaveAsync();
		}

		public void ClearQueue()
		{
			_manualQueue.Clear();
			_queuePageInstance?.SetQueue(_manualQueue);
			SaveManualQueue();
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				UpdateUpNextPanel();
			});
		}

				public void RefreshQueueIfMatches(List<Track> queueReference)
		{
			if (_queue == queueReference && _nowPlayingId != null)
			{
				_queueIndex = _queue.FindIndex(t => t.Id == _nowPlayingId);
				if (_playbackMode == PlaybackMode.Shuffle)
				{
					RegenerateShuffleUpcoming();
				}
				((Window)this).DispatcherQueue.TryEnqueue((Microsoft.UI.Dispatching.DispatcherQueueHandler)delegate
				{
					UpdateUpNextPanel();
				});
			}
		}

public void UpdateQueue(List<Track> newQueue)
		{
			_manualQueue.Clear();
			_manualQueue.AddRange(newQueue);
			_queuePageInstance?.SetQueue(_manualQueue);
			SaveManualQueue();
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				UpdateUpNextPanel();
			});
		}

		public void NavigateToArtist(string artist)
		{
			List<Track> list = _library.Where((Track t) => string.Equals(t.Artist, artist, StringComparison.OrdinalIgnoreCase)).ToList();
			if (list.Count > 0)
			{
				if (RootNav.SelectedItem != null)
				{
					_lastNavSelectedItem = RootNav.SelectedItem;
				}
				RootNav.SelectedItem = null;
				ShowTrackCollection(artist, list, Strings.Current.CS_Artiste);
			}
		}

		public void NavigateToAlbum(string album)
		{
			List<Track> list = _library.Where((Track t) => string.Equals(t.Album, album, StringComparison.OrdinalIgnoreCase)).ToList();
			if (list.Count > 0)
			{
				if (RootNav.SelectedItem != null)
				{
					_lastNavSelectedItem = RootNav.SelectedItem;
				}
				RootNav.SelectedItem = null;
				ShowTrackCollection(album, list, Strings.Current.CS_Album);
			}
		}

		public async Task<bool> ShowAutoTagDialogAsync(Track track)
		{
			string coverPath = track.CoverArtPath;
			Image coverImage = new Image
			{
				Width = 140.0,
				Height = 140.0,
				Stretch = Stretch.UniformToFill,
				Source = (ImageSource)((!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath)) ? CoverCacheService.GetBitmap(coverPath, 140) : null)
			};
			Border coverPlaceholder = new Border
			{
				Width = 140.0,
				Height = 140.0,
				CornerRadius = new CornerRadius(8.0),
				Background = (Brush)Application.Current.Resources["AppSurfaceBrush"],
				Child = (UIElement)new FontIcon
				{
					Glyph = "\ue93c",
					FontSize = 40.0,
					Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				}
			};
			if (coverImage.Source != (ImageSource)null)
			{
				coverPlaceholder.Child = (UIElement)coverImage;
			}
			Button changeCoverBtn = new Button
			{
				Content = (Strings.Current.IsFr ? "Changer la pochette" : "Change cover"),
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
				UseLayoutRounding = true,
				Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
				BorderThickness = new Thickness(1.0),
				BorderBrush = (Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],
				CornerRadius = new CornerRadius(6.0),
				Padding = new Thickness(10.0, 4.0, 10.0, 4.0)
			};
			((ButtonBase)changeCoverBtn).Click += (RoutedEventHandler)async delegate
			{
				FileOpenPicker picker = new FileOpenPicker
				{
					ViewMode = PickerViewMode.Thumbnail,
					FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" }
				};
				InitializeWithWindow.Initialize((object)picker, WindowNative.GetWindowHandle((object)this));
				StorageFile file = await picker.PickSingleFileAsync();
				if (file != (StorageFile)null)
				{
					coverPath = file.Path;
					BitmapImage bmp2 = CoverCacheService.GetBitmap(coverPath, 140);
					if (bmp2 != (BitmapImage)null)
					{
						coverImage.Source = (ImageSource)bmp2;
						coverPlaceholder.Child = (UIElement)coverImage;
					}
				}
			};
			TextBox titleBox = new TextBox
			{
				Text = track.Title,
				PlaceholderText = (Strings.Current.IsFr ? "Titre" : "Title")
			};
			TextBox artistBox = new TextBox
			{
				Text = track.Artist,
				PlaceholderText = (Strings.Current.IsFr ? "Artiste" : "Artist")
			};
			TextBox albumBox = new TextBox
			{
				Text = track.Album,
				PlaceholderText = "Album"
			};
			TextBox genreBox = new TextBox
			{
				Text = (track.Genre ?? ""),
				PlaceholderText = "Genre"
			};
			TextBox yearBox = new TextBox
			{
				Text = ((track.Year > 0) ? track.Year.ToString() : ""),
				PlaceholderText = (Strings.Current.IsFr ? "Année" : "Year")
			};
			TextBox trackNumBox = new TextBox
			{
				Text = ((track.TrackNumber > 0) ? track.TrackNumber.ToString() : ""),
				PlaceholderText = (Strings.Current.IsFr ? "Piste" : "Track")
			};
			Button searchCoverBtn = new Button
			{
				Content = (Strings.Current.IsFr ? "Chercher en ligne" : "Search online"),
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
				UseLayoutRounding = true,
				Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
				BorderThickness = new Thickness(1.0),
				BorderBrush = (Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],
				CornerRadius = new CornerRadius(6.0),
				Padding = new Thickness(10.0, 4.0, 10.0, 4.0)
			};
			((ButtonBase)searchCoverBtn).Click += (RoutedEventHandler)async delegate
			{
				string term = App.CoverArt.BuildCoverSearchQuery(titleBox.Text, artistBox.Text, albumBox.Text);
				object originalContent = ((ContentControl)searchCoverBtn).Content;
				((ContentControl)searchCoverBtn).Content = (Strings.Current.IsFr ? "Recherche..." : "Searching...");
				((Control)searchCoverBtn).IsEnabled = false;
				List<string> results = (await App.CoverArt.SearchGoogleImagesAsync(term, 12)).Distinct().Take(12).ToList();
				((ContentControl)searchCoverBtn).Content = originalContent;
				((Control)searchCoverBtn).IsEnabled = true;
				if (results.Count == 0)
				{
					HyperlinkButton googleBtn = new HyperlinkButton
					{
						Content = "Chercher sur Google Images",
						NavigateUri = new Uri("https://www.google.com/search?tbm=isch&q=" + Uri.EscapeDataString(term))
					};
					Flyout val = new Flyout();
					StackPanel val2 = new StackPanel();
					((Panel)val2).Children.Add((UIElement)new TextBlock
					{
						Text = "Aucune pochette trouvée pour '" + term + "'."
					});
					((Panel)val2).Children.Add((UIElement)googleBtn);
					val.Content = (UIElement)val2;
					Flyout errorFlyout = val;
					((FlyoutBase)errorFlyout).ShowAt((FrameworkElement)searchCoverBtn);
				}
				else
				{
					GridView gridView = new GridView
					{
						SelectionMode = ListViewSelectionMode.None,
						MaxHeight = 400.0
					};
					Grid containerGrid = new Grid
					{
						Width = 520.0
					};
					((Panel)containerGrid).Children.Add((UIElement)gridView);
					Flyout flyout = new Flyout();
					Style style = new Style(typeof(FlyoutPresenter));
					style.Setters.Add((SetterBase)new Setter(FrameworkElement.MaxWidthProperty, (object)600.0));
					style.Setters.Add((SetterBase)new Setter(Control.PaddingProperty, (object)new Thickness(8.0)));
					flyout.FlyoutPresenterStyle = style;
					foreach (string url in results)
					{
						Image img = new Image
						{
							Width = 150.0,
							Height = 150.0,
							Stretch = Stretch.UniformToFill,
							Margin = new Thickness(4.0)
						};
						try
						{
							img.Source = (ImageSource)new BitmapImage(new Uri(url));
						}
						catch
						{
							continue;
						}
						((UIElement)img).Tapped += (TappedEventHandler)delegate
						{
							coverPath = url;
							coverImage.Source = img.Source;
							coverPlaceholder.Child = (UIElement)coverImage;
							((FlyoutBase)flyout).Hide();
						};
						((ItemsControl)gridView).Items.Add((object)img);
					}
					flyout.Content = (UIElement)containerGrid;
					((FlyoutBase)flyout).ShowAt((FrameworkElement)searchCoverBtn);
				}
			};
			StackPanel coverPanel = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0.0, 0.0, 16.0, 0.0)
			};
			((Panel)coverPanel).Children.Add((UIElement)coverPlaceholder);
			((Panel)coverPanel).Children.Add((UIElement)changeCoverBtn);
			((Panel)coverPanel).Children.Add((UIElement)searchCoverBtn);
			Button searchTagsBtn = new Button
			{
				Content = (object)new TextBlock
				{
					Text = (Strings.Current.IsFr ? "Rechercher métadonnées" : "Search metadata"),
					TextWrapping = TextWrapping.Wrap,
					TextAlignment = TextAlignment.Center
				},
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
			};
			Button resetTagsBtn = new Button
			{
				Content = (Strings.Current.IsFr ? "Réinitialiser" : "Reset"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
			};
			TextBlock statusTextBlock = new TextBlock
			{
				FontSize = 11.0,
				Opacity = 0.6,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
				TextWrapping = TextWrapping.Wrap,
				Visibility = Visibility.Collapsed
			};
			((ButtonBase)searchTagsBtn).Click += (RoutedEventHandler)async delegate
			{
				((Control)searchTagsBtn).IsEnabled = false;
				object originalContent = ((ContentControl)searchTagsBtn).Content;
				((ContentControl)searchTagsBtn).Content = (object)new TextBlock
				{
					Text = (Strings.Current.IsFr ? "Recherche en cours..." : "Searching..."),
					TextWrapping = TextWrapping.Wrap,
					TextAlignment = TextAlignment.Center
				};
				AutoTagResult result2 = await AutoTagService.LookupAsync(artistBox.Text, titleBox.Text, track.Duration, track.FilePath);
				((ContentControl)searchTagsBtn).Content = originalContent;
				((Control)searchTagsBtn).IsEnabled = true;
				if (result2 != null)
				{
					if (!string.IsNullOrWhiteSpace(result2.Title))
					{
						titleBox.Text = result2.Title;
					}
					if (!string.IsNullOrWhiteSpace(result2.Artist))
					{
						artistBox.Text = result2.Artist;
					}
					if (!string.IsNullOrWhiteSpace(result2.Album))
					{
						albumBox.Text = result2.Album;
					}
					if (!string.IsNullOrWhiteSpace(result2.Genre))
					{
						genreBox.Text = result2.Genre;
					}
					if (result2.Year.HasValue)
					{
						yearBox.Text = result2.Year.Value.ToString();
					}
					if (result2.TrackNumber.HasValue)
					{
						trackNumBox.Text = result2.TrackNumber.Value.ToString();
					}
					if (!string.IsNullOrWhiteSpace(result2.CoverPath))
					{
						coverPath = result2.CoverPath;
						try
						{
							coverImage.Source = (ImageSource)new BitmapImage(new Uri(result2.CoverPath));
							coverPlaceholder.Child = (UIElement)coverImage;
						}
						catch
						{
						}
					}
					statusTextBlock.Text = (Strings.Current.IsFr ? "✅ Métadonnées trouvées et pré-remplies." : "✅ Metadata found and pre-filled.");
					((UIElement)statusTextBlock).Visibility = Visibility.Visible;
				}
				else
				{
					statusTextBlock.Text = (Strings.Current.IsFr ? "⚠\ufe0f Aucune donnée trouvée — remplis manuellement." : "⚠\ufe0f No data found — fill manually.");
					((UIElement)statusTextBlock).Visibility = Visibility.Visible;
				}
			};
			((ButtonBase)resetTagsBtn).Click += (RoutedEventHandler)delegate
			{
				titleBox.Text = track.Title;
				artistBox.Text = track.Artist;
				albumBox.Text = track.Album;
				genreBox.Text = track.Genre ?? "";
				yearBox.Text = ((track.Year > 0) ? track.Year.ToString() : "");
				trackNumBox.Text = ((track.TrackNumber > 0) ? track.TrackNumber.ToString() : "");
				((UIElement)statusTextBlock).Visibility = Visibility.Collapsed;
			};
			Grid tagsActionPanel = new Grid
			{
				ColumnSpacing = 8.0
			};
			tagsActionPanel.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			tagsActionPanel.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Grid.SetColumn((FrameworkElement)searchTagsBtn, 0);
			Grid.SetColumn((FrameworkElement)resetTagsBtn, 1);
			((Panel)tagsActionPanel).Children.Add((UIElement)searchTagsBtn);
			((Panel)tagsActionPanel).Children.Add((UIElement)resetTagsBtn);
			StackPanel form = new StackPanel
			{
				Spacing = 6.0
			};
			((Panel)form).Children.Add((UIElement)MakeField(Strings.Current.IsFr ? "Titre" : "Title", (FrameworkElement)titleBox));
			((Panel)form).Children.Add((UIElement)MakeField(Strings.Current.IsFr ? "Artiste" : "Artist", (FrameworkElement)artistBox));
			((Panel)form).Children.Add((UIElement)MakeField("Album", (FrameworkElement)albumBox));
			((Panel)form).Children.Add((UIElement)MakeField("Genre", (FrameworkElement)genreBox));
			((Panel)form).Children.Add((UIElement)MakeField(Strings.Current.IsFr ? "Année" : "Year", (FrameworkElement)yearBox));
			((Panel)form).Children.Add((UIElement)MakeField(Strings.Current.IsFr ? "Piste" : "Track", (FrameworkElement)trackNumBox));
			((Panel)form).Children.Add((UIElement)tagsActionPanel);
			((Panel)form).Children.Add((UIElement)statusTextBlock);
			Grid bodyGrid = new Grid
			{
				ColumnSpacing = 16.0,
				Width = 480.0,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			bodyGrid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			bodyGrid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Grid.SetColumn((FrameworkElement)coverPanel, 0);
			Grid.SetColumn((FrameworkElement)form, 1);
			((Panel)bodyGrid).Children.Add((UIElement)coverPanel);
			((Panel)bodyGrid).Children.Add((UIElement)form);
			CheckBox writeToFileCheckBox = new CheckBox
			{
				Content = (Strings.Current.IsFr ? "Enregistrer les modifications directement dans le fichier (écraser)" : "Save changes directly to file (overwrite)"),
				IsChecked = App.Settings.Current.AutoTagWriteToFile,
				Margin = new Thickness(0.0, 16.0, 0.0, 0.0),
				Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
			};
			Grid dialogContentPanel = new Grid();
			dialogContentPanel.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			dialogContentPanel.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			Grid.SetRow((FrameworkElement)bodyGrid, 0);
			Grid.SetRow((FrameworkElement)writeToFileCheckBox, 1);
			((Panel)dialogContentPanel).Children.Add((UIElement)bodyGrid);
			((Panel)dialogContentPanel).Children.Add((UIElement)writeToFileCheckBox);
			ContentDialog editDialog = new ContentDialog
			{
				Title = (Strings.Current.IsFr ? "Autotag - Édition" : "Autotag - Edit"),
				Content = dialogContentPanel,
				PrimaryButtonText = (Strings.Current.IsFr ? "Sauvegarder" : "Save"),
				CloseButtonText = Strings.Current.CS_Annuler,
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = ((UIElement)ContentFrame).XamlRoot
			};
			ContentDialogResult result = await editDialog.ShowAsync();
			if (result == ContentDialogResult.Primary)
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
				if (titleBox.Text != track.Title || artistBox.Text != track.Artist || albumBox.Text != track.Album || genreBox.Text != (track.Genre ?? "") || yearBox.Text != ((track.Year > 0) ? track.Year.ToString() : "") || trackNumBox.Text != ((track.TrackNumber > 0) ? track.TrackNumber.ToString() : ""))
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
					string coverDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "Covers");
					Directory.CreateDirectory(coverDir);
					string newCoverName = $"{track.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";
					string coverDest = Path.Combine(coverDir, newCoverName);
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
							hc.DefaultRequestHeaders.Add("User-Agent", "Resona/2.4");
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
				if (saved && ((ToggleButton)writeToFileCheckBox).IsChecked == true)
				{
					data.CoverPath = track.CoverArtPath;
					AutoTagService.WriteMetadata(track.FilePath, data);
					App.Settings.Current.AutoTagWriteToFile = true;
					App.Settings.SaveSync();
				}
				else if (((ToggleButton)writeToFileCheckBox).IsChecked == false && App.Settings.Current.AutoTagWriteToFile)
				{
					App.Settings.Current.AutoTagWriteToFile = false;
					App.Settings.SaveSync();
				}
				if (saved)
				{
					await App.Cache.UpdateTrackAsync(track);
					if (App.NowPlayingId == track.Id)
					{
						((ContentControl)NowPlayingTitle).Content = track.Title;
						UpdateMiniPlayerUI(track);
						App.DiscordRpc?.UpdatePresence(track, isPlaying: true);
						((ContentControl)NowPlayingArtist).Content = track.Artist;
						((ContentControl)NowPlayingAlbum).Content = track.Album;
						if (!string.IsNullOrEmpty(track.CoverArtPath))
						{
							BitmapImage bmp = CoverCacheService.GetBitmap(track.CoverArtPath, 140);
							if (bmp != (BitmapImage)null)
							{
								NowPlayingCover.Source = (ImageSource)bmp;
								((UIElement)NowPlayingPlaceholderIcon).Visibility = Visibility.Collapsed;
							}
						}
					}
					_libraryPageInstance?.SetTracks(_library);
				}
				return true;
			}
			return false;
		}

		private void NowPlayingArtist_Click(object sender, RoutedEventArgs e)
		{
			if (((ContentControl)NowPlayingArtist).Content is string text && !string.IsNullOrWhiteSpace(text))
			{
				NavigateToArtist(text);
			}
		}

		private void NowPlayingAlbum_Click(object sender, RoutedEventArgs e)
		{
			if (((ContentControl)NowPlayingAlbum).Content is string text && !string.IsNullOrWhiteSpace(text))
			{
				NavigateToAlbum(text);
			}
		}

		public void TogglePlayPause()
		{
			PlayPauseButton_Click(this, new RoutedEventArgs());
		}

		public void SetShuffleModeAndPlay(Track track, List<Track> queue)
		{
			_playbackMode = PlaybackMode.Shuffle;
			UpdateRepeatButtonVisual();
			PlayTrack(track, queue);
			RegenerateShuffleUpcoming();
		}

		public void NavigateToPlaylistDetail(Playlist playlist, List<Track> librarySnapshot)
		{
			ContentFrame.Navigate(typeof(PlaylistDetailPage), (object)new Tuple<Playlist, List<Track>>(playlist, librarySnapshot), (NavigationTransitionInfo)new SuppressNavigationTransitionInfo());
			((Window)this).DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, (DispatcherQueueHandler)delegate
			{
				if (((ContentControl)ContentFrame).Content is PlaylistDetailPage playlistDetailPage)
				{
					playlistDetailPage.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
				}
			});
		}

		public async void ShowTrackCollection(string title, List<Track> tracks, string? subtitle = null)
		{
			if (((ContentControl)ContentFrame).Content != null && (int)((UIElement)ContentFrame).Visibility == 0)
			{
				await AnimationHelper.PlayExitAnimationAsync((UIElement)ContentFrame, -20f);
			}
			if ((Page)_libraryPageInstance == (Page)null)
			{
				_libraryPageInstance = new LibraryPage();
			}
			_libraryPageInstance.ShowCollection(title, subtitle, tracks);
			((ContentControl)ContentFrame).Content = _libraryPageInstance;
			AnimationHelper.PlayEntranceAnimation((UIElement)ContentFrame);
		}

		private void RepeatButton_Click(object sender, RoutedEventArgs e)
		{
			PlaybackMode playbackMode = _playbackMode;
			bool flag = false;
			PlaybackMode playbackMode2 = playbackMode switch
			{
				PlaybackMode.Off => PlaybackMode.RepeatAll, 
				PlaybackMode.RepeatAll => PlaybackMode.RepeatOne, 
				PlaybackMode.RepeatOne => PlaybackMode.Shuffle, 
				_ => PlaybackMode.Off, 
			};
			bool flag2 = false;
			_playbackMode = playbackMode2;
			App.Settings.Current.SavedPlaybackMode = (int)_playbackMode;
			App.Settings.SaveAsync();
			UpdateRepeatButtonVisual();
			UpdatePlayerButtonsColor();
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				UpdateUpNextPanel();
			});
		}

		private Track PickRandomTrack()
		{
			if (_queue.Count <= 1)
			{
				return _queue.FirstOrDefault();
			}
			if (_shuffleUpcoming.Count == 0)
			{
				RegenerateShuffleUpcoming(includeCurrent: true);
			}
			if (_shuffleUpcoming.Count == 0)
			{
				return _queue.FirstOrDefault();
			}
			Track result = _shuffleUpcoming[0];
			_shuffleUpcoming.RemoveAt(0);
			return result;
		}

		private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			if (sender == MiniProgressSlider && ProgressSlider != (Slider)null)
			{
				((RangeBase)ProgressSlider).Value = e.NewValue;
			}
			if (sender == ProgressSlider && MiniProgressSlider != (Slider)null)
			{
				((RangeBase)MiniProgressSlider).Value = e.NewValue;
			}
			UpdateCustomProgress();
		}

		private void ProgressSlider_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateCustomProgress();
		}

		private void UpdateCustomProgress()
		{
			if (ProgressSlider != (Slider)null && CustomProgressFill != (Grid)null && CustomProgressThumb != (Grid)null && CustomProgressTrack != (Grid)null)
			{
				double num = Math.Max(0.001, ((RangeBase)ProgressSlider).Maximum);
				double num2 = ((RangeBase)ProgressSlider).Value / num;
				double actualWidth = ((FrameworkElement)CustomProgressTrack).ActualWidth;
				if (actualWidth > 0.0)
				{
					double num3 = actualWidth * num2;
					((FrameworkElement)CustomProgressFill).Width = num3;
					((FrameworkElement)CustomProgressThumb).Margin = new Thickness(num3, 5.0, 0.0, 0.0);
				}
			}
			if (MiniProgressSlider != (Slider)null && MiniCustomProgressFill != (Grid)null && MiniCustomProgressTrack != (Grid)null)
			{
				double num4 = Math.Max(0.001, ((RangeBase)MiniProgressSlider).Maximum);
				double num5 = ((RangeBase)MiniProgressSlider).Value / num4;
				double actualWidth2 = ((FrameworkElement)MiniCustomProgressTrack).ActualWidth;
				if (actualWidth2 > 0.0)
				{
					double width = actualWidth2 * num5;
					((FrameworkElement)MiniCustomProgressFill).Width = width;
				}
			}
		}

		public async void ShowTrackInfo(Track track)
		{
			if (track == null)
			{
				return;
			}
			EnsureInfoOverlayCreated();
			if (_infoTrackTitle != (TextBlock)null)
			{
				_infoTrackTitle.Text = track.Title;
			}
			if (_infoTrackArtist != (TextBlock)null)
			{
				_infoTrackArtist.Text = track.Artist;
			}
			if (_infoContent != (TextBlock)null)
			{
				_infoContent.Text = Strings.Current.TrackInfo_Loading;
			}
			if (_infoOverlay != (Grid)null)
			{
				((UIElement)_infoOverlay).Visibility = Visibility.Visible;
			}
			_infoOverlayOpen = true;
			Storyboard sb = new Storyboard();
			DoubleAnimation fi = new DoubleAnimation
			{
				To = 1.0,
				Duration = new Duration(TimeSpan.FromMilliseconds(200.0)),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			Storyboard.SetTarget((Timeline)(object)fi, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)fi, "Opacity");
			sb.Children.Add((Timeline)(object)fi);
			if (!(((UIElement)_infoOverlay).RenderTransform is CompositeTransform))
			{
				((UIElement)_infoOverlay).RenderTransform = (Transform)new CompositeTransform();
			}
			((UIElement)_infoOverlay).RenderTransformOrigin = new Point(0.5, 0.5);
			CompositeTransform transform = (CompositeTransform)((UIElement)_infoOverlay).RenderTransform;
			transform.ScaleX = 0.95;
			transform.ScaleY = 0.95;
			transform.TranslateY = 20.0;
			DoubleAnimation sx = new DoubleAnimation
			{
				To = 1.0,
				Duration = new Duration(TimeSpan.FromMilliseconds(400.0)),
				EasingFunction = (EasingFunctionBase)new ExponentialEase
				{
					EasingMode = EasingMode.EaseOut,
					Exponent = 4.0
				}
			};
			DoubleAnimation sy = new DoubleAnimation
			{
				To = 1.0,
				Duration = new Duration(TimeSpan.FromMilliseconds(400.0)),
				EasingFunction = (EasingFunctionBase)new ExponentialEase
				{
					EasingMode = EasingMode.EaseOut,
					Exponent = 4.0
				}
			};
			DoubleAnimation ty = new DoubleAnimation
			{
				To = 0.0,
				Duration = new Duration(TimeSpan.FromMilliseconds(400.0)),
				EasingFunction = (EasingFunctionBase)new ExponentialEase
				{
					EasingMode = EasingMode.EaseOut,
					Exponent = 4.0
				}
			};
			Storyboard.SetTarget((Timeline)(object)sx, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
			Storyboard.SetTarget((Timeline)(object)sy, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
			Storyboard.SetTarget((Timeline)(object)ty, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
			sb.Children.Add((Timeline)(object)sx);
			sb.Children.Add((Timeline)(object)sy);
			sb.Children.Add((Timeline)(object)ty);
			sb.Begin();
			if (string.IsNullOrWhiteSpace(track.Artist) || track.Artist.Equals("Inconnu", StringComparison.OrdinalIgnoreCase) || track.Artist.Equals("Unknown", StringComparison.OrdinalIgnoreCase) || track.Artist.Equals("Unknown Artist", StringComparison.OrdinalIgnoreCase) || track.Artist.Equals("Artiste inconnu", StringComparison.OrdinalIgnoreCase))
			{
				if (_infoContent != (TextBlock)null)
				{
					_infoContent.Text = Strings.Current.TrackInfo_NoBio;
				}
				return;
			}
			try
			{
				using HttpClient http = new HttpClient();
				http.DefaultRequestHeaders.Add("User-Agent", "Resona/1.0");
				string artistEncoded = Uri.EscapeDataString(track.Artist);
				string lang = (Strings.Current.IsFr ? "fr" : "en");
				string url = $"https://{lang}.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch={artistEncoded}&gsrlimit=1&prop=extracts&exintro&explaintext&format=json";
				HttpResponseMessage response = await http.GetAsync(url);
				response.EnsureSuccessStatusCode();
				JsonDocument root = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
				JsonElement pages = root.RootElement.GetProperty("query").GetProperty("pages");
				string infoText = Strings.Current.TrackInfo_NoBio;
				bool isDisambiguation = false;
				foreach (JsonProperty item3 in pages.EnumerateObject())
				{
					if (item3.Value.TryGetProperty("extract", out var extractElement))
					{
						string extract = extractElement.GetString() ?? "";
						if (!string.IsNullOrWhiteSpace(extract))
						{
							infoText = extract;
							if (extract.Contains("may refer to:", StringComparison.OrdinalIgnoreCase) || extract.Contains("peut faire rÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â\u00a0ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â\u00a0ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â\u00a0ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â\u00a0ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©fÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â\u00a0ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â\u00a0ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â\u00a0ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â\u00a0ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©rence ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â\u00a0ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â\u00a0ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â\u00a0ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â\u00a0ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â\u00a0ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€\u00a0Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â\u00a0", StringComparison.OrdinalIgnoreCase) || extract.Contains("est une page d'homonymie", StringComparison.OrdinalIgnoreCase) || extract.Contains("may also refer to:", StringComparison.OrdinalIgnoreCase))
							{
								isDisambiguation = true;
							}
							break;
						}
					}
					extractElement = default(JsonElement);
				}
				if (isDisambiguation)
				{
					string artistEncodedWithSuffix = Uri.EscapeDataString(string.Concat(str1: Strings.Current.IsFr ? " musique" : " musician", str0: track.Artist));
					url = $"https://{lang}.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch={artistEncodedWithSuffix}&gsrlimit=1&prop=extracts&exintro&explaintext&format=json";
					response = await http.GetAsync(url);
					response.EnsureSuccessStatusCode();
					root = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
					if (root.RootElement.GetProperty("query").TryGetProperty("pages", out pages))
					{
						foreach (JsonProperty item4 in pages.EnumerateObject())
						{
							if (item4.Value.TryGetProperty("extract", out var extractElement2))
							{
								string extract2 = extractElement2.GetString() ?? "";
								if (!string.IsNullOrWhiteSpace(extract2))
								{
									infoText = extract2;
									break;
								}
							}
							extractElement2 = default(JsonElement);
						}
					}
				}
				if (_infoContent != (TextBlock)null)
				{
					_infoContent.Text = infoText;
				}
			}
			catch (Exception ex)
			{
				if (_infoContent != (TextBlock)null)
				{
					_infoContent.Text = string.Format(Strings.Current.TrackInfo_Error, ex.Message);
				}
			}
		}

		private void EnsureInfoOverlayCreated()
		{
			if (!(_infoOverlay != (Grid)null))
			{
				StackPanel val = new StackPanel
				{
					Spacing = 20.0,
					VerticalAlignment = VerticalAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Center,
					MaxWidth = 800.0
				};
				_infoTrackTitle = new TextBlock
				{
					FontSize = 32.0,
					FontWeight = FontWeights.Bold,
					TextWrapping = TextWrapping.Wrap,
					TextAlignment = TextAlignment.Center
				};
				_infoTrackArtist = new TextBlock
				{
					FontSize = 20.0,
					Opacity = 0.8,
					Margin = new Thickness(0.0, 0.0, 0.0, 20.0),
					TextWrapping = TextWrapping.Wrap,
					TextAlignment = TextAlignment.Center
				};
				_infoContent = new TextBlock
				{
					FontSize = 16.0,
					Opacity = 0.9,
					TextWrapping = TextWrapping.Wrap,
					IsTextSelectionEnabled = true
				};
				((Panel)val).Children.Add((UIElement)_infoTrackTitle);
				((Panel)val).Children.Add((UIElement)_infoTrackArtist);
				((Panel)val).Children.Add((UIElement)_infoContent);
				((Panel)val).Background = (Brush)new SolidColorBrush(Colors.Transparent);
				((UIElement)val).PointerPressed += (PointerEventHandler)delegate
				{
					((UIElement)_infoOverlay).Visibility = Visibility.Collapsed;
				};
				ScrollViewer val2 = new ScrollViewer
				{
					Content = val,
					Padding = new Thickness(32.0, 24.0, 32.0, 40.0),
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
					HorizontalAlignment = HorizontalAlignment.Stretch
				};
				Microsoft.UI.Xaml.Shapes.Rectangle val3 = new Microsoft.UI.Xaml.Shapes.Rectangle
				{
					Fill = (Brush)new SolidColorBrush(ColorHelper.FromArgb((byte)220, (byte)0, (byte)0, (byte)0))
				};
				_infoOverlay = new Grid
				{
					Visibility = Visibility.Collapsed,
					Opacity = 0.0,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Stretch
				};
				Grid.SetRowSpan((FrameworkElement)_infoOverlay, 2);
				Canvas.SetZIndex((UIElement)_infoOverlay, 100);
				((Panel)_infoOverlay).Children.Add((UIElement)val3);
				((Panel)_infoOverlay).Children.Add((UIElement)val2);
				((UIElement)_infoOverlay).PointerPressed += (PointerEventHandler)delegate(object s, PointerRoutedEventArgs e)
				{
					HideInfoOverlay();
					e.Handled = true;
				};
				((Panel)RootGrid).Children.Add((UIElement)_infoOverlay);
			}
		}

		private void RootNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
		{
			NavigationViewItemBase selectedItemContainer = args.SelectedItemContainer;
			string tag = ((selectedItemContainer == (NavigationViewItemBase)null) ? null : ((FrameworkElement)selectedItemContainer).Tag?.ToString()) ?? "";
			NavigateToSidebarItem(tag, args.IsSettingsSelected);
		}

		private void LyricsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			AnimationHelper.ApplyBouncyScale((UIElement)LyricsButton, 1.05f);
			LyricsButton.Background = (Brush)new SolidColorBrush(Color.FromArgb((byte)10, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}

		private void NowPlayingCoverBorder_RightTapped(object sender, RightTappedRoutedEventArgs e)
		{
			Track track = _library.FirstOrDefault((Track t) => t.Id == App.NowPlayingId);
			if (track != null)
			{
				e.Handled = true;
				MenuFlyout val = BuildTrackMenu(track, _library);
				val.ShowAt((UIElement)NowPlayingCoverBorder, e.GetPosition((UIElement)NowPlayingCoverBorder));
			}
		}

		private void NowPlayingInfo_RightTapped(object sender, RightTappedRoutedEventArgs e)
		{
			Track track = _library.FirstOrDefault((Track t) => t.Id == App.NowPlayingId);
			if (track != null)
			{
				e.Handled = true;
				MenuFlyout val = BuildTrackMenu(track, _library);
				val.ShowAt((UIElement)NowPlayingCoverBorder, e.GetPosition((UIElement)NowPlayingCoverBorder));
			}
		}

		private void NowPlayingTitle_Click(object sender, RoutedEventArgs e)
		{
			if (_currentIndex >= 0 && _currentIndex < _library.Count)
			{
				ShowTrackInfo(_library[_currentIndex]);
			}
		}

		private void UpdateRepeatButtonVisual()
		{
			((UIElement)RepeatOneBadge).Visibility = Visibility.Collapsed;
			if (MiniRepeatOneBadge != (TextBlock)null)
			{
				((UIElement)MiniRepeatOneBadge).Visibility = Visibility.Collapsed;
			}
			switch (_playbackMode)
			{
			case PlaybackMode.Off:
				RepeatIcon.Glyph = "\ue8ee";
				((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
				if (MiniRepeatIcon != (FontIcon)null)
				{
					MiniRepeatIcon.Glyph = "\ue8ee";
					((UIElement)MiniRepeatIcon).Opacity = 1.0;
					((IconElement)MiniRepeatIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)180, (byte)180, (byte)180));
				}
				ToolTipService.SetToolTip((DependencyObject)RepeatButton, (object)(Strings.Current.IsFr ? "Lecture simple" : "Normal playback"));
				break;
			case PlaybackMode.RepeatAll:
				RepeatIcon.Glyph = "\ue8ee";
				((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
				if (MiniRepeatIcon != (FontIcon)null)
				{
					MiniRepeatIcon.Glyph = "\ue8ee";
					((UIElement)MiniRepeatIcon).Opacity = 1.0;
					((IconElement)MiniRepeatIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				ToolTipService.SetToolTip((DependencyObject)RepeatButton, (object)(Strings.Current.IsFr ? "Répéter la liste" : "Repeat all"));
				break;
			case PlaybackMode.RepeatOne:
				RepeatIcon.Glyph = "\ue8ee";
				((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
				if (MiniRepeatIcon != (FontIcon)null)
				{
					MiniRepeatIcon.Glyph = "\ue8ee";
					((UIElement)MiniRepeatIcon).Opacity = 1.0;
					((IconElement)MiniRepeatIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				((UIElement)RepeatOneBadge).Visibility = Visibility.Visible;
				if (MiniRepeatOneBadge != (TextBlock)null)
				{
					((UIElement)MiniRepeatOneBadge).Visibility = Visibility.Visible;
				}
				ToolTipService.SetToolTip((DependencyObject)RepeatButton, (object)(Strings.Current.IsFr ? "Répéter ce morceau" : "Repeat one"));
				break;
			case PlaybackMode.Shuffle:
				RepeatIcon.Glyph = "\ue8b1";
				((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
				if (MiniRepeatIcon != (FontIcon)null)
				{
					MiniRepeatIcon.Glyph = "\ue8b1";
					((UIElement)MiniRepeatIcon).Opacity = 1.0;
					((IconElement)MiniRepeatIcon).Foreground = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				ToolTipService.SetToolTip((DependencyObject)RepeatButton, (object)(Strings.Current.IsFr ? "Lecture aléatoire" : "Shuffle"));
				break;
			}
		}

		private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
		{
			PlaybackState state = App.AudioEngine.State;
			PlaybackState val = state;
			if ((int)val > 0)
			{
				if ((int)val == 1)
				{
					App.AudioEngine.Pause();
					App.DiscordRpc?.UpdatePresence(App.AudioEngine.CurrentTrack, isPlaying: false);
					try
					{
						if (_smtc != (SystemMediaTransportControls)null)
						{
							_smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
						}
					}
					catch
					{
					}
					PlayPauseIcon.Glyph = "\ue768";
					if (MiniPlayPauseIcon != (FontIcon)null)
					{
						MiniPlayPauseIcon.Glyph = "\ue768";
					}
					((FrameworkElement)PlayPauseIcon).Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
					if (MiniPlayPauseIcon != (FontIcon)null)
					{
						((FrameworkElement)MiniPlayPauseIcon).Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
					}
					UpdateTaskbarPlayPauseIcon();
					return;
				}
			}
			else
			{
				TimeSpan totalDuration = App.AudioEngine.TotalDuration;
				TimeSpan currentPosition = App.AudioEngine.CurrentPosition;
				if (totalDuration.TotalSeconds > 0.0 && currentPosition.TotalSeconds >= totalDuration.TotalSeconds - 0.5)
				{
					App.AudioEngine.Seek(TimeSpan.Zero);
				}
			}
			App.AudioEngine.Resume();
			App.DiscordRpc?.UpdatePresence(App.AudioEngine.CurrentTrack, isPlaying: true);
			try
			{
				if (_smtc != (SystemMediaTransportControls)null)
				{
					_smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
				}
			}
			catch
			{
			}
			PlayPauseIcon.Glyph = "\ue769";
			if (MiniPlayPauseIcon != (FontIcon)null)
			{
				MiniPlayPauseIcon.Glyph = "\ue769";
			}
			((FrameworkElement)PlayPauseIcon).Margin = new Thickness(0.0);
			if (MiniPlayPauseIcon != (FontIcon)null)
			{
				((FrameworkElement)MiniPlayPauseIcon).Margin = new Thickness(0.0);
			}
			UpdateTaskbarPlayPauseIcon();
		}

		private void NextButton_Click(object sender, RoutedEventArgs e)
		{
			if (_manualQueue.Count > 0)
			{
				Track track = _manualQueue[0];
				RemoveFromQueue(track);
				PlayTrack(track, null, isGoingBack: false, isGoingForward: false, null, fromManualQueue: true);
			}
			else
			{
				if (_queue.Count == 0)
				{
					return;
				}
				if (_playbackMode == PlaybackMode.Shuffle)
				{
					if (_playbackFuture.Count > 0)
					{
						PlayTrack(_playbackFuture.Pop(), _queue, isGoingBack: false, isGoingForward: true);
					}
					else
					{
						PlayTrack(PickRandomTrack(), _queue, isGoingBack: false, isGoingForward: true);
					}
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
			if (_queue.Count == 0)
			{
				return;
			}
			if (_playbackMode == PlaybackMode.Shuffle)
			{
				if (_playbackHistory.Count > 0)
				{
					Track track = _playbackHistory.Pop();
					PlayTrack(track, _queue, isGoingBack: true);
				}
				else
				{
					PlayTrack(PickRandomTrack(), _queue, isGoingBack: true);
				}
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

		private void HideInfoOverlay()
		{
			if (!_infoOverlayOpen || _infoOverlay == (Grid)null)
			{
				return;
			}
			_infoOverlayOpen = false;
			Storyboard val = new Storyboard();
			DoubleAnimation val2 = new DoubleAnimation
			{
				To = 0.0,
				Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			DoubleAnimation val3 = new DoubleAnimation
			{
				To = 10.0,
				Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			DoubleAnimation val4 = new DoubleAnimation
			{
				To = 0.98,
				Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			DoubleAnimation val5 = new DoubleAnimation
			{
				To = 0.98,
				Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
				EasingFunction = (EasingFunctionBase)new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			Storyboard.SetTarget((Timeline)(object)val2, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val2, "Opacity");
			Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val3, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
			Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val4, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
			Storyboard.SetTarget((Timeline)(object)val5, (DependencyObject)_infoOverlay);
			Storyboard.SetTargetProperty((Timeline)(object)val5, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
			val.Children.Add((Timeline)(object)val2);
			val.Children.Add((Timeline)(object)val3);
			val.Children.Add((Timeline)(object)val4);
			val.Children.Add((Timeline)(object)val5);
			((Timeline)val).Completed += delegate
			{
				if (!_infoOverlayOpen)
				{
					((UIElement)_infoOverlay).Visibility = Visibility.Collapsed;
				}
			};
			val.Begin();
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
					if ((int)((UIElement)ContentFrame).Visibility == 0 && ((ContentControl)ContentFrame).Content != null)
					{
						await AnimationHelper.PlayExitAnimationAsync((UIElement)ContentFrame, -20f);
					}
					else if ((int)((UIElement)SettingsContainer).Visibility == 0)
					{
						await AnimationHelper.PlayExitAnimationAsync((UIElement)SettingsContainer, -20f);
					}
					if (_pendingNavTag != currentTag || _pendingNavIsSettings != currentSettings)
					{
						continue;
					}
					if (currentSettings)
					{
						SetNowPlayingMode(isActive: false, null);
						((UIElement)ContentFrame).Visibility = Visibility.Collapsed;
						((UIElement)SettingsContainer).Visibility = Visibility.Visible;
						AnimationHelper.PlayEntranceAnimation((UIElement)SettingsContainer);
					}
					else
					{
						((UIElement)SettingsContainer).Visibility = Visibility.Collapsed;
						((UIElement)ContentFrame).Visibility = Visibility.Visible;
						ContentFrame.BackStack.Clear();
						if (!string.IsNullOrEmpty(currentTag))
						{
							switch (currentTag)
							{
							case "library":
								((ContentControl)ContentFrame).Content = _libraryPageInstance;
								_libraryPageInstance?.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								if ((Page)_libraryPageInstance != (Page)null)
								{
									_libraryPageInstance.ResetToLibrary(_library);
								}
								break;
							case "albums":
								if ((Page)_albumsPageInstance == (Page)null)
								{
									_albumsPageInstance = new AlbumsPage();
								}
								((ContentControl)ContentFrame).Content = _albumsPageInstance;
								_albumsPageInstance.LoadData(_library);
								_albumsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								if (currentParameter is string albumName)
								{
									_albumsPageInstance.SetSearch(albumName);
								}
								break;
							case "playlists":
								if ((Page)_playlistsPageInstance == (Page)null)
								{
									_playlistsPageInstance = new PlaylistsPage();
								}
								((ContentControl)ContentFrame).Content = _playlistsPageInstance;
								_playlistsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								_playlistsPageInstance.RefreshAsync();
								break;
							case "artists":
								if ((Page)_artistsPageInstance == (Page)null)
								{
									_artistsPageInstance = new ArtistsPage();
								}
								((ContentControl)ContentFrame).Content = _artistsPageInstance;
								_artistsPageInstance.LoadData(_library);
								_artistsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								if (currentParameter is string artistName)
								{
									_artistsPageInstance.SetSearch(artistName);
								}
								break;
							case "genres":
								if ((Page)_genresPageInstance == (Page)null)
								{
									_genresPageInstance = new GenresPage();
								}
								((ContentControl)ContentFrame).Content = _genresPageInstance;
								_genresPageInstance.LoadData(_library);
								_genresPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								break;
							case "folders":
								if ((Page)_foldersPageInstance == (Page)null)
								{
									_foldersPageInstance = new FoldersPage();
								}
								((ContentControl)ContentFrame).Content = _foldersPageInstance;
								_foldersPageInstance.LoadData(_library);
								_foldersPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								break;
							case "statistics":
								if ((Page)_statisticsPageInstance == (Page)null)
								{
									_statisticsPageInstance = new StatisticsPage();
								}
								((ContentControl)ContentFrame).Content = _statisticsPageInstance;
								_statisticsPageInstance.LoadData(_library);
								break;
							case "queue":
								if ((Page)_queuePageInstance == (Page)null)
								{
									_queuePageInstance = new QueuePage();
								}
								((ContentControl)ContentFrame).Content = _queuePageInstance;
								_queuePageInstance.SetQueue(_manualQueue);
								_queuePageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
								break;
							case "download":
								if ((Page)_downloadPageInstance == (Page)null)
								{
									_downloadPageInstance = new DownloadPage();
								}
								((ContentControl)ContentFrame).Content = _downloadPageInstance;
								break;
							}
						}
						AnimationHelper.PlayEntranceAnimation((UIElement)ContentFrame);
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

		private void ShowPlayerBar()
		{
			if ((int)((UIElement)PlayerBar).Visibility > 0)
			{
				((UIElement)PlayerBar).Visibility = Visibility.Visible;
				((UIElement)PlayerBar).Opacity = 0.0;
				((FrameworkElement)PlayerBar).Height = 0.0;
				if (App.Settings.Current.PlayerGradientOverflowEnabled && App.Settings.Current.Backdrop == AppBackdropStyle.Solid)
				{
					((UIElement)PlayerGradientOverflow).Visibility = Visibility.Visible;
					((UIElement)PlayerGradientFadeLayer).Visibility = Visibility.Visible;
				}
				TranslateTransform val = new TranslateTransform
				{
					Y = 15.0
				};
				((UIElement)PlayerBar).RenderTransform = (Transform)val;
				Storyboard val2 = new Storyboard();
				DoubleAnimation val3 = new DoubleAnimation
				{
					From = 0.0,
					To = 1.0,
					Duration = new Duration(TimeSpan.FromMilliseconds(250.0)),
					EasingFunction = (EasingFunctionBase)new QuadraticEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)PlayerBar);
				Storyboard.SetTargetProperty((Timeline)(object)val3, "Opacity");
				DoubleAnimation val4 = new DoubleAnimation
				{
					From = 15.0,
					To = 0.0,
					Duration = new Duration(TimeSpan.FromMilliseconds(300.0)),
					EasingFunction = (EasingFunctionBase)new QuadraticEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				Storyboard.SetTarget((Timeline)(object)val4, (DependencyObject)val);
				Storyboard.SetTargetProperty((Timeline)(object)val4, "Y");
				DoubleAnimation val5 = new DoubleAnimation
				{
					From = 0.0,
					To = 98.0,
					Duration = new Duration(TimeSpan.FromMilliseconds(300.0)),
					EasingFunction = (EasingFunctionBase)new QuadraticEase
					{
						EasingMode = EasingMode.EaseOut
					},
					EnableDependentAnimation = true
				};
				Storyboard.SetTarget((Timeline)(object)val5, (DependencyObject)PlayerBar);
				Storyboard.SetTargetProperty((Timeline)(object)val5, "Height");
				val2.Children.Add((Timeline)(object)val3);
				val2.Children.Add((Timeline)(object)val4);
				val2.Children.Add((Timeline)(object)val5);
				((Timeline)val2).Completed += delegate
				{
					((DependencyObject)PlayerBar).ClearValue(FrameworkElement.HeightProperty);
					((UIElement)PlayerBar).RenderTransform = (Transform)new TranslateTransform();
				};
				val2.Begin();
			}
		}

		public void RestoreSidebarSelection()
		{
			object obj = RootNav.SelectedItem ?? _lastNavSelectedItem;
			NavigationViewItem val = obj as NavigationViewItem;
			if (val != (NavigationViewItem)null)
			{
				object settingsItem = RootNav.SettingsItem;
				bool isSettings = (settingsItem as NavigationViewItem) == val;
				RootNav.SelectedItem = val;
				NavigateToSidebarItem(((FrameworkElement)val).Tag?.ToString(), isSettings);
			}
		}

		private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			Microsoft.UI.Input.PointerPointProperties properties = e.GetCurrentPoint((UIElement)RootGrid).Properties;
			LastClickWasXButton = (int)properties.PointerUpdateKind == 7 || properties.IsXButton1Pressed;
			if (LastClickWasXButton)
			{
				if ((!(((ContentControl)ContentFrame).Content is PlaylistsPage playlistsPage) || !playlistsPage.TryGoBack()) && (!(((ContentControl)ContentFrame).Content is LibraryPage libraryPage) || !libraryPage.TryGoBack()))
				{
					if (((ContentControl)ContentFrame).Content is PlaylistDetailPage)
					{
						RestoreSidebarSelection();
					}
					else if (ContentFrame.CanGoBack)
					{
						ContentFrame.GoBack();
					}
				}
				e.Handled = true;
			}
			else if (properties.IsXButton2Pressed && ContentFrame.CanGoForward)
			{
				ContentFrame.GoForward();
				e.Handled = true;
			}
			HideInfoOverlay();
		}

		private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
		}

		private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			_isSliderDragging = true;
			if (sender is Slider slider)
			{
				Microsoft.UI.Input.PointerPoint point = e.GetCurrentPoint((UIElement)slider);
				double width = ((FrameworkElement)slider).ActualWidth;
				if (width > 0.0)
				{
					double ratio = Math.Clamp(point.Position.X / width, 0.0, 1.0);
					double newValue = ((RangeBase)slider).Minimum + ratio * (((RangeBase)slider).Maximum - ((RangeBase)slider).Minimum);
					((RangeBase)slider).Value = newValue;
				}
			}
		}

		private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			TimeSpan totalDuration = App.AudioEngine.TotalDuration;
			if (totalDuration.TotalSeconds > 0.0)
			{
				App.AudioEngine.Seek(TimeSpan.FromSeconds(((RangeBase)ProgressSlider).Value));
				if ((int)App.AudioEngine.State == 0 && ((RangeBase)ProgressSlider).Value < totalDuration.TotalSeconds)
				{
					App.AudioEngine.Resume();
					App.DiscordRpc?.UpdatePresence(App.AudioEngine.CurrentTrack, isPlaying: true);
					try
					{
						if (_smtc != (SystemMediaTransportControls)null)
						{
							_smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
						}
					}
					catch
					{
					}
					PlayPauseIcon.Glyph = "\ue769";
					if (MiniPlayPauseIcon != (FontIcon)null)
					{
						MiniPlayPauseIcon.Glyph = "\ue769";
					}
					((FrameworkElement)PlayPauseIcon).Margin = new Thickness(0.0);
					if (MiniPlayPauseIcon != (FontIcon)null)
					{
						((FrameworkElement)MiniPlayPauseIcon).Margin = new Thickness(0.0);
					}
					UpdateTaskbarPlayPauseIcon();
				}
			}
			_isSliderDragging = false;
		}

		private void AudioEngine_PlaybackStopped(object? sender, EventArgs e)
		{
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
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
						if (_playbackFuture.Count > 0)
						{
							PlayTrack(_playbackFuture.Pop(), _queue, isGoingBack: false, isGoingForward: true);
						}
						else
						{
							PlayTrack(PickRandomTrack(), _queue, isGoingBack: false, isGoingForward: true);
						}
						break;
					case PlaybackMode.RepeatAll:
						PlayTrack(_queue[(_queueIndex + 1) % _queue.Count], _queue);
						break;
					case PlaybackMode.Off:
						PlayPauseIcon.Glyph = "\ue768";
						if (MiniPlayPauseIcon != (FontIcon)null)
						{
							MiniPlayPauseIcon.Glyph = "\ue768";
						}
						((FrameworkElement)PlayPauseIcon).Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
						if (MiniPlayPauseIcon != (FontIcon)null)
						{
							((FrameworkElement)MiniPlayPauseIcon).Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
						}
						UpdateTaskbarPlayPauseIcon();
						break;
					}
				}
			});
		}

		private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
		{
			if (App.Settings.Current.MinimizeToTrayOnClose)
			{
				args.Cancel = true;
				((Window)this).AppWindow.Hide();
				return;
			}
			bool saveWindowSize = App.Settings.Current.SaveWindowSize;
			bool saveWindowPosition = App.Settings.Current.SaveWindowPosition;
			if (saveWindowSize | saveWindowPosition)
			{
				if (saveWindowSize)
				{
					App.Settings.Current.WindowWidth = ((Window)this).AppWindow.Size.Width;
					App.Settings.Current.WindowHeight = ((Window)this).AppWindow.Size.Height;
				}
				if (saveWindowPosition)
				{
					App.Settings.Current.WindowX = ((Window)this).AppWindow.Position.X;
					App.Settings.Current.WindowY = ((Window)this).AppWindow.Position.Y;
				}
				App.Settings.SaveSync();
			}
		}

		public void UpdateMiniPlayerButtonVisibility()
		{
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				if (MiniPlayerToggleBtn != (Button)null)
				{
					((UIElement)MiniPlayerToggleBtn).Visibility = (App.Settings.Current.EnableMiniPlayerButton ? Visibility.Visible : Visibility.Collapsed);
				}
				UpdatePlaybackControlsCentering();
			});
		}

		public void UpdatePlayerBarButtonsVisibility()
		{
			((Window)this).DispatcherQueue.TryEnqueue((DispatcherQueueHandler)delegate
			{
				if (NowPlayingFavoriteButton != (Button)null)
				{
					((UIElement)NowPlayingFavoriteButton).Visibility = (App.Settings.Current.EnableFavoriteButton ? Visibility.Visible : Visibility.Collapsed);
				}
				if (EqualizerQuickButton != (Button)null)
				{
					((UIElement)EqualizerQuickButton).Visibility = (App.Settings.Current.EnableEqualizerQuickButton ? Visibility.Visible : Visibility.Collapsed);
				}
				UpdatePlaybackControlsCentering();
			});
		}

		public void UpdateUpNextPanelVisibility()
		{
			if (UpNextToggleBtn != (Button)null)
			{
				((UIElement)UpNextToggleBtn).Visibility = (App.Settings.Current.EnableUpNextPanel ? Visibility.Visible : Visibility.Collapsed);
				ApplyLyricsButtonVisibility();
				if (!App.Settings.Current.EnableUpNextPanel && _isUpNextPanelOpen)
				{
					_isUpNextPanelOpen = false;
					((UIElement)UpNextPanel).Visibility = Visibility.Collapsed;
				}
			}
		}

		private void UpNextToggleBtn_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				_isUpNextPanelOpen = !_isUpNextPanelOpen;
				if (_isUpNextPanelOpen)
				{
					((UIElement)UpNextOverlay).Visibility = Visibility.Visible;
					((UIElement)UpNextPanel).Visibility = Visibility.Visible;
					UpdateUpNextPanel();
					DoubleAnimation val = new DoubleAnimation
					{
						From = 520.0,
						To = 0.0,
						Duration = TimeSpan.FromMilliseconds(400.0),
						EasingFunction = (EasingFunctionBase)new ExponentialEase
						{
							EasingMode = EasingMode.EaseOut,
							Exponent = 6.0
						}
					};
					Storyboard val2 = new Storyboard();
					val2.Children.Add((Timeline)(object)val);
					Storyboard.SetTarget((Timeline)(object)val, (DependencyObject)UpNextTransform);
					Storyboard.SetTargetProperty((Timeline)(object)val, "Y");
					val2.Begin();
				}
				else
				{
					((UIElement)UpNextOverlay).Visibility = Visibility.Collapsed;
					DoubleAnimation val3 = new DoubleAnimation
					{
						From = 0.0,
						To = 520.0,
						Duration = TimeSpan.FromMilliseconds(250.0),
						EasingFunction = (EasingFunctionBase)new ExponentialEase
						{
							EasingMode = EasingMode.EaseIn,
							Exponent = 4.0
						}
					};
					Storyboard val4 = new Storyboard();
					val4.Children.Add((Timeline)(object)val3);
					Storyboard.SetTarget((Timeline)(object)val3, (DependencyObject)UpNextTransform);
					Storyboard.SetTargetProperty((Timeline)(object)val3, "Y");
					((Timeline)val4).Completed += delegate
					{
						((UIElement)UpNextPanel).Visibility = Visibility.Collapsed;
					};
					val4.Begin();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
		}

		private void UpNextOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			if (_isUpNextPanelOpen)
			{
				UpNextToggleBtn_Click(this, new RoutedEventArgs());
			}
		}

		private void UpdateUpNextPanel()
		{
			if (!_isUpNextPanelOpen || UpNextPanel == (Grid)null)
			{
				return;
			}
			Track track = _library.FirstOrDefault((Track t) => t.Id == _nowPlayingId);
			if (track != null)
			{
				UpNextCurrentTitle.Text = track.Title;
				UpNextCurrentArtist.Text = track.Artist;
				string value = (string.IsNullOrEmpty(track.Album) ? Strings.Current.CS_AlbumInconnu : track.Album);
				string value2 = ((track.Year > 0) ? $" • {track.Year}" : "");
				string value3 = (_isCurrentlyPlayingManualQueue ? Strings.Current.MainWindow_UpNext_ManualQueue : (_queueSourceName ?? Strings.Current.MainWindow_UpNext_FromLibrary));
				UpNextCurrentAlbum.Text = $"{value}{value2} • {track.DurationDisplay} • {value3}";
				((UIElement)UpNextCurrentSource).Visibility = Visibility.Collapsed;
				if (!string.IsNullOrEmpty(track.CoverArtPath))
				{
					ImageBrush val = new ImageBrush
					{
						Stretch = Stretch.UniformToFill
					};
					try
					{
						val.ImageSource = (ImageSource)new BitmapImage(new Uri(track.CoverArtPath));
						UpNextCurrentCoverBorder.Background = (Brush)val;
					}
					catch
					{
						UpNextCurrentCoverBorder.Background = null;
					}
				}
				else
				{
					UpNextCurrentCoverBorder.Background = null;
				}
			}
			_upNextDisplayList.Clear();
			if (_manualQueue.Count > 0)
			{
				_upNextDisplayList.AddRange(_manualQueue);
			}
			if (_playbackMode == PlaybackMode.Shuffle)
			{
				if (_playbackFuture != null && _playbackFuture.Count > 0)
				{
					_upNextDisplayList.AddRange(_playbackFuture);
				}
				int count = Math.Max(0, 30 - _upNextDisplayList.Count);
				_upNextDisplayList.AddRange(_shuffleUpcoming.Take(count));
				UpNextHeader.Text = Strings.Current.MainWindow_UpNext_Header + (Strings.Current.IsFr ? " (aléatoire)" : " (shuffle)");
			}
			else
			{
				if (_queue != null && _queueIndex >= 0 && _queueIndex < _queue.Count - 1)
				{
					_upNextDisplayList.AddRange(_queue.Skip(_queueIndex + 1).Take(50));
				}
				UpNextHeader.Text = Strings.Current.MainWindow_UpNext_Header;
			}
			if (((ItemsControl)UpNextListView).ItemsSource == null)
			{
				((ItemsControl)UpNextListView).ItemsSource = _upNextObsList;
			}
			if (_upNextObsList.SequenceEqual(_upNextDisplayList))
			{
				return;
			}
			_upNextObsList.Clear();
			foreach (Track upNextDisplay in _upNextDisplayList)
			{
				_upNextObsList.Add(upNextDisplay);
			}
		}

		private void UpNextListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			if (e.ClickedItem is Track track)
			{
				if (_manualQueue.Contains(track))
				{
					_manualQueue.Remove(track);
					PlayTrack(track, null, isGoingBack: false, isGoingForward: false, null, fromManualQueue: true);
					SaveManualQueue();
				}
				else
				{
					PlayTrack(track, _queue);
				}
			}
		}

		private void UpNextRemoveBtn_Click(object sender, RoutedEventArgs e)
		{
			Button val = sender as Button;
			if (val == null || !(((FrameworkElement)val).Tag is Track item))
			{
				return;
			}
			if (_manualQueue.Remove(item))
			{
				_queuePageInstance?.SetQueue(_manualQueue);
				SaveManualQueue();
			}
			else if (_playbackMode == PlaybackMode.Shuffle)
			{
				_shuffleUpcoming.Remove(item);
				if (_queue != null)
				{
					int num = _queue.IndexOf(item);
					if (num >= 0)
					{
						_queue.RemoveAt(num);
						if (num <= _queueIndex)
						{
							_queueIndex = Math.Max(0, _queueIndex - 1);
						}
					}
				}
			}
			else if (_queue != null)
			{
				int num2 = _queue.IndexOf(item);
				if (num2 >= 0)
				{
					_queue.RemoveAt(num2);
					if (num2 <= _queueIndex)
					{
						_queueIndex = Math.Max(0, _queueIndex - 1);
					}
				}
			}
			UpdateUpNextPanel();
		}

		private void UpNextListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
		{
			if (!(((ItemsControl)UpNextListView).ItemsSource is ObservableCollection<Track> observableCollection))
			{
				return;
			}
			_manualQueue.Clear();
			List<Track> list = new List<Track>();
			foreach (Track item in observableCollection)
			{
				list.Add(item);
			}
			if (_playbackMode == PlaybackMode.Shuffle)
			{
				_shuffleUpcoming.Clear();
				_shuffleUpcoming.AddRange(list);
			}
			else if (_queue != null && _queueIndex >= 0)
			{
				List<Track> collection = _queue.Take(_queueIndex + 1).ToList();
				_queue.Clear();
				_queue.AddRange(collection);
				_queue.AddRange(list);
			}
			_queuePageInstance?.SetQueue(_manualQueue);
			SaveManualQueue();
		}

		private void ShowMiniPlayer_Click(object sender, RoutedEventArgs e)
		{
			ShowMiniPlayer();
		}

		public void ShowMiniPlayer()
		{
			if (!_isMiniPlayerMode)
			{
				_isMiniPlayerMode = true;
				_savedMainWindowWidth = ((Window)this).AppWindow.Size.Width;
				_savedMainWindowHeight = ((Window)this).AppWindow.Size.Height;
				_savedMainWindowX = ((Window)this).AppWindow.Position.X;
				_savedMainWindowY = ((Window)this).AppWindow.Position.Y;
				((UIElement)AppTitleBar).Visibility = Visibility.Collapsed;
				((UIElement)RootNav).Visibility = Visibility.Collapsed;
				((UIElement)PlayerBar).Visibility = Visibility.Collapsed;
				((UIElement)MiniPlayerGrid).Visibility = Visibility.Visible;
				AppWindowPresenter presenter = ((Window)this).AppWindow.Presenter;
				OverlappedPresenter val = presenter as OverlappedPresenter;
				if (val != null)
				{
					val.SetBorderAndTitleBar(false, false);
					val.IsAlwaysOnTop = App.Settings.Current.MiniPlayerAlwaysOnTop;
				}
				DisplayArea fromWindowId = DisplayArea.GetFromWindowId(((Window)this).AppWindow.Id, DisplayAreaFallback.Nearest);
				RectInt32 workArea = fromWindowId.WorkArea;
				int num = 340;
				int num2 = 600;
				int num3 = workArea.X + workArea.Width - num - 24;
				int num4 = workArea.Y + workArea.Height - num2 - 24;
				((Window)this).AppWindow.MoveAndResize(new RectInt32(num3, num4, num, num2));
				// Re-enregistre la zone de drag pour la nouvelle geometrie de fenetre : la zone
				// declaree sur AppTitleBar correspond a la fenetre principale (pleine largeur, en
				// haut) et devient incoherente une fois la fenetre redimensionnee/repositionnee en
				// mini-lecteur, ce qui provoquait un saut au premier glisser-deposer.
				try
				{
					((Window)this).SetTitleBar((UIElement)MiniPlayerDragHandle);
				}
				catch { }
				UpdateMiniPlayerUI();
			}
		}

		public async void RestoreMainWindow()
		{
			if (!_isMiniPlayerMode)
			{
				((Window)this).AppWindow.Show();
				return;
			}
			_isMiniPlayerMode = false;
			((UIElement)RootGrid).Opacity = 0.0;
			((UIElement)MiniPlayerGrid).Visibility = Visibility.Collapsed;
			((UIElement)AppTitleBar).Visibility = Visibility.Visible;
			((UIElement)RootNav).Visibility = Visibility.Visible;
			if (App.AudioEngine.CurrentTrack != null)
			{
				((UIElement)PlayerBar).Visibility = Visibility.Visible;
			}
			((Window)this).AppWindow.Hide();
			await Task.Delay(30);
			AppWindowPresenter presenter = ((Window)this).AppWindow.Presenter;
			OverlappedPresenter val = presenter as OverlappedPresenter;
			if (val != null)
			{
				val.SetBorderAndTitleBar(true, true);
				val.IsAlwaysOnTop = false;
			}
			((Window)this).AppWindow.MoveAndResize(new RectInt32(_savedMainWindowX, _savedMainWindowY, _savedMainWindowWidth, _savedMainWindowHeight));
			try
			{
				((Window)this).SetTitleBar((UIElement)AppTitleBar);
			}
			catch { }
			await Task.Delay(30);
			UpdateGradientOverflowLayout();
			((UIElement)RootGrid).Opacity = 1.0;
			((Window)this).AppWindow.Show();
		}

		private void RestoreMainWindow_Click(object sender, RoutedEventArgs e)
		{
			RestoreMainWindow();
		}

		private void TrayToggle_Click(object sender, RoutedEventArgs e)
		{
			if (((Window)this).AppWindow.IsVisible && !_isMiniPlayerMode)
			{
				((Window)this).AppWindow.Hide();
			}
			else
			{
				RestoreMainWindow();
			}
		}

		private void TrayExit_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Exit();
		}

		private void TrayIcon_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
		{
			RestoreMainWindow();
		}

		private void UpdateMiniPlayerUI(Track track = null)
		{
			if (!_isMiniPlayerMode)
			{
				return;
			}
			if (track == null)
			{
				track = App.AudioEngine.CurrentTrack;
			}
			UpdateRepeatButtonVisual();
			if (track != null)
			{
				MiniTitle.Text = track.Title;
				MiniArtist.Text = track.Artist;
				if (!string.IsNullOrEmpty(track.CoverArtPath))
				{
					BitmapImage val = new BitmapImage(new Uri(track.CoverArtPath));
					MiniPlayerBackground.Background = (Brush)new ImageBrush
					{
						ImageSource = (ImageSource)val,
						Stretch = Stretch.UniformToFill
					};
					if (MiniCoverImage != (Image)null)
					{
						MiniCoverImage.Source = (ImageSource)val;
					}
				}
				else
				{
					MiniPlayerBackground.Background = null;
					if (MiniCoverImage != (Image)null)
					{
						MiniCoverImage.Source = null;
					}
				}
				if (MiniPlayerColorTint != (Border)null)
				{
					if (_gradientStartColor.HasValue)
					{
						MiniPlayerColorTint.Background = (Brush)new SolidColorBrush(_gradientStartColor.Value);
					}
					else
					{
						MiniPlayerColorTint.Background = (Brush)new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)30, (byte)30, (byte)30));
					}
				}
			}
			else
			{
				MiniTitle.Text = "";
				MiniArtist.Text = "";
				MiniPlayerBackground.Background = null;
				if (MiniCoverImage != (Image)null)
				{
					MiniCoverImage.Source = null;
				}
			}
		}

}
