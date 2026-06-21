using System.Runtime.InteropServices;

namespace ScreenQuickMark;

public static class MddBootstrap
{
    private const string DllName = "Microsoft.WindowsAppRuntime.Bootstrap.dll";

    [DllImport(DllName, ExactSpelling = true)]
    public static extern int MddBootstrapInitialize(
        uint majorMinorVersion,
        [MarshalAs(UnmanagedType.LPWStr)] string? versionTag,
        ulong minVersion);

    [DllImport(DllName, ExactSpelling = true)]
    public static extern void MddBootstrapShutdown();
}

public static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(int value);

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
    private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    [STAThread]
    static void Main(string[] args)
    {
        // 强制 Per-Monitor V2 DPI 感知
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        // 原生 Bootstrap——从 NuGet 缓存加载 Windows App Runtime，无需系统安装
        int hr = MddBootstrap.MddBootstrapInitialize(0x00010006, null, 0);
        if (hr < 0) return;

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(p =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
