namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeLifetimeStep(NativeBattleCuragaFrame Frame, bool Complete);

public static partial class NativeBattleCuragaNodeKinematics
{
    /// <summary>881650 decrements only the selected lifetime mode and8816D0 appends its optional child before returning completion.</summary>
    public static NativeBattleCuragaNodeLifetimeStep CompleteGeometry881650(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        byte[] source = ReadSource(memory, frame.Memory, unchecked((sbyte)memory.Byte(node + 0x1D6)));
        bool complete = CompleteGeometry(memory, node, source);
        return new(memory.Flush(), complete);
    }

    /// <summary>882D90 tests each of the four literal birth flags independently and appends children in source order.</summary>
    public static NativeBattleCuragaFrame AppendChildren882D90(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        byte[] source = ReadSource(memory, frame.Memory, unchecked((sbyte)memory.Byte(node + 0x1D6)));
        for (int index = 0; index < 4; index++)
        {
            if (source[0x28 + index] == 1)
            {
                NativeBattleCuragaNodes.AllocateEmitter(memory, memory.Pointer(node), unchecked((sbyte)source[0x2C + index]), unchecked((sbyte)memory.Byte(node + 0x1D9)));
            }
        }
        return memory.Flush();
    }

    private static bool CompleteGeometry(NativeBattleCuragaNodeMemory memory, int node, ReadOnlySpan<byte> source)
    {
        bool complete;
        switch (unchecked((sbyte)source[0x1E]))
        {
            case 0:
                complete = memory.Byte(node + 0x1D2) == 1;
                break;
            case 1:
            case 2:
                short remaining = unchecked((short)(memory.Word(node + 0x1C8) - 1));
                memory.Word(node + 0x1C8, unchecked((ushort)remaining));
                complete = remaining < 0 || source[0x1E] == 2 && memory.Byte(node + 0x1D2) == 1;
                break;
            default:
                complete = false;
                break;
        }
        if (complete && source[0x30] == 1)
        {
            NativeBattleCuragaNodes.AllocateEmitter(memory, memory.Pointer(node), unchecked((sbyte)source[0x31]), unchecked((sbyte)memory.Byte(node + 0x1D9)));
        }
        return complete;
    }
}
