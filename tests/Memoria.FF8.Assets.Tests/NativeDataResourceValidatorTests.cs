using System.Buffers.Binary;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.Imaging.Tex;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeDataResourceValidatorTests
{
    [Theory]
    [InlineData("main/init.out")]
    [InlineData("battle/scene.out")]
    [InlineData("menu/m000.bin")]
    [InlineData("menu/m001.bin")]
    [InlineData("menu/m002.bin")]
    [InlineData("menu/m003.bin")]
    [InlineData("menu/m004.bin")]
    [InlineData("menu/shop.bin")]
    [InlineData("menu/price.bin")]
    [InlineData("menu/mwepon.bin")]
    [InlineData("menu/mwepon.msg")]
    [InlineData("menu/mthomas.bin")]
    [InlineData("menu/mitem.bin")]
    [InlineData("main/kernel.bin")]
    [InlineData("battle/c0m000.dat")]
    [InlineData("battle/c0m127.dat")]
    [InlineData("battle/c0m143.dat")]
    [InlineData("main/namedic.bin")]
    [InlineData("menu/mngrp.bin")]
    [InlineData("menu/mngrphd.bin")]
    [InlineData("world/wmsetus.obj")]
    public void InstalledFamiliesReachTheirTypedContracts(string path)
    {
        RequireInstallation();
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        ResourceContent original = source.Find(new ResourceKey(path))!;
        Assert.NotNull(original);
        using NativeDataResourceValidator validator = new(resources);
        validator.Validate(original, original);
    }

    [Fact]
    public void EnabledFormationReferencesMustExistInTheSelectedSource()
    {
        using EmptySource source = new();
        using NativeDataResourceValidator validator = new(new ResourceService(source));
        ResourceKey key = new("battle/scene.out");
        byte[] bytes = new byte[1024 * 128];
        EncodedResource scenes = new(key, "out", 1, "fixture", bytes);
        validator.Validate(scenes.Content, null);
        bytes[7] = 0x80;
        bytes[56] = 16;
        scenes = new(key, "out", 2, "fixture", bytes);
        Assert.Throws<FileNotFoundException>(() => validator.Validate(scenes.Content, null));
        bytes[56] = 0;
        scenes = new(key, "out", 3, "fixture", bytes);
        Assert.Throws<InvalidDataException>(() => validator.Validate(scenes.Content, null));
    }

    [Theory]
    [InlineData("menu/m000.bin")]
    [InlineData("menu/shop.bin")]
    [InlineData("menu/price.bin")]
    [InlineData("menu/mwepon.bin")]
    [InlineData("menu/mwepon.msg")]
    [InlineData("main/init.out")]
    [InlineData("battle/scene.out")]
    public void TruncatedTypedFamiliesFailBeforePublication(string path)
    {
        using EmptySource source = new();
        ResourceService resources = new(source);
        using NativeDataResourceValidator validator = new(resources);
        EncodedResource content = new(new ResourceKey(path), "bin", 1, "fixture", [0]);
        Assert.Throws<InvalidDataException>(() => validator.Validate(content.Content, null));
        Assert.Equal(0, resources.Generation);
    }

    [Fact]
    public void DictionaryValidationIsPortableAndDoesNotOwnTheCandidateSource()
    {
        using EmptySource source = new();
        ResourceService resources = new(source);
        NativeDataResourceValidator validator = new(resources);
        EncodedResource dictionary = new(new ResourceKey("main/namedic.bin"), "bin", 1, "fixture", NamedicBin.WriteStrings(["First", "Second"], FF8Encoding.CreateEuropean()));
        validator.Validate(dictionary.Content, null);
        EncodedResource malformed = new(dictionary.Key, "bin", 2, "fixture", [2, 0, 0]);
        Assert.Throws<InvalidDataException>(() => validator.Validate(malformed.Content, null));
        validator.Dispose();
        Assert.False(source.Disposed);
        Assert.Throws<ObjectDisposedException>(() => validator.Validate(dictionary.Content, null));
    }

    [Theory]
    [InlineData("menu/mag00.tex")]
    [InlineData("menu/mag12.tex")]
    [InlineData("menu/mag99.tex")]
    public void DecodableMagazineTexWithWrongCanvasIsRejectedBeforePublication(string path)
    {
        using EmptySource source = new();
        ResourceService resources = new(source);
        using NativeDataResourceValidator validator = new(resources);
        ResourceKey key = new(path);
        EncodedResource original = Texture(key, 256, 256);
        EncodedResource malformed = Texture(key, 128, 128);
        Assert.Equal(128, TexFile.Read(Read(malformed.Content)).Decode().Width);
        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => validator.Validate(malformed.Content, original.Content));
        Assert.Contains("256 by 256", failure.Message);
        Assert.Equal(0, resources.Generation);
        validator.Validate(original.Content, original.Content);
    }

    [Fact]
    public void UnusedFontTexRetainsItsOwnValidBoundedCanvas()
    {
        using EmptySource source = new();
        using NativeDataResourceValidator validator = new(new ResourceService(source));
        ResourceKey key = new("menu/hires/sysfld00.tex");
        validator.Validate(Texture(key, 128, 128).Content, Texture(key, 256, 256).Content);
    }

    private static EncodedResource Texture(ResourceKey key, int width, int height)
    {
        TexHeader header = new()
        {
            Width = width,
            Height = height,
            Pitch = width,
            BitDepth = 8,
            BitsPerPixel = 8,
            BytesPerPixel = 1,
            BitsPerIndex = 8,
            PaletteFlags = TexPaletteFlags.HasPalette,
            PaletteCount = 1,
            ColorsPerPalette = 256,
            RepeatedColorsPerPalette = 256,
            PaletteSize = 256
        };
        TexFile texture = new(header, [Enumerable.Repeat(0xFF112233u, 256).ToArray()], new byte[width * height]);
        return new(key, "tex", 1, "fixture", texture.Write());
    }

    [Fact]
    public void SelectedKernelTextResizeUsesItsCurrentNativeRecordContracts()
    {
        RequireInstallation();
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceKey key = new("main/kernel.bin");
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        KernelData kernel = KernelData.ReadFromArray(Read(source.Find(key)!), encoding);
        kernel.MagicSpells[1].Name = "Selected longer spell label";
        EncodedResource selected = new(key, "bin", 1, "fixture", kernel.WriteToArray(encoding));
        ResourceService resources = Selected(source, selected);
        using NativeDataResourceValidator validator = new(resources);
        validator.Validate(selected.Content, source.Find(key));
    }

    [Fact]
    public void EnemyAbilityReferencesAreCheckedAgainstTheSelectedKernel()
    {
        RequireInstallation();
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceKey enemyKey = new("battle/c0m127.dat");
        ResourceKey kernelKey = new("main/kernel.bin");
        byte[] enemy = Read(source.Find(enemyKey)!);
        int information = BinaryPrimitives.ReadInt32LittleEndian(enemy.AsSpan(4));
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        KernelData kernel = KernelData.ReadFromArray(Read(source.Find(kernelKey)!), encoding);
        int last = kernel.EnemyAttacks.Count - 1;
        enemy[information + 52] = 8;
        enemy[information + 53] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(enemy.AsSpan(information + 54), checked((ushort)last));
        EncodedResource selectedEnemy = new(enemyKey, "dat", 1, "fixture", enemy);
        using (NativeDataResourceValidator valid = new(Selected(source, selectedEnemy)))
        {
            valid.Validate(selectedEnemy.Content, source.Find(enemyKey));
        }
        kernel.EnemyAttacks.RemoveAt(last);
        EncodedResource selectedKernel = new(kernelKey, "bin", 1, "fixture", kernel.WriteToArray(encoding));
        using NativeDataResourceValidator invalid = new(Selected(source, selectedEnemy, selectedKernel));
        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => invalid.Validate(selectedEnemy.Content, source.Find(enemyKey)));
        Assert.Contains("absent original kernel attack", failure.Message);
    }

    [Theory]
    [InlineData("enemy-directory")]
    [InlineData("enemy-information")]
    [InlineData("menu-pair")]
    [InlineData("world-messages")]
    [InlineData("world-interior-sentinel")]
    public void MalformedWinningFamilyIsRejectedBeforePublication(string defect)
    {
        RequireInstallation();
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceKey key = new(defect.StartsWith("enemy", StringComparison.Ordinal) ? "battle/c0m127.dat" : defect == "menu-pair" ? "menu/mngrphd.bin" : "world/wmsetus.obj");
        ResourceContent original = source.Find(key)!;
        byte[] bytes = Read(original);
        switch (defect)
        {
            case "enemy-directory":
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length + 1);
                break;
            case "enemy-information":
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) + 100);
                break;
            case "menu-pair":
                for (int slot = 0; slot < 256; slot++)
                {
                    if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(slot * 8 + 4)) != 0)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(slot * 8), 0);
                        break;
                    }
                }
                break;
            case "world-interior-sentinel":
                int text = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(13 * 4));
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(text + 4), 0);
                break;
            case "world-messages":
                int section = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(13 * 4));
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(section), bytes.Length);
                break;
        }
        EncodedResource selected = new(key, original.Format, 1, "fixture", bytes);
        using NativeDataResourceValidator validator = new(Selected(source, selected));
        Assert.Throws<InvalidDataException>(() => validator.Validate(selected.Content, original));
    }

    private static ResourceService Selected(IResourceSource source, params EncodedResource[] selected)
    {
        ResourceService resources = new(source);
        resources.Prepare(null, encodedResources: selected).Commit();
        return resources;
    }

    private static byte[] Read(ResourceContent content)
    {
        using Stream input = content.OpenRead();
        using MemoryStream bytes = new();
        input.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static void RequireInstallation() => Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native data resources are required.");

    private sealed class EmptySource : IResourceSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("memory", GameDataEdition.Classic, []);
        public bool Disposed { get; private set; }
        public ResourceContent? Find(ResourceKey key) => throw new InvalidOperationException("Dictionary validation does not require installed resources.");
        public void Dispose() => Disposed = true;
    }
}
