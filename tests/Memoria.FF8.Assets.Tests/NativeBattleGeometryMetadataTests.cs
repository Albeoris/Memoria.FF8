using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGeometryMetadataTests
{
    [Fact]
    public void Reached_player_and_stage31_keep_original_vertex_WORDs_polygon_bytes_and_stage_groups()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        BattleModel player = assets.LoadPlayer(0);
        BattleStage stage = assets.LoadStage(31);
        Assert.NotEmpty(player.Meshes);
        Assert.NotEmpty(stage.Meshes);
        foreach (BattleMesh mesh in player.Meshes)
        {
            Assert.All(mesh.Vertices, vertex => Assert.NotNull(vertex.NativePosition));
            Assert.All(mesh.Faces, face => Assert.Equal(face.Vertices.Length == 4 ? 20 : 16, face.NativePolygon.Length));
        }
        foreach (BattleMesh mesh in stage.Meshes)
        {
            Assert.InRange(mesh.NativeStageGroup!.Value, 0, 3);
            Assert.All(mesh.Vertices, vertex => Assert.NotNull(vertex.NativePosition));
            Assert.All(mesh.Vertices, vertex => Assert.Equal(1, vertex.Bone));
            Assert.All(mesh.Faces, face => Assert.Equal(face.Vertices.Length == 4 ? 24 : 20, face.NativePolygon.Length));
        }
    }
}
