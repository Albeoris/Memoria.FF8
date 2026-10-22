using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMasamuneResourcesTests
{
    [Fact]
    public void Original_Masamune_resources_select_the_actual_camera_vibration_and_short_file()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Masamune.");
        using GameAssets game = new(directory!);
        NativeBattleGilgameshEffectResources resources = game.ReadNativeGilgameshEffectResources(328);
        Assert.Same(resources, game.ReadNativeGilgameshEffectResources(328));
        resources.Files.Validate();
        resources.Director.Validate();
        Assert.Equal((ushort)328, resources.Files.Effect);
        Assert.Equal("FF8_EN:CD069C", resources.Director.CameraProgram.Identity);
        Assert.Equal(234, resources.Director.CameraProgram.Data.Length);
        Assert.Equal(332, resources.Director.Vibration.Bytes.Length);
        Assert.Equal((ushort)66, resources.Director.Vibration.ReadWord(0x100));
        Assert.Equal((ushort)4, resources.Director.Vibration.ReadWord(0x102));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(resources.Director.Vibration.Bytes.AsSpan().Slice(330)));
        Assert.Equal(173, resources.InitialStreamNumber);
        Assert.Equal(0x3AA9Fu, resources.Director.SoundClosing);
        NativeBattleArchiveFile file = Assert.Single(resources.Files.Files, selected => selected.FileId == 754);
        Assert.Equal("mag327_j.dat", file.Name);
        Assert.Equal(33312, file.Bytes.Length);
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32LittleEndian(file.Bytes.AsSpan().Slice(4)));
        Assert.Equal((ushort)64, BinaryPrimitives.ReadUInt16LittleEndian(file.Bytes.AsSpan().Slice(540)));
        Assert.Equal((ushort)256, BinaryPrimitives.ReadUInt16LittleEndian(file.Bytes.AsSpan().Slice(542)));
        Assert.DoesNotContain(resources.Files.Files, selected => selected.FileId is 756 or 757);
        Assert.Throws<InvalidDataException>(() => (resources.Files with { Files = resources.Files.Files.SetItem(10, file with { Bytes = file.Bytes.Add(0) }) }).Validate());
        NativeBattleMasamuneResources geometry = resources.Masamune!;
        Assert.NotNull(geometry);
        Assert.Same(game.ReadNativeMasamuneResources(), geometry);
        geometry.Validate();
        Assert.Equal(2008, geometry.Flare.Bytes.Length);
        Assert.Equal(1520, geometry.BurstA.Bytes.Length);
        Assert.Equal(1768, geometry.BurstB.Bytes.Length);
        Assert.Equal(476, geometry.RotatingSprite.Bytes.Length);
        Assert.Equal(428, geometry.RingSprite.Bytes.Length);
        NativeBattleGilgameshEffectResources restored = JsonSerializer.Deserialize<NativeBattleGilgameshEffectResources>(JsonSerializer.Serialize(resources))!;
        restored.Files.Validate();
        restored.Director.Validate();
        restored.Masamune!.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }
}
