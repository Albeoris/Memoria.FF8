using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSiren95ResourcesTests
{
    [Fact]
    public void Original_selected_profile_supplies_its_complete_io_files_vibration_and_sound_descriptors()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([95])[94];
        Assert.Equal(0x739DA0u, binding.Entry);
        Assert.Equal(0x739D80u, binding.Initializer);
        NativeBattleSiren95Resources resources = Assert.IsType<NativeBattleSiren95Resources>(binding.Siren95Resources);
        Assert.Same(resources, game.ReadNativeSiren95Resources());
        resources.Validate();
        Assert.Equal(0x180, resources.IoScript.Length);
        Assert.Equal(Enumerable.Range(418, 20).Select(value => (short)value), resources.Files.Select(file => file.FileId));
        Assert.All(resources.Files, file => Assert.NotEmpty(file.Data));
        Assert.Equal(0x13A, resources.Vibration!.Bank.Bytes.Length);
        Assert.Equal((ushort)50, resources.Vibration.Bank.ReadWord(0x100));
        Assert.Equal((ushort)4, resources.Vibration.Bank.ReadWord(0x102));
        Assert.Equal(16, resources.SoundDescriptors!.Data.Length);
        Assert.NotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.SoundDescriptors.Data.AsSpan()));
        Assert.Equal(0x358, resources.CameraPrograms!.Data.Length);
        Assert.Equal(0x16A5380u, resources.PhaseOneSource!.Address);
        Assert.Equal(0x5A00, resources.PhaseOneSource.Data.Length);
        Assert.Equal(0x1AC, resources.PhaseOneSprites!.Data.Length);
        Assert.Equal(0xF8, resources.PhaseTwoSprites!.Data.Length);
        Assert.Equal((ushort)0xE0, BinaryPrimitives.ReadUInt16LittleEndian(resources.PhaseTwoSprites.Data.AsSpan().Slice(26)));
        Assert.Equal(0x1533E10u, resources.OverlayProgram!.Address);
        Assert.Equal(24, resources.OverlayProgram.Commands.Length);
        Assert.Equal(0xFF000000u, resources.OverlayProgram.Commands[^1]);
        string json = JsonSerializer.Serialize(resources);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleSiren95Resources>(json)));
    }
}
