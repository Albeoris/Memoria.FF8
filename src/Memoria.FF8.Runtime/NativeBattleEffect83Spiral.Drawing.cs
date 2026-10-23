using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect83SpiralDrawingStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary148, NativeBattleEffectArena Transformed, NativeBattleEffectArena Faces, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleEffect83SpiralDrawing
{
    public static NativeBattleEffect83SpiralDrawingStep Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary148, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattlePackedMatrix32 matrix, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleEffectArena transformed, int transformedLength, NativeBattleEffectArena faces)
    {
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect83 requires its current geometry registers.");
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(geometry, matrix.ReadMatrix(), true, true, Padding(matrix)) };
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        int faceOffset = 0;
        temporary148 = Word(temporary148, 0x8C, 0);
        int objects = Int(geometryAddress);
        for (int item = 0; item < objects; item++)
        {
            if ((Dword(temporary148, 0x50) & (1u << (item & 31))) == 0)
            {
                continue;
            }
            uint cursor = unchecked(geometryAddress + (uint)Int(geometryAddress + 4 + (uint)(item * 4)));
            int groups = Short(cursor);
            cursor += 2;
            int vertices = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = Short(cursor);
                int count = Short(cursor + 2);
                cursor += 4;
                if ((uint)bone >= (uint)bones.Length || count < 0)
                {
                    throw new InvalidDataException("Effect83 requires a bounded current target bone group.");
                }
                memory = NativeBattleLightBackTransform.LoadBone(memory, bones[bone]);
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int destination = checked(vertices * 16);
                    if (destination > transformedLength - 8)
                    {
                        throw new InvalidDataException("Effect83 target vertices exceed their supplied transformed workspace.");
                    }
                    temporary148 = temporary148.Write(0x28, readGeometry(cursor, 6).Read(0, 6));
                    var registers = Registers();
                    registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporary148, 0x28), NullableWord(temporary148, 0x2E));
                    memory = NativeBattleLightBackTransform.Transform(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, 0);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int value = memory.ScriptedColorRegisters!.Ir[axis] ?? throw new NotSupportedException("Effect83 target transform requires current IR coordinates.");
                        temporary148 = Word(temporary148, 0x28 + axis * 2, value);
                    }
                    transformed = NativeBattleEffectNodeContextMemory.Copy(temporary148, 0x28, transformed, destination, 8);
                    vertices++;
                    cursor += 6;
                }
            }
            cursor = (cursor + 3) & ~3u;
            int triangles = Short(cursor);
            int quads = Short(cursor + 2);
            cursor += 12;
            DrawFaces(triangles, false);
            DrawFaces(quads, true);

            void DrawFaces(int count, bool quad)
            {
                for (int face = 0; face < count; face++)
                {
                    byte[] source = readGeometry(cursor, quad ? 20 : 16).Read(0, quad ? 20 : 16);
                    int corners = quad ? 4 : 3;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        if (index >= vertices)
                        {
                            throw new InvalidDataException("Effect83 face leaves its transformed target object.");
                        }
                        temporary148 = SetDword(temporary148, 0x38 + corner * 4, (uint)index);
                        temporary148 = NativeBattleEffectNodeContextMemory.Copy(transformed, index * 16, temporary148, 0x54 + corner * 8, 8);
                    }
                    temporary148 = Word(temporary148, 0x8C, ReadWord(temporary148, 0x8C) + 1);
                    faceOffset += 32;
                    NativeBattleEffect83SpiralStep advanced = NativeBattleEffect83Spiral.AdvanceFace(faces, faceOffset, temporary148, quad);
                    faces = advanced.Faces;
                    temporary148 = advanced.Temporary148;
                    var registers = Registers();
                    for (int corner = 0; corner < 3; corner++)
                    {
                        registers = NativeBattleGeometry.LoadVector(registers, corner, Vector(temporary148, 0x54 + corner * 8), NullableWord(temporary148, 0x5A + corner * 8));
                    }
                    registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect83 requires current NCLIP.");
                    temporary148 = SetDword(temporary148, 0x74, area);
                    if (unchecked((int)area) > 0)
                    {
                        uint[] screens = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Effect83 requires current screen coordinates.")).ToArray();
                        if (quad)
                        {
                            registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporary148, 0x6C), NullableWord(temporary148, 0x72));
                            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        }
                        registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect83 requires current ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect83 requires current ZSF3."));
                        uint depth = (registers.Geometry.Otz ?? throw new NotSupportedException("Effect83 requires current OTZ.")) >> 2;
                        temporary148 = SetDword(temporary148, 0x78, depth);
                        SkinWrites packet = new(quad ? 40 : 32, allocation);
                        packet.Word(0, quad ? 0x09000000u : 0x07000000u);
                        packet.Word(4, Dword(temporary148, quad ? 0x4C : 0x48) | (((source[15] & 2) != 0 || ReadWord(faces, faceOffset + 6) >= 2) ? 0x02000000u : 0));
                        for (int corner = 0; corner < 3; corner++)
                        {
                            packet.Word(8 + corner * 8, screens[corner]);
                        }
                        packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
                        packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12)));
                        packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(quad ? 16 : 6)));
                        if (quad)
                        {
                            packet.Word(32, registers.Geometry.Sxy[2]!.Value);
                            packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(18)));
                        }
                        ImmutableArray<int> payload = quad ? registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect83 requires current depth FIFO.")))).ToImmutableArray() : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
                        attempts.Add(packet.Attempt(new(checked(17 + (int)depth), packet.Words(), payload)));
                        allocation += quad ? 40 : 32;
                    }
                    memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
                    cursor += quad ? 20u : 16u;
                }
            }
        }
        return new(memory, temporary148, transformed, faces, new(attempts.ToImmutable(), allocation));

        NativeBattleGeometryRegisterStep Registers() => new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect83 requires geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect83 requires arithmetic registers."));
        int Int(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short Short(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }

    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
    private static short? NullableWord(NativeBattleEffectArena memory, int offset)
    {
        (byte[] data, bool[] known) = memory.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
    }
    private static NativeBattleVector Vector(NativeBattleEffectArena memory, int offset) => new(ReadWord(memory, offset), ReadWord(memory, offset + 2), ReadWord(memory, offset + 4));
    private static short ReadWord(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena memory, int offset, int value) => memory.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena SetDword(NativeBattleEffectArena memory, int offset, uint value) => memory.Write(offset, BitConverter.GetBytes(value));
}
