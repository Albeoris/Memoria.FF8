using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88OverlayStep(NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets, bool ReadyToComplete);

public static class NativeBattleRinoa88Overlay
{
    public static NativeBattleRinoa88OverlayStep Advance773090(NativeBattleModulePoolTask task, int pause, ImmutableArray<uint> commands, Func<int, ImmutableArray<int>> readRetainedProxyDepth)
    {
        if (task.Callback != 0x773090 || task.ReadDword(52) != 0x1557CAC || commands.IsDefaultOrEmpty || commands.Length != 27 || commands[^1] != 0xFF000000)
        {
            throw new InvalidDataException("Native773090 requires its actual64-byte task and complete original screen-color program.");
        }
        NativeBattleGfGeometryBatch packets = new([], 0);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                if (pause == 0)
                {
                    task = Add(task, 56, -1);
                    if (Signed(56) <= 0)
                    {
                        task = task.WriteWord(56, 0).WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
                    }
                }
                break;
            case 1:
                uint command = ReadCommand(Signed(58));
                byte opcode = (byte)(command >> 24);
                if (opcode == 255)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1));
                }
                else
                {
                    if (opcode == 254)
                    {
                        if (Signed(60) >= (byte)(command >> 16) && pause == 0)
                        {
                            task = Add(task, 58, 1).WriteDword(48, ReadCommand(Signed(58) + 1));
                        }
                    }
                    else
                    {
                        task = task.WriteDword(48, command);
                        if (pause == 0)
                        {
                            task = Add(task, 58, 1);
                        }
                    }
                    packets = Draw(task.ReadDword(48), readRetainedProxyDepth);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native773090 signed phase exceeds its three callbacks.");
        }
        if (pause == 0)
        {
            task = Add(task, 60, 1);
        }
        task = Add(task, 36, 1);
        return new(task, packets, (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        uint ReadCommand(int index) => (uint)index < commands.Length ? commands[index] : throw new InvalidDataException("Native773130 screen-color index leaves its actual source.");
    }

    private static NativeBattleGfGeometryBatch Draw(uint color, Func<int, ImmutableArray<int>> readRetainedProxyDepth)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        Add(0, [0x02000000, 0xE100004A, 0], true);
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            int x = (quadrant & 1) * 160;
            int y = (quadrant >> 1) * 120;
            Add(12 + quadrant * 24, [0x05000000, 0x2A000000 | (color & 0xFFFFFF), Screen(x, y), Screen(x + 160, y), Screen(x, y + 120), Screen(x + 160, y + 120)], false);
        }
        Add(108, [0x02000000, 0xE100004A, 0], true);
        return new(attempts.ToImmutable(), 120);

        void Add(int offset, ImmutableArray<uint> words, bool retainLink)
        {
            byte[] data = new byte[words.Length * 4];
            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, data.Length).Select(index => retainLink && index < 3 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(7, words, readRetainedProxyDepth(attempts.Count)) { PhysicalWriteMask = mask };
            attempts.Add(new(offset, data.ToImmutableArray(), mask, primitive));
        }
    }

    private static uint Screen(int x, int y) => unchecked((uint)(ushort)x | (uint)(ushort)y << 16);
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(unchecked((short)task.ReadWord(offset)) + value)));
}
