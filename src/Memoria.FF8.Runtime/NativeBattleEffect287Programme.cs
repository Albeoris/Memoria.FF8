using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect287Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect287Programme
{
    public static NativeBattleEffect287Frame Initialize(NativeBattleEffect287Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect287Resources resources)
    {
        NativeBattleGfAnimationWorkspace cache = new(ReadBytes(frame, workspace, clearBytes).ReadSnapshot(0, clearBytes).Data.ToImmutableArray());
        cache = cache.Initialize7016B0(resources.Act(source), clearBytes);
        return WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, cache.Data.AsSpan()), clearBytes);
    }

    public static NativeBattleEffect287Step Advance(NativeBattleEffect287Frame frame, uint workspace, int length, NativeBattleEffectArena caller40, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Current() => new(ReadBytes(frame, workspace, length).Read(0, length).ToImmutableArray());
        NativeBattleGfAnimationSource source = resources.Act(Current().ResourceAddress);
        NativeBattleGfAnimationAdvance result = Current().Advance701970(source, 0, (cache, node) =>
        {
            frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, cache.Data.AsSpan()), length);
            frame = DrawNode(frame, source, node, caller40, resources, inputs);
            return Current();
        });
        frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, result.Workspace.Data.AsSpan()), length);
        return new(frame, result.Remaining);
    }

    public static NativeBattleEffect287Frame DrawNode(NativeBattleEffect287Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller40, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
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
            vertices = Dword287(caller40, 0x24);
            if (vertices != checked(frame.State.ReadDword(0x2556380) + 0x1A18) || count * 8 > 1408)
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
                    xyz = PutWord287(xyz, axis * 2, registers.Colors.Ir[axis]!.Value);
                }
                frame = WriteBytes(frame, checked(vertices + (uint)(vertex * 8)), xyz, 6);
            }
            frame = Publish(frame, registers);
        }
        (byte[] matrixBytes, bool[] matrixKnown) = caller40.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 caller = new(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray());
        NativeBattleActionMatrix basis = caller.ReadMatrix();
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector translated = (node.Header & 0x800) != 0 ? NativeBattleGfMatrices.RotateShort56C4F0(rotation, node.Vector8) : node.Vector8;
        int x = translated.X;
        int y = translated.Y;
        int z = translated.Z;
        if ((node.Header & 0x200) == 0)
        {
            frame = LoadMatrix(frame, caller, true, false);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, translated, null), false);
            frame = Publish(frame, registers);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            if ((node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
            }
        }
        rotation = rotation with { X = unchecked(x + basis.X), Y = unchecked(y + basis.Y), Z = unchecked(z + basis.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleEffectArena context = Put287(Put287(Put287(Put287(Put287(Context(frame, temporary, 108), 0, geometry), 4, vertices), 12, unchecked((uint)(int)node.Scalar36)), 0x18, unchecked((uint)(int)Word287(caller40, 0x20))), 0x1C, flags);
        if (node.Scalar36 != 0)
        {
            context = Put287(context, 8, node.Color32);
        }
        byte phase = caller40.Read(35, 1)[0];
        if (phase != 0 && node.GeometryIndex <= 1)
        {
            foreach (int offset in new[] { 0x24, 0x26 })
            {
                context = PutWord287(context, offset, 0);
            }
            foreach (int offset in new[] { 0x28, 0x2A, 0x2C, 0x30 })
            {
                context = PutWord287(context, offset, 256);
            }
            context = PutWord287(context, 0x2E, node.GeometryIndex == 0 && caller40.Read(34, 1)[0] == 0 ? 0 : 128);
            context = PutWord287(PutWord287(context, 0x32, 128), 0x22, phase);
        }
        else
        {
            context = PutWord287(context, 0x22, 0);
        }
        context = PutWord287(context, 0x20, 0);
        frame = PublishContext(frame, temporary, context, 108);
        NativeBattleEffectArena PackedRow(int offset) => morph ? ReadBytes(frame, checked(vertices + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
        (NativeBattleVector Vertex, short? W) ReadRow(int offset)
        {
            NativeBattleEffectArena row = PackedRow(offset);
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            if (!known.Take(6).All(value => value))
            {
                throw new NotSupportedException("The selected programme vertex requires its current XYZ bytes.");
            }
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : (short?)null);
        }
        NativeBattleWindowedMeshContext108Step draw = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, ReadRow, readSelectedPackedVertex: PackedRow);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context108, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect287DrawBatch(draw.Batch, 0x2556374));
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
}
