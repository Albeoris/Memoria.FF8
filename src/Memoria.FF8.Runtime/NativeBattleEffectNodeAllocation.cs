namespace Memoria.FF8.Runtime;

/// <summary>The normalized881700/891610/886BC0 and882080/891F90/887540 scans, with each native physical capacity supplied explicitly.</summary>
internal static class NativeBattleEffectNodeAllocation
{
    internal static int? AllocateEmitter(NativeBattleEffectNodeMemory memory, int start, int physicalSlots, ref ushort scanCursor, uint parentPointer, sbyte sourceIndex, sbyte targetIndex)
    {
        int index = unchecked((short)scanCursor);
        if (index < 0 || index >= physicalSlots)
        {
            throw new InvalidDataException("Native881700 emitter scan cursor leaves its original physical emitter slots.");
        }
        int? result = null;
        for (int attempt = 0; attempt < physicalSlots; attempt++)
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
            if (index >= physicalSlots - 1)
            {
                index = 0;
            }
        }
        index++;
        scanCursor = index >= physicalSlots - 1 ? (ushort)0 : unchecked((ushort)index);
        return result;
    }

    internal static int? AllocateGeometry(NativeBattleEffectNodeMemory memory, int start, int physicalSlots, ref ushort scanCursor, int emitter, sbyte sourceIndex)
    {
        int index = unchecked((short)scanCursor);
        if (index < 0 || index >= physicalSlots)
        {
            throw new InvalidDataException("Native882080 geometry scan cursor leaves its original physical geometry slots.");
        }
        int? result = null;
        for (int attempt = 0; attempt < physicalSlots; attempt++)
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
            if (index >= physicalSlots - 1)
            {
                index = 0;
            }
        }
        index++;
        scanCursor = index >= physicalSlots - 1 ? (ushort)0 : unchecked((ushort)index);
        return result;
    }

    internal static void WalkList(Func<NativeBattleEffectNodeMemory> readMemory, Action<int, short> advance)
    {
        NativeBattleEffectNodeMemory memory = readMemory();
        uint pointer = memory.ContextDword(0x2C);
        while (pointer != 0)
        {
            int node = memory.Offset(pointer);
            advance(node, unchecked((short)memory.Word(node + 8)));
            memory = readMemory();
            pointer = memory.Dword(node + 4);
        }
    }
}
