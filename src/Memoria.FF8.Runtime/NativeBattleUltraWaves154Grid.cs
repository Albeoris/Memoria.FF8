using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleUltraWaves154Grid
{
    public static NativeBattleUltraWaves154State Initialize(NativeBattleUltraWaves154State state)
    {
        byte[] grid = state.Grid.ToArray();
        NativeBattleEffectArena quads = state.Quads;
        for (int row = 0; row < 28; row++)
        {
            for (int column = 0; column < 41; column++)
            {
                int offset = (row * 41 + column) * 12;
                Put(grid, offset + 4, -164 + column * 8);
                Put(grid, offset + 6, -112 + row * 8);
                Put(grid, offset + 8, 0);
                Put(grid, offset + 10, (row is 0 or 27 ? 1 : 0) | (column is 0 or 40 ? 2 : 0));
            }
        }
        for (int row = 0; row < 27; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                int record = (row * 40 + column) * 96;
                byte[] pointers = new byte[16];
                int vertex = (row * 41 + column) * 12;
                for (int corner = 0; corner < 4; corner++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(pointers.AsSpan(corner * 4), checked(0x241EF00u + (uint)(vertex + (corner & 1) * 12 + (corner >> 1) * 492)));
                }
                quads = quads.Write(record, pointers);
                for (int buffer = 0; buffer < 2; buffer++)
                {
                    int packet = record + 16 + buffer * 40;
                    quads = Dword(quads, packet, 0x09000000);
                    quads = Dword(quads, packet + 4, 0x2C808080);
                    int u0 = column * 8 % 64;
                    int u1 = u0 + (column == 39 ? 7 : 8);
                    int v0 = row * 8;
                    int v1 = v0 + (row == 26 ? 7 : 8);
                    quads = Word(quads, packet + 12, u0 | v0 << 8);
                    quads = Word(quads, packet + 20, u1 | v0 << 8);
                    quads = Word(quads, packet + 22, column * 8 / 64 + 0x16 | 0x120);
                    quads = Word(quads, packet + 28, u0 | v1 << 8);
                    quads = Word(quads, packet + 36, u1 | v1 << 8);
                }
            }
        }
        return state with { Grid = grid.ToImmutableArray(), Quads = quads };
    }

    public static ImmutableArray<byte> Wave(ImmutableArray<byte> source, int frame)
    {
        byte[] grid = source.ToArray();
        int phase = frame * ((frame >> 1) + 208);
        int sine = NativeBattleCameraMath.Sine((frame << 10) / 35);
        sine = ((sine * 35 >> 12) << 12) / 35;
        int amplitude = NativeBattleCameraMath.Sine(sine) * 48 >> 12;
        for (int row = 1; row < 27; row++)
        {
            for (int column = 1; column < 40; column++)
            {
                int angle = column * 0x1800 / 41;
                int q = NativeBattleCameraMath.Sine((NativeBattleCameraMath.Cosine(angle) * 3 << 9) >> 12);
                int bend = (((q * 7 << 7) >> 12) + row * 0x400) * 3 / 5;
                bend += (NativeBattleCameraMath.Sine(angle) * 11 << 7) >> 12;
                Put(grid, (row * 41 + column) * 12 + 8, NativeBattleCameraMath.Sine(bend + phase) * amplitude >> 12);
            }
        }
        return grid.ToImmutableArray();
    }

    public static (NativeBattleUltraWaves154State State, NativeBattleLogicalOrdering Ordering) Draw(NativeBattleUltraWaves154State state, NativeBattleLogicalOrdering ordering, NativeBattleCameraProjection projection, byte buffer, uint color)
    {
        if (buffer > 1)
        {
            throw new InvalidDataException("Native Ultra Waves requires its actual display buffer.");
        }
        int focal = unchecked((short)(projection.FocalLength ?? throw new NotSupportedException("Native Ultra Waves requires its signed H register.")));
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 4, 4, focal, 4096);
        byte[] grid = state.Grid.ToArray();
        for (int vertex = 0; vertex < 28 * 41; vertex++)
        {
            int offset = vertex * 12;
            NativeBattleVector point = new(Read(grid, offset + 4), Read(grid, offset + 6), Read(grid, offset + 8));
            BinaryPrimitives.WriteUInt32LittleEndian(grid.AsSpan(offset), NativeBattleModuleProjection.Project45FE10(matrix, point, projection).Screen);
        }
        NativeBattleEffectArena quads = state.Quads;
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        for (int row = 0; row < 27; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                int record = (row * 40 + column) * 96;
                int packet = record + 16 + buffer * 40;
                links.InvalidatePacketRange(new("24059F8", 0, packet + 4), 4);
                quads = Dword(quads, packet + 4, color | 0x2C000000);
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = ((row + (corner >> 1)) * 41 + column + (corner & 1)) * 12;
                    links.InvalidatePacketRange(new("24059F8", 0, packet + 8 + corner * 8), 4);
                    quads = quads.Write(packet + 8 + corner * 8, grid.AsSpan(vertex, 4));
                }
                byte[] bytes = quads.Read(packet, 40);
                ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
                bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
                links.Prepend(0x101A, new("24059F8", 0, packet), words, 0, [0, 0, 0, 0]);
                if (linked)
                {
                    quads = quads.Write(packet, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
                }
            }
        }
        return (state with { Grid = grid.ToImmutableArray(), Quads = quads }, links.ToImmutable());
    }

    private static short Read(byte[] source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset));
    private static void Put(byte[] source, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(offset), unchecked((short)value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] data = new byte[2];
        Put(data, 0, value);
        return arena.Write(offset, data);
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        return arena.Write(offset, data);
    }
}
