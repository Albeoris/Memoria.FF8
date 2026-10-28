using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterMonsterPartialPlaneTests
{
    [Theory]
    [InlineData(384, 192)]
    [InlineData(384, 144)]
    [InlineData(4, 4)]
    [InlineData(383, 384)]
    public void ActualUploadRectangleIsCopiedWithoutStretchIntoTheRowZeroPlane(int width, int height)
    {
        DemasterTextureResolver resolver = Resolver(Image(0, 384, 384), Image(1, width, height));

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(1)));

        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal((128, 128), (replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal(Color(width - 1, height - 1, 0), replacement.Image[width - 1, height - 1]);
        Assert.Equal(width == 384 && height == 384 ? Color(383, 383, 0) : default, replacement.Image[383, 383]);
        Assert.Null(replacement.Variants);
        Assert.Null(replacement.Animation);
    }

    [Fact]
    public void APartialRowZeroImageAnchorsItsOwnSquareAllocationByWidth()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 192)).Resolve(Context(0)));

        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(383, 191, 0), replacement.Image[383, 191]);
        Assert.Equal(default, replacement.Image[383, 192]);
    }

    [Fact]
    public void NativeCropsKeepTheirAnchoredCoordinatesAcrossUploadedAndTransparentPixels()
    {
        TextureBuildContext context = Context(1, 16, 32, 64, 64);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(1, 192, 144)).Resolve(context));

        Assert.Equal((192, 192), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(48, 96, 0), replacement.Image[0, 0]);
        Assert.Equal(Color(191, 143, 0), replacement.Image[143, 47]);
        Assert.Equal(default, replacement.Image[144, 47]);
        Assert.Equal(default, replacement.Image[143, 48]);
    }

    [Fact]
    public void SuppliedMipPixelsKeepTheirOriginalPhysicalRectanglesAndPadding()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(1, 384, 192, mipmaps: true)).Resolve(Context(1)));

        IReadOnlyList<PixelImage> mips = Assert.IsAssignableFrom<IReadOnlyList<PixelImage>>(replacement.MipLevels);
        Assert.Equal((384, 384), (mips[0].Width, mips[0].Height));
        Assert.Equal((192, 192), (mips[1].Width, mips[1].Height));
        Assert.Equal(Color(191, 95, 1), mips[1][191, 95]);
        Assert.Equal(default, mips[1][191, 96]);
    }

    [Theory]
    [InlineData(769, 384)]
    [InlineData(384, 385)]
    public void AnUploadCannotEscapeItsSourceOwnersAnchoredPlane(int width, int height)
    {
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, 384, 384), Image(1, width, height)).Resolve(Context(1)));

        Assert.Equal(Path(1), failure.RelativePath);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(383)]
    public void AnAnchorMustHavePositiveIntegralNativePlaneScale(int width)
    {
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, width, width), Image(1, 4, 4)).Resolve(Context(1)));

        Assert.Equal(Path(0), failure.RelativePath);
    }

    [Fact]
    public void APartialUploadCannotBorrowAnotherOwnersAnchor()
    {
        ModFile anchor = Image(0, 384, 384);
        ModFile foreign = new(anchor.PackageId, anchor.Format, "textures/battle.fs/hd_new/c0m778_0.dds", anchor.Revision, anchor.ReadBytes);
        Assert.Throws<ModTextureLoadException>(() => Resolver(foreign, Image(1, 384, 192)).Resolve(Context(1)));
    }

    [Fact]
    public void PreferredDdsAnchorKeepsItsShadowedPngInactive()
    {
        ModFile inactive = new("Demaster/Fixture", ModFormat.Demaster, Path(0).Replace(".dds", ".png"), 1, () => throw new InvalidOperationException("A shadowed anchor must not be decoded."));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(inactive, Image(0, 384, 384), Image(1, 4, 4)).Resolve(Context(1)));

        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(3, 3, 0), replacement.Image[3, 3]);
        Assert.Equal(default, replacement.Image[4, 3]);
    }

    [Fact]
    public void AnUploadCanCrossThePlaneBoundaryWhileFittingTheWholeNativeAtlas()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(1, 768, 288)).Resolve(Context(1)));

        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(383, 287, 0), replacement.Image[383, 287]);
        Assert.Equal(default, replacement.Image[383, 288]);
    }

    [Fact]
    public void OrderedPairCopiesPreserveAnEarlierUploadsPixelsOutsideTheLaterRectangle()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(2, 768, 576), Image(3, 192, 144)).Resolve(Context(3)));

        Assert.Equal(Color(191, 143, 0), replacement.Image[191, 143]);
        Assert.Equal(Color(192, 384 + 143, 0), replacement.Image[192, 143]);
        Assert.Equal(Color(191, 384 + 144, 0), replacement.Image[191, 144]);
        Assert.Equal(Color(383, 575, 0), replacement.Image[383, 191]);
        Assert.Equal(default, replacement.Image[383, 192]);
    }

    [Fact]
    public void ACompanionOutsideTheSelectedBanksPairCannotContributePixels()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(1, 768, 384), Image(2, 384, 384), Image(3, 4, 4)).Resolve(Context(3)));

        Assert.Equal(Color(3, 3, 0), replacement.Image[3, 3]);
        Assert.Equal(default, replacement.Image[4, 3]);
    }

    [Theory]
    [InlineData(2, 768, 576)]
    [InlineData(3, 768, 288)]
    public void AProvidedRowsOwnQueryDoesNotNeedADisjointStockCompanion(int row, int width, int height)
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(row, width, height)).Resolve(Context(row)));

        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(383, Math.Min(383, height - 1), 0), replacement.Image[383, Math.Min(383, height - 1)]);
    }

    [Fact]
    public void ACopyOverlappingAnUnprovidedMaterialRequiresItsOriginalCompanionPixels()
    {
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, 384, 384), Image(2, 768, 576)).Resolve(Context(3)));

        Assert.Equal(Path(2), failure.RelativePath);
        Assert.Null(Resolver(Image(0, 384, 384), Image(2, 384, 384)).Resolve(Context(3)));
    }

    private static string Path(int row) => $"textures/battle.fs/hd_new/c0m777_{row}.dds";
    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));
    private static Rgba32 Color(int x, int y, int mip) => new((byte)(x % 251), (byte)(y % 251), (byte)(mip + 50), 255);

    private static TextureBuildContext Context(int row, int x = 0, int y = 0, int width = 128, int height = 128)
    {
        ResourceKey owner = new("battle/c0m777.dat");
        return new(new(owner, "texture/0"), new("fixture", GameDataEdition.Classic, ["native-battle-model-sheet"]), 1, "classic-battle", width, height, () => throw new InvalidOperationException("Original pixels stay lazy."))
        {
            NativeSheet = new(owner, $"row/{row}", 0, x, y, width, height, 128, 128)
        };
    }

    private static ModFile Image(int row, int width, int height, bool mipmaps = false)
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
        return new("Demaster/Fixture", ModFormat.Demaster, Path(row), 1, () => bytes);

        void Write(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset, 4), value);
        }
    }
}
