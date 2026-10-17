using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuraMeshFrame(NativeBattleStaticMeshResource Resource, ImmutableArray<byte> Vertices);

/// <summary>88AF90 selects an original scene mesh and its +12 vertex-frame array before572200.</summary>
public static class NativeBattleCuraSceneMesh
{
    public static NativeBattleCuraMeshFrame Select(NativeBattleCuraSceneResource scene, short selector, short frame)
    {
        int count = Read32(scene.Data, 0);
        if (selector < 0 || selector >= count)
        {
            throw new InvalidDataException("Native88AF90 scene mesh selector leaves its original directory.");
        }
        int meshOffset = Read32(scene.Data, checked(8 + 4 * selector));
        int primitiveOffset = Read32(scene.Data, meshOffset);
        int vertices = Read32(scene.Data, checked(meshOffset + 4));
        int frames = Read32(scene.Data, checked(meshOffset + 8));
        if (meshOffset < 0 || primitiveOffset < 12 || vertices < 0 || frames < 1 || frame < 0 || frame >= frames)
        {
            throw new InvalidDataException("Native88AF90 scene vertex-frame pointer leaves its original mesh.");
        }
        int bytes = checked(vertices * 8);
        int vertexOffset = checked(12 + frame * bytes);
        if (vertexOffset > primitiveOffset - bytes)
        {
            throw new InvalidDataException("Native88AF90 scene vertex frame overlaps its primitive groups.");
        }
        Require(scene.Data, meshOffset, primitiveOffset);
        Require(scene.Data, checked(meshOffset + vertexOffset), bytes);
        NativeBattleStaticMeshResource resource = new(unchecked((byte)selector), checked(scene.Address + (uint)meshOffset), scene.Data.AsSpan().Slice(meshOffset).ToArray().ToImmutableArray());
        return new(resource, scene.Data.AsSpan().Slice(meshOffset + vertexOffset, bytes).ToArray().ToImmutableArray());
    }

    private static int Read32(ImmutableArray<byte> data, int offset)
    {
        Require(data, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset));
    }

    private static void Require(ImmutableArray<byte> data, int offset, int count)
    {
        if (data.IsDefault || offset < 0 || count < 0 || offset > data.Length - count)
        {
            throw new InvalidDataException("Native Cura scene pointer leaves its authenticated resource bytes.");
        }
    }
}
