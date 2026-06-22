using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using WinRT.Interop;
using System.Numerics;

namespace ScreenQuickMark;

public enum DrawMode { Pen, Text }

public sealed partial class MainWindow : Window
{
    // ─── 数据模型（共享给 OverlayWindow） ──────────
    public List<Stroke> Strokes { get; } = new();
    public List<TextAnnotation> TextAnnotations { get; } = new();
    public Stroke? CurrentStroke;
    public bool IsAnnotationMode { get; private set; } = true;
    public DrawMode Mode { get; private set; } = DrawMode.Pen;
    public Windows.UI.Color CurrentColor { get; private set; } = Windows.UI.Color.FromArgb(255, 255, 40, 40);
    public float StrokeWidth { get; private set; } = 4f;

    // 文字输入
    public bool IsTyping { get; private set; }
    public string TypingText { get; internal set; } = "";
    public Vector2 TypingPosition { get; private set; }
    public Windows.UI.Color TypingColor { get; private set; }
    public float TypingFontSize { get; private set; }

    private OverlayWindow? _overlay;
    private IntPtr _hwnd;
    private bool _toolbarVisible = true;

    private readonly HashSet<byte> _lastKeysDown = new();

    private static readonly Windows.UI.Color[] PresetColors = new[]
    {
        Windows.UI.Color.FromArgb(255, 255, 40, 40),
        Windows.UI.Color.FromArgb(255, 255, 180, 40),
        Windows.UI.Color.FromArgb(255, 255, 240, 40),
        Windows.UI.Color.FromArgb(255, 40, 220, 40),
        Windows.UI.Color.FromArgb(255, 40, 160, 255),
        Windows.UI.Color.FromArgb(255, 160, 40, 255),
        Windows.UI.Color.FromArgb(255, 255, 255, 255),
        Windows.UI.Color.FromArgb(255, 40, 40, 40),
    };
    private static readonly float[] PresetWidths = { 2f, 4f, 6f, 10f, 16f };

