using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleStickyWeb153Drawing
{
    public static NativeBattleGfGeometryBatch Line(NativeBattleVector first, NativeBattleVector second, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short zsf3, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleModuleProjectedVertex a = NativeBattleModuleProjection.Project45FE10(view, first, projection);
        NativeBattleModuleProjectedVertex b = NativeBattleModuleProjection.Project45FE10(view, second, projection);
        int bucket = 17 + (unchecked((int)((uint)((a.Depth + b.Depth + b.Depth) * zsf3) >> 12)) >> 2);
        return new([Packet([0x04000000, 0x52808080, a.Screen, 0x808080, b.Screen], [a.Depth, b.Depth, b.Depth, 0], bucket, 0, pilotIdentity is { } identity ? Capture(identity, [first, second], view, projection, [a.Depth, b.Depth, b.Depth, 0]) : null)], 20);
    }

    public static NativeBattleGfGeometryBatch Strips(ImmutableArray<NativeBattleVector> first, ImmutableArray<NativeBattleVector> second, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short zsf4, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int cursor = 0;
        uint previous = 0x3A203040;
        for (int segment = 0; segment < 14; segment++)
        {
            NativeBattleModuleProjectedVertex a = NativeBattleModuleProjection.Project45FE10(view, first[segment], projection);
            NativeBattleModuleProjectedVertex b = NativeBattleModuleProjection.Project45FE10(view, first[segment + 1], projection);
            NativeBattleModuleProjectedVertex c = NativeBattleModuleProjection.Project45FE10(view, second[segment], projection);
            byte[] tag = new byte[36];
            BinaryPrimitives.WriteUInt32LittleEndian(tag, 0x08000000);
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[36];
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, 0, 4);
            if (((a.Flags | b.Flags | c.Flags) & 0x60000) != 0)
            {
                attempts.Add(new(cursor, tag.ToImmutableArray(), mask.ToImmutableArray(), null));
                continue;
            }
            NativeBattleModuleProjectedVertex d = NativeBattleModuleProjection.Project45FE10(view, second[segment + 1], projection);
            int bucket = 17 + (unchecked((int)((uint)((a.Depth + b.Depth + c.Depth + d.Depth) * zsf4) >> 12)) >> 2);
            uint next = colors.Interpolate(0x3A203040, segment * 4096 / 14, 0);
            ImmutableArray<int> depths = [a.Depth, b.Depth, c.Depth, d.Depth];
            attempts.Add(Packet([0x08000000, previous, a.Screen, next, b.Screen, previous, c.Screen, next, d.Screen], depths, bucket, cursor, pilotIdentity is { } identity ? Capture(identity with { Primitive = unchecked(identity.Primitive * 397 + segment * 2) }, [first[segment], first[segment + 1], second[segment], second[segment + 1]], view, projection, depths) : null));
            attempts.Add(Packet([0x08000000, 0x3A000000, a.Screen, 0x3A000000, b.Screen, previous, c.Screen, next, d.Screen], depths, bucket, cursor + 36, pilotIdentity is { } layer ? Capture(layer with { Primitive = unchecked(layer.Primitive * 397 + segment * 2 + 1) }, [first[segment], first[segment + 1], second[segment], second[segment + 1]], view, projection, depths) : null));
            previous = next;
            cursor += 72;
        }
        return new(attempts.ToImmutable(), cursor + 36);
    }

    private static NativeBattlePilotVisualGeometry Capture(NativeBattlePilotVisualIdentity identity, IReadOnlyList<NativeBattleVector> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, ImmutableArray<int> depths)
    {
        System.Numerics.Vector3 World(int corner) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, vertices[corner]), projection);
        return new(identity, World(0), World(1), vertices.Count > 2 ? World(2) : default, vertices.Count == 4 ? World(3) : default, checked((byte)vertices.Count)) { SourceDepths = new(depths[0], depths[1], depths[2], depths[3]), UseSourceDepths = true };
    }

    private static NativeBattleGfGeometryAttempt Packet(ImmutableArray<uint> words, ImmutableArray<int> depths, int bucket, int offset, NativeBattlePilotVisualGeometry? geometry = null)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), new(bucket, words, depths) { PilotGeometry = geometry });
    }

    public static NativeBattleGfGeometryBatch Clipped(NativeBattleStickyWeb153Resources resources, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int height, short zsf3, short zsf4, short dqa, int dqb, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<byte> model = resources.Web.Bytes;
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan());
        if (BinaryPrimitives.ReadUInt32LittleEndian(model.AsSpan().Slice(cursor)) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(model.AsSpan().Slice(cursor + 4)) != 0)
        {
            throw new InvalidDataException("Native Sticky Web requires its original untextured-empty primitive groups.");
        }
        cursor += 8;
        NativeBattleShivaPlane plane = NativeBattleShivaPlaneClipping.CreatePlane5C3130(new(0, unchecked((short)height), 0), new(0, 4096, 0));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 2; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan().Slice(cursor));
            cursor += 4;
            int stride = group == 0 ? 20 : 24;
            for (int face = 0; face < count; face++)
            {
                int record = cursor;
                uint material0 = Dword(record + 12);
                uint material1 = Dword(record + 16);
                ushort thirdUv = Word(record + (group == 0 ? 10 : 20));
                NativeBattleShivaClipVertex first = Vertex(Word(record + 4), unchecked((ushort)material0));
                NativeBattleShivaClipVertex second = Vertex(Word(record + 6), unchecked((ushort)material1));
                NativeBattleShivaClipVertex third = Vertex(Word(record + 8), thirdUv);
                uint flags = NativeBattleModuleProjection.Project45FE10(matrix, first.Position, projection).Flags | NativeBattleModuleProjection.Project45FE10(matrix, second.Position, projection).Flags | NativeBattleModuleProjection.Project45FE10(matrix, third.Position, projection).Flags;
                if ((flags & 0x60000) == 0)
                {
                    Emit([first, second, third], 0);
                    if (group == 1)
                    {
                        Emit([Vertex(Word(record + 10), Word(record + 22)), second, third], 1);
                    }
                }
                cursor += stride;

                void Emit(ImmutableArray<NativeBattleShivaClipVertex> input, int half)
                {
                    ImmutableArray<NativeBattleShivaClipVertex> clipped = NativeBattleShivaPlaneClipping.Clip5C34B0(input, plane, resources.ClipSelectors);
                    if (!clipped.IsEmpty)
                    {
                        NativeBattleGfGeometryAttempt attempt = NativeBattleShivaPlaneClipping.Emit5C3410(clipped, matrix, projection, Dword(record) & 0x2FFFFFFu, material0, material1, zsf3, zsf4, dqa, dqb, allocated);
                        if (pilotIdentity is { } identity)
                        {
                            int topology = NativeBattleShivaVisualIdentity.ClipTopology(input.Select(vertex => vertex.Position), plane);
                            attempt = NativeBattleShivaVisualIdentity.Capture(attempt, identity with { Primitive = unchecked(((identity.Primitive * 397 + record) * 2 + half) * 31 + topology) }, clipped.Select(vertex => vertex.Position).ToArray(), matrix, projection);
                        }
                        attempts.Add(attempt);
                        allocated += attempt.PacketLength;
                    }
                }
            }
        }
        return new(attempts.ToImmutable(), allocated);

        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(model.AsSpan().Slice(offset));
        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(model.AsSpan().Slice(offset));
        NativeBattleShivaClipVertex Vertex(int index, ushort uv)
        {
            int offset = checked(8 + index * 4);
            return new(new(unchecked((short)Word(offset)), unchecked((short)Word(offset + 2)), unchecked((short)Word(offset + 4))), uv);
        }
    }
}
