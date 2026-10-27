using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWavesGridInitialization(NativeBattleEffectArena Grid, NativeBattleEffectArena Quads);
public sealed record NativeBattleUltraWavesGridDraw(NativeBattleEffectMemory Memory, NativeBattleEffectArena Grid, NativeBattleEffectArena Quads, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleUltraWavesGrid
{
    public const int GridLength = 28 * 41 * 12;
    public const int QuadsLength = 27 * 40 * 96;

    public static NativeBattleUltraWavesGridInitialization Initialize(NativeBattleEffectArena grid, NativeBattleEffectArena quads, uint gridAddress)
    {
        for (int row = 0; row < 28; row++)
        {
            for (int column = 0; column < 41; column++)
            {
                int at = (row * 41 + column) * 12;
                grid = Word(grid, at + 4, -164 + column * 8);
                grid = Word(grid, at + 6, -112 + row * 8);
                grid = Word(grid, at + 8, 0);
                grid = Word(grid, at + 10, (row is 0 or 27 ? 1 : 0) | (column is 0 or 40 ? 2 : 0));
            }
        }
        for (int row = 0; row < 27; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                int at = (row * 40 + column) * 96;
                int vertex = (row * 41 + column) * 12;
                for (int corner = 0; corner < 4; corner++)
                {
                    quads = Dword(quads, at + corner * 4, checked(gridAddress + (uint)(vertex + (corner & 1) * 12 + (corner >> 1) * 492)));
                }
                for (int buffer = 0; buffer < 2; buffer++)
                {
                    int packet = at + 16 + buffer * 40;
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
        return new(grid, quads);
    }

    public static NativeBattleEffectArena Wave(NativeBattleEffectArena grid, int age)
    {
        int phase = unchecked(age * ((age >> 1) + 208));
        int sine = NativeBattleCameraMath.Sine(unchecked(age << 10) / 35);
        sine = unchecked((sine * 35 >> 12) << 12) / 35;
        int amplitude = unchecked(NativeBattleCameraMath.Sine(sine) * 48) >> 12;
        for (int row = 0; row < 28; row++)
        {
            for (int column = 0; column < 41; column++)
            {
                int at = (row * 41 + column) * 12;
                if ((grid.Read(at + 10, 1)[0] & 3) != 0)
                {
                    continue;
                }
                int angle = column * 0x1800 / 41;
                int q = NativeBattleCameraMath.Sine(unchecked(NativeBattleCameraMath.Cosine(angle) * 3 << 9) >> 12);
                int bend = unchecked(((unchecked(q * 7 << 7) >> 12) + row * 0x400) * 3) / 5;
                bend = unchecked(bend + (unchecked(NativeBattleCameraMath.Sine(angle) * 11 << 7) >> 12));
                grid = Word(grid, at + 8, unchecked(NativeBattleCameraMath.Sine(unchecked(bend + phase)) * amplitude) >> 12);
            }
        }
        return grid;
    }

    public static NativeBattleUltraWavesGridDraw Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena grid, NativeBattleEffectArena quads, uint gridAddress, uint quadsAddress, NativeBattleLogicalOrdering ordering, NativeBattleCameraProjection projection, short focalShadow, byte buffer, uint color)
    {
        if (buffer > 1)
        {
            throw new InvalidDataException("Ultra Waves grid requires its actual display buffer.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Ultra Waves grid requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Ultra Waves grid requires its current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Ultra Waves grid requires its current projection controls.");
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 4, Y = 4, Z = focalShadow };
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        for (int vertex = 0; vertex < 28 * 41; vertex++)
        {
            int at = vertex * 12;
            byte[] packed = grid.Read(at + 4, 8);
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(4))), BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(6)));
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            grid = Dword(grid, at, registers.Geometry.Sxy[2]!.Value);
        }
        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        string bank = quadsAddress.ToString("X");
        for (int index = 0; index < 27 * 40; index++)
        {
            int record = index * 96;
            int packet = record + 16 + buffer * 40;
            links.InvalidatePacketRange(new(bank, 0, packet + 4), 4);
            quads = Dword(quads, packet + 4, color | 0x2C000000);
            for (int corner = 0; corner < 4; corner++)
            {
                uint pointer = U32(quads, record + corner * 4);
                if (pointer < gridAddress || (ulong)pointer - gridAddress > GridLength - 12 || (pointer - gridAddress) % 12 != 0)
                {
                    throw new InvalidDataException("Ultra Waves quad leaves its actual retained grid pointer owner.");
                }
                int vertex = checked((int)(pointer - gridAddress));
                links.InvalidatePacketRange(new(bank, 0, packet + 8 + corner * 8), 4);
                quads = quads.Write(packet + 8 + corner * 8, grid.Read(vertex, 4));
            }
            byte[] bytes = quads.Read(packet, 40);
            ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(0x101A, new(bank, 0, packet), words, 0, [0, 0, 0, 0]);
            if (linked)
            {
                quads = quads.Write(packet, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, grid, quads, links.ToImmutable());
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
}
