using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticEnemyAbilityTests
{
    private static readonly BattleDiagnosticEnemyAbility Selection = new(1, 0, 0, 0, 2, 7, 1, 201);

    private static BattleSession Create(bool hold = true)
    {
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(2, 1, 7), 48).ToArray(), 0, []);
        BattleNativeAction action = new(2, 7, 1, 7) { MagicId = 3, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [action],
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0 }, new() { Id = "enemy", Name = "Enemy", ModelId = 1, SceneSlot = 0, NativeCombatantFlags = 1, NativeAi = ai, InitialAtb = BattleSession.AtbMaximum }]
        }, BattleAtbMode.Normal);
        battle.ConfigureDiagnosticScheduling(hold);
        return battle;
    }

    [Fact]
    public void Diagnostic_selection_uses_the_actual_row_sequence_target_selector_and_normal_packet_bank()
    {
        BattleSession battle = Create();
        Assert.True(battle.CanSubmitDiagnosticEnemyAbility("enemy", Selection));
        battle.SubmitDiagnosticEnemyAbility("enemy", Selection, 3);
        BattleSnapshot snapshot = battle.Capture();
        BattleAttackCommand queued = Assert.Single(snapshot.QueuedActions);
        Assert.Equal("enemy", queued.ActorId);
        Assert.Equal("party", queued.TargetId);
        Assert.Equal((byte)1, queued.NativeSequence);
        Assert.Equal((byte)2, queued.NativeQueuedCommand);
        Assert.Equal((ushort)1, queued.NativeTargetMask);
        Assert.Equal(7, queued.ActionId);
        Assert.Equal(BattleAbility.EnemyAbility, queued.Ability);
        Assert.False(battle.CanSubmitDiagnosticEnemyAbility("enemy", Selection));
    }

    [Fact]
    public void Turn_selection_queues_the_original_stage_one_callback_without_inventing_an_ability_row()
    {
        BattleSession battle = Create();
        battle.SubmitDiagnosticEnemyAbility("enemy", Selection with { UseTurnScript = true, AbilitySlot = -1 }, 3);
        BattleSnapshot saved = battle.Capture();
        BattleAttackCommand callback = Assert.Single(saved.NativeCommandBanks!.BoundCommands, command => command.NativeCallback?.Stage == 1);
        Assert.Equal(new BattleNativeCallback(3, 1), callback.NativeCallback);
        Assert.Equal("enemy", callback.ActorId);
        Assert.DoesNotContain(saved.QueuedActions, command => command.NativeCallback is null && command.ActionId == 7);
    }

    [Fact]
    public void Wrong_owner_band_row_or_effect_cannot_mutate_the_battle_or_random_owner()
    {
        BattleSession battle = Create();
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Throws<InvalidOperationException>(() => battle.CanSubmitDiagnosticEnemyAbility("enemy", Selection with { Model = 2 }));
        Assert.Throws<InvalidOperationException>(() => battle.CanSubmitDiagnosticEnemyAbility("enemy", Selection with { LevelBand = 1 }));
        Assert.Throws<InvalidOperationException>(() => battle.CanSubmitDiagnosticEnemyAbility("enemy", Selection with { Sequence = 2 }));
        Assert.Throws<InvalidOperationException>(() => battle.SubmitDiagnosticEnemyAbility("enemy", Selection, 2));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Throws<InvalidOperationException>(() => Create(false).CanSubmitDiagnosticEnemyAbility("enemy", Selection));
    }
}
