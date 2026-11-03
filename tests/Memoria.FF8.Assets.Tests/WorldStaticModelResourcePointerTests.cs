using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldStaticModelResourcePointerTests
{
    [Fact]
    public void ModelGeometryCanBeRelocatedBeyondItsPointerTableSection()
    {
        WithResources((resources, key, original) =>
        {
            using WorldAssets baseline = new(TestResources.GameDirectory, resources: resources);
            WorldStaticModel expected = baseline.ReadScriptedStaticModel(64);
            int table = Read(original, 15 * 4);
            int start = table + Read(original, table);
            int length = 8 + BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(start)) * 12 + BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(start + 2)) * 16 + BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(start + 6)) * 8;
            byte[] relocated = new byte[original.Length + 16 + length];
            original.CopyTo(relocated, 0);
            original.AsSpan(start, length).CopyTo(relocated.AsSpan(original.Length + 16));
            Write(relocated, table, original.Length + 16 - table);
            ResourceService replaced = Replace(resources, key, relocated);
            using WorldAssets restored = new(TestResources.GameDirectory, resources: replaced);
            WorldStaticModel actual = restored.ReadScriptedStaticModel(64);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
            Assert.Same(actual, restored.ReadScriptedStaticModel(64));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void InvalidModelPointerIsRejected(int pointer)
    {
        WithResources((resources, key, bytes) =>
        {
            Write(bytes, Read(bytes, 15 * 4), pointer);
            ResourceService replaced = Replace(resources, key, bytes);
            using WorldAssets assets = new(TestResources.GameDirectory, resources: replaced);
            Assert.Throws<InvalidDataException>(() => assets.ReadScriptedStaticModel(64));
        });
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void RelocatedModelRequiresItsCompleteHeaderAndDeclaredGeometry(int retainedBytes)
    {
        WithResources((resources, key, original) =>
        {
            int table = Read(original, 15 * 4);
            int start = table + Read(original, table);
            byte[] truncated = new byte[original.Length + retainedBytes];
            original.CopyTo(truncated, 0);
            original.AsSpan(start, retainedBytes).CopyTo(truncated.AsSpan(original.Length));
            Write(truncated, table, original.Length - table);
            ResourceService replaced = Replace(resources, key, truncated);
            using WorldAssets assets = new(TestResources.GameDirectory, resources: replaced);
            Assert.Throws<InvalidDataException>(() => assets.ReadScriptedStaticModel(64));
        });
    }

    [Fact]
    public void ModelRelocationDoesNotAdmitTexturePointersBeyondTheirSection()
    {
        WithResources((resources, key, bytes) =>
        {
            int textureTable = Read(bytes, 41 * 4);
            Write(bytes, textureTable, Read(bytes, 42 * 4) - textureTable);
            ResourceService replaced = Replace(resources, key, bytes);
            using WorldAssets assets = new(TestResources.GameDirectory, resources: replaced);
            Assert.Throws<InvalidDataException>(() => assets.ReadScriptedStaticModel(64));
        });
    }

    private static void WithResources(Action<ResourceService, ResourceKey, byte[]> action)
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        ResourceKey key = NativeResourceNames.WorldSettings(resources.ResourceLanguage);
        action(resources, key, resources.Read(key).ToArray());
    }

    private static ResourceService Replace(ResourceService resources, ResourceKey key, byte[] bytes)
    {
        ResourceService replaced = new(resources.GetSource<IResourceSource>()!, resources.ResourceLanguage);
        replaced.Prepare(new NoTextureReplacement(), encodedResources: [new(key, "obj", 1, "fixture", bytes)]).Commit();
        return replaced;
    }

    private static int Read(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));

    private static void Write(byte[] bytes, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    }

    private sealed class NoTextureReplacement : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => null;
    }
}
