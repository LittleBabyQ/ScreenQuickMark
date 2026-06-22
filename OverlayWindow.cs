using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Graphics.DirectX;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ScreenQuickMark;

/// <summary>纯 Win32 WS_EX_LAYERED 透明浮层窗口，标注渲染在此</summary>
public sealed class OverlayWindow : IDisposable
{
    public MainWindow Owner { get; }

    // ─── 屏幕几何 ────────────────────────────────
    private readonly int _screenW, _screenH, _screenX, _screenY;
    private readonly float _dpi, _dpiScale, _dipW, _dipH;
    private IntPtr _hwnd;

    // ─── 离屏渲染 ────────────────────────────────
    private CanvasDevice? _device;
    private CanvasRenderTarget? _renderTarget;
    private IntPtr _hdcMem, _hDib, _pDibBits;
    private PInvoke.BITMAPINFO _bmi;
    private readonly Dictionary<Windows.UI.Color, CanvasSolidColorBrush> _brushCache = new();

    // ─── Win32 窗口过程 ──────────────────────────
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate _wndProc;
    private GCHandle _gcHandle;

    // ─── 低级键盘钩子 ────────────────────────────
    private static OverlayWindow? _hookOwner;
    private static PInvoke.HOOKPROC? _hookProc;
    private static IntPtr _hookHandle;

    public OverlayWindow(MainWindow owner)
    {
        _screenW = PInvoke.GetSystemMetrics(PInvoke.SM_CXSCREEN);
        _screenH = PInvoke.GetSystemMetrics(PInvoke.SM_CYSCREEN);
        _screenX = PInvoke.GetSystemMetrics(PInvoke.SM_XVIRTUALSCREEN);
        _screenY = PInvoke.GetSystemMetrics(PInvoke.SM_YVIRTUALSCREEN);
        _dpi = PInvoke.GetDpiForWindow(PInvoke.GetDesktopWindow());
        _dpiScale = _dpi / 96f;
        _dipW = _screenW / _dpiScale;
        _dipH = _screenH / _dpiScale;

        Owner = owner;
        _wndProc = WndProc;
        _gcHandle = GCHandle.Alloc(this);
        try
        {
            CreateWin32Window();
        }
        catch
        {
            _gcHandle.Free();
            throw;
        }
        EnsureRenderResources();
        RenderFrame();
    }

    private void CreateWin32Window()
    {
        var wc = new PInvoke.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<PInvoke.WNDCLASSEXW>(),
            style = PInvoke.CS_HREDRAW,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = PInvoke.GetModuleHandleW(null),
            lpszClassName = "ScreenQuickMark_Overlay",
            hCursor = IntPtr.Zero
        };
        ushort atom = PInvoke.RegisterClassExW(ref wc);
        if (atom == 0)
        {
            int err = Marshal.GetLastWin32Error();
            if (err != 1410) // ERROR_CLASS_ALREADY_EXISTS
            {
                string msg = $"RegisterClassExW failed. Win32 error: {err}";
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash.log"), msg);
                throw new InvalidOperationException(msg);
            }
        }

        // 使用 atom 创建窗口，绕过 delegate thunk 跨模块问题
        _hwnd = PInvoke.CreateWindowExAtom(
            PInvoke.WS_EX_LAYERED | PInvoke.WS_EX_NOACTIVATE | PInvoke.WS_EX_TOOLWINDOW,
            new IntPtr(atom), "",
            PInvoke.WS_POPUP,
            _screenX, _screenY, _screenW, _screenH,
            IntPtr.Zero, IntPtr.Zero, PInvoke.GetModuleHandleW(null), IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            string msg = $"CreateWindowExW for overlay failed. Win32 error: {err}";
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash.log"), msg);
            throw new InvalidOperationException(msg);
        }

        PInvoke.SetWindowLongPtr(_hwnd, PInvoke.GWLP_USERDATA,
            (IntPtr)GCHandle.ToIntPtr(_gcHandle));

        PInvoke.SetWindowPos(_hwnd, PInvoke.HWND_TOPMOST,
            _screenX, _screenY, _screenW, _screenH,
            PInvoke.SWP_NOACTIVATE | PInvoke.SWP_FRAMECHANGED | PInvoke.SWP_SHOWWINDOW);
        PInvoke.ShowWindow(_hwnd, PInvoke.SW_SHOW);
        Owner.BringToolbarToFront();

