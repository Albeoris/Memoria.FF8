using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect301Programme
{
    public static NativeBattleEffect301Frame Initialize(NativeBattleEffect301Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect301Resources resources)
    {
        NativeBattleGfAnimationWorkspace cache = new(ReadBytes(frame, workspace, clearBytes).ReadSnapshot(0, clearBytes).Data.ToImmutableArray());
        cache = cache.Initialize7016B0(resources.Act(source), clearBytes);
        return WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, cache.Data.AsSpan()), clearBytes);
    }

    public static NativeBattleEffect301Step Advance(NativeBattleEffect301Frame frame, uint workspace, int length, NativeBattleEffectArena caller16, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Current() => new(ReadBytes(frame, workspace, length).Read(0, length).ToImmutableArray());
        NativeBattleGfAnimationSource source = resources.Act(Current().ResourceAddress);
        NativeBattleGfAnimationAdvance result = Current().Advance701970(source, 0, (cache, node) =>
        {
            frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, cache.Data.AsSpan()), length);
            frame = DrawNode(frame, source, node, caller16, resources, inputs);
            return Current();
        });
        frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().Write(0, result.Workspace.Data.AsSpan()), length);
        return new(frame, result.Remaining);
    }

    public static NativeBattleEffect301Frame DrawNode(NativeBattleEffect301Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller16, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        uint address = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(address);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (count < 0 || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("The programme requires its actual selected geometry shape.");
        }
        if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
        {
            throw new NotSupportedException("The selected programme does not publish an intermediate morph.");
        }
        int shape = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = checked(address + 12 + (uint)(count * shape * 8));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = unchecked(node.Vector8.X + Word301(caller16, 0)), Y = unchecked(node.Vector8.Y + Word301(caller16, 2)), Z = unchecked(node.Vector8.Z + Word301(caller16, 4)) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        NativeBattleEffectArena context = Put301(Put301(Put301(Put301(Put301(Context(frame, temporary, 88), 0, address), 4, vertices), 12, unchecked((uint)(int)node.Scalar36)), 24, Dword301(caller16, 8)), 28, node.Scalar36 == 0 ? 0x2000u : 0x20C0u);
        if (node.Scalar36 != 0)
        {
            context = Put301(context, 8, node.Color32);
        }
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleVector Point(int offset)
        {
            ReadOnlySpan<byte> row = source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
        }
        short? W(int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset + 6, 2));
        NativeBattleDefaultActMeshStep result = NativeBattleDefaultActMesh.Draw(frame.Memory, source, checked((int)(address - source.Address)), context, inputs.ReadProjection(frame), inputs.Colors, Point, W, 17, 2);
        frame = PublishContext(frame with { Memory = result.Memory }, temporary, result.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect301DrawBatch(result.Batch, 0x250773C));
        return Release(frame, 88);
    }
}
