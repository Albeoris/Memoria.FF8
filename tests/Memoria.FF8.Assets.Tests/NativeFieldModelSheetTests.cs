using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldModelSheetTests
{
    [Fact]
    public void OriginalClassicModelsDoNotAdvertiseRemasteredMaterialAtlasCompatibility()
    {
        using ClassicGameDataSource source = new(Path.GetTempPath());
        Assert.DoesNotContain("native-field-model-sheet", source.Descriptor.Layouts);
    }

    [Theory]
    [InlineData(true, "main_chr")]
    [InlineData(false, "second_chr")]
    public void SourceOrdinalsAndAliasedNamesStayIndependentOfTimAllocation(bool main, string kind)
    {
        FieldModel model = Model() with { Name = "o099alias", TextureResourceName = "c021", MainCharacterTextures = main, Textures = [new(128, 96), new(128, 128)], TextureRegions = [new(640, 128, 8, 128, 96), new(704, 0, 8, 128, 128)] };
        TextureSourceAddress first = Address(model, 0)!;
        TextureSourceAddress second = Address(model, 1)!;
        Assert.Equal(new ResourceKey($"field/model/{kind}/c021"), first.Owner);
        Assert.Equal("row/0", first.Subresource);
        Assert.Equal("row/1", second.Subresource);
        Assert.Equal((0, 0, 128, 96, 128, 128), (first.X, first.Y, first.Width, first.Height, first.CanvasWidth, first.CanvasHeight));
        Assert.Null(Address(model with { TextureResourceName = null }, 0));
        Assert.Null(Address(model with { TextureSourceVariant = "unknown" }, 0));
        Assert.Null(Address(model with { TextureRegions = [new(0, 0, 8, 256, 128)] }, 0));
    }

    [Theory]
    [InlineData(0x36F, "alpha")]
    [InlineData(0x370, "alpha")]
    [InlineData(0x36E, null)]
    public void SourceAlternateMaterialRequiresItsOriginalFieldContext(int fieldId, string? expected)
    {
        Type type = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.NativeFieldModelTextureSheets")!;
        MethodInfo method = type.GetMethod("Variant")!;
        Assert.Equal(expected, method.Invoke(null, ["o006", fieldId]));
        Assert.Null(method.Invoke(null, ["o007", fieldId]));
        TextureSourceAddress address = Address(Model() with { TextureSourceVariant = expected }, 0)!;
        Assert.Equal(expected is null ? "row/0" : "row/0/alpha", address.Subresource);
    }

    [Fact]
    public void RegisteredEyePlanesFollowActualCopiedWordsAndPaletteAcrossRestore()
    {
        FieldModel model = Model();
        NativeFieldRegisteredTexture registered = new(1, 200, 150, 8, 8, 0, 400, 0, 0);
        Rgba32 openColor = new(23, 41, 79, 255);
        Rgba32 closedColor = new(71, 29, 19, 255);
        PixelImage native = new(16, 8);
        PixelImage open = new(32, 16, Enumerable.Repeat(openColor, 512).ToArray());
        PixelImage closed = new(32, 16, Enumerable.Repeat(closedColor, 512).ToArray());
        TextureValue source = new(open, 16, 8, "synthetic:planes", Variants: new Dictionary<string, PixelImage> { ["eyes-closed"] = closed }) { IsReplacement = true };
        byte[] expectedPalette = NativeFieldRegistrationMachine.ProjectPalette532A40(model.NativeTexturePalettes[0].AsSpan(), new byte[] { 100, 48, 48, 48, 0, 128, 128, 128 }, "d000"u8);
        Assert.NotEqual(model.NativeTexturePalettes[0].ToArray(), expectedPalette);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(registered.X, registered.Y, registered.WidthWords, registered.Height, model.NativeEyes!.Texture!.Words).Upload45BD30(registered.PaletteX, registered.PaletteY, 256, 1, expectedPalette);
        Assert.Same(open, Apply(state).Image);
        NativeEyeCopy copy = model.NativeEyes.Copy(true)!;
        state = Copy(state, copy);
        TextureValue selected = Apply(state);
        Assert.True(selected.IsReplacement);
        Assert.Same(source.Variants, selected.Variants);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                Assert.Equal(x is >= 8 and < 16 && y is >= 4 and < 8 ? closedColor : openColor, selected.Image[x, y]);
            }
        }
        NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(selected.Image.Pixels, Apply(restored).Image.Pixels);
        NativeBattleEffectTextureState unrelated = state.Upload45BD30(registered.X, registered.Y, 1, 1, [9, 9]);
        Assert.Same(native, Apply(unrelated).Image);
        Assert.False(Apply(unrelated).IsReplacement);
        NativeBattleEffectTextureState palette = state.Upload45BD30(registered.PaletteX, registered.PaletteY, 1, 1, [9, 9]);
        Assert.Same(native, Apply(palette).Image);
        Assert.Same(open, Apply(Copy(restored, model.NativeEyes.Copy(false)!)).Image);

        TextureValue Apply(NativeBattleEffectTextureState current)
        {
            Type type = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.NativeFieldModelTextureCopies")!;
            return (TextureValue)type.GetMethod("Apply")!.Invoke(null, [source, native, model, registered, current.ReadVram(), expectedPalette])!;
        }

        NativeBattleEffectTextureState Copy(NativeBattleEffectTextureState current, NativeEyeCopy rectangle) => current.EnqueueCopy505EB0((short)(registered.X + rectangle.SourceXWords), (short)(registered.Y + rectangle.SourceY), (short)rectangle.WidthWords, (short)rectangle.Height, (short)(registered.X + rectangle.DestinationXWords), (short)(registered.Y + rectangle.DestinationY)).Drain505D20().State;
    }

    [Fact]
    public void NativePaletteProjectionKeepsZeroAndUsesExactPcbClampingAndNameSelection()
    {
        byte[] palette = new byte[512];
        ushort[] input = [0, 31, 992, 0x8000, 0x7FFF, (ushort)(10 | 20 << 5 | 30 << 10)];
        for (int index = 0; index < input.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(index * 2), input[index]);
        }
        byte[] colors = [120, 120, 120, 120, 0, 128, 128, 128, 100, 48, 48, 49, 0, 144, 112, 136];
        byte[] neutral = NativeFieldRegistrationMachine.ProjectPalette532A40(palette, colors, "none"u8);
        Assert.Equal(new ushort[] { 0, 0x843F, 0x87E1, 0x8421, 0xFFFF, (ushort)(0x8000 | 10 | 20 << 5 | 30 << 10) }, Words(neutral));
        byte[] tinted = NativeFieldRegistrationMachine.ProjectPalette532A40(palette, colors, "d001extra"u8);
        Assert.Equal((ushort)(0x8000 | 12 | 18 << 5 | 31 << 10), BinaryPrimitives.ReadUInt16LittleEndian(tinted.AsSpan(10)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(tinted));
        colors[12] = 1;
        Assert.Equal(neutral, NativeFieldRegistrationMachine.ProjectPalette532A40(palette, colors, "d001"u8));
        colors[12] = 0;
        colors[13] = 127;
        colors[14] = 127;
        colors[15] = 127;
        Assert.Equal(neutral, NativeFieldRegistrationMachine.ProjectPalette532A40(palette, colors, "d001"u8));
        Assert.Equal(input, Words(palette));

        static ushort[] Words(byte[] bytes) => Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
    }

    [Fact]
    public void RegisteredAliasesProjectTheUploadDonorPaletteRatherThanTheAliasPcbEntry()
    {
        FieldModel model = Model();
        byte[] colors = [100, 48, 48, 49, 0, 144, 112, 136, 111, 48, 57, 57, 0, 112, 144, 120];
        NativeFieldModelSource member = new("synthetic", new string('A', 64), [0]);
        NativeFieldRegistrationResources source = new(114, "synthetic", new string('A', 64), new string('B', 64), member, [Entry("d001"), Entry("o099")], [])
        {
            Loader = new(44, 0, member, member with { Data = colors.ToImmutableArray() }, [], [])
        };
        NativeFieldRegisteredTexture texture = new(1, 200, 150, 8, 8, 0, 400, 0, 0);
        NativeFieldRegisteredResource donor = new(0, 0, 0, 0, 0, 1, new string('A', 64), 0, [texture]);
        NativeFieldRegisteredResource alias = new(1, 1, 0, 0, 0, 1, new string('B', 64), 0, [texture]);
        NativeFieldRegistrationState registration = new(1, 114, new string('A', 64), new string('B', 64), 0, 0, 0, 0, [alias, donor], []);
        Type type = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.NativeFieldModelTextureCopies")!;
        byte[] actual = (byte[])type.GetMethod("ProjectPalette")!.Invoke(null, [model, source, registration, texture])!;
        Assert.Equal(NativeFieldRegistrationMachine.ProjectPalette532A40(model.NativeTexturePalettes[0].AsSpan(), colors, "d001"u8), actual);
        Assert.NotEqual(NativeFieldRegistrationMachine.ProjectPalette532A40(model.NativeTexturePalettes[0].AsSpan(), colors, "o099"u8), actual);

        static NativeFieldOneModelEntry Entry(string name) => new(0, 0, 0, 0, [], name) { RawName = Encoding.ASCII.GetBytes(name.PadRight(8, '\0')).ToImmutableArray() };
    }

    private static TextureSourceAddress? Address(FieldModel model, int ordinal)
    {
        Type type = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.NativeFieldModelTextureSheets")!;
        return (TextureSourceAddress?)type.GetMethod("Address")!.Invoke(null, [model, ordinal]);
    }

    [Fact]
    public void MainLibraryMaterialTablesDecodeOriginalMaskedPointersWithoutGeometryOrAFieldPlacement()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(0x20000010u);
        writer.Write(0x100002B0u);
        writer.Write(uint.MaxValue);
        writer.Write(1360);
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            writer.Write(16);
            writer.Write(9);
            writer.Write(524);
            writer.Write((ushort)0);
            writer.Write((ushort)(224 + ordinal));
            writer.Write((ushort)256);
            writer.Write((ushort)1);
            for (int index = 0; index < 256; index++)
            {
                writer.Write((ushort)(index == 1 ? 31 : index == 2 ? 992 : 0));
            }
            writer.Write(140);
            writer.Write((ushort)640);
            writer.Write((ushort)(ordinal == 0 ? 128 : 0));
            writer.Write((ushort)64);
            writer.Write((ushort)1);
            writer.Write(Enumerable.Repeat((byte)(ordinal + 1), 128).ToArray());
        }
        Assert.Equal(1360, stream.Length);
        Type reader = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.FieldModelReader")!;
        FieldModel model = (FieldModel)reader.GetMethod("ReadMainCharacterMaterials", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, ["d026", stream.ToArray()])!;
        Assert.True(model.MainCharacterTextures);
        Assert.Equal("d026", model.TextureResourceName);
        Assert.Empty(model.Vertices);
        Assert.Empty(model.Animations);
        Assert.Equal(2, model.Textures.Count);
        Assert.Equal(Rgba32.FromPsxBgr555(31), model.Textures[0][0, 0]);
        Assert.Equal(Rgba32.FromPsxBgr555(992), model.Textures[1][0, 0]);
        Assert.Equal(new[] { 128, 0 }, model.TextureRegions.Select(region => region.Y));
        Assert.Equal(new[] { "row/0", "row/1" }, Enumerable.Range(0, 2).Select(ordinal => Address(model, ordinal)!.Subresource));
        Assert.Equal((ushort)31, BinaryPrimitives.ReadUInt16LittleEndian(model.NativeTexturePalettes[0].AsSpan().Slice(2)));
    }

    private static FieldModel Model()
    {
        byte[] words = Enumerable.Range(0, 64).SelectMany(_ => new byte[] { 1, 1 }).ToArray();
        for (int y = 6; y < 8; y++)
        {
            for (int x = 4; x < 6; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan((y * 8 + x) * 2), 0x0202);
            }
        }
        byte[] bank = new byte[20];
        bank[2] = 8;
        bank[3] = 4;
        bank[4] = 2;
        bank[5] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(12), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(14), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(16), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(18), 6);
        byte[] palette = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(4), 992);
        return new("d000", [], [], [], [new(16, 8)], [])
        {
            TextureResourceName = "d000",
            MainCharacterTextures = true,
            TextureRegions = [new(640, 128, 8, 16, 8)],
            NativeTexturePalettes = [palette.ToImmutableArray()],
            NativeEyes = new(new string('A', 64), 0, 0, bank, new(1, 8, 8, words))
        };
    }
}
