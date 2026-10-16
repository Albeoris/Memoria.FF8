using Albeoris.Games.Core.Abstractions.NsStreams;

namespace Albeoris.Games.Core.NsStreams;

public class DisposableStream : DelegatingStream
{
    public delegate void DisposingDelegate(Stream stream, Boolean managedDisposing);

    public event DisposingDelegate? BeforeDispose;
    public event DisposingDelegate? AfterDispose;

    public event Action<Stream>? BeforeDisposeAsync;
    public event Action<Stream>? AfterDisposeAsync;

    public DisposableStream(Stream baseStream)
        : base(baseStream)
    {
    }

    public override void Close()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(Boolean disposing)
    {
        DisposingDelegate? before = Interlocked.Exchange(ref BeforeDispose, null);
        before?.Invoke(this, disposing);

        base.Dispose(disposing);

        DisposingDelegate? after = Interlocked.Exchange(ref AfterDispose, null);
        after?.Invoke(this, disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        Action<Stream>? before = Interlocked.Exchange(ref BeforeDisposeAsync, null);
        before?.Invoke(this);

        await base.DisposeAsync();

        Action<Stream>? after = Interlocked.Exchange(ref AfterDisposeAsync, null);
        after?.Invoke(this);
    }
}