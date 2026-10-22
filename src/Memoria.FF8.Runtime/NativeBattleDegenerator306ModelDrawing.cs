using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDegenerator306ModelStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary136, NativeBattleEffectArena VertexScratch, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleDegenerator306ModelDrawing
{
    public static NativeBattleDegenerator306ModelStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary136, NativeBattleEffectArena vertexScratch, Func<int, int, byte[]> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, uint scratchAddress, int scratchLength, int tableBaseBucket = 17, byte depthShift = 2)
    {
        if (scratchLength < 0 || depthShift > 31 || U32(temporary136, 4) != scratchAddress)
        {
            throw new InvalidDataException("Degenerator body requires its actual bounded native projection workspace.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Degenerator body requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Degenerator body requires its current arithmetic registers."));
        int objects = I32(0);
        if (objects < 0)
        {
            objects = 0;
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < objects; index++)
        {
            int cursor = I32(checked(4 + index * 4));
            if ((U32(temporary136, 0x20) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            int groups = ReadS16(cursor);
            cursor = checked(cursor + 2);
            int count = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = ReadS16(cursor);
                if (bone < 0 || bone >= bones.Length)
                {
                    throw new InvalidDataException("Degenerator selected bone leaves its current physical matrix bank.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, Padding(matrix)) };
                int vertices = ReadS16(checked(cursor + 2));
                cursor = checked(cursor + 4);
                for (int vertex = 0; vertex < vertices; vertex++)
                {
                    int output = checked(count * 8);
                    if (output > scratchLength - 8)
                    {
                        throw new InvalidDataException("Degenerator deformation leaves its current shared vertex workspace.");
                    }
                    byte[] xyz = Read(cursor, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    byte[] packed = new byte[8];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        short transformed = unchecked((short)registers.Colors.Ir[axis]!.Value);
                        short target = S16(temporary136, 0x60 + axis * 2);
                        int blend = unchecked((int)U32(temporary136, 0x2C));
                        short value = unchecked((short)(transformed + (unchecked((target - transformed) * blend) >> 12)));
                        BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(axis * 2), value);
                    }
                    vertexScratch = vertexScratch.Write(output, packed);
                    count++;
                    cursor = checked(cursor + 6);
                }
            }
            cursor = checked((cursor + 3) & ~3);
            temporary136 = temporary136.Write(8, Read(cursor, 8));
            cursor = checked(cursor + 12);
            temporary136 = Dword(temporary136, 0, checked(geometryAddress + (uint)cursor));
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), true, true, Padding(camera)) };
            DrawFaces(false);
            DrawFaces(true);

            void DrawFaces(bool quad)
            {
                int faces = U16(temporary136, quad ? 10 : 8);
                for (int face = 0; face < faces; face++)
                {
                    byte[] source = Read(cursor, quad ? 20 : 16);
                    var drawn = DrawFace(source, quad, vertexScratch, count, temporary136, registers, projection, controls, tableBaseBucket, depthShift, allocated);
                    temporary136 = drawn.Temporary;
                    registers = drawn.Registers;
                    NativeBattleGfGeometryAttempt attempt = drawn.Attempt;
                    attempts.Add(attempt);
                    if (attempt.Primitive is not null)
                    {
                        allocated = checked(allocated + attempt.PacketLength);
                    }
                    cursor = checked(cursor + (quad ? 20 : 16));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary136, vertexScratch, new(attempts.ToImmutable(), allocated));

        byte[] Read(int offset, int length)
        {
            byte[] result = readGeometry(offset, length);
            return result is not null && result.Length == length ? result : throw new InvalidDataException("Degenerator requires its exact current addressed geometry bytes.");
        }
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        short ReadS16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Read(offset, 2));
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short S16(NativeBattleEffectArena arena, int offset) => unchecked((short)U16(arena, offset));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
}
