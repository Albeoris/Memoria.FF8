using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRestorationAnimatedAnchor
{
    public static NativeBattleRestorationFrame Advance(NativeBattleRestorationFrame frame, uint contextTask, int node)
    {
        using NativeBattleRestorationNodeMemory memory = new(frame, contextTask);
        NativeBattleRestorationExecutableState bank = frame.Memory.GetRestorationExecutable(memory.Profile.Effect) ?? throw new NotSupportedException("Native8921B0/887760 requires its own retained executable channels.");
        int sourceIndex = unchecked((sbyte)memory.Byte(node + 0x6A));
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(unchecked(memory.ContextDword(0x224) + (uint)(sourceIndex * 4)), 4));
        NativeBattleEffectAnimatedAnchor.Advance(memory, node, bank.Read(pointer, 0x158), bank.Read);
        return memory.Flush();
    }
}
