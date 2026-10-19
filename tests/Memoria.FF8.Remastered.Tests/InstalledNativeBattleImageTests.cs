using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledNativeBattleImageTests
{
    [Theory]
    [InlineData(0, 0, "d0c000.dat")]
    [InlineData(5, 28, "d5c011.dat")]
    public void NativeLinkedWeaponAllocationsSampleTheResolvedHdStrip(int characterId, int weaponId, string body)
    {
        using BattleAssets assets = new(InstalledDirectory());
        BattleModel weapon = assets.LoadWeapon(characterId, weaponId, assets.LoadModel(body));
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(weapon.NativeWeaponTexturePack!, unchecked((ushort)~(1 << 12)));
        NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
        IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(weapon, load.Binding, true);
        NativeBattleTextureReplacement replacement = Assert.Single(replacements);
        Assert.Equal(224, replacement.V);
        Assert.Equal(32, replacement.Height);
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(TextureRow(replacement.Page), new(replacement.U, replacement.V, replacement.Width, replacement.Height), state);
        PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, replacement.Page, replacement.Clut, replacement.U, replacement.V, replacement.Width, replacement.Height, pixelWords: retained);
        PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, replacement.Page, replacement.Clut, replacement.U, replacement.V, retained, replacements);
        Assert.Equal(replacement.Image.Pixels, selected.Pixels);
        Assert.Equal(96, selected.Height);
    }

    [Theory]
    [InlineData("d0c000.dat", 0)]
    [InlineData("c0m007.dat", 63)]
    [InlineData("c0m084.dat", 63)]
    public void NativeRelocatedAndRetainedTextureWindowsSampleTheirResolvedHdMaterials(string name, ushort blockedSlots)
    {
        using BattleAssets assets = new(InstalledDirectory());
        BattleModel model = assets.LoadModel(name);
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, blockedSlots);
        NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
        IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
        Assert.Equal(model.Textures.Count, replacements.Count);
        foreach (NativeBattleTextureReplacement replacement in replacements)
        {
            PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, replacement.Page, replacement.Clut, replacement.U, replacement.V, replacement.Width, replacement.Height);
            PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, replacement.Page, replacement.Clut, replacement.U, replacement.V, null, replacements);
            Assert.Equal(replacement.Image.Pixels, selected.Pixels);
            int row = TextureRow(replacement.Page);
            NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(row, new(replacement.U + 16, replacement.V + 16, 32, 32), state);
            PixelImage croppedOriginal = NativeBattleEffectTextureReader.DecodePcRegion(state, replacement.Page, replacement.Clut, replacement.U + 16, replacement.V + 16, 32, 32, pixelWords: retained);
            PixelImage cropped = NativeBattleEffectTextureReader.ApplyModelReplacements(croppedOriginal, state, replacement.Page, replacement.Clut, replacement.U + 16, replacement.V + 16, retained, replacements);
            Assert.Equal(96, cropped.Width);
            Assert.Equal(96, cropped.Height);
            Assert.Equal(replacement.Image[48, 48], cropped[0, 0]);
        }
    }

    [Fact]
    public void NativePixelAndPaletteWritesKeepTheirOriginalOwnership()
    {
        using BattleAssets assets = new(InstalledDirectory());
        BattleModel model = assets.LoadModel("c0m007.dat");
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, 63);
        NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
        IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
        NativeBattleTextureReplacement replacement = replacements[0];
        short x = checked((short)((replacement.Page & 15) * 64 + replacement.U / 2));
        short y = checked((short)((replacement.Page & 16) * 16 + replacement.V));
        byte[] changed = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(changed, (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(replacement.PixelWords.AsSpan()) ^ 1));
        NativeBattleEffectTextureState changedState = state.Upload45BD30(x, y, 1, 1, changed);
        PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(changedState, replacement.Page, replacement.Clut, replacement.U, replacement.V, replacement.Width, replacement.Height);
        Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, changedState, replacement.Page, replacement.Clut, replacement.U, replacement.V, null, replacements));
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(TextureRow(replacement.Page), new(replacement.U, replacement.V, replacement.Width, replacement.Height), state);
        PixelImage retainedImage = NativeBattleEffectTextureReader.ApplyModelReplacements(original, changedState, replacement.Page, replacement.Clut, replacement.U, replacement.V, retained, replacements);
        Assert.Equal(replacement.Image.Pixels, retainedImage.Pixels);
        short paletteX = checked((short)((replacement.Clut & 63) * 16));
        short paletteY = checked((short)((replacement.Clut & 0x7FFF) >> 6));
        BinaryPrimitives.WriteUInt16LittleEndian(changed, (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(replacement.PaletteWords.AsSpan()) ^ 1));
        NativeBattleEffectTextureState changedPalette = state.Upload45BD30(paletteX, paletteY, 1, 1, changed);
        Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, changedPalette, replacement.Page, replacement.Clut, replacement.U, replacement.V, retained, replacements));
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        return directory!;
    }

    private static int TextureRow(ushort page) => (page & 31) + ((page >> 7) & 3) * 32;

    [Fact]
    public void EveryNativeMappedMonsterMaterialMatchesItsAllocatedVramAndRetainedWindow()
    {
        string directory = InstalledDirectory();
        using BattleAssets assets = new(directory);
        using FileStream file = File.OpenRead(Path.Combine(directory, "FFVIII_EFIGS.dll"));
        using PEReader image = new(file);
        byte[] records = image.GetSectionData(checked((int)(0x117733D8u - image.PEHeaders.PEHeader!.ImageBase))).GetContent(0, 121 * 32).ToArray();
        int models = 0;
        int materials = 0;
        for (int index = 0; index < 121; index++)
        {
            int identity = BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan(index * 32 + 8));
            int part = BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan(index * 32 + 12));
            if (identity < 1000 || part != -1)
            {
                continue;
            }
            BattleModel model = assets.LoadModel($"c0m{identity - 1000:000}.dat");
            NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, 63);
            NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
            IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
            Assert.Equal(model.Textures.Count, replacements.Count);
            foreach (NativeBattleTextureReplacement replacement in replacements)
            {
                NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(TextureRow(replacement.Page), new(replacement.U, replacement.V, replacement.Width, replacement.Height), state);
                PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, replacement.Page, replacement.Clut, replacement.U, replacement.V, replacement.Width, replacement.Height, pixelWords: retained);
                PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, replacement.Page, replacement.Clut, replacement.U, replacement.V, retained, replacements);
                Assert.Equal(replacement.Image.Pixels, selected.Pixels);
                materials++;
            }
            models++;
        }
        Assert.Equal(72, models);
        Assert.Equal(175, materials);
    }
}
