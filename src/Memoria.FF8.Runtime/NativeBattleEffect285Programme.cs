using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect285Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect285Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect285Programme
{
    public static NativeBattleEffect285Frame Initialize(NativeBattleEffect285Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect285Resources resources)
    {
        NativeBattleGfAnimationWorkspace cache = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, clearBytes);
        cache = cache.Initialize7016B0(resources.Act(source), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, cache);
        return frame;
    }

    public static NativeBattleEffect285Step Advance(NativeBattleEffect285Frame frame, uint workspace, int length, NativeBattleEffectArena caller16, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Current() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, length);
        NativeBattleGfAnimationSource source = resources.Act(Current().ResourceAddress);
        NativeBattleGfAnimationAdvance result = Current().Advance701970(source, 0, (cache, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, cache);
            frame = DrawNode(frame, source, node, caller16, resources, inputs);
            return Current();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, result.Workspace);
        return new(frame, result.Remaining);
    }

    public static NativeBattleEffect285Frame DrawNode(NativeBattleEffect285Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller16, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 108);
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometry);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (count < 0 || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("The programme requires its current selected geometry shapes.");
        }
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = checked(geometry + 12 + (uint)(selected * count * 8));
        if (morph)
        {
            vertices = Dword285(caller16, 12);
            if (vertices != checked(frame.State.ReadDword(0x2507D68) + 0x1AB8) || count * 8 > 896)
            {
                throw new InvalidDataException("The programme morph requires its current physical scratch rows.");
            }
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int vertex = 0; vertex < count; vertex++)
            {
                registers = Weight(registers, Shape(node.Word40, vertex), 4096 - node.Scalar38, false);
                registers = Weight(registers, Shape(node.Word42, vertex), node.Scalar38, true);
                NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear();
                for (int axis = 0; axis < 3; axis++)
                {
                    xyz = PutWord285(xyz, axis * 2, registers.Colors.Ir[axis]!.Value);
                }
                frame = WriteBytes(frame, checked(vertices + (uint)(vertex * 8)), xyz, 6);
            }
            frame = Publish(frame, registers);
        }
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = view.ReadMatrix();
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x;
        int y;
        int z;
        if ((node.Header & 0x1000) != 0)
        {
            frame = Transform(frame, view, caller16, out NativeBattleActionMatrix first);
            NativeBattlePackedMatrix32 normalized = Packed(frame.State.ReadView(0x2507D70, 32));
            NativeBattleEffectArena local = PutPoint(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, node.Vector8);
            frame = Transform(frame, normalized, local, out NativeBattleActionMatrix second);
            x = unchecked(first.X + second.X);
            y = unchecked(first.Y + second.Y);
            z = unchecked(first.Z + second.Z);
            if ((node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(normalized.ReadMatrix(), rotation);
            }
        }
        else if ((node.Header & 0x200) != 0)
        {
            frame = Transform(frame, view, caller16, out NativeBattleActionMatrix point);
            x = unchecked(point.X + node.Vector8.X);
            y = unchecked(point.Y + node.Vector8.Y);
            z = unchecked(point.Z + node.Vector8.Z);
        }
        else
        {
            NativeBattleVector vector = new(unchecked((short)(Word285(caller16, 0) + node.Vector8.X)), unchecked((short)(Word285(caller16, 2) + node.Vector8.Y)), unchecked((short)(Word285(caller16, 4) + node.Vector8.Z)));
            NativeBattleEffectArena local = PutPoint(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, vector);
            frame = Transform(frame, view, local, out NativeBattleActionMatrix point);
            x = point.X;
            y = point.Y;
            z = point.Z;
            if ((node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(camera, rotation);
            }
        }
        rotation = rotation with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Put285(Put285(Put285(Put285(Put285(Context(frame, temporary, 108), 0, geometry), 4, vertices), 12, unchecked((uint)(int)node.Scalar36)), 0x18, unchecked((uint)(int)Word285(caller16, 8))), 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            context = Put285(context, 8, node.Color32);
        }
        short phase = Word285(caller16, 10);
        context = PutWord285(context, 0x20, 0);
        if (phase != 0 && node.GeometryIndex == 0)
        {
            context = PutWord285(PutWord285(PutWord285(PutWord285(PutWord285(PutWord285(PutWord285(PutWord285(PutWord285(context, 0x22, phase), 0x24, 0), 0x26, 0), 0x28, 256), 0x2A, 256), 0x2C, 128), 0x2E, 128), 0x30, 256), 0x32, 128);
        }
        else
        {
            context = PutWord285(context, 0x22, 0);
        }
        frame = PublishContext(frame, temporary, context, 108);
        NativeBattleWindowedMeshContext108Step draw = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, offset =>
        {
            NativeBattleEffectArena row = morph ? ReadBytes(frame, checked(vertices + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            if (!known.Take(6).All(value => value))
            {
                throw new NotSupportedException("The selected programme vertex requires its current XYZ bytes.");
            }
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : (short?)null);
        });
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context108, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect285DrawBatch(draw.Batch, 0x2507D60));
        return Release(frame, 108);

        NativeBattleVector Shape(int shape, int vertex)
        {
            int offset = checked((int)(geometry - source.Address) + 12 + (shape * count + vertex) * 8);
            ReadOnlySpan<byte> row = source.Data.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
        }
        NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector vector, int factor, bool accumulate)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
            return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors);
        }
    }

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena source)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffect285Frame Transform(NativeBattleEffect285Frame frame, NativeBattlePackedMatrix32 matrix, NativeBattleEffectArena point8, out NativeBattleActionMatrix point)
    {
        frame = LoadMatrix(frame, matrix, true, false);
        (byte[] bytes, bool[] known) = point8.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(Word285(point8, 0), Word285(point8, 2), Word285(point8, 4)), w), false);
        point = NativeBattleCuragaMatrices.Identity8DD770() with { X = registers.Colors.Mac[0]!.Value, Y = registers.Colors.Mac[1]!.Value, Z = registers.Colors.Mac[2]!.Value };
        return Publish(frame, registers);
    }

}
