using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleConfuse36Auxiliary
{
    public static NativeBattleConfuse36LeafStep Advance(NativeBattleConfuse36Frame frame, uint address, NativeBattleConfuse36Resources resources, Func<NativeBattleConfuse36Frame, NativeBattleModulePacketBatch, NativeBattleConfuse36Frame> append, Action<short> shake)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback == 0x85F930)
        {
            if (task.ReadDword(0x30) != 0x15D97EC)
            {
                throw new NotSupportedException("Native Confuse overlay requires its actual script pointer.");
            }
            short index = unchecked((short)task.ReadWord(0x38));
            uint entry = resources.Overlay[index];
            byte marker = unchecked((byte)(entry >> 24));
            if (marker == 0xFF)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            }
            else
            {
                if (marker == 0xFE)
                {
                    if (unchecked((short)task.ReadWord(0x24)) >= (short)((entry >> 16) & 0xFF))
                    {
                        task = task.WriteWord(0x38, unchecked((ushort)(index + 1))).WriteDword(0x34, resources.Overlay[index + 1]);
                    }
                }
                else
                {
                    task = task.WriteDword(0x34, entry).WriteWord(0x38, unchecked((ushort)(index + 1)));
                }
                NativeBattleGeometryPrimitive quad = new(7, [0x05000000u, (task.ReadDword(0x34) & 0xFFFFFF) | 0x2A000000, 0u, 0x140u, 0xD80000u, 0xD80140u], [0, 0, 0, 0]);
                NativeBattleGeometryPrimitive mode = new(7, [0x02000000u, 0xE100002Au, 0u], [0, 0, 0, 0]);
                frame = append(frame, new([new(quad, 0, 0), new(mode, 0x18, 0)], 0x24));
            }
        }
        else if (task.Callback == 0x85FA30)
        {
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    short value = resources.Shake[unchecked((short)task.ReadWord(0x24))];
                    if (value == 0x7F)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 1);
                    }
                    else
                    {
                        shake(value);
                    }
                    break;
                case 1:
                    break;
                default:
                    throw new InvalidDataException("Native Confuse shake phase leaves its original two callbacks.");
            }
        }
        else
        {
            throw new NotSupportedException("Native Confuse auxiliary task requires its selected original callback.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        return new(frame with { Effect = frame.Effect with { Pools = (complete ? hierarchy.Complete8DC530(address) : hierarchy).Pools } }, complete ? 2 : 0);
    }
}
