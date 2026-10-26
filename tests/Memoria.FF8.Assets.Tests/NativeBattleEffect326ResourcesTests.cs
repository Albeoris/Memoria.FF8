using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect326ResourcesTests
{
    [Fact]
    public void ActualResourcesRetainThreeDistinctModelsAndTheEntireModifiedCameraProgramme()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffect326Resources resources = game.ReadNativeEffect326Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect326Resources());
        Assert.Equal(new[] { 100896, 83680, 4, 101056, 4, 19488, 4, 13084 }, resources.Files.Select(file => file.Data.Length));
        Assert.Equal(Enumerable.Range(737, 8), resources.Files.Select(file => (int)file.FileId));
        Assert.Equal(60, resources.MainModel.PoseSource.BoneCount);
        Assert.Equal(2, resources.EmbeddedModel.PoseSource.BoneCount);
        Assert.Equal(23, resources.LateModel.PoseSource.BoneCount);
        Assert.Equal(13, resources.Meshes.Count);
        Assert.Equal(5, resources.Sprites.Count);
        Assert.Equal(438, resources.Camera.Data.Length);
        Assert.Equal((ushort)0x81, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan()));
        Assert.Equal((ushort)0x8000, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(436)));
        Assert.Equal(16384, resources.ScrollPixels.Length);
        Assert.Equal(1008, resources.ReadStatic(0xE15668, 1008).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xE15668, 1009));
        Assert.Equal(new short[] { 576, 256, 64, 128 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(0xE196B8, 8).AsSpan(index * 2, 2))));
    }
}
