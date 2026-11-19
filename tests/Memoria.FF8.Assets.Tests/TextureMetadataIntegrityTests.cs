using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureMetadataIntegrityTests
{
    [Fact]
    public void InvalidEmbeddedIdentityChecksumDoesNotFallBackToCanonicalName()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-metadata-integrity-" + Guid.NewGuid().ToString("N"));
        try
        {
            PixelImage image = new(1, 1, [new(1, 2, 3)]);
            TextureBuildContext context = new(new(new("battle/synthetic.dat"), "texture/0"), new("synthetic", GameDataEdition.Classic, []), 1, "image", 1, 1, () => image);
            string path = Assert.Single(TextureExchange.Export([new TextureSourceSnapshot(context, new(image, 1, 1, "synthetic"), 0)], directory, cancellationToken: TestContext.Current.CancellationToken).Files);
            byte[] png = File.ReadAllBytes(path);
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33, 4));
            png[33 + 8 + length] ^= 1;
            File.WriteAllBytes(path, png);
            Assert.Throws<InvalidDataException>(() => TextureExchange.ReadMetadata(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void UsageAliasesRemainPortableInformationalMetadata()
    {
        TextureExchangeMetadata metadata = new(1, new("ff8", "field/model/main_chr/d000", "row/0"), new("native-field-model-sheet", 128, 128))
        {
            SourceAliases = ["field/example/chara.one#model/0", "field/second/chara.one#model/2"]
        };
        Assert.Equal(metadata.SourceAliases, TextureExchangeMetadata.Parse(metadata.ToBytes()).SourceAliases);
        Assert.Throws<InvalidDataException>(() => (metadata with { SourceAliases = ["C:/private/editor"] }).ToBytes());
        Assert.Throws<InvalidDataException>(() => (metadata with { SourceAliases = ["../other/source"] }).ToBytes());
    }
}
