using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect227Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect227Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect227Programme
{
    public static NativeBattleEffect227Frame Initialize(NativeBattleEffect227Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect227Resources resources)
    {
        NativeBattleGfAnimationWorkspace cache = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, clearBytes);
        cache = cache.Initialize7016B0(resources.Act(source), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, cache);
        return frame;
    }

    public static NativeBattleEffect227Step Advance(NativeBattleEffect227Frame frame, uint workspace, int length, NativeBattleEffectArena caller16, NativeBattleEffect227Resources resources, NativeBattleEffect227Inputs inputs)
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

    public static NativeBattleEffect227Frame DrawNode(NativeBattleEffect227Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller16, NativeBattleEffect227Resources resources, NativeBattleEffect227Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometry);
        if (node.Word40 != 0 || node.Word42 != 0)
        {
            throw new NotSupportedException("The programme requires its selected unchanged shape.");
        }
        uint vertices = checked(geometry + 12);
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleVector caller = Vector(caller16);
        NativeBattleEffectArena local;
        if ((node.Header & 0x200) != 0)
        {
            local = NativeBattleEffectNodeContextMemory.Copy(caller16, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        }
        else
        {
            local = Store(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), new(unchecked((short)(caller.X + node.Vector8.X)), unchecked((short)(caller.Y + node.Vector8.Y)), unchecked((short)(caller.Z + node.Vector8.Z))));
        }
        frame = LoadMatrix(frame, view, true, false);
        (byte[] wBytes, bool[] wKnown) = local.ReadSnapshot(6, 2);
        short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(local), w), false);
        frame = Publish(frame, registers);
        int x = registers.Colors.Mac[0]!.Value;
        int y = registers.Colors.Mac[1]!.Value;
        int z = registers.Colors.Mac[2]!.Value;
        if ((node.Header & 0x200) != 0)
        {
            x = unchecked(x + node.Vector8.X);
            y = unchecked(y + node.Vector8.Y);
            z = unchecked(z + node.Vector8.Z);
        }
        else
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(view.ReadMatrix(), rotation);
        }
        NativeBattleActionMatrix camera = view.ReadMatrix();
        rotation = rotation with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            NativeBattleVector scale = node.Vector24;
            rotation = (node.Header & 0x100) != 0
                ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation }
                : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Put(Put(Put(Put(Put(Context(frame, temporary, 88), 0, geometry), 4, vertices), 12, unchecked((uint)(int)node.Scalar36)), 0x18, Dword(caller16, 8)), 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            context = Put(context, 8, node.Color32);
        }
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleVector ReadVertex(int offset)
        {
            ReadOnlySpan<byte> row = source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
        }
        short? ReadW(int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset + 6, 2));
        NativeBattleDefaultActMeshStep draw = NativeBattleDefaultActMesh.Draw(frame.Memory, source, checked((int)(geometry - source.Address)), context, inputs.ReadProjection(frame), inputs.Colors, ReadVertex, ReadW, 17, 2, pilotIdentity: frame.PresentationIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 65537 + node.ResourceOffset), ResourceGeneration = geometry } : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect227DrawBatch(draw.Batch, CursorGlobal));
        return Release(frame, 88);
    }
}
