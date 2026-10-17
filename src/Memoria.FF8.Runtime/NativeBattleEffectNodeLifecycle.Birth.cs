using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal void Birth()
    {
        uint directory = memory.ContextDword(0x224);
        for (int index = 0; index < 16; index++)
        {
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(unchecked(directory + (uint)(index * 4)), 4));
            if (source == 0 || (memory.ContextWord(0x22) & (1 << index)) != 0)
            {
                continue;
            }
            byte[] record = bank.Read(source, 0x38);
            if (record[0x11] is not (0 or 1 or 4) || unchecked((short)(sbyte)record[0x13]) != unchecked((short)memory.ContextWord(0x18)))
            {
                continue;
            }
            if (record[0x37] == 1)
            {
                for (int target = 0; target < unchecked((short)memory.ContextWord(0x1C)); target++)
                {
                    inputs.AllocateEmitter(0, unchecked((sbyte)index), unchecked((sbyte)target));
                }
            }
            else
            {
                inputs.AllocateEmitter(0, unchecked((sbyte)index), 0);
            }
        }
    }

    internal static void AdvanceEmitterLifetime(NativeBattleEffectNodeMemory memory, int node)
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
}
