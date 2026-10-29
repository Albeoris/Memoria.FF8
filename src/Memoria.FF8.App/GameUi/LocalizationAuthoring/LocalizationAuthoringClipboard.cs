using SDL3;

namespace Memoria.FF8.GameUi;

/// <summary>Owns only the SDL3 video reference needed by clipboard services; it creates no window and consumes no events.</summary>
public sealed class LocalizationAuthoringClipboard : IDisposable
{
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private bool ownsVideo;
    private bool ready;
    private bool disposed;
    private readonly Action initialize;
    private readonly Func<string, bool> write;
    private readonly Func<string> read;
    private readonly Func<string> error;
    private readonly Action clearError;
    public LocalizationAuthoringClipboard() : this(null, null, null, null)
    {
    }
    internal LocalizationAuthoringClipboard(Action? initialize, Func<string, bool>? write, Func<string>? read, Func<string>? error)
    {
        this.initialize = initialize ?? InitializeVideo;
        this.write = write ?? SDL.SetClipboardText;
        this.read = read ?? (() => SDL.GetClipboardText() ?? "");
        this.error = error ?? SDL.GetError;
        clearError = initialize is null ? () => SDL.ClearError() : () => { };
    }
    private void InitializeVideo()
    {
        if ((SDL.WasInit(SDL.InitFlags.Video) & SDL.InitFlags.Video) == 0)
        {
            if (!SDL.InitSubSystem(SDL.InitFlags.Video))
            {
                throw new IOException("Clipboard video service could not initialize: " + SDL.GetError());
            }
            ownsVideo = true;
        }
    }
    private void EnsureReady()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Environment.CurrentManagedThreadId != ownerThread)
        {
            throw new InvalidOperationException("Clipboard operations must run on the game host's main thread.");
        }
        if (!ready)
        {
            initialize();
            ready = true;
        }
    }
    public void Copy(string text)
    {
        EnsureReady();
        if (!write(text))
        {
            throw new IOException("Clipboard copy failed: " + error());
        }
    }
    public string Paste()
    {
        EnsureReady();
        clearError();
        string text = read();
        string problem = error();
        if (!string.IsNullOrEmpty(problem))
        {
            throw new IOException("Clipboard paste failed: " + problem);
        }
        return text;
    }
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        if (ownsVideo)
        {
            if (Environment.CurrentManagedThreadId != ownerThread)
            {
                throw new InvalidOperationException("Clipboard video service must be released on its owning thread.");
            }
            SDL.QuitSubSystem(SDL.InitFlags.Video);
        }
        disposed = true;
    }
}
