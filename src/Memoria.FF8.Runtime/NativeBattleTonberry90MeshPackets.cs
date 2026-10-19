using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleTonberry90MeshPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch764540(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEsunaMeshContext context, NativeBattleSummonColors? colors = null, Func<int, NativeBattleVector>? readSelectedVertex = null, Action<uint>? observeFirstThreeFlags = null)
    {
        RequireContext(context);
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, readCorner, offset) => EmitFace764540(group, face, readCorner, context, colors, offset), observeFirstThreeFlags);
    }

    public static NativeBattleGfGeometryBatch EmitFace764540(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEsunaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        RequireContext(context);
        NativeBattleEffectMeshPacketContext normalized = new(context.Flags, context.FarColor, context.Fade, null, null, 0, false, context.DepthBias, context.TableBaseBucket, context.DepthShift);
        switch (group)
        {
            case 0:
            case 1:
                NativeBattleGfGeometryAttempt flat = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(group == 1 ? 4 : 3, readCorner), group, new(context.Flags, context.FarColor, context.Fade, null, null, 0, context.DepthBias, context.TableBaseBucket, context.DepthShift), colors, byteOffset);
                return new([flat], flat.Primitive is null ? 0 : flat.PacketLength);
            case 4:
            case 5:
                return NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, group == 5, normalized, colors, byteOffset);
            case 2:
            case 3:
            case 6:
            case 7:
                NativeBattleGfGeometryBatch textured = NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, (group & 1) != 0, group >= 6, normalized, colors, byteOffset);
                if (textured.AllocationBytes == 0)
                {
                    return textured;
                }
                NativeBattleGfGeometryAttempt polygon = textured.Attempts.Single();
                NativeBattleGeometryPrimitive primitive = polygon.Primitive ?? throw new InvalidDataException("Native Tonberry accepted texture leaf requires its actual polygon link.");
                byte[] bytes = new byte[12];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x02000000);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xE1000020);
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 12).ToImmutableArray().SetItem(0, NativeBattleGfPacketByteState.Untouched).SetItem(1, NativeBattleGfPacketByteState.Untouched).SetItem(2, NativeBattleGfPacketByteState.Untouched);
                ImmutableArray<uint> words = [0x02000000, 0xE1000020, 0];
                NativeBattleGeometryPrimitive mode = new(primitive.Bucket, words, []) { PhysicalWriteMask = mask };
                NativeBattleGfGeometryAttempt modeAttempt = new(checked(byteOffset + textured.AllocationBytes), bytes.ToImmutableArray(), mask, mode);
                NativeBattleGfGeometryAttempt polygonLink = polygon with { WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, polygon.PacketLength).ToImmutableArray() };
                return new([polygon with { Primitive = null }, modeAttempt, polygonLink], checked(textured.AllocationBytes + 12));
            default:
                throw new NotSupportedException($"Native Tonberry mesh group {group} leaves its original eight-entry dispatcher.");
        }
    }

    private static void RequireContext(NativeBattleEsunaMeshContext context)
    {
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        if (context.UOffset != 0 || context.VOffset != 0 || context.WindowBefore != full || context.WindowAfter != full)
        {
            throw new NotSupportedException("Native Tonberry prepared mesh requires its actual zero UV offset and full texture windows.");
        }
    }

    private sealed class Corners(int count, Func<int, NativeBattleModuleProjectedVertex> read) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        private readonly NativeBattleModuleProjectedVertex?[] values = new NativeBattleModuleProjectedVertex?[count];
        public int Count => count;
        public NativeBattleModuleProjectedVertex this[int index]
        {
            get
            {
                if (values[index] is { } value)
                {
                    return value;
                }
                value = read(index);
                values[index] = value;
                return value;
            }
        }
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, count).Select(index => this[index]).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
