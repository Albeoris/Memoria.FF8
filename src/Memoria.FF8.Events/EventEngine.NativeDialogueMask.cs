using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static NativeEventMemory RestoreNativeDialogueMask(NativeEventMemory memory, IEnumerable<DialogueState> windows)
    {
        byte occupied = 0;
        foreach (DialogueState window in windows)
        {
            if ((uint)window.Channel >= 8)
            {
                throw new InvalidDataException("Saved native dialogue has an invalid channel.");
            }
            if (window.IsNamePrompt)
            {
                continue;
            }
            occupied |= unchecked((byte)(1 << window.Channel));
        }
        return memory.Write(211, 1, NativeScalarValue.Exact(occupied));
    }

    private byte ReadNativeDialogueMask()
    {
        return checked((byte)EventMemory.Read(211, 1).RequireExact());
    }

    private bool NativeDialogueChannelOccupied(int channel)
    {
        return (ReadNativeDialogueMask() & (1 << channel)) != 0;
    }

    private string? UnsupportedNativeDialogueMask()
    {
        try
        {
            _ = ReadNativeDialogueMask();
            return null;
        }
        catch (NotSupportedException)
        {
            return "Native dialogue requires its exact BYTE211 occupied-channel mask";
        }
    }

    private void SetNativeDialogueChannelOccupied(int channel, bool occupied)
    {
        byte previous = ReadNativeDialogueMask();
        int bit = 1 << channel;
        byte next = unchecked((byte)(occupied ? previous | bit : previous & ~bit));
        SetEventMemory(EventMemory.Write(211, 1, NativeScalarValue.Exact(next)));
    }
}
