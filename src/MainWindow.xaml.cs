using System.Text.Json;
using Microsoft.UI;
using Windows.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.UI;
using Windows.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Text;
using System.Text.Json.Serialization;
using Windows.Storage;
using Windows.Media;
using Microsoft.UI.Dispatching;
using NAudio.Wave;
using System.Runtime.CompilerServices;
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


	[System.Runtime.InteropServices.ComImport]
	[System.Runtime.InteropServices.Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
	[System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
	public interface ITaskbarList3
	{
		void HrInit();
		void AddTab(IntPtr hwnd);
		void DeleteTab(IntPtr hwnd);
		void ActivateTab(IntPtr hwnd);
		void SetActiveAlt(IntPtr hwnd);
		void MarkFullscreenWindow(IntPtr hwnd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool fFullscreen);
		void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
		void SetProgressState(IntPtr hwnd, int tbpFlags);
		void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
		void UnregisterTab(IntPtr hwndTab);
		void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
		void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
		void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPArray)] THUMBBUTTON[] pButton);
		void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPArray)] THUMBBUTTON[] pButton);
		void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
		void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszDescription);
		void SetThumbnailTooltip(IntPtr hwnd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszTip);
		void SetThumbnailClip(IntPtr hwnd, ref RECT prcClip);
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
	public struct THUMBBUTTON
	{
		public uint dwMask;
		public uint iId;
		public uint iBitmap;
		public IntPtr hIcon;
		[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szTip;
		public uint dwFlags;
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	public struct RECT { public int left; public int top; public int right; public int bottom; }

	[System.Runtime.InteropServices.ComImport]
	[System.Runtime.InteropServices.Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
	[System.Runtime.InteropServices.ClassInterface(System.Runtime.InteropServices.ClassInterfaceType.None)]
	public class TaskbarInstance { }


	[System.Runtime.InteropServices.ComImport]
	[System.Runtime.InteropServices.Guid("ddb0472d-c911-4a1f-86d9-dc3d71a95f5a")]
	[System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
	public interface IMySMTCInterop
	{
		Windows.Media.SystemMediaTransportControls GetForWindow(IntPtr appWindow, [System.Runtime.InteropServices.In] ref Guid riid);
	}

public sealed partial class MainWindow : Window
{
    private string? _pendingNavTag;
    private bool _pendingNavIsSettings;
    private object? _pendingNavParameter;
    private bool _isNavigating;

    public static bool LastClickWasXButton;


	[System.Runtime.InteropServices.ComImport]
	[System.Runtime.InteropServices.Guid("00021401-0000-0000-C000-000000000046")]
	internal class ShellLink { }

	[System.Runtime.InteropServices.ComImport]
	[System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
	[System.Runtime.InteropServices.Guid("000214F9-0000-0000-C000-000000000046")]
	internal interface IShellLink
	{
		void GetPath([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
		void GetIDList(out IntPtr ppidl);
		void SetIDList(IntPtr pidl);
		void GetDescription([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
		void SetDescription([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszName);
		void GetWorkingDirectory([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
		void SetWorkingDirectory([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszDir);
		void GetArguments([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
		void SetArguments([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszArgs);
		void GetHotkey(out short pwHotkey);
		void SetHotkey(short wHotkey);
		void GetShowCmd(out int piShowCmd);
		void SetShowCmd(int iShowCmd);
		void GetIconLocation([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
		void SetIconLocation([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
		void SetRelativePath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
		void Resolve(IntPtr hwnd, int fFlags);
		void SetPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszFile);
	}

	[System.Runtime.InteropServices.ComImport]
	[System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
	[System.Runtime.InteropServices.Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
	internal interface IPropertyStore
	{
		void GetCount(out uint cProps);
		void GetAt(uint iProp, out object pkey);
		void GetValue(ref PropertyKey key, out object pv);
		void SetValue(ref PropertyKey key, ref PropVariant propvar);
		void Commit();
	}

	[System.Runtime.InteropServices.ComImport]
	[System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
	[System.Runtime.InteropServices.Guid("0000010b-0000-0000-C000-000000000046")]
	internal interface IPersistFile
	{
		void GetClassID(out Guid pClassID);
		[System.Runtime.InteropServices.PreserveSig]
		int IsDirty();
		void Load([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
		void Save([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszFileName, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool fRemember);
		void SaveCompleted([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszFileName);
		void GetCurFile([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder ppszFileName);
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	internal struct PropertyKey
	{
		public Guid fmtid;
		public uint pid;
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
	internal struct PropVariant
	{
		[System.Runtime.InteropServices.FieldOffset(0)]
		public ushort vt;
		[System.Runtime.InteropServices.FieldOffset(8)]
		public IntPtr pwszVal;
	}

	private void CreateStartMenuShortcut()
	{
		try
		{
			string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
			string shortcutPath = System.IO.Path.Combine(startMenu, "Resona.lnk");
			
			// always overwrite

			IShellLink link = (IShellLink)new ShellLink();
			string exePath = Environment.ProcessPath;
			link.SetPath(exePath);
			link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(exePath));
			link.SetDescription("Resona");

			IPropertyStore propertyStore = (IPropertyStore)link;
			PropertyKey appUserModelIdKey = new PropertyKey
			{
				fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
				pid = 5
			};

			PropVariant propVar = new PropVariant();
			propVar.vt = 31; // VT_LPWSTR
			propVar.pwszVal = System.Runtime.InteropServices.Marshal.StringToCoTaskMemUni("Resona");

			propertyStore.SetValue(ref appUserModelIdKey, ref propVar);
			propertyStore.Commit();

			IPersistFile persistFile = (IPersistFile)link;
			persistFile.Save(shortcutPath, false);

			System.Runtime.InteropServices.Marshal.FreeCoTaskMem(propVar.pwszVal);
		}
		catch { }
	}


	private ITaskbarList3 _taskbar;
	private THUMBBUTTON _thumbPlay, _thumbPrev, _thumbNext;
	private IntPtr _hIconPlay, _hIconPause;

	

	private void InitTaskbarThumbnailButtons()
	{
		try
		{
			IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
			
			var taskbar = (ITaskbarList3)new TaskbarInstance();
			taskbar.HrInit();

			IntPtr hIconPrev = GetIconForText("\ue76b"); // Previous
			IntPtr hIconPlay = GetIconForText("\ue768"); // Play
			IntPtr hIconPause = GetIconForText("\ue769"); // Pause
			IntPtr hIconNext = GetIconForText("\ue76c"); // Next

            _hIconPlay = hIconPlay;
            _hIconPause = hIconPause;

			_thumbPrev = new THUMBBUTTON { dwMask = 1 | 2 | 4 | 8, iId = 101, hIcon = hIconPrev, szTip = "PrÃƒÆ’Ã‚Â©cÃƒÆ’Ã‚Â©dent", dwFlags = 0 };
			_thumbPlay = new THUMBBUTTON { dwMask = 1 | 2 | 4 | 8, iId = 100, hIcon = hIconPlay, szTip = "Lecture/Pause", dwFlags = 0 };
			_thumbNext = new THUMBBUTTON { dwMask = 1 | 2 | 4 | 8, iId = 102, hIcon = hIconNext, szTip = "Suivant", dwFlags = 0 };

			taskbar.ThumbBarAddButtons(hwnd, 3, new THUMBBUTTON[] { _thumbPrev, _thumbPlay, _thumbNext });
            
            _taskbar = taskbar;

			
		}
		catch { }
	}

	

	private IntPtr GetIconForText(string text)
	{
		try {
			using (var bmp = new System.Drawing.Bitmap(32, 32))
			using (var g = System.Drawing.Graphics.FromImage(bmp))
			{
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
				using (var font = new System.Drawing.Font("Segoe MDL2 Assets", 16))
				using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
				{
					var size = g.MeasureString(text, font);
					g.DrawString(text, font, brush, (32 - size.Width) / 2, (32 - size.Height) / 2);
				}
				return bmp.GetHicon();
			}
		} catch { return IntPtr.Zero; }
	}


	[System.Runtime.InteropServices.DllImport("combase.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
	private static extern void RoGetActivationFactory([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.HString)] string activatableClassId, [System.Runtime.InteropServices.In] ref Guid iid, [System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] out object factory);


	

	
	
	private Windows.Media.Playback.MediaPlayer _smtcPlayer;
	private Windows.Media.SystemMediaTransportControls _smtc;

	private void InitSMTC()
	{
		try
		{
			_smtcPlayer = new Windows.Media.Playback.MediaPlayer();
			_smtcPlayer.CommandManager.IsEnabled = true;
			_smtcPlayer.CommandManager.PlayReceived += (s, e) => { e.Handled = true; DispatcherQueue.TryEnqueue(() => PlayPauseButton_Click(null, null)); };
			_smtcPlayer.CommandManager.PauseReceived += (s, e) => { e.Handled = true; DispatcherQueue.TryEnqueue(() => PlayPauseButton_Click(null, null)); };
			_smtcPlayer.CommandManager.NextReceived += (s, e) => { e.Handled = true; DispatcherQueue.TryEnqueue(() => NextButton_Click(null, null)); };
			_smtcPlayer.CommandManager.PreviousReceived += (s, e) => { e.Handled = true; DispatcherQueue.TryEnqueue(() => PrevButton_Click(null, null)); };

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
			System.IO.File.WriteAllText("smtc_error.txt", ex.ToString());
		}
	}




	
	
    private void UpdateTaskbarPlayPauseIcon(bool? forcePlaying = null)
    {
        try {
            if (_taskbar != null)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                if (forcePlaying ?? (App.AudioEngine.State == NAudio.Wave.PlaybackState.Playing))
                {
                    _thumbPlay.hIcon = _hIconPause;
                    _thumbPlay.szTip = "Pause";
                }
                else
                {
                    _thumbPlay.hIcon = _hIconPlay;
                    _thumbPlay.szTip = "Lecture";
                }
                _taskbar.ThumbBarUpdateButtons(hwnd, 1, new THUMBBUTTON[] { _thumbPlay });
            }
        } catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "resona_err.log"), ex.ToString()); }
    }
    public void SetNowPlayingMode(bool isActive, string? trackId)
    {
        bool wasActive = _isNowPlayingModeActive;
        _isNowPlayingModeActive = isActive;
        if (isActive)
        {
            var track = _library.FirstOrDefault(t => t.Id == trackId) ?? _queue.FirstOrDefault(t => t.Id == trackId);
            if (track != null) UpdateNowPlayingBackground(track.CoverArtPath);
            
            if (wasActive) {
                UpdatePlayerButtonsColor();
                return;
            }
            
            NowPlayingBlurredBackground.Visibility = Visibility.Visible; NowPlayingDimOverlay.Visibility = Visibility.Visible; NowPlayingCenterCoverBorder.IsHitTestVisible = true;
            PlayerGradientFadeLayer.Opacity = 1; PlayerGradientColorLayer.Opacity = 1; PlayerGradientOverflow.Opacity = 0;
            
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var anim1 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim1, NowPlayingBlurredBackground);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim1, "Opacity");
            sb.Children.Add(anim1);
            
            var anim2 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0.6, Duration = TimeSpan.FromMilliseconds(400) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim2, NowPlayingDimOverlay);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim2, "Opacity");
            sb.Children.Add(anim2);

            var anim3 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim3, NowPlayingCenterCoverBorder);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim3, "Opacity");
            sb.Children.Add(anim3);

            NowPlayingCenterCoverTransform.X = 0;
            var animMoveIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = -5, Duration = TimeSpan.FromMilliseconds(400) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animMoveIn, NowPlayingCenterCoverTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animMoveIn, "Y");
            sb.Children.Add(animMoveIn);

            var anim4 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150) }; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim4, NowPlayingCoverBorder); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim4, "Opacity"); sb.Children.Add(anim4); var anim4Y = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 20, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim4Y, NowPlayingCoverTransform); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim4Y, "Y"); sb.Children.Add(anim4Y);

            var anim5 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = -58, Duration = TimeSpan.FromMilliseconds(150), BeginTime = TimeSpan.FromMilliseconds(80), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim5, NowPlayingTextTransform); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim5, "X"); sb.Children.Add(anim5); NowPlayingTextContainer.MaxWidth = 258;
            
            sb.Begin();
            
            if (trackId != null && trackId == _nowPlayingId)
            {
                UpdatePlayerButtonsColor();
            }
        }
        else
        {
            
            
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            
            
            
             var animOverflow = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(300) }; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animOverflow, PlayerGradientOverflow); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animOverflow, "Opacity"); sb.Children.Add(animOverflow);
            var anim1 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(300) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim1, NowPlayingBlurredBackground);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim1, "Opacity");
            sb.Children.Add(anim1);
            
            var anim2 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(300) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim2, NowPlayingDimOverlay);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim2, "Opacity");
            sb.Children.Add(anim2);

            var anim3 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim3, NowPlayingCenterCoverBorder);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim3, "Opacity");
            sb.Children.Add(anim3);

            var animMoveOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = -5, Duration = TimeSpan.FromMilliseconds(150) };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animMoveOut, NowPlayingCenterCoverTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animMoveOut, "Y");
            sb.Children.Add(animMoveOut);

            NowPlayingCenterCoverBorder.IsHitTestVisible = false;
            var anim4 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(150), BeginTime = TimeSpan.FromMilliseconds(100) }; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim4, NowPlayingCoverBorder); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim4, "Opacity"); sb.Children.Add(anim4); var anim4Y = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim4Y, NowPlayingCoverTransform); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim4Y, "Y"); sb.Children.Add(anim4Y); var anim5 = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim5, NowPlayingTextTransform); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim5, "X"); sb.Children.Add(anim5); NowPlayingTextContainer.MaxWidth = 200;
            
            sb.Completed += (s, e) => {
                if (ContentFrame.Content is not NowPlayingPage)
                {
                    NowPlayingBlurredBackground.Visibility = Visibility.Collapsed; NowPlayingDimOverlay.Visibility = Visibility.Collapsed; 
                    NowPlayingBlurredBackground.Source = null;
                }
            };
            sb.Begin();
        }
        UpdatePlayerButtonsColor();
    }

    public async void UpdateNowPlayingBackground(string? coverPath)
    {
        if (string.IsNullOrEmpty(coverPath))
        {
            NowPlayingBlurredBackground.Source = null;
            NowPlayingCenterCoverImage.Source = null;
            NowPlayingCenterPlaceholder.Visibility = Visibility.Visible;
            return;
        }
        try
        {
            NowPlayingCenterCoverImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(coverPath));
            NowPlayingCenterPlaceholder.Visibility = Visibility.Collapsed;

            var bytes = await Task.Run(() => {
                byte[] fileBytes = System.IO.File.ReadAllBytes(coverPath);
                using var msBmp = new System.IO.MemoryStream(fileBytes);
                using var original = System.Drawing.Image.FromStream(msBmp);
                using var small = new System.Drawing.Bitmap(original, new System.Drawing.Size(32, 32));
                var blurred = new System.Drawing.Bitmap(512, 512);
                using (var g = System.Drawing.Graphics.FromImage(blurred))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(small, 0, 0, 512, 512);
                }
                using var ms = new System.IO.MemoryStream();
                blurred.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                return ms.ToArray();
            });

            using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            using var dw = new Windows.Storage.Streams.DataWriter(ras.GetOutputStreamAt(0));
            dw.WriteBytes(bytes);
            await dw.StoreAsync();
            ras.Seek(0);
            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            await bmp.SetSourceAsync(ras);
            NowPlayingBlurredBackground.Source = bmp;
        }
        catch { }
    }

        private void AnimateTrackChange(bool isGoingBack)
    {
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        
        // Initial state: shifted left or right, and transparent
        double startX = isGoingBack ? -100 : 100;
        NowPlayingCenterCoverTransform.X = startX;
        NowPlayingCenterCoverBorder.Opacity = 0;
        NowPlayingBlurredBackground.Opacity = 0;
        
        // Animate X to 0
        var animX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { 
            To = 0, 
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animX, NowPlayingCenterCoverTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animX, "X");
        
        // Animate Opacity to 1
        var animOpacity = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { 
            To = 1, 
            Duration = TimeSpan.FromMilliseconds(400) 
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animOpacity, NowPlayingCenterCoverBorder);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animOpacity, "Opacity");

        // Animate Background Opacity to 1
        var animBgOpacity = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { 
            To = 1, 
            Duration = TimeSpan.FromMilliseconds(600) 
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animBgOpacity, NowPlayingBlurredBackground);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animBgOpacity, "Opacity");
        
        sb.Children.Add(animX);
        sb.Children.Add(animOpacity);
        sb.Children.Add(animBgOpacity);
        sb.Begin();
    }

    public void NavigateToNowPlaying(Track track, bool isGoingBack = false) { 
    if (NowPlayingCenterCoverBorder.RenderTransform is Microsoft.UI.Xaml.Media.CompositeTransform ct) { ct.ScaleX = 1.0; ct.ScaleY = 1.0; }
    SettingsContainer.Visibility = Visibility.Collapsed; ContentFrame.Visibility = Visibility.Visible; if (ContentFrame.Content is Views.NowPlayingPage existingPage)
        {
            existingPage.UpdateTrackInfo(track);
            SetNowPlayingMode(true, track.Id);
            AnimateTrackChange(isGoingBack);
            return;
        }
        var page = new Views.NowPlayingPage();
        ContentFrame.Content = page;
        page.UpdateTrackInfo(track); if (RootNav.SelectedItem != null) _lastNavSelectedItem = RootNav.SelectedItem; RootNav.SelectedItem = null; } private object? _lastNavSelectedItem;
    private void NowPlayingCover_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) { Resona.Helpers.AnimationHelper.ApplyBouncyScale(NowPlayingCoverBorder, 1.0f); if (_currentIndex >= 0 && _currentIndex < _library.Count) { NavigateToNowPlaying(_library[_currentIndex], false); } } private void NowPlayingCenterCover_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) { 
    if (_lastNavSelectedItem != null) { RootNav.SelectedItem = _lastNavSelectedItem; } else { NavigateToSidebarItem("library", false); } 
} private void NowPlayingCenterCover_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) { var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard(); var scaleX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1.03, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; var scaleY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1.03, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; if (NowPlayingCenterCoverBorder.RenderTransform is not Microsoft.UI.Xaml.Media.CompositeTransform) { var transform = new Microsoft.UI.Xaml.Media.CompositeTransform(); transform.TranslateY = -5; NowPlayingCenterCoverBorder.RenderTransform = transform; } NowPlayingCenterCoverBorder.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleX, NowPlayingCenterCoverBorder); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleX, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)"); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleY, NowPlayingCenterCoverBorder); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleY, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)"); sb.Children.Add(scaleX); sb.Children.Add(scaleY); sb.Begin(); } private void NowPlayingCenterCover_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) { var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard(); var scaleX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; var scaleY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut } }; if (NowPlayingCenterCoverBorder.RenderTransform is not Microsoft.UI.Xaml.Media.CompositeTransform) { var transform = new Microsoft.UI.Xaml.Media.CompositeTransform(); transform.TranslateY = -5; NowPlayingCenterCoverBorder.RenderTransform = transform; } Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleX, NowPlayingCenterCoverBorder); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleX, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)"); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleY, NowPlayingCenterCoverBorder); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleY, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)"); sb.Children.Add(scaleX); sb.Children.Add(scaleY); sb.Begin(); }

