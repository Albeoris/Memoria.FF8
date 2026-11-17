using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelLifecycleTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattlePartyActorModel LeadAlly = new(new("ally-0"));
    private static readonly BattlePartyActorModel FourthAlly = new(new("ally-3"));
    private static readonly BattlePartyActorModel MissingAlly = new(new("ally-4"));
    private static readonly BattlePartyActorModel LastAlly = new(new("ally-10"));
    private static readonly BattleEnemyActorModel FifthEnemy = new(new("enemy-4"));
    private static readonly BattleEnemyActorModel LastEnemy = new(new("enemy-13"));
    private static readonly BattleEnemyActorModel ForeignEnemy = new(new("wrong-enemy"));
    private static readonly NativeBattleGraphicsResource PartySequence = new([1, 0, 4, 0, 0x91, 0, 17, 0xA0, 0, 0xA1]) { AnimationFrames = [2], TextBank = ["Party text"] };
    private static readonly NativeBattleGraphicsResource EnemySequence = new([1, 0, 4, 0, 0xB8, 12, 4, 0x91, 0, 19, 0x9D, 0xA0, 0, 0xA1]) { AnimationFrames = [2], TextBank = ["Original enemy text"] };

    [Fact]
    public void Bomb_director_and_source_sequence_share_one_logical_contact_in_the_current_frame()
    {
        NativeBattleContactPacket bomb = new(new(FifthEnemy, 0, 0, 0, 0, 0, 0), null) { Occurrence = new(4) };
        NativeBattleContactPacket other = bomb with { Occurrence = new(5) };
        NativeBattleModelLifecycleServices services = new([]);
        services.EmitContact(bomb, origin: NativeBattleContactOrigin.BombGlowDirector);
        services.EmitContact(other, origin: NativeBattleContactOrigin.SourceModelSequence);
        services.EmitContact(bomb with { }, origin: NativeBattleContactOrigin.SourceModelSequence);
        Assert.Equal(new[] { bomb, other }, services.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet));

        services.EmitContact(bomb, origin: NativeBattleContactOrigin.SourceModelSequence);
        Assert.Equal(3, services.ReadOrderedReceipts().Length);
        NativeBattleModelLifecycleServices next = new([]);
        next.EmitContact(bomb, origin: NativeBattleContactOrigin.SourceModelSequence);
        Assert.Equal(bomb, Assert.IsType<NativeBattlePresentationContactReceived>(Assert.Single(next.ReadOrderedReceipts())).Packet);
    }

    [Fact]
    public void Shared_loop_visits_extended_bound_rows_and_emits_ordered_receipts_without_mutating_actors()
    {
        BattleSession session = Session(expanded: true);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        NativeBattleGraphicsState graphics = Graphics(session);
        graphics = graphics with
        {
            ActorModels = graphics.ActorModels.Set(FourthAlly, Sequenced(0)).Set(LastAlly, Sequenced(0)).Set(FifthEnemy, Removing()).Set(LastEnemy, Sequenced(20))
        };
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(roster);
        var before = ObserveActors(session);
        var logicalIndex = (Dictionary<string, BattleCombatant>)typeof(BattleSession).GetField("byId", PrivateInstance)!.GetValue(session)!;
        logicalIndex.Clear();

        NativeBattleGraphicsState advanced = Advance(session, graphics, services);

        Assert.Equal(before, ObserveActors(session));
        Assert.Equal(1, advanced.ActorModels.Get(FourthAlly).SequenceFrame);
        Assert.Equal(1, advanced.ActorModels.Get(LastAlly).SequenceFrame);
        Assert.Equal(0, advanced.ActorModels.Get(FifthEnemy).Flags0);
        Assert.Equal(1, advanced.ActorModels.Get(LastEnemy).SequenceFrame);
        Assert.Collection(services.ReadReceipts(),
            item => Assert.Equal(new NativeBattleModelTextPublished(FourthAlly, "ally-3", "Party text", 17, null), item),
            item => Assert.Equal(new NativeBattleModelTextPublished(LastAlly, "ally-10", "Party text", 17, null), item),
            item => Assert.Equal(new NativeBattleModelParticipantReleased(FifthEnemy, "enemy-4"), item),
            item => Assert.Equal(new NativeBattleModelTextPublished(LastEnemy, "enemy-13", "Original enemy text", 19, BattleTextIdentity.Enemy(43, 0)), item),
            item => Assert.Equal(new NativeBattleModelParticipantSuspended(LastEnemy, "enemy-13", 1), item));
        BattleEvent sound = Assert.Single(session.Events);
        Assert.Equal("NativeSequenceSound", sound.Kind);
        Assert.Equal("enemy-13", sound.ActorId);
        Assert.Equal<uint?>(12, sound.NativeModuleSound?.Sound);
    }

    [Fact]
    public void Sparse_roster_visits_only_bound_owners()
    {
        BattleSession session = Session(expanded: true);
        session.Combatants.Single(actor => actor.Id == "ally-4").BindRosterSlot(null, bindNative: false);
        typeof(BattleSession).GetMethod("RefreshNativePacketBindings", PrivateInstance)!.Invoke(session, null);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        NativeBattleGraphicsState graphics = Graphics(session);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(LastAlly, Sequenced(0)) };
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(roster);

        NativeBattleGraphicsState advanced = Advance(session, graphics, services);

        Assert.False(services.Facts.ContainsKey(MissingAlly));
        Assert.False(advanced.ActorModels.Contains(MissingAlly));
        Assert.Equal("ally-10", Assert.Single(services.ReadReceipts()).ActorId);
    }

    [Fact]
    public void Native_and_hosted_seven_row_loops_agree_on_models_and_route_logical_outputs_to_their_respective_owners()
    {
        BattleSession native = Session(expanded: false, partyResource: EnemySequence);
        BattleSession hosted = Session(expanded: false, partyResource: EnemySequence);
        BattleModelRoster roster = hosted.ReadBattlePresentationRoster();
        BindPresentationResources(hosted, roster, partyResource: EnemySequence);
        NativeBattleGraphicsState graphics = Graphics(hosted);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(LeadAlly, Sequenced(0)) };
        NativeBattleModelLifecycleServices services = hosted.CaptureModelLifecycleServices(roster);
        var before = ObserveActors(hosted);
        BattleCombatant nativeActor = native.Combatants.Single(actor => actor.Id == "ally-0");
        uint initialFlags = nativeActor.NativeCombatantFlags;
        ushort initialStatus = nativeActor.NativeStatus0;

        NativeBattleGraphicsState nativeResult = Advance(native, graphics, null);
        NativeBattleGraphicsState hostedResult = Advance(hosted, graphics, services);

        Assert.Equal(2, nativeResult.ActorModels.Roster.Count);
        Assert.Equal(1, nativeResult.ActorModels.Get(LeadAlly).SequenceFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeResult), NativeBattleTestSnapshots.Json(hostedResult));
        Assert.Equal(initialFlags | 0x40u, nativeActor.NativeCombatantFlags);
        Assert.Equal((ushort)(initialStatus | 1), nativeActor.NativeStatus0);
        Assert.Equal(before, ObserveActors(hosted));
        Assert.Equal(new[] { "NativeSequenceSound", "NativeText" }, native.Events.Select(item => item.Kind));
        Assert.Equal(native.Events[0], Assert.Single(hosted.Events));
        BattleEvent text = native.Events[1];
        Assert.Null(text.NativeTextIdentity);
        Assert.Collection(services.ReadReceipts(),
            item => Assert.Equal(new NativeBattleModelTextPublished(LeadAlly, text.ActorId!, text.Message, text.Amount, text.NativeTextIdentity), item),
            item => Assert.Equal(new NativeBattleModelParticipantSuspended(LeadAlly, "ally-0", 1), item));
    }

    [Fact]
    public void Shared_loop_parses_AB_and_emits_the_registered_process_widget_without_changing_logical_state()
    {
        BattleSession session = Session(expanded: true);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        NativeBattleGraphicsResource ab = new([1, 0, 4, 0, 0xAB, 1, 0, 0xA0, 1, 0xA1, 0xA0, 0, 0xA1]) { AnimationFrames = [15, 9] };
        BindPresentationResources(session, roster, ab);
        NativeBattleGraphicsState graphics = Graphics(session);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(FourthAlly, Sequenced(0)), UiServices = NativeBattleUiServices.Initialize() };
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(roster);
        var before = ObserveActors(session);
        MethodInfo captureLimits = typeof(BattleSession).GetMethod("CaptureNativeLimits", PrivateInstance)!;
        string limits = NativeBattleTestSnapshots.Json((BattleNativeLimitState[])captureLimits.Invoke(session, null)!);

        NativeBattleGraphicsState advanced = Advance(session, graphics, services);

        NativeBattleTriggerWidget widget = Assert.IsType<NativeBattleTriggerWidget>(advanced.TriggerWidget);
        Assert.Equal(FourthAlly, widget.Actor);
        Assert.Equal((byte?)1, widget.SourceSequence);
        Assert.Equal(0, widget.SourceAbOffset);
        Assert.True(widget.Visible);
        Assert.Equal((sbyte)6, advanced.UiServices!.Menu957);
        Assert.Equal(new NativeBattleModelTriggerRegistered(FourthAlly, "ally-3", widget), Assert.Single(services.ReadReceipts()));
        Assert.Equal(before, ObserveActors(session));
        Assert.Equal(limits, NativeBattleTestSnapshots.Json((BattleNativeLimitState[])captureLimits.Invoke(session, null)!));
        Assert.Null(session.Presentation?.Graphics);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void Countdown_timer_selection_uses_signed_facts_captured_from_the_current_allocation()
    {
        BattleSession session = Session(expanded: true);
        BattleCombatant actor = session.Combatants.Single(actor => actor.Id == "ally-3");
        actor.NativeStatusTimers[12] = -1111;
        actor.NativeStatusTimers[10] = -23;
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(session.ReadBattlePresentationRoster());
        actor.NativeStatusTimers[12] = 290;
        MethodInfo timers = typeof(BattleSession).GetMethod("ReadModelCountdownTimers", PrivateInstance)!;

        var (doom, petrify) = ((short, short))timers.Invoke(session, [Graphics(session), FourthAlly, services.Facts[FourthAlly]])!;

        Assert.Equal((short)-1111, doom);
        Assert.Equal((short)-23, petrify);
        var glyphs = NativeBattleStatusCountdowns.Layout50A410(0x82000, 10, 20, 30, 0, doom, petrify);
        Assert.Equal(2, glyphs.Length);
        Assert.All(glyphs, item => Assert.Equal(new byte[] { 0x18, 0 }, item.Glyphs));
    }

    [Theory]
    [InlineData("missing facts")]
    [InlineData("wrong fact identity")]
    [InlineData("unplanned model")]
    [InlineData("wrong late identity")]
    [InlineData("wrong late side")]
    [InlineData("extra hole facts")]
    public void Malformed_hosted_bank_fails_before_any_model_side_effect(string malformed)
    {
        BattleSession session = Session(expanded: true);
        session.Combatants.Single(actor => actor.Id == "ally-4").BindRosterSlot(null, bindNative: false);
        typeof(BattleSession).GetMethod("RefreshNativePacketBindings", PrivateInstance)!.Invoke(session, null);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        NativeBattleGraphicsState graphics = Graphics(session);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(LeadAlly, Sequenced(0)) };
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(roster);
        switch (malformed)
        {
            case "missing facts":
                services = new(services.Facts.Remove(LastEnemy));
                break;
            case "wrong fact identity":
                services = new(services.Facts.SetItem(LastEnemy, services.Facts[LastEnemy] with { ActorId = "wrong" }));
                break;
            case "unplanned model":
                graphics = graphics with { ActorModels = graphics.ActorModels.Add(ForeignEnemy, new(0)) };
                break;
            case "wrong late identity":
                graphics = graphics with { ActorModels = graphics.ActorModels.ReplaceOwner(LastEnemy, ForeignEnemy, new(0)) };
                break;
            case "wrong late side":
                services = new(services.Facts.SetItem(LastEnemy, services.Facts[LastEnemy] with { IsParty = true }));
                break;
            case "extra hole facts":
                services = new(services.Facts.Add(MissingAlly, new("ally-4", true, 0, 0, 0)));
                break;
        }
        NativeBattleEffectMemory memory = session.Presentation!.Memory;
        var actors = ObserveActors(session);

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Advance(session, graphics, services));

        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Same(memory, session.Presentation?.Memory);
        Assert.Equal(actors, ObserveActors(session));
        Assert.Empty(session.Events);
        if (services is not null)
        {
            Assert.Empty(services.ReadReceipts());
        }
    }

    private static BattleSession Session(bool expanded, NativeBattleGraphicsResource? partyResource = null)
    {
        BattleDefinition definition = new()
        {
            Combatants = Enumerable.Range(0, expanded ? 11 : 1).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true })
                .Concat(Enumerable.Range(0, expanded ? 14 : 1).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = "Enemy", ModelId = 30 + index })).ToArray(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            LimitProgress = new(new string('0', 64), 0, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]) { Options978 = 0 }
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [], []) { SummonColors = NativeBattleGteColorInterpolation.CodeConstants45F270 };
        ImmutableArray<NativeBattleGraphicsBinding> bindings = definition.Combatants.Select(actor => new NativeBattleGraphicsBinding(actor.Id, actor.IsPlayer ? (byte)0 : (byte)20, actor.IsPlayer ? partyResource ?? PartySequence : EnemySequence)).ToImmutableArray();
        typeof(BattleSession).GetField("definition", PrivateInstance)!.SetValue(session, definition with { NativeGraphics = new(rules, bindings, []) });
        if (expanded)
        {
            BindPresentationResources(session, session.ReadBattlePresentationRoster());
        }
        NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { ModelColorOverrides = NativeBattleModelColorOverrides.FromColdProcess() });
        NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeStage), NativeBattleStageState.FromColdProcess());
        return session;
    }

    private static void BindPresentationResources(BattleSession session, BattleModelRoster roster, NativeBattleGraphicsResource? extendedPartyResource = null, NativeBattleGraphicsResource? partyResource = null)
    {
        Dictionary<BattleModelOwner, NativeBattleEffectModelBinding> presentationBindings = roster.Owners.ToDictionary(owner => owner, owner =>
        {
            bool ally = owner.Side == BattleSide.Allies;
            NativeBattleGraphicsResource resource = ally ? (owner == FourthAlly ? extendedPartyResource : null) ?? partyResource ?? PartySequence : EnemySequence;
            return new NativeBattleEffectModelBinding(owner, new(0), resource);
        });
        typeof(BattleSession).GetField("presentationNativeTargets", PrivateInstance)!.SetValue(session, presentationBindings);
    }

    private static NativeBattleGraphicsState Graphics(BattleSession session)
    {
        var (roster, aliases) = session.ReadBattlePresentationPacketBindings();
        return NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0), BattleContactTaskStorageContract.ExpandableArena);
    }

    private static NativeBattleGraphicsModel Sequenced(byte modelId)
    {
        return new(2)
        {
            Flags2 = 4,
            Flags8 = 0,
            ModelId4 = modelId,
            ResourceFlags44 = 1,
            Sequence = 1,
            SequenceState = new()
        };
    }

    private static NativeBattleGraphicsModel Removing()
    {
        return new(2)
        {
            ModelId4 = 143,
            Flags2 = 0,
            Flags8 = 0,
            Kind5 = 5,
            Phase6 = 0,
            ResourceFlags44 = 1,
            Transform = new(default, default, 0, 0, 4096)
        };
    }

    private static (uint Flags, ushort Status, int Hp)[] ObserveActors(BattleSession session) => session.Combatants.Select(actor => (actor.NativeCombatantFlags, actor.NativeStatus0, actor.CurrentHp)).ToArray();

    private static NativeBattleGraphicsState Advance(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices? services)
    {
        return (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("AdvanceNativeLoadedModels", PrivateInstance)!.Invoke(session, [graphics, 0u, services])!;
    }

    private static void Set(BattleSession session, string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);
}
