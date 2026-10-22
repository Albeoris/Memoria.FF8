using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRaldo19BallParameters(short Actor, NativeBattleVector Pivot, short Weight, NativeBattleVector Radii, NativeBattleVector Angles);
public sealed record NativeBattleRaldo19BallPreparation(NativeBattleEffectArena BallC4, NativeBattleGeometryRegisterStep Registers);
public sealed record NativeBattleRaldo19BallDraw(NativeBattleEffectArena TemporaryA8, NativeBattleEffectArena VertexScratch, NativeBattleGeometryRegisterStep Registers, NativeBattleScriptedLightingMemory Lighting, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleRaldo19BallRendering
{
    public const uint BallAddress = 0x2726910;
    public const uint DescriptorAddress = 0x27303E8;
    public const uint VertexAddress = 0x272E0A0;
    public const int TemporaryLength = 0xA8;
    public const int VertexCapacity = 0x1F40;

    public static NativeBattleRaldo19BallParameters Read(NativeBattleEffectArena ball)
    {
        ValidateBound(ball, 0xC4);
        short actor = Short(ball, 0xAC);
        if (actor is < 0 or >= 11 || Dword(ball, 0x64) != DescriptorAddress || Dword(ball, 0x68) != 0x1D972C0u + (uint)actor * 0x9C || Dword(ball, 0x6C) != 0x1D8E054)
        {
            throw new InvalidDataException("Raldo ball rendering requires its actual selected actor, descriptor and common cursor owner.");
        }
        return new(actor, Vector(ball, 0x74), Short(ball, 0xA4), Vector(ball, 0xA6), Vector(ball, 0x9C));
    }

    public static NativeBattleRaldo19BallPreparation Prepare(NativeBattleEffectArena ball, NativeBattleActionMatrix world, short? worldPadding18, NativeBattleActionMatrix view, short? viewPadding18, NativeBattleGeometryRegisterStep registers)
    {
        _ = Read(ball);
        world.Validate();
        view.Validate();
        ball = Matrix(ball, 0x20, world, worldPadding18, true);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, true, false, viewPadding18) };
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)world.Rotation[column]), unchecked((ushort)world.Rotation[column + 3]), unchecked((ushort)world.Rotation[column + 6]));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int row = 0; row < 3; row++)
            {
                ball = Word(ball, 0x40 + row * 6 + column * 2, unchecked((ushort)registers.Colors.Ir[row]!.Value));
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, false, true, null) };
        NativeBattleVector translation = new(unchecked((short)world.X), unchecked((short)world.Y), unchecked((short)world.Z));
        registers = NativeBattleGeometry.LoadVector(registers, 0, translation, translation.Z < 0 ? (short)-1 : (short)0);
        registers = NativeBattleGeometry.Transform(registers);
        for (int axis = 0; axis < 3; axis++)
        {
            ball = Dword(ball, 0x54 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
        }
        return new(ball, registers);
    }

    public static NativeBattleRaldo19BallDraw DrawMeshes(NativeBattleEffectArena ball, NativeBattleEffectArena temporaryA8, NativeBattleEffectArena vertexScratch, ImmutableArray<NativeBattleRawMesh> meshes, ImmutableArray<NativeBattleActionMatrix> bones, Func<int, short?> readBonePadding18, uint meshMask, uint actorColor40, NativeBattleCameraProjection projection, short dqa, int dqb, NativeBattleGeometryRegisterStep registers, NativeBattleScriptedLightingMemory lighting, NativeBattleSummonColors constants)
    {
        NativeBattleRaldo19BallParameters parameters = Read(ball);
        ValidateBound(temporaryA8, TemporaryLength);
        ValidateBound(vertexScratch, VertexCapacity);
        if (meshes.IsDefault || bones.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Raldo ball drawing requires its original objects and current body bone matrices.");
        }
        lighting.Validate();
        temporaryA8 = Copy(ball, 0, temporaryA8, 0, 32);
        temporaryA8 = Copy(ball, 0x74, temporaryA8, 0x5C, 8);
        temporaryA8 = Word(temporaryA8, 0x9C, unchecked((ushort)(4096 - unchecked((ushort)parameters.Weight))));
        temporaryA8 = Word(temporaryA8, 0x9E, unchecked((ushort)parameters.Weight));
        temporaryA8 = Copy(ball, 0xA6, temporaryA8, 0xA0, 6);
        temporaryA8 = Dword(temporaryA8, 0x90, (actorColor40 & 0xFFFFFF) | 0x24000000);
        temporaryA8 = Dword(temporaryA8, 0x94, (actorColor40 & 0xFFFFFF) | 0x2C000000);
        temporaryA8 = Dword(temporaryA8, 0x98, meshMask);
        NativeBattleActionMatrix rotation = NativeBattleWendigoBallGeometry.Rotation(ReadMatrix(temporaryA8, 0), parameters.Angles);
        temporaryA8 = Matrix(temporaryA8, 0, rotation, null, false);
        lighting = lighting with { ColorMatrix = Packed(temporaryA8, 0) };
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, ReadMatrix(ball, 0x40), true, true, Padding(ball, 0x52)) };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            if ((meshMask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[meshIndex];
            mesh.Validate();
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Raldo ball drawing requires every original vertex group, including empty groups.");
            int vertexCount = 0;
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                if (group.Bone >= bones.Length || group.Count < 0 || group.Bytes.IsDefault || group.Bytes.Length < group.Count * 6)
                {
                    throw new InvalidDataException("Raldo ball vertex group leaves its original source or bone bank.");
                }
                NativeBattleActionMatrix bone = bones[group.Bone];
                lighting = lighting with { LightMatrix = Packed(bone, readBonePadding18(group.Bone)), BackColor = new(bone.X, bone.Y, bone.Z) };
                for (int index = 0; index < group.Count; index++)
                {
                    NativeBattleVector source = new(BinaryPrimitives.ReadInt16LittleEndian(group.Bytes.AsSpan().Slice(index * 6)), BinaryPrimitives.ReadInt16LittleEndian(group.Bytes.AsSpan().Slice(index * 6 + 2)), BinaryPrimitives.ReadInt16LittleEndian(group.Bytes.AsSpan().Slice(index * 6 + 4)));
                    temporaryA8 = Vector(temporaryA8, 0x4C, source);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, source, Padding(temporaryA8, 0x52));
                    registers = TransformLighting(registers, lighting, true);
                    temporaryA8 = Vector(temporaryA8, 0x4C, Ir(registers));
                    NativeBattleVector posed = Vector(temporaryA8, 0x4C);
                    NativeBattleVector relative = new(unchecked((short)(posed.X - parameters.Pivot.X)), unchecked((short)(posed.Y - parameters.Pivot.Y)), unchecked((short)(posed.Z - parameters.Pivot.Z)));
                    temporaryA8 = Vector(temporaryA8, 0x4C, relative);
                    temporaryA8 = Copy(temporaryA8, 0x4C, temporaryA8, 0x54, 8);
                    var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(relative.X, relative.Y, relative.Z);
                    NativeBattleVector sphere = new(unchecked((short)(parameters.Radii.X * unchecked((short)normalized.X) / 4096)), unchecked((short)(parameters.Radii.Y * unchecked((short)normalized.Y) / 4096)), unchecked((short)(parameters.Radii.Z * unchecked((short)normalized.Z) / 4096)));
                    temporaryA8 = Vector(temporaryA8, 0x4C, sphere);
                    registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)Short(temporaryA8, 0x9C)), Ir = [(int)unchecked((ushort)relative.X), (int)unchecked((ushort)relative.Y), (int)unchecked((ushort)relative.Z)] } };
                    registers = Weighted(registers, false, constants);
                    registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)parameters.Weight), Ir = [(int)unchecked((ushort)sphere.X), (int)unchecked((ushort)sphere.Y), (int)unchecked((ushort)sphere.Z)] } };
                    registers = Weighted(registers, true, constants);
                    temporaryA8 = Vector(temporaryA8, 0x4C, Ir(registers));
                    registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporaryA8, 0x4C), Padding(temporaryA8, 0x52));
                    registers = TransformLighting(registers, lighting, false);
                    NativeBattleVector turned = Ir(registers);
                    NativeBattleVector final = new(unchecked((short)(turned.X + parameters.Pivot.X)), unchecked((short)(turned.Y + parameters.Pivot.Y)), unchecked((short)(turned.Z + parameters.Pivot.Z)));
                    temporaryA8 = Vector(temporaryA8, 0x4C, final);
                    int target = checked(vertexCount++ * 20);
                    if (target > VertexCapacity - 20)
                    {
                        throw new InvalidDataException("Raldo ball vertices exceed their original scratch owner.");
                    }
                    vertexScratch = Copy(temporaryA8, 0x4C, vertexScratch, target, 8);
                }
            }
            if (vertexCount != mesh.Vertices.Length)
            {
                throw new InvalidDataException("Raldo ball packed groups disagree with their decoded vertices.");
            }
            ImmutableArray<NativeBattleModuleProjectedVertex>.Builder projected = ImmutableArray.CreateBuilder<NativeBattleModuleProjectedVertex>(vertexCount);
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                int target = vertex * 20;
                temporaryA8 = Copy(vertexScratch, target, temporaryA8, 0x34, 8);
                registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporaryA8, 0x34), Padding(temporaryA8, 0x3A));
                registers = NativeBattleGeometry.ProjectSingle(registers, projection, dqa, dqb);
                uint screen = registers.Geometry.Sxy[2]!.Value;
                uint depth = registers.Geometry.Sz[3]!.Value;
                vertexScratch = Dword(vertexScratch, target + 8, screen);
                vertexScratch = Dword(vertexScratch, target + 12, depth);
                projected.Add(new(screen, unchecked((ushort)depth), registers.Colors.Flags!.Value));
            }
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
            {
                if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
                {
                    continue;
                }
                uint[] screen = new uint[polygon.Quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF;
                    if (index >= vertexCount)
                    {
                        throw new InvalidDataException("Raldo ball polygon leaves its current vertex scratch.");
                    }
                    temporaryA8 = Dword(temporaryA8, 0x80 + corner * 4, (uint)index);
                    screen[corner] = projected[index].Screen;
                    temporaryA8 = Dword(temporaryA8, 0x24 + corner * 4, screen[corner]);
                }
                registers = registers with { Geometry = registers.Geometry with { Sxy = [screen[0], screen[1], screen[2]] } };
                registers = NativeBattleGeometry.Clip45EE10(registers);
                temporaryA8 = Dword(temporaryA8, 0x7C, registers.Geometry.Mac0!.Value);
                if (unchecked((int)registers.Geometry.Mac0.Value) <= 0)
                {
                    continue;
                }
                if (polygon.Quad)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(6)) & 0xFFF;
                    if (index >= vertexCount)
                    {
                        throw new InvalidDataException("Raldo ball fourth corner leaves its current vertex scratch.");
                    }
                    temporaryA8 = Dword(temporaryA8, 0x8C, (uint)index);
                    temporaryA8 = Dword(temporaryA8, 0x30, projected[index].Screen);
                }
                NativeBattleGfGeometryBatch batch = NativeBattleWendigoBallGeometry.Packets([polygon], projected.ToImmutable(), actorColor40);
                NativeBattleGeometryPrimitive primitive = batch.Attempts.Single().Primitive!;
                temporaryA8 = Dword(temporaryA8, 0x78, unchecked((uint)(primitive.Bucket - 17)));
                attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
                allocated = checked(allocated + batch.AllocationBytes);
            }
        }
        return new(temporaryA8, vertexScratch, registers, lighting, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleGeometryRegisterStep TransformLighting(NativeBattleGeometryRegisterStep registers, NativeBattleScriptedLightingMemory lighting, bool bone)
    {
        NativeBattleActionMatrix matrix = (bone ? lighting.LightMatrix : lighting.ColorMatrix)?.ToActionMatrix() ?? throw new NotSupportedException("Raldo ball requires its current selected light/color matrix.");
        if (bone)
        {
            NativeBattleScriptedBackColor back = (lighting.BackColor ?? throw new NotSupportedException("Raldo ball requires its current raw bone translation controls.")).RequireKnown();
            matrix = matrix with { X = back.Red, Y = back.Green, Z = back.Blue };
        }
        NativeBattleGeometryRegisterStep calculation = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        calculation = NativeBattleGeometry.Transform(calculation, bone);
        return registers with { Colors = calculation.Colors };
    }

    public static NativeBattleGeometryRegisterStep Weighted(NativeBattleGeometryRegisterStep registers, bool accumulate, NativeBattleSummonColors constants)
    {
        registers.Colors.Validate();
        if (constants.FixedScale != 1f / 4096f || BitConverter.DoubleToInt64Bits(constants.IntegerBias) != 0x4330000080000000L)
        {
            throw new NotSupportedException("Raldo ball weighted interpolation requires its original fixed-scale constants.");
        }
        uint? material = registers.Colors.Material;
        short cue = unchecked((short)(registers.Colors.Ir0 ?? throw new NotSupportedException("Raldo ball requires its actual interpolation factor.")));
        int?[] mac = new int?[3];
        int?[] ir = new int?[3];
        uint[] irFlags = [0x81000000, 0x80800000, 0x00400000];
        uint[] rgbFlags = [0x00200000, 0x00100000, 0x00080000];
        uint flags = 0;
        uint output = 0;
        for (int lane = 0; lane < 3; lane++)
        {
            short input = unchecked((short)(registers.Colors.Ir[lane] ?? throw new NotSupportedException("Raldo ball weighted interpolation requires all current IR inputs.")));
            int value = NativePrecision53.RoundInteger(input * (cue * (double)constants.FixedScale));
            if (accumulate)
            {
                value = unchecked(value + (registers.Colors.Mac[lane] ?? throw new NotSupportedException("Raldo ball weighted accumulation requires its previous MAC.")));
            }
            mac[lane] = value;
            ir[lane] = Math.Clamp(value, short.MinValue, short.MaxValue);
            if (value is < short.MinValue or > short.MaxValue)
            {
                flags |= irFlags[lane];
            }
            int visible = value;
            if ((uint)visible > 255)
            {
                flags |= rgbFlags[lane];
            }
            output |= (uint)Math.Clamp(visible, 0, 255) << (lane * 8);
        }
        // The retained code byte affects only the FIFO word, not interpolation or saturation.
        uint? fifo = material is uint code ? (code & 0xFF000000) | output : null;
        return registers with { Colors = registers.Colors with { Mac = mac.ToImmutableArray(), Ir = ir.ToImmutableArray(), Rgb = [registers.Colors.Rgb[1], registers.Colors.Rgb[2], fifo], Flags = flags } };
    }

    private static NativeBattleVector Ir(NativeBattleGeometryRegisterStep registers) => new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset) => new(Short(arena, offset), Short(arena, offset + 2), Short(arena, offset + 4));
    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short? Padding(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static NativeBattleActionMatrix ReadMatrix(NativeBattleEffectArena arena, int offset) => new(Enumerable.Range(0, 9).Select(index => Short(arena, offset + index * 2)).ToImmutableArray(), unchecked((int)Dword(arena, offset + 20)), unchecked((int)Dword(arena, offset + 24)), unchecked((int)Dword(arena, offset + 28)), 4096);
    private static NativeBattleScriptedLightingMatrix Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 20);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static NativeBattleScriptedLightingMatrix Packed(NativeBattleActionMatrix matrix, short? padding)
    {
        NativeBattleEffectArena arena = Matrix(NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, matrix, padding, true);
        return Packed(arena, 0);
    }
    private static NativeBattleEffectArena Matrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix, short? padding, bool writeTranslation)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            arena = Word(arena, offset + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        if (writeTranslation)
        {
            arena = padding is short value ? Word(arena, offset + 18, unchecked((ushort)value)) : arena.MarkUnknown(offset + 18, 2);
            arena = Dword(arena, offset + 20, unchecked((uint)matrix.X));
            arena = Dword(arena, offset + 24, unchecked((uint)matrix.Y));
            arena = Dword(arena, offset + 28, unchecked((uint)matrix.Z));
        }
        return arena;
    }
    private static NativeBattleEffectArena Vector(NativeBattleEffectArena arena, int offset, NativeBattleVector vector)
    {
        return Word(Word(Word(arena, offset, unchecked((ushort)vector.X)), offset + 2, unchecked((ushort)vector.Y)), offset + 4, unchecked((ushort)vector.Z));
    }
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena target, int targetOffset, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(source, sourceOffset, target, targetOffset, length);
    }
    private static void ValidateBound(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Raldo ball arena leaves its actual original physical owner.");
        }
    }
}
