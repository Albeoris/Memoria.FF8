using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGastricJuice169Resources(ImmutableArray<byte> Mesh, ImmutableArray<byte> SecondShape, ImmutableArray<byte> Puddle, ImmutableArray<NativeBattleModuleResource> Sprites, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<int> DripCounts);

public sealed record NativeBattleGastricJuice169State([property: JsonRequired] ImmutableArray<byte> Streams, [property: JsonRequired] ImmutableArray<byte> Drops, [property: JsonRequired] ImmutableArray<byte> MeshVertices, [property: JsonRequired] NativeBattleFullMacVertex SplashTranslation)
{
    public static NativeBattleGastricJuice169State FromColdProcess() => new(new byte[5 * 0x110].ToImmutableArray(), new byte[100 * 24].ToImmutableArray(), new byte[5 * 0x248].ToImmutableArray(), new(0, 0, 0, 0));
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGastricJuice169State? GastricJuice169
    {
        get => effectState.GastricJuice169;
        init => effectState = effectState with { GastricJuice169 = value };
    }
}

public static class NativeBattleGastricJuice169
{
    public static int MeshFade(int frame) => frame < 8 ? 0 : frame < 16 ? (frame - 8) << 9 : frame < 24 ? (24 - frame) << 9 : (frame - 24) << 9;
    public static int TintFade(int frame) => frame < 8 ? (8 - frame) << 9 : frame >= 13 ? (frame - 13) << 9 : 0;

    public static void Morph(ImmutableArray<byte> first, ImmutableArray<byte> second, Span<byte> destination, int frame)
    {
        int ratio = NativeBattleCameraMath.Sine((frame << 10) / 32);
        for (int vertex = 0; vertex < 73; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = vertex * 8 + axis * 2;
                short a = BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan().Slice(8 + offset));
                short b = BinaryPrimitives.ReadInt16LittleEndian(second.AsSpan().Slice(offset));
                BinaryPrimitives.WriteInt16LittleEndian(destination[offset..], unchecked((short)(a + ((b - a) * ratio >> 12))));
            }
        }
    }

    public static NativeBattleGfGeometryBatch Emit5E0450(ImmutableArray<byte> source, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int fade, NativeBattleSummonColors colors)
    {
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan());
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor));
            cursor += 4;
            if (group is 0 or 1 or 4 or 5)
            {
                if (count != 0)
                {
                    throw new InvalidDataException("Native Gastric Juice custom prim source contains a skipped nonempty group.");
                }
                continue;
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                ImmutableArray<byte> face = source.Slice(cursor, lengths[group]);
                int corners = (group & 1) != 0 ? 4 : 3;
                NativeBattleGfGeometryPackets.ProjectedCorners projected = new(Enumerable.Range(0, corners).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + corner * 2))).ToArray(), Project);
                NativeBattleGfGeometryAttempt original = NativeBattleGfGeometryPackets.EmitAttempt(face.AsSpan(), projected, group, new(0x24F3, 0, fade, 0x20, null, 0, 0, 17, 2), colors, allocated, variant: new(null, null, false, true));
                byte[] data = new byte[original.PacketLength + 4];
                original.Data.AsSpan().CopyTo(data);
                ImmutableArray<NativeBattleGfPacketByteState>.Builder mask = original.WriteMask.ToBuilder();
                mask.AddRange(Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 4));
                uint header = BinaryPrimitives.ReadUInt32LittleEndian(data);
                BinaryPrimitives.WriteUInt32LittleEndian(data, unchecked(header + 0x01000000));
                NativeBattleGeometryPrimitive? primitive = null;
                if (original.Primitive is { } accepted)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(original.PacketLength), 0xE1000220);
                    for (int index = original.PacketLength; index < data.Length; index++)
                    {
                        mask[index] = NativeBattleGfPacketByteState.Known;
                    }
                    ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                    primitive = accepted with { Words = words };
                    allocated += data.Length;
                }
                attempts.Add(new(original.ByteOffsetRelative, data.ToImmutableArray(), mask.ToImmutable(), primitive));
                cursor += lengths[group];
            }
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort offset)
        {
            ReadOnlySpan<byte> bytes = vertices.AsSpan().Slice(offset * 4, 6);
            return NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), projection);
        }
    }

    public static NativeBattleGfGeometryBatch Tint5DF540(ImmutableArray<NativeBattleRawPolygon> polygons, ImmutableArray<byte> worldVertices, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleVector center, uint modelColor, int frame, NativeBattleSummonColors colors)
    {
        uint tint = colors.Interpolate(0x28C098, TintFade(frame), 0);
        return NativeBattleActorTintPackets.Emit(polygons, worldVertices, view, projection, modelColor, tint, colors, (positions, ratios) =>
        {
            bool near = false;
            for (int corner = 0; corner < positions.Count; corner++)
            {
                short x = unchecked((short)(positions[corner].X - center.X));
                short y = positions[corner].Y;
                short z = unchecked((short)(positions[corner].Z - center.Z));
                int distance = unchecked((int)Math.Sqrt(unchecked(x * x + y * y + z * z)));
                if (distance < 1200)
                {
                    ratios[corner] = unchecked((short)(4096 - (distance << 12) / 1200));
                    near = true;
                }
            }
            return near;
        });
    }
}
