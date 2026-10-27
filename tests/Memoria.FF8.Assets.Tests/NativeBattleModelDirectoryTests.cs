using System.Buffers.Binary;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModelDirectoryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void ModelGeometryUsesTheLoadedExtentInsteadOfTheUnusedTerminalWord(int declared)
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        ResourceKey key = new("battle/c0m000.dat");
        byte[] bytes = resources.Read(key);
        using BattleAssets installed = new(TestResources.GameDirectory, resources);
        BattleModel expected = installed.LoadEnemy(0);
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + sections * 4), declared);
        resources.Prepare(null, encodedResources: [new EncodedResource(key, "dat", 1, "fixture", bytes)]).Commit();
        using BattleAssets selected = new(TestResources.GameDirectory, resources);
        BattleModel model = selected.LoadEnemy(0);
        Assert.Equal(expected.Meshes.Sum(mesh => mesh.Vertices.Count), model.Meshes.Sum(mesh => mesh.Vertices.Count));
        Assert.Equal(expected.Meshes.Sum(mesh => mesh.Faces.Count), model.Meshes.Sum(mesh => mesh.Faces.Count));
        Assert.Equal(expected.Textures.Count, model.Textures.Count);
        Assert.Equal(expected.Bones.Count, model.Bones.Count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length + 1);
        resources.Prepare(null, encodedResources: [new EncodedResource(key, "dat", 2, "fixture", bytes)]).Commit();
        using BattleAssets malformed = new(TestResources.GameDirectory, resources);
        Assert.Throws<InvalidDataException>(() => malformed.LoadEnemy(0));
    }
}