        // 初始：标注模式（不穿透）
        PInvoke.DisableClickThrough(_hwnd);
    }

    private void EnsureRenderResources()
    {
        if (_hdcMem != IntPtr.Zero) return;
        _device = CanvasDevice.GetSharedDevice();
        _renderTarget = new CanvasRenderTarget(_device, _dipW, _dipH, _dpi);

        _bmi = new PInvoke.BITMAPINFO();
        _bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<PInvoke.BITMAPINFOHEADER>();
        _bmi.bmiHeader.biWidth = _screenW;
        _bmi.bmiHeader.biHeight = -_screenH;
        _bmi.bmiHeader.biPlanes = 1;
        _bmi.bmiHeader.biBitCount = 32;
        _bmi.bmiHeader.biCompression = 0;

        _hDib = PInvoke.CreateDIBSection(IntPtr.Zero, ref _bmi, 0, out _pDibBits, IntPtr.Zero, 0);
        _hdcMem = PInvoke.CreateCompatibleDC(IntPtr.Zero);
        PInvoke.SelectObject(_hdcMem, _hDib);
    }

    // ─── Window Procedure ─────────────────────────
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (!Owner.IsAnnotationMode) goto def;

        switch (msg)
        {
            case PInvoke.WM_LBUTTONDOWN:
                OnMouseDown(lParam); break;
            case PInvoke.WM_MOUSEMOVE:
                OnMouseMove(lParam); break;
            case PInvoke.WM_LBUTTONUP:
                OnMouseUp(); break;
            case PInvoke.WM_DESTROY:
                // 不主动退出，MainWindow 控制生命周期
                break;
        }
    def:
        return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void OnMouseDown(IntPtr lParam)
    {
        int px = PInvoke.GET_X_LPARAM(lParam), py = PInvoke.GET_Y_LPARAM(lParam);
        var dip = new Vector2(px / _dpiScale, py / _dpiScale);

        if (Owner.Mode == DrawMode.Text)
        {
            Owner.StartTypingInternal(dip);
            InstallKeyboardHook();
        }
        else
        {
            Owner.CurrentStroke = new Stroke(Owner.CurrentColor, Owner.StrokeWidth);
            Owner.CurrentStroke.Points.Add(dip);
        }
    }

    private void OnMouseMove(IntPtr lParam)
    {
        var cs = Owner.CurrentStroke;
        if (cs == null || Owner.Mode != DrawMode.Pen) return;
        int px = PInvoke.GET_X_LPARAM(lParam), py = PInvoke.GET_Y_LPARAM(lParam);
        cs.Points.Add(new(px / _dpiScale, py / _dpiScale));
        RenderFrame();
    }

    private void OnMouseUp()
    {
        var cs = Owner.CurrentStroke;
        if (cs == null) return;
        Owner.Strokes.Add(cs);
        Owner.CurrentStroke = null;
        RenderFrame();
    }

    // ─── 穿透切换 ────────────────────────────────
    public void SetAnnotationMode(bool annotate)
    {
        if (annotate) PInvoke.DisableClickThrough(_hwnd);
        else PInvoke.EnableClickThrough(_hwnd);
        // 强制应用扩展样式变更
        PInvoke.SetWindowPos(_hwnd, PInvoke.HWND_TOPMOST,
            _screenX, _screenY, _screenW, _screenH,
            PInvoke.SWP_NOACTIVATE | PInvoke.SWP_FRAMECHANGED);
        Owner.BringToolbarToFront();
    }

    // ─── 低级键盘钩子 ────────────────────────────
    public void InstallKeyboardHook()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _hookOwner = this;
        _hookProc = HookProc;
        _hookHandle = PInvoke.SetWindowsHookExW(PInvoke.WH_KEYBOARD_LL, _hookProc, IntPtr.Zero, 0);
    }

    public void UninstallKeyboardHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            PInvoke.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _hookOwner = null;
        _hookProc = null;
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || _hookOwner == null)
            return PInvoke.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        uint msg = (uint)wParam;
        if (msg != PInvoke.WM_KEYDOWN && msg != PInvoke.WM_SYSKEYDOWN)
            return PInvoke.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        var kb = Marshal.PtrToStructure<PInvoke.KBDLLHOOKSTRUCT>(lParam);

        // Ctrl+Shift+Q → 放行（退出快捷键）
        bool ctrl = (PInvoke.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool shift = (PInvoke.GetAsyncKeyState(0x10) & 0x8000) != 0;
        if (ctrl && shift && kb.vkCode == 0x51)
            return PInvoke.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        _hookOwner.ProcessHookKey(kb.vkCode, shift);
        return (IntPtr)1; // 吃掉按键，阻止传递到下层窗口
    }

    private void ProcessHookKey(uint vkCode, bool shift)
    {
        if (vkCode == 0x0D) // Enter → 提交文字
        {
            Owner.CommitTyping();
            return;
        }
        if (vkCode == 0x1B) // Escape → 取消
        {
            Owner.CancelTyping();
            return;
        }
        if (vkCode == 0x08) // Backspace
        {
            if (!string.IsNullOrEmpty(Owner.TypingText))
            {
                Owner.TypingText = Owner.TypingText[..^1];
                RenderFrame();
            }
            return;
        }

        char? ch = MainWindow.VkToChar((byte)vkCode, shift);
        if (ch != null)
        {
            Owner.TypingText += ch.Value;
            RenderFrame();
        }
    }

    // ─── 渲染 ────────────────────────────────────
    public void RenderFrame()
    {
        if (_renderTarget == null || _hdcMem == IntPtr.Zero) return;

        using (var ds = _renderTarget.CreateDrawingSession())
        {
            // 分层窗口命中测试由 alpha 通道决定：alpha=0=穿透，alpha>0=接收鼠标
            // 标注模式：近乎透明 (alpha=1) 的背景确保全部像素命中
            // 穿透模式：全透明 (alpha=0) 背景让所有点击下穿
            ds.Clear(Owner.IsAnnotationMode
                ? Windows.UI.Color.FromArgb(1, 0, 0, 0)
                : Microsoft.UI.Colors.Transparent);

            // 模式指示条 (顶部 4px)
            if (Owner.IsAnnotationMode)
            {
                var barColor = Owner.Mode == DrawMode.Pen
                    ? Windows.UI.Color.FromArgb(160, 255, 80, 40)
                    : Windows.UI.Color.FromArgb(160, 40, 160, 255);
                ds.FillRectangle(0, 0, _dipW, 4, GetBrush(barColor));
            }

            foreach (var s in Owner.Strokes) DrawStroke(ds, s);
            if (Owner.CurrentStroke != null) DrawStroke(ds, Owner.CurrentStroke);
            foreach (var ta in Owner.TextAnnotations) DrawTextAnnotation(ds, ta);

            if (Owner.IsTyping)
            {
                string display = string.IsNullOrEmpty(Owner.TypingText) ? "|" : Owner.TypingText + "|";
                using var fmt = new CanvasTextFormat
                {
                    FontFamily = "Microsoft YaHei",
                    FontSize = Owner.TypingFontSize,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };
                ds.DrawText(display, Owner.TypingPosition, GetBrush(Owner.TypingColor), fmt);
            }
        }

        byte[] pixels = _renderTarget.GetPixelBytes();
        Marshal.Copy(pixels, 0, _pDibBits, pixels.Length);

        var blend = new PInvoke.BLENDFUNCTION
            { BlendOp = PInvoke.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = PInvoke.AC_SRC_ALPHA };
        var size = new PInvoke.SIZE { cx = _screenW, cy = _screenH };
        var pt = new PInvoke.POINT();
        PInvoke.UpdateLayeredWindow(_hwnd, IntPtr.Zero, ref pt, ref size, _hdcMem, ref pt, 0, ref blend, PInvoke.ULW_ALPHA);
        Owner.BringToolbarToFront();
    }

    private CanvasSolidColorBrush GetBrush(Windows.UI.Color c)
    {
        if (!_brushCache.TryGetValue(c, out var b))
            _brushCache[c] = b = new CanvasSolidColorBrush(_renderTarget!, c);
        return b;
    }

    private void DrawStroke(CanvasDrawingSession ds, Stroke s)
    {
        if (s.Points.Count < 2) return;
        var b = GetBrush(s.Color);
        for (int i = 1; i < s.Points.Count; i++)
            ds.DrawLine(s.Points[i - 1].X, s.Points[i - 1].Y, s.Points[i].X, s.Points[i].Y, b, s.Width);
    }

    private void DrawTextAnnotation(CanvasDrawingSession ds, TextAnnotation ta)
    {
        using var fmt = new CanvasTextFormat { FontFamily = "Microsoft YaHei", FontSize = ta.FontSize, WordWrapping = CanvasWordWrapping.NoWrap };
        ds.DrawText(ta.Text, ta.Position, GetBrush(ta.Color), fmt);
    }

    // ─── 导出（截桌面 + 合标注）───────────────────
    public async Task ScreenshotAsync()
    {
        if (_renderTarget == null || _device == null) return;
        var hdcScreen = PInvoke.GetDC(IntPtr.Zero);
        var hdcCap = PInvoke.CreateCompatibleDC(hdcScreen);
        var hBmp = PInvoke.CreateCompatibleBitmap(hdcScreen, _screenW, _screenH);
        var hOld = PInvoke.SelectObject(hdcCap, hBmp);
        PInvoke.BitBlt(hdcCap, 0, 0, _screenW, _screenH, hdcScreen, _screenX, _screenY, PInvoke.SRCCOPY | PInvoke.CAPTUREBLT);

        var bi = new PInvoke.BITMAPINFO();
        bi.bmiHeader.biSize = (uint)Marshal.SizeOf<PInvoke.BITMAPINFOHEADER>();
        bi.bmiHeader.biWidth = _screenW; bi.bmiHeader.biHeight = -_screenH;
        bi.bmiHeader.biPlanes = 1; bi.bmiHeader.biBitCount = 32; bi.bmiHeader.biCompression = 0;
        var capBytes = new byte[_screenW * _screenH * 4];
        PInvoke.GetDIBits(hdcCap, hBmp, 0, (uint)_screenH, capBytes, ref bi, PInvoke.DIB_RGB_COLORS);
        PInvoke.SelectObject(hdcCap, hOld); PInvoke.DeleteDC(hdcCap); PInvoke.DeleteObject(hBmp); PInvoke.ReleaseDC(IntPtr.Zero, hdcScreen);

        var bg = CanvasBitmap.CreateFromBytes(_device, capBytes, _screenW, _screenH,
            DirectXPixelFormat.B8G8R8A8UIntNormalized, _dpi, CanvasAlphaMode.Premultiplied);
        var rt = new CanvasRenderTarget(_device, _screenW, _screenH, _dpi);
        using (var ds = rt.CreateDrawingSession())
        {
            ds.DrawImage(bg, new Windows.Foundation.Rect(0, 0, _screenW, _screenH));
            foreach (var s in Owner.Strokes) { for (int i = 1; i < s.Points.Count; i++) ds.DrawLine(s.Points[i - 1].X * _dpiScale, s.Points[i - 1].Y * _dpiScale, s.Points[i].X * _dpiScale, s.Points[i].Y * _dpiScale, GetBrush(s.Color), s.Width); }
            foreach (var ta in Owner.TextAnnotations)
            { using var fmt = new CanvasTextFormat { FontFamily = "Microsoft YaHei", FontSize = ta.FontSize, WordWrapping = CanvasWordWrapping.NoWrap }; ds.DrawText(ta.Text, ta.Position * _dpiScale, GetBrush(ta.Color), fmt); }
        }
        bg.Dispose();

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var path = System.IO.Path.Combine(desktop, $"annotation_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
            Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        var outBytes = rt.GetPixelBytes();
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)_screenW, (uint)_screenH, 96, 96, outBytes);
        await encoder.FlushAsync();
        rt.Dispose();
    }

    public void Dispose()
    {
        UninstallKeyboardHook();
        _brushCache.Clear();
        _renderTarget?.Dispose();
        _device?.Dispose();
        if (_hdcMem != IntPtr.Zero) { PInvoke.DeleteDC(_hdcMem); _hdcMem = IntPtr.Zero; }
        if (_hDib != IntPtr.Zero) { PInvoke.DeleteObject(_hDib); _hDib = IntPtr.Zero; }
        if (_hwnd != IntPtr.Zero) { PInvoke.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
        if (_gcHandle.IsAllocated) _gcHandle.Free();
    }
}
