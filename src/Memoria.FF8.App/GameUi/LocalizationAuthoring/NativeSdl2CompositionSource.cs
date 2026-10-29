using System.Runtime.InteropServices;

namespace Memoria.FF8.GameUi;

/// <summary>Attaches to the SDL2 instance embedded in the actual graphics host, independently of SDL3 services.</summary>
internal sealed class NativeSdl2CompositionSource : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CompositionCallback(int committed, nint utf8);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetCallback(nint window, CompositionCallback? callback);
    private readonly CompositionCallback callback;
    private readonly SetCallback? setCallback;
    private readonly nint library;
    private readonly nint window;
    public volatile bool Enabled = true;
    public NativeSdl2CompositionSource(nint nativeWindow, Action<string> commit, Action<string> composition)
    {
        window = nativeWindow;
        callback = (committed, text) =>
        {
            if (!Enabled)
            {
                return;
            }
            string value = Marshal.PtrToStringUTF8(text) ?? "";
            if (committed != 0)
            {
                commit(value);
            }
            else
            {
                composition(value);
            }
        };
        string filename = OperatingSystem.IsWindows() ? "mgruntime.dll" : OperatingSystem.IsMacOS() ? "libmgruntime.dylib" : "libmgruntime.so";
        string path = Path.Combine(AppContext.BaseDirectory, filename);
        if (!NativeLibrary.TryLoad(path, out library))
        {
            Problem = "The native graphics runtime cannot provide IME composition events.";
            return;
        }
        if (!NativeLibrary.TryGetExport(library, "Memoria_TextInput_SetCallback", out nint address))
        {
            Problem = "This native graphics runtime lacks the SDL2 IME bridge; IME authoring is unavailable on this host.";
            return;
        }
        setCallback = Marshal.GetDelegateForFunctionPointer<SetCallback>(address);
        if (setCallback(window, callback) == 0)
        {
            Problem = "The native graphics window is not ready for IME composition.";
        }
    }
    public string? Problem { get; }
    public void Dispose()
    {
        setCallback?.Invoke(window, null);
        if (library != 0)
        {
            NativeLibrary.Free(library);
        }
        GC.KeepAlive(callback);
    }
}
