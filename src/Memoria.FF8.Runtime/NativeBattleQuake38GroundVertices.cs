using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38GroundVertices
{
    public static ImmutableArray<byte> Transform8D8B10(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, ImmutableArray<byte> workspace)
    {
        mesh.Validate();
        if (bones.IsDefault || workspace.IsDefault || workspace.Length < checked(mesh.Vertices.Length * 8))
        {
            throw new InvalidDataException("Native Quake ground transform requires its actual bone matrices and retained vertex workspace.");
        }
        byte[] result = workspace.ToArray();
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleVector vertex = NativeBattleCureActorPackets.TransformBone4607E0(mesh.Vertices[index], bones);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8), vertex.X);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8 + 2), vertex.Y);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8 + 4), vertex.Z);
        }
        return result.ToImmutableArray();
    }
}
