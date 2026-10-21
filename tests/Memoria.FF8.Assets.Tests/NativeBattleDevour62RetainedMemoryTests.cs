using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDevour62RetainedMemoryTests
{
    [Fact]
    public void Authenticated_loader_supplies_counters_and_known_null_arena_pointers()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Devour retained memory.");
        using GameAssets game = new(directory!);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        NativeBattleDevour62State state = memory.RequireDevour62();
        state.Validate();
        Assert.Equal(0u, state.Counter2601918);
        Assert.Equal(0u, state.Counter2601770);
        Assert.Null(state.PrimaryDrawBase);
        Assert.Null(state.AlternatePrimaryDrawBase);
        Assert.Null(state.SecondaryDrawBase);
        Assert.Null(state.AlternateSecondaryDrawBase);
        Assert.Null(state.PrimaryDrawCursor);
        Assert.Null(state.SecondaryDrawCursor);
        Assert.Null(state.AuxiliaryCursor);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        restored.Validate();
        Assert.Equal(state, restored.RequireDevour62());
    }

    [Fact]
    public void Missing_module_owner_is_not_implicitly_hydrated_and_current_owner_is_validated()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Null(memory.Devour62);
        Assert.DoesNotContain("Devour62", JsonSerializer.Serialize(memory));
        Assert.Throws<NotSupportedException>(() => memory.RequireDevour62());
        NativeBattleDevour62State current = NativeBattleDevour62State.FromColdProcess(12, 34) with { PrimaryDrawCursor = 99 };
        memory = memory.WithDevour62(current);
        Assert.Same(current, memory.RequireDevour62());
        memory.Validate();
        Assert.Throws<InvalidDataException>(() => memory.WithDevour62(current with { PrimaryDrawCursor = -1 }));
        Assert.Throws<InvalidDataException>(() => (memory with { Devour62 = current with { PrimaryDrawCursor = -1 } }).Validate());
    }
}
