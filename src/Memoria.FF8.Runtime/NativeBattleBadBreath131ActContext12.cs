using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath131ActPreparation(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleActionMatrix Matrix, int GeometryOffset, uint SelectedVerticesAddress, Func<int, NativeBattleVector> ReadVertex, Func<int, short?> ReadW);
public sealed record NativeBattleBadBreath131ActContextStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleBadBreath131ActContext12
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleBadBreath131ActContextStep Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context12, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena temporary88, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint morphAddress, int morphLength, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleBadBreath131ActPreparation prepared = Prepare(memory, source, node, context12, camera, temporary88, colors, morphAddress, morphLength);
        NativeBattleScriptedGraphicsMemory controls = prepared.Memory.ScriptedGraphics ?? throw new NotSupportedException("Bad Breath ACT requires its current projection controls.");
        NativeBattlePrimitiveProcess primitive = prepared.Memory.PrimitiveProcess ?? throw new NotSupportedException("Bad Breath ACT requires its current primitive process.");
        NativeBattleModelRendererRegisters far = primitive.ModelRendererRegisters ?? throw new NotSupportedException("Bad Breath ACT requires its current renderer controls.");
        int geometry = prepared.GeometryOffset;
        int end = checked(geometry + BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan(geometry, 4)));
        foreach (int stride in new[] { 12, 12, 20, 24, 20, 24, 28, 36 })
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan(end, 4));
            if (count < 0)
            {
                throw new InvalidDataException("Bad Breath ACT requires nonnegative primitive counts.");
            }
            end = checked(end + 4 + count * stride);
        }
        ImmutableArray<byte> shape = source.Data.AsSpan(geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        NativeBattleGeometryRegisterStep registers = new(prepared.Memory.GeometryRegisters!, prepared.Memory.ScriptedColorRegisters!);
        NativeBattleCuraga88MeshStep emitted = NativeBattleCuraga88Mesh.EmitBatch(shape, checked(source.Address + (uint)geometry), prepared.Matrix, projection, prepared.Temporary88, registers, far, controls.Dqa, controls.Dqb, controls.Zsf4, colors, zsf3: controls.Zsf3, selectedVerticesAddress: prepared.SelectedVerticesAddress, readSelectedVertex: offset => (prepared.ReadVertex(offset), prepared.ReadW(offset)), pilotIdentity: pilotIdentity);
        memory = prepared.Memory with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = emitted.FarControls } };
        return new(memory, emitted.Context88, emitted.Batch);
    }

    public static NativeBattleBadBreath131ActPreparation Prepare(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context12, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena temporary88, NativeBattleSummonColors colors, uint morphAddress, int morphLength)
    {
        if (!Visible(node))
        {
            throw new InvalidOperationException("Bad Breath ACT context12 allocation requires a visible node.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Bad Breath ACT context12 requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Bad Breath ACT context12 requires its current color registers."));
        int geometry = Integer(checked(8 + node.GeometryIndex * 4));
        int count = Integer(checked(geometry + 4));
        int variants = Integer(checked(geometry + 8));
        if (count < 1 || variants < 1 || node.Word40 < 0 || node.Word40 >= variants || node.Word42 < 0 || node.Word42 >= variants)
        {
            throw new InvalidDataException("Bad Breath ACT context12 requires its original selected vertex variants.");
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
                throw new NotSupportedException("Bad Breath ACT context12 interpolation requires its current effect arena.");
            }
            long offset = (long)morphAddress - NativeBattleEffectNodeMemory.ArenaAddress;
            if (count > morphLength / 8 || offset < 0 || offset > NativeBattleEffectArena.Length - morphLength || Dword(context12, 8) != morphAddress)
            {
                throw new InvalidDataException("Bad Breath ACT context12 interpolation leaves its actual working scratch.");
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
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16) with
        {
            X = unchecked(node.Vector8.X + Signed(context12, 0)),
            Y = unchecked(node.Vector8.Y + Signed(context12, 2)),
            Z = unchecked(node.Vector8.Z + Signed(context12, 4))
        };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), local);
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
        temporary88 = Store(temporary88, 0x1C, node.Scalar36 == 0 ? 0x2000u : 0x20C0u);
        if (node.Scalar36 != 0)
        {
            temporary88 = Store(temporary88, 8, node.Color32);
        }
        NativeBattleEffectArena? selectedArena = arena;
        return new(memory, temporary88, matrix, geometry, selectedAddress, ReadVertex, ReadW);

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
                throw new InvalidDataException("Bad Breath ACT context12 face leaves its selected packed vertex owner.");
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
                throw new InvalidDataException("Bad Breath ACT context12 read leaves its immutable source.");
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
