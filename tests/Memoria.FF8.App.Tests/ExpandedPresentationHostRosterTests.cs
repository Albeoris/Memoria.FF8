using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Tests;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedPresentationHostRosterTests
{
    private static readonly FieldInfo PresenterSession = typeof(NativeBattleEffectPresentation).GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo AdvanceExpandedAction = typeof(BattleSession).GetMethod("AdvanceExpandedAction", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ActionPackets = typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(4, 5, 7, 15)]
    [InlineData(11, 14, 14, 24)]
    public void Installed_fire_binds_the_entire_roster_and_emits_one_selected_contact(int allies, int enemies, byte sourceRow, byte targetRow)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, allies, enemies);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        GameWorld.AttachExpandedEffects(battle, game, assets);
        BattleModelRoster roster = battle.ReadExpandedPresentationRoster();
        string caster = roster[sourceRow].ActorId;
        string recipient = roster[targetRow].ActorId;
        Assert.Equal(3, battle.MagicVitals!.GetMagicCount(0, 1));
        battle.QueueAction(caster, recipient, BattleAbility.Magic, 1);

        TickUntil(battle, () => battle.ExpandedEffect is not null);
        NativeBattleEffectPresentation presenter = Assert.IsType<NativeBattleEffectPresentation>(battle.ExpandedEffect);
        AssertFullBindings(presenter, roster, allies, enemies, sourceRow);
        NativeBattleActionPacketsState packets = Packets(presenter);
        Assert.Equal(1, packets.ContactCount);
        Assert.Equal(sourceRow, packets.Headers[0].ReadByte(0));
        Assert.Equal(targetRow, packets.ReadContact(0).Primary.ActorIndex);
        Assert.Equal(2, battle.MagicVitals!.GetMagicCount(0, 1));

        int committedHp = battle.Combatants.Single(actor => actor.Id == recipient).CurrentHp;
        TickUntil(battle, () => battle.ActiveAction is null);
        BattleEvent impact = Assert.Single(battle.Events, entry => entry.Kind == "EffectContact");
        Assert.Equal((caster, recipient), (impact.ActorId, impact.TargetId));
        Assert.Equal(committedHp, battle.Combatants.Single(actor => actor.Id == recipient).CurrentHp);
        Assert.Equal(2, battle.MagicVitals!.GetMagicCount(0, 1));
    }

    [Fact]
    public void Installed_fire_host_prepares_all_ordered_enemy_contacts_at_full_roster()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, 11, 14);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        GameWorld.AttachExpandedEffects(battle, game, assets);
        BattleModelRoster roster = battle.ReadExpandedPresentationRoster();
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == roster[14].ActorId);
        byte[] targetRows = roster.EnemyRows.Select(row => checked((byte)row)).Reverse().ToArray();
        ImmutableArray<ExpandedBattleContact> contacts = targetRows.Select(row =>
            new ExpandedBattleContact(roster[row].ActorId, new(new(row, 0, 0, 0, 0, 50, 0), new(255, 0, 0, 0, 0, 0, 0)))).ToImmutableArray();
        string before = JsonSerializer.Serialize(battle.Capture());

        using NativeBattleEffectPresentation presenter = battle.ExpandedPresentationHost!.PrepareEffect(battle, caster, Assert.Single(definition.NativeActions), 1, contacts, null);
        AssertFullBindings(presenter, roster, 11, 14, 14);
        NativeBattleActionPacketsState packets = Packets(presenter);
        Assert.Equal(14, packets.ContactCount);
        Assert.Equal(14, packets.Headers[0].ReadContactCount());
        Assert.Equal(targetRows, Enumerable.Range(0, packets.ContactCount).Select(index => packets.ReadContact(index).Primary.ActorIndex).ToArray());
        List<byte> presented = [];
        for (int frame = 0; frame < 500 && !presenter.Complete; frame++)
        {
            presented.AddRange(presenter.AdvanceCurrent().Contacts.Select(packet => packet.Primary.ActorIndex));
        }
        Assert.True(presenter.Complete);
        Assert.Equal(targetRows, presented);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Theory]
    [InlineData(4, 5, 7, 15)]
    [InlineData(11, 14, 14, 24)]
    public void Ordinary_installed_attack_matches_graphicsless_commit_for_an_expanded_roster(int allies, int enemies, byte sourceRow, byte targetRow)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, allies, enemies);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        GameWorld.AttachExpandedEffects(battle, game, assets);
        BattleModelRoster roster = battle.ReadExpandedPresentationRoster();
        string caster = roster[sourceRow].ActorId;
        string recipient = roster[targetRow].ActorId;
        ExpandedBattleSceneFrame frame = Assert.IsType<ExpandedBattleSceneFrame>(battle.ExpandedPresentationHost!.ReadSceneFrame(battle));
        Assert.True(frame.Positions.ContainsKey(caster));
        Assert.True(frame.Positions.ContainsKey(recipient));
        battle.QueueAttack(caster, recipient);
        BattleSession control = BattleSession.Restore(battle.Capture());

        TickUntil(battle, () => battle.Events.Any(entry => entry.Kind == "ActionComplete"));
        TickUntil(control, () => control.Events.Any(entry => entry.Kind == "ActionComplete"));

        Assert.Null(battle.ExpandedEffect);
        Assert.Equal(JsonSerializer.Serialize(control.Capture()), JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal((caster, recipient), Assert.Single(battle.Events.Where(entry => entry.Kind == "ActionComplete").Select(entry => (entry.ActorId, entry.TargetId))));
    }

    [Fact]
    public void A_presenter_prepared_for_another_source_is_disposed_without_committing_the_action()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, 4, 5);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        GameWorld.AttachExpandedEffects(battle, game, assets);
        IExpandedBattlePresentationHost host = battle.ExpandedPresentationHost!;
        typeof(BattleSession).GetField("expandedPresentationHost", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, null);
        NativeBattleEffectPresentation? wrong = null;
        NativeBattleGraphicsState? beforeDisposal = null;
        BattleModelRoster roster = battle.ReadExpandedPresentationRoster();
        string casterId = roster[7].ActorId;
        string targetId = roster[15].ActorId;
        battle.AttachExpandedEffectFactory((candidate, _, native, sequence, contacts, mode) =>
        {
            BattleCombatant other = candidate.Combatants.Single(actor => actor.Id == roster[0].ActorId);
            wrong = host.PrepareEffect(candidate, other, native, sequence, contacts, mode);
            Assert.Equal(0, wrong.Source.Index);
            beforeDisposal = wrong.Graphics;
            return wrong;
        });
        battle.QueueAction(casterId, targetId, BattleAbility.Magic, 1);
        TickUntil(battle, () => battle.ActiveAction is { Phase: BattleActionPhase.Windup, RemainingFrames: 1 });
        string before = JsonSerializer.Serialize(battle.Capture());

        InvalidDataException failure = Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => AdvanceExpandedAction.Invoke(battle, [battle.ActiveAction!])).InnerException);
        Assert.Contains("roster and caster", failure.Message);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(3, battle.MagicVitals!.GetMagicCount(0, 1));
        Assert.Null(battle.ExpandedEffect);
        Assert.NotNull(beforeDisposal);
        Assert.NotSame(beforeDisposal, wrong!.Graphics);
    }

    private static void AssertFullBindings(NativeBattleEffectPresentation presenter, BattleModelRoster roster, int allies, int enemies, int source)
    {
        BattleModelRoster presented = Assert.IsType<BattleModelRoster>(presenter.Graphics.PresentationRoster);
        Assert.Equal(roster.Rows.ToArray(), presented.Rows.ToArray());
        Assert.Equal(roster.ActorCount, presenter.Graphics.Models.Length);
        Assert.Equal(allies, presented.PartyRows.Count());
        Assert.Equal(enemies, presented.EnemyRows.Count());
        Assert.Equal(source, presenter.Source.Index);
        Assert.Equal(roster.Rows.Select(actor => actor?.ActorId), presenter.Graphics.ActorIds);
    }

    private static NativeBattleActionPacketsState Packets(NativeBattleEffectPresentation presenter)
    {
        return (NativeBattleActionPacketsState)ActionPackets.GetValue(PresenterSession.GetValue(presenter))!;
    }

    private static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets, int allies, int enemies)
    {
        BattleDefinition baseDefinition = ExpandedBattleSceneLayoutTests.CreateDefinitionForEncounter(assets, allies, enemies, assets.LoadEncounter(7));
        BattleNativeAction fire = Assert.Single(assets.NativeActions, action => action is { Command: 2, ActionId: 1 });
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().ChangeMagicStock(0, fire.ActionId, 3, MagicOperationReason.ScriptReward, out _);
        BattleCombatantDefinition caster = baseDefinition.Combatants[0] with
        {
            MagicRecordId = 0,
            Magic = 100,
            Attack = 100,
            InitialAtb = BattleSession.AtbMaximum,
            Abilities = [BattleAbility.Attack, BattleAbility.Magic],
            NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0)
        };
        return baseDefinition with
        {
            MagicVitals = vitals,
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeActions = [fire],
            ActionDefinitions = [new(BattleAbility.Magic, fire.ActionId, fire.Name!, BattleActionEffect.Native, fire.Power, false) { NativeAction = fire }],
            Combatants = [.. baseDefinition.Combatants[1..allies].Select(actor => actor with { InitialAtb = 0 }), caster, .. baseDefinition.Combatants[allies..].Select(actor => actor with { InitialAtb = 0 })]
        };
    }

    private static void TickUntil(BattleSession battle, Func<bool> reached)
    {
        for (int frame = 0; frame < 500 && !reached(); frame++)
        {
            battle.Tick();
        }
        Assert.True(reached());
    }
}
