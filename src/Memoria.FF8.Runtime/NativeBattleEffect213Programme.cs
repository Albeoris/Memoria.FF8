using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect213Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect213Programme
{
    public static NativeBattleEffect213Frame Initialize(NativeBattleEffect213Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect213Resources resources)
    {
        NativeBattleGfAnimationWorkspace result = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, clearBytes);
        result = result.Initialize7016B0(resources.Act(source), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, result);
        return frame;
    }

    public static NativeBattleEffect213Step Advance(NativeBattleEffect213Frame frame, uint workspace, int length, NativeBattlePackedMatrix32 matrix, uint flagsAddress, uint morphAddress, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGfAnimationWorkspace Current() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, length);
        NativeBattleGfAnimationSource source = resources.Act(Current().ResourceAddress);
        NativeBattleGfAnimationAdvance result = Current().Advance701970(source, 0, (cache, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, cache);
            frame = DrawNode(frame, source, node, matrix, flagsAddress, morphAddress, resources, inputs, pilotIdentity.HasValue ? pilotIdentity.Value with { Primitive = node.ResourceOffset, ResourceGeneration = ((long)source.Address << 32) | 0x64L } : null);
            return Current();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, result.Workspace);
        return new(frame, result.Remaining);
    }

    public static NativeBattleEffect213Frame DrawNode(NativeBattleEffect213Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 matrix, uint flagsAddress, uint morphAddress, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
        if (node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("The programme shape leaves its selected geometry.");
        }
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint vertices = morph ? morphAddress : checked(geometry + 12 + (uint)((node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40) * count * 8));
        if (morph)
        {
            frame = Morph(frame, source, geometry, node.Word40, node.Word42, node.Scalar38, morphAddress, inputs);
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(node.Vector16) with { X = node.Vector8.X, Y = node.Vector8.Y, Z = node.Vector8.Z };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(matrix.ReadMatrix(), local);
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            composed = NativeBattlePoseMatrices.Scale56BEF0(composed, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        uint flags = (BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(flagsAddress + (uint)(node.NodeIndex * 4)), 4)) & 0xDFFF) | 0x2000;
        NativeBattleEffectArena context = Put(Put(Put(Context(frame, temporary, 88), 0, geometry), 4, vertices), 12, unchecked((uint)(int)node.Scalar36));
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
            context = Put(context, 8, node.Color32);
        }
        context = Put(context, 0x1C, flags);
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, vertices, offset =>
        {
            NativeBattleEffectArena row = morph ? ReadBytes(frame, vertices + checked((uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
            (byte[] bytes, bool[] known) = row.ReadSnapshot(6, 2);
            short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
            return (Vector(row), w);
        }, pilotIdentity: pilotIdentity);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect213DrawBatch(draw.Batch, CursorGlobal));
        return Release(frame, 88);
    }

    public static NativeBattleEffect213Frame Morph(NativeBattleEffect213Frame frame, NativeBattleGfAnimationSource source, uint geometry, int first, int second, int factor, uint destination, NativeBattleEffect213Inputs inputs)
    {
        int geometryOffset = checked((int)(geometry - source.Address));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometryOffset + 4));
        for (int vertex = 0; vertex < count; vertex++)
        {
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int part = 0; part < 2; part++)
            {
                int shape = part == 0 ? first : second;
                int offset = checked(geometryOffset + 12 + (shape * count + vertex) * 8);
                NativeBattleVector value = Vector(NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(offset, 8)));
                registers = Weight(registers, value, part == 0 ? 4096 - factor : factor, part != 0, inputs.Colors);
            }
            frame = Publish(frame, registers);
            uint address = checked(destination + (uint)(vertex * 8));
            frame = WriteBytes(frame, address, Store(ReadBytes(frame, address, 8), Ir(registers)), 8);
        }
        return frame;
    }
}
