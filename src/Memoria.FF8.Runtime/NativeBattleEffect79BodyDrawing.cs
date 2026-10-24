using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79BodyDrawingStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary168, NativeBattleEffectArena Rows, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect79BodyDrawing
{
    public static NativeBattleEffect79BodyDrawingStep Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena context, NativeBattleEffectArena temporary168, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, uint color, uint mask, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleEffectArena rows, int rowLength, NativeBattleSummonColors constants, byte depthShift = 4)
    {
        if (depthShift > 29 || rowLength < 0 || rowLength % 16 != 0)
        {
            throw new InvalidDataException("Effect79 requires its actual strided body workspace and depth shift.");
        }
        temporary168 = Copy(context, 0, temporary168, 0, 32);
        temporary168 = Copy(context, 0x70, temporary168, 0x5C, 8);
        temporary168 = Word(temporary168, 0x9E, Signed(context, 0xA0));
        temporary168 = Word(temporary168, 0x9C, 4096 - Signed(context, 0xA0));
        temporary168 = Copy(context, 0xA2, temporary168, 0xA0, 6);
        temporary168 = Word(temporary168, 0xA6, Signed(context, 0xD0));
        temporary168 = Dword(temporary168, 0x90, (color & 0xFFFFFF) | 0x24000000);
        temporary168 = Dword(temporary168, 0x94, (color & 0xFFFFFF) | 0x2C000000);
        temporary168 = Dword(temporary168, 0x98, mask);
        NativeBattleActionMatrix rotation = NativeBattleWendigoBallGeometry.Rotation(Matrix(temporary168, 0).ReadMatrix(), Vector(context, 0x98));
        for (int coefficient = 0; coefficient < 9; coefficient++)
        {
            temporary168 = Word(temporary168, coefficient * 2, rotation.Rotation[coefficient]);
        }
        (byte[] colorBytes, bool[] colorKnown) = temporary168.ReadSnapshot(0, 20);
        NativeBattleScriptedLightingMemory lighting = (memory.ScriptedLighting ?? throw new NotSupportedException("Effect79 requires its current lighting owner.")) with { ColorMatrix = new(colorBytes.ToImmutableArray(), colorKnown.ToImmutableArray()) };
        NativeBattlePackedMatrix32 view = Matrix(context, 0x40);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? throw new NotSupportedException("Effect79 requires geometry registers."), view.ReadMatrix(), true, true, Padding(view)), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect79 requires arithmetic registers."));
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        int objects = Int(geometryAddress);
        for (int item = 0; item < objects; item++)
        {
            if ((mask & (1u << (item & 31))) == 0)
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
                    throw new InvalidDataException("Effect79 body requires its current bounded bone groups.");
                }
                NativeBattlePackedMatrix32 packed = bones[bone];
                NativeBattleActionMatrix currentBone = packed.ReadMatrix();
                lighting = lighting with { LightMatrix = new(packed.Bytes.Take(20).ToImmutableArray(), packed.Known.Take(20).ToImmutableArray()), BackColor = new(currentBone.X, currentBone.Y, currentBone.Z) };
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int destination = checked(vertices * 16);
                    if (destination > rowLength - 16)
                    {
                        throw new InvalidDataException("Effect79 body vertices leave their supplied current row workspace.");
                    }
                    temporary168 = temporary168.Write(0x4C, readGeometry(cursor, 6).Read(0, 6));
                    registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporary168, 0x4C), NullableWord(temporary168, 0x52));
                    registers = NativeBattleRaldo19BallRendering.TransformLighting(registers, lighting, true);
                    temporary168 = StoreVector(temporary168, 0x4C, Ir(registers));
                    NativeBattleVector posed = Vector(temporary168, 0x4C);
                    NativeBattleVector pivot = Vector(temporary168, 0x5C);
                    NativeBattleVector relative = new(unchecked((short)(posed.X - pivot.X)), unchecked((short)(posed.Y - pivot.Y)), unchecked((short)(posed.Z - pivot.Z)));
                    temporary168 = StoreVector(temporary168, 0x4C, relative);
                    temporary168 = Copy(temporary168, 0x4C, temporary168, 0x54, 8);
                    var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(relative.X, relative.Y, relative.Z);
                    NativeBattleVector sphere = new(Scale(Signed(temporary168, 0xA0), normalized.X), Scale(Signed(temporary168, 0xA2), normalized.Y), Scale(Signed(temporary168, 0xA4), normalized.Z));
                    temporary168 = StoreVector(temporary168, 0x4C, sphere);
                    registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)Signed(temporary168, 0x9C)) } }, unchecked((ushort)relative.X), unchecked((ushort)relative.Y), unchecked((ushort)relative.Z));
                    registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
                    registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)Signed(temporary168, 0x9E)) } }, unchecked((ushort)sphere.X), unchecked((ushort)sphere.Y), unchecked((ushort)sphere.Z));
                    registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, constants);
                    temporary168 = StoreVector(temporary168, 0x4C, Ir(registers));
                    registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporary168, 0x4C), NullableWord(temporary168, 0x52));
                    registers = NativeBattleRaldo19BallRendering.TransformLighting(registers, lighting, false);
                    NativeBattleVector turned = Ir(registers);
                    temporary168 = StoreVector(temporary168, 0x4C, new(unchecked((short)(turned.X + pivot.X)), unchecked((short)(turned.Y + pivot.Y)), unchecked((short)(turned.Z + pivot.Z))));
                    rows = Copy(temporary168, 0x4C, rows, destination, 8);
                    cursor += 6;
                    vertices++;
                }
            }
            for (int vertex = 0; vertex < vertices; vertex++)
            {
                temporary168 = Copy(rows, vertex * 16, temporary168, 0x34, 8);
                registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporary168, 0x34), NullableWord(temporary168, 0x3A));
                registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                rows = Dword(rows, vertex * 16 + 8, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect79 requires current SXY2."));
                rows = Dword(rows, vertex * 16 + 12, registers.Geometry.Sz[3] ?? throw new NotSupportedException("Effect79 requires current SZ3."));
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
                    int[] indices = new int[quad ? 4 : 3];
                    uint[] screens = new uint[indices.Length];
                    int[] depths = new int[indices.Length];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        ReadCorner(corner);
                        temporary168 = Dword(Dword(temporary168, 0x80 + corner * 4, (uint)indices[corner]), 0x24 + corner * 4, screens[corner]);
                    }
                    registers = registers with { Geometry = registers.Geometry with { Sxy = [screens[0], screens[1], screens[2]] } };
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect79 requires current NCLIP.");
                    temporary168 = Dword(temporary168, 0x7C, area);
                    if (unchecked((int)area) > 0)
                    {
                        if (quad)
                        {
                            ReadCorner(3);
                            temporary168 = Dword(Dword(temporary168, 0x8C, (uint)indices[3]), 0x30, screens[3]);
                        }
                        int sum = unchecked(depths[0] + depths[1] + depths[2]);
                        int depth = quad ? unchecked(sum + depths[3]) >> (depthShift + 2) : (sum / 3) >> depthShift;
                        depth = Math.Max(0, unchecked(depth + Signed(temporary168, 0xA6)));
                        temporary168 = Dword(temporary168, 0x78, unchecked((uint)depth));
                        SkinWrites packet = new(quad ? 40 : 32, allocation);
                        packet.Word(0, quad ? 0x09000000u : 0x07000000u);
                        packet.Word(4, Unsigned(temporary168, quad ? 0x94 : 0x90) | ((source[15] & 2) != 0 ? 0x02000000u : 0));
                        for (int corner = 0; corner < indices.Length; corner++)
                        {
                            packet.Word(8 + corner * 8, screens[corner]);
                        }
                        packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
                        packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12)));
                        packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(quad ? 16 : 6)));
                        if (quad)
                        {
                            packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(18)));
                        }
                        ImmutableArray<int> sidecars = quad ? depths.ToImmutableArray() : [depths[0], depths[1], depths[2], 0];
                        attempts.Add(packet.Attempt(new(checked(17 + depth), packet.Words(), sidecars)));
                        allocation += quad ? 40 : 32;
                    }
                    cursor += quad ? 20u : 16u;

                    void ReadCorner(int corner)
                    {
                        int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        if (index >= vertices)
                        {
                            throw new InvalidDataException("Effect79 face leaves its current projected object.");
                        }
                        indices[corner] = index;
                        screens[corner] = Unsigned(rows, index * 16 + 8);
                        depths[corner] = unchecked((int)Unsigned(rows, index * 16 + 12));
                    }
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, ScriptedLighting = lighting }, temporary168, rows, new(attempts.ToImmutable(), allocation));

        int Int(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short Short(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }

    private static short Scale(short radius, int normalized) => unchecked((short)(unchecked(radius * (short)normalized) / 4096));
    private static NativeBattleVector Ir(NativeBattleGeometryRegisterStep registers) => new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
    private static NativeBattleVector Vector(NativeBattleEffectArena memory, int offset) => new(Signed(memory, offset), Signed(memory, offset + 2), Signed(memory, offset + 4));
    private static short Signed(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(offset, 2));
    private static uint Unsigned(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena memory, int offset, int value) => memory.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena memory, int offset, uint value) => memory.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int offset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, sourceOffset, destination, offset, length);
    private static NativeBattleEffectArena StoreVector(NativeBattleEffectArena memory, int offset, NativeBattleVector vector) => Word(Word(Word(memory, offset, vector.X), offset + 2, vector.Y), offset + 4, vector.Z);
    private static NativeBattlePackedMatrix32 Matrix(NativeBattleEffectArena memory, int offset)
    {
        (byte[] bytes, bool[] known) = memory.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
    private static short? NullableWord(NativeBattleEffectArena memory, int offset)
    {
        (byte[] bytes, bool[] known) = memory.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
