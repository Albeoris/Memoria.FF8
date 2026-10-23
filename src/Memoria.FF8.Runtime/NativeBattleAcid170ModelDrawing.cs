using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid170ModelStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary172, NativeBattleEffectArena VertexScratch, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleAcid170ModelDrawing
{
    public static NativeBattleAcid170ModelStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary172, NativeBattleEffectArena vertexScratch, Func<int, int, byte[]> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, uint scratchAddress, int scratchLength, int tableBaseBucket = 17, byte depthShift = 2)
    {
        if (scratchLength < 0 || depthShift > 31 || U32(temporary172, 4) != scratchAddress)
        {
            throw new InvalidDataException("Acid body requires its actual bounded native projection workspace.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Acid body requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid body requires its current arithmetic registers."));
        memory = LoadPlaneControls(memory, temporary172);
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
            if ((U32(temporary172, 0x20) & (1u << (index & 31))) == 0)
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
                    throw new InvalidDataException("Acid body selected bone leaves its current physical matrix bank.");
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
                        throw new InvalidDataException("Acid body deformation leaves its current shared vertex workspace.");
                    }
                    byte[] xyz = Read(cursor, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    byte[] packed = new byte[8];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        short value = unchecked((short)registers.Colors.Ir[axis]!.Value);
                        BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(axis * 2), value);
                    }
                    vertexScratch = vertexScratch.Write(output, packed);
                    memory = NativeBattleLightBackTransform.TransformIr(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors });
                    registers = new(memory.GeometryRegisters!, memory.ScriptedColorRegisters!);
                    int plane = unchecked((registers.Colors.Mac[0] ?? throw new NotSupportedException("Acid requires its current LIGHT/IR MAC1.")) << 12);
                    temporary172 = Dword(temporary172, 0x2C, unchecked((uint)plane));
                    (temporary172, vertexScratch) = DeformVertex(temporary172, vertexScratch, output, plane);
                    count++;
                    cursor = checked(cursor + 6);
                }
            }
            cursor = checked((cursor + 3) & ~3);
            temporary172 = temporary172.Write(8, Read(cursor, 8));
            cursor = checked(cursor + 12);
            temporary172 = Dword(temporary172, 0, checked(geometryAddress + (uint)cursor));
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), true, true, Padding(camera)) };
            DrawFaces(false);
            DrawFaces(true);

            void DrawFaces(bool quad)
            {
                int faces = U16(temporary172, quad ? 10 : 8);
                for (int face = 0; face < faces; face++)
                {
                    byte[] source = Read(cursor, quad ? 20 : 16);
                    var drawn = NativeBattleSkinnedBodyPackets.EmitBaseFace(source, quad, vertexScratch, count, temporary172, registers, projection, controls, tableBaseBucket, depthShift, allocated, 0x50, 0x44, 0x4C);
                    temporary172 = drawn.Temporary;
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
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary172, vertexScratch, new(attempts.ToImmutable(), allocated));

        byte[] Read(int offset, int length)
        {
            byte[] result = readGeometry(offset, length);
            return result is not null && result.Length == length ? result : throw new InvalidDataException("Acid body requires its exact current addressed geometry bytes.");
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
