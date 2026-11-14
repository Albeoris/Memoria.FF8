using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleArenaBodyDrawingTests
{
    [Theory]
    [InlineData(100000, -80000, 90000)]
    [InlineData(-100000, 80000, -90000)]
    public void Distant_arena_translation_preserves_native_packets_topology_and_materials(int x, int y, int z)
    {
        Material material = new();
        NativeBattleForeignSurface surface = new(material, [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattleArenaFace face = new(new(2, 3, 4), new(-100, -100, 0), new(100, -100, 0), new(-100, 100, 0), surface);
        NativeBattleActionMatrix camera = NativeBattleForeignModelGeometry.Identity with { Z = 5000 };
        NativeBattleGfGeometryBatch original = Draw(face, camera);
        Vector3 translation = new(x, y, z);
        NativeBattleArenaFace distant = face with { A = face.A + translation, B = face.B + translation, C = face.C + translation };
        NativeBattleGfGeometryBatch moved = Draw(distant, camera with { X = -x, Y = -y, Z = 5000 - z });
        Assert.NotEmpty(original.Attempts);
        Assert.Equal(original.AllocationBytes, moved.AllocationBytes);
        Assert.Equal(original.Attempts.Length, moved.Attempts.Length);
        foreach ((NativeBattleGfGeometryAttempt first, NativeBattleGfGeometryAttempt second) in original.Attempts.Zip(moved.Attempts))
        {
            Assert.Equal(first.Data, second.Data);
            Assert.Equal(first.Primitive!.Words, second.Primitive!.Words);
            Assert.Equal(first.Primitive.VertexPayload4, second.Primitive.VertexPayload4);
            Assert.Same(surface, second.Primitive.PilotGeometry!.ForeignSurface);
        }
        Assert.Equal(face.Id, distant.Id);
    }

    private static NativeBattleGfGeometryBatch Draw(NativeBattleArenaFace face, NativeBattleActionMatrix matrix)
    {
        NativeBattleCameraProjection projection = new(matrix, 512, 160, 108);
        NativeBattleSummonColors colors = new(0x808080, 0x101010, 1f / 4096, 4503601774854144d);
        return NativeBattleArenaBodyDrawing.Draw(new([face]), matrix, projection, 0x808080, colors, new(1, 2, 3, 4, 5), 4, 17, 0);
    }

    private sealed class Material : INativeBattleForeignMaterial;
}
