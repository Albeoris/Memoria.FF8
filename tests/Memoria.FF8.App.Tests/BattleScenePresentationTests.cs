using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleScenePresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_backface_rejection_uses_original_face_winding_before_splitting(bool quad)
    {
        BattleVertex[] vertices = [new(new Vector3(-1, 0, 0), 0), new(new Vector3(1, 0, 0), 0), new(new Vector3(-1, 2, 0), 0), new(new Vector3(1, 2, 0), 0)];
        int[] indices = quad ? [0, 1, 2, 3] : [0, 1, 2];
        Vector2[] uv = Enumerable.Repeat(Vector2.Zero, indices.Length).ToArray();
        PixelImage texture = new(1, 1, [new Rgba32(255, 255, 255, 255)]);
        BattleFace face = new(indices, uv, 0, new Rgba32(128, 128, 128, 255));
        BattleRenderer renderer = new(64, 64);
        BattleCamera camera = new(new Vector3(0, 1, -10), new Vector3(0, 1, 0));
        renderer.Render(new BattleStage(0, [new BattleMesh(vertices, [face])], [texture]), [], camera);
        Assert.Equal(quad ? 2 : 1, renderer.DrawnTriangles);
        BattleFace reversed = face with { Vertices = quad ? [1, 0, 3, 2] : [1, 0, 2] };
        renderer.Render(new BattleStage(0, [new BattleMesh(vertices, [reversed])], [texture]), [], camera);
        Assert.Equal(0, renderer.DrawnTriangles);
    }

    [Fact]
    public void Native_party_slots_preserve_count_order_and_opposing_depth_axis()
    {
        Assert.Equal(new NativeBattleVector(0, 0, 1800), NativeBattleFormation.PartyPosition(0, 1));
        Assert.Equal(new NativeBattleVector(-1000, 0, 1800), NativeBattleFormation.PartyPosition(0, 2));
        Assert.Equal(new NativeBattleVector(1000, 0, 1800), NativeBattleFormation.PartyPosition(1, 2));
        Assert.Equal(new NativeBattleVector(-1200, 0, 2100), NativeBattleFormation.PartyPosition(0, 3));
        Assert.Equal(new NativeBattleVector(0, 0, 1500), NativeBattleFormation.PartyPosition(1, 3));
        Assert.Equal(new NativeBattleVector(1200, 0, 2100), NativeBattleFormation.PartyPosition(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleFormation.PartyPosition(3, 3));
    }
}
