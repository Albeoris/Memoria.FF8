using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterWholeMonsterAtlasTests
{
    private static readonly TextureLayoutMetadata Whole = new(TextureLayoutMode.WholeNativeAtlas, 256, 256);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ADeclaredWholeBankUsesBothMaterialHalvesWithoutAnAnchorOrOddImage(int row)
    {
        ModFile file = Image(row & ~1, 768, 768, Whole);

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(file).Resolve(Context(row)));

        int sourceY = (row & 1) * 384;
        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal((128, 128), (replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal(Color(0, sourceY, 0), replacement.Image[0, 0]);
        Assert.Equal(Color(383, sourceY + 383, 0), replacement.Image[383, 383]);
        Assert.Equal($"{file.PackageId}/{file.RelativePath}@1", replacement.Provenance);
        Assert.Null(replacement.Variants);
    }

    [Fact]
    public void AWhole1536SquareImageProjectsA768SquareMaterialAtTheNativeHalfScale()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 1536, 1536, Whole)).Resolve(Context(1)));

        Assert.Equal((768, 768), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(384, 1152, 0), replacement.Image[384, 384]);
    }

    [Fact]
    public void TheDeclaredLayoutAlsoAppliesToAWinningPngFile()
    {
        using MagickImage image = new(MagickColors.Red, 512, 512);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        ModFile file = new("Fixture/Png", ModFormat.Demaster, System.IO.Path.ChangeExtension(Path(0), ".png"), 1, () => bytes) { TextureLayout = Whole };

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(file).Resolve(Context(1)));

        Assert.Equal((256, 256), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), replacement.Image[255, 255]);
    }

    [Fact]
    public void ASourceCropKeepsItsPositionInsideTheDeclaredWholeCanvas()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(2, 1024, 1024, Whole)).Resolve(Context(3, 16, 32, 32, 64)));

        Assert.Equal((128, 256), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(64, 640, 0), replacement.Image[0, 0]);
        Assert.Equal(Color(191, 895, 0), replacement.Image[127, 255]);
    }

    [Fact]
    public void AClaimedBankOwnsItsOddHalfEvenAgainstAHigherPriorityComponent()
    {
        ModFile odd = Unread(1, ".png", "Higher/Odd");
        ModFile bank = Image(0, 512, 512, Whole, "Lower/Bank");

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(odd, bank).Resolve(Context(1)));

        Assert.StartsWith("Lower/Bank/", replacement.Provenance);
        Assert.Equal((256, 256), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(0, 256, 0), replacement.Image[0, 0]);
    }

    [Fact]
    public void AClaimedBankDoesNotValidateAnInactiveOddDeclaration()
    {
        ModFile inactive = Unread(1, ".png", "Other/Odd", new(TextureLayoutMode.WholeNativeAtlas, 128, 128));

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(inactive, Image(0, 512, 512, Whole)).Resolve(Context(1)));

        Assert.Equal((256, 256), (replacement.Image.Width, replacement.Image.Height));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WithoutAWholeBankClaimAnUnsupportedOddDeclarationRetainsFailureOwnership(int queriedRow)
    {
        ModFile unsupported = Unread(1, ".png", "Unsupported/Odd", Whole);

        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, 512, 512), unsupported).Resolve(Context(queriedRow)));

        Assert.Equal(unsupported.PackageId, failure.PackageId);
        Assert.Equal(unsupported.RelativePath, failure.RelativePath);
    }

    [Fact]
    public void AWinningComponentFileDoesNotInheritALowerPriorityWholeDeclaration()
    {
        ModFile winning = Image(0, 512, 512, package: "Higher/Component");
        ModFile losing = Unread(0, ".dds", "Lower/Whole", Whole);
        ModFile odd = Image(1, 512, 512);

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(winning, losing, odd).Resolve(Context(1)));

        Assert.Equal((512, 512), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(0, 0, 0), replacement.Image[0, 0]);
        Assert.Equal($"{odd.PackageId}/{odd.RelativePath}@1", replacement.Provenance);
    }

    [Fact]
    public void DdsPrecedenceDoesNotBorrowAClaimFromAHigherPackagePng()
    {
        ModFile preferred = Image(0, 512, 512);
        ModFile shadowed = Unread(0, ".png", "Higher/Whole", Whole);

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(shadowed, preferred, Image(1, 512, 512)).Resolve(Context(0)));

        Assert.Equal((512, 512), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal($"{preferred.PackageId}/{preferred.RelativePath}@1", replacement.Provenance);
    }

    [Fact]
    public void ADeclaredDdsBankKeepsItsShadowedPngAndOddImageInactive()
    {
        ModFile preferred = Image(0, 512, 512, Whole, "Lower/Whole");
        ModFile shadowed = Unread(0, ".png", "Higher/Component");

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(shadowed, preferred, Unread(1, ".png", "Higher/Odd")).Resolve(Context(1)));

        Assert.Equal((256, 256), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal($"{preferred.PackageId}/{preferred.RelativePath}@1", replacement.Provenance);
    }

    [Fact]
    public void CorruptPreferredWholeImageRetainsFailureOwnershipWithoutComponentFallback()
    {
        ModFile corrupt = new("Broken/Bank", ModFormat.Demaster, Path(0), 1, () => [0]) { TextureLayout = Whole };
        ModFile png = Unread(0, ".png", "Other/Png", Whole);

        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(corrupt, png, Unread(1, ".png", "Other/Odd")).Resolve(Context(1)));

        Assert.Equal(corrupt.PackageId, failure.PackageId);
        Assert.Equal(corrupt.RelativePath, failure.RelativePath);
    }

    [Theory]
    [InlineData(128, 128)]
    [InlineData(256, 128)]
    [InlineData(512, 512)]
    public void TheDeclarationMustMatchTheSourceCanvasBeforePixelsAreRead(int width, int height)
    {
        ModFile file = Unread(0, ".dds", "Invalid/Canvas", new(TextureLayoutMode.WholeNativeAtlas, width, height));

        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(file).Resolve(Context(1)));

        Assert.Equal(file.RelativePath, failure.RelativePath);
    }

    [Theory]
    [InlineData(384, 384)]
    [InlineData(512, 256)]
    public void AWholeImageMustHaveOnePositiveIntegralScaleOverTheNativeCanvas(int width, int height)
    {
        Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, width, height, Whole)).Resolve(Context(1)));
    }

    [Fact]
    public void SuppliedMipPixelsKeepTheSameWholeCanvasProjection()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 512, 512, Whole, mipmaps: true)).Resolve(Context(1)));
        IReadOnlyList<PixelImage> mips = Assert.IsAssignableFrom<IReadOnlyList<PixelImage>>(replacement.MipLevels);

        Assert.Equal(9, mips.Count);
        Assert.Equal((128, 128), (mips[1].Width, mips[1].Height));
        Assert.Equal(Color(127, 255, 1), mips[1][127, 127]);
        Assert.Equal((1, 1), (mips[^1].Width, mips[^1].Height));
    }

    [Fact]
    public void AClaimForAnotherBankCannotAffectTheQueriedPair()
    {
        ModFile claimed = Image(0, 512, 512, Whole, "Other/Bank");
        ModFile component = Image(2, 512, 512);

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(claimed, component).Resolve(Context(2)));

        Assert.Equal((512, 512), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal($"{component.PackageId}/{component.RelativePath}@1", replacement.Provenance);
    }

    private static string Path(int row) => $"textures/battle.fs/hd_new/c0m777_{row}.dds";
    private static Rgba32 Color(int x, int y, int mip) => new((byte)(x % 251), (byte)(y % 251), (byte)(mip + 40), 255);
    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));

    private static ModFile Unread(int row, string extension, string package, TextureLayoutMetadata? layout = null) => new(package, ModFormat.Demaster, System.IO.Path.ChangeExtension(Path(row), extension), 1, () => throw new InvalidOperationException("Inactive image pixels must stay lazy.")) { TextureLayout = layout };

    private static TextureBuildContext Context(int row, int x = 0, int y = 0, int width = 128, int height = 128)
    {
        ResourceKey owner = new("battle/c0m777.dat");
        return new(new(owner, "texture/0"), new("fixture", GameDataEdition.Classic, ["native-battle-model-sheet"]), 1, "classic-battle", width, height, () => throw new InvalidOperationException("Original pixels stay lazy."))
        {
            NativeSheet = new(owner, $"row/{row}", 0, x, y, width, height, 128, 128)
        };
    }

    private static ModFile Image(int row, int width, int height, TextureLayoutMetadata? layout = null, string package = "Demaster/Fixture", bool mipmaps = false)
    {
        using MemoryStream stream = new();
        byte[] header = new byte[128];
        Write(0, 0x20534444);
        Write(4, 124);
        Write(12, (uint)height);
        Write(16, (uint)width);
        Write(28, mipmaps ? (uint)(1 + (int)Math.Floor(Math.Log2(Math.Max(width, height)))) : 1);
        Write(76, 32);
        Write(80, 0x41);
        Write(88, 32);
        Write(92, 0xFF);
        Write(96, 0xFF00);
        Write(100, 0xFF0000);
        Write(104, 0xFF000000);
        stream.Write(header);
        for (int level = 0; ; level++, width = Math.Max(1, width / 2), height = Math.Max(1, height / 2))
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rgba32 color = Color(x, y, level);
                    stream.Write([color.R, color.G, color.B, color.A]);
                }
            }
            if (!mipmaps || width == 1 && height == 1)
            {
                break;
            }
        }
        byte[] bytes = stream.ToArray();
        return new(package, ModFormat.Demaster, Path(row), 1, () => bytes) { TextureLayout = layout };

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset, 4), value);
        }
    }
}
