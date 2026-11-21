using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public Func<byte, bool>? NativeDiscMediaAvailable { get; set; }

    private FieldHostReply ChangeDisc(long requestId, FieldDiscChangeCommand command)
    {
        if (Process.DiscChange is { } current && current.RequestId == requestId)
        {
            if (Process.GlobalCommand != Ff7GlobalCommand.DiscChange || Process.GlobalRequestId != requestId || current.Disc != command.Disc)
            {
                throw new InvalidDataException("The native disc-change process lost its admitted request identity.");
            }
            if (current.Completed)
            {
                Process = Process with { DiscChange = null, GlobalCommand = Ff7GlobalCommand.None, GlobalRequestId = null };
                return new(true);
            }
            return new(false);
        }
        if (Process.GlobalCommand != Ff7GlobalCommand.None)
        {
            return new(false);
        }
        Process = Process with { DiscChange = new(requestId, command.Disc), GlobalCommand = Ff7GlobalCommand.DiscChange, GlobalRequestId = requestId };
        // The VM writes its existing disc byte only on this first actual admission.
        return new(false, 1);
    }

    public void AdvanceNativeDiscChange()
    {
        if (Process.GlobalCommand != Ff7GlobalCommand.DiscChange || Process.DiscChange is not { Completed: false } change || Process.GlobalRequestId != change.RequestId)
        {
            return;
        }
        bool available = (NativeDiscMediaAvailable ?? throw new NotSupportedException("Native disc change requires a media-availability backend."))(change.Disc);
        if (available)
        {
            Process = Process with { DiscChange = change with { Completed = true } };
        }
    }
}
