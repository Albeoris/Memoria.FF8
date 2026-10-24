using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldAssetTests
{
    private static string GameDirectory => TestResources.GameDirectory;
    private static readonly Dictionary<int, int> StaticGarden = new() { [274] = 827, [275] = 828 };

    [Fact]
    public void SelectedOriginalGardenSegmentsKeepSourceIdsUvsAndCollisionFlags()
    {
        using WorldAssets assets = new(GameDirectory);
        Assert.Equal(new WorldSpawn(24576, -29406, -658, 32779), assets.Spawns[0]);
        Assert.Equal(275, WorldAssets.SegmentAt(assets.Spawns[0].X, assets.Spawns[0].Z));
        WorldSegment segment = assets.GetSegment(275, StaticGarden);
        Assert.Equal(828, segment.SourceId);
        Assert.NotEmpty(segment.Triangles);
        Assert.Equal(1, assets.LoadedSegmentCount);
        foreach (WorldTriangle triangle in segment.Triangles)
        {
            Assert.InRange(triangle.TexturePage, 0, 15);
            Assert.InRange(triangle.Palette, 0, 15);
            Assert.InRange(triangle.UvA.X, 0.5f / 256, 255.5f / 256);
            Assert.InRange(triangle.UvA.Y, 0.5f / 256, 255.5f / 256);
        }
        WorldGround ground = Assert.IsType<WorldGround>(assets.FindGround(24576, -29406, StaticGarden, 658));
        Assert.True(ground.Triangle.Walkable);
        Assert.InRange(ground.Height, 500, 900);
        WorldGround nativeGround = Assert.IsType<WorldGround>(assets.FindNativeGround(24576, -29406, StaticGarden, 658));
        Assert.Equal(657, nativeGround.Height);
        Assert.Equal(28, nativeGround.Triangle.GroundType);
        Assert.Equal(96, nativeGround.Triangle.TextureFlags);
        Assert.Equal(208, nativeGround.Triangle.VertexFlags);
        WorldTriangle ordinary = segment.Triangles.First(triangle => !triangle.Road && !triangle.Water);
        var texture = assets.GetTexture(ordinary);
        Assert.Equal(256, texture.Width);
        Assert.Equal(256, texture.Height);
        Assert.Contains(texture.Pixels, color => color.R != color.G || color.G != color.B);
        Assert.Equal(1, assets.LoadedTextureCount);
        Assert.Same(texture, assets.GetTexture(ordinary));
        Assert.Equal(0, WorldAssets.SegmentAt(-131072, -98304));
        Assert.Equal(0, WorldAssets.SegmentAt(131072, 98304));
    }

    [Fact]
    public void OriginalWorldSquallRetainsFullSignedWordRotationsAndEightAnimations()
    {
        using WorldAssets assets = new(GameDirectory);
        FieldModel model = assets.Squall;
        Assert.Equal(16, model.Bones.Count);
        Assert.Equal(166, model.Vertices.Count);
        Assert.Equal(163, model.Faces.Count);
        Assert.Equal(new[] { 1, 20, 11, 12, 50, 1, 20, 30 }, model.Animations.Select(animation => animation.Frames.Count));
        Assert.Equal(new Vector3(-2, 5, 154), model.Animations[0].Frames[0].Translation);
        Assert.Equal(new Vector3(3 * MathF.PI / 2, 0, 3 * MathF.PI / 2), model.Animations[0].Frames[0].Rotations[0]);
        Assert.Equal(new[] { 20, 44 }, model.Textures.Select(texture => texture.Width));
        Assert.All(model.Textures, texture => Assert.Equal(64, texture.Height));
        Assert.All(model.Animations.SelectMany(animation => animation.Frames), frame => Assert.Equal(16, frame.Rotations.Count));
    }

    [Fact]
    public void UniformFootTypeUsesOriginalShapeFourGeometryTexturesAndAnimationTable()
    {
        using WorldAssets assets = new(GameDirectory);
        FieldModel normal = assets.GetFootModel(0);
        FieldModel uniform = assets.GetFootModel(1);
        Assert.Same(assets.Squall, normal);
        Assert.Same(uniform, assets.GetFootModel(1));
        Assert.NotSame(normal, uniform);
        Assert.Equal("World character shape 4", uniform.Name);
        Assert.Equal(16, uniform.Bones.Count);
        Assert.Equal(164, uniform.Vertices.Count);
        Assert.Equal(166, uniform.Faces.Count);
        Assert.Equal(new[] { 1, 20, 11, 12, 50, 1, 20, 30 }, uniform.Animations.Select(animation => animation.Frames.Count));
        Assert.Equal(new[] { 20, 44 }, uniform.Textures.Select(texture => texture.Width));
        Assert.Equal("World character shape 5", assets.GetFootModel(2).Name);
        Assert.Throws<NotSupportedException>(() => assets.GetFootModel(3));
        WorldTerrainRenderer renderer = new(assets, 320, 224);
        Vector3 position = new(24576, 658, -29406);
        WorldCamera camera = WorldCamera.Foot(2048, 2048) with { TargetPosition = position + new Vector3(0, 256, 0) };
        byte[] normalPixels = renderer.Render(position, MathF.PI, 0, 0, camera, StaticGarden, avatarModel: normal).ToArray();
        byte[] uniformPixels = renderer.Render(position, MathF.PI, 0, 0, camera, StaticGarden, avatarModel: uniform).ToArray();
        Assert.NotEqual(normalPixels, uniformPixels);
        Assert.Equal(uniformPixels, renderer.Render(position, MathF.PI, 0, 0, camera, StaticGarden, avatarModel: uniform));
    }

    [Fact]
    public void SelectedWorldRasterIsDeterministicAndRespondsToOriginalTerrainPosition()
    {
        using WorldAssets assets = new(GameDirectory);
        WorldTerrainRenderer renderer = new(assets, 320, 240);
        WorldCamera camera = new(0, 120, 160, 10, MathF.PI / 3, 900);
        Vector3 position = new(24576, 658, -29406);
        byte[] first = renderer.Render(position, 0, 0, 0, camera, StaticGarden).ToArray();
        Assert.True(renderer.DrawnTriangles > 100);
        Assert.Equal(first, renderer.Render(position, 0, 0, 0, camera, StaticGarden));
        Assert.NotEqual(first, renderer.Render(position + new Vector3(128, 0, 0), 0, 1, 4, camera, StaticGarden));
        Assert.InRange(assets.LoadedSegmentCount, 1, 25);
        Assert.Equal(320 * 240 * 4, first.Length);
        Assert.All(Enumerable.Range(0, first.Length / 4), index => Assert.Equal(255, first[index * 4 + 3]));
    }
}
