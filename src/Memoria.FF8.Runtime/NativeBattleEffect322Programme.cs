using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect322Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect322Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect322Programme
{
    public static NativeBattleEffect322Frame Initialize(NativeBattleEffect322Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect322Resources resources)
    {
        NativeBattleGfAnimationWorkspace cache = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, clearBytes);
        cache = cache.Initialize7016B0(resources.Act(source), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, cache);
        return frame;
    }

    public static NativeBattleEffect322Step Advance(NativeBattleEffect322Frame frame, uint workspace, int length, NativeBattleEffectArena caller16, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
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

    public static NativeBattleEffect322Frame DrawNode(NativeBattleEffect322Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller16, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 88);
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
            vertices = Dword322(caller16, 0xC);
            if (vertices != checked(frame.State.ReadDword(0x2505340) + 0x1CCF0) || count * 8 > 200)
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
                    xyz = PutWord322(xyz, axis * 2, registers.Colors.Ir[axis]!.Value);
                }
                frame = WriteBytes(frame, checked(vertices + (uint)(vertex * 8)), xyz, 6);
            }
            frame = Publish(frame, registers);
        }
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = view.ReadMatrix();
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleEffectArena point;
        if ((node.Header & 0x200) != 0)
        {
            point = caller16;
        }
        else
        {
            point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                int component = axis == 0 ? node.Vector8.X : axis == 1 ? node.Vector8.Y : node.Vector8.Z;
                point = PutWord322(point, axis * 2, Word322(caller16, axis * 2) + component);
            }
            rotation = NativeBattleGfMatrices.Multiply56C090(camera, rotation);
        }
        frame = LoadMatrix(frame, view, true, false);
        (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
        short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
        NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector322(point), w), false);
        frame = Publish(frame, transformed);
        bool addNode = (node.Header & 0x200) != 0;
        rotation = rotation with
        {
            X = unchecked(transformed.Colors.Mac[0]!.Value + camera.X + (addNode ? node.Vector8.X : 0)),
            Y = unchecked(transformed.Colors.Mac[1]!.Value + camera.Y + (addNode ? node.Vector8.Y : 0)),
            Z = unchecked(transformed.Colors.Mac[2]!.Value + camera.Z + (addNode ? node.Vector8.Z : 0))
        };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Put322(Put322(Put322(Put322(Put322(Context(frame, temporary, 88), 0, geometry), 4, vertices), 12, unchecked((uint)(int)node.Scalar36)), 0x18, Dword322(caller16, 8)), 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            context = Put322(context, 8, node.Color32);
        }
        frame = PublishContext(frame, temporary, context, 88);
        (NativeBattleVector Vertex, short? W) ReadRow(int offset)
        {
            NativeBattleEffectArena row = morph ? ReadBytes(frame, checked(vertices + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            if (!known.Take(6).All(value => value))
            {
                throw new NotSupportedException("The selected programme vertex requires its current XYZ bytes.");
            }
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : (short?)null);
        }
        NativeBattleDefaultActMeshStep draw = NativeBattleDefaultActMesh.Draw(frame.Memory, source, checked((int)(geometry - source.Address)), context, inputs.ReadProjection(frame), inputs.Colors, offset => ReadRow(offset).Vertex, offset => ReadRow(offset).W, 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect322DrawBatch(draw.Batch, 0x2505334));
        return Release(frame, 88);

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
}
