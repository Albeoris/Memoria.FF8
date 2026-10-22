using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAbsorb56ResourcesTests
{
    [Fact]
    public void Original_Absorb_resources_keep_absolute_bank_channels_and_shared_camera()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Absorb.");
        using GameAssets game = new(directory!);
        NativeBattleDrain39Resources resources = game.ReadNativeAbsorb56Resources();
        NativeBattleDrain39Resources drain = game.ReadNativeDrain39Resources();
        Assert.Same(resources, game.ReadNativeAbsorb56Resources());
        Assert.Equal((ushort)56, resources.Effect);
        Assert.Equal(0x159EF34u, resources.Profile.BankAddress);
        Assert.Equal(0x15A3088u, resources.Profile.DirectoryAddress);
        Assert.Equal(0x260F144u, resources.Profile.ChannelsAddress);
        Assert.Equal(0x4258, resources.NodeBank.Length);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeBank.AsSpan().Slice(0x4154)));
        Assert.Equal(0x15A1F10u, BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeBank.AsSpan().Slice(0x4158)));
        Assert.Equal(0x738, resources.Animation.Data.Length);
        Assert.Equal(drain.Animation.Data.ToArray(), resources.Animation.Data.ToArray());
        Assert.Equal(drain.Sound, resources.Sound);
        Assert.Equal(drain.FirstFlash.ToArray(), resources.FirstFlash.ToArray());
        Assert.Equal(drain.SecondFlash.ToArray(), resources.SecondFlash.ToArray());
        Assert.Equal(0x1E60, resources.Channels!.Value.Length);
        Assert.Equal(drain.Channels!.Value.ToArray(), resources.Channels.Value.ToArray());
        Assert.Equal(new byte[14], resources.ReadChannels(0x2610D24, 14));
        Assert.Equal(new byte[4], resources.ReadChannels(0x2610FA0, 4));
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(0x2610FA4, 1));
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(0x2610FA8, 1));
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(0x269E764, 1));
        NativeBattleDrain39Resources restored = JsonSerializer.Deserialize<NativeBattleDrain39Resources>(JsonSerializer.Serialize(resources))!;
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Equal((ushort)56, restored.Profile.Effect);
        Assert.Throws<NotSupportedException>(() => (restored with { Channels = null }).ReadChannels(0x260F144, 1));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([56])[55];
        Assert.Equal(0x803200u, binding.Entry);
        Assert.Equal(0x8031E0u, binding.Initializer);
        Assert.Equal("mag038-055.tim", binding.InitializerAsset!.Name);
        Assert.Equal(167456, binding.InitializerAsset.Data.Length);
        Assert.NotNull(binding.Absorb56Resources);
    }

    [Fact]
    public void Authenticated_Absorb_cold_bank_is_separate_from_Drain_and_retains_current_bytes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Absorb retained owners.");
        using GameAssets game = new(directory!);
        NativeBattleDrain39Resources resources = game.ReadNativeAbsorb56Resources();
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleDrainFamilyProfile profile = resources.Profile;
        Assert.Null(profile.RequireBank(memory).Data);
        Assert.Throws<NotSupportedException>(() => profile.RequireBank(memory with { Absorb56Bank = null }));
        NativeBattleDrain39BankState retained = new(resources.NodeBank);
        retained.Validate();
        NativeBattleEffectMemory updated = profile.WithBank(memory, retained);
        Assert.Same(memory.Drain39Bank, updated.Drain39Bank);
        Assert.Same(retained, profile.RequireBank(updated));
        NativeBattleDrain39BankState restored = JsonSerializer.Deserialize<NativeBattleDrain39BankState>(JsonSerializer.Serialize(retained))!;
        restored.Validate();
        Assert.Equal(retained.Data!.Value.ToArray(), restored.Data!.Value.ToArray());
        Assert.Throws<InvalidDataException>(() => (retained with { Data = resources.NodeBank.SetItem(0x4154, 0) }).Validate());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleDrain39BankState>("{}"));
    }
}
