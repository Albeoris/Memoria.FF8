using Memory = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicShapeAnchor8
{
    public static NativeBattleWaterExecutionFrame Update(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, short, uint> resolveNode)
    {
        Memory memory = new(frame);
        uint current = memory.Slot;
        uint reference = resolveNode(frame, memory.Signed(memory.Dword(current + 0xA0)));
        uint work = memory.Dword(0x2797624);
        int factor = unchecked((int)memory.Dword(current + 0x64)) >> 16;
        memory.Dword(work + 0x58, unchecked((uint)factor));
        uint shape = memory.Dword(reference + 0xD8);
        uint vertices = unchecked(shape + memory.Dword(shape + 0x14));
        memory.Dword(work + 0x74, vertices);
        int count = unchecked((int)memory.Dword(shape + 0x18));
        memory.Dword(work + 0x48, unchecked((uint)count));
        int maximumZ = 0;
        int minimumZ = short.MaxValue;
        int maximumIndex = 0;
        int minimumIndex = 0;
        for (int index = 0; index < count; index++)
        {
            int z = memory.Signed(unchecked(vertices + (uint)(index * 8) + 4));
            if (z > maximumZ)
            {
                maximumZ = z;
                maximumIndex = index;
            }
            if (z < minimumZ)
            {
                minimumZ = z;
                minimumIndex = index;
            }
        }
        memory.Dword(work + 0x44, unchecked((uint)maximumZ));
        memory.Dword(work + 0x50, unchecked((uint)maximumIndex));
        memory.Dword(work + 0x54, unchecked((uint)minimumIndex));
        int selected;
        if (factor <= 0)
        {
            selected = minimumIndex;
        }
        else if (factor >= 0x1000)
        {
            selected = maximumIndex;
        }
        else
        {
            int targetZ = unchecked(maximumZ * factor) / 0x1000;
            int distance = short.MaxValue;
            selected = 0;
            for (int index = 0; index < count; index++)
            {
                int difference = unchecked(targetZ - memory.Signed(unchecked(vertices + (uint)(index * 8) + 4)));
                if (difference < 0)
                {
                    difference = unchecked(-difference);
                }
                if (difference < distance)
                {
                    distance = difference;
                    selected = index;
                }
            }
        }
        uint vertex = unchecked(vertices + (uint)(selected * 8));
        short[] position = [memory.Signed(vertex), memory.Signed(vertex + 2), memory.Signed(vertex + 4)];
        for (uint axis = 0; axis < 3; axis++)
        {
            int product = unchecked(memory.Signed(reference + 0x8C + axis * 2) * position[axis]);
            memory.Word(current + 0x94 + axis * 2, product / 0x100);
        }
        return memory.Frame;
    }
}
