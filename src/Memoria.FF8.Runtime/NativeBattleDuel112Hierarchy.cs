using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112HierarchyContext(uint FlagsAddress, uint MorphAddress, NativeBattleVector? Anchor, short? DepthOffset, NativeBattleActionMatrix? Basis);
public sealed record NativeBattleDuel112HierarchyStep(NativeBattleDuel112Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDuel112Hierarchy
{
    public static NativeBattleDuel112HierarchyStep Draw6CD280(NativeBattleDuel112Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleDuel112HierarchyContext context, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleDuel112Resources resources = frame.Memory.Duel112?.Resources ?? throw new NotSupportedException("Native Duel hierarchy requires its actual resource owner.");
        int index = unchecked((short)node.NodeIndex);
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(unchecked(context.FlagsAddress + (uint)(4 * index)), 4).AsSpan());
        NativeBattleVector scale = node.Vector24;
        if (unchecked((int)flags) < 0 || scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0));
        }
        uint relative = BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(unchecked(source.Address + 8u + (uint)(4 * unchecked((short)node.GeometryIndex))), 4).AsSpan());
        uint mesh = unchecked(source.Address + relative);
        int geometry = checked((int)((long)mesh - NativeBattleDuel112Resources.Address));
        int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Read(unchecked(mesh + 4), 4).AsSpan());
        if (count < 0 || count > resources.StaticData.Length / 8)
        {
            throw new InvalidDataException("Native Duel hierarchy has no complete vertex owner.");
        }
        uint selected;
        bool morphed = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        if (morphed)
        {
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(resources.StaticData, geometry, node.Word40, node.Word42, node.Scalar38);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel morph requires its actual arena owner.");
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, vertices[vertex].X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vertices[vertex].Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vertices[vertex].Z);
                arena = arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(checked(context.MorphAddress + 8u * (uint)vertex), 6), xyz);
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            selected = context.MorphAddress;
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = unchecked(mesh + 12u + (uint)(count * shape * 8));
        }
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(node.Vector16);
        if ((flags & 0x20000000) != 0)
        {
            matrix = matrix with { X = node.Vector8.X, Y = node.Vector8.Y, Z = node.Vector8.Z };
            matrix = NativeBattleGfMatrices.Compose56C2F0(context.Basis ?? throw new NotSupportedException("Native Duel hierarchy requires the caller's actual basis matrix."), matrix);
        }
        else
        {
            NativeBattleVector anchor = context.Anchor ?? throw new NotSupportedException("Native Duel hierarchy requires its actual caller anchor.");
            matrix = matrix with { X = anchor.X + node.Vector8.X, Y = anchor.Y + node.Vector8.Y, Z = anchor.Z + node.Vector8.Z };
            if ((flags & 0x10000000) != 0)
            {
                (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, matrix.X, matrix.Y, matrix.Z);
                matrix = matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
            }
            else
            {
                matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            }
        }
        if ((flags & 0x40000000) != 0)
        {
            short depth = context.DepthOffset ?? throw new NotSupportedException("Native Duel hierarchy requires the actual caller depth WORD.");
            var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(matrix.X, matrix.Y, matrix.Z);
            matrix = matrix with { X = unchecked(matrix.X - (unchecked(normal.X * depth) >> 13)), Y = unchecked(matrix.Y - (unchecked(normal.Y * depth) >> 13)), Z = unchecked(matrix.Z - (unchecked(normal.Z * depth) >> 13)) };
        }
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        uint renderFlags = (flags & 0xDFFFu) | 0x2000u;
        uint? far = null;
        if (node.Scalar36 != 0)
        {
            renderFlags |= 0xC0;
            far = node.Color32;
        }
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(resources.StaticData, geometry, default, matrix, projection, new(renderFlags, far, node.Scalar36, null, null, null, 17, 2), colors, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset / 8 >= count)
            {
                throw new InvalidDataException("Native Duel selected vertex leaves its actual shape owner.");
            }
            uint address = unchecked(selected + (uint)offset);
            byte[] xyz = morphed ? frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, 6), 6) : resources.Read(address, 6).ToArray();
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
        }, pilotIdentity: pilotIdentity);
        return new(frame, batch);
    }
}
