using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal void AppendChildren(int node, ReadOnlySpan<byte> source)
    {
        for (int index = 0; index < 4; index++)
        {
            if (source[0x28 + index] == 1)
            {
                inputs.AllocateEmitter(memory.Pointer(node), unchecked((sbyte)source[0x2C + index]), unchecked((sbyte)memory.Byte(node + 0x1D9)));
            }
        }
    }

    internal bool CompleteGeometry(int node, ReadOnlySpan<byte> source)
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
            inputs.AllocateEmitter(memory.Pointer(node), unchecked((sbyte)source[0x31]), unchecked((sbyte)memory.Byte(node + 0x1D9)));
        }
        return complete;
    }
}
