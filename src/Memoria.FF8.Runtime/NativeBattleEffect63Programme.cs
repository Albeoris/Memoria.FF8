using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect63Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect63Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect63Programme
{
    public static NativeBattleEffect63Frame Initialize(NativeBattleEffect63Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect63Resources resources)
    {
        NativeBattleGfAnimationWorkspace cache = new(ReadBytes(frame, workspace, clearBytes).ReadSnapshot(0, clearBytes).Data.ToImmutableArray());
        cache = cache.Initialize7016B0(resources.Act(source), clearBytes);
        return WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, cache.Data.AsSpan()), clearBytes);
    }

    public static NativeBattleEffect63Step Advance(NativeBattleEffect63Frame frame, uint workspace, int length, NativeBattleEffectArena caller52, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Current() => new(ReadBytes(frame, workspace, length).Read(0, length).ToImmutableArray());
        NativeBattleGfAnimationSource source = resources.Act(Current().ResourceAddress);
        NativeBattleGfAnimationAdvance result = Current().Advance701970(source, 0, (cache, node) =>
        {
            frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, cache.Data.AsSpan()), length);
            frame = DrawNode(frame, source, node, caller52, resources, inputs);
            return Current();
        });
        frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, result.Workspace.Data.AsSpan()), length);
        return new(frame, result.Remaining);
    }

    public static NativeBattleEffect63Frame DrawNode(NativeBattleEffect63Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller52, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
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
            throw new InvalidDataException("Effect63 requires its actual selected geometry shapes.");
        }
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = checked(geometry + 12 + (uint)(selected * count * 8));
        if (morph)
        {
            vertices = Dword63(caller52, 0x30);
            NativeBattleGeometryRegisterStep weighted = Registers(frame);
            for (int vertex = 0; vertex < count; vertex++)
            {
                weighted = Weight(weighted, Shape(node.Word40, vertex), 4096 - node.Scalar38, false);
                weighted = Weight(weighted, Shape(node.Word42, vertex), node.Scalar38, true);
                NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear();
                for (int axis = 0; axis < 3; axis++)
                {
                    xyz = PutWord63(xyz, axis * 2, weighted.Colors.Ir[axis]!.Value);
                }
                frame = WriteBytes(frame, checked(vertices + (uint)(vertex * 8)), xyz, 6);
            }
            frame = Publish(frame, weighted);
        }
        NativeBattlePackedMatrix32 caller = Packed(caller52);
        NativeBattleActionMatrix basis = caller.ReadMatrix();
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        frame = LoadMatrix(frame, caller, true, false);
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, node.Vector8, null);
        registers = NativeBattleGeometry.Transform(registers, false);
        frame = Publish(frame, registers);
        if ((node.Header & 0x8000) == 0)
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        rotation = rotation with { X = unchecked(registers.Colors.Mac[0]!.Value + basis.X), Y = unchecked(registers.Colors.Mac[1]!.Value + basis.Y), Z = unchecked(registers.Colors.Mac[2]!.Value + basis.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : ScaleColumns(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Put63(Put63(Put63(Put63(Context(frame, temporary, 108), 0, geometry), 4, vertices), 12, unchecked((uint)(int)node.Scalar36)), 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        context = Put63(context, 0x18, unchecked((uint)(int)Word63(caller52, 0x24)));
        if (node.Scalar36 != 0)
        {
            context = Put63(context, 8, node.Color32);
        }
        foreach (int offset in new[] { 0x20, 0x22, 0x24, 0x26 })
        {
            context = PutWord63(context, offset, 0);
        }
        context = PutWord63(PutWord63(context, 0x28, 256), 0x2A, 256);
        uint selectors = Dword63(caller52, 0x20);
        if (resources.ReadStatic(checked(selectors + (uint)node.GeometryIndex), 1)[0] == 0x31)
        {
            context = PutWord63(PutWord63(PutWord63(PutWord63(context, 0x2C, 0), 0x2E, 0), 0x30, 256), 0x32, 128);
            context = PutWord63(context, 0x22, caller52.Read(0x26, 1)[0] & 0x7F);
        }
        NativeBattleEffectArena Row(int offset) => morph ? ReadBytes(frame, checked(vertices + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
        frame = DrawWindow108(frame, temporary, context, mesh, count, Row, inputs);
        return Release(frame, 108);

        NativeBattleVector Shape(int shape, int vertex)
        {
            int offset = checked((int)(geometry - source.Address) + 12 + (shape * count + vertex) * 8);
            ReadOnlySpan<byte> row = source.Data.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
        }
        NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep step, NativeBattleVector value, int factor, bool accumulate)
        {
            step = NativeBattleGeometry.LoadIrColumn(step with { Colors = step.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)value.X), unchecked((ushort)value.Y), unchecked((ushort)value.Z));
            return NativeBattleRaldo19BallRendering.Weighted(step, accumulate, inputs.Colors);
        }
    }

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena source)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}
