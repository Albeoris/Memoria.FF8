using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshModelALabels(NativeBattleEffectArena Arena, int LabelEndOffset);
public sealed record NativeBattleGilgameshModelAReveal(NativeBattleGfGeometryBatch Packets, int LabelEndOffset);

/// <summary>5964B0's radial WORD labels and596220's exact common-heap FT3/FT4 prefix, culling and allocation order.</summary>
public static class NativeBattleGilgameshModelAPackets
{
    public static NativeBattleGilgameshModelALabels SetupLabels5964B0(NativeBattleRawMesh mesh, NativeBattleEffectArena arena, int worldOffset, int labelOffset)
    {
        RequirePlain(mesh);
        foreach (NativeBattleRawPolygon polygon in Ordered(mesh))
        {
            int corners = polygon.Quad ? 4 : 3;
            int x = 0;
            int z = 0;
            for (int corner = 0; corner < corners; corner++)
            {
                NativeBattleVector point = Point(arena, worldOffset, Index(polygon, corner));
                x += point.X;
                z += point.Z;
            }
            x /= corners;
            z /= corners;
            int radius = NativeWorldVectorMagnitude.Truncate(x, 0, z);
            int delay = unchecked(20 * radius) / 3000;
            ushort previous = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(labelOffset, 2));
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)(previous | (ushort)delay)));
            arena = arena.Write(labelOffset, bytes);
            labelOffset = checked(labelOffset + 2);
        }
        return new(arena, labelOffset);
    }

    public static NativeBattleGilgameshModelAReveal Emit596220(NativeBattleRawMesh mesh, NativeBattleEffectArena arena, int worldOffset, int labelOffset, int relativePhase, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint modelColor, NativeBattleScriptedGraphicsMemory? registers)
    {
        RequirePlain(mesh);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        foreach (NativeBattleRawPolygon polygon in Ordered(mesh))
        {
            ushort label = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(labelOffset, 2));
            labelOffset = checked(labelOffset + 2);
            if (relativePhase < (label & 0x3FFF))
            {
                continue;
            }
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[polygon.Quad ? 4 : 3];
            for (int corner = 0; corner < 3; corner++)
            {
                projected[corner] = NativeBattleModuleProjection.Project45FE10(worldView, Point(arena, worldOffset, Index(polygon, corner)), projection);
            }
            int length = polygon.Quad ? 40 : 32;
            byte[] bytes = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            Store(0, polygon.Quad ? 0x09000000u : 0x07000000u);
            uint uv1 = BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(12));
            Store(4, modelColor | (uv1 & 0x02000000) | (polygon.Quad ? 0x2C000000u : 0x24000000u));
            Store(12, BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(8)));
            Store(20, uv1 & 0x01FFFFFF);
            if (((projected[0].Flags | projected[1].Flags | projected[2].Flags) & 0x60000) != 0 || !NativeBattleGeometryPackets.Accept510680(projected[0].Screen, projected[1].Screen, projected[2].Screen))
            {
                attempts.Add(new(allocation, bytes.ToImmutableArray(), mask.ToImmutableArray(), null));
                continue;
            }
            Store(8, projected[0].Screen);
            Store(16, projected[1].Screen);
            Store(24, projected[2].Screen);
            if (polygon.Quad)
            {
                NativeBattleScriptedGraphicsMemory current = registers ?? throw new NotSupportedException("Native596220 requires its actual current DQA/DQB registers.");
                projected[3] = NativeBattleModuleProjection.Project45FAA0(worldView, Point(arena, worldOffset, Index(polygon, 3)), projection, current.Dqa, current.Dqb);
                Store(28, BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(16)));
                Store(32, projected[3].Screen);
                Store(36, unchecked((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(polygon.Bytes.AsSpan().Slice(18))));
            }
            else
            {
                Store(28, unchecked((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(polygon.Bytes.AsSpan().Slice(6))));
            }
            uint sum = (uint)projected.Sum(vertex => (int)vertex.Depth);
            short zsf = (polygon.Quad ? registers?.Zsf4 : registers?.Zsf3) ?? throw new NotSupportedException("Native596220 requires its actual current selected ZSF register.");
            uint depth = unchecked(sum * (uint)(int)zsf) >> 12;
            int bucket = unchecked(17 + (int)(depth >> 2));
            ImmutableArray<int> precision = polygon.Quad ? projected.Select(vertex => (int)vertex.Depth).ToImmutableArray() : [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0];
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(bucket, words, precision) { PhysicalWriteMask = mask.ToImmutableArray() };
            attempts.Add(new(allocation, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            allocation = checked(allocation + length);

            void Store(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }
        }
        return new(new(attempts.ToImmutable(), allocation), labelOffset);
    }

    private static IEnumerable<NativeBattleRawPolygon> Ordered(NativeBattleRawMesh mesh) => mesh.Polygons.Where(polygon => !polygon.Quad).Concat(mesh.Polygons.Where(polygon => polygon.Quad));
    private static int Index(NativeBattleRawPolygon polygon, int corner) => BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF;

    private static NativeBattleVector Point(NativeBattleEffectArena arena, int worldOffset, int index)
    {
        byte[] bytes = arena.Read(checked(worldOffset + index * 8), 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
    }

    private static void RequirePlain(NativeBattleRawMesh mesh)
    {
        mesh.Validate();
        if (mesh.Polygons.Any(polygon => polygon.Bytes.Length != (polygon.Quad ? 20 : 16)))
        {
            throw new NotSupportedException("Native Gilgamesh radial reveal requires its original plain FT3/FT4 groups.");
        }
    }
}


