using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect210Programme
{
    public static NativeBattleEffect210Frame Initialize(NativeBattleEffect210Frame frame, uint workspace, uint sourceAddress, int length, NativeBattleEffect210Resources resources)
    {
        NativeBattleGfAnimationWorkspace current = new(new byte[length].ToImmutableArray());
        current = current.Initialize7016B0(resources.Act(sourceAddress), length);
        return WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), length);
    }

    public static NativeBattleEffect210Step Advance(NativeBattleEffect210Frame frame, uint workspace, int length, NativeBattleEffectArena caller48, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Current() => new(ReadBytes(frame, workspace, length).Read(0, length).ToImmutableArray());
        NativeBattleGfAnimationSource source = resources.Act(Current().ResourceAddress);
        NativeBattleGfAnimationAdvance result = Current().Advance701970(source, unchecked((int)frame.State.ReadDword(FrozenGlobal)), (cache, node) =>
        {
            frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, cache.Data.AsSpan()), length);
            frame = DrawNode(frame, source, node, caller48, resources, inputs);
            return Current();
        });
        frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, result.Workspace.Data.AsSpan()), length);
        return new(frame, result.Remaining);
    }

    public static NativeBattleEffect210Step Advance9AA0(NativeBattleEffect210Frame frame, uint taskAddress, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(0x20, BitConverter.GetBytes(SelectorsAddress)).Write(0x24, BitConverter.GetBytes((short)-128)).Write(0x2C, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + 0xAB10));
        NativeBattleEffect210Step step = Advance(frame, taskAddress + 0x14, 0x1C0, caller, resources, inputs);
        return new(step.Frame, step.Result == 0 ? 2 : 0);
    }

    public static NativeBattleEffect210Frame DrawNode(NativeBattleEffect210Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller48, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs, uint cursorGlobal = CursorGlobal)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometry);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (count < 1 || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect210 ACT geometry leaves its current selected shapes.");
        }
        (frame, uint temporaryAddress) = Allocate(frame, 108);
        uint vertices = checked(geometry + 12 + (uint)((node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40) * count * 8));
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        if (morph)
        {
            vertices = Dword(caller48, 0x2C);
            if (vertices != checked(frame.State.ReadDword(ArenaGlobal) + 0xAB10))
            {
                throw new InvalidDataException("Effect210 morph requires its actual shared row owner.");
            }
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector first = Shape(node.Word40, vertex);
                NativeBattleVector second = Shape(node.Word42, vertex);
                registers = Weight(registers, first, 4096 - node.Scalar38, false);
                registers = Weight(registers, second, node.Scalar38, true);
                byte[] xyz = new byte[6];
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(axis * 2), unchecked((short)registers.Colors.Ir[axis]!.Value));
                }
                frame = WriteBytes(frame, checked(vertices + (uint)(vertex * 8)), NativeBattleEffectArena.Clear().Write(0, xyz), 6);
            }
            frame = Publish(frame, registers);
        }
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector point = node.Vector8;
        (byte[] matrixBytes, bool[] matrixKnown) = caller48.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 callerMatrix = new(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray());
        NativeBattlePackedMatrix32 selectedBasis = (node.Header & 0x1000) != 0 ? Packed(frame.State.ReadView(ViewAddress, 32)) : callerMatrix;
        NativeBattleActionMatrix basis = selectedBasis.ReadMatrix();
        NativeBattleActionMatrix caller = callerMatrix.ReadMatrix();
        frame = LoadMatrix(frame, selectedBasis, true, false);
        NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.LoadVector(Registers(frame), 0, point, null);
        transformed = NativeBattleGeometry.Transform(transformed, false);
        frame = Publish(frame, transformed);
        rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation) with { X = unchecked(caller.X + transformed.Colors.Mac[0]!.Value), Y = unchecked(caller.Y + transformed.Colors.Mac[1]!.Value), Z = unchecked(caller.Z + transformed.Colors.Mac[2]!.Value) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        flags |= (node.Header & 0x2000) != 0 ? 0xCu : 0;
        flags |= node.Scalar36 != 0 ? 0xC0u : 0;
        NativeBattleEffectArena context = Context(frame, temporaryAddress, 108).Write(0, BitConverter.GetBytes(geometry)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(0x18, BitConverter.GetBytes((int)Word(caller48, 0x24))).Write(0x1C, BitConverter.GetBytes(flags)).Write(0x20, new byte[8]).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        byte selector = resources.ReadStatic(checked(Dword(caller48, 0x20) + node.GeometryIndex), 1)[0];
        if (selector is (byte)'1' or (byte)'2' or (byte)'3')
        {
            context = context.Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes((ushort)(selector == '3' ? 128 : 256))).Write(0x32, BitConverter.GetBytes((ushort)(selector == '3' ? 256 : 128)));
            if (selector == '2')
            {
                context = context.Write(0x2E, BitConverter.GetBytes((ushort)128));
            }
            context = context.Write(selector == '3' ? 0x20 : 0x22, BitConverter.GetBytes(unchecked((ushort)(caller48.Read(selector == '3' ? 0x28 : 0x26, 1)[0] & 127))));
        }
        else if (selector == '9')
        {
            context = context.Write(0x18, BitConverter.GetBytes(128));
        }
        NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, offset =>
        {
            NativeBattleEffectArena row = morph ? ReadBytes(frame, checked(vertices + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            if (!known.Take(6).All(value => value))
            {
                throw new NotSupportedException("Effect210 ACT requires its reached current vertex XYZ.");
            }
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : (short?)null);
        });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporaryAddress, drawn.Context108, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect210DrawBatch(drawn.Batch, cursorGlobal));
        return Release(frame, 108);

        NativeBattleVector Shape(int shape, int vertex)
        {
            int offset = checked((int)(geometry - source.Address) + 12 + (shape * count + vertex) * 8);
            ReadOnlySpan<byte> bytes = source.Data.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }
        NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector vector, int factor, bool accumulate)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
            return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors);
        }
    }

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
