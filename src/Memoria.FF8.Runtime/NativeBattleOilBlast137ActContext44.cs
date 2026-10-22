using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilBlast137ActPreparation(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleActionMatrix Matrix, int GeometryOffset, uint SelectedVerticesAddress, bool DefaultBackend, Func<int, NativeBattleVector> ReadVertex, Func<int, short?> ReadW);
public sealed record NativeBattleOilBlast137ActContextStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleOilBlast137ActContext44
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleOilBlast137ActContextStep Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context44, NativeBattleEffectArena temporary88, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint morphAddress, int morphLength, short? localVectorW = null)
    {
        NativeBattleOilBlast137ActPreparation prepared = Prepare(memory, source, node, context44, temporary88, colors, morphAddress, morphLength, localVectorW);
        if (prepared.DefaultBackend)
        {
            NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(prepared.Memory, source, prepared.GeometryOffset, prepared.Temporary88, projection, colors, prepared.ReadVertex, prepared.ReadW);
            return new(drawn.Memory, drawn.Context88, drawn.Batch);
        }
        NativeBattleScriptedGraphicsMemory controls = prepared.Memory.ScriptedGraphics ?? throw new NotSupportedException("Oil Blast ACT requires its current projection controls.");
        NativeBattlePrimitiveProcess primitive = prepared.Memory.PrimitiveProcess ?? throw new NotSupportedException("Oil Blast ACT requires its current primitive process.");
        NativeBattleModelRendererRegisters far = primitive.ModelRendererRegisters ?? throw new NotSupportedException("Oil Blast ACT requires its current renderer controls.");
        int geometry = prepared.GeometryOffset;
        int end = checked(geometry + BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan(geometry, 4)));
        foreach (int stride in new[] { 12, 12, 20, 24, 20, 24, 28, 36 })
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan(end, 4));
            if (count < 0)
            {
                throw new InvalidDataException("Oil Blast ACT requires nonnegative primitive counts.");
            }
            end = checked(end + 4 + count * stride);
        }
        ImmutableArray<byte> shape = source.Data.AsSpan(geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        NativeBattleGeometryRegisterStep registers = new(prepared.Memory.GeometryRegisters!, prepared.Memory.ScriptedColorRegisters!);
        NativeBattleCuraga88MeshStep emitted = NativeBattleCuraga88Mesh.EmitBatch(shape, checked(source.Address + (uint)geometry), prepared.Matrix, projection, prepared.Temporary88, registers, far, controls.Dqa, controls.Dqb, controls.Zsf4, colors, zsf3: controls.Zsf3, selectedVerticesAddress: prepared.SelectedVerticesAddress, readSelectedVertex: offset => (prepared.ReadVertex(offset), prepared.ReadW(offset)));
        memory = prepared.Memory with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = emitted.FarControls } };
        return new(memory, emitted.Context88, emitted.Batch);
    }

    public static NativeBattleOilBlast137ActPreparation Prepare(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context44, NativeBattleEffectArena temporary88, NativeBattleSummonColors colors, uint morphAddress, int morphLength, short? localVectorW = null)
    {
        if (!Visible(node))
        {
            throw new InvalidOperationException("Oil Blast ACT context44 allocation requires a visible node.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Oil Blast ACT context44 requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Oil Blast ACT context44 requires its current color registers."));
        int geometry = Integer(checked(8 + node.GeometryIndex * 4));
        int count = Integer(checked(geometry + 4));
        int variants = Integer(checked(geometry + 8));
        if (count < 1 || variants < 1 || node.Word40 < 0 || node.Word40 >= variants || node.Word42 < 0 || node.Word42 >= variants)
        {
            throw new InvalidDataException("Oil Blast ACT context44 requires its original selected vertex variants.");
        }
        int first = checked(geometry + 12 + node.Word40 * count * 8);
        int second = checked(geometry + 12 + node.Word42 * count * 8);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? second : first;
        bool interpolate = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        NativeBattleEffectArena? arena = memory.Arena;
        uint selectedAddress;
        int morphOffset = 0;
        if (interpolate)
        {
            if (arena is null)
            {
                throw new NotSupportedException("Oil Blast ACT context44 interpolation requires its current effect arena.");
            }
            long offset = (long)morphAddress - NativeBattleEffectNodeMemory.ArenaAddress;
            if (count > morphLength / 8 || offset < 0 || offset > NativeBattleEffectArena.Length - morphLength || Dword(context44, 40) != morphAddress)
            {
                throw new InvalidDataException("Oil Blast ACT context44 interpolation leaves its actual working scratch.");
            }
            morphOffset = checked((int)offset);
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector a = Vector(checked(first + vertex * 8));
                NativeBattleVector b = Vector(checked(second + vertex * 8));
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - node.Scalar38)), Ir = [unchecked((ushort)a.X), unchecked((ushort)a.Y), unchecked((ushort)a.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)node.Scalar38), Ir = [unchecked((ushort)b.X), unchecked((ushort)b.Y), unchecked((ushort)b.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, colors);
                byte[] xyz = new byte[6];
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(axis * 2), unchecked((short)registers.Colors.Ir[axis]!.Value));
                }
                arena = arena.Write(checked(morphOffset + vertex * 8), xyz);
            }
            selectedAddress = morphAddress;
            memory = memory with { Arena = arena };
        }
        else
        {
            _ = Read(selected, checked(count * 8));
            selectedAddress = checked(source.Address + (uint)selected);
        }
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (byte[] cameraBytes, bool[] cameraKnown) = context44.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 camera = new(cameraBytes.ToImmutableArray(), cameraKnown.ToImmutableArray());
        NativeBattleActionMatrix view = camera.ReadMatrix();
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, true, false, PackedPadding(camera)) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, localVectorW);
        registers = NativeBattleGeometry.Transform(registers, false);
        (int X, int Y, int Z) translation = (registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value);
        if ((node.Header & 0x8000) == 0)
        {
            matrix = NativeBattleGfMatrices.Multiply56C090(view, matrix);
        }
        matrix = matrix with { X = unchecked(translation.X + unchecked((int)Dword(context44, 20))), Y = unchecked(translation.Y + unchecked((int)Dword(context44, 24))), Z = unchecked(translation.Z + unchecked((int)Dword(context44, 28))) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        temporary88 = Store(temporary88, 0, checked(source.Address + (uint)geometry));
        temporary88 = Store(temporary88, 4, selectedAddress);
        temporary88 = Store(temporary88, 12, unchecked((uint)(int)node.Scalar36));
        temporary88 = Store(temporary88, 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            temporary88 = Store(temporary88, 8, node.Color32);
        }
        bool useDefault = Dword(context44, 36) == 1 && node.GeometryIndex is 3 or 4;
        if (useDefault)
        {
            temporary88 = Store(temporary88, 0x18, Dword(context44, 32));
        }
        NativeBattleEffectArena? selectedArena = arena;
        return new(memory, temporary88, matrix, geometry, selectedAddress, useDefault, ReadVertex, ReadW);

        NativeBattleVector ReadVertex(int offset)
        {
            RequireVertex(offset);
            if (!interpolate)
            {
                return Vector(checked(selected + offset));
            }
            byte[] xyz = selectedArena!.Read(checked(morphOffset + offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
        }

        short? ReadW(int offset)
        {
            RequireVertex(offset);
            return interpolate ? OptionalWord(selectedArena!, checked(morphOffset + offset + 6)) : BinaryPrimitives.ReadInt16LittleEndian(Read(checked(selected + offset + 6), 2));
        }

        void RequireVertex(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Oil Blast ACT context44 face leaves its selected packed vertex owner.");
            }
        }

        int Integer(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        NativeBattleVector Vector(int offset)
        {
            ReadOnlySpan<byte> bytes = Read(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }
        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Oil Blast ACT context44 read leaves its immutable source.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
    }

    private static short Signed(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleEffectArena Store(NativeBattleEffectArena memory, int offset, uint value) => memory.Write(offset, BitConverter.GetBytes(value));
    private static short? OptionalWord(NativeBattleEffectArena memory, int offset)
    {
        (byte[] bytes, bool[] known) = memory.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static short? PackedPadding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null;
}