    public MainWindow()
    {
        this.InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);
        SetupToolbar();
        try
        {
            _overlay = new OverlayWindow(this);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash.log"),
                $"[{DateTime.Now}] OverlayWindow creation failed:\n{ex}");
            throw;
        }
        StartHotkeyPolling();
    }

    private void SetupToolbar()
    {
        int ex = PInvoke.GetWindowLong(_hwnd, PInvoke.GWL_EXSTYLE);
        ex |= (int)(PInvoke.WS_EX_TOOLWINDOW | PInvoke.WS_EX_NOACTIVATE);
        PInvoke.SetWindowLong(_hwnd, PInvoke.GWL_EXSTYLE, ex);

        int style = PInvoke.GetWindowLong(_hwnd, PInvoke.GWL_STYLE);
        style &= ~(int)(PInvoke.WS_CAPTION | PInvoke.WS_THICKFRAME | PInvoke.WS_SYSMENU);
        PInvoke.SetWindowLong(_hwnd, PInvoke.GWL_STYLE, style);

        BuildToolbar();
        // 强制布局后根据内容自动适配窗口宽度
        Toolbar.UpdateLayout();
        Toolbar.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        double dpiScale = PInvoke.GetDpiForWindow(_hwnd) / 96.0;
        int w = (int)Math.Ceiling((Toolbar.DesiredSize.Width + 22) * dpiScale);
        int h = (int)Math.Ceiling((Toolbar.DesiredSize.Height + 22) * dpiScale);
        PInvoke.SetWindowPos(_hwnd, PInvoke.HWND_TOPMOST,
            100, 10, w, h,
            PInvoke.SWP_NOACTIVATE | PInvoke.SWP_FRAMECHANGED | PInvoke.SWP_SHOWWINDOW);
    }

    private void BuildToolbar()
    {
        var tb = Toolbar;

        foreach (var c in PresetColors)
            tb.Children.Add(ColorBtn(c));
        tb.Children.Add(Sep());
        foreach (var w in PresetWidths)
            tb.Children.Add(WidthBtn(w));
        tb.Children.Add(Sep());
        tb.Children.Add(ModeBtn("✎", DrawMode.Pen));
        tb.Children.Add(ModeBtn("Aa", DrawMode.Text));
        tb.Children.Add(Sep());
        tb.Children.Add(UndoBtn());
        tb.Children.Add(Sep());
        tb.Children.Add(ClearBtn());
        tb.Children.Add(ExitBtn());
    }

    private Button ColorBtn(Windows.UI.Color c)
    {
        var b = new Button
        {
            Width = 26, Height = 26,
            Margin = new Thickness(3, 0, 3, 0),
            Background = new SolidColorBrush(c),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(0)
        };
        b.Click += (_, _) => { CurrentColor = c; };
        return b;
    }

    private Button WidthBtn(float w)
    {
        var b = new Button
        {
            Width = 32, Height = 26,
            Margin = new Thickness(2, 0, 2, 0),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255)),
            CornerRadius = new CornerRadius(5),
            Content = new TextBlock { Text = "●", FontSize = Math.Min(w + 2, 18),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center }
        };
        b.Click += (_, _) => { StrokeWidth = w; };
        return b;
    }

    private Button ModeBtn(string label, DrawMode mode)
    {
        bool isActive = mode == Mode;
        var b = new Button
        {
            Width = label.Length > 1 ? 40 : 32,
            Height = 26,
            Margin = new Thickness(2, 0, 2, 0),
            Background = isActive
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(130, 255, 255, 255))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255)),
            CornerRadius = new CornerRadius(5),
            Content = new TextBlock { Text = label, FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center }
        };
        b.Click += (_, _) => { if (IsTyping) { CancelTyping(); _overlay?.UninstallKeyboardHook(); } Mode = mode; UpdateModeButtons(); _overlay?.RenderFrame(); };
        return b;
    }

    private Button UndoBtn() => SmallBtn("↩", () =>
    {
        if (TextAnnotations.Count > 0) TextAnnotations.RemoveAt(TextAnnotations.Count - 1);
        else if (Strokes.Count > 0) Strokes.RemoveAt(Strokes.Count - 1);
        _overlay?.RenderFrame();
    });

    private Button ClearBtn() => SmallBtn("✕", () =>
    {
        Strokes.Clear(); TextAnnotations.Clear(); CurrentStroke = null;
        _overlay?.RenderFrame();
    });

    private Button ExitBtn() => SmallBtn("╳", () => CloseAll());

    private Button SmallBtn(string text, Action click)
    {
        var b = new Button
        {
            Width = 32, Height = 26,
            Margin = new Thickness(2, 0, 2, 0),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255)),
            CornerRadius = new CornerRadius(5),
            Content = new TextBlock { Text = text, FontSize = 13,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center }
        };
        b.Click += (_, _) => click();
        return b;
    }

    private Border Sep() => new Border { Width = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255)),
        Margin = new Thickness(5, 5, 5, 5) };

    // ─── 快捷键 ──────────────────────────────────
    private bool _lastToggle, _lastClear, _lastUndo, _lastToolbar, _lastModeSwitch;
    private void StartHotkeyPolling()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            static bool down(int vk) => (PInvoke.GetAsyncKeyState(vk) & 0x8000) != 0;
            bool ctrl = down(0x11), shift = down(0x10);

            if (ctrl && shift && down(0x51)) { CloseAll(); return; }

            // 快捷键在文字输入期间依然生效（GetAsyncKeyState 不受钩子影响）
            var toggle = ctrl && shift && down(0x44);
            if (toggle && !_lastToggle) Toggle();
            _lastToggle = toggle;

            // Ctrl+Shift+E → 圈画 ↔ 文字 互切
            var modeSwitch = ctrl && shift && down(0x45);
            if (modeSwitch && !_lastModeSwitch) SwitchPenText();
            _lastModeSwitch = modeSwitch;

            var clear = ctrl && shift && down(0x43);
            if (clear && !_lastClear) { Strokes.Clear(); TextAnnotations.Clear(); CurrentStroke = null; _overlay?.RenderFrame(); }
            _lastClear = clear;

            var undo = ctrl && shift && down(0x5A);
            if (undo && !_lastUndo) { if (TextAnnotations.Count > 0) TextAnnotations.RemoveAt(TextAnnotations.Count - 1); else if (Strokes.Count > 0) Strokes.RemoveAt(Strokes.Count - 1); _overlay?.RenderFrame(); }
            _lastUndo = undo;

            var tb = ctrl && shift && down(0x54);
            if (tb && !_lastToolbar) ToggleToolbar();
            _lastToolbar = tb;

            // 文字输入期间，文本键由键盘钩子全权处理，轮询不干预
            if (IsTyping) return;
        };
        timer.Start();
    }

    private void Toggle()
    {
        IsAnnotationMode = !IsAnnotationMode;
        _overlay?.SetAnnotationMode(IsAnnotationMode);
        _overlay?.RenderFrame();
    }

    private void UpdateModeButtons()
    {
        // 简单重绘工具栏以更新模式按钮高亮
        // 由于 WinUI 控件绑定有限，直接遍历 StackPanel 找到 ModeBtn 并更新背景
        foreach (var child in Toolbar.Children)
        {
            if (child is Button b && b.Content is TextBlock tb && (tb.Text == "✎" || tb.Text == "Aa"))
            {
                bool isActive = (tb.Text == "✎" && Mode == DrawMode.Pen)
                    || (tb.Text == "Aa" && Mode == DrawMode.Text);
                b.Background = isActive
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(130, 255, 255, 255))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255));
            }
        }
    }

    internal void BringToolbarToFront()
    {
        if (!_toolbarVisible) return;
        PInvoke.SetWindowPos(_hwnd, PInvoke.HWND_TOPMOST,
            0, 0, 0, 0,
            PInvoke.SWP_NOMOVE | PInvoke.SWP_NOSIZE | PInvoke.SWP_NOACTIVATE | PInvoke.SWP_SHOWWINDOW);
    }

    private void ToggleToolbar()
    {
        _toolbarVisible = !_toolbarVisible;
        PInvoke.ShowWindow(_hwnd, _toolbarVisible ? PInvoke.SW_SHOW : PInvoke.SW_HIDE);
        BringToolbarToFront();
    }

    private void SwitchPenText()
    {
        // 标注模式下有效，打字中先取消
        if (!IsAnnotationMode) return;
        if (IsTyping) CancelTyping();
        Mode = Mode == DrawMode.Pen ? DrawMode.Text : DrawMode.Pen;
        UpdateModeButtons();
        _overlay?.RenderFrame();
    }

    private void CloseAll()
    {
        _overlay?.Dispose();
        this.Close();
    }

    // ─── 文字输入（由 OverlayWindow 鼠标事件触发）──
    internal void StartTypingInternal(Vector2 dipPos)
    {
        IsTyping = true; TypingText = "";
        TypingPosition = dipPos; TypingColor = CurrentColor;
        TypingFontSize = Math.Max(16f, 10f + StrokeWidth * 3f);
        _lastKeysDown.Clear();
        _overlay?.RenderFrame();
    }

    private void ProcessTypingKeys(bool ctrl, bool shift, Func<int, bool> down)
    {
        if (down(0x1B)) { CancelTyping(); return; }
        if (down(0x0D)) { if (!_lastKeysDown.Contains(0x0D)) { _lastKeysDown.Add(0x0D); CommitTyping(); } return; }
        if (down(0x08)) { if (!_lastKeysDown.Contains(0x08) && TypingText.Length > 0) { TypingText = TypingText[..^1]; _overlay?.RenderFrame(); } _lastKeysDown.Add(0x08); return; }
        for (byte vk = 0x20; vk <= 0xDE; vk++)
        {
            if (!down(vk) || _lastKeysDown.Contains(vk)) continue;
            _lastKeysDown.Add(vk);
            char? ch = VkToChar(vk, shift);
            if (ch != null) { TypingText += ch.Value; _overlay?.RenderFrame(); }
        }
        _lastKeysDown.RemoveWhere(vk => !down(vk));
    }

    public static char? VkToChar(byte vk, bool shift)
    {
        if (vk >= 0x30 && vk <= 0x39) return shift ? ")!@#$%^&*("[vk - 0x30] : (char)('0' + vk - 0x30);
        if (vk >= 0x41 && vk <= 0x5A) { char c = (char)('A' + vk - 0x41); return shift ? c : char.ToLowerInvariant(c); }
        return vk switch
        {
            0x20 => ' ', 0xBA => shift ? ':' : ';', 0xBB => shift ? '+' : '=',
            0xBC => shift ? '<' : ',', 0xBD => shift ? '_' : '-', 0xBE => shift ? '>' : '.',
            0xBF => shift ? '?' : '/', 0xC0 => shift ? '~' : '`',
            0xDB => shift ? '{' : '[', 0xDC => shift ? '|' : '\\', 0xDD => shift ? '}' : ']',
            0xDE => shift ? '"' : '\'', _ => null
        };
    }

    internal void CommitTyping()
    {
        _overlay?.UninstallKeyboardHook();
        IsTyping = false; _lastKeysDown.Clear();
        if (!string.IsNullOrEmpty(TypingText))
            TextAnnotations.Add(new TextAnnotation(TypingText, TypingPosition, TypingColor, TypingFontSize));
        TypingText = ""; _overlay?.RenderFrame();
    }

    internal void CancelTyping()
    {
        _overlay?.UninstallKeyboardHook();
        IsTyping = false; _lastKeysDown.Clear();
        TypingText = ""; _overlay?.RenderFrame();
    }
}

public class Stroke
{
    public List<Vector2> Points { get; } = new();
    public Windows.UI.Color Color { get; }
    public float Width { get; }
    public Stroke(Windows.UI.Color color, float width) { Color = color; Width = width; }
}

public class TextAnnotation
{
    public string Text { get; }
    public Vector2 Position { get; }
    public Windows.UI.Color Color { get; }
    public float FontSize { get; }
    public TextAnnotation(string text, Vector2 position, Windows.UI.Color color, float fontSize)
    { Text = text; Position = position; Color = color; FontSize = fontSize; }
}
