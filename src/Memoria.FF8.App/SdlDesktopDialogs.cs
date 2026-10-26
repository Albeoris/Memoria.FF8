using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Memoria.FF8.Platform;
using SDL3;

namespace Memoria.FF8;

/// <summary>Owns native dialog callbacks and their buffers, never the host's SDL video subsystem or event queue.</summary>
internal sealed class SdlDesktopDialogs : IDesktopDialogs, IDisposable
{
    private static readonly SDL.DialogFileCallback Callback = Complete;
    private static readonly ConcurrentDictionary<nint, PendingDialog> Pending = new();
    private static long nextId;
    private readonly nint sdlWindow;
    private readonly Action<SDL.FileDialogType, FileDialogRequest, nint, SDL.DialogFileCallback> start;
    private readonly Action pump;
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private bool disposed;

    internal SdlDesktopDialogs(nint sdlWindow = 0, Action<SDL.FileDialogType, FileDialogRequest, nint, SDL.DialogFileCallback>? start = null, Action? pump = null)
    {
        this.sdlWindow = sdlWindow;
        this.start = start ?? ShowNative;
        this.pump = pump ?? SDL.PumpEvents;
    }

    internal int NativeCallbacksPending => Pending.Values.Count(pending => ReferenceEquals(pending.Owner, this));

    public Task<DialogResult> OpenFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Show(request, SDL.FileDialogType.OpenFile, cancellationToken);
    public Task<DialogResult> SaveFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Show(request, SDL.FileDialogType.SaveFile, cancellationToken);
    public Task<DialogResult> SelectFolderAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Show(request, SDL.FileDialogType.OpenFolder, cancellationToken);

    internal void PumpPendingEvents()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Environment.CurrentManagedThreadId != ownerThread)
        {
            throw new InvalidOperationException("SDL dialog events must be pumped on the game host's main thread.");
        }
        if (NativeCallbacksPending != 0)
        {
            // Pump only SDL3's subsystem events. The graphics host retains its
            // own window loop, and this adapter never polls or consumes events.
            pump();
        }
    }

    private Task<DialogResult> Show(FileDialogRequest request, SDL.FileDialogType type, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Environment.CurrentManagedThreadId != ownerThread)
        {
            throw new InvalidOperationException("Native file dialogs must be started on the game host's main thread.");
        }
        if (token.IsCancellationRequested)
        {
            return Task.FromCanceled<DialogResult>(token);
        }
        nint id = checked((nint)Interlocked.Increment(ref nextId));
        PendingDialog pending = new(this);
        Pending[id] = pending;
        pending.Cancellation = token.Register(() => pending.Completion.TrySetCanceled(token));
        try
        {
            start(type, request, id, Callback);
        }
        catch (Exception error)
        {
            if (Pending.TryRemove(id, out _))
            {
                pending.Dispose();
                pending.Completion.TrySetResult(new(DialogStatus.Failed, Error: error.Message));
            }
        }
        return pending.Completion.Task;
    }

    private void ShowNative(SDL.FileDialogType type, FileDialogRequest request, nint id, SDL.DialogFileCallback callback)
    {
        PendingDialog pending = Pending[id];
        pending.Properties = SDL.CreateProperties();
        if (pending.Properties == 0)
        {
            throw new IOException(SDL.GetError());
        }
        Check(SDL.SetStringProperty(pending.Properties, SDL.Props.FileDialogTitleString, request.Title));
        if (request.InitialPath is not null)
        {
            Check(SDL.SetStringProperty(pending.Properties, SDL.Props.FileDialogLocationString, request.InitialPath));
        }
        // An HWND is never an SDL_Window*. DX11 uses an unparented native picker;
        // an SDL host supplies its actual SDL_Window through the constructor.
        if (sdlWindow != 0)
        {
            Check(SDL.SetPointerProperty(pending.Properties, SDL.Props.FileDialogWindowPointer, sdlWindow));
        }
        if (type != SDL.FileDialogType.OpenFolder && request.Filters is { Count: > 0 } filters)
        {
            pending.Filters = Marshal.AllocHGlobal(checked(filters.Count * Marshal.SizeOf<NativeFilter>()));
            for (int index = 0; index < filters.Count; index++)
            {
                nint name = pending.Utf8(filters[index].Name);
                string pattern = string.Join(';', filters[index].Extensions.Select(extension => extension == "*" ? "*" : extension.TrimStart('.', '*')));
                nint extension = pending.Utf8(pattern);
                Marshal.StructureToPtr(new NativeFilter(name, extension), pending.Filters + index * Marshal.SizeOf<NativeFilter>(), false);
            }
            Check(SDL.SetPointerProperty(pending.Properties, SDL.Props.FileDialogFiltersPointer, pending.Filters));
            Check(SDL.SetNumberProperty(pending.Properties, SDL.Props.FileDialogNFiltersNumber, filters.Count));
        }
        SDL.ShowFileDialogWithProperties(type, callback, id, pending.Properties);
    }

    private static void Check(bool success)
    {
        if (!success)
        {
            throw new IOException(SDL.GetError());
        }
    }

    private static void Complete(nint id, nint fileList, int filter)
    {
        if (!Pending.TryRemove(id, out PendingDialog? pending))
        {
            return;
        }
        try
        {
            pending.Completion.TrySetResult(ReadResult(fileList, SDL.GetError));
        }
        catch (Exception error)
        {
            pending.Completion.TrySetResult(new(DialogStatus.Failed, Error: error.Message));
        }
        finally
        {
            pending.Dispose();
        }
    }

    internal static DialogResult ReadResult(nint fileList, Func<string> readError)
    {
        if (fileList == 0)
        {
            return new(DialogStatus.Failed, Error: readError());
        }
        nint first = Marshal.ReadIntPtr(fileList);
        return first == 0 ? new(DialogStatus.Cancelled) : new(DialogStatus.Selected, Marshal.PtrToStringUTF8(first));
    }

    public void Dispose()
    {
        disposed = true;
        foreach (PendingDialog pending in Pending.Values.Where(pending => ReferenceEquals(pending.Owner, this)))
        {
            pending.Completion.TrySetCanceled();
        }
        // SDL has no API to close an already displayed picker. Its callback and
        // filter storage remain rooted until that native picker actually closes.
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeFilter(nint Name, nint Pattern);

    private sealed class PendingDialog(SdlDesktopDialogs owner) : IDisposable
    {
        private readonly List<nint> strings = [];
        internal SdlDesktopDialogs Owner { get; } = owner;
        internal TaskCompletionSource<DialogResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationTokenRegistration Cancellation;
        internal uint Properties;
        internal nint Filters;

        internal nint Utf8(string value)
        {
            nint pointer = Marshal.StringToCoTaskMemUTF8(value);
            strings.Add(pointer);
            return pointer;
        }

        public void Dispose()
        {
            Cancellation.Dispose();
            if (Properties != 0)
            {
                SDL.DestroyProperties(Properties);
            }
            if (Filters != 0)
            {
                Marshal.FreeHGlobal(Filters);
            }
            foreach (nint pointer in strings)
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }
    }
}
