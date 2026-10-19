using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleTonberry90ResourcesTests
{
    [Fact]
    public void Original_Tonberry_profile_binds_script_camera_curves_vibration_and_both_archive_files()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([90])[89];
        Assert.Equal(0x762360u, binding.Entry);
        Assert.Equal(0x762320u, binding.Initializer);
        NativeBattleTonberry90Resources resources = Assert.IsType<NativeBattleTonberry90Resources>(binding.Tonberry90Resources);
        Assert.Same(resources, game.ReadNativeTonberry90Resources());
        Assert.Equal(0x1547170u, resources.StaticAddress);
        Assert.Equal(0x7B00, resources.StaticData.Data.Length);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(0x15474D0, 2)));
        Assert.Equal((ushort)0xFF, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(0x1547500, 2)));
        Assert.Equal(72, resources.ReadStatic(0x154EC28, 72).Length);
        Assert.Equal(0xD8, resources.ReadStatic(0x154E910, 0xD8).Length);
        Assert.Equal(new[] { 0, 0x100 }, resources.Vibration.Bank.Offsets.ToArray());
        Assert.Equal(0x11C, resources.Vibration.Bank.Bytes.Length);
        Assert.Equal((ushort)0x10, resources.Vibration.Bank.ReadWord(0x100));
        Assert.Equal((ushort)4, resources.Vibration.Bank.ReadWord(0x102));
        Assert.Equal(new short[] { 416, 417 }, resources.Files.Select(file => file.FileId).ToArray());
        Assert.Equal(new[] { "MAG089_B.1T0", "MAG089_B.1Z0" }, resources.Files.Select(file => file.Name.ToUpperInvariant()).ToArray());
        Assert.All(resources.Files, file => Assert.False(file.Data.IsDefaultOrEmpty));
        string json = JsonSerializer.Serialize(binding);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!));
        Assert.Null(game.ReadNativeEffectCatalog()[89].Tonberry90Resources);
    }
}
