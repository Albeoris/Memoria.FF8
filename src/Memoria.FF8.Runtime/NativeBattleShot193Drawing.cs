using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>5B84E0's four textured groups with the original in-packet E1 store and zero-area rejection.</summary>
public static class NativeBattleShot193Drawing
{
    public static NativeBattleGfGeometryBatch Draw5B84E0(ImmutableArray<byte> source, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint flags, int? fade, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (source.IsDefault || source.Length < 8)
        {
            throw new InvalidDataException("Native Dark Shot mesh requires its original directory.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan());
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Dictionary<ushort, System.Numerics.Vector3>? pilotWorld = pilotIdentity.HasValue ? [] : null;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor, 4));
            cursor = checked(cursor + 4);
            if ((group & 2) == 0)
            {
                if (count != 0)
                {
                    throw new NotSupportedException("Native5B84E0 skips the original empty untextured group headers.");
                }
                continue;
            }
            int length = group switch { 2 => 20, 3 => 24, 6 => 28, 7 => 36, _ => throw new InvalidDataException("Native Dark Shot selected an invalid mesh group.") };
            for (int polygon = 0; polygon < count; polygon++)
            {
                ReadOnlySpan<byte> face = source.AsSpan().Slice(cursor, length);
                ushort[] offsets = Enumerable.Range(0, (group & 1) == 0 ? 3 : 4).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(cursor + 4 + corner * 2, 2))).ToArray();
                NativeBattleGfGeometryAttempt attempt = EmitFace5B84E0(face, new Corners(offsets, Project), group, flags, fade, colors, allocated);
                if (pilotIdentity is { } identity && attempt.Primitive is { } primitive)
                {
                    int corners = offsets.Length;
                    NativeBattlePilotVisualGeometry visual = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, pilotWorld![offsets[0]], pilotWorld[offsets[1]], pilotWorld[offsets[2]], corners == 4 ? pilotWorld[offsets[3]] : default, (byte)corners);
                    attempt = attempt with { Primitive = primitive with { PilotGeometry = visual } };
                }
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocated = checked(allocated + attempt.PacketLength);
                }
                cursor = checked(cursor + length);
            }
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort offset)
        {
            ReadOnlySpan<byte> bytes = source.AsSpan().Slice(checked(8 + offset * 4), 6);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4)));
            if (pilotWorld is not null)
            {
                pilotWorld[offset] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
            }
            return NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
        }
    }

    public static NativeBattleGfGeometryAttempt EmitFace5B84E0(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, int group, uint flags, int? fade, NativeBattleSummonColors colors, int offset = 0)
    {
        if (group is not (2 or 3 or 6 or 7))
        {
            throw new InvalidDataException("Native5B84E0 only reaches its four textured groups.");
        }
        NativeBattleMeshPacketContext context = new(flags, 0, fade, null, null, 0, 0, 17, 2);
        NativeBattleGfGeometryAttempt original = NativeBattleGfGeometryPackets.EmitAttempt(face, vertices, group, context, colors, offset, variant: new(null, null, false, true));
        byte[] bytes = new byte[original.Data.Length + 4];
        original.Data.AsSpan().CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, unchecked(BinaryPrimitives.ReadUInt32LittleEndian(bytes) + 0x1000000));
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        original.WriteMask.AsSpan().CopyTo(mask);
        NativeBattleGeometryPrimitive? primitive = original.Primitive;
        if (primitive is not null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(original.Data.Length), 0xE1000220);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, original.Data.Length, 4);
            primitive = primitive with { Words = primitive.Words.SetItem(0, unchecked(primitive.Words[0] + 0x1000000)).Add(0xE1000220) };
        }
        return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
    }

    private sealed class Corners(ushort[] offsets, Func<ushort, NativeBattleModuleProjectedVertex> project) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => offsets.Length;
        public NativeBattleModuleProjectedVertex this[int index] => project(offsets[index]);
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
