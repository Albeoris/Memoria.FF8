using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureMetadataIntegrityTests
{
    [Theory]
    [InlineData("source")]
    [InlineData("original-pixels")]
    [InlineData("exported-pixels")]
    [InlineData("original-semantics")]
    [InlineData("exported-semantics")]
    [InlineData("coverage")]
    [InlineData("width")]
    [InlineData("height")]
    public void SidecarCannotSilentlyReplaceOverlappingEmbeddedSourceOrSemanticClaims(string field)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-metadata-conflict-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = ExportMetadataFixture(directory);
            TextureExchangeMetadata metadata = TextureExchange.ReadMetadata(path)!;
            string changed = new('B', 64);
            metadata = field switch
            {
                "source" => metadata with { SourceSha256 = changed },
                "original-pixels" => metadata with { OriginalPixelSha256 = changed },
                "exported-pixels" => metadata with { ExportedPixelSha256 = changed },
                "original-semantics" => metadata with { OriginalSemanticSha256 = changed },
                "exported-semantics" => metadata with { ExportedSemanticSha256 = changed },
                "coverage" => metadata with { NativeFlagsBase64 = "GA==" },
                "width" => metadata with { NativeWidth = 2 },
                "height" => metadata with { NativeHeight = 2 },
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            };
            File.WriteAllBytes(path + ".texture.json", metadata.ToBytes());
            Assert.Throws<InvalidDataException>(() => TextureExchange.ReadMetadata(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LegacyMissingFieldsMergeWithEmbeddedMetadataAndInformationalDifferencesRemainAllowed()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-metadata-legacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = ExportMetadataFixture(directory);
            TextureExchangeMetadata embedded = TextureExchange.ReadMetadata(path)!;
            TextureExchangeMetadata legacy = new(1, embedded.Target, embedded.Contract, embedded.SourceSha256!.ToLowerInvariant(), Provenance: "editor", OriginalPixelSha256: embedded.OriginalPixelSha256!.ToLowerInvariant())
            {
                SourceAliases = ["field/example/chara.one#model/0"]
            };
            File.WriteAllBytes(path + ".texture.json", legacy.ToBytes());
            TextureExchangeMetadata merged = TextureExchange.ReadMetadata(path)!;
            Assert.Equal(embedded.ExportedSemanticSha256, merged.ExportedSemanticSha256);
            Assert.Equal(embedded.OriginalSemanticSha256, merged.OriginalSemanticSha256);
            Assert.Equal(embedded.ExportedPixelSha256, merged.ExportedPixelSha256);
            Assert.Equal(embedded.NativeFlagsBase64, merged.NativeFlagsBase64);
            Assert.Equal(embedded.NativeWidth, merged.NativeWidth);
            Assert.Equal(embedded.NativeHeight, merged.NativeHeight);
            Assert.Equal("editor", merged.Provenance);
            Assert.Equal(legacy.SourceAliases, merged.SourceAliases);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImageOrCompanionCreatedDuringSourceDecodeSurvivesTheFinalAtomicRename(bool companion)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-export-race-" + Guid.NewGuid().ToString("N"));
        try
        {
            TextureKey key = new(new("battle/synthetic.dat"), "texture/0");
            string path = Path.Combine(directory, TextureTarget.FromKey(key).CanonicalPath().Replace('/', Path.DirectorySeparatorChar));
            string raced = companion ? path + ".texture.json" : path;
            byte[] sentinel = [1, 2, 3, 4];
            PixelImage image = new(1, 1, [new(1, 2, 3)]);
            TextureBuildContext context = new(key, new("synthetic", GameDataEdition.Classic, []), 1, "image", 1, 1, () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(raced)!);
                File.WriteAllBytes(raced, sentinel);
                return image;
            });
            Assert.Throws<IOException>(() => TextureExchange.Export([new TextureSourceSnapshot(context, new(image, 1, 1, "synthetic"), 0)], directory, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(sentinel, File.ReadAllBytes(raced));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string ExportMetadataFixture(string directory)
    {
        PixelImage image = new(1, 1, [new(1, 2, 3, 128)]);
        TextureBuildContext context = new(new(new("battle/synthetic.dat"), "texture/0"), new("synthetic", GameDataEdition.Classic, []), 1, "classic-battle", 1, 1, () => image)
        {
            AuthoringSourceSha256 = new string('A', 64)
        };
        return Assert.Single(TextureExchange.Export([new TextureSourceSnapshot(context, new(image, 1, 1, "synthetic"), 0)], directory, cancellationToken: TestContext.Current.CancellationToken).Files);
    }

    [Fact]
    public void FrozenSourceFingerprintSurvivesTheIndependentAuthoringReaderLifetime()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-frozen-source-" + Guid.NewGuid().ToString("N"));
        try
        {
            PixelImage image = new(1, 1, [new(1, 2, 3)]);
            ResourceKey owner = new("battle/synthetic.dat");
            ResourceContent encoded = new(owner, "synthetic", 1, "synthetic", () => throw new ObjectDisposedException("authoring-reader"));
            TextureBuildContext context = new(new(owner, "texture/0"), new("synthetic", GameDataEdition.Classic, []), 1, "image", 1, 1, () => image, encodedSource: encoded)
            {
                AuthoringSourceSha256 = new string('A', 64)
            };
            string path = Assert.Single(TextureExchange.Export([new TextureSourceSnapshot(context, new(image, 1, 1, "synthetic"), 0)], directory, cancellationToken: TestContext.Current.CancellationToken).Files);
            Assert.Equal(context.AuthoringSourceSha256, TextureExchange.ReadMetadata(path)!.SourceSha256);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

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
