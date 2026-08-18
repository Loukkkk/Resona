using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Resona.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint NIM_ADD = 0;
    private const uint NIM_DELETE = 2;
    private const uint NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 1;
    private const uint NIF_ICON = 2;
    private const uint NIF_TIP = 4;
    private const uint NIF_SHOWTIP = 0x80;
    private const uint WM_APP = 0x8000;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const uint WS_EX_APPWINDOW = 0x40000;
    private const uint WS_CHILD = 0x40000000;

    private nint _msgHwnd;
    private nint _iconHandle;
    private bool _disposed;
    private readonly Window _window;

    public event EventHandler? LeftClick;
    public event EventHandler? RightClick;

    public TrayIconService(Window window, string tooltip)
    {
        _window = window;
        string exePath = Environment.ProcessPath ?? "";
        _iconHandle = ExtractIcon(GetModuleHandle(null), exePath, 0);
        if (_iconHandle == IntPtr.Zero)
            _iconHandle = LoadIcon(IntPtr.Zero, 32512);

        // Create a message-only window to receive tray notifications
        var wndProc = new WndProcDelegate(MessageWindowProc);
        _wndProcHandle = GCHandle.Alloc(wndProc);

        var className = "ResonaTrayMsgWindow_" + Guid.NewGuid().ToString("N");
        var wc = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };
        RegisterClass(ref wc);
        _msgHwnd = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
    }

    public void Show()
    {
        var nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>();
        nid.hWnd = _msgHwnd;
        nid.uID = 100;
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        nid.uCallbackMessage = WM_APP + 100;
        nid.hIcon = _iconHandle;
        nid.szTip = "Resona";
        nid.uTimeoutOrVersion = NOTIFYICON_VERSION_4;

        Shell_NotifyIcon(NIM_ADD, ref nid);
        Shell_NotifyIcon(NIM_SETVERSION, ref nid);
    }

    public void Hide()
    {
        var nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>();
        nid.hWnd = _msgHwnd;
        nid.uID = 100;
        Shell_NotifyIcon(NIM_DELETE, ref nid);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Hide();
        if (_iconHandle != IntPtr.Zero)
            DestroyIcon(_iconHandle);
        if (_msgHwnd != IntPtr.Zero)
            DestroyWindow(_msgHwnd);
        if (_wndProcHandle.IsAllocated)
            _wndProcHandle.Free();
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private GCHandle _wndProcHandle;

    private IntPtr MessageWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_APP + 100)
        {
            uint wp = (uint)wParam;
            if (wp == 100)
            {
                uint lp = (uint)lParam;
                if (lp == WM_LBUTTONUP)
                    LeftClick?.Invoke(this, EventArgs.Empty);
                else if (lp == WM_RBUTTONUP)
                    RightClick?.Invoke(this, EventArgs.Empty);
            }
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint cmd, ref NOTIFYICONDATA data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent,
        IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, int lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
