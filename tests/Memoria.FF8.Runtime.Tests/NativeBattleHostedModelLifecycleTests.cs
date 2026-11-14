using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelLifecycleTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeBattleGraphicsResource PartySequence = new([1, 0, 4, 0, 0x91, 0, 17, 0xA0, 0, 0xA1]) { AnimationFrames = [2], TextBank = ["Party text"] };
    private static readonly NativeBattleGraphicsResource EnemySequence = new([1, 0, 4, 0, 0xB8, 12, 4, 0x91, 0, 19, 0x9D, 0xA0, 0, 0xA1]) { AnimationFrames = [2], TextBank = ["Original enemy text"] };

    [Fact]
    public void Shared_loop_visits_extended_bound_rows_and_emits_ordered_receipts_without_mutating_actors()
    {
        BattleSession session = Session(expanded: true);
        BattleModelRoster roster = session.ReadExpandedPresentationRoster();
        NativeBattleGraphicsState graphics = Graphics(roster);
        graphics = graphics with
        {
            Models = graphics.Models.SetItem(7, Sequenced(7, 0)).SetItem(14, Sequenced(14, 0)).SetItem(15, Removing(15)).SetItem(24, Sequenced(24, 20))
        };
        NativeBattleModelLifecycleServices services = session.CaptureExpandedModelLifecycleServices(roster);
        var before = ObserveActors(session);
        var logicalIndex = (Dictionary<string, BattleCombatant>)typeof(BattleSession).GetField("byId", PrivateInstance)!.GetValue(session)!;
        logicalIndex.Clear();

        NativeBattleGraphicsState advanced = Advance(session, graphics, services);

        Assert.Equal(before, ObserveActors(session));
        Assert.Equal(1, advanced.Models[7].SequenceFrame);
        Assert.Equal(1, advanced.Models[14].SequenceFrame);
        Assert.Equal(0, advanced.Models[15].Flags0);
        Assert.Equal(1, advanced.Models[24].SequenceFrame);
        Assert.Collection(services.ReadReceipts(),
            item => Assert.Equal(new NativeBattleModelTextPublished(7, "ally-3", "Party text", 17, null), item),
            item => Assert.Equal(new NativeBattleModelTextPublished(14, "ally-10", "Party text", 17, null), item),
            item => Assert.Equal(new NativeBattleModelParticipantReleased(15, "enemy-4"), item),
            item => Assert.Equal(new NativeBattleModelTextPublished(24, "enemy-13", "Original enemy text", 19, BattleTextIdentity.Enemy(43, 0)), item),
            item => Assert.Equal(new NativeBattleModelParticipantSuspended(24, "enemy-13", 1), item));
        BattleEvent sound = Assert.Single(session.Events);
        Assert.Equal("NativeSequenceSound", sound.Kind);
        Assert.Equal("enemy-13", sound.ActorId);
        Assert.Equal<uint?>(12, sound.NativeModuleSound?.Sound);
    }

    [Fact]
    public void Sparse_roster_skips_its_placeholder_even_if_its_flags_claim_loaded()
    {
        BattleSession session = Session(expanded: true);
        session.Combatants.Single(actor => actor.Id == "ally-4").BindRosterSlot(null, bindNative: false);
        BattleModelRoster roster = session.ReadExpandedPresentationRoster();
        NativeBattleGraphicsState graphics = Graphics(roster);
        NativeBattleGraphicsModel placeholder = new(8, 2);
        graphics = graphics with { Models = graphics.Models.SetItem(8, placeholder).SetItem(14, Sequenced(14, 0)) };
        NativeBattleModelLifecycleServices services = session.CaptureExpandedModelLifecycleServices(roster);

        NativeBattleGraphicsState advanced = Advance(session, graphics, services);

        Assert.Null(services.Facts[8]);
        Assert.Same(placeholder, advanced.Models[8]);
        Assert.Equal("ally-10", Assert.Single(services.ReadReceipts()).ActorId);
    }

    [Fact]
    public void Native_and_hosted_seven_row_loops_agree_on_models_and_route_logical_outputs_to_their_respective_owners()
    {
        BattleSession native = Session(expanded: false, partyResource: EnemySequence);
        BattleSession hosted = Session(expanded: false, partyResource: EnemySequence);
        BattleModelRoster roster = hosted.ReadExpandedPresentationRoster();
        BindPresentationResources(hosted, roster, partyResource: EnemySequence);
        NativeBattleGraphicsState graphics = Graphics(roster);
        graphics = graphics with { Models = graphics.Models.SetItem(0, Sequenced(0, 0)) };
        NativeBattleModelLifecycleServices services = hosted.CaptureExpandedModelLifecycleServices(roster);
        var before = ObserveActors(hosted);
        BattleCombatant nativeActor = native.Combatants.Single(actor => actor.Id == "ally-0");
        uint initialFlags = nativeActor.NativeCombatantFlags;
        ushort initialStatus = nativeActor.NativeStatus0;

        NativeBattleGraphicsState nativeResult = Advance(native, graphics with { PresentationRoster = null }, null);
        NativeBattleGraphicsState hostedResult = Advance(hosted, graphics, services);

        Assert.Equal(7, nativeResult.Models.Length);
        Assert.Equal(1, nativeResult.Models[0].SequenceFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeResult), NativeBattleTestSnapshots.Json(hostedResult with { PresentationRoster = null }));
        Assert.Equal(initialFlags | 0x40u, nativeActor.NativeCombatantFlags);
        Assert.Equal((ushort)(initialStatus | 1), nativeActor.NativeStatus0);
        Assert.Equal(before, ObserveActors(hosted));
        Assert.Equal(new[] { "NativeSequenceSound", "NativeText" }, native.Events.Select(item => item.Kind));
        Assert.Equal(native.Events[0], Assert.Single(hosted.Events));
        BattleEvent text = native.Events[1];
        Assert.Null(text.NativeTextIdentity);
        Assert.Collection(services.ReadReceipts(),
            item => Assert.Equal(new NativeBattleModelTextPublished(0, text.ActorId!, text.Message, text.Amount, text.NativeTextIdentity), item),
            item => Assert.Equal(new NativeBattleModelParticipantSuspended(0, "ally-0", 1), item));
    }

    [Fact]
    public void Shared_loop_parses_AB_and_emits_the_registered_process_widget_without_changing_logical_state()
    {
        BattleSession session = Session(expanded: true);
        BattleModelRoster roster = session.ReadExpandedPresentationRoster();
        NativeBattleGraphicsResource ab = new([1, 0, 4, 0, 0xAB, 1, 0, 0xA0, 1, 0xA1, 0xA0, 0, 0xA1]) { AnimationFrames = [15, 9] };
        BindPresentationResources(session, roster, ab);
        NativeBattleGraphicsState graphics = Graphics(roster);
        graphics = graphics with { Models = graphics.Models.SetItem(7, Sequenced(7, 0)), UiServices = NativeBattleUiServices.Initialize() };
        NativeBattleModelLifecycleServices services = session.CaptureExpandedModelLifecycleServices(roster);
        var before = ObserveActors(session);
        MethodInfo captureLimits = typeof(BattleSession).GetMethod("CaptureNativeLimits", PrivateInstance)!;
        string limits = NativeBattleTestSnapshots.Json((BattleNativeLimitState[])captureLimits.Invoke(session, null)!);

        NativeBattleGraphicsState advanced = Advance(session, graphics, services);

        NativeBattleTriggerWidget widget = Assert.IsType<NativeBattleTriggerWidget>(advanced.TriggerWidget);
        Assert.Equal((byte)7, widget.ActorIndex);
        Assert.Equal((byte?)1, widget.SourceSequence);
        Assert.Equal(0, widget.SourceAbOffset);
        Assert.True(widget.Visible);
        Assert.Equal((sbyte)6, advanced.UiServices!.Menu957);
        Assert.Equal(new NativeBattleModelTriggerRegistered(7, "ally-3", widget), Assert.Single(services.ReadReceipts()));
        Assert.Equal(before, ObserveActors(session));
        Assert.Equal(limits, NativeBattleTestSnapshots.Json((BattleNativeLimitState[])captureLimits.Invoke(session, null)!));
        Assert.Null(session.NativeGraphics);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void Countdown_timer_selection_uses_signed_facts_captured_from_the_current_allocation()
    {
        BattleSession session = Session(expanded: true);
        BattleCombatant actor = session.Combatants.Single(actor => actor.Id == "ally-3");
        actor.NativeStatusTimers[12] = -1111;
        actor.NativeStatusTimers[10] = -23;
        NativeBattleModelLifecycleServices services = session.CaptureExpandedModelLifecycleServices(session.ReadExpandedPresentationRoster());
        actor.NativeStatusTimers[12] = 290;
        MethodInfo timers = typeof(BattleSession).GetMethod("ReadModelCountdownTimers", PrivateInstance)!;

        var (doom, petrify) = ((short, short))timers.Invoke(session, [7, services.Facts[7]])!;

        Assert.Equal((short)-1111, doom);
        Assert.Equal((short)-23, petrify);
        var glyphs = NativeBattleStatusCountdowns.Layout50A410(0x82000, 10, 20, 30, 0, doom, petrify);
        Assert.Equal(2, glyphs.Length);
        Assert.All(glyphs, item => Assert.Equal(new byte[] { 0x18, 0 }, item.Glyphs));
    }

    [Theory]
    [InlineData("missing services")]
    [InlineData("missing roster")]
    [InlineData("short bank")]
    [InlineData("wrong late row")]
    [InlineData("wrong late identity")]
    [InlineData("wrong late side")]
    [InlineData("extra hole facts")]
    public void Malformed_hosted_bank_fails_before_any_model_side_effect(string malformed)
    {
        BattleSession session = Session(expanded: true);
        session.Combatants.Single(actor => actor.Id == "ally-4").BindRosterSlot(null, bindNative: false);
        BattleModelRoster roster = session.ReadExpandedPresentationRoster();
        NativeBattleGraphicsState graphics = Graphics(roster);
        graphics = graphics with { Models = graphics.Models.SetItem(0, Sequenced(0, 0)) };
        NativeBattleModelLifecycleServices? services = session.CaptureExpandedModelLifecycleServices(roster);
        switch (malformed)
        {
            case "missing services":
                services = null;
                break;
            case "missing roster":
                graphics = graphics with { PresentationRoster = null };
                break;
            case "short bank":
                graphics = graphics with { Models = graphics.Models.RemoveAt(24) };
                break;
            case "wrong late row":
                graphics = graphics with { Models = graphics.Models.SetItem(24, new(23, 0)) };
                break;
            case "wrong late identity":
                graphics = graphics with { ActorIds = graphics.ActorIds.SetItem(24, "other") };
                break;
            case "wrong late side":
                services = new(services.Facts.SetItem(24, services.Facts[24]! with { IsParty = true }));
                break;
            case "extra hole facts":
                services = new(services.Facts.SetItem(8, new("other", true, 0, 0, 0)));
                break;
        }
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        var actors = ObserveActors(session);

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Advance(session, graphics, services));

        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Same(memory, session.NativeEffectMemory);
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
            UsesExpandedRoster = expanded,
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
            BindPresentationResources(session, session.ReadExpandedPresentationRoster());
        }
        Set(session, nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { ModelColorOverrides = NativeBattleModelColorOverrides.FromColdProcess() });
        Set(session, nameof(BattleSession.NativeStage), NativeBattleStageState.FromColdProcess());
        return session;
    }

    private static void BindPresentationResources(BattleSession session, BattleModelRoster roster, NativeBattleGraphicsResource? extendedPartyResource = null, NativeBattleGraphicsResource? partyResource = null)
    {
        Dictionary<int, NativeBattleEffectModelBinding> presentationBindings = roster.PartyRows.Concat(roster.EnemyRows).ToDictionary(row => row, row =>
        {
            BattleModelOwner actor = roster[row];
            NativeBattleGraphicsResource resource = actor.IsParty ? (row == 7 ? extendedPartyResource : null) ?? partyResource ?? PartySequence : EnemySequence;
            return new NativeBattleEffectModelBinding(actor.ActorId, new(row, 0), resource) { IsParty = actor.IsParty };
        });
        typeof(BattleSession).GetField("presentationNativeTargets", PrivateInstance)!.SetValue(session, presentationBindings);
    }

    private static NativeBattleGraphicsState Graphics(BattleModelRoster roster)
    {
        return new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
    }

    private static NativeBattleGraphicsModel Sequenced(int row, byte modelId)
    {
        return new(row, 2)
        {
            Flags2 = 4,
            Flags8 = 0,
            ModelId4 = modelId,
            ResourceFlags44 = 1,
            Sequence = 1,
            SequenceState = new()
        };
    }

    private static NativeBattleGraphicsModel Removing(int row)
    {
        return new(row, 2)
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
