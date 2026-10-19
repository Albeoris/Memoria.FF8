using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274HierarchyContext(NativeBattleActionMatrix Matrix, NativeBattleActionMatrix? Billboard, int? DepthBias, short DrawMode, ushort? VerticalOffset, uint? MorphAddress);
public sealed record NativeBattleDuel274HierarchyStep(NativeBattleDuel274Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDuel274Hierarchy
{
    public static NativeBattleDuel274HierarchyStep Draw68A400(NativeBattleDuel274Frame frame, NativeBattleDuel274Resources resources, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleDuel274HierarchyContext context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0));
        }
        int geometryIndex = unchecked((short)node.GeometryIndex);
        uint relative = BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(unchecked(source.Address + 8u + (uint)(geometryIndex * 4)), 4).AsSpan());
        uint mesh = unchecked(source.Address + relative);
        int geometry = checked((int)((long)mesh - NativeBattleDuel274Resources.Address));
        int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Read(unchecked(mesh + 4), 4).AsSpan());
        if (count < 0 || count > resources.StaticData.Length / 8)
        {
            throw new InvalidDataException("Native Duel hierarchy has no bounded original vertex owner.");
        }
        bool morphed = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint selected;
        if (morphed)
        {
            uint destination = context.MorphAddress ?? throw new NotSupportedException("Native Duel shape interpolation requires its actual context morph pointer.");
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(resources.StaticData, geometry, node.Word40, node.Word42, node.Scalar38);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel interpolation requires its actual destination arena.");
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, vertices[vertex].X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vertices[vertex].Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vertices[vertex].Z);
                arena = arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(checked(destination + (uint)vertex * 8), 6), xyz);
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            selected = destination;
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = unchecked(mesh + 12u + (uint)(count * shape * 8));
        }
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose6C53A0(node, context.Matrix, context.Billboard);
        uint flags = (node.Header & 0x2000) != 0 ? 0x203Cu : 0x2030u;
        bool fade = node.Scalar36 != 0;
        if (fade)
        {
            flags |= 0xC0;
        }
        ImmutableArray<byte> meshBytes = resources.Read(mesh, checked((int)(NativeBattleDuel274Resources.EndAddress - mesh)));
        NativeBattleGfGeometryBatch batch;
        if (context.DrawMode == 1)
        {
            batch = NativeBattleDuel274MeshPackets.EmitBatch68BD50(meshBytes, matrix, projection, node.Scalar36, flags, ReadSelected);
        }
        else if (context.DrawMode == 2 && geometryIndex == 0)
        {
            ushort vertical = context.VerticalOffset ?? throw new NotSupportedException("Native Duel textured hierarchy requires its actual context WORD38.");
            batch = NativeBattleDuel274MeshPackets.EmitBatch68C320(meshBytes, matrix, projection, vertical, ReadSelected);
        }
        else
        {
            int depthBias = context.DepthBias ?? throw new NotSupportedException("Native Duel geometry requires its actual context DWORD20.");
            NativeBattleGfGeometryContext operands = new(flags, fade ? node.Color32 : 0, node.Scalar36, null, null, depthBias, 17, 2);
            batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(resources.StaticData, geometry, default, matrix, projection, operands, colors, ReadSelected);
        }
        return new(frame, batch);

        NativeBattleVector ReadSelected(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset >= count * 8)
            {
                throw new InvalidDataException("Native Duel hierarchy selected a vertex outside its actual shape.");
            }
            byte[] bytes = morphed ? (frame.Memory.Arena ?? throw new NotSupportedException("Native Duel morph publication disappeared.")).Read(NativeBattleWaterExecutionFields.ArenaOffset(checked(selected + (uint)offset), 6), 6) : resources.Read(unchecked(selected + (uint)offset), 6).ToArray();
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
    }
}