private async void UpdateSMTCInfo(Track track, bool isPlaying)
	{
		try
		{
			if (_smtc == null) return;
			var smtc = _smtc;
			smtc.PlaybackStatus = isPlaying ? Windows.Media.MediaPlaybackStatus.Playing : Windows.Media.MediaPlaybackStatus.Paused;
			var updater = smtc.DisplayUpdater;
			updater.Type = Windows.Media.MediaPlaybackType.Music;
			updater.MusicProperties.Title = string.IsNullOrEmpty(track.Title) ? "Unknown" : track.Title;
			updater.MusicProperties.Artist = string.IsNullOrEmpty(track.Artist) ? "Unknown" : track.Artist;
			
			if (!string.IsNullOrEmpty(track.CoverArtPath) && System.IO.File.Exists(track.CoverArtPath))
			{
				try {
					var sf = await Windows.Storage.StorageFile.GetFileFromPathAsync(track.CoverArtPath);
					updater.Thumbnail = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(sf);
				} catch { updater.Thumbnail = null; }
			}
			else
			{
				updater.Thumbnail = null;
			}
			updater.Update();
		}
		catch { }
	}


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
	private System.Collections.Generic.Stack<Models.Track> _playbackHistory = new();
	private System.Collections.Generic.Stack<Models.Track> _playbackFuture = new();
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

    
	[System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
	public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

	public MainWindow()
	{
		try { SetCurrentProcessExplicitAppUserModelID("Resona"); CreateStartMenuShortcut(); } catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "resona_err.log"), ex.ToString()); }
		InitSMTC();
        this.InitializeComponent();
        this.RootGrid.Loaded += (s,e) => { if (App.Settings.Current.AutoUpdateEnabled) { _ = UpdateManager.CheckForUpdatesAsync(this.Content.XamlRoot, false); } };
 this.RootGrid.AddHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(RootGrid_PointerReleased), true);
        this.RootGrid.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(RootGrid_PointerPressed), true);
        this.RootGrid.PreviewKeyDown += (s, e) => {
    if (e.Key == Windows.System.VirtualKey.Space) {
        var focus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.Content.XamlRoot);
        if (focus is Microsoft.UI.Xaml.Controls.TextBox || focus is Microsoft.UI.Xaml.Controls.PasswordBox || focus is Microsoft.UI.Xaml.Controls.AutoSuggestBox || focus is Microsoft.UI.Xaml.Controls.RichEditBox) return;
        e.Handled = true;
        PlayPauseButton_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
    }
};
		RootNav.Loaded += (s, e) => InitTaskbarThumbnailButtons(); Strings.Current.PropertyChanged += (_, _) => { if (RootNav.SettingsItem is NavigationViewItem si) si.Content = Strings.Current.CS_Settings; };
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
        
        // Charger le volume par dÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©faut depuis les paramÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¨tres
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
        RootNav.Loaded += (_, _) => { ResolveNavigationViewChromeElements(); if (RootNav.SettingsItem is NavigationViewItem si) si.Content = Strings.Current.CS_Settings; UpdateGradientOverflowLayout(); AnimateNavItemsText(RootNav.IsPaneOpen); };
        Models.Strings.Current.PropertyChanged += (_, e) => { if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "IsFr") { RootNav.OpenPaneLength = Models.Strings.Current.IsFr ? 210 : 160;  } };
        PlayerBar.SizeChanged += (_, _) => UpdateGradientOverflowLayout();
        this.SizeChanged += (_, _) => UpdateGradientOverflowLayout();

        SetupPositionTimer();
        ProgressSlider.ThumbToolTipValueConverter = new Resona.Converters.SecondsToTimeStringConverter();
        ProgressSlider.PointerEntered += (s, e) => {
            CustomProgressThumb.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            Resona.Helpers.AnimationHelper.ApplyBouncyScale(CustomProgressThumb, 1.25f);
            if (CustomProgressFill.Background is Microsoft.UI.Xaml.Media.SolidColorBrush scb) {
                var c = scb.Color;
                CustomProgressFill.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(c.A, (byte)Math.Min(255, c.R + 40), (byte)Math.Min(255, c.G + 40), (byte)Math.Min(255, c.B + 40)));
            }
        };
        ProgressSlider.PointerExited += (s, e) => {
            Resona.Helpers.AnimationHelper.ApplyBouncyScale(CustomProgressThumb, 1.0f);
            UpdatePlayerButtonsColor();
        };
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
        PlayPauseButton.PointerEntered += (s, e) => PlayPauseButton.Opacity = 0.85;
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

    
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        else
            return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_GETMINMAXINFO = 0x0024;
    private const uint WM_COMMAND = 0x0111;
    private const uint THBN_CLICKED = 0x1800;

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
        if (msg == 0x0024)
        {
            MINMAXINFO info = (MINMAXINFO)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            info.ptMinTrackSize.X = 940; // Default min width
            info.ptMinTrackSize.Y = 600; // Default min height
            System.Runtime.InteropServices.Marshal.StructureToPtr(info, lParam, true);
        }
        else if (msg == WM_COMMAND)
        {
            // Clics sur les boutons de la thumbnail toolbar (barre des tâches)
            long wp = wParam.ToInt64();
            uint notifCode = (uint)((wp >> 16) & 0xFFFF);
            uint buttonId = (uint)(wp & 0xFFFF);

            if (notifCode == THBN_CLICKED)
            {
                switch (buttonId)
                {
                    case 100: // Play/Pause
                        DispatcherQueue.TryEnqueue(() => PlayPauseButton_Click(null, null));
                        return IntPtr.Zero;
                    case 101: // Précédent
                        DispatcherQueue.TryEnqueue(() => PrevButton_Click(null, null));
                        return IntPtr.Zero;
                    case 102: // Suivant
                        DispatcherQueue.TryEnqueue(() => NextButton_Click(null, null));
                        return IntPtr.Zero;
                }
            }
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

    private static Windows.UI.Color Darken(Windows.UI.Color c, double factor) => Windows.UI.Color.FromArgb(255, (byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));

    private Windows.UI.Color BrightenIfNeeded(Windows.UI.Color c)
    {
        double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
        if (luminance < 0.45)
        {
            return Windows.UI.Color.FromArgb(255, (byte)Math.Min(255, c.R + 80), (byte)Math.Min(255, c.G + 80), (byte)Math.Min(255, c.B + 80));
        }
        return c;
    }

    private bool _isNowPlayingModeActive = false;
    private Windows.UI.Color _lastComputedAvgColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    private void UpdatePlayerButtonsColor()
    {
        if (_isNowPlayingModeActive)
        {
            var brightened = BrightenIfNeeded(_lastComputedAvgColor);
            var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(brightened); var hoverBrightened = Windows.UI.Color.FromArgb(255, (byte)Math.Min(255, brightened.R + 30), (byte)Math.Min(255, brightened.G + 30), (byte)Math.Min(255, brightened.B + 30)); var hoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(hoverBrightened);
            double lum = (0.299 * brightened.R + 0.587 * brightened.G + 0.114 * brightened.B) / 255;
            
            PrevButton.Foreground = brush;
            NextButton.Foreground = brush;
            NowPlayingTitle.Foreground = brush;
            NowPlayingArtist.Foreground = brush;
            RepeatButton.Foreground = brush;
            RepeatIcon.Foreground = brush;
            RepeatIcon.Opacity = _playbackMode == PlaybackMode.Off ? 0.5 : 1.0; RepeatOneBadge.Foreground = brush;
            PlayPauseButton.Background = brush; PlayPauseButton.Resources["ButtonBackgroundPointerOver"] = hoverBrush; PlayPauseButton.Resources["ButtonBackgroundPressed"] = hoverBrush; var ppt = PlayPauseButton.RequestedTheme; PlayPauseButton.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark; PlayPauseButton.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light; PlayPauseButton.RequestedTheme = ppt; PlayPauseIcon.Foreground = lum > 0.6 ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)) : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            
            CurrentTimeText.Foreground = brush;
            TotalTimeText.Foreground = brush;
            CustomProgressFill.Background = brush;
            CustomProgressThumbInner.Fill = brush;
            
            LyricsIcon.Foreground = brush;
            if (LyricsText != null) LyricsText.Foreground = brush;
            LyricsButton.BorderBrush = brush;
            
            VolumeIcon.Foreground = brush; VolumeSlider.Foreground = brush; VolumeSlider.Resources["SliderThumbBackground"] = brush; VolumeSlider.Resources["SliderThumbBackgroundPointerOver"] = hoverBrush; VolumeSlider.Resources["SliderThumbBackgroundPressed"] = hoverBrush; VolumeSlider.Resources["SliderTrackValueFillPointerOver"] = hoverBrush; VolumeSlider.Resources["SliderTrackValueFillPressed"] = hoverBrush; var t = VolumeSlider.RequestedTheme; VolumeSlider.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark; VolumeSlider.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light; VolumeSlider.RequestedTheme = t; CustomProgressThumbOuter.Fill = brush; } else
        {
            var whiteBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            var accentBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AppAccentBrush"];
            
            PrevButton.Foreground = whiteBrush;
            NextButton.Foreground = whiteBrush;
            NowPlayingTitle.Foreground = whiteBrush;
            NowPlayingArtist.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(165, 255, 255, 255));
            RepeatButton.Foreground = whiteBrush;
            RepeatIcon.Foreground = _playbackMode == PlaybackMode.Off ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
            RepeatIcon.Opacity = 1.0; RepeatOneBadge.Foreground = accentBrush;
            PlayPauseButton.Background = accentBrush; PlayPauseIcon.Foreground = whiteBrush;
            PlayPauseButton.Resources.Remove("ButtonBackgroundPointerOver");
            PlayPauseButton.Resources.Remove("ButtonBackgroundPressed");
            
            if (App.Settings.Current.Backdrop == Models.AppBackdropStyle.Solid)
            {
                if (App.Settings.Current.ThemePresetIndex == 7) // Noir Absolu
                {
                    PlayPauseButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 40, 40, 40));
                    PlayPauseIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
                    
                    if (_playbackMode != PlaybackMode.Off)
                    {
                        RepeatIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
                        RepeatOneBadge.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
                    }
                }
                else if (App.Settings.Current.ThemePresetIndex == 8) // Blanc Pur
                {
                    PlayPauseButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 220, 220));
                    PlayPauseIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
                    
                    if (_playbackMode != PlaybackMode.Off)
                    {
                        RepeatIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
                        RepeatOneBadge.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
                    }
                }
            }
            var ppt2 = PlayPauseButton.RequestedTheme; PlayPauseButton.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark; PlayPauseButton.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light; PlayPauseButton.RequestedTheme = ppt2; 
            CurrentTimeText.Foreground = whiteBrush; 
            TotalTimeText.Foreground = whiteBrush; 
            
            var progressBrush = accentBrush;
            var blackBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            if (App.Settings.Current.Backdrop == Models.AppBackdropStyle.Solid)
            {
                if (App.Settings.Current.ThemePresetIndex == 7) // Noir Absolu
                {
                    progressBrush = whiteBrush;
                    PlayPauseButton.Resources["ButtonBackgroundPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 60, 60));
                    PlayPauseButton.Resources["ButtonBackgroundPressed"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 80, 80));
                }
                else if (App.Settings.Current.ThemePresetIndex == 8) // Blanc Pur
                {
                    progressBrush = blackBrush;
                    PlayPauseButton.Resources["ButtonBackgroundPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 220, 220));
                    PlayPauseButton.Resources["ButtonBackgroundPressed"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200));
                    PlayPauseButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)); 
                    PlayPauseIcon.Foreground = blackBrush;
                    PrevButton.Foreground = blackBrush;
                    NextButton.Foreground = blackBrush;
                }
            }
            
            CustomProgressFill.Background = progressBrush; 
            CustomProgressThumbInner.Fill = progressBrush; 
            CustomProgressThumbOuter.Fill = progressBrush; 
            
            LyricsIcon.Foreground = whiteBrush; 
            if (LyricsText != null) LyricsText.Foreground = whiteBrush; 
            LyricsButton.BorderBrush = accentBrush; 
            
            if (App.Settings.Current.Backdrop == Models.AppBackdropStyle.Solid && App.Settings.Current.ThemePresetIndex == 8)
            {
                var darkGrayBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 60, 60));
                VolumeIcon.Foreground = blackBrush;
                VolumeSlider.Foreground = blackBrush;
                VolumeSlider.Resources["SliderThumbBackground"] = blackBrush;
                VolumeSlider.Resources["SliderThumbBackgroundPointerOver"] = darkGrayBrush;
                VolumeSlider.Resources["SliderThumbBackgroundPressed"] = darkGrayBrush;
                VolumeSlider.Resources["SliderTrackValueFillPointerOver"] = darkGrayBrush;
                VolumeSlider.Resources["SliderTrackValueFillPressed"] = darkGrayBrush;
            }
            else
            {
                VolumeIcon.Foreground = whiteBrush; 
                VolumeSlider.ClearValue(Microsoft.UI.Xaml.Controls.Slider.ForegroundProperty); 
                VolumeSlider.Resources.Remove("SliderThumbBackground"); 
                VolumeSlider.Resources.Remove("SliderThumbBackgroundPointerOver"); 
                VolumeSlider.Resources.Remove("SliderThumbBackgroundPressed"); 
                VolumeSlider.Resources.Remove("SliderTrackValueFillPointerOver"); 
                VolumeSlider.Resources.Remove("SliderTrackValueFillPressed"); 
            }
            var t2 = VolumeSlider.RequestedTheme; 
            VolumeSlider.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark; 
            VolumeSlider.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light; 
            VolumeSlider.RequestedTheme = t2; 
        }
    }

    private void UpdatePlayerBarColorAsync(Track track)
    {
        var preset = ThemePresets.All[Math.Clamp(App.Settings.Current.ThemePresetIndex, 0, ThemePresets.All.Length - 1)];
        var themeSurface = ParseHexColor(preset.SurfaceHex);
        
        System.Threading.Tasks.Task.Run(() => {
            var c = GetAverageColorCpu(track.CoverArtPath ?? "");
            DispatcherQueue.TryEnqueue(() => ApplyPlayerBarColor(track.Id, c, themeSurface));
        });
    }
    private void ApplyPlayerBarColor(string trackId, Windows.UI.Color? avg, Windows.UI.Color themeSurface)
    {
        var baseColor = avg ?? Windows.UI.Color.FromArgb(255, 30, 30, 30);
        _lastComputedAvgColor = baseColor;
        UpdatePlayerButtonsColor();
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
            PlayerGradientFadeLayer.Background = BuildBackgroundFadeBrush(bgColor, Bounds.Height, Math.Min(Bounds.Height * 0.6, 800), PlayerBar.ActualHeight > 0 ? PlayerBar.ActualHeight : 88);

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

    private static LinearGradientBrush BuildBackgroundFadeBrush(Windows.UI.Color bgColor, double windowHeight, double fadeHeight, double playerH) { var fade = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) }; double topEdgeY = windowHeight - playerH - fadeHeight; double transparentTopY = Math.Max(0, topEdgeY - 150); double transparentBottomY = topEdgeY + fadeHeight * 0.4; var transparentBg = Windows.UI.Color.FromArgb(0, bgColor.R, bgColor.G, bgColor.B); fade.GradientStops.Add(new GradientStop { Color = transparentBg, Offset = 0 }); fade.GradientStops.Add(new GradientStop { Color = transparentBg, Offset = Math.Clamp(transparentTopY / windowHeight, 0, 1) }); fade.GradientStops.Add(new GradientStop { Color = bgColor, Offset = Math.Clamp(topEdgeY / windowHeight, 0, 1) }); fade.GradientStops.Add(new GradientStop { Color = transparentBg, Offset = Math.Clamp(transparentBottomY / windowHeight, 0, 1) }); fade.GradientStops.Add(new GradientStop { Color = transparentBg, Offset = 1 }); return fade; }

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
        
        double windowHeight = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : Bounds.Height;
        double fadeHeight = Math.Min(windowHeight * 0.6, 800);
        PlayerGradientColorLayer.Height = fadeHeight + playerH;
        PlayerGradientFadeLayer.Height = double.NaN;
        PlayerGradientFadeLayer.VerticalAlignment = VerticalAlignment.Stretch;
        PlayerGradientFadeLayer.Margin = new Thickness(0);

        if (_gradientStartColor.HasValue && _gradientEndColor.HasValue)
        {
            var overflowGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            overflowGradient.GradientStops.Add(new GradientStop { Color = _gradientStartColor.Value, Offset = 0 });
            overflowGradient.GradientStops.Add(new GradientStop { Color = _gradientEndColor.Value, Offset = 0.5 });
            overflowGradient.GradientStops.Add(new GradientStop { Color = _gradientEndColor.Value, Offset = 1 });
            PlayerGradientColorLayer.Background = overflowGradient;
        }

        var bgColor = ((SolidColorBrush)Application.Current.Resources["AppDeepBackgroundBrush"]).Color;
        PlayerGradientFadeLayer.Background = BuildBackgroundFadeBrush(bgColor, windowHeight, fadeHeight, playerH);
    }

    private void ResolveNavigationViewChromeElements() { RootNav.OpenPaneLength = Models.Strings.Current.IsFr ? 210 : 160; if (_navMainContentGrid != null) return;

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

        private void AnimateNavItemsText(bool opening)
    {
        var items = RootNav.MenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>().Concat(RootNav.FooterMenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>());
        if (RootNav.SettingsItem is Microsoft.UI.Xaml.Controls.NavigationViewItem si) items = items.Append(si);

        foreach (var item in items)
        {
            var contentPresenter = WalkVisualTree(item).OfType<Microsoft.UI.Xaml.Controls.ContentPresenter>().FirstOrDefault(c => c.Name == "ContentPresenter");
            if (contentPresenter != null)
            {
                var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { 
                    To = opening ? 1 : 0, 
                    Duration = TimeSpan.FromMilliseconds(opening ? 300 : 150) 
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, contentPresenter);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Opacity");
                sb.Children.Add(anim);
                sb.Begin();
            }
        }
    }
    
    private void RootNav_PaneOpening(Microsoft.UI.Xaml.Controls.NavigationView sender, object args)
    {
        AnimateNavItemsText(true);
    }

    private bool _isManuallyClosingPane = false; private async void RootNav_PaneClosing(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewPaneClosingEventArgs args) { if (_isManuallyClosingPane) { AnimateNavItemsText(false); return; } args.Cancel = true; _isManuallyClosingPane = true; RootNav.UpdateLayout(); AnimateNavItemsText(false); await Task.Delay(150); RootNav.IsPaneOpen = false; _isManuallyClosingPane = false; }
    private void ApplyNavigationViewChrome(bool enabled)
    {
        ResolveNavigationViewChromeElements(); if (RootNav.SettingsItem is NavigationViewItem si) si.Content = Strings.Current.CS_Settings;

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

        // ===================== CATÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â°GORIES DE NAVIGATION =====================
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

        // ===================== BIBLIOTHÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¹ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â QUE =====================

    public async Task ReloadLibraryFromCacheAsync()
    {
        await LoadLibraryFromCacheAsync();
        RestoreSidebarSelection();
    }

    public void AddTrackToLibrary(Track track)
    {
        var existing = _library.FirstOrDefault(t => string.Equals(t.FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            int index = _library.IndexOf(existing);
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
        _ = FetchMissingCoversInBackgroundAsync(_library.ToList());
        _ = BackgroundRescanAsync();
    }

        private bool _isFetchingCovers = false;
    private HashSet<string> _failedCoverSearches = new HashSet<string>();

    private async Task FetchMissingCoversInBackgroundAsync(List<Track> tracks)
    {
        if (_isFetchingCovers) return;
        _isFetchingCovers = true;
        try
        {
            var missing = tracks.Where(t => string.IsNullOrEmpty(t.CoverArtPath)).ToList();
            var stillMissing = new List<Track>();
            
            // Extract embedded covers extremely fast (no delays)
            foreach (var track in missing)
            {
                var embedded = App.Scanner.ExtractEmbeddedCover(track.FilePath);
                if (embedded != null && embedded.Length > 0)
                {
                    var localPath = await App.CoverArt.SaveEmbeddedCoverAsync(track.Id, embedded);
                    if (localPath != null) { track.CoverArtPath = localPath; await App.Cache.UpdateTrackAsync(track); continue; }
                }
                stillMissing.Add(track);
            }

            if (!App.Settings.Current.AutoFetchMissingCovers) return;

            // Group by Album/Artist to reduce iTunes API calls
            var grouped = stillMissing
                .Where(t => !_failedCoverSearches.Contains(t.Id))
                .GroupBy(t => {
                    string safeArtist = t.Artist ?? "";
                    string safeAlbum = t.Album ?? "";
                    if (safeAlbum.ToLowerInvariant().Contains("inconnu") || safeAlbum.ToLowerInvariant().Contains("unknown")) safeAlbum = "";
                    if (safeArtist.ToLowerInvariant().Contains("inconnu") || safeArtist.ToLowerInvariant().Contains("unknown")) safeArtist = "";
                    
                    if (!string.IsNullOrEmpty(safeAlbum) && !string.IsNullOrEmpty(safeArtist))
                        return safeArtist + "|" + safeAlbum;
                    return t.Id;
                })
                .ToList();

            foreach (var group in grouped)
            {
                if (!App.Settings.Current.AutoFetchMissingCovers) break;

                var firstTrack = group.First();
                if (string.IsNullOrWhiteSpace(firstTrack.Artist) && string.IsNullOrWhiteSpace(firstTrack.Album) && string.IsNullOrWhiteSpace(firstTrack.Title)) continue;

                var path = await App.CoverArt.FindAndCacheCoverAsync(firstTrack.Id, firstTrack.Artist, firstTrack.Album, firstTrack.Title);
                if (path != null)
                {
                    foreach (var track in group)
                    {
                        track.CoverArtPath = path;
                        await App.Cache.UpdateTrackAsync(track);
                    }
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_currentIndex >= 0 && group.Any(t => t.Id == _library[_currentIndex].Id))
                            SetPlayerCover(path);
                    });
                }
                else
                {
                    foreach (var track in group)
                    {
                        _failedCoverSearches.Add(track.Id);
                    }
                }
                
                await Task.Delay(800);
            }
        }
        catch { }
        finally
        {
            _isFetchingCovers = false;
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
                    var reallyNew = new List<Track>();
                    foreach (var track in allNew)
                    {
                        if (existingPaths.Contains(track.FilePath))
                        {
                            int index = _library.FindIndex(t => string.Equals(t.FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase));
                            if (index >= 0)
                            {
                                _library[index] = track;
                                libraryChanged = true;
                            }
                        }
                        else
                        {
                            reallyNew.Add(track);
                        }
                    }
                    
                    if (reallyNew.Count > 0)
                    {
                        _library.AddRange(reallyNew);
                        libraryChanged = true;
                        _ = FetchMissingCoversInBackgroundAsync(reallyNew.ToList());
                    }
                }
                
                DispatcherQueue.TryEnqueue(() => StopScanAnimation());
                if (!libraryChanged) return;
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _library = _library.Where(t => seen.Add(t.FilePath)).ToList();
                    _libraryPageInstance?.SetTracks(_library);
                    _albumsPageInstance?.LoadData(_library);
                    _artistsPageInstance?.LoadData(_library);
                    _genresPageInstance?.LoadData(_library);
                    _foldersPageInstance?.LoadData(_library);
                });
            });
    }
    
    private Storyboard? _scanStoryboard;

    public void StartScanAnimation()
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

    public void StopScanAnimation()
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
		if (_queue.FirstOrDefault(x => x.FilePath == filePath) is Track trackForSMTC) UpdateSMTCInfo(trackForSMTC, isPlaying);
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

    public async void PlayTrack(Track track, List<Track>? queue = null, bool isGoingBack = false, bool isGoingForward = false)
    {
        var old = _queue.FirstOrDefault(t => t.Id == _nowPlayingId);
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
        UpdateTaskbarPlayPauseIcon(true);
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
        if (ContentFrame.Content is NowPlayingPage npp) npp.UpdateTrackInfo(track);

        if (App.Settings.Current.AutoOpenNowPlaying)
        {
            NavigateToNowPlaying(track, isGoingBack);
        }

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

            // Mettre ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â  jour la durÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©e si le dÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©codage a rÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©vÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©lÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â© une durÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©e diffÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©rente (ex: FFMpeg)
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

    private string SanitizeUnknown(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var l = input.ToLowerInvariant();
        if (l.Contains("inconnu") || l.Contains("unknown")) return "";
        return input;
    }

    private async Task FindMissingCoverAsync(Track track)
    {
        string sArtist = SanitizeUnknown(track.Artist);
        string sAlbum = SanitizeUnknown(track.Album);
        string sTitle = SanitizeUnknown(track.Title);
        if (string.IsNullOrEmpty(sArtist) && string.IsNullOrEmpty(sAlbum) && string.IsNullOrEmpty(sTitle)) return;

        var path = await App.CoverArt.FindAndCacheCoverAsync(track.Id, sArtist, sAlbum, track.Title, track.FilePath);
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

    private async Task FindMissingLyricsAsync(Track track)
    {
        string sArtist = SanitizeUnknown(track.Artist);
        string sTitle = SanitizeUnknown(track.Title);
        string sAlbum = SanitizeUnknown(track.Album);
        if (string.IsNullOrEmpty(sArtist) && string.IsNullOrEmpty(sTitle)) 
        {
            DispatcherQueue.TryEnqueue(() => ShowPlainLyrics(Models.Strings.Current.IsFr ? "Paroles introuvables." : "Lyrics not found."));
            return;
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var lyricsTask = App.Lyrics.SearchAsync(sArtist, sTitle, sAlbum, track.Duration);
        var autoTagTask = AutoTagService.LookupAsync(sArtist, sTitle, track.Duration, track.FilePath);

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
            else ShowPlainLyrics(Models.Strings.Current.IsFr ? "Aucune parole trouvée pour ce morceau." : "No lyrics found for this track.");
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
                    statusText = new Microsoft.UI.Xaml.Controls.TextBlock { Text = Models.Strings.Current.IsFr ? $"Analyse de {tracksToAnalyze.Count} titres..." : $"Analyzing {tracksToAnalyze.Count} tracks...", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center };
                    var panel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 10 };
                    panel.Children.Add(new Microsoft.UI.Xaml.Controls.ProgressRing { IsActive = true, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center });
                    panel.Children.Add(statusText);
                    loadingDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = Models.Strings.Current.IsFr ? "Normalisation Audio" : "Audio Normalization",
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
                        DispatcherQueue.TryEnqueue(() => statusText.Text = (Models.Strings.Current.IsFr ? "Analyse... " : "Analyzing... ") + $"{currentProcessed}/{tracksToAnalyze.Count}");
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
        bool enabled = App.Settings.Current.LyricsEnabled;
        LyricsButton.Opacity = enabled ? 1 : 0;
        LyricsButton.IsHitTestVisible = enabled;
        if (!enabled) CloseLyricsOverlay();
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
            ShowPlainLyrics(lyrics);
            return;
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
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(t.Artist + " " + t.Title + (Models.Strings.Current.IsFr ? " paroles" : " lyrics"))}"));
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
        if (_lyricsGoogleBtn != null) _lyricsGoogleBtn.Visibility = (text.StartsWith("Aucune parole") || text.StartsWith("No lyrics")) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateLyricsOverlayTrackInfo(Track track)
    {
        EnsureLyricsOverlayCreated();
        if (_lyricsTrackTitle != null) _lyricsTrackTitle.Text = track.Title;
        if (_lyricsTrackArtist != null) _lyricsTrackArtist.Text = track.Artist;
        ShowPlainLyrics(Models.Strings.Current.IsFr ? "Recherche des paroles..." : "Searching for lyrics...");
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
        var fi = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }; Storyboard.SetTarget(fi, _lyricsOverlay); Storyboard.SetTargetProperty(fi, "Opacity"); sb.Children.Add(fi);
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

    private void CloseLyricsOverlay() { if (_lyricsOverlay == null) return; if (_lyricsOverlay.RenderTransform is not Microsoft.UI.Xaml.Media.CompositeTransform) _lyricsOverlay.RenderTransform = new Microsoft.UI.Xaml.Media.CompositeTransform(); _lyricsOverlayOpen = false;
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

    private double _previousVolume = 50;

    private void VolumeMuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (VolumeSlider.Value > 0)
        {
            _previousVolume = VolumeSlider.Value;
            VolumeSlider.Value = 0;
        }
        else
        {
            VolumeSlider.Value = _previousVolume > 0 ? _previousVolume : 50;
        }
    }

    private void VolumeSlider_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(VolumeSlider).Properties.MouseWheelDelta;
        double change = delta > 0 ? 5 : -5;
        double newValue = VolumeSlider.Value + change;
        VolumeSlider.Value = Math.Max(0, Math.Min(100, newValue));
        e.Handled = true;
    }

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (VolumeIcon != null)
        {
            if (VolumeSlider.Value <= 0)
                VolumeIcon.Glyph = "\xE74F"; // Mute
            else if (VolumeSlider.Value < 33)
                VolumeIcon.Glyph = "\xE992"; // Low
            else if (VolumeSlider.Value < 66)
                VolumeIcon.Glyph = "\xE993"; // Medium
            else
                VolumeIcon.Glyph = "\xE767"; // High (standard)
        }

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
        LyricsButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
    }

    private void LyricsButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Resona.Helpers.AnimationHelper.ApplyBouncyScale(LyricsButton, 1.0f);
        LyricsButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void LyricsButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        LyricsButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255));
    }

    private void LyricsButton_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        LyricsButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
    }

    public void RefreshPlaylistsPage() => _playlistsPageInstance?.RefreshAsync();

                
	public MenuFlyout BuildTrackMenu(Track track, List<Track>? trackList = null, List<Track>? selectedTracks = null)
    {
        var flyout = new MenuFlyout();

        if (selectedTracks != null && selectedTracks.Count > 1 && selectedTracks.Contains(track))
        {
            var headerItem = new MenuFlyoutItem { Text = $"{(Resona.Models.Strings.Current.IsFr ? "Sélection de" : "Selection of")} {selectedTracks.Count} {(Resona.Models.Strings.Current.IsFr ? "titres" : "tracks")}", IsEnabled = false };
            flyout.Items.Add(headerItem);
            flyout.Items.Add(new MenuFlyoutSeparator());

            var queueItem = new MenuFlyoutItem { Text = Resona.Models.Strings.Current.CS_Ajouterlafiledatten, Icon = new FontIcon { Glyph = "\uE81E" } };
            queueItem.Click += (s, e) => {
                foreach (var t in selectedTracks) AddToQueue(t);
            };
            flyout.Items.Add(queueItem);

            var playlistItem = new MenuFlyoutSubItem { Text = Resona.Models.Strings.Current.CS_Ajouteruneplaylist, Icon = new FontIcon { Glyph = "\uE90B" } };
            flyout.Items.Add(playlistItem);
            _ = LoadPlaylistSubItemsAsync(playlistItem, selectedTracks);

            flyout.Items.Add(new MenuFlyoutSeparator());
            var deleteMultiItem = new MenuFlyoutItem
            {
                Text = Resona.Models.Strings.Current.CS_Delete,
                Icon = new FontIcon { Glyph = "\uE74D" },
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed)
            };
            deleteMultiItem.Click += async (s, e) => await ShowDeleteTracksDialogAsync(selectedTracks);
            flyout.Items.Add(deleteMultiItem);

            return flyout;
        }

        var playItemSingle = new MenuFlyoutItem { Text = Resona.Models.Strings.Current.CS_Play, Icon = new FontIcon { Glyph = "\uE768" } };
        playItemSingle.Click += (s, e) => PlayTrack(track, trackList ?? _library);
        flyout.Items.Add(playItemSingle);

        var queueItemSingle = new MenuFlyoutItem { Text = Resona.Models.Strings.Current.CS_Ajouterlafiledatten, Icon = new FontIcon { Glyph = "\uE81E" } };
        queueItemSingle.Click += (s, e) => AddToQueue(track);
        flyout.Items.Add(queueItemSingle);

        var playlistItemSingle = new MenuFlyoutSubItem { Text = Resona.Models.Strings.Current.CS_Ajouteruneplaylist, Icon = new FontIcon { Glyph = "\uE90B" } };
        flyout.Items.Add(playlistItemSingle);

        _ = LoadPlaylistSubItemsAsync(playlistItemSingle, new List<Track> { track });

        var autoTagItem = new MenuFlyoutItem { Text = "Autotag", Icon = new FontIcon { Glyph = "\uE943" } };
        autoTagItem.Click += (s, e) => _ = ShowAutoTagDialogAsync(track);
        flyout.Items.Add(autoTagItem);

        flyout.Items.Add(new MenuFlyoutSeparator());
        var deleteItem = new MenuFlyoutItem
        {
            Text = Resona.Models.Strings.Current.CS_Delete,
            Icon = new FontIcon { Glyph = "\uE74D" },
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed)
        };
        deleteItem.Click += async (s, e) => await ShowDeleteTracksDialogAsync(new List<Track> { track });
        flyout.Items.Add(deleteItem);

        return flyout;
    }

    public async Task ShowDeleteTracksDialogAsync(List<Track> tracks)
    {
        if (tracks == null || tracks.Count == 0) return;

        bool isMulti = tracks.Count > 1;
        string title = isMulti
            ? Resona.Models.Strings.Current.CS_DeleteTracksTitle
            : Resona.Models.Strings.Current.CS_DeleteTrackTitle;
        string body = isMulti
            ? string.Format(Resona.Models.Strings.Current.CS_DeleteTracksBody, tracks.Count)
            : Resona.Models.Strings.Current.CS_DeleteTrackBody;

        var removeBtn = new Button
        {
            Content = Resona.Models.Strings.Current.CS_RemoveFromApp,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var deleteBtn = new Button
        {
            Content = Resona.Models.Strings.Current.CS_DeleteFilePermanently,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 220, 50, 50)),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed)
        };
        var cancelBtn = new Button
        {
            Content = Resona.Models.Strings.Current.CS_Annuler,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(removeBtn);
        panel.Children.Add(deleteBtn);
        panel.Children.Add(cancelBtn);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            XamlRoot = ContentFrame.XamlRoot
        };

        removeBtn.Click += async (s, e) =>
        {
            dialog.Hide();
            bool wasPlaying = false;
            foreach (var track in tracks)
            {
                await App.Cache.DeleteTrackAsync(track.Id);
                _library.Remove(track);
                if (App.NowPlayingId == track.Id) wasPlaying = true;
            }
            if (wasPlaying) { App.AudioEngine.Stop(); App.NowPlayingId = null; }
            _libraryPageInstance?.SetTracks(_library);
        };

        deleteBtn.Click += async (s, e) =>
        {
            dialog.Hide();
            bool wasPlaying = false;
            foreach (var track in tracks)
            {
                await App.Cache.DeleteTrackAsync(track.Id);
                _library.Remove(track);
                if (App.NowPlayingId == track.Id) wasPlaying = true;
                try { if (File.Exists(track.FilePath)) File.Delete(track.FilePath); } catch { }
            }
            if (wasPlaying) { App.AudioEngine.Stop(); App.NowPlayingId = null; }
            _libraryPageInstance?.SetTracks(_library);
        };

        cancelBtn.Click += (s, e) => dialog.Hide();

        await dialog.ShowAsync();
    }

    private async Task LoadPlaylistSubItemsAsync(MenuFlyoutSubItem parent, List<Track> tracksToAdd)
    {
        try
        {
            var playlists = await App.Cache.LoadAllPlaylistsAsync();
            foreach (var pl in playlists)
            {
                var item = new MenuFlyoutItem { Text = pl.Name };
                var captured = pl;
                item.Click += async (s, e) =>
                {
                    bool modified = false;
                    foreach (var track in tracksToAdd)
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
                parent.Items.Add(new MenuFlyoutItem { Text = "Aucune playlist", IsEnabled = false });
        }
        catch { }
    }

    private static StackPanel MakeField(string label, FrameworkElement input)
    {
        var icon = new FontIcon { Glyph = "\uE8F1", FontSize = 10,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin = new Thickness(0, 0, 4, 0) };
        var labelBlock = new TextBlock
        {
            Text = label, FontSize = 11, Opacity = 0.7,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, labelBlock } };
        return new StackPanel { Children = { header, input }, Spacing = 2 };
    }

    
    public void AddToQueue(Track track)
    {
        _manualQueue.Add(track);
        _queuePageInstance?.SetQueue(_manualQueue);
        SaveManualQueue();
    }

    public void RemoveFromQueue(Track track)
    {
        _manualQueue.Remove(track);
        _queuePageInstance?.SetQueue(_manualQueue);
        SaveManualQueue();
    }

        public void EnableContinuousPlaybackIfOff()
    {
        if (_playbackMode == PlaybackMode.Off)
        {
            _playbackMode = PlaybackMode.RepeatAll;
            App.Settings.Current.SavedPlaybackMode = (int)_playbackMode;
            RepeatIcon.Glyph = "";
            ((Microsoft.UI.Xaml.Controls.IconElement)RepeatIcon).Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["SystemControlHighlightAccentBrush"];
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip((Microsoft.UI.Xaml.DependencyObject)RepeatButton, Models.Strings.Current.IsFr ? "Répéter la liste" : "Repeat all");
        }
    }
    private void RestoreManualQueue()
    {
        _manualQueue.Clear();
        if (App.Settings.Current.SavedQueueIds != null && _library != null)
        {
            foreach (var id in App.Settings.Current.SavedQueueIds)
            {
                var track = _library.FirstOrDefault(t => t.Id == id);
                if (track != null) _manualQueue.Add(track);
            }
        }
        _queuePageInstance?.SetQueue(_manualQueue);
    }

    private void SaveManualQueue()
    {
        App.Settings.Current.SavedQueueIds = _manualQueue.Select(t => t.Id).ToList();
        _ = App.Settings.SaveAsync();
    }
public void ClearQueue()
    {
        _manualQueue.Clear();
        _queuePageInstance?.SetQueue(_manualQueue);
        SaveManualQueue();
    }

    public void UpdateQueue(List<Track> newQueue)
    {
        _manualQueue.Clear();
        _manualQueue.AddRange(newQueue);
        _queuePageInstance?.SetQueue(_manualQueue);
        SaveManualQueue();
    }

    public void NavigateToArtist(string artist)
    {
        var tracks = _library.Where(t => string.Equals(t.Artist, artist, StringComparison.OrdinalIgnoreCase)).ToList();
        if (tracks.Count > 0)
        {
            RootNav.SelectedItem = null;
            ShowTrackCollection(artist, tracks, Resona.Models.Strings.Current.CS_Artiste);
        }
    }

    public void NavigateToAlbum(string album)
    {
        var tracks = _library.Where(t => string.Equals(t.Album, album, StringComparison.OrdinalIgnoreCase)).ToList();
        if (tracks.Count > 0)
        {
            RootNav.SelectedItem = null;
            ShowTrackCollection(album, tracks, Resona.Models.Strings.Current.CS_Album);
        }
    }

public async Task<bool> ShowAutoTagDialogAsync(Track track)
    {
        string? coverPath = track.CoverArtPath;
        var coverImage = new Image
        {
            Width = 140, Height = 140, Stretch = Stretch.UniformToFill,
            Source = !string.IsNullOrEmpty(coverPath) && File.Exists(coverPath)
                ? CoverCacheService.GetBitmap(coverPath, 140) : null
        };
        var coverPlaceholder = new Border
        {
            Width = 140, Height = 140, CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["AppSurfaceBrush"],
            Child = new FontIcon { Glyph = "", FontSize = 40,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        if (coverImage.Source != null) coverPlaceholder.Child = coverImage;

        var changeCoverBtn = new Button
        {
            Content = Resona.Models.Strings.Current.IsFr ? "Changer la pochette" : "Change cover", HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0), UseLayoutRounding = true,
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 4, 10, 4)
        };
        changeCoverBtn.Click += async (s, e) =>
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
                FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" }
            };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                coverPath = file.Path;
                var bmp = CoverCacheService.GetBitmap(coverPath, 140);
                if (bmp != null) { coverImage.Source = bmp; coverPlaceholder.Child = coverImage; }
            }
        };

        var titleBox = new TextBox { Text = track.Title, PlaceholderText = Resona.Models.Strings.Current.IsFr ? "Titre" : "Title" };
        var artistBox = new TextBox { Text = track.Artist, PlaceholderText = Resona.Models.Strings.Current.IsFr ? "Artiste" : "Artist" };
        var albumBox = new TextBox { Text = track.Album, PlaceholderText = "Album" };
        var genreBox = new TextBox { Text = track.Genre ?? "", PlaceholderText = "Genre" };
        var yearBox = new TextBox { Text = track.Year > 0 ? track.Year.ToString() : "", PlaceholderText = Resona.Models.Strings.Current.IsFr ? "Année" : "Year" };
        var trackNumBox = new TextBox { Text = track.TrackNumber > 0 ? track.TrackNumber.ToString() : "", PlaceholderText = Resona.Models.Strings.Current.IsFr ? "Piste" : "Track" };

        var searchCoverBtn = new Button
        {
            Content = Resona.Models.Strings.Current.IsFr ? "Chercher en ligne" : "Search online", HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0), UseLayoutRounding = true,
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 4, 10, 4)
        };
        searchCoverBtn.Click += async (s, e) =>
        {
            string term = App.CoverArt.BuildCoverSearchQuery(titleBox.Text, artistBox.Text, albumBox.Text);

			var originalContent = searchCoverBtn.Content;
			searchCoverBtn.Content = Resona.Models.Strings.Current.IsFr ? "Recherche..." : "Searching...";
			searchCoverBtn.IsEnabled = false;

			var results = await App.CoverArt.SearchGoogleImagesAsync(term, 12);
			results = results.Distinct().Take(12).ToList();

            searchCoverBtn.Content = originalContent;
            searchCoverBtn.IsEnabled = true;

            if (results.Count == 0)
            {
                var googleBtn = new HyperlinkButton { Content = "Chercher sur Google Images", NavigateUri = new Uri($"https://www.google.com/search?tbm=isch&q={Uri.EscapeDataString(term)}") };
                var errorFlyout = new Flyout
                {
                    Content = new StackPanel { Children = { new TextBlock { Text = $"Aucune pochette trouvée pour '{term}'." }, googleBtn } }
                };
                errorFlyout.ShowAt(searchCoverBtn);
                return;
            }

            var gridView = new GridView
            {
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 400
            };
            
            // Put it inside a Grid with a fixed width to force wrapping and prevent horizontal scroll
            var containerGrid = new Grid { Width = 520 };
            containerGrid.Children.Add(gridView);

            var flyout = new Flyout();
            var style = new Style(typeof(FlyoutPresenter));
            style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 600.0));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
            flyout.FlyoutPresenterStyle = style;

            foreach (var url in results)
            {
                var img = new Image { Width = 150, Height = 150, Stretch = Stretch.UniformToFill, Margin = new Thickness(4) };
                try { img.Source = new BitmapImage(new Uri(url)); } catch { continue; }
                img.Tapped += (_, __) =>
                {
                    coverPath = url;
                    coverImage.Source = img.Source;
                    coverPlaceholder.Child = coverImage;
                    flyout.Hide();
                };
                gridView.Items.Add(img);
            }
            flyout.Content = containerGrid;
            flyout.ShowAt(searchCoverBtn);
        };

        var coverPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 16, 0) };
        coverPanel.Children.Add(coverPlaceholder);
        coverPanel.Children.Add(changeCoverBtn);
        coverPanel.Children.Add(searchCoverBtn);

        var searchTagsBtn = new Button { Content = Resona.Models.Strings.Current.IsFr ? "Rechercher métadonnées" : "Search metadata", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 8, 0, 0) };
        var resetTagsBtn = new Button { Content = Resona.Models.Strings.Current.IsFr ? "Réinitialiser" : "Reset", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 8, 0, 0) };

        var statusTextBlock = new TextBlock
        {
            FontSize = 11, Opacity = 0.6, Margin = new Thickness(0, 6, 0, 0),
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Microsoft.UI.Xaml.Visibility.Collapsed
        };

        searchTagsBtn.Click += async (s, e) =>
        {
            searchTagsBtn.IsEnabled = false;
            var originalContent = searchTagsBtn.Content;
            searchTagsBtn.Content = Resona.Models.Strings.Current.IsFr ? "Recherche en cours..." : "Searching...";
            var result = await AutoTagService.LookupAsync(artistBox.Text, titleBox.Text, track.Duration, track.FilePath);
            searchTagsBtn.Content = originalContent;
            searchTagsBtn.IsEnabled = true;

            if (result != null)
            {
                if (!string.IsNullOrWhiteSpace(result.Title)) titleBox.Text = result.Title;
                if (!string.IsNullOrWhiteSpace(result.Artist)) artistBox.Text = result.Artist;
                if (!string.IsNullOrWhiteSpace(result.Album)) albumBox.Text = result.Album;
                if (!string.IsNullOrWhiteSpace(result.Genre)) genreBox.Text = result.Genre;
                if (result.Year.HasValue) yearBox.Text = result.Year.Value.ToString();
                if (result.TrackNumber.HasValue) trackNumBox.Text = result.TrackNumber.Value.ToString();
                
                if (!string.IsNullOrWhiteSpace(result.CoverPath))
                {
                    coverPath = result.CoverPath;
                    try 
                    { 
                        coverImage.Source = new BitmapImage(new Uri(result.CoverPath)); 
                        coverPlaceholder.Child = coverImage;
                    } 
                    catch { }
                }
                
                statusTextBlock.Text = Resona.Models.Strings.Current.IsFr ? "✅ Métadonnées trouvées et pré-remplies." : "✅ Metadata found and pre-filled.";
                statusTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                statusTextBlock.Text = Resona.Models.Strings.Current.IsFr ? "⚠️ Aucune donnée trouvée — remplis manuellement." : "⚠️ No data found — fill manually.";
                statusTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
        };

        resetTagsBtn.Click += (s, e) =>
        {
            titleBox.Text = track.Title;
            artistBox.Text = track.Artist;
            albumBox.Text = track.Album;
            genreBox.Text = track.Genre ?? "";
            yearBox.Text = track.Year > 0 ? track.Year.ToString() : "";
            trackNumBox.Text = track.TrackNumber > 0 ? track.TrackNumber.ToString() : "";
            statusTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        };

        var tagsActionPanel = new Grid { ColumnSpacing = 8 };
        tagsActionPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tagsActionPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(searchTagsBtn, 0);
        Grid.SetColumn(resetTagsBtn, 1);
        tagsActionPanel.Children.Add(searchTagsBtn);
        tagsActionPanel.Children.Add(resetTagsBtn);

        var form = new StackPanel { Spacing = 6 };
        form.Children.Add(MakeField(Resona.Models.Strings.Current.IsFr ? "Titre" : "Title", titleBox));
        form.Children.Add(MakeField(Resona.Models.Strings.Current.IsFr ? "Artiste" : "Artist", artistBox));
        form.Children.Add(MakeField("Album", albumBox));
        form.Children.Add(MakeField("Genre", genreBox));
        form.Children.Add(MakeField(Resona.Models.Strings.Current.IsFr ? "Année" : "Year", yearBox));
        form.Children.Add(MakeField(Resona.Models.Strings.Current.IsFr ? "Piste" : "Track", trackNumBox));
        form.Children.Add(tagsActionPanel);
        form.Children.Add(statusTextBlock);

        var bodyGrid = new Grid { ColumnSpacing = 16, Width = 480, HorizontalAlignment = HorizontalAlignment.Center };
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(coverPanel, 0);
        Grid.SetColumn(form, 1);
        bodyGrid.Children.Add(coverPanel);
        bodyGrid.Children.Add(form);

        var writeToFileCheckBox = new CheckBox
        {
            Content = Resona.Models.Strings.Current.IsFr ? "Enregistrer les modifications directement dans le fichier (écraser)" : "Save changes directly to file (overwrite)",
            IsChecked = App.Settings.Current.AutoTagWriteToFile,
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
        };
        
        // Use Grid instead of StackPanel for dialogContentPanel so it obeys ContentDialog MaxWidth!
        var dialogContentPanel = new Grid();
        dialogContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(bodyGrid, 0);
        Grid.SetRow(writeToFileCheckBox, 1);
        dialogContentPanel.Children.Add(bodyGrid);
        dialogContentPanel.Children.Add(writeToFileCheckBox);

        var editDialog = new ContentDialog
        {
            Title = Resona.Models.Strings.Current.IsFr ? "Autotag - Édition" : "Autotag - Edit",
            Content = dialogContentPanel,
            PrimaryButtonText = Resona.Models.Strings.Current.IsFr ? "Sauvegarder" : "Save",
            CloseButtonText = Resona.Models.Strings.Current.CS_Annuler,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = ContentFrame.XamlRoot
        };

        if (await editDialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var data = new AutoTagResult
            {
                Title = titleBox.Text,
                Artist = artistBox.Text,
                Album = albumBox.Text,
                Genre = genreBox.Text,
                Year = int.TryParse(yearBox.Text, out var y) ? y : null,
                TrackNumber = int.TryParse(trackNumBox.Text, out var tn) ? tn : null,
                CoverPath = coverPath
            };

            bool saved = false;
            if (titleBox.Text != track.Title || artistBox.Text != track.Artist
                || albumBox.Text != track.Album || genreBox.Text != (track.Genre ?? "")
                || yearBox.Text != (track.Year > 0 ? track.Year.ToString() : "") || trackNumBox.Text != (track.TrackNumber > 0 ? track.TrackNumber.ToString() : ""))
            {
                saved = true; 
                track.Title = data.Title ?? track.Title;
                track.Artist = data.Artist ?? track.Artist;
                track.Album = data.Album ?? track.Album;
                track.Genre = data.Genre ?? track.Genre ?? "";
                if (data.Year.HasValue) track.Year = data.Year.Value;
                if (data.TrackNumber.HasValue) track.TrackNumber = data.TrackNumber.Value;
            }

            if (coverPath != track.CoverArtPath && !string.IsNullOrWhiteSpace(coverPath))
            {
                string coverDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resona", "Covers");
                System.IO.Directory.CreateDirectory(coverDir);
                string newCoverName = $"{track.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";
                string coverDest = Path.Combine(coverDir, newCoverName);
                
                try
                {
                    // Clean up old covers for this track
                    var oldCovers = Directory.GetFiles(coverDir, $"{track.Id}*.jpg");
                    foreach (var old in oldCovers) { try { File.Delete(old); } catch { } }

                    if (coverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        using var hc = new System.Net.Http.HttpClient();
                        hc.DefaultRequestHeaders.Add("User-Agent", "Resona/2.3");
                        var imgBytes = await hc.GetByteArrayAsync(coverPath);
                        await System.IO.File.WriteAllBytesAsync(coverDest, imgBytes);
                        
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
                catch { }
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
                // Note: we DO NOT call MarkPathDirty or TriggerLibraryRescan because we don't want the scanner 
                // to immediately read the original file and overwrite our new database metadata!
                await App.Cache.UpdateTrackAsync(track);

                if (App.NowPlayingId == track.Id)
                {
                    NowPlayingTitle.Content = track.Title;
                    NowPlayingArtist.Content = track.Artist;
                    NowPlayingAlbum.Content = track.Album;
                    if (!string.IsNullOrEmpty(track.CoverArtPath))
                    {
                        var bmp = CoverCacheService.GetBitmap(track.CoverArtPath, 140);
                        if (bmp != null) 
                        { 
                            NowPlayingCover.Source = bmp; 
                            NowPlayingPlaceholderIcon.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                        }
                    }
                }

                // Force UI refresh so the new cover and text show up immediately in the list
                _libraryPageInstance?.SetTracks(_library);
            }
            return true;
        }

        return false;
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
		
		//IL_000d: Expected O, but got Unknown
		PlayPauseButton_Click(this, new RoutedEventArgs());
	}


	public void SetShuffleModeAndPlay(Track track, List<Track> queue)
	{
		_playbackMode = PlaybackMode.Shuffle;
		UpdateRepeatButtonVisual();
		PlayTrack(track, queue);
	}


	public void NavigateToPlaylistDetail(Playlist playlist, List<Track> librarySnapshot)
	{
		
		
		ContentFrame.Navigate(typeof(PlaylistDetailPage), new Tuple<Playlist, List<Track>>(playlist, librarySnapshot), new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
		DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
		{
			if (ContentFrame.Content is PlaylistDetailPage playlistDetailPage)
			{
				playlistDetailPage.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
			}
		});
	}


	public async void ShowTrackCollection(string title, List<Track> tracks, string? subtitle = null)
	{
		if (ContentFrame.Content != null && (int)ContentFrame.Visibility == 0)
		{
			await Helpers.AnimationHelper.PlayExitAnimationAsync(ContentFrame, -20f);
		}
		if ((Page)(object)_libraryPageInstance == (Page)null)
		{
			_libraryPageInstance = new LibraryPage();
		}
		_libraryPageInstance.ShowCollection(title, subtitle, tracks);
		ContentFrame.Content = _libraryPageInstance;
		Helpers.AnimationHelper.PlayEntranceAnimation(ContentFrame);
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
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		if (!(ProgressSlider == (Slider)null) && !(CustomProgressFill == (Grid)null) && !(CustomProgressThumb == (Grid)null) && !(CustomProgressTrack == (Grid)null))
		{
			double num = Math.Max(0.001, ProgressSlider.Maximum);
			double num2 = ProgressSlider.Value / num;
			double actualWidth = CustomProgressTrack.ActualWidth;
			if (actualWidth > 0.0)
			{
				double num3 = actualWidth * num2;
				CustomProgressFill.Width = num3;
				CustomProgressThumb.Margin = new Thickness(num3, 5.0, 0.0, 0.0);
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
			_infoOverlay.Visibility = (Visibility)0;
		}
		_infoOverlayOpen = true;
		Storyboard sb = new Storyboard();
		DoubleAnimation fi = new DoubleAnimation
		{
			To = 1.0,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(200.0)),
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		Storyboard.SetTarget(fi, _infoOverlay);
		Storyboard.SetTargetProperty(fi, "Opacity");
		sb.Children.Add(fi);
		if (!(_infoOverlay.RenderTransform is CompositeTransform))
		{
			_infoOverlay.RenderTransform = (Transform)new CompositeTransform();
		}
		_infoOverlay.RenderTransformOrigin = new Point(0.5, 0.5);
		CompositeTransform transform = (CompositeTransform)_infoOverlay.RenderTransform;
		transform.ScaleX = 0.95;
		transform.ScaleY = 0.95;
		transform.TranslateY = 20.0;
		DoubleAnimation sx = new DoubleAnimation
		{
			To = 1.0,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(400.0)),
			EasingFunction = new ExponentialEase
			{
				EasingMode = EasingMode.EaseOut,
				Exponent = 4.0
			}
		};
		DoubleAnimation sy = new DoubleAnimation
		{
			To = 1.0,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(400.0)),
			EasingFunction = new ExponentialEase
			{
				EasingMode = EasingMode.EaseOut,
				Exponent = 4.0
			}
		};
		DoubleAnimation ty = new DoubleAnimation
		{
			To = 0.0,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(400.0)),
			EasingFunction = new ExponentialEase
			{
				EasingMode = EasingMode.EaseOut,
				Exponent = 4.0
			}
		};
		Storyboard.SetTarget(sx, _infoOverlay);
		Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
		Storyboard.SetTarget(sy, _infoOverlay);
		Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
		Storyboard.SetTarget(ty, _infoOverlay);
		Storyboard.SetTargetProperty(ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
		sb.Children.Add(sx);
		sb.Children.Add(sy);
		sb.Children.Add(ty);
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
			foreach (JsonProperty item in pages.EnumerateObject())
			{
				if (item.Value.TryGetProperty("extract", out var extractElement))
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
					foreach (JsonProperty item2 in pages.EnumerateObject())
					{
						if (item2.Value.TryGetProperty("extract", out var extractElement2))
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
		
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		
		
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		
		
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		//IL_016f: Expected O, but got Unknown
		
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Expected O, but got Unknown
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		
		
		
		
		
		
		//IL_028c: Expected O, but got Unknown
		
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		
		if (_infoOverlay != (Grid)null)
		{
			return;
		}
		StackPanel val = new StackPanel
		{
			Spacing = 20.0,
			VerticalAlignment = (VerticalAlignment)1,
			HorizontalAlignment = (HorizontalAlignment)1,
			MaxWidth = 800.0
		};
		_infoTrackTitle = new TextBlock
		{
			FontSize = 32.0,
			FontWeight = Microsoft.UI.Text.FontWeights.Bold,
			TextWrapping = (TextWrapping)2,
			TextAlignment = (TextAlignment)0
		};
		_infoTrackArtist = new TextBlock
		{
			FontSize = 20.0,
			Opacity = 0.8,
			Margin = new Thickness(0.0, 0.0, 0.0, 20.0),
			TextWrapping = (TextWrapping)2,
			TextAlignment = (TextAlignment)0
		};
		_infoContent = new TextBlock
		{
			FontSize = 16.0,
			Opacity = 0.9,
			TextWrapping = (TextWrapping)2,
			IsTextSelectionEnabled = true
		};
		((Panel)val).Children.Add(_infoTrackTitle);
		((Panel)val).Children.Add(_infoTrackArtist);
		((Panel)val).Children.Add(_infoContent);
		((Panel)val).Background = (Brush)new SolidColorBrush(Colors.Transparent);
		
		val.PointerPressed += (s, e) => _infoOverlay.Visibility = Visibility.Collapsed;
		ScrollViewer val3 = new ScrollViewer
		{
			Content = val,
			Padding = new Thickness(32.0, 24.0, 32.0, 40.0),
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			HorizontalAlignment = (HorizontalAlignment)3
		};
		Microsoft.UI.Xaml.Shapes.Rectangle val4 = new Microsoft.UI.Xaml.Shapes.Rectangle
		{
			Fill = (Brush)new SolidColorBrush(ColorHelper.FromArgb((byte)220, (byte)0, (byte)0, (byte)0))
		};
		_infoOverlay = new Grid
		{
			Visibility = (Visibility)1,
			Opacity = 0.0,
			HorizontalAlignment = (HorizontalAlignment)3,
			VerticalAlignment = (VerticalAlignment)3
		};
		Grid.SetRowSpan(_infoOverlay, 2);
		Canvas.SetZIndex(_infoOverlay, 100);
		((Panel)_infoOverlay).Children.Add(val4);
		((Panel)_infoOverlay).Children.Add(val3);
		_infoOverlay.PointerPressed += (PointerEventHandler)delegate(object s, PointerRoutedEventArgs e)
		{
			HideInfoOverlay();
			e.Handled = true;
		};
		((Panel)RootGrid).Children.Add(_infoOverlay);
	}


	private void RootNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
	{
		NavigationViewItemBase selectedItemContainer = args.SelectedItemContainer;
		string tag = ((selectedItemContainer == null) ? null : selectedItemContainer.Tag?.ToString()) ?? "";
		NavigateToSidebarItem(tag, args.IsSettingsSelected);
	}


	private void LyricsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		
		Helpers.AnimationHelper.ApplyBouncyScale(LyricsButton, 1.05f);
		LyricsButton.Background = (Brush)new SolidColorBrush(Color.FromArgb((byte)10, byte.MaxValue, byte.MaxValue, byte.MaxValue));
	}


	private void NowPlayingCoverBorder_RightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		var track = _library.FirstOrDefault(t => t.Id == App.NowPlayingId);
		if (track == null) return;
		e.Handled = true;
		var menu = BuildTrackMenu(track, _library);
		menu.ShowAt(NowPlayingCoverBorder, e.GetPosition(NowPlayingCoverBorder));
	}

	private void NowPlayingInfo_RightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		var track = _library.FirstOrDefault(t => t.Id == App.NowPlayingId);
		if (track == null) return;
		e.Handled = true;
		var menu = BuildTrackMenu(track, _library);
		menu.ShowAt(NowPlayingCoverBorder, e.GetPosition(NowPlayingCoverBorder));
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
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		
		
		//IL_018e: Expected O, but got Unknown
		RepeatOneBadge.Visibility = (Visibility)1;
		switch (_playbackMode)
		{
		case PlaybackMode.Off:
			RepeatIcon.Glyph = "\ue8ee";
			((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
			ToolTipService.SetToolTip((DependencyObject)RepeatButton, Strings.Current.IsFr ? "Lecture simple" : "Normal playback");
			break;
		case PlaybackMode.RepeatAll:
			RepeatIcon.Glyph = "\ue8ee";
			((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
			ToolTipService.SetToolTip((DependencyObject)RepeatButton, Strings.Current.IsFr ? "Répéter la liste" : "Repeat all");
			break;
		case PlaybackMode.RepeatOne:
			RepeatIcon.Glyph = "\ue8ee";
			((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
			RepeatOneBadge.Visibility = (Visibility)0;
			ToolTipService.SetToolTip((DependencyObject)RepeatButton, Strings.Current.IsFr ? "Répéter ce morceau" : "Repeat one");
			break;
		case PlaybackMode.Shuffle:
			RepeatIcon.Glyph = "\ue8b1";
			((IconElement)RepeatIcon).Foreground = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
			ToolTipService.SetToolTip((DependencyObject)RepeatButton, Strings.Current.IsFr ? "Lecture aléatoire" : "Shuffle");
			break;
		}
	}


	private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
	{
		
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		PlaybackState state = App.AudioEngine.State;
		PlaybackState val = state;
		if ((int)val != 0)
		{
			if ((int)val == 1)
			{
				App.AudioEngine.Pause();
				try
				{
					if (_smtc != (SystemMediaTransportControls)null)
					{
						_smtc.PlaybackStatus = (MediaPlaybackStatus)4;
					}
				}
				catch
				{
				}
				PlayPauseIcon.Glyph = "\ue768";
				PlayPauseIcon.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
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
		try
		{
			if (_smtc != (SystemMediaTransportControls)null)
			{
				_smtc.PlaybackStatus = (MediaPlaybackStatus)3;
			}
		}
		catch
		{
		}
		PlayPauseIcon.Glyph = "\ue769";
		PlayPauseIcon.Margin = new Thickness(0.0);
		UpdateTaskbarPlayPauseIcon();
	}


	private void NextButton_Click(object sender, RoutedEventArgs e)
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
				PlayTrack(PickRandomTrack(), _queue);
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
				PlayTrack(_playbackHistory.Pop(), _queue, isGoingBack: true);
			}
			else
			{
				PlayTrack(PickRandomTrack(), _queue);
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
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		//IL_00bc: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Expected O, but got Unknown
		//IL_014f: Expected O, but got Unknown
		if (!_infoOverlayOpen || _infoOverlay == (Grid)null)
		{
			return;
		}
		_infoOverlayOpen = false;
		Storyboard val = new Storyboard();
		DoubleAnimation val2 = new DoubleAnimation
		{
			To = 0.0,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(150.0)),
			EasingFunction = (EasingFunctionBase)new CubicEase
			{
				EasingMode = (EasingMode)1
			}
		};
		DoubleAnimation val3 = new DoubleAnimation
		{
			To = 10.0,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(150.0)),
			EasingFunction = (EasingFunctionBase)new CubicEase
			{
				EasingMode = (EasingMode)1
			}
		};
		DoubleAnimation val4 = new DoubleAnimation
		{
			To = 0.98,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(150.0)),
			EasingFunction = (EasingFunctionBase)new CubicEase
			{
				EasingMode = (EasingMode)1
			}
		};
		DoubleAnimation val5 = new DoubleAnimation
		{
			To = 0.98,
			Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(150.0)),
			EasingFunction = (EasingFunctionBase)new CubicEase
			{
				EasingMode = (EasingMode)1
			}
		};
		Storyboard.SetTarget((Timeline)val2, (DependencyObject)_infoOverlay);
		Storyboard.SetTargetProperty((Timeline)val2, "Opacity");
		Storyboard.SetTarget((Timeline)val3, (DependencyObject)_infoOverlay);
		Storyboard.SetTargetProperty((Timeline)val3, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
		Storyboard.SetTarget((Timeline)val4, (DependencyObject)_infoOverlay);
		Storyboard.SetTargetProperty((Timeline)val4, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
		Storyboard.SetTarget((Timeline)val5, (DependencyObject)_infoOverlay);
		Storyboard.SetTargetProperty((Timeline)val5, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");
		val.Children.Add((Timeline)val2);
		val.Children.Add((Timeline)val3);
		val.Children.Add((Timeline)val4);
		val.Children.Add((Timeline)val5);
		((Timeline)val).Completed += delegate
		{
			if (!_infoOverlayOpen)
			{
				_infoOverlay.Visibility = (Visibility)1;
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
				if ((int)ContentFrame.Visibility == 0 && ContentFrame.Content != null)
				{
					await Helpers.AnimationHelper.PlayExitAnimationAsync(ContentFrame, -20f);
				}
				else if ((int)SettingsContainer.Visibility == 0)
				{
					await Helpers.AnimationHelper.PlayExitAnimationAsync(SettingsContainer, -20f);
				}
				if (_pendingNavTag != currentTag || _pendingNavIsSettings != currentSettings)
				{
					continue;
				}
				if (currentSettings)
				{
					SetNowPlayingMode(isActive: false, null);
					ContentFrame.Visibility = (Visibility)1;
					SettingsContainer.Visibility = (Visibility)0;
					Helpers.AnimationHelper.PlayEntranceAnimation(SettingsContainer);
				}
				else
				{
					SettingsContainer.Visibility = (Visibility)1;
					ContentFrame.Visibility = (Visibility)0;
					ContentFrame.BackStack.Clear();
					if (!string.IsNullOrEmpty(currentTag))
					{
						switch (currentTag)
						{
						case "library":
							ContentFrame.Content = _libraryPageInstance;
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
							ContentFrame.Content = _albumsPageInstance;
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
							ContentFrame.Content = _playlistsPageInstance;
							_playlistsPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							_playlistsPageInstance.RefreshAsync();
							break;
						case "artists":
							if ((Page)_artistsPageInstance == (Page)null)
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
							if ((Page)_genresPageInstance == (Page)null)
							{
								_genresPageInstance = new GenresPage();
							}
							ContentFrame.Content = _genresPageInstance;
							_genresPageInstance.LoadData(_library);
							_genresPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							break;
						case "folders":
							if ((Page)_foldersPageInstance == (Page)null)
							{
								_foldersPageInstance = new FoldersPage();
							}
							ContentFrame.Content = _foldersPageInstance;
							_foldersPageInstance.LoadData(_library);
							_foldersPageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							break;
						case "statistics":
							if ((Page)_statisticsPageInstance == (Page)null)
							{
								_statisticsPageInstance = new StatisticsPage();
							}
							ContentFrame.Content = _statisticsPageInstance;
							_statisticsPageInstance.LoadData(_library);
							break;
						case "queue":
							if ((Page)_queuePageInstance == (Page)null)
							{
								_queuePageInstance = new QueuePage();
							}
							ContentFrame.Content = _queuePageInstance;
							_queuePageInstance.SetQueue(_manualQueue);
							_queuePageInstance.SetNowPlayingId(_nowPlayingId, _nowPlayingFilePath);
							break;
						case "download":
							if ((Page)_downloadPageInstance == (Page)null)
							{
								_downloadPageInstance = new DownloadPage();
							}
							ContentFrame.Content = _downloadPageInstance;
							break;
						}
					}
					Helpers.AnimationHelper.PlayEntranceAnimation(ContentFrame);
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
		
		//IL_000d: Invalid comparison between Unknown and I4
		
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		
		
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Expected O, but got Unknown
		//IL_011d: Expected O, but got Unknown
		
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		
		
		
		if ((int)PlayerBar.Visibility > 0)
		{
			PlayerBar.Visibility = (Visibility)0;
			PlayerBar.Opacity = 0.0;
			PlayerBar.Height = 0.0;
			if (App.Settings.Current.PlayerGradientOverflowEnabled && App.Settings.Current.Backdrop == AppBackdropStyle.Solid)
			{
				PlayerGradientOverflow.Visibility = (Visibility)0;
				PlayerGradientFadeLayer.Visibility = (Visibility)0;
			}
			TranslateTransform val = new TranslateTransform
			{
				Y = 15.0
			};
			PlayerBar.RenderTransform = (Transform)val;
			Storyboard val2 = new Storyboard();
			DoubleAnimation val3 = new DoubleAnimation
			{
				From = 0.0,
				To = 1.0,
				Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(250.0)),
				EasingFunction = (EasingFunctionBase)new QuadraticEase
				{
					EasingMode = (EasingMode)0
				}
			};
			Storyboard.SetTarget((Timeline)val3, (DependencyObject)PlayerBar);
			Storyboard.SetTargetProperty((Timeline)val3, "Opacity");
			DoubleAnimation val4 = new DoubleAnimation
			{
				From = 15.0,
				To = 0.0,
				Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(300.0)),
				EasingFunction = (EasingFunctionBase)new QuadraticEase
				{
					EasingMode = (EasingMode)0
				}
			};
			Storyboard.SetTarget((Timeline)val4, (DependencyObject)val);
			Storyboard.SetTargetProperty((Timeline)val4, "Y");
			DoubleAnimation val5 = new DoubleAnimation
			{
				From = 0.0,
				To = 98.0,
				Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(300.0)),
				EasingFunction = (EasingFunctionBase)new QuadraticEase
				{
					EasingMode = (EasingMode)0
				},
				EnableDependentAnimation = true
			};
			Storyboard.SetTarget((Timeline)val5, (DependencyObject)PlayerBar);
			Storyboard.SetTargetProperty((Timeline)val5, "Height");
			val2.Children.Add((Timeline)val3);
			val2.Children.Add((Timeline)val4);
			val2.Children.Add((Timeline)val5);
			((Timeline)val2).Completed += delegate
			{
				
				
				((DependencyObject)PlayerBar).ClearValue(FrameworkElement.HeightProperty);
				PlayerBar.RenderTransform = (Transform)new TranslateTransform();
			};
			val2.Begin();
		}
	}


	public void RestoreSidebarSelection()
	{
		object selectedItem = RootNav.SelectedItem;
		NavigationViewItem val = (NavigationViewItem)((selectedItem is NavigationViewItem) ? selectedItem : null);
		if (val != null)
		{
			object settingsItem = RootNav.SettingsItem;
			bool isSettings = (NavigationViewItem)((settingsItem is NavigationViewItem) ? settingsItem : null) == val;
			NavigateToSidebarItem(val.Tag?.ToString(), isSettings);
		}
	}

	private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		
		//IL_001a: Invalid comparison between Unknown and I4
		Microsoft.UI.Input.PointerPointProperties properties = e.GetCurrentPoint(RootGrid).Properties;
		LastClickWasXButton = (int)properties.PointerUpdateKind == 7 || properties.IsXButton1Pressed;
		if (LastClickWasXButton)
		{
			if ((!(ContentFrame.Content is PlaylistsPage playlistsPage) || !playlistsPage.TryGoBack()) && (!(ContentFrame.Content is LibraryPage libraryPage) || !libraryPage.TryGoBack()))
			{
				if (ContentFrame.Content is PlaylistDetailPage)
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
	}


	private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		TimeSpan totalDuration = App.AudioEngine.TotalDuration;
		if (totalDuration.TotalSeconds > 0.0)
		{
			App.AudioEngine.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
			if ((int)App.AudioEngine.State == 0 && ProgressSlider.Value < totalDuration.TotalSeconds)
			{
				App.AudioEngine.Resume();
				try
				{
					if (_smtc != (SystemMediaTransportControls)null)
					{
						_smtc.PlaybackStatus = (MediaPlaybackStatus)3;
					}
				}
				catch
				{
				}
				PlayPauseIcon.Glyph = "\ue769";
				PlayPauseIcon.Margin = new Thickness(0.0);
				UpdateTaskbarPlayPauseIcon();
			}
		}
		_isSliderDragging = false;
	}


	private void AudioEngine_PlaybackStopped(object? sender, EventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		
		DispatcherQueue.TryEnqueue(() =>
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
						PlayTrack(PickRandomTrack(), _queue);
					}
					break;
				case PlaybackMode.RepeatAll:
					PlayTrack(_queue[(_queueIndex + 1) % _queue.Count], _queue);
					break;
				case PlaybackMode.Off:
					PlayPauseIcon.Glyph = "\ue768";
					PlayPauseIcon.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
					UpdateTaskbarPlayPauseIcon();
					break;
				}
			}
		});
	

	
	}

	
}










