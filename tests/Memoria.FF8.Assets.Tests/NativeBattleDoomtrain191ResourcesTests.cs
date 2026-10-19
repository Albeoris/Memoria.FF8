using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDoomtrain191ResourcesTests
{
    [Fact]
    public void Original_profile_supplies_complete_embedded_TIM_camera_vibration_and_all_seven_timeline_files()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([191])[190];
        Assert.Equal(0x63E730u, binding.Entry);
        Assert.Equal(0x575560u, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        NativeBattleDoomtrain191Resources resources = Assert.IsType<NativeBattleDoomtrain191Resources>(binding.Doomtrain191Resources);
        Assert.Same(resources, game.ReadNativeDoomtrain191Resources());
        resources.Validate();
        Assert.Equal(0x2D120, resources.ModelTim.Data.Length);
        Assert.Equal(0x162, resources.CameraProgram.Data.Length);
        Assert.Equal((ushort)0x8000, BinaryPrimitives.ReadUInt16LittleEndian(resources.CameraProgram.Data.AsSpan().Slice(0x160)));
        Assert.Equal(144, resources.InitialStream);
        Assert.NotNull(resources.OpeningRigs);
        Assert.Equal(new[] { 6332, 2104, 4244, 1320 }, new[] { resources.OpeningRigs.First.Bytes.Length, resources.OpeningRigs.Second.Bytes.Length, resources.OpeningRigs.Wheel.Bytes.Length, resources.OpeningRigs.Steam.Bytes.Length });
        Assert.Equal(700, resources.OpeningRigs.Sprite.Bytes.Length);
        Assert.NotNull(resources.Sky);
        Assert.Equal(64, resources.Sky.RainColors!.Value.Length);
        Assert.Equal(0x2E101010u, resources.Sky.RainColors.Value[0]);
        Assert.Equal(0x26B8, resources.Sky.Source.Bytes.Length);
        Assert.Equal(0x590, resources.Sky.TargetVertices.Data.Length);
        Assert.Equal(32, resources.Sky.Palette.Length);
        Assert.Equal(0x4040A0u, resources.Sky.Palette[0]);
        Assert.Equal(0x136, resources.Vibration.Bank.Bytes.Length);
        Assert.Equal((ushort)46, resources.Vibration.Bank.ReadWord(0x100));
        Assert.Equal(Enumerable.Range(0x22B, 7).Select(id => (short)id), resources.Files.Select(file => file.FileId));
        Assert.All(resources.Files, file => Assert.NotEmpty(file.Data));
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleDoomtrain191Resources>(JsonSerializer.Serialize(resources))));
    }
}
