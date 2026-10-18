using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22OverlayStep(NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets, uint Result);

/// <summary>The single-screen overlay program and its ordered private F4/E1 stores.</summary>
public static class NativeBattleBio22Overlay
{
    public static NativeBattleBio22OverlayStep Advance89C650(NativeBattleModulePoolTask task, NativeBattleEffectHeapAllocation program, Func<int, ImmutableArray<int>> readRetainedProxyDepth)
    {
        if (task.Callback != 0x89C650 || task.Payload.Length != 0x3C - 12 || task.ReadDword(0x30) != 0x162693C || program.Identity != "FF8_EN:162693C" || program.Data.IsDefault || program.Data.Length != 0x10)
        {
            throw new InvalidDataException("Native overlay requires its actual3C-byte task and bounded four-DWORD program.");
        }
        int index = unchecked((short)task.ReadWord(0x38));
        uint value = ReadProgram(index);
        NativeBattleGfGeometryBatch packets = new([], 0);
        if ((byte)(value >> 24) == 0xFF)
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
        }
        else
        {
            if ((byte)(value >> 24) == 0xFE)
            {
                if (unchecked((short)task.ReadWord(0x24)) >= (byte)(value >> 16))
                {
                    task = task.WriteWord(0x38, unchecked((ushort)(index + 1))).WriteDword(0x34, ReadProgram(index + 1));
                }
            }
            else
            {
                task = task.WriteDword(0x34, value).WriteWord(0x38, unchecked((ushort)(index + 1)));
            }
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            Add(0, [0x05000000, 0x2A000000 | (task.ReadDword(0x34) & 0xFFFFFF), 0, 0x00000140, 0x00D80000, 0x00D80140], false);
            Add(24, [0x02000000, 0xE100002A, 0], true);
            packets = new(attempts.ToImmutable(), 36);

            void Add(int offset, ImmutableArray<uint> words, bool retainLink)
            {
                byte[] data = new byte[words.Length * 4];
                for (int word = 0; word < words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(word * 4), words[word]);
                }
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, data.Length).Select(position => retainLink && position < 3 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known).ToImmutableArray();
                ImmutableArray<int> depths = readRetainedProxyDepth(attempts.Count);
                if (depths.IsDefault || depths.Length != 0 && depths.Length != 4)
                {
                    throw new InvalidDataException("Native overlay requires actual retained proxy depth metadata or an explicit empty unknown value.");
                }
                NativeBattleGeometryPrimitive primitive = new(7, words, depths) { PhysicalWriteMask = mask };
                attempts.Add(new(offset, data.ToImmutableArray(), mask, primitive));
            }
        }
        byte flags = task.ReadByte(0x26);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, packets, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);

        uint ReadProgram(int position)
        {
            if ((uint)position >= 4)
            {
                throw new InvalidDataException("Native overlay signed index leaves its supplied program.");
            }
            return BinaryPrimitives.ReadUInt32LittleEndian(program.Data.AsSpan(position * 4, 4));
        }
    }
}
