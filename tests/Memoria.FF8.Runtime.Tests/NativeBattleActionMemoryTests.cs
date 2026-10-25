using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionMemoryTests
{
    [Fact]
    public void Statistics_clear_only_the_first_three_bytes_and_leave_free_slots_retained()
    {
        NativeBattleLastAction previous = new(2, 77, 8, 241, 16, 4);
        NativeBattleActionMemory bank = NativeBattleActionMemory.Unknown().WithAction(3, previous).WithHealing(3, 1).WithAction(6, previous);
        bank = bank with { Rows = bank.Rows.SetItem(3, bank.Rows[3].SetItem(2, 170)) };
        NativeBattleActionMemory initialized = bank.InitializeStatistics(3);
        Assert.Equal(previous with { AttackType = 0, AttackerIndex = 0 }, initialized.Action(3));
        Assert.Equal((byte)0, initialized.Rows[3][2]);
        Assert.Equal((byte)1, initialized.Healing(3));
        Assert.Equal(previous, initialized.Action(6));
        Assert.Equal((byte)170, bank.Rows[3][2]);
        Assert.Equal(previous, bank.Action(3));
    }

    [Fact]
    public void New_battle_and_JSON_restore_preserve_unallocated_history_until_NewGame_resets_it()
    {
        NativeBattleLastAction previous = new(2, 77, 8, 241, 16, 4);
        NativeBattleActionMemory bank = NativeBattleActionMemory.FromLoaderBytes(new byte[56]).WithAction(3, previous).WithHealing(3, 1).WithAction(6, previous).WithHealing(6, 1);
        BattleDefinition definition = Definition() with { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { ActionMemory = bank } };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        NativeBattleParticipantSnapshot enemy = battle.Capture().Combatants[1].NativeParticipant!;
        Assert.Equal(previous with { AttackType = 0, AttackerIndex = 0 }, enemy.LastAction);
        Assert.Equal((byte)1, enemy.LastHealing);
        Assert.Equal(previous, battle.NativeActionMemory.Action(6));
        GameSession session = GameSession.CreateColdProcess();
        session.SetBattle(battle.Capture());
        session.SetBattle(null);
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        BattleSession next = new(Definition() with { NativeRetainedMemory = restored.NativeBattleMemory }, BattleAtbMode.Normal);
        Assert.Equal(JsonSerializer.Serialize(battle.NativeActionMemory), JsonSerializer.Serialize(next.NativeActionMemory));
        Assert.Equal(previous, next.NativeActionMemory.Action(6));
        Assert.Equal((byte)1, next.NativeActionMemory.Healing(6));
        Assert.Empty(next.Events);
        session.StartNewGame();
        GameState newGame = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        BattleSession fresh = new(Definition() with { NativeRetainedMemory = newGame.NativeBattleMemory }, BattleAtbMode.Normal);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleRetainedMemory.FromColdProcess().ActionMemory), JsonSerializer.Serialize(fresh.NativeActionMemory));
        Assert.Equal(new NativeBattleLastAction(0, 0, 0, 0, 0, 0), fresh.NativeActionMemory.Action(6));
        Assert.Equal((byte)0, fresh.NativeActionMemory.Healing(6));
        Assert.Empty(fresh.Events);
    }

    [Fact]
    public void Actor_memory_fields_are_validated_projections_of_the_same_saved_bank()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        BattleSnapshot snapshot = battle.Capture();
        NativeBattleLastAction memory = new(1, 2, 8, 42, 4, 3);
        BattleCombatantSnapshot[] actors = snapshot.Combatants.ToArray();
        actors[0] = actors[0] with { NativeParticipant = actors[0].NativeParticipant! with { LastAction = memory, LastHealing = 1 } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = actors }));
        BattleSnapshot factual = snapshot with { Combatants = actors, NativeActionMemory = snapshot.NativeActionMemory!.WithAction(0, memory).WithHealing(0, 1) };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(factual))!);
        Assert.Equal(memory, restored.NativeActionMemory.Action(0));
        Assert.Equal(memory, restored.Capture().Combatants[0].NativeParticipant!.LastAction);
        Assert.Equal((byte)1, restored.Capture().Combatants[0].NativeParticipant!.LastHealing);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeActionMemory = null }));
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void Old_unknown_bytes_remain_unknown_after_restore_and_fail_only_when_the_condition_reads_them()
    {
        NativeBattleAiDefinition ai = new([[0], [2, 10, 0, 0, 0, 0, 0, 0, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleDefinition definition = Definition();
        BattleSession battle = new(definition with { Combatants = [definition.Combatants[0], definition.Combatants[1] with { NativeAi = ai, InitialAtb = BattleSession.AtbMaximum }] }, BattleAtbMode.Normal);
        BattleSnapshot snapshot = battle.Capture();
        snapshot = snapshot with { NativeActionMemory = NativeBattleActionMemory.Unknown(), Combatants = snapshot.Combatants.Select(actor => actor with { NativeParticipant = actor.NativeParticipant! with { LastAction = null, LastHealing = null } }).ToArray() };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Null(restored.NativeActionMemory.Action(3));
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
        {
            for (int callback = 0; callback < 10; callback++)
            {
                restored.Tick();
            }
        });
        Assert.Contains("B99", error.ToString(), StringComparison.Ordinal);
        Assert.Null(restored.NativeActionMemory.Action(3));
    }

    private static BattleDefinition Definition() => new()
    {
        Combatants =
        [
            new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1 },
            new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }
        ]
    };
}
