using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Memoria.FF8.GameUi;

/// <summary>Observes the owning GUI thread's HWND, including native hosts whose GameWindow.Handle is an SDL_Window*.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsImmCompositionSource : IDisposable
{
    private const uint CompositionString = 8;
    private const uint ResultString = 0x800;
    private readonly nint hwnd;
    private readonly Action<string> commit;
    private readonly Action<string, bool> compose;
    private readonly SubclassProc callback;
    private string pendingResultCharacters = "";
    private bool attached;
    public WindowsImmCompositionSource(nint window, Action<string> committed, Action<string, bool> composition)
    {
        hwnd = window;
        commit = committed;
        compose = composition;
        callback = WindowMessage;
        attached = IsWindow(hwnd) && SetWindowSubclass(hwnd, callback, 0x4D464638, 0);
        if (!attached)
        {
            Problem = "The game window cannot attach its Unicode/IME text input observer.";
        }
        else
        {
            string value = Read(hwnd, CompositionString);
            if (value.Length != 0)
            {
                compose(value, true);
            }
        }
    }
    public bool Enabled { get; set; } = true;
    public string? Problem { get; }
    private nint WindowMessage(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (Enabled)
        {
            switch (message)
            {
                case 0x100:
                    pendingResultCharacters = "";
                    break;
                case 0x102:
                    char character = (char)wParam;
                    if (pendingResultCharacters.Length != 0 && pendingResultCharacters[0] == character)
                    {
                        pendingResultCharacters = pendingResultCharacters[1..];
                    }
                    else if (!char.IsControl(character))
                    {
                        pendingResultCharacters = "";
                        commit(character.ToString());
                    }
                    break;
                case 0x10D:
                    compose("", true);
                    break;
                case 0x10E:
                    compose("", false);
                    break;
                case 0x10F:
                    if (((long)lParam & ResultString) != 0)
                    {
                        pendingResultCharacters = Read(hwnd, ResultString);
                        compose("", false);
                        if (pendingResultCharacters.Length != 0)
                        {
                            commit(pendingResultCharacters);
                        }
                    }
                    if (((long)lParam & CompositionString) != 0)
                    {
                        string value = Read(hwnd, CompositionString);
                        compose(value, value.Length != 0);
                    }
                    break;
                case 0x8:
                    pendingResultCharacters = "";
                    compose("", false);
                    break;
            }
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }
    public static nint FocusedWindow()
    {
        nint window = GetFocus();
        if (window == 0)
        {
            window = GetActiveWindow();
        }
        GetWindowThreadProcessId(window, out uint process);
        return process == Environment.ProcessId ? window : 0;
    }
    private static string Read(nint window, uint index)
    {
        nint context = ImmGetContext(window);
        if (context == 0)
        {
            return "";
        }
        try
        {
            int length = ImmGetCompositionStringW(context, index, null, 0);
            if (length <= 0)
            {
                return "";
            }
            byte[] bytes = new byte[length];
            int copied = ImmGetCompositionStringW(context, index, bytes, bytes.Length);
            return copied > 0 ? System.Text.Encoding.Unicode.GetString(bytes, 0, copied) : "";
        }
        finally
        {
            ImmReleaseContext(window, context);
        }
    }
    public void Dispose()
    {
        if (attached)
        {
            RemoveWindowSubclass(hwnd, callback, 0x4D464638);
            attached = false;
        }
        GC.KeepAlive(callback);
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")]
    private static extern nint GetFocus();
    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);
    [DllImport("imm32.dll")]
    private static extern nint ImmGetContext(nint hwnd);
    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(nint hwnd, nint context);
    [DllImport("imm32.dll")]
    private static extern int ImmGetCompositionStringW(nint context, uint index, byte[]? buffer, int bytes);
}
