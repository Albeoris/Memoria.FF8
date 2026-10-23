using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDeformedBodyStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary132, NativeBattleEffectArena VertexScratch, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleDeformedBodyDrawing
{
    public static NativeBattleDeformedBodyStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary132, NativeBattleEffectArena vertexScratch, Func<int, int, byte[]> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, uint scratchAddress, int scratchLength, uint cacheAddress, int cacheLength, int tableBaseBucket = 17, byte depthShift = 0)
    {
        if (scratchLength < 0 || depthShift > 31 || U32(temporary132, 0x24) != scratchAddress)
        {
            throw new InvalidDataException("Acid body requires its actual bounded physical projection workspace.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Acid body requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid body requires its current arithmetic registers."));
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Acid body requires its actual coefficient cache arena.");
        int objects = I32(0);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < objects; index++)
        {
            int cursor = I32(checked(4 + index * 4));
            temporary132 = Dword(temporary132, 0x30, Address(cursor));
            if ((U32(temporary132, 0x4C) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            temporary132 = Dword(temporary132, 0x28, scratchAddress);
            int groups = S16(cursor);
            cursor = checked(cursor + 2);
            temporary132 = Dword(temporary132, 0x30, Address(cursor));
            int vertexCount = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = S16(cursor);
                if (bone < 0 || bone >= bones.Length)
                {
                    throw new InvalidDataException("Acid selected bone leaves its current physical matrix bank.");
                }
                cursor = checked(cursor + 2);
                temporary132 = Dword(temporary132, 0x30, Address(cursor));
                int count = S16(cursor);
                if (count < 0)
                {
                    throw new InvalidDataException("Acid has an invalid native signed vertex count.");
                }
                cursor = checked(cursor + 2);
                NativeBattlePackedMatrix32 matrix = bones[bone];
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, Padding(matrix)) };
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int output = checked((vertexCount + vertex) * 8);
                    RequireScratch(output);
                    byte[] xyz = Read(checked(cursor + vertex * 6), 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(xyz), 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    byte[] transformed = new byte[6];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(transformed.AsSpan(axis * 2), unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Acid requires its transformed IR values."))));
                    }
                    vertexScratch = vertexScratch.Write(output, transformed);
                }
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), true, true, Padding(camera)) };
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int output = checked((vertexCount + vertex) * 8);
                    uint coefficient = U32(temporary132, 0x2C);
                    if (coefficient < cacheAddress || cacheLength < 0 || (ulong)coefficient - cacheAddress >= (ulong)cacheLength)
                    {
                        throw new InvalidDataException("Acid body leaves its bounded physical coefficient cache.");
                    }
                    int coefficientOffset = checked((int)(coefficient - NativeBattleEffectNodeMemory.ArenaAddress));
                    int delta = unchecked(arena.Read(coefficientOffset, 1)[0] * Word(temporary132, 0x80)) >> 10;
                    short y = unchecked((short)(Word(vertexScratch, output + 2) + delta));
                    vertexScratch = vertexScratch.Write(output + 2, BitConverter.GetBytes(y));
                    temporary132 = Dword(temporary132, 0x2C, checked(coefficient + 1));
                    byte[] xyz = vertexScratch.Read(output, 6);
                    (byte[] w, bool[] known) = vertexScratch.ReadSnapshot(output + 6, 2);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(xyz), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Acid requires its reached screen FIFO.");
                    uint depth = registers.Geometry.Sz[3] ?? throw new NotSupportedException("Acid requires its reached depth FIFO.");
                    temporary132 = Dword(temporary132, 0x50, screen);
                    temporary132 = Dword(temporary132, 0x54, unchecked((uint)(unchecked((int)depth) >> 2)));
                    if (U16(temporary132, 0x54) == 0)
                    {
                        vertexScratch = vertexScratch.Write(output + 6, BitConverter.GetBytes((ushort)0x10));
                    }
                    else
                    {
                        short x = unchecked((short)screen);
                        short sy = unchecked((short)(screen >> 16));
                        byte clip = temporary132.Read(0x56, 1)[0];
                        if (x < Word(temporary132, 0x40) * 8)
                        {
                            clip |= 1;
                        }
                        else if (x >= Word(temporary132, 0x44) * 8)
                        {
                            clip |= 2;
                        }
                        if (sy < Word(temporary132, 0x42) * 8)
                        {
                            clip |= 4;
                        }
                        else if (sy >= Word(temporary132, 0x46) * 8)
                        {
                            clip |= 8;
                        }
                        temporary132 = temporary132.Write(0x56, [clip]);
                        vertexScratch = vertexScratch.Write(output, temporary132.Read(0x50, 8));
                    }
                }
                vertexCount = checked(vertexCount + count);
                cursor = checked(cursor + count * 6);
                temporary132 = Dword(temporary132, 0x28, checked(scratchAddress + (uint)(vertexCount * 8)));
                temporary132 = Dword(temporary132, 0x30, Address(cursor));
            }
            cursor = checked((cursor + 3) & ~3);
            temporary132 = temporary132.Write(0x34, Read(cursor, 12));
            cursor = checked(cursor + 12);
            temporary132 = Dword(temporary132, 0x30, Address(cursor));
            temporary132 = Dword(temporary132, 0x20, Address(cursor));
            DrawFaces(false);
            DrawFaces(true);

            void DrawFaces(bool quad)
            {
                int countOffset = quad ? 0x36 : 0x34;
                ushort count = U16(temporary132, countOffset);
                temporary132 = temporary132.Write(countOffset, BitConverter.GetBytes(unchecked((ushort)(count - 1))));
                for (int face = 0; face < count; face++)
                {
                    byte[] source = Read(cursor, quad ? 20 : 16);
                    var drawn = DrawFace(source, quad, vertexScratch, vertexCount, temporary132, registers, controls, tableBaseBucket, depthShift, allocated);
                    temporary132 = drawn.Temporary;
                    registers = drawn.Registers;
                    attempts.Add(drawn.Attempt);
                    if (drawn.Attempt.Primitive is not null)
                    {
                        allocated = checked(allocated + drawn.Attempt.PacketLength);
                    }
                    cursor = checked(cursor + (quad ? 20 : 16));
                    temporary132 = temporary132.Write(countOffset, BitConverter.GetBytes(unchecked((ushort)(count - face - 2))));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary132, vertexScratch, new(attempts.ToImmutable(), allocated));

        byte[] Read(int offset, int length)
        {
            byte[] result = readGeometry(offset, length);
            return result is not null && result.Length == length ? result : throw new InvalidDataException("Acid requires its exact current addressed geometry bytes.");
        }
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        short S16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Read(offset, 2));
        uint Address(int offset) => checked(geometryAddress + (uint)offset);
        void RequireScratch(int output)
        {
            if (output < 0 || output > scratchLength - 8)
            {
                throw new InvalidDataException("Acid deformation leaves its actual shared projection workspace.");
            }
        }
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short Word(NativeBattleEffectArena arena, int offset) => unchecked((short)U16(arena, offset));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleVector Vector(byte[] xyz) => new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
}
