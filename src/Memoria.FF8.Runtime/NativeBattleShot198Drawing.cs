using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Hyper Shot's signed scrolling5AAD60 and inline-E1 textured5AC8F0 packet paths.</summary>
public static class NativeBattleShot198Drawing
{
    public static NativeBattleGfGeometryBatch Draw5AAD60(NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint flags, int? fade, int phase, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleShivaScrollingContext scrolling = new(flags, fade, 0, -16 * phase, 17, 2, new(0, 128, 64, 128));
        return NativeBattleEffectMeshPackets.EmitBatch(mesh.Bytes, matrix, projection, 0, null, (group, face, project, offset) =>
        {
            return EmitFace5AAD60(face, project, group, scrolling, colors, offset);
        }, projectFourth: vertex => NativeBattleModuleProjection.Project45FAA0(matrix, vertex, projection, 0, 0), pilotIdentity: pilotIdentity);
    }

    public static NativeBattleGfGeometryBatch EmitFace5AAD60(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> project, int group, NativeBattleShivaScrollingContext scrolling, NativeBattleSummonColors colors, int offset = 0)
    {
        if (group is 3 or 6 or 7)
        {
            return NativeBattleShivaScrollingPackets.EmitFace(face, project, (group & 1) != 0, group >= 6, scrolling, 341, 256, colors, offset);
        }
        if (group is not (4 or 5))
        {
            throw new NotSupportedException("Native Hyper Shot scrolling mesh requires its original reached five groups.");
        }
        NativeBattleMeshPacketContext context = new(scrolling.Flags, 0, scrolling.Fade, null, null, 0, 0, scrolling.TableBaseBucket, scrolling.DepthShift);
        NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new ProjectedCorners((group & 1) == 0 ? 3 : 4, project), group, context, colors, offset, variant: new(null, null, false, true));
        return new([attempt], attempt.Primitive is null ? 0 : attempt.PacketLength);
    }

    public static NativeBattleGfGeometryBatch Draw5AC8F0(ImmutableArray<byte> source, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint flags, int? fade, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (source.IsDefault || source.Length < 8)
        {
            throw new InvalidDataException("Native Hyper Shot mesh requires its original directory.");
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
                    throw new NotSupportedException("Native5AC8F0 skips the original empty untextured group headers.");
                }
                continue;
            }
            int length = group switch { 2 => 20, 3 => 24, 6 => 28, 7 => 36, _ => throw new InvalidDataException("Native Hyper Shot selected an invalid mesh group.") };
            for (int polygon = 0; polygon < count; polygon++)
            {
                ReadOnlySpan<byte> face = source.AsSpan().Slice(cursor, length);
                ushort[] offsets = Enumerable.Range(0, (group & 1) == 0 ? 3 : 4).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(cursor + 4 + corner * 2, 2))).ToArray();
                NativeBattleGfGeometryAttempt attempt = EmitFace5AC8F0(face, new Corners(offsets, Project), group, flags, fade, colors, allocated);
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

    public static NativeBattleGfGeometryAttempt EmitFace5AC8F0(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, int group, uint flags, int? fade, NativeBattleSummonColors colors, int offset = 0)
    {
        if (group is not (3 or 7))
        {
            throw new InvalidDataException("Native5AC8F0 requires its original reached textured quad groups.");
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

    private sealed class ProjectedCorners(int count, Func<int, NativeBattleModuleProjectedVertex> project) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => count;
        public NativeBattleModuleProjectedVertex this[int index] => project(index);
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Corners(ushort[] offsets, Func<ushort, NativeBattleModuleProjectedVertex> project) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => offsets.Length;
        public NativeBattleModuleProjectedVertex this[int index] => project(offsets[index]);
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
