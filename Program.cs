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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
    private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [STAThread]
    static void Main(string[] args)
    {
        // 强制 Per-Monitor V2 DPI 感知
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        bool bootstrapInitialized = false;
        if (!IsPackagedProcess())
        {
            // 原生 Bootstrap——仅用于非打包运行；MSIX 安装后由包依赖加载 Windows App Runtime
            int hr = MddBootstrap.MddBootstrapInitialize(0x00010006, null, 0);
            if (hr < 0) return;
            bootstrapInitialized = true;
        }

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start(p =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        finally
        {
            if (bootstrapInitialized)
            {
                MddBootstrap.MddBootstrapShutdown();
            }
        }
    }

    private static bool IsPackagedProcess()
    {
        int length = 0;
        int result = GetCurrentPackageFullName(ref length, null);
        return result != APPMODEL_ERROR_NO_PACKAGE;
    }
}
