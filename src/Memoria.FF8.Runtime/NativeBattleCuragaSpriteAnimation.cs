namespace Memoria.FF8.Runtime;

/// <summary>880CE0's signed-byte sprite-frame advance; all stores target the original geometry node.</summary>
public static class NativeBattleCuragaSpriteAnimation
{
    public static NativeBattleCuragaFrame Advance880CE0(NativeBattleCuragaFrame frame, uint contextTask, int node, sbyte resourceKind)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        if (resourceKind is < 0 or > 2)
        {
            return frame;
        }
        sbyte phase = unchecked((sbyte)(memory.Byte(node + 0x1D0) + 1));
        memory.Byte(node + 0x1D0, unchecked((byte)phase));
        if (resourceKind == 2 && phase > unchecked((sbyte)memory.Byte(node + 0x1D4)))
        {
            sbyte repeats = unchecked((sbyte)memory.Byte(node + 0x1D5));
            if (repeats > 0)
            {
                memory.Byte(node + 0x1D5, unchecked((byte)(repeats - 1)));
                phase = unchecked((sbyte)memory.Byte(node + 0x1D3));
                memory.Byte(node + 0x1D0, unchecked((byte)phase));
            }
        }
        if (phase > unchecked((sbyte)memory.Byte(node + 0x1D1)))
        {
            memory.Byte(node + 0x1D0, 0);
            if (resourceKind != 1)
            {
                memory.Byte(node + 0x1D2, 1);
            }
        }
        return memory.Flush();
    }
}
