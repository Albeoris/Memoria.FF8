using Albeoris.Games.Core.Abstractions.NsStreams;

namespace Albeoris.Games.Core.NsStreams;

public class RestrictedStream : DelegatingStream
{
    public Boolean CanFlush { get; init; } = true;
    public Boolean CanDispose { get; init; } = true;

    public RestrictedStream(Stream baseStream)
        : base(baseStream)
    {
    }

    public override void Flush()
    {
        if (CanFlush)
            base.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return CanFlush ? base.FlushAsync(cancellationToken) : Task.CompletedTask;
    }

    public override void Close()
    {
        if (CanDispose)
            base.Close();
    }

    protected override void Dispose(Boolean disposing)
    {
        if (CanDispose)
            base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        return CanDispose ? base.DisposeAsync() : ValueTask.CompletedTask;
    }
}