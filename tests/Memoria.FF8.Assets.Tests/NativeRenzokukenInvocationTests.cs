using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRenzokukenInvocationTests
{
    [Fact]
    public void Unknown_retained_finisher_byte_is_preserved_through_intermediate_contacts_and_required_at_the_last()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets);
        battle.QueueNativePlayerCommand("squall", "enemy", 5, 0);
        battle.Tick();
        BattleNativeRenzokukenState process = battle.GetNativeRenzokuken("squall")!;
        int contacts = game.ReadNativeRenzokukenResources().Single(resource => resource.MagicId == process.MagicId).Timeline.InputThresholds.Length;
        Assert.True(contacts > 1);
        BattleSnapshot historical = battle.Capture();
        battle = BattleSession.Restore(historical with { NativeActionScratch = historical.NativeActionScratch! with { FinisherPendingE03 = null } });
        Until(battle, () => battle.GetNativeRenzokuken("squall")!.Input.ContactIndex == contacts - 1);
        Assert.Equal(contacts - 1, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.True(battle.Capture().NativeActionScratch!.DeferredEffectsE00);
        Assert.Null(battle.Capture().NativeActionScratch!.FinisherPendingE03);
        BattleSnapshot beforeLast = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(beforeLast))!);
        Assert.Equal(JsonSerializer.Serialize(beforeLast), JsonSerializer.Serialize(restored.Capture()));
        NotSupportedException failure = Assert.Throws<NotSupportedException>(() => Until(restored, () => restored.GetNativeRenzokuken("squall") is null));
        Assert.Contains("retained E03", failure.Message);
        Assert.Equal(JsonSerializer.Serialize(beforeLast.Random), JsonSerializer.Serialize(restored.Capture().Random));
        Assert.Equal(contacts - 1, restored.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Null(restored.Capture().NativeActionScratch!.FinisherPendingE03);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void FinisherCallbackAppendsAppliedFactsBeforeTheFirstTriggerAndDoesNotReplayAtTheEnd(int finisher)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, enemyHp: 1000000, crisis: 4, finisher: true, chosenFinisher: (byte)finisher);
        battle.QueueNativePlayerCommand("squall", "enemy", 5, 0);
        battle.Tick();
        BattleNativeRenzokukenState process = battle.GetNativeRenzokuken("squall")!;
        BattleNativeAction action = assets.NativeActions.Single(action => action.Command == 249 && action.ActionId == finisher);
        Assert.Equal((byte)finisher, process.FinisherActionId);
        Assert.Equal(0, process.Input.ContactIndex);
        Assert.Equal(0, battle.Capture().NativeTimedAction!.Contacts);
        Assert.Equal((byte)2, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)5, battle.NativeActionPackets.Headers[0].ReadByte(1));
        Assert.Equal((byte)250, battle.NativeActionPackets.Headers[1].ReadByte(1));
        Assert.Equal((byte)255, battle.NativeActionPackets.Headers[1].ReadByte(4));
        Assert.Equal((byte)255, battle.NativeActionPackets.Headers[1].ReadByte(5));
        Assert.Equal(action.Name, battle.NativeActionPackets.Headers[1].Data!.FormattedText);
        Assert.Equal(action.HitCount, battle.Capture().NativeActionScratch!.LastPublishedTargetCountE05);
        Assert.Equal(action.HitCount, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Equal(1 + 2 * action.HitCount, battle.NativeActionPackets.ContactCount);
        Assert.Equal((byte)250, battle.Capture().Combatants[1].NativeParticipant!.LastAction!.Command);
        Assert.Equal((byte)255, battle.Capture().Combatants[1].NativeParticipant!.LastAction!.ActionId);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        int contacts = game.ReadNativeRenzokukenResources().Single(resource => resource.MagicId == process.MagicId).Timeline.InputThresholds.Length;
        Until(battle, () => battle.GetNativeRenzokuken("squall") is null);
        Until(restored, () => restored.GetNativeRenzokuken("squall") is null);
        Assert.Equal(action.HitCount + contacts, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.False(battle.Capture().NativeActionScratch!.FinisherPendingE03);
        Assert.Equal((byte)250, battle.Capture().Combatants[1].NativeParticipant!.LastAction!.Command);
        Assert.Equal((byte)255, battle.Capture().Combatants[1].NativeParticipant!.LastAction!.ActionId);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Contacts_restore_the_deferred_reaction_and_finish_poison_and_counter_only_once()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, poison: true, counter: true);
        battle.QueueNativePlayerCommand("squall", "enemy", 5, 0);
        BattleAttackCommand invocation = Assert.Single(battle.QueuedActions, command => command.NativeRenzokuken is not null);
        Assert.Null(invocation.NativeAction);
        Assert.Equal(new BattleNativeRenzokukenInvocation(1), invocation.NativeRenzokuken);
        for (int tick = 0; tick < 100 && battle.GetNativeRenzokuken("squall") is null; tick++)
        {
            battle.Tick();
        }
        Assert.NotNull(battle.GetNativeRenzokuken("squall"));
        long frame = battle.Frame;
        Until(battle, () => battle.GetNativeRenzokuken("squall")!.Input.ContactIndex == 1);
        BattleSnapshot first = battle.Capture();
        Assert.Equal(1, first.NativeTimedAction!.Contacts);
        Assert.Single(first.Combatants, actor => actor.NativeParticipant?.PendingReaction?.Stage is > 0);
        Assert.Equal(0u, first.NativeAiVariables![0]);
        Assert.DoesNotContain(battle.Events, item => item.Message.StartsWith("Poison:", StringComparison.Ordinal));
        string snapshot = JsonSerializer.Serialize(first);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(first with { NativeTimedAction = first.NativeTimedAction with { Contacts = 2 } }));

        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);

        Assert.Equal(snapshot, JsonSerializer.Serialize(battle.Capture()));
        Until(battle, () => battle.GetNativeRenzokuken("squall") is null);
        Assert.Equal(frame, battle.Frame);
        Assert.Equal(0u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(0x47E120u, battle.Capture().NativeActionScratch!.ComponentCompletion);
        for (int tick = 0; tick < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); tick++)
        {
            battle.Tick();
        }
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "squall" && item.Message.StartsWith("Poison:", StringComparison.Ordinal));
        Assert.Null(battle.Capture().NativeTimedAction);
        Assert.False(battle.Combatants[0].IsBusy);
    }

    [Fact]
    public void Queued_Renzokuken_waits_for_the_preceding_action_and_retains_its_typed_invocation_on_restore()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, precedingActor: true);
        battle.QueueAttack("ally", "enemy");
        for (int tick = 0; tick < 100 && battle.ActiveAction is null; tick++)
        {
            battle.Tick();
        }
        //Bank1 Limits outrank queued bank2 inputs; this contract concerns an already admitted action.
        Assert.Equal("ally", battle.ActiveAction!.ActorId);
        battle.QueueNativePlayerCommand("squall", "enemy", 5, 0);
        battle = BattleSession.Restore(battle.Capture());
        Assert.Equal("ally", battle.ActiveAction!.ActorId);
        Assert.Null(battle.GetNativeRenzokuken("squall"));
        Assert.Null(battle.Capture().NativeTimedAction);
        for (int frame = 0; frame < 100 && battle.GetNativeRenzokuken("squall") is null; frame++)
        {
            battle.Tick();
        }
        Assert.NotNull(battle.GetNativeRenzokuken("squall"));
        Assert.Null(battle.ActiveAction);
        Assert.Equal("squall", battle.Capture().NativeTimedAction!.Command.ActorId);
    }

    [Fact]
    public void Defeated_original_target_is_retained_and_victory_waits_for_the_whole_process()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession single = Create(game, assets, enemyHp: 1);
        single.QueueNativePlayerCommand("squall", "enemy", 5, 0);
        single.Tick();
        Until(single, () => single.GetNativeRenzokuken("squall")!.Input.ContactIndex == 1);

        Assert.Equal(0, single.Combatants[1].CurrentHp);
        Assert.Equal(BattleOutcome.Active, single.Outcome);
        single = BattleSession.Restore(single.Capture());
        Until(single, () => single.GetNativeRenzokuken("squall") is null);
        Assert.Equal(BattleOutcome.Victory, single.Outcome);
        BattleSession multiple = Create(game, assets, enemyHp: 1, secondEnemy: true);
        multiple.QueueNativePlayerCommand("squall", "enemy", 5, 0);
        multiple.Tick();
        Until(multiple, () => multiple.GetNativeRenzokuken("squall") is null);

        Assert.Equal(50000, multiple.Combatants[2].CurrentHp);
        Assert.DoesNotContain(multiple.Events, item => item.Kind == "Damage" && item.TargetId == "other");
        Assert.Equal(BattleOutcome.Active, multiple.Outcome);
    }

    [Fact]
    public void Host_gunblade_assistance_does_not_replace_Renzokuken_contact_input()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession manual = Create(game, assets);
        BattleSession assisted = Create(game, assets);
        assisted.SetGunbladeMode(BattleGunbladeMode.Automatic);
        BattleSession triggered = Create(game, assets);
        foreach (BattleSession battle in new[] { manual, assisted, triggered })
        {
            battle.QueueNativePlayerCommand("squall", "enemy", 5, 0);
            battle.Tick();
        }
        Until(manual, () => manual.GetNativeRenzokuken("squall")!.Input.ContactIndex == 1);
        Until(assisted, () => assisted.GetNativeRenzokuken("squall")!.Input.ContactIndex == 1);
        Until(triggered, () => triggered.NativeRenzokukenTriggerWindowOpen);
        Assert.True(triggered.TriggerGunblade());
        Until(triggered, () => triggered.GetNativeRenzokuken("squall")!.Input.ContactIndex == 1);

        Assert.Equal(FirstHit(manual), FirstHit(assisted));
        Assert.True(FirstHit(triggered) > FirstHit(manual));
        Assert.Equal(manual.Capture().Random!.Indexes, triggered.Capture().Random!.Indexes);
        Assert.Equal(manual.Capture().Random!.Lane, triggered.Capture().Random!.Lane);
    }

    [Fact]
    public void Original_auto_option_uses_its_owned_UI_random_and_finisher_stays_inside_the_same_command()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, options: 1, crisis: 4, finisher: true, poison: true);
        NativeBattleUiRandomState before = battle.Capture().NativeBattleUiRandom!;
        battle.QueueNativePlayerCommand("squall", "enemy", 5, 0);
        battle.Tick();
        BattleNativeRenzokukenState process = battle.GetNativeRenzokuken("squall")!;
        Assert.NotNull(process.FinisherActionId);
        int contacts = game.ReadNativeRenzokukenResources().Single(resource => resource.MagicId == process.MagicId).Timeline.InputThresholds.Length;

        Until(battle, () => battle.GetNativeRenzokuken("squall") is null);

        NativeBattleUiRandomState expected = before;
        for (int contact = 0; contact < contacts; contact++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, battle.Capture().NativeBattleUiRandom);
        Assert.True(battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy") > contacts);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "squall" && item.Message.StartsWith("Poison:", StringComparison.Ordinal));
    }

    private static int FirstHit(BattleSession battle)
    {
        return battle.Events.First(item => item.Kind == "Damage" && item.TargetId == "enemy").Amount;
    }

    private static void Until(BattleSession battle, Func<bool> complete)
    {
        for (int callback = 0; callback < 5000 && !complete(); callback++)
        {
            battle.AdvanceNativeRenzokukenPresentation(TimeSpan.FromTicks(83334));
        }
        Assert.True(complete());
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, int enemyHp = 50000, bool secondEnemy = false, bool poison = false, bool counter = false, ushort options = 0, byte crisis = 1, bool finisher = false, bool precedingActor = false, byte? chosenFinisher = null)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        if (chosenFinisher is not null)
        {
            rules = rules with { WeaponRecord = rules.WeaponRecord.SetItem(2, 15) };
        }
        NativeBattleAiDefinition? ai = counter ? new([[0], [0], [15, 96, 1, 0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []) : null;
        List<BattleCombatantDefinition> actors =
        [
            new() { Id = "squall", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = character.Weapon, Attack = 80, MaximumHp = 486, CurrentHp = 400, NativeStatus0 = poison ? (ushort)2 : (ushort)0, NativeDeathCount = 0, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(0), NativePlayerStatRules = rules, NativeStatusResistances = rules.ProjectStatusResistances(character.MagicInventory).ToArray(), NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0), NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()) },
            new() { Id = "enemy", Name = "Enemy", ModelId = 61, MaximumHp = enemyHp, CurrentHp = enemyHp, Speed = 1, Defense = 1, NativeAi = ai, NativeStatusResistances = new byte[40], NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(61) }
        ];
        if (secondEnemy)
        {
            actors.Add(new() { Id = "other", Name = "Other", MaximumHp = 50000, CurrentHp = 50000, Speed = 1 });
        }
        if (precedingActor)
        {
            actors.Add(new() { Id = "ally", Name = "Ally", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum });
        }
        BattleSession battle = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = assets.NativeActions.ToArray(), NativeRenzokukenResources = game.ReadNativeRenzokukenResources(), NativeStatusRules = assets.ReadNativeStatusRules(0), NativeBattleUiRandom = new(100), LimitProgress = game.ReadNewGameLimitProgress() with { Options978 = options }, NativeLimitsDisabled = false, Combatants = actors.ToArray() }, BattleAtbMode.TurnBased);
        NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
        {
            candidate = candidate.Draw(out _).Draw(out byte roll);
            if (NativeBattleRenzokukenRules.HasFinisher(crisis, roll) != finisher)
            {
                return false;
            }
            if (chosenFinisher is not byte selected)
            {
                return true;
            }
            candidate.Draw(out byte selection);
            return NativeBattleRenzokukenRules.Finisher(rules.WeaponRecord[2], selection) == selected;
        });
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("squall", crisis)], Random = random });
    }
}
