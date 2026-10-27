using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShellOwnedSourceTests
{
    [Fact]
    public void Pending_actor_bank_preserves_legacy_empty_array_and_stays_unmaterialized_after_restore()
    {
        NativeBattleShellState cold = NativeBattleShellState.FromColdProcess();
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(cold));
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(0, document.RootElement.GetProperty("ActorBank").GetArrayLength());
        NativeBattleShellState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleShellState>(json)!);
        Assert.True(restored.PendingColdActorBank);
        Assert.Null(restored.ActorBank);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
    }

    [Fact]
    public void Materialized_actor_source_uses_legacy_bytes_and_independent_restored_ownership()
    {
        NativeBattleShellState state = NativeBattleShellState.FromColdProcess() with { ActorBank = new byte[NativeBattleShellResources.ActorBankLength].ToImmutableArray(), PendingColdActorBank = false };
        state.ActorBank!.Arena.WriteByte(17, 9);
        NativeBattleShellState frozen = NativeBattleStorageSnapshots.Capture(state);
        string json = JsonSerializer.Serialize(frozen);
        NativeBattleShellState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleShellState>(json)!);
        restored.ActorBank!.Arena.WriteByte(17, 7);
        Assert.Equal((byte)9, state.ActorBank.ReadByte(17));
        Assert.Equal((byte)9, frozen.ActorBank!.ReadByte(17));
        Assert.Equal((byte)7, restored.ActorBank.ReadByte(17));
    }
}
