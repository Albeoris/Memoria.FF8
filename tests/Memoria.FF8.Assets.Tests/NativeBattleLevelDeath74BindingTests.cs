using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLevelDeath74BindingTests
{
    [Fact]
    public void Original_catalog_selects_the_Level_Death_initializer_and_family_resource_identity()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([74])[73];
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(74);
        Assert.Equal((ushort)74, binding.Effect);
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag013-073-344.tim", 0x25EC518), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.NotEmpty(binding.InitializerAsset!.Data);
        Assert.Equal("mag013-073-344.tim", binding.InitializerAsset.Name);
        NativeBattleDeathResources resources = binding.DeathResources!;
        Assert.NotNull(resources);
        Assert.Equal((ushort)74, resources.Effect);
        Assert.Same(profile, resources.Profile);
        Assert.Equal(0x15863B0u, resources.CasterResource!.Address);
        Assert.Equal(game.ReadNativeLevelDeath74Resources().Animation, resources.Animation);
        Assert.Equal(0x7C45E0u, profile.Callback(0x8B6A40));
        Assert.Equal(0x7C4830u, profile.Callback(0x8B6C90));
        Assert.Equal(0x7C49D0u, profile.Callback(0x8B6E30));
        Assert.Equal(0x7C54A0u, profile.Callback(0x8B78C0));
    }

    [Fact]
    public void Family_bridge_JSON_preserves_Level_Death_resources_without_selecting_Death()
    {
        using GameAssets game = Open();
        NativeBattleDeathResources source = game.ReadNativeLevelDeath74Resources().FamilyResources();
        string json = JsonSerializer.Serialize(source);
        NativeBattleDeathResources restored = JsonSerializer.Deserialize<NativeBattleDeathResources>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal((ushort)74, restored.Profile.Effect);
        Assert.Equal(0x1771F5Cu, restored.Profile.AuraAddress);
        Assert.Equal(0x1776950u, restored.Profile.DarkAddress);
        Assert.Equal(440, restored.Animation.Data.Length);
        Assert.NotEqual(game.ReadNativeDeathResources().Animation.Identity, restored.Animation.Identity);
        Assert.Equal(source.CasterResource!.Bytes.ToArray(), restored.CasterResource!.Bytes.ToArray());
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(restored.AuraActors.AsSpan()));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(restored.DarkActors.AsSpan()));
    }

    [Fact]
    public void Cold_module_owners_are_separate_and_JSON_preserves_explicit_unknown_bytes()
    {
        using GameAssets game = Open();
        NativeBattleEffectMemory cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(74);
        NativeBattleDeathState level = profile.RequireState(cold);
        Assert.NotSame(cold.Death14, level);
        Assert.True(level.PendingColdSources);
        Assert.Null(level.AuraActors);
        Assert.Null(level.DarkActors);
        Assert.Equal(new byte[profile.ModuleLength], level.Module.Read(0, profile.ModuleLength));
        Assert.Throws<NotSupportedException>(() => profile.RequireState(cold with { LevelDeath74 = null }));
        NativeBattleDeathState retained = level with { Module = level.Module.Write(0x20, [0xA1, 0xB2, 0xC3, 0xD4]).MarkUnknown(0, 4), Cursor = 1234 };
        NativeBattleEffectMemory changed = profile.WithState(cold, retained);
        Assert.Same(cold.Death14, changed.Death14);
        string json = JsonSerializer.Serialize(changed);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!;
        NativeBattleDeathState actual = profile.RequireState(restored);
        Assert.Throws<NotSupportedException>(() => actual.Module.Read(0, 4));
        Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 }, actual.Module.Read(0x20, 4));
        Assert.Equal((int?)1234, actual.Cursor);
        Assert.Equal(new byte[4], restored.Death14!.Module.Read(0, 4));
        Assert.Equal(json, JsonSerializer.Serialize(restored));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Level Death binding.");
        return new(directory!);
    }
}
