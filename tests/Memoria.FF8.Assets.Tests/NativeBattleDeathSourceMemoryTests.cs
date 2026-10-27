using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDeathSourceMemoryTests
{
    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Cold_ingress_stages_only_original_model_bytes_and_leaves_atomic_source_transition_to_initializer(ushort effect)
    {
        using GameAssets game = Open();
        NativeBattleDeathResources resources = Resources(game, effect);
        NativeBattleEffectMemory cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleFileStaging prior = NativeBattleFileStaging.Unknown().Write(resources.Model.Address + (uint)resources.Model.Bytes.Length, [0xAB, 0xCD]);
        var prepared = NativeBattleDeathSourceMemory.Prepare(cold, prior, resources);
        Assert.Same(cold, prepared.Memory);
        Assert.True(resources.Profile.RequireState(prepared.Memory).PendingColdSources);
        Assert.Null(resources.Profile.RequireState(prepared.Memory).AuraActors);
        Assert.Null(resources.Profile.RequireState(prepared.Memory).DarkActors);
        Assert.Equal(resources.Model.Bytes.ToArray(), prepared.Staging.ReadDat(resources.Model.Address).ToArray());
        Assert.Equal(new byte[] { 0xAB, 0xCD }, prepared.Staging.Read(resources.Model.Address + (uint)resources.Model.Bytes.Length, 2));
        Assert.Throws<NotSupportedException>(() => prepared.Staging.Read(resources.CasterResource?.Address ?? 0x15863B0u, 1));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Warm_reentry_and_JSON_keep_mutated_model_bytes_and_both_mutable_banks(ushort effect)
    {
        using GameAssets game = Open();
        NativeBattleDeathResources resources = Resources(game, effect);
        NativeBattleEffectMemory cold = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        var prepared = NativeBattleDeathSourceMemory.Prepare(cold, NativeBattleFileStaging.Unknown(), resources);
        NativeBattleDeathState changed = resources.Profile.RequireState(cold) with { PendingColdSources = false, AuraActors = resources.AuraActors.SetItem(0x20, 0xAB), DarkActors = resources.DarkActors.SetItem(0x20, 0xCD) };
        NativeBattleEffectMemory warm = resources.Profile.WithState(prepared.Memory, changed);
        uint last = resources.Model.Address + (uint)resources.Model.Bytes.Length - 1;
        NativeBattleFileStaging mutated = prepared.Staging.Write(last, [(byte)(resources.Model.Bytes[^1] ^ 0xFF)]);
        NativeBattleEffectMemory imported = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(warm)))!);
        NativeBattleFileStaging importedStaging = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(mutated)))!);
        var next = NativeBattleDeathSourceMemory.Prepare(imported, importedStaging, resources);
        Assert.Same(imported, next.Memory);
        Assert.Same(importedStaging, next.Staging);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(warm)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(next.Memory)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(mutated)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(next.Staging)));
        Assert.NotEqual(resources.Model.Bytes[^1], next.Staging.Read(last, 1)[0]);
        Assert.Equal((byte)0xAB, resources.Profile.RequireState(next.Memory).AuraActors!.ReadByte(0x20));
        Assert.Equal((byte)0xCD, resources.Profile.RequireState(next.Memory).DarkActors!.ReadByte(0x20));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Warm_unknown_staging_is_not_hydrated_and_fails_only_at_reached_model_read(ushort effect)
    {
        using GameAssets game = Open();
        NativeBattleDeathResources resources = Resources(game, effect);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        memory = resources.Profile.WithState(memory, resources.Profile.RequireState(memory) with { PendingColdSources = false });
        NativeBattleFileStaging unknown = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleFileStaging.Unknown())))!);
        var retained = NativeBattleDeathSourceMemory.Prepare(memory, unknown, resources);
        Assert.Same(memory, retained.Memory);
        Assert.Same(unknown, retained.Staging);
        Assert.Throws<NotSupportedException>(() => retained.Staging.ReadDat(resources.Model.Address));
        Assert.Null(resources.Profile.RequireState(retained.Memory).AuraActors);
        Assert.Null(resources.Profile.RequireState(retained.Memory).DarkActors);
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Missing_or_contradictory_cold_owner_cannot_materialize_sources(ushort effect)
    {
        using GameAssets game = Open();
        NativeBattleDeathResources resources = Resources(game, effect);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleEffectMemory missing = effect == 14 ? memory with { Death14 = null } : memory with { LevelDeath74 = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathSourceMemory.Prepare(missing, NativeBattleFileStaging.Unknown(), resources));
        NativeBattleDeathState invalid = resources.Profile.RequireState(memory) with { AuraActors = resources.AuraActors };
        Assert.Throws<InvalidDataException>(() => NativeBattleDeathSourceMemory.Prepare(resources.Profile.WithState(memory, invalid), NativeBattleFileStaging.Unknown(), resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathSourceMemory.Prepare(memory, NativeBattleFileStaging.Unknown(), resources with { Model = resources.Model with { Bytes = ImmutableArray<byte>.Empty } }));
    }

    private static NativeBattleDeathResources Resources(GameAssets game, ushort effect) => effect == 14 ? game.ReadNativeDeathResources() : game.ReadNativeLevelDeath74Resources().FamilyResources();

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for Death source ingress.");
        return new(directory!);
    }
}
