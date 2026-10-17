namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeAllocation(NativeBattleCuragaFrame Frame, int? ArenaOffset);

/// <summary>881700/882080's exact scan/clear/link allocators and8817F0's signed emitter lifetime.</summary>
public static class NativeBattleCuragaNodes
{
    public static NativeBattleCuragaNodeAllocation AllocateEmitter881700(NativeBattleCuragaFrame frame, uint contextTask, uint parentPointer, sbyte sourceIndex, sbyte targetIndex)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        int? node = AllocateEmitter(memory, parentPointer, sourceIndex, targetIndex);
        return new(memory.Flush(), node);
    }

    public static NativeBattleCuragaNodeAllocation AllocateGeometry882080(NativeBattleCuragaFrame frame, uint contextTask, int emitter, sbyte sourceIndex)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        int? node = AllocateGeometry(memory, emitter, sourceIndex);
        return new(memory.Flush(), node);
    }

    public static NativeBattleCuragaFrame Unlink882960(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        memory.Unlink882960(node);
        return memory.Flush();
    }

    public static NativeBattleCuragaFrame AdvanceEmitterLifetime8817F0(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        AdvanceEmitterLifetime(memory, node);
        return memory.Flush();
    }

    internal static void AdvanceEmitterLifetime(NativeBattleCuragaNodeMemory memory, int node)
    {
        short remaining = unchecked((short)(memory.Word(node + 0x64) - 1));
        memory.Word(node + 0x64, unchecked((ushort)remaining));
        if (remaining <= 0)
        {
            memory.Unlink882960(node);
            memory.Byte(node + 0x69, 0);
            memory.ContextWord(0x14, unchecked((ushort)(memory.ContextWord(0x14) - 1)));
        }
    }

    internal static int? AllocateEmitter(NativeBattleCuragaNodeMemory memory, uint parentPointer, sbyte sourceIndex, sbyte targetIndex)
    {
        int start = memory.State.Auxiliary25F8 ?? throw new NotSupportedException("Native881700 requires87DE10's emitter pool.");
        short index = unchecked((short)(memory.State.Counter26D76C0 ?? throw new NotSupportedException("Native881700 requires the actual26D76C0 scan cursor.")));
        if (index is < 0 or >= 90)
        {
            throw new InvalidDataException("Native881700 emitter scan cursor leaves its original90 physical slots.");
        }
        int? result = null;
        for (int attempt = 0; attempt < 90; attempt++)
        {
            int node = checked(start + index * 108);
            if (memory.Byte(node + 0x69) == 0)
            {
                memory.Clear(node, 108);
                memory.Byte(node + 0x6A, unchecked((byte)sourceIndex));
                memory.Byte(node + 0x69, 1);
                memory.Dword(node + 0x5C, parentPointer);
                memory.Byte(node + 0x6B, unchecked((byte)targetIndex));
                memory.ContextWord(0x14, unchecked((ushort)(memory.ContextWord(0x14) + 1)));
                memory.Link882930(node, 0);
                result = node;
                break;
            }
            index++;
            if (index >= 89)
            {
                index = 0;
            }
        }
        index++;
        memory.State = memory.State with { Counter26D76C0 = index >= 89 ? (ushort)0 : unchecked((ushort)index) };
        return result;
    }

    internal static int? AllocateGeometry(NativeBattleCuragaNodeMemory memory, int emitter, sbyte sourceIndex)
    {
        int start = memory.State.Auxiliary16F80 ?? throw new NotSupportedException("Native882080 requires87DE10's geometry pool.");
        short index = unchecked((short)(memory.State.Counter26D82E8 ?? throw new NotSupportedException("Native882080 requires the actual26D82E8 scan cursor.")));
        if (index is < 0 or >= 140)
        {
            throw new InvalidDataException("Native882080 geometry scan cursor leaves its original140 physical slots.");
        }
        int? result = null;
        for (int attempt = 0; attempt < 140; attempt++)
        {
            int node = checked(start + index * 672);
            if (memory.Byte(node + 0x1D7) == 0)
            {
                memory.Clear(node, 672);
                memory.Dword(node + 0x1BC, memory.Pointer(emitter));
                memory.Byte(node + 0x1D7, 1);
                memory.Byte(node + 0x1D6, unchecked((byte)sourceIndex));
                memory.Byte(node + 0x1D9, memory.Byte(emitter + 0x6B));
                memory.ContextWord(0x16, unchecked((ushort)(memory.ContextWord(0x16) + 1)));
                memory.Link882930(node, 1);
                result = node;
                break;
            }
            index++;
            if (index >= 139)
            {
                index = 0;
            }
        }
        index++;
        memory.State = memory.State with { Counter26D82E8 = index >= 139 ? (ushort)0 : unchecked((ushort)index) };
        return result;
    }
}
