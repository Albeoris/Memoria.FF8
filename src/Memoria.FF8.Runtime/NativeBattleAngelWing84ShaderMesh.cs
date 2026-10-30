using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84ShaderDraw(NativeBattleEffectArena Vertices, int VertexBytesWritten, NativeBattleGfGeometryBatch Packets);

/// <summary>796720's posed local vertex publication and private plain polygon packets.</summary>
public static class NativeBattleAngelWing84ShaderMesh
{
    public const int VertexCapacityBytes = 12000;

    public static NativeBattleAngelWing84ShaderDraw Draw(ImmutableArray<NativeBattleRawMesh> meshes, ImmutableArray<NativeBattleActionMatrix> localBones, NativeBattleActionMatrix modelView, NativeBattleCameraProjection projection, uint objectMask, short radius, short centerY, NativeBattleEffectArena vertices, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (meshes.IsDefault || localBones.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native796720 requires its original objects and current local bone matrices.");
        }
        vertices.Validate();
        modelView.Validate();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int maximumWritten = 0;
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            if ((objectMask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[meshIndex];
            mesh.Validate();
            int written = checked(mesh.Vertices.Length * 12);
            if (written > VertexCapacityBytes)
            {
                throw new InvalidDataException("Native796720 leaves its original local vertex workspace.");
            }
            maximumWritten = Math.Max(maximumWritten, written);
            for (int index = 0; index < mesh.Vertices.Length; index++)
            {
                NativeBattleVector posed = NativeBattleCureActorPackets.TransformBone4607E0(mesh.Vertices[index], localBones);
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, posed.X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), posed.Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), posed.Z);
                byte[] shade = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(shade, NativeBattleAngelWing84ShaderPackets.Shade(posed, centerY, radius));
                vertices = vertices.Write(index * 12, xyz).MarkUnknown(index * 12 + 6, 2).Write(index * 12 + 8, shade);
            }
            int polygonIndex = 0;
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons.Where(face => !face.Quad && face.Bytes.Length == 16).Concat(mesh.Polygons.Where(face => face.Quad && face.Bytes.Length == 20)))
            {
                int sourcePolygon = polygonIndex++;
                int count = polygon.Quad ? 4 : 3;
                System.Numerics.Vector3[] world = new System.Numerics.Vector3[count];
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[count];
                byte[] shades = new byte[count];
                for (int corner = 0; corner < 3; corner++)
                {
                    ReadCorner(corner);
                }
                if (NativeBattleModulePackets.Nclip(corners[0].Screen, corners[1].Screen, corners[2].Screen) <= 0)
                {
                    continue;
                }
                if (polygon.Quad)
                {
                    ReadCorner(3);
                }
                NativeBattleGfGeometryBatch batch = NativeBattleAngelWing84ShaderPackets.Emit(polygon.Bytes.AsSpan(), polygon.Quad, corners, shades);
                if (pilotIdentity is { } identity)
                {
                    batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked((identity.Primitive * 397 + meshIndex) * 65537 + sourcePolygon) }, world[0], world[1], world[2], polygon.Quad ? world[3] : default, (byte)count) { SourceDepths = new(corners[0].Depth, corners[1].Depth, corners[2].Depth, polygon.Quad ? corners[3].Depth : 0), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
                }
                attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
                allocated = checked(allocated + batch.AllocationBytes);

                void ReadCorner(int corner)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF;
                    if (index >= mesh.Vertices.Length)
                    {
                        throw new InvalidDataException("Native796720 polygon leaves its current object's vertex publication.");
                    }
                    byte[] xyz = vertices.Read(index * 12, 6);
                    NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
                    corners[corner] = NativeBattleModuleProjection.Project45FE10(modelView, position, projection);
                    if (pilotIdentity.HasValue)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(modelView, position), projection);
                    }
                    shades[corner] = vertices.Read(index * 12 + 8, 1)[0];
                }
            }
        }
        return new(vertices, maximumWritten, new(attempts.ToImmutable(), allocated));
    }
}
