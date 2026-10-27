using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldPalettePolicyTests
{
    [Fact]
    public void AbsentPcbIsExplicitAndRoundTripsWithoutChangingPresentSources()
    {
        NativeFieldRegistrationLoaderInputs absent = Fixture(null).Loader!;
        string absentJson = JsonSerializer.Serialize(absent);
        Assert.Contains("\"Colors\":null", absentJson);
        NativeFieldRegistrationLoaderInputs restored = JsonSerializer.Deserialize<NativeFieldRegistrationLoaderInputs>(absentJson)!;
        restored.Validate(absent.Program.Sha256);
        Assert.Null(restored.Colors);
        JsonObject omitted = JsonNode.Parse(absentJson)!.AsObject();
        omitted.Remove("Colors");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeFieldRegistrationLoaderInputs>(omitted.ToJsonString()));
        NativeFieldRegistrationLoaderInputs present = Fixture(Source("sample.pcb", [0, 0, 0, 0, 0, 128, 128, 128])).Loader!;
        string presentJson = JsonSerializer.Serialize(present);
        NativeFieldRegistrationLoaderInputs presentRestored = JsonSerializer.Deserialize<NativeFieldRegistrationLoaderInputs>(presentJson)!;
        presentRestored.Validate(present.Program.Sha256);
        Assert.Equal(presentJson, JsonSerializer.Serialize(presentRestored));
    }

    [Fact]
    public void AbsentPcbPreservesEveryNativeWordIncludingTransparentAndStpValues()
    {
        byte[] palette = Palette();
        Assert.Equal(palette, NativeFieldRegistrationMachine.ProjectPalette532A40(palette, "unit"u8, null));
        NativeFieldModelSource neutral = Source("sample.pcb", [0, 0, 0, 0, 0, 128, 128, 128]);
        byte[] projected = NativeFieldRegistrationMachine.ProjectPalette532A40(palette, "unit"u8, neutral);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(projected));
        Assert.Equal((ushort)0x8421, BinaryPrimitives.ReadUInt16LittleEndian(projected.AsSpan(2)));
        Assert.NotEqual(palette, projected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void MalformedPresentPcbDoesNotBecomeAbsent(int length)
    {
        NativeFieldModelSource malformed = Source("sample.pcb", new byte[length]);
        Assert.Throws<InvalidDataException>(() => Fixture(malformed).Validate());
        Assert.Throws<InvalidDataException>(() => NativeFieldRegistrationMachine.ProjectPalette532A40(Palette(), "unit"u8, malformed));
    }

    [Fact]
    public void ActualRegistrationUploadsUnmodifiedPaletteWithoutConsumingFakePcbBytes()
    {
        NativeFieldRegistrationResources source = Fixture(null);
        NativeFieldRetainedActor[] actors = [new(0, 0, 0, 1)];
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        NativeFieldRegistrationStep absent = NativeFieldRegistrationMachine.ReloadKind3(source, actors, cold);
        byte[] palette = Palette();
        Assert.Equal(NativeFieldRegistrationMachine.FieldArena + 56, absent.State.PreModelAddress);
        Assert.Equal(2, absent.Uploads.Length);
        Assert.Equal(palette, absent.Uploads[1].Bytes.ToArray());
        byte[] actualPalette = new byte[512];
        absent.Memory.EffectMemory!.Textures!.ReadVramInto(2 * (240 * 1024 + 512), actualPalette);
        Assert.Equal(palette, actualPalette);
        NativeFieldRegisteredResource resource = Assert.Single(absent.State.Resources);
        Assert.Equal(palette, absent.Memory.FileStaging!.Read(resource.GeometryAddress + 76 + 20, 512));
        NativeFieldRegistrationStep restored = NativeFieldRegistrationMachine.ReloadKind3(JsonSerializer.Deserialize<NativeFieldRegistrationResources>(JsonSerializer.Serialize(source))!, actors, cold);
        NativeFieldRegistrationStep absentSnapshot = absent with { Memory = new NativeFieldEyeProcess(1, absent.Memory, null).CaptureSnapshot().Memory };
        NativeFieldRegistrationStep restoredSnapshot = restored with { Memory = new NativeFieldEyeProcess(1, restored.Memory, null).CaptureSnapshot().Memory };
        Assert.Equal(JsonSerializer.Serialize(absentSnapshot), JsonSerializer.Serialize(restoredSnapshot));

        NativeFieldModelSource neutral = Source("sample.pcb", [0, 0, 0, 0, 0, 128, 128, 128]);
        NativeFieldRegistrationStep present = NativeFieldRegistrationMachine.ReloadKind3(Fixture(neutral), actors, cold);
        Assert.Equal(absent.State.PreModelAddress + 8, present.State.PreModelAddress);
        Assert.Equal(NativeFieldRegistrationMachine.ProjectPalette532A40(palette, "unit"u8, neutral), present.Uploads[1].Bytes.ToArray());
        Assert.Equal(palette, source.One.Data.AsSpan().Slice(4 + 76 + 20, 512).ToArray());
    }

    [Fact]
    public void Cold_registration_creates_working_field_ownership_without_changing_the_cold_template()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        string original = JsonSerializer.Serialize(cold);
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.ReloadKind3(Fixture(null), [new(0, 0, 0, 1)], cold);
        Assert.False(registered.Memory.FileStaging!.IsSnapshot);
        Assert.False(((INativeBattleStorageNode)registered.Memory.EffectMemory!.Textures!).IsSnapshot);
        Assert.True(cold.FileStaging!.IsSnapshot);
        NativeFieldEyeProcess captured = new NativeFieldEyeProcess(1, registered.Memory, NativeCrtRandomState.FromColdProcess()).CaptureSnapshot();
        string snapshot = JsonSerializer.Serialize(captured);
        NativeFieldEyeProcess restored = JsonSerializer.Deserialize<NativeFieldEyeProcess>(snapshot)!.RestoreOwned();
        NativeBattleEffectTextureState before = captured.Memory.EffectMemory!.Textures!;
        ushort originalWord = before.ReadVramWord(0, 0);
        ushort nextWord = (ushort)(originalWord ^ 0xFF);
        NativeBattleEffectTextureState updated = restored.Memory.EffectMemory!.Textures!.Upload45BD30(0, 0, 1, 1, [unchecked((byte)nextWord), (byte)(nextWord >> 8)]);
        Assert.Equal(nextWord, updated.ReadVramWord(0, 0));
        Assert.Equal(originalWord, before.ReadVramWord(0, 0));
        Assert.Equal(snapshot, JsonSerializer.Serialize(captured));
        Assert.Equal(original, JsonSerializer.Serialize(cold));
    }

    private static NativeFieldModelSource Source(string member, byte[] bytes) => new(member, Convert.ToHexString(SHA256.HashData(bytes)), bytes.ToImmutableArray());

    private static byte[] Palette()
    {
        byte[] result = new byte[512];
        for (int word = 0; word < 256; word++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(word * 2), (ushort)(word * 127));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), 0x8000);
        return result;
    }

    private static NativeFieldRegistrationResources Fixture(NativeFieldModelSource? colors)
    {
        byte[] program = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(6), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(10), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(12), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(16), 6);
        byte[] one = new byte[4 + 76 + 546];
        BinaryPrimitives.WriteUInt32LittleEndian(one, 1);
        Span<byte> model = one.AsSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(model[56..], 64);
        BinaryPrimitives.WriteUInt16LittleEndian(model[64..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(model[66..], 1);
        Span<byte> texture = model[76..];
        BinaryPrimitives.WriteUInt32LittleEndian(texture, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[4..], 9);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[8..], 524);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[16..], 256);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[18..], 1);
        Palette().CopyTo(texture[20..]);
        BinaryPrimitives.WriteUInt32LittleEndian(texture[532..], 14);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[540..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[542..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(texture[544..], 0x1234);
        NativeFieldModelSource code = Source("sample.jsm", program);
        NativeFieldOneModelEntry entry = new(4, one.Length - 4, 0, 0, [76], "unit") { SourceWord8 = 0, RawName = [117, 110, 105, 116, 0, 0, 0, 0], RawTextureWords = [76] };
        return new(72, "sample", NativeFieldRuntimeProfile.FF8EnExecutableHash, code.Sha256, Source("chara.one", one), [entry], [])
        {
            Loader = new(44, 0, code, colors, [0x00F00200], [0x008003C0])
        };
    }
}
