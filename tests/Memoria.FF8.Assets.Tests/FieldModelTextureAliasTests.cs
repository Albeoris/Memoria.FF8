using Memoria.FF8.Assets;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using System.Buffers.Binary;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldModelTextureAliasTests
{
    private sealed class OneOverride(byte[] bytes) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            content = bytes;
            return logicalPath.EndsWith("chara.one", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NativeClassNibblePreservesDReferenceAndAReferenceAboveFifteen()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using IFlArchive outer = FlArchive.OpenForRead(Path.Combine(directory, "Data", "lang-en", "field.fs"), FlArchiveRepresentation.Files);
        byte[] Read(IFlArchive archive, string name)
        {
            using Stream stream = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(name, StringComparison.OrdinalIgnoreCase)).OpenForRead();
            using MemoryStream content = new();
            stream.CopyTo(content);
            return content.ToArray();
        }
        using IFlArchive nested = FlArchive.Open(new MemoryStream(Read(outer, "bgpaty_1.fl")), new MemoryStream(Read(outer, "bgpaty_1.fi")), new MemoryStream(Read(outer, "bgpaty_1.fs")), false, FlArchiveRepresentation.Files);
        byte[] one = Read(nested, "chara.one");
        // Four 32-byte D headers, two 36-byte embedded headers, one 36-byte A header.
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(one));
        Assert.Equal(0xD0100005u, BinaryPrimitives.ReadUInt32LittleEndian(one.AsSpan(16)));
        Assert.Equal(0xA054C000u, BinaryPrimitives.ReadUInt32LittleEndian(one.AsSpan(216)));
        byte[] textureOwner = one.AsSpan(168, 36).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(one, 17);
        for (int index = 7; index <= 16; index++)
        {
            textureOwner.CopyTo(one.AsSpan(240 + (index - 7) * 36));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(one.AsSpan(16), 0xD1100005);
        BinaryPrimitives.WriteUInt32LittleEndian(one.AsSpan(216), 0xA104C000);
        using GameAssets originalAssets = new(directory);
        FieldAssets original = originalAssets.LoadField(221);
        using GameAssets variantAssets = new(directory, new OneOverride(one));
        FieldAssets variant = variantAssets.LoadField(221);
        Assert.Equal(17, variant.Models.Count);
        Assert.Same(variant.Models[16].Textures[0], Assert.Single(variant.Models[6].Textures));
        Assert.NotSame(variant.Models[5].Textures[0], variant.Models[6].Textures[0]);
        Assert.Equal(original.Models[6].Textures[0].Pixels, variant.Models[6].Textures[0].Pixels);
        Assert.Equal(original.Models[6].Vertices, variant.Models[6].Vertices);
        Assert.Equal(original.GetAnimationFrameCount(6, 2), variant.GetAnimationFrameCount(6, 2));
        Assert.Equal(original.Models[0].Vertices, variant.Models[0].Vertices);
        Assert.Equal(original.Models[0].Textures[0].Pixels, variant.Models[0].Textures[0].Pixels);
        Assert.Equal(original.GetAnimationFrameCount(0, 0), variant.GetAnimationFrameCount(0, 0));
    }

    [Fact]
    public void OriginalPartyModelKeepsOwnGeometryAndAnimationsWithNativeAliasedTexture()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(221);
        Assert.Equal(7, field.Models.Count);
        FieldModel model = field.Models[6];
        Assert.Equal("o029???", model.Name);
        Assert.NotEmpty(model.Bones);
        Assert.NotEmpty(model.Vertices);
        Assert.NotEmpty(model.Faces);
        Assert.True(model.Animations.Count >= 3);
        Assert.True(field.GetAnimationFrameCount(6, 0) > 0);
        Assert.True(field.GetAnimationFrameCount(6, 1) > 0);
        Assert.True(field.GetAnimationFrameCount(6, 2) > 0);
        Assert.Same(field.Models[5].Textures[0], Assert.Single(model.Textures));
        Assert.NotEqual(field.Models[5].Name, model.Name);
        Assert.All(model.Faces, face => Assert.Equal(0, face.Texture));
        Assert.Equal(model.Vertices.Count, model.Pose(0, 0).Length);
    }
}
