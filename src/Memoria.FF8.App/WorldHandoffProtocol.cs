using System.IO.Pipes;

namespace Memoria.FF8;

/// <summary>Orders destination readiness, Steam shutdown, visible coverage and predecessor exit.</summary>
internal sealed class WorldHandoffProtocol(PipeStream pipe, Task peerExited, CancellationToken cancellation)
{
    private Task? ready;
    private Task? released;
    private Task? covered;
    private volatile bool releaseReceived;
    private bool coverAcknowledged;

    internal bool DestinationReady => ready?.IsCompletedSuccessfully == true;
    internal bool ReleaseReceived => releaseReceived;
    internal bool IsReleased => released?.IsCompletedSuccessfully == true;
    internal bool IsCovered => covered?.IsCompletedSuccessfully == true;
    internal bool Committed { get; private set; }

    internal Task WaitForReadyAsync()
    {
        return ready ??= ExpectAsync(1, "The destination game host did not acknowledge readiness.");
    }

    internal void AcknowledgeReady()
    {
        if (released is not null)
        {
            return;
        }

        Send(1);
        released = WaitForReleaseAsync();
    }

    internal void AcknowledgeCover()
    {
        if (!ReleaseReceived)
        {
            throw new InvalidOperationException("The originating game host has not released the world transfer.");
        }

        if (coverAcknowledged)
        {
            return;
        }

        Send(3);
        coverAcknowledged = true;
    }

    internal void Release(Action shutdown)
    {
        if (!DestinationReady)
        {
            throw new InvalidOperationException("The destination game host is not ready.");
        }

        if (Committed)
        {
            throw new InvalidOperationException("The world transfer has already been released.");
        }

        shutdown();
        Committed = true;
        Send(2);
        covered = ExpectAsync(3, "The destination game host did not acknowledge visible coverage.");
    }

    internal void ThrowIfFailed()
    {
        ThrowIfFailed(ready);
        ThrowIfFailed(released);
        ThrowIfFailed(covered);
    }

    private static void ThrowIfFailed(Task? task)
    {
        if (task?.IsCompleted == true)
        {
            task.GetAwaiter().GetResult();
        }
    }

    private async Task WaitForReleaseAsync()
    {
        await ExpectAsync(2, "The world transfer was cancelled before Steam shutdown.");
        releaseReceived = true;
        await peerExited.WaitAsync(cancellation);
    }

    private async Task ExpectAsync(byte expected, string error)
    {
        byte[] message = new byte[1];
        try
        {
            await pipe.ReadExactlyAsync(message, cancellation);
        }
        catch (EndOfStreamException failure)
        {
            throw new EndOfStreamException(error, failure);
        }
        if (message[0] != expected)
        {
            throw new IOException(error);
        }
    }

    private void Send(byte message)
    {
        pipe.WriteByte(message);
        pipe.Flush();
    }
}
