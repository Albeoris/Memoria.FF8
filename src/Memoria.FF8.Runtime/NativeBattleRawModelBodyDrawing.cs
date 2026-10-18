using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRawModelBodyDraw(ImmutableArray<byte> ProjectedScratch, int ProjectedBytesWritten, NativeBattleGfGeometryBatch Packets);

/// <summary>One packed object's posed body projection and5099D0 packets, without shadow or bounds side effects.</summary>
public static class NativeBattleRawModelBodyDrawing
{
    public static NativeBattleRawModelBodyDraw Draw5099D0(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleCameraProjection projection, uint color, uint objectMask, ImmutableArray<byte> projectedScratch, NativeBattleSummonColors colors)
    {
        if (bones.IsDefault || projectedScratch.IsDefault)
        {
            throw new InvalidDataException("Native raw body drawing requires explicit bone matrices and projected scratch owners.");
        }
        byte[] scratch = projectedScratch.ToArray();
        int bytesWritten = 0;
        ImmutableArray<NativeBattleGeometryPrimitive> body = [];
        if ((objectMask & 1) != 0)
        {
            mesh.Validate();
            NativeBattleProjectedVertex[] vertices = mesh.Vertices.Select(vertex => NativeBattleGeometryProjection.Project50F900(vertex.Bone < bones.Length ? bones[vertex.Bone] : throw new InvalidDataException("Native raw body lacks its selected bone matrix."), vertex.Position, projection, new(0, 0, 320, 216))).ToArray();
            bytesWritten = checked(vertices.Length * 8);
            if (bytesWritten > scratch.Length)
            {
                throw new InvalidDataException("Native raw body projection leaves its supplied scratch allocation.");
            }
            for (int index = 0; index < vertices.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.AsSpan(index * 8), vertices[index].Screen);
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.AsSpan(index * 8 + 4), vertices[index].DepthAndClip);
            }
            body = NativeBattleRawGeometryPackets.EmitProjected5099D0(mesh.Polygons, vertices, color, 17, 4, 0, null, colors);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach (NativeBattleGeometryPrimitive primitive in body)
        {
            byte[] data = new byte[primitive.Words.Length * 4];
            for (int index = 0; index < primitive.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), primitive.Words[index]);
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), primitive.PhysicalWriteMask ?? Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToImmutableArray(), primitive));
            allocated += data.Length;
        }
        return new(scratch.ToImmutableArray(), bytesWritten, new(attempts.ToImmutable(), allocated));
    }
}
