using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class DiagnosticLimitCapabilityTests
{
    [Fact]
    public void Borrowed_blue_magic_preserves_Squall_and_uses_original_crisis_spell_records()
    {
        BattleDefinition source = CreateBlueMagicDefinition();
        BattleNativeLimitDefinition owner = new(3, 15, 0, 250, Enumerable.Repeat((byte)0, 32).ToImmutableArray());
        string before = JsonSerializer.Serialize(source);

        (BattleDefinition adapted, BattleDiagnosticBorrowedLimitReceipt receipt) = BattleSession.PrepareDiagnosticBlueMagic(source, "squall", owner);
        BattleSession battle = new(adapted, BattleAtbMode.TurnBased);
        BattleDiagnosticLimitSelectionReceipt selection = battle.SelectDiagnosticLimit("squall", new(1, 4, 256));

        Assert.True(selection.Accepted);
        Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "squall").ModelId);
        Assert.Equal(3, receipt.CommandOwner);
        Assert.Equal((byte)15, receipt.Command);
        Assert.Equal(owner, adapted.Combatants[0].NativeLimit);
        Assert.Equal(source.Combatants[0], adapted.Combatants[0] with { NativeLimit = source.Combatants[0].NativeLimit });
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("squall", limits: true));
        Assert.Equal(15 << 16, choice.Id);
        Assert.Equal(selection.CrisisSelections.Last(), choice.NativeAction!.CrisisLevel);
        Assert.Equal(3, choice.NativeAction.CharacterId);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Assert.Equal(0, restored.Combatants.Single(actor => actor.Id == "squall").ModelId);
        Assert.Equal(3, restored.Combatants.Single(actor => actor.Id == "squall").NativeLimit!.CharacterId);
        Assert.Equal(JsonSerializer.Serialize(choice), JsonSerializer.Serialize(Assert.Single(restored.GetNativePlayerActionChoices("squall", limits: true))));
    }

    [Fact]
    public void Borrowed_blue_magic_rejects_missing_crisis_inputs_and_another_actor_identity()
    {
        BattleDefinition source = CreateBlueMagicDefinition();
        BattleNativeLimitDefinition owner = new(3, 15, 0, 250, Enumerable.Repeat((byte)0, 32).ToImmutableArray());

        Assert.Throws<NotSupportedException>(() => BattleSession.PrepareDiagnosticBlueMagic(source with { NativeActions = source.NativeActions.Take(3).ToArray() }, "squall", owner));
        Assert.Throws<NotSupportedException>(() => BattleSession.PrepareDiagnosticBlueMagic(source with { Combatants = [source.Combatants[0] with { ModelId = 3 }, source.Combatants[1]] }, "squall", owner));
        Assert.Throws<NotSupportedException>(() => BattleSession.PrepareDiagnosticBlueMagic(source, "squall", owner with { Command = 11 }));
    }

    [Fact]
    public void Selection_advances_only_the_normal_menu_random_owner()
    {
        BattleSession adapted = CreateBattle();
        BattleSession normal = CreateBattle();
        BattleDiagnosticLimitSelectionReceipt receipt = adapted.SelectDiagnosticLimit("squall", new(4, 4, 32));

        foreach (byte expected in receipt.CrisisSelections)
        {
            normal.SelectPlayerCommandActor("squall");
            Assert.Equal(expected, normal.GetNativeLimitLevel("squall"));
        }

        Assert.Equal(JsonSerializer.Serialize(normal.Capture()), JsonSerializer.Serialize(adapted.Capture()));
        Assert.Equal(0, receipt.CommandOwner);
        Assert.Equal((byte)5, receipt.Command);
        Assert.Equal(receipt.CrisisSelections.Last() == 4, receipt.Accepted);
        Assert.Equal(JsonSerializer.Serialize(adapted.Capture().Random), JsonSerializer.Serialize(receipt.RandomAfter));
    }

    [Fact]
    public void Exhausted_selection_reports_a_blocker_without_forcing_crisis()
    {
        BattleSession battle = CreateBattle(disabled: true);
        string beforeRandom = JsonSerializer.Serialize(battle.Capture().Random);

        BattleDiagnosticLimitSelectionReceipt receipt = battle.SelectDiagnosticLimit("squall", new(1, 4, 3));

        Assert.False(receipt.Accepted);
        Assert.Equal(new byte[] { 0, 0, 0 }, receipt.CrisisSelections.ToArray());
        Assert.Equal(beforeRandom, JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal((byte)0, battle.GetNativeLimitLevel("squall"));
    }

    [Fact]
    public void Once_per_turn_selection_is_not_rerolled()
    {
        BattleSession battle = CreateBattle(oncePerTurn: true);
        battle.SelectPlayerCommandActor("squall");
        byte initial = battle.GetNativeLimitLevel("squall");
        string beforeRandom = JsonSerializer.Serialize(battle.Capture().Random);

        BattleDiagnosticLimitSelectionReceipt receipt = battle.SelectDiagnosticLimit("squall", new(initial == 4 ? (byte)1 : (byte)4, initial == 4 ? (byte)1 : (byte)4, 32));

        Assert.False(receipt.Accepted);
        Assert.Single(receipt.CrisisSelections);
        Assert.Equal(initial, battle.GetNativeLimitLevel("squall"));
        Assert.Equal(beforeRandom, JsonSerializer.Serialize(battle.Capture().Random));
    }

    [Fact]
    public void Invalid_or_unready_selection_leaves_battle_unchanged()
    {
        BattleSession battle = CreateBattle(ready: false);
        string before = JsonSerializer.Serialize(battle.Capture());

        Assert.Throws<InvalidDataException>(() => battle.SelectDiagnosticLimit("squall", new(0, 4, 1)));
        Assert.Throws<InvalidOperationException>(() => battle.SelectDiagnosticLimit("squall", new(1, 4, 1)));
        Assert.False(battle.TriggerDiagnosticGunbladeWindow());
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private static BattleSession CreateBattle(bool disabled = false, bool oncePerTurn = false, bool ready = true)
    {
        return new(new BattleDefinition
        {
            NativeLimitsDisabled = disabled,
            LimitOncePerTurn = oncePerTurn,
            Combatants =
            [
                new() { Id = "squall", Name = "Squall", IsPlayer = true, ModelId = 0, MaximumHp = 100, CurrentHp = 20, InitialAtb = ready ? BattleSession.AtbMaximum : 0, NativeLimit = new(0, 5, 0, 250, Enumerable.Repeat((byte)0, 32).ToImmutableArray()) },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleDefinition CreateBlueMagicDefinition()
    {
        BattleDefinition definition = CreateBattle().Capture().Definition;
        return definition with
        {
            LimitProgress = new(new string('A', 64), 1, 0, 0, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray()),
            NativeCommandSequences = Enumerable.Repeat((byte)0, 39).ToImmutableArray(),
            NativeActions = Enumerable.Range(1, 4).Select(crisis => new BattleNativeAction(15, 0, 19, 0)
            {
                Name = "Blue Magic", CharacterId = 3, CrisisLevel = (byte)crisis, SupplementalKernelIndex = crisis - 1,
                MagicId = 1, AttackType = 1, Power = 1, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 4,
                Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null,
                RawRecord = Enumerable.Repeat((byte)0, 16).ToImmutableArray(), SupplementalRecord = Enumerable.Repeat((byte)0, 8).ToImmutableArray()
            }).ToArray()
        };
    }
}
