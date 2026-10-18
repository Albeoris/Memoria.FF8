using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>595550/595AA0/595C80's original triangle-plane routing and masked FT stores.</summary>
public static class NativeBattleGilgameshSwordPackets
{
    public static NativeBattleGfGeometryBatch Emit595550(NativeBattleRawMesh mesh, NativeBattleEffectArena arena, int worldOffset, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint modelColor, NativeBattleScriptedGraphicsMemory? registers, int depthShift = 2)
    {
        mesh.Validate();
        if (mesh.Polygons.Any(polygon => polygon.Bytes.Length != (polygon.Quad ? 20 : 16)))
        {
            throw new NotSupportedException("Native sword clipping requires its original plain FT3/FT4 face groups.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons.Where(face => !face.Quad).Concat(mesh.Polygons.Where(face => face.Quad)))
        {
            NativeBattleGilgameshSwordClipVertex first = Vertex(0, 8);
            NativeBattleGilgameshSwordClipVertex second = Vertex(1, 12);
            NativeBattleGilgameshSwordClipVertex third = Vertex(2, polygon.Quad ? 16 : 6);
            uint uv0 = BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(8));
            uint uv1 = BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(12));
            if (!ClipAndEmit(first, second, third, true))
            {
                continue;
            }
            if (polygon.Quad)
            {
                NativeBattleGilgameshSwordClipVertex fourth = Vertex(3, 18);
                ClipAndEmit(fourth, second, third, false);
            }

            NativeBattleGilgameshSwordClipVertex Vertex(int corner, int uvOffset)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF;
                byte[] bytes = arena.Read(checked(worldOffset + index * 8), 6);
                NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
                return new(point, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(uvOffset)));
            }

            bool ClipAndEmit(NativeBattleGilgameshSwordClipVertex a, NativeBattleGilgameshSwordClipVertex b, NativeBattleGilgameshSwordClipVertex c, bool firstGate)
            {
                NativeBattleModuleProjectedVertex p0 = NativeBattleModuleProjection.Project45FE10(worldView, a.Position, projection);
                NativeBattleModuleProjectedVertex p1 = NativeBattleModuleProjection.Project45FE10(worldView, b.Position, projection);
                NativeBattleModuleProjectedVertex p2 = NativeBattleModuleProjection.Project45FE10(worldView, c.Position, projection);
                if (firstGate && (((p0.Flags | p1.Flags | p2.Flags) & 0x60000) != 0 || !NativeBattleGeometryPackets.Accept510680(p0.Screen, p1.Screen, p2.Screen)))
                {
                    return false;
                }
                ImmutableArray<NativeBattleGilgameshSwordClipVertex> chosen = NativeBattleGilgameshSwordClip.Clip595B40(a, b, c);
                if (!chosen.IsEmpty)
                {
                    NativeBattleGfGeometryAttempt attempt = EmitChosen(chosen, uv0, uv1, modelColor, worldView, projection, registers, depthShift, allocation);
                    attempts.Add(attempt);
                    allocation = checked(allocation + attempt.PacketLength);
                }
                return true;
            }
        }
        return new(attempts.ToImmutable(), allocation);
    }

    public static NativeBattleGfGeometryAttempt EmitChosen(ImmutableArray<NativeBattleGilgameshSwordClipVertex> points, uint uv0Clut, uint uv1Page, uint modelColor, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, int depthShift = 2, int byteOffset = 0)
    {
        if (points.IsDefault || points.Length is not (3 or 4))
        {
            throw new InvalidDataException("Native sword FT emitters require their exact three or four selected clip points.");
        }
        bool quad = points.Length == 4;
        NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[points.Length];
        for (int corner = 0; corner < 3; corner++)
        {
            projected[corner] = NativeBattleModuleProjection.Project45FE10(worldView, points[corner].Position, projection);
        }
        if (quad)
        {
            NativeBattleScriptedGraphicsMemory current = registers ?? throw new NotSupportedException("Native595C80 requires its current DQA/DQB registers.");
            projected[3] = NativeBattleModuleProjection.Project45FAA0(worldView, points[3].Position, projection, current.Dqa, current.Dqb);
        }
        byte[] bytes = new byte[quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        Store(0, quad ? 0x09000000u : 0x07000000u);
        uint command = modelColor | (quad ? 0x2C000000u : 0x24000000u);
        Store(4, command);
        Store(12, uv0Clut);
        Store(20, uv1Page);
        Store(8, projected[0].Screen);
        Store(16, projected[1].Screen);
        Store(24, projected[2].Screen);
        StoreWord(12, points[0].Uv);
        StoreWord(20, points[1].Uv);
        StoreWord(28, points[2].Uv);
        if (quad)
        {
            Store(32, projected[3].Screen);
            StoreWord(36, points[3].Uv);
        }
        short zsf = (quad ? registers?.Zsf4 : registers?.Zsf3) ?? throw new NotSupportedException("Native sword FT emission requires its actual selected ZSF register.");
        uint sum = (uint)projected.Sum(vertex => (int)vertex.Depth);
        uint depth = unchecked(sum * (uint)(int)zsf) >> 12;
        int bucket = unchecked(17 + (unchecked((int)depth) >> (depthShift & 31)));
        uint commandClass = (command >> 24) & 0xFC;
        ImmutableArray<int> precision;
        if (commandClass < 0x20 || commandClass > 0x3C)
        {
            precision = [0, 0, 0, 0];
        }
        else if ((commandClass & 8) != 0)
        {
            if (!quad)
            {
                throw new NotSupportedException("Native sword color high BYTE reclassifies FT3 as a quad and requires its unavailable prior SZ0 owner.");
            }
            precision = projected.Select(vertex => (int)vertex.Depth).ToImmutableArray();
        }
        else
        {
            precision = [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0];
        }
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, precision) { PhysicalWriteMask = mask.ToImmutableArray() };
        return new(byteOffset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);

        void Store(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }

        void StoreWord(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
        }
    }
}
