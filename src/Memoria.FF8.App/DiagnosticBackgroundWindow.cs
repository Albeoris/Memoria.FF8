using System.ComponentModel;
using System.Runtime.InteropServices;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class DiagnosticBackgroundWindow : IDisposable
{
    private const int ComputerBasedTrainingHook = 5;
    private const int ActivateWindow = 5;
    private readonly HookProcedure callback;
    private readonly nint hook;

    private DiagnosticBackgroundWindow()
    {
        callback = FilterActivation;
        hook = SetWindowsHookEx(ComputerBasedTrainingHook, callback, 0, GetCurrentThreadId());
        if (hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not prevent diagnostic window activation.");
        }
    }

    internal static IDisposable? FromEnvironment(LaunchOptions options)
    {
        if (!options.IsDiagnosticGraphicalHost || Environment.GetEnvironmentVariable("MEMORIA_FF8_DIAGNOSTIC_BACKGROUND_WINDOW") != "1")
        {
            return null;
        }
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Diagnostic background window activation control requires Windows.");
        }
        return new DiagnosticBackgroundWindow();
    }

    private static nint FilterActivation(int code, nint window, nint data)
    {
        if (code == ActivateWindow)
        {
            return 1;
        }
        return CallNextHookEx(0, code, window, data);
    }

    public void Dispose()
    {
        UnhookWindowsHookEx(hook);
        GC.KeepAlive(callback);
    }

    private delegate nint HookProcedure(int code, nint window, nint data);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int type, HookProcedure callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint window, nint data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
}
