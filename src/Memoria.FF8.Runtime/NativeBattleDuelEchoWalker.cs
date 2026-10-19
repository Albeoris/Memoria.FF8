using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuelEchoWalker
{
    public static NativeBattleModulePoolTask Draw68A990(NativeBattleModulePoolTask context, int offset, uint sectionAddress, Func<int, int, byte[]> readSection, NativeBattleModulePoolTask echo, int echoOffset, int history, NativeBattleCameraProjection projection, Action<int, ImmutableArray<byte>> writeCpu, Func<int, (byte[] Data, bool[] Known)> readCpu, Action<NativeBattleGfGeometryBatch> submit, int tableBaseBucket, byte depthShift, short zsf3, short zsf4, Action<NativeBattleModulePoolTask, int, int>? observeContext = null)
    {
        int count = Int(0);
        if (count > 256)
        {
            throw new InvalidDataException("Native Duel echo object directory leaves its bounded original resource.");
        }
        for (int mesh = 0; mesh < count; mesh++)
        {
            int cursor = Int(checked(4 + mesh * 4));
            if ((context.ReadDword(offset + 24) & (1u << (mesh & 31))) == 0)
            {
                continue;
            }
            int groups = Signed(cursor);
            cursor = checked(cursor + 2);
            int destination = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = Signed(cursor);
                cursor = checked(cursor + 2);
                if ((uint)bone >= 128)
                {
                    throw new NotSupportedException("Native Duel echo requires its actual signed bone selector map.");
                }
                int selected = unchecked((sbyte)echo.ReadByte(echoOffset + 5 + bone));
                int vertices = Signed(cursor);
                if (vertices < 0)
                {
                    throw new NotSupportedException("Native Duel echo vertex group requires its actual bounded count.");
                }
                if (selected < 0)
                {
                    cursor = checked(cursor + 2 + vertices * 6);
                    for (int vertex = 0; vertex < vertices; vertex++)
                    {
                        writeCpu(checked(destination + 6), BitConverter.GetBytes((ushort)16).ToImmutableArray());
                        destination = checked(destination + 8);
                    }
                    continue;
                }
                int matrixAddress = checked(NativeBattleDuelEcho.ReadHistoryOffset(echo, echoOffset, history) + 256 + selected * 32);
                NativeBattleActionMatrix matrix = Matrix(echo, matrixAddress);
                NativeBattleDuelEchoProjectionResult projected = NativeBattleDuelEchoProjection.Project6A2A90(context, offset, Bytes(cursor, checked(2 + vertices * 6)).ToImmutableArray(), matrix, projection, destination, writeCpu, observeContext);
                context = projected.Context;
                cursor = checked(cursor + projected.SourceBytes);
                destination = checked(destination + projected.VertexBytes);
            }
            cursor = checked((cursor + 3) & ~3);
            ushort triangles = Word(cursor);
            ushort quads = Word(checked(cursor + 2));
            cursor = checked(cursor + 12);
            context = context.WriteWord(offset + 8, triangles).WriteWord(offset + 10, quads).WriteDword(offset, checked(sectionAddress + (uint)cursor));
            observeContext?.Invoke(context, offset + 8, 4);
            observeContext?.Invoke(context, offset, 4);
            ImmutableArray<byte> faces = Bytes(cursor, checked(triangles * 16 + quads * 20)).ToImmutableArray();
            NativeBattleDuelEchoPacketsResult packets = NativeBattleDuelEchoPackets.Emit68AAE0(context, offset, faces, vertex =>
            {
                (byte[] raw, bool[] known) = readCpu(checked(vertex * 8));
                if (raw.Length != 8 || known.Length != 8)
                {
                    throw new InvalidDataException("Native Duel echo requires its canonical eight-byte projected vertex row.");
                }
                byte mask = 0;
                for (int current = 0; current < 8; current++)
                {
                    if (known[current])
                    {
                        mask |= (byte)(1 << current);
                    }
                }
                return new(BinaryPrimitives.ReadUInt32LittleEndian(raw), BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(4)), BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(6))) { KnownBytesMask = mask };
            }, tableBaseBucket, depthShift, zsf3, zsf4, observeContext);
            context = packets.Context;
            submit(packets.Batch);
        }
        return context;

        byte[] Bytes(int source, int length)
        {
            if (source < 0 || length < 0)
            {
                throw new InvalidDataException("Native Duel echo read leaves its original source extent.");
            }
            byte[] bytes = readSection(source, length);
            return bytes.Length == length ? bytes : throw new InvalidDataException("Native Duel echo source returned an incomplete physical read.");
        }
        ushort Word(int source) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes(source, 2));
        short Signed(int source) => unchecked((short)Word(source));
        int Int(int source) => BinaryPrimitives.ReadInt32LittleEndian(Bytes(source, 4));
    }

    public static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, int address)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(address + index * 2))).ToImmutableArray();
        return new(rotation, unchecked((int)task.ReadDword(address + 20)), unchecked((int)task.ReadDword(address + 24)), unchecked((int)task.ReadDword(address + 28)), 4096);
    }
}
