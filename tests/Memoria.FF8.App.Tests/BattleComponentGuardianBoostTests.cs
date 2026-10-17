using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class BattleComponentGuardianBoostTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks(333_334);

    [Fact]
    public void Learned_Boost_holds_component_GF_contact_until_the_real_widget_closes_and_restores_without_repeating_math()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = CreateBattle(assets, true);
        StartGuardian(battle);
        NativeBattleGuardianBoost initial = battle.NativeGuardianBoost!;
        BattleNativeAction native = battle.Capture().Definition.ActionDefinitions.Single(action => action.Ability == BattleAbility.GuardianForce && action.Id == 0).NativeAction!;
        Assert.Equal(15 * native.RawRecord[128], initial.WindowRemaining);
        Assert.Equal(15 * native.RawRecord[129], initial.Remaining);
        int hp = battle.Combatants[1].CurrentHp;
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(BattleActionPhase.Windup, battle.ActiveAction!.Phase);
        Assert.Equal(initial, battle.NativeGuardianBoost);
        Assert.Equal(hp, battle.Combatants[1].CurrentHp);
        UntilPresentation(battle, () => battle.NativeGuardianBoost?.Phase == 4);
        BattleSession baseline = BattleSession.Restore(battle.Capture());
        long frameBeforePress = battle.Frame;

        GameWorld.ExecuteBattleCommand(battle, new(BattleInputKind.GuardianBoost));
        battle.AdvanceComponentGuardianBoostPresentation(Callback);
        baseline.AdvanceComponentGuardianBoostPresentation(Callback);

        Assert.Equal((ushort)76, battle.NativeGuardianBoost!.Percent);
        Assert.Equal(frameBeforePress, battle.Frame);
        Assert.Equal(hp, battle.Combatants[1].CurrentHp);
        string midway = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(midway)!);
        BattleSnapshot midwayState = battle.Capture();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(midwayState with { NativeActionScratch = midwayState.NativeActionScratch! with { GuardianFormulaE1E = true } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(midwayState with { NativeGuardianBoost = midwayState.NativeGuardianBoost! with { Phase = 6, Enabled = false } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(midwayState with { NativeGuardianBoost = midwayState.NativeGuardianBoost! with { Enabled = false } }));
        for (int callback = 0; callback < 5000 && battle.NativeGuardianBoost?.Phase != 6; callback++)
        {
            battle.AdvanceComponentGuardianBoostPresentation(Callback);
            baseline.AdvanceComponentGuardianBoostPresentation(Callback);
            restored.AdvanceComponentGuardianBoostPresentation(Callback);
        }
        Assert.Equal((byte)6, battle.NativeGuardianBoost!.Phase);
        Assert.Null(battle.ActiveAction!.NativeAction);
        string completedMath = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(completedMath, JsonSerializer.Serialize(restored.Capture()));
        restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(completedMath)!);
        Assert.Equal(completedMath, JsonSerializer.Serialize(restored.Capture()));
        battle.AdvanceComponentGuardianBoostPresentation(Callback);
        baseline.AdvanceComponentGuardianBoostPresentation(Callback);
        restored.AdvanceComponentGuardianBoostPresentation(Callback);
        Assert.Null(battle.NativeGuardianBoost);
        Assert.Null(baseline.NativeGuardianBoost);
        Assert.Equal(frameBeforePress, battle.Frame);
        Assert.Equal((byte)76, battle.Capture().NativeActionScratch!.GuardianBoostPercent);
        Assert.Equal((byte)100, baseline.Capture().NativeActionScratch!.GuardianBoostPercent);
        int damage = Assert.Single(battle.Events, item => item.Kind == "Damage").Amount;
        Assert.True(damage > 0);
        Assert.True(Assert.Single(baseline.Events, item => item.Kind == "Damage").Amount > damage);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        for (int callback = 0; callback < 128 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(damage, Assert.Single(battle.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(hp - damage, battle.Combatants[1].CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Boost_uses_the_remapped_original_square_edge_and_modal_HUD_reads_the_actual_window()
    {
        KeyboardInput keyboard = new();
        keyboard.Profile = keyboard.Profile.WithBinding(ControlAction.Card, ControlDevice.Keyboard, new InputChord([Keys.T], []));
        keyboard.Read(default, true);
        keyboard.Read(new KeyboardState(Keys.T), true);
        Assert.True(keyboard.GuardianBoostRequested);
        Assert.Equal((ushort)0x80, keyboard.HeldFieldButtons);
        Assert.False(keyboard.GunbladeTriggerRequested);
        keyboard.Read(new KeyboardState(Keys.T), true);
        Assert.False(keyboard.GuardianBoostRequested);
        using GameAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = CreateBattle(assets, true);
        StartGuardian(battle);
        UntilPresentation(battle, () => battle.NativeGuardianBoost?.Phase == 4);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }) { NativeButtonBindingHint = keyboard.NativeButtonBindingHint };
        string before = JsonSerializer.Serialize(battle.Capture());

        hud.BuildView();
        hud.Navigate(MenuInput.Confirm);

        Assert.Empty(hud.Commands);
        Assert.Contains("GF Boost 0%", hud.Message);
        Assert.Contains("T: PRESS", hud.Message);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.True(battle.PressNativeGuardianBoost());
        Assert.False(battle.PressNativeGuardianBoost());
    }

    [Fact]
    public void A_current_press_on_the_last_allowed_window_callback_is_consumed_before_that_window_closes()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = CreateBattle(assets, true);
        StartGuardian(battle);
        UntilPresentation(battle, () => battle.NativeGuardianBoost?.Phase == 4);
        BattleSnapshot saved = battle.Capture();
        // Explicit pre-callback diagnostic boundary: the existing allowed window has one published frame remaining.
        battle = BattleSession.Restore(saved with { NativeGuardianBoost = saved.NativeGuardianBoost! with { Remaining = 20, WindowRemaining = 1 } });
        int hp = battle.Combatants[1].CurrentHp;

        GameWorld.ExecuteBattleCommand(battle, new(BattleInputKind.GuardianBoost));
        battle.AdvanceComponentGuardianBoostPresentation(Callback);

        Assert.Equal((ushort)76, battle.NativeGuardianBoost!.Percent);
        Assert.Equal((sbyte)0, battle.NativeGuardianBoost.Window);
        Assert.False(battle.NativeGuardianBoost.PendingPress);
        Assert.False(battle.Capture().NativeActionScratch!.GuardianFormulaE1E);
        Assert.Equal(hp, battle.Combatants[1].CurrentHp);
    }

    [Fact]
    public void Unlearned_Boost_keeps_the_declared_component_contact_and_an_unknown_learned_owner_is_not_assumed_false()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = CreateBattle(assets, false);
        StartGuardian(battle);
        Assert.Null(battle.NativeGuardianBoost);
        for (int frame = 0; frame < 100 && !battle.Events.Any(item => item.Kind == "Damage"); frame++)
        {
            battle.Tick();
        }
        Assert.Single(battle.Events, item => item.Kind == "Damage");
        Assert.Equal((byte)100, battle.Capture().NativeActionScratch!.GuardianBoostPercent);
        Assert.False(battle.PressNativeGuardianBoost());
        BattleSession unknown = CreateBattle(assets, false);
        BattleSnapshot saved = unknown.Capture();
        unknown = BattleSession.Restore(saved with { Definition = saved.Definition with { ActionDefinitions = saved.Definition.ActionDefinitions.Select(action => action.Ability == BattleAbility.GuardianForce && action.Id == 0 ? action with { NativeGuardianBoostAvailable = null } : action).ToArray() } });
        Assert.Throws<NotSupportedException>(() => StartGuardian(unknown));
    }

    private static void StartGuardian(BattleSession battle)
    {
        BattleCombatant party = battle.Combatants.Single(actor => actor.IsPlayer && actor.ModelId == 0);
        BattleCombatant enemy = battle.Combatants.Single(actor => !actor.IsPlayer);
        Assert.True(party.IsReady, $"Caster HP={party.CurrentHp}, ATB={party.Atb}, busy={party.IsBusy}.");
        Assert.Contains(BattleAbility.GuardianForce, party.Abilities);
        Assert.Contains(0, party.EquipmentIds);
        Assert.False(enemy.Untargetable);
        Assert.True(enemy.IsActive);
        Assert.True(enemy.IsAlive);
        battle.QueueAction(party.Id, enemy.Id, BattleAbility.GuardianForce, 0);
        for (int callback = 0; callback < 128 && battle.ActiveAction?.NativeQueuedCommand != 254; callback++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)254, battle.ActiveAction?.NativeQueuedCommand);
    }

    private static void UntilPresentation(BattleSession battle, Func<bool> complete)
    {
        for (int callback = 0; callback < 5000 && !complete(); callback++)
        {
            battle.AdvanceComponentGuardianBoostPresentation(Callback);
        }
        Assert.True(complete());
    }

    private static BattleSession CreateBattle(GameAssets assets, bool learned)
    {
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        vitals = vitals with { CombatParty = [0, 255, 255] };
        int bit = Assert.Single(Enumerable.Range(19, 9), index => (vitals.Definitions.GuardianForceModifiers[index - 19].Flags & 1) != 0);
        NativeGuardianForceVital gf = vitals.GuardianForces[0];
        vitals.GuardianForces[0] = gf with { LearnedBank = gf.LearnedBank with { High = learned ? gf.LearnedBank.High | (1UL << bit) : gf.LearnedBank.High & ~(1UL << bit) } };
        int guardianCommand = 20 + Enumerable.Range(0, vitals.MenuDefinitions.CommandAbilityCommands.Length).Single(index => vitals.MenuDefinitions.CommandAbilityCommands[index] == 3);
        vitals = vitals.SetJunctionAbility(0, 0, guardianCommand, true);
        GameSession session = new(GameSession.CreateColdProcess().State with { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), InputConfiguration = assets.ReadNewGameInputConfiguration(), Economy = assets.ReadNewGameEconomy(), BattleVariables = NativeBattleVariableState.CreateNewGame(), LimitProgress = assets.ReadNewGameLimitProgress(), SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(), PocketStation = assets.ReadNewGamePocketStation(), ScannedModels = assets.ReadNewGameScannedModels(), ShotAvailability = assets.ReadNewGameShotAvailability(), GlobalRandom = assets.ReadNewGameGlobalRandom(), KnownMagic = assets.ReadNewGameKnownMagic() });
        using GameWorld world = new(session, assets.GameDirectory);
        BattleDefinition original = world.CreateEncounterBattle(512);
        BattleCombatantDefinition caster = original.Combatants.Single(actor => actor.IsPlayer && actor.ModelId == 0);
        BattleCombatantDefinition enemy = original.Combatants.First(actor => !actor.IsPlayer && actor.InitialActive);
        // This independent formation512 component owns one original enabled enemy and one authentic caster.
        BattleDefinition definition = original with { NativeGraphics = null, NativeClockRules = null, NativeEntry = null, Combatants = [caster with { InitialAtb = BattleSession.AtbMaximum, GuardianForceCompatibility = new ushort[16] }, enemy with { InitialAtb = 0, Speed = 1, MaximumHp = 10000, CurrentHp = 10000, NativeAi = enemy.NativeAi! with { Scripts = [[0], [0], [0], [0], [0]] } }] };
        return GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.TurnBased);
    }
}
