using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDoom8MemoryTests
{
    [Fact]
    public void First_reached_Doom_source_ingress_seeds_only_known_cold_owners_and_warm_entry_keeps_mutations()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.True(cold.Doom8ColdSourcesPending);
        Assert.Null(cold.Doom8);
        (NativeBattleEffectMemory memory, NativeBattleFileStaging staging) = NativeBattleDoom8Memory.Prepare(cold, NativeBattleFileStaging.Unknown(), resources);
        Assert.False(memory.Doom8ColdSourcesPending);
        Assert.Equal(resources.InitialIoScript.Data.ToArray(), memory.Doom8!.IoScript!.Value.ToArray());
        Assert.Equal(BitConverter.GetBytes(resources.RequireColdTargetPointer()), staging.Read(0x1636C04, 4));
        Assert.Empty(memory.Doom8.Models!.Value);
        Assert.False(memory.Doom8.ReaperSourceLoaded);
        NativeBattleDoom8State changed = memory.Doom8.WriteWord(NativeBattleDoom8State.SharedAddress + 0x4A, 123) with { IoScript = memory.Doom8.IoScript.Value.SetItem(12, 0xAB) };
        memory = memory with { Doom8 = changed, SoundPublishedFlags = 0x12345678 };
        staging = staging.Write(0x1636C04, [1, 2, 3, 4]);
        NativeBattleEffectMemory imported = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleFileStaging importedStaging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;
        var warm = NativeBattleDoom8Memory.Prepare(imported, importedStaging, resources);
        Assert.Equal(JsonSerializer.Serialize(memory), JsonSerializer.Serialize(warm.Memory));
        Assert.Equal(JsonSerializer.Serialize(staging), JsonSerializer.Serialize(warm.Staging));
    }

    [Fact]
    public void Imported_unknown_module_initialization_cannot_be_reconstructed_from_original_resources()
    {
        using GameAssets game = OpenGame();
        NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
        NativeBattleEffectMemory unknown = NativeBattleEffectMemory.FromColdProcess() with { Doom8ColdSourcesPending = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Memory.Prepare(unknown, NativeBattleFileStaging.Unknown(), resources));
        NativeBattleEffectMemory contradictory = NativeBattleEffectMemory.FromColdProcess() with { Doom8 = NativeBattleDoom8State.FromColdProcess(resources.InitialIoScript.Data) };
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Memory.Prepare(contradictory, NativeBattleFileStaging.Unknown(), resources));
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Doom source ownership.");
        return new(directory!);
    }
}
