namespace Memoria.FF8.Runtime;

public readonly record struct NativeWorldMapDisplay(byte Mode, byte ScriptMode);

/// <summary>World display selection from the mapped Select edge and the current world UI owner.</summary>
public static class NativeWorldMapControl
{
    public static NativeWorldMapDisplay Select(byte mode, byte scriptMode, NativeWorldInputHistory input, bool messageActive, bool mapWindowOccupied)
    {
        if (mode > 3)
        {
            throw new InvalidDataException("World map display mode must be in 0..3.");
        }
        input.Validate();
        ushort previous = input.PreviousButtons ?? throw new NotSupportedException("World map controls require the preceding mapped input sample.");
        bool pressed = (input.CurrentButtons & ~previous & 0x100) != 0;
        if (!pressed || messageActive || scriptMode is not (0 or 1 or 2 or 3 or 4 or 12 or 13))
        {
            return new(mode, scriptMode);
        }
        byte next = (byte)((mode + 1) % 4);
        if (next == 3)
        {
            if (scriptMode == 0 && !mapWindowOccupied)
            {
                return new(3, 12);
            }
            next = 0;
        }
        return new(next, next == 0 && scriptMode == 12 ? (byte)0 : scriptMode);
    }
}
