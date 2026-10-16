using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed record NativeBattleGeometrySubmission(NativeBattlePrimitiveOrdering Ordering, uint NextPacketAddress, int EmittedPackets);

/// <summary>502D40's player body submission into the shared physical OT; linked and billboard paths stay explicit.</summary>
public static class NativeBattleActorPackets
{
    public static NativeBattleGeometrySubmission SubmitPlayerBody502D40(NativeBattlePrimitiveOrdering ordering, uint packetAddress, BattleModel asset, NativeBattleGraphicsModel model, NativeBattleCameraProjection projection)
    {
        byte id = model.ModelId4 ?? throw new NotSupportedException("Native502D40 requires its actual model ID BYTE4.");
        NativeBattleModelTextureBinding binding = model.BodyTextureSlots ?? throw new NotSupportedException("Native player packets require their original507400 texture allocation.");
        NativeBattlePoseSource source = asset.NativePoseSource ?? throw new NotSupportedException("Native player packets require their original DAT skeleton/pose source.");
        uint color = model.Color40 ?? throw new NotSupportedException("Native502D40 requires its original model color DWORD40.");
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native502D40 requires its original mesh mask DWORD124.");
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.GeometryMatrices(model, source, projection.View ?? throw new NotSupportedException("Native actor packets require the actual5033E0 view matrix."));
        NativeBattlePrimitiveOrdering result = ordering;
        uint cursor = packetAddress;
        int emitted = 0;
        for (int index = 0; index < asset.Meshes.Count; index++)
        {
            if ((mask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleGeometrySubmission submission = SubmitMesh5099D0(result, cursor, asset.Meshes[index], matrices, projection, color, null, 17, 4, face => RelocatePlayerPolygon507550(face, binding));
            result = submission.Ordering;
            cursor = submission.NextPacketAddress;
            emitted += submission.EmittedPackets;
        }
        return new(result, cursor, emitted);
    }

    public static NativeBattleGeometrySubmission SubmitMesh5099D0(NativeBattlePrimitiveOrdering ordering, uint packetAddress, BattleMesh mesh, IReadOnlyList<NativeBattleActionMatrix> modelViewMatrices, NativeBattleCameraProjection projection, uint colorFlags28, short? colorDepth36, int tableBaseBucket, byte depthShift, Func<BattleFace, byte[]> resolveOriginalPolygon)
    {
        NativeBattleProjectedVertex[] vertices = new NativeBattleProjectedVertex[mesh.Vertices.Count];
        for (int index = 0; index < vertices.Length; index++)
        {
            BattleVertex vertex = mesh.Vertices[index];
            if ((uint)vertex.Bone >= modelViewMatrices.Count)
            {
                throw new InvalidDataException("Native5099D0 vertex references a missing original bone matrix.");
            }
            vertices[index] = NativeBattleGeometryProjection.Project50F900(modelViewMatrices[vertex.Bone], vertex.NativePosition ?? throw new NotSupportedException("Native5099D0 requires the original signed vertex WORDs."), projection, new(0, 0, 320, 216));
        }
        NativeBattlePrimitiveOrdering result = ordering;
        uint cursor = packetAddress;
        int emitted = 0;
        foreach (BattleFace face in mesh.Faces)
        {
            byte[] polygon = resolveOriginalPolygon(face);
            bool quad = face.Vertices.Length == 4;
            if (face.Vertices.Length is not (3 or 4))
            {
                throw new NotSupportedException("Native50FDF0 requires its original triangle or quad.");
            }
            NativeBattleGeometryPacket? packet = NativeBattleGeometryPackets.Emit50FDF0(polygon, vertices, quad, colorFlags28, tableBaseBucket, depthShift, cursor, colorDepth36);
            if (packet is null)
            {
                continue;
            }
            result = result.PrependProjected45C870(packet.Bucket, packet.Packet, packet.VertexPayload4);
            cursor = checked(cursor + (uint)(packet.Packet.Words.Length * 4));
            emitted++;
        }
        return new(result, cursor, emitted);
    }

    public static byte[] RelocatePlayerPolygon507550(BattleFace face, NativeBattleModelTextureBinding binding)
    {
        if (face.NativePolygon.IsDefaultOrEmpty || face.NativeMaterial is null)
        {
            throw new NotSupportedException("Native507550 requires the original polygon record and material WORDs.");
        }
        byte[] polygon = face.NativePolygon.ToArray();
        bool quad = face.Vertices.Length == 4;
        if (polygon.Length == (quad ? 24 : 20))
        {
            //507550 visits only the two plain groups; the colored groups retain their original material and command.
            return polygon;
        }
        NativeBattleFaceMaterial material = NativeBattleFaceMaterialBinding.Apply507550(face.NativeMaterial, binding);
        int[] uvOffsets = quad ? [8, 12, 16, 18] : [6, 8, 12];
        if (material.Uv.Length != uvOffsets.Length || polygon.Length != (quad ? 20 : 16))
        {
            throw new InvalidDataException("Native507550 player polygon material does not match its original record.");
        }
        for (int index = 0; index < uvOffsets.Length; index++)
        {
            polygon[uvOffsets[index]] = unchecked((byte)material.Uv[index].X);
            polygon[uvOffsets[index] + 1] = unchecked((byte)material.Uv[index].Y);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(10), material.Clut);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(14), material.TexturePage);
        return polygon;
    }
}
