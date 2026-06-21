using System.Runtime.InteropServices;

namespace ScreenQuickMark;

internal static partial class PInvoke
{
    public const int HWND_TOPMOST = -1;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const uint WS_CAPTION = 0x00C00000;
    public const uint WS_THICKFRAME = 0x00040000;
    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const uint WS_EX_LAYERED = 0x00080000;

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;

    // ── GDI ──────────────────────────────────────
    public const int SRCCOPY = 0x00CC0020;
    public const int CAPTUREBLT = 0x40000000;

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);
    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines,
        byte[] lpvBits, ref BITMAPINFO lpbmi, uint usage);
    [DllImport("gdi32.dll")]
    public static extern int SetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint cLines,
        byte[] lpBits, ref BITMAPINFO lpbmi, uint ColorUse);
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage,
        out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    public static int GetWindowLong(IntPtr hWnd, int nIndex) =>
        (int)(GetWindowLongPtr(hWnd, nIndex) & 0xFFFFFFFF);
    public static void SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong) =>
        SetWindowLongPtr(hWnd, nIndex, dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
        ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
        uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

    public static void EnableClickThrough(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | (int)(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
    }
    public static void DisableClickThrough(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex & ~(int)WS_EX_TRANSPARENT);
    }

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    // ── 分层窗口常量 ─────────────────────────────
    public const uint ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx; public int cy; }
    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    public const uint DIB_RGB_COLORS = 0;

    // ── 纯 Win32 窗口创建 ────────────────────────
    public const uint WS_POPUP = 0x80000000;
    public const uint CS_HREDRAW = 0x0002;

    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP   = 0x0202;
    public const uint WM_MOUSEMOVE   = 0x0200;
    public const uint WM_DESTROY     = 0x0002;

    public const int GWLP_USERDATA = -21;

    public static int GET_X_LPARAM(nint lp) => (short)(lp & 0xFFFF);
    public static int GET_Y_LPARAM(nint lp) => (short)((lp >> 16) & 0xFFFF);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowExW")]
    public static extern IntPtr CreateWindowExAtom(
        uint dwExStyle, IntPtr lpClassName, string lpWindowName,
        uint dwStyle, int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    // ── 低级键盘钩子 ────────────────────────────
    public const int WH_KEYBOARD_LL = 13;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_SYSKEYDOWN = 0x0104;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate IntPtr HOOKPROC(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(int idHook, HOOKPROC lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
}
