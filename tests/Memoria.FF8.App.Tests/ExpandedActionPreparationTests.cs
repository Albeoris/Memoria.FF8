using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Tests;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedActionPreparationTests
{
    private static readonly MethodInfo AdvanceExpandedAction = typeof(BattleSession).GetMethod("AdvanceExpandedAction", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo EffectFactory = typeof(BattleSession).GetField("expandedEffectFactory", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void InstantVictoryReleasesCommittedIfritFireAndRestoresTheHumanActor()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition definition = CreateFireDefinition(game, assets, doubleCast: false) with
        {
            GuardianFormsEnabled = true,
            GuardianFormResources = [assets.ReadIfritGuardianFormResources()],
            GuardianForces = [new(2, 80, 100, true) { DeathCount = 0 }]
        };
        definition.Combatants[0] = definition.Combatants[0] with { CurrentHp = 37, EquipmentIds = [2], GuardianForceCompatibility = new ushort[16], NativeStatusResistances = new byte[40] };
        BattleSession initial = new(definition, BattleAtbMode.TurnBased);
        BattleSession battle = BattleSession.Restore(initial.Capture() with
        {
            GuardianForms = new(true, 1, [new("caster", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 73) { EntryCompleted = true }])
        });
        BattleCombatant caster = battle.Combatants[0];
        GameWorld.AttachExpandedEffects(battle, game, assets);
        battle.QueueGuardianFormAction(caster.Id, "enemy-a", BattleGuardianFormAction.Fire);
        ReachCommit(battle);
        Advance(battle);

        Assert.NotNull(battle.ExpandedEffect);
        Assert.NotNull(battle.GetGuardianFormVisual(caster.Id));
        Assert.Equal(3, Stock(battle));
        Assert.True(battle.Combatants[1].CurrentHp < definition.Combatants[1].CurrentHp);
        int damageEvents = battle.Events.Count(entry => entry.Kind == "Damage");

        battle.InstantVictory();

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Null(battle.GetGuardianForm(caster.Id));
        Assert.Null(battle.GetGuardianFormVisual(caster.Id));
        Assert.Null(battle.ExpandedEffect);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Same(caster, battle.Combatants[0]);
        Assert.Equal(37, caster.CurrentHp);
        Assert.Equal(3, Stock(battle));
        Assert.Equal(damageEvents, battle.Events.Count(entry => entry.Kind == "Damage"));
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EffectContact");
        Assert.Empty(battle.Capture().GuardianForms!.Forms);
    }

    [Fact]
    public void SecondInstalledDoublePresenterFailureDisposesTheFirstAndPublishesNothing()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition definition = CreateFireDefinition(game, assets, doubleCast: true);
        BattleSession live = new(definition, BattleAtbMode.TurnBased);
        GameWorld.AttachExpandedEffects(live, game, assets);
        var factory = Assert.IsType<Func<BattleSession, BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, GuardianFormInvocationMode?, NativeBattleEffectPresentation>>(EffectFactory.GetValue(live));
        BattleCombatant[] actors = live.Combatants.ToArray();
        NativeBattleEffectPresentation? first = null;
        NativeBattleGraphicsState? beforeDisposal = null;
        string? before = null;
        int calls = 0;
        InvalidOperationException failure = new("Injected second Fire presenter resource failure.");
        Func<BattleSession, BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, GuardianFormInvocationMode?, NativeBattleEffectPresentation> wrapped = (owner, caster, native, sequence, contacts, mode) =>
        {
            calls++;
            Assert.NotSame(live, owner);
            Assert.Same(owner.Combatants[0], caster);
            Assert.Equal(before, Snapshot(live));
            Assert.Null(live.ExpandedEffect);
            Assert.Equal(calls == 1 ? "enemy-a" : "enemy-b", Assert.Single(contacts).RecipientId);
            if (calls == 2)
            {
                beforeDisposal = first!.Graphics;
                throw failure;
            }
            first = factory(owner, caster, native, sequence, contacts, mode);
            return first;
        };
        EffectFactory.SetValue(live, wrapped);
        QueueFire(live, doubleCast: true);
        before = Snapshot(live);
        long resets = live.DiagnosticResetCount;

        Assert.Same(failure, Assert.Throws<TargetInvocationException>(() => Advance(live)).InnerException);
        Assert.Equal(2, calls);
        Assert.Equal(before, Snapshot(live));
        Assert.Null(live.ExpandedEffect);
        Assert.Equal(resets, live.DiagnosticResetCount);
        AssertActorIdentity(actors, live);
        // Dispose restores presentation graphics and model ownership from the presenter's initial state.
        Assert.NotNull(beforeDisposal);
        Assert.NotSame(beforeDisposal, first!.Graphics);
    }

    [Fact]
    public void InstalledFirePublishesOneStockPaymentAndTheSameLogicalHitAsGraphicslessFire()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition definition = CreateFireDefinition(game, assets, doubleCast: false);
        BattleSession live = new(definition, BattleAtbMode.TurnBased);
        GameWorld.AttachExpandedEffects(live, game, assets);
        BattleCombatant[] actors = live.Combatants.ToArray();
        QueueFire(live, doubleCast: false);
        long resets = live.DiagnosticResetCount;
        BattleSnapshot before = live.Capture();
        BattleSession control = BattleSession.Restore(before);
        Assert.Equal(Snapshot(control), Snapshot(live));

        Advance(control);
        Advance(live);
        Assert.NotNull(live.ExpandedEffect);
        Assert.Equal(2, Stock(live));
        int hitPointsAfterCommit = actors[1].CurrentHp;
        for (int callback = 0; callback < 500 && live.ActiveAction is not null; callback++)
        {
            Advance(live);
        }

        Assert.Null(live.ActiveAction);
        Assert.Null(live.ExpandedEffect);
        Assert.Equal(resets, live.DiagnosticResetCount);
        AssertActorIdentity(actors, live);
        Assert.Equal(2, Stock(control));
        Assert.Equal(Stock(control), Stock(live));
        Assert.Equal(hitPointsAfterCommit, actors[1].CurrentHp);
        Assert.True(actors[1].CurrentHp < before.Combatants[1].CurrentHp);
        Assert.Equal(control.Combatants.Select(actor => actor.CurrentHp), live.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(control.Capture().Random), JsonSerializer.Serialize(live.Capture().Random));
        Assert.Equal(JsonSerializer.Serialize(control.Capture().QueuedActions), JsonSerializer.Serialize(live.Capture().QueuedActions));
        // Hosted presentation adds audiovisual receipts and delays the impact receipt until its module emits contact.
        BattleEvent controlHit = Assert.Single(control.Events, entry => entry.Kind == "EffectContact");
        BattleEvent hostedHit = Assert.Single(live.Events, entry => entry.Kind == "EffectContact");
        Assert.Equal((controlHit.ActorId, controlHit.TargetId, controlHit.Amount), (hostedHit.ActorId, hostedHit.TargetId, hostedHit.Amount));
        Assert.Single(live.Events, entry => entry.Kind == "ActionComplete");
    }

    private static BattleDefinition CreateFireDefinition(GameAssets game, BattleAssets assets, bool doubleCast)
    {
        BattleNativeAction fire = Assert.Single(assets.NativeActions, action => action is { Command: 2, ActionId: 1 });
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().ChangeMagicStock(0, fire.ActionId, 3, MagicOperationReason.ScriptReward, out _);
        int weapon = assets.WeaponDefinitions.First(entry => entry.CharacterId == 0).Id;
        return new()
        {
            UsesExpandedRoster = true,
            StageId = 0,
            MagicVitals = vitals,
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeActions = [fire],
            ActionDefinitions = [new(BattleAbility.Magic, fire.ActionId, fire.Name!, BattleActionEffect.Native, fire.Power, false) { NativeAction = fire }],
            Combatants =
            [
                new() { Id = "caster", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = weapon, MagicRecordId = 0, InitialAtb = BattleSession.AtbMaximum, Magic = 100, NativeStatus1 = doubleCast ? 0x20000u : 0u, Abilities = [BattleAbility.Magic] },
                new() { Id = "enemy-a", Name = "Enemy A", ModelId = 1, MaximumHp = 60000, CurrentHp = 60000, Speed = 1 },
                new() { Id = "enemy-b", Name = "Enemy B", ModelId = 1, MaximumHp = 60000, CurrentHp = 60000, Speed = 1 }
            ]
        };
    }

    private static void QueueFire(BattleSession battle, bool doubleCast)
    {
        Assert.Equal(3, Stock(battle));
        battle.QueueAction("caster", "enemy-a", BattleAbility.Magic, 1, magicTargets: doubleCast ? ["enemy-a", "enemy-b"] : null);
        ReachCommit(battle);
    }

    private static void ReachCommit(BattleSession battle)
    {
        battle.Tick();
        while (battle.ActiveAction is { RemainingFrames: > 1, Phase: BattleActionPhase.Windup })
        {
            battle.Tick();
        }
        Assert.Equal(BattleActionPhase.Windup, battle.ActiveAction?.Phase);
        Assert.Equal(1, battle.ActiveAction?.RemainingFrames);
    }

    private static int Stock(BattleSession battle) => battle.MagicVitals!.GetMagicCount(0, 1);

    private static void AssertActorIdentity(BattleCombatant[] actors, BattleSession battle)
    {
        Assert.Equal(actors.Length, battle.Combatants.Count);
        for (int index = 0; index < actors.Length; index++)
        {
            Assert.Same(actors[index], battle.Combatants[index]);
        }
    }

    private static void Advance(BattleSession battle) => AdvanceExpandedAction.Invoke(battle, [battle.ActiveAction!]);

    private static string Snapshot(BattleSession battle) => JsonSerializer.Serialize(battle.Capture());
}
