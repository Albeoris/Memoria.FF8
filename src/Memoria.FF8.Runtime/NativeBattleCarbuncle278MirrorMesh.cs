using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The mirror's textured quad group with two ordered draw-mode commands around each accepted packet.</summary>
public static class NativeBattleCarbuncle278MirrorMesh
{
    public static NativeBattleCommonPacketWrite Append6839B0(NativeBattlePrimitiveProcess process, NativeBattleLogicalOrdering ordering, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, byte intensity, Func<int, NativeBattleVector>? readVertices = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ReadOnlySpan<byte> source = mesh.Bytes.AsSpan();
        int group = checked(BinaryPrimitives.ReadInt32LittleEndian(source) + 12);
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(group, 4));
        int faceOffset = checked(group + 4);
        NativeBattleMeshPacketContext context = new(0x10, null, null, null, null, 0, -128, 17, 2);
        for (int faceIndex = 0; faceIndex < count; faceIndex++)
        {
            byte[] face = source.Slice(faceOffset, 24).ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(face, 0x2C000000u | intensity | (uint)intensity << 8 | (uint)intensity << 16);
            ushort[] offsets = Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + index * 2))).ToArray();
            Corners corners = new(offsets, mesh.Bytes, matrix, projection, readVertices, pilotIdentity.HasValue);
            NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, corners, 3, context, null, 0);
            if (pilotIdentity is { } identity && attempt.Primitive is { } captured)
            {
                System.Numerics.Vector3[] world = corners.World!;
                attempt = attempt with { Primitive = captured with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + faceIndex) }, world[0], world[1], world[2], world[3], 4) } };
            }
            NativeBattleGfGeometryBatch batch;
            if (attempt.Primitive is { } primitive)
            {
                NativeBattleGfGeometryAttempt prefix = attempt with { Primitive = null };
                NativeBattleGfGeometryAttempt link = attempt with { WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToImmutableArray() };
                batch = new([prefix, Mode(40, 0xE1000620, primitive.Bucket), link, Mode(48, 0xE1000420, primitive.Bucket)], 56);
            }
            else
            {
                batch = new([attempt], 0);
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, ordering, batch);
            process = written.Process;
            ordering = written.Ordering;
            faceOffset = checked(faceOffset + 24);
        }
        return new(process, ordering);
    }

    private static NativeBattleGfGeometryAttempt Mode(int offset, uint command, int bucket)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(bucket, [0x01000000, command], [0, 0, 0, 0]));
    }

    private sealed class Corners(ushort[] offsets, ImmutableArray<byte> source, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, Func<int, NativeBattleVector>? readVertices, bool capture) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        private readonly NativeBattleModuleProjectedVertex?[] projected = new NativeBattleModuleProjectedVertex?[4];

        internal System.Numerics.Vector3[]? World { get; } = capture ? new System.Numerics.Vector3[4] : null;

        public int Count => 4;

        public NativeBattleModuleProjectedVertex this[int index]
        {
            get
            {
                if (projected[index] is not { } value)
                {
                    NativeBattleVector position;
                    if (readVertices is not null)
                    {
                        position = readVertices(offsets[index] * 4);
                    }
                    else
                    {
                        ReadOnlySpan<byte> vertex = source.AsSpan().Slice(checked(8 + offsets[index] * 4), 6);
                        position = new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
                    }
                    value = NativeBattleModuleProjection.Project45FE10(matrix, position, projection);
                    projected[index] = value;
                    if (World is not null)
                    {
                        World[index] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, position), projection);
                    }
                }
                return value;
            }
        }

        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator()
        {
            for (int index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
