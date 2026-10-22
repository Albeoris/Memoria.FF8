using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleNeedlesFamilyResourcesTests
{
    [Theory]
    [InlineData(177, 356, 0xD44C5Cu)]
    [InlineData(178, 464, 0xD4499Cu)]
    public void Original_addressed_sources_retain_exact_camera_and_four_G3_faces(int effect, int cameraLength, uint cameraAddress)
    {
        using GameAssets game = Open();
        NativeBattleNeedlesFamilyResources resources = game.ReadNativeNeedlesFamilyResources(effect);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeNeedlesFamilyResources(effect));
        Assert.Equal($"FF8_EN:{cameraAddress:X}", resources.Camera.Identity);
        Assert.Equal(cameraLength, resources.Camera.Data.Length);
        Assert.Equal(32, resources.NeedleVertices.Bytes.Length);
        Assert.Equal(80, resources.NeedleFaces.Bytes.Length);
        Assert.Equal(40, resources.SparkSprite.Bytes.Length);
        Assert.Equal(new[] { 0, 0, -2, 0, -3, -8, -4 }, resources.SparkDepthOffsets.ToArray());
        Assert.Equal(-4, resources.SparkDepthOffsets[6]);
        Assert.Equal(-8, resources.SparkDepthOffsets[5]);
        Assert.Throws<InvalidDataException>(() => (resources with { SparkDepthOffsets = resources.SparkDepthOffsets.Take(4).ToImmutableArray() }).Validate());
        Assert.Equal(0x641A9u, resources.Sound);
        NativeBattleNeedlesFamilyResources restored = JsonSerializer.Deserialize<NativeBattleNeedlesFamilyResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { Effect = effect == 177 ? (ushort)178 : (ushort)177 }).Validate());
    }
    [Fact]
    public void Shared_needle_templates_do_not_alias_distinct_spark_material_sources()
    {
        using GameAssets game = Open();
        NativeBattleNeedlesFamilyResources first = game.ReadNativeNeedlesFamilyResources(177);
        NativeBattleNeedlesFamilyResources second = game.ReadNativeNeedlesFamilyResources(178);
        Assert.Equal(first.NeedleVertices.Bytes.ToArray(), second.NeedleVertices.Bytes.ToArray());
        Assert.Equal(first.NeedleFaces.Bytes.ToArray(), second.NeedleFaces.Bytes.ToArray());
        Assert.False(first.SparkSprite.Bytes.SequenceEqual(second.SparkSprite.Bytes));
        Assert.Throws<NotSupportedException>(() => game.ReadNativeNeedlesFamilyResources(176));
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for Needles.");
        return new(directory!);
    }
}
