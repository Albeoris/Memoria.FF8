using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect140Fields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect140Programme
{
    public static NativeBattleEffect140Frame Initialize(NativeBattleEffect140Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect140Resources resources)
    {
        NativeBattleGfAnimationWorkspace initialized = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, clearBytes).Initialize7016B0(resources.Act(source), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, initialized);
        return frame;
    }

    public static NativeBattleEffect140Step Advance(NativeBattleEffect140Frame frame, uint workspace, int workspaceLength, NativeBattleEffectArena caller40, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        uint sourceAddress = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, 4).ResourceAddress;
        int required = resources.WorkspaceLength(sourceAddress);
        if (required > workspaceLength)
        {
            throw new InvalidDataException("Effect140 programme cache leaves its actual physical workspace.");
        }
        NativeBattleGfAnimationWorkspace Current() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, required);
        NativeBattleGfAnimationSource source = resources.Act(sourceAddress);
        NativeBattleGfAnimationAdvance advanced = Current().Advance701970(source, unchecked((int)frame.State.ReadDword(Pause)), (current, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, current);
            frame = DrawNode(frame, source, node, caller40, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattleEffect140VisualIdentity.Workspace(frame, workspace, sourceAddress, node.ResourceOffset) : null);
            return Current();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, advanced.Workspace);
        return new(frame, advanced.Remaining);
    }

    public static NativeBattleEffect140Frame DrawNode(NativeBattleEffect140Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller40, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometry);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (count is < 1 or > 96 || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect140 node requires its actual selected geometry shapes.");
        }
        (frame, uint temporary) = Allocate(frame, 108);
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int shape = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = geometry + 12u + (uint)(shape * count * 8);
        if (morph)
        {
            vertices = U(caller40, 0x24);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int vertex = 0; vertex < count; vertex++)
            {
                registers = Weight(registers, Shape(node.Word40, vertex), 4096 - node.Scalar38, false);
                registers = Weight(registers, Shape(node.Word42, vertex), node.Scalar38, true);
                NativeBattleEffectArena row = NativeBattleEffectArena.Clear();
                for (int axis = 0; axis < 3; axis++)
                {
                    row = Word(row, axis * 2, registers.Colors.Ir[axis]!.Value);
                }
                frame = WriteBytes(frame, vertices + (uint)(vertex * 8), row, 6);
            }
            frame = Publish(frame, registers);
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x1000) != 0 || (node.Header & 0x200) == 0)
        {
            NativeBattleEffectArena basis = (node.Header & 0x1000) != 0 ? ReadBytes(frame, 0x2517B30, 32) : caller40;
            NativeBattleActionMatrix parent = Rotation(basis);
            frame = LoadMatrix(frame, Packed(basis, 0), true, false);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, node.Vector8, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = Publish(frame, registers);
            rotation = NativeBattleGfMatrices.Multiply56C090(parent, rotation);
        }
        rotation = rotation with { X = unchecked(x + I(caller40, 20)), Y = unchecked(y + I(caller40, 24)), Z = unchecked(z + I(caller40, 28)) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Put(Put(Put(Put(Put(Context(frame, temporary, 108), 0, geometry), 4, vertices), 12, unchecked((uint)(int)node.Scalar36)), 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u), 0x18, unchecked((uint)(int)S(caller40, 0x20)));
        if (node.Scalar36 != 0)
        {
            context = Put(context, 8, node.Color32);
        }
        if ((node.Header & 0x3000000) != 0)
        {
            context = Word(Word(Word(Word(context, 0x24, 0), 0x26, 0), 0x28, 256), 0x2A, 256);
            bool vertical = (node.Header & 0x1000000) != 0;
            context = Word(Word(context, 0x20, vertical ? 0 : (-caller40.Read(0x22, 1)[0]) & 127), 0x22, vertical ? caller40.Read(0x22, 1)[0] & 127 : 0);
            context = Word(Word(Word(Word(context, 0x2C, 0), 0x2E, vertical ? 128 : 0), 0x30, vertical ? 256 : 128), 0x32, vertical ? 128 : 256);
        }
        else
        {
            context = Word(Word(context, 0x20, 0), 0x22, 0);
        }
        NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, 96, offset =>
        {
            NativeBattleEffectArena row = morph ? ReadBytes(frame, vertices + (uint)offset, 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            NativeBattleVector point = new(S(row, 0), S(row, 2), S(row, 4));
            return (point, known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : (short?)null);
        }, pilotIdentity: pilotIdentity);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect140DrawBatch(drawn.Batch, Cursor));
        return Release(frame, 108);

        NativeBattleVector Shape(int selected, int index)
        {
            int offset = checked((int)(geometry - source.Address) + 12 + (selected * count + index) * 8);
            return new(BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(offset + 4)));
        }
        NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector point, int factor, bool accumulate)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)point.X), unchecked((ushort)point.Y), unchecked((ushort)point.Z));
            return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors);
        }
    }

    private static NativeBattleActionMatrix Rotation(NativeBattleEffectArena arena) => new(Enumerable.Range(0, 9).Select(index => S(arena, index * 2)).ToImmutableArray(), 0, 0, 0, 4096);
}
