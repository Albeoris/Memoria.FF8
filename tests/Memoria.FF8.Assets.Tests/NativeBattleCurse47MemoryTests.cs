using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCurse47MemoryTests
{
    [Fact]
    public void First_reached_Curse_source_ingress_seeds_only_known_cold_owners_and_warm_entry_keeps_mutations()
    {
        using GameAssets game = OpenGame();
        NativeBattleCurse47Resources resources = game.ReadNativeCurse47Resources();
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.True(cold.Curse47ColdSourcesPending);
        Assert.Null(cold.Curse47);
        (NativeBattleEffectMemory memory, NativeBattleFileStaging staging) = NativeBattleCurse47Memory.Prepare(cold, NativeBattleFileStaging.Unknown(), resources);
        Assert.False(memory.Curse47ColdSourcesPending);
        Assert.Equal(resources.InitialIoScript.Data.ToArray(), memory.Curse47!.IoScript!.Value.ToArray());
        Assert.Equal(BitConverter.GetBytes(resources.ColdSharedPointer), staging.Read(0x15B8028, 4));
        NativeBattleCurse47State changed = memory.Curse47.WriteWord(NativeBattleCurse47State.SharedAddress + 0x4A, 123) with { IoScript = memory.Curse47.IoScript.Value.SetItem(12, 0xAB) };
        memory = memory with { Curse47 = changed, SoundPublishedFlags = 0x12345678 };
        staging = staging.Write(0x15B8028, [1, 2, 3, 4]);
        NativeBattleEffectMemory imported = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(memory))!;
        NativeBattleFileStaging importedStaging = NativeBattleSnapshotJson.Deserialize<NativeBattleFileStaging>(NativeBattleSnapshotJson.Serialize(staging))!;
        var warm = NativeBattleCurse47Memory.Prepare(imported, importedStaging, resources);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(memory), NativeBattleSnapshotJson.Serialize(warm.Memory));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(staging), NativeBattleSnapshotJson.Serialize(warm.Staging));
    }

    [Fact]
    public void Imported_unknown_module_initialization_cannot_be_reconstructed_from_original_resources()
    {
        using GameAssets game = OpenGame();
        NativeBattleCurse47Resources resources = game.ReadNativeCurse47Resources();
        NativeBattleEffectMemory unknown = NativeBattleEffectMemory.FromColdProcess() with { Curse47ColdSourcesPending = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleCurse47Memory.Prepare(unknown, NativeBattleFileStaging.Unknown(), resources));
        NativeBattleEffectMemory contradictory = NativeBattleEffectMemory.FromColdProcess() with { Curse47 = NativeBattleCurse47State.FromColdProcess(resources.InitialIoScript.Data) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCurse47Memory.Prepare(contradictory, NativeBattleFileStaging.Unknown(), resources));
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Curse source ownership.");
        return new(directory!);
    }
}
