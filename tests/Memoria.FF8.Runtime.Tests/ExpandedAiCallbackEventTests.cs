using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedAiCallbackEventTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Enemy = "enemy";

    [Fact]
    public void Queued_init_commits_hits_before_the_next_predicate_and_keeps_one_event_owner()
    {
        byte[] init = [4, 200, 7, 100, 6, 2, 0, 200, 1, 0x10, 0x27, 3, 0, 15, 96, 77, 7, 101, 6, 0];
        var (logical, process) = Fixture(Program(init: init), actions: [FixedHit(100), FixedHit(101)]);
        BattleCombatant actor = Actor(logical, Enemy);
        BattleAttackCommand initRequest = Assert.Single(logical.QueuedActions);
        Assert.Equal(new BattleExpandedAiCallbackRequest(new BattleEnemyActorReference(Enemy), 0), initRequest.ExpandedAiCallback);
        Assert.Equal(10000, actor.CurrentHp);
        Assert.Equal(0u, logical.Capture().NativeAiVariables![0]);
        Assert.Empty(process.Graphics.Descriptors.Queue);

        Dispatch(logical);

        Assert.Equal(9998, actor.CurrentHp);
        Assert.Equal(77u, logical.Capture().NativeAiVariables![0]);
        Assert.Empty(logical.QueuedActions);
        Assert.Equal(initRequest.ExpandedAiCallback, logical.ActiveAction!.ExpandedAiCallback);
        Assert.Equal(new ushort[] { 100, 101 }, process.ActionPackets.PublishedHeaders().Select(header => header.Slot.Action));
        Assert.Equal(2, process.ActionPackets.ContactCount);
        Assert.Equal((ushort)104, process.Graphics.Descriptors.Packets[Assert.Single(process.Graphics.Descriptors.Queue)].Command);
        Assert.NotNull(process.ActiveActionToken);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(logical.Capture()));
    }

    [Fact]
    public void Empty_init_and_turn_own_real_completion_and_stage_three_observes_cleared_ko_only_inline()
    {
        byte[] death = [2, 4, 200, 3, 0, 0, 3, 0, 15, 96, 42, 0];
        var (logical, process) = Fixture(Program(death: death));
        BattleCombatant actor = Actor(logical, Enemy);
        Dispatch(logical);
        CompleteEmpty(logical, process, 0x47DDA0);
        Assert.Equal((byte)0, actor.AiTurnCount);
        Queue(logical, actor, 1);
        Dispatch(logical);
        Assert.Equal((byte)1, actor.AiTurnCount);
        CompleteEmpty(logical, process, 0x47DE70);

        actor.CurrentHp = 0;
        actor.NativeStatus0 |= 1;
        Queue(logical, actor, 3);
        Assert.Equal(1, actor.NativeStatus0 & 1);
        Assert.Equal(0u, logical.Capture().NativeAiVariables![0]);
        Dispatch(logical);
        Assert.Equal(42u, logical.Capture().NativeAiVariables![0]);
        Assert.Equal(1, actor.NativeStatus0 & 1);
        Assert.True(actor.IsBusy);
        CompleteEmpty(logical, process, 0x47DDA0);
        Assert.False(actor.IsBusy);
        Assert.Equal(3, logical.Events.Count(entry => entry.Kind == "ActionComplete"));
    }

    [Fact]
    public void Immediate_counter_follows_the_parent_header_and_keeps_the_whole_turn_reaction_order()
    {
        byte[] turn = [4, 38, 7, 100, 6, 4, 39, 7, 101, 6, 0];
        byte[] immediate = [4, 0, 7, 102, 6, 0];
        BattleCombatantDefinition first = Combatant("first", 22, Program(immediate: immediate));
        BattleCombatantDefinition second = Combatant("second", 23, Program());
        var (logical, process) = Fixture(Program(turn: turn), [first, second], [FixedHit(100), FixedHit(101), FixedHit(102)]);
        while (logical.QueuedActions.Count > 0)
        {
            Dispatch(logical);
            CompleteEmpty(logical, process, 0x47DDA0);
        }
        int allyHp = Actor(logical, "ally").CurrentHp;
        Queue(logical, Actor(logical, Enemy), 1);

        Dispatch(logical);

        Assert.Equal(new ushort[] { 100, 102, 101 }, process.ActionPackets.PublishedHeaders().Select(header => header.Slot.Action));
        Assert.Equal((byte)2, logical.Capture().NativeReactionOrder);
        Assert.Equal((byte)0, Actor(logical, "first").NativePendingReaction.Order);
        Assert.Equal((byte)1, Actor(logical, "second").NativePendingReaction.Order);
        Assert.Equal(9999, Actor(logical, "first").CurrentHp);
        Assert.Equal(9999, Actor(logical, "second").CurrentHp);
        Assert.Equal(allyHp - 1, Actor(logical, "ally").CurrentHp);
        Assert.Empty(logical.QueuedActions);
        Assert.Equal((byte)1, logical.ActiveAction!.ExpandedAiCallback!.Stage);
    }

    [Fact]
    public void A_second_command_without_its_original_target_does_not_cancel_the_callback()
    {
        byte[] init = [4, 38, 7, 100, 6, 7, 101, 6, 0];
        BattleCombatantDefinition target = Combatant("first", 22, Program()) with { MaximumHp = 1, CurrentHp = 1 };
        var (logical, process) = Fixture(Program(init: init), [target], [FixedHit(100), FixedHit(101)]);
        BattleExpandedAiCallbackRequest? request = logical.QueuedActions.First().ExpandedAiCallback;

        Dispatch(logical);

        Assert.Equal(0, Actor(logical, "first").CurrentHp);
        Assert.Equal((ushort)100, Assert.Single(process.ActionPackets.PublishedHeaders()).Slot.Action);
        Assert.Equal(request, logical.ActiveAction!.ExpandedAiCallback);
        Assert.True(Actor(logical, Enemy).IsBusy);
        Assert.NotNull(process.ActiveActionToken);
        NativeBattleDescriptorState descriptors = process.Graphics.Descriptors;
        Assert.Collection(descriptors.Queue.Select(slot => descriptors.Packets[slot]),
            action => Assert.Equal((ushort)104, action.Command),
            completion =>
            {
                Assert.Equal((ushort)10, completion.Command);
                Assert.Equal(0x47DDA0u, completion.Argument);
            });
    }

    [Fact]
    public void Routed_counter_produces_after_its_source_and_consumes_the_selector_once()
    {
        var routed = Run(201);
        var exact = Run(0);

        Assert.Equal(new ushort[] { 100, 102 }, routed.Process.ActionPackets.PublishedHeaders().Select(header => header.Slot.Action));
        Assert.Equal(10000, Actor(routed.Logical, "first").CurrentHp);
        Assert.Equal(9999, Actor(routed.Logical, "second").CurrentHp);
        Assert.Equal(Actor(exact.Logical, "ally").CurrentHp, Actor(routed.Logical, "ally").CurrentHp);
        NativeBattleRandomState exactRandom = Assert.IsType<NativeBattleRandomState>(exact.Logical.Capture().Random);
        NativeBattleRandomState routedRandom = Assert.IsType<NativeBattleRandomState>(routed.Logical.Capture().Random);
        Assert.Equal(exactRandom.Draw(out _).Indexes.ToArray(), routedRandom.Indexes.ToArray());

        static (BattleSession Logical, BattlePresentationProcess Process) Run(byte targetSelector)
        {
            byte[] turn = [4, 38, 7, 100, 6, 0];
            byte[] immediate = [4, targetSelector, 7, 102, 6, 0];
            BattleCombatantDefinition first = Combatant("first", 22, Program());
            BattleCombatantDefinition second = Combatant("second", 23, Program(immediate: immediate));
            EncounterCompiledDefinition mod = new("fixture.encounter", "Classic", "fixture-generation", "first", 22,
                ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, EncounterCompiledTemplate>.Empty, ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty,
                ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty.Add("share", new("share", "original", ["second"])), ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty, ImmutableDictionary<string, double>.Empty);
            var (logical, process) = Fixture(Program(turn: turn), [first, second], [FixedHit(100), FixedHit(102)], mod);
            while (logical.QueuedActions.Count > 0)
            {
                Dispatch(logical);
                CompleteEmpty(logical, process, 0x47DDA0);
            }
            Queue(logical, Actor(logical, Enemy), 1);
            Dispatch(logical);
            return (logical, process);
        }
    }

    [Fact]
    public void Direct_callback_text_uses_its_reached_descriptor_before_completion()
    {
        NativeBattleAiDefinition program = Program(init: [1, 0, 0]) with { Texts = ["original callback text"] };
        var (logical, process) = Fixture(program);
        Dispatch(logical);
        Assert.DoesNotContain(logical.Events, item => item.Kind == "NativeText");
        NativeBattleGraphicsState graphics = process.Graphics;
        Assert.Equal(new ushort[] { 8, 10 }, graphics.Descriptors.Queue.Select(slot => graphics.Descriptors.Packets[slot].Command));
        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(graphics.ActorModels.Roster);

        Field(process, "hostedFrameServices").SetValue(process, services);
        try
        {
            SetGraphics(process, (NativeBattleGraphicsState)Invoke(process, "AdvanceNativeDescriptors", graphics)!);
        }
        finally
        {
            Field(process, "hostedFrameServices").SetValue(process, null);
        }

        NativeBattlePresentationCompletionRequested completion = Assert.IsType<NativeBattlePresentationCompletionRequested>(services.ReadOrderedReceipts()[1]);
        Assert.IsType<NativeBattlePresentationDescriptorText>(services.ReadOrderedReceipts()[0]);
        Invoke(logical, "ApplyExpandedHostedFrameReceipts", services, null);
        Assert.Equal("original callback text", Assert.Single(logical.Events, item => item.Kind == "NativeText").Message);
        Assert.True(process.IsCompletionAcknowledged(completion.Token));
        Assert.Null(logical.ActiveAction);
    }

    [Fact]
    public void Hosted_text_receipts_keep_plain_and_composed_messages_in_order_and_omit_zero_duration()
    {
        var (logical, process) = Fixture(Program());
        NativeBattleGraphicsState graphics = process.Graphics;
        BattleEvent plain = new(0, "NativeText", null, null, 8, "plain") { NativeMessageMode = 3 };
        BattleEvent composed = new(0, "NativeText", Enemy, null, 16, "Kweh is away.") { NativeMessageMode = 3 };
        var controls = graphics.Descriptors.EnqueueMessage(plain, 128, 86).State
            .EnqueueMessage(plain with { Amount = 256, Message = "invisible" }, 128, 86).State
            .EnqueueMessage(composed, 128, 86).State;
        graphics = graphics with { Descriptors = controls };
        SetGraphics(process, graphics);
        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(graphics.ActorModels.Roster);

        Field(process, "hostedFrameServices").SetValue(process, services);
        try
        {
            SetGraphics(process, (NativeBattleGraphicsState)Invoke(process, "AdvanceNativeDescriptors", graphics)!);
        }
        finally
        {
            Field(process, "hostedFrameServices").SetValue(process, null);
        }

        NativeBattlePresentationDescriptorText[] texts = services.ReadOrderedReceipts().OfType<NativeBattlePresentationDescriptorText>().ToArray();
        Assert.Equal(new[] { plain.Message, composed.Message }, texts.Select(text => text.Message.Message));
        Assert.Equal(new[] { 8, 16 }, texts.Select(text => text.Message.Amount));
        Assert.All(texts, text => Assert.Null(text.Message.NativeTextIdentity));
        Assert.Empty(process.Graphics.Descriptors.Queue);
        Invoke(logical, "ApplyExpandedHostedFrameReceipts", services, null);
        Assert.Equal(new[] { plain.Message, composed.Message }, logical.Events.Where(item => item.Kind == "NativeText").Select(item => item.Message));
    }

    [Fact]
    public void Retired_shot_widget_accepts_only_its_cancelled_callback_without_reopening()
    {
        const long token = 1;
        const uint callback = 0x4AD7C0;
        var (logical, process) = Fixture(Program());
        BattleCombatant actor = Actor(logical, "ally");
        BattleModelOwner owner = logical.ReadPresentationModelOwner(actor.Id);
        BattleModelOwner recipient = logical.ReadPresentationModelOwner(Enemy);
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference(Enemy)]), BattleTargetEligibility.Living, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        BattleActionPresentation command = new(actor.Id, Enemy, BattleActionPhase.Recovery, target, BattleAbility.LimitBreak)
        {
            NativeQueuedCommand = 237,
            NativeAction = FixedHit(100) with { Command = 237, ActionId = 0 },
            NativeLimitCallback = NativeLimitCallback.ShotEffect
        };
        Field(logical, "preparingExpandedHostedAction").SetValue(logical, true);
        NativeBattleContactPacket contact = new(new(recipient, 0, 0, 0, 0, 0, 0), null);
        Invoke(logical, "StageExpandedHostedInvocation", command, new NativeBattleActionHeaderPreparation(owner, 237, 0, -1, 0, 188, null), ImmutableArray.Create(new ExpandedBattleContact(Enemy, contact)), null);
        SetHostedOperationIndex(logical, BindingFlags.Instance | BindingFlags.NonPublic, 0);
        Field(logical, "expandedHostedActionToken").SetValue(logical, token);
        FieldInfo eventField = Field(logical, "expandedHostedEvent");
        eventField.SetValue(logical, Activator.CreateInstance(eventField.FieldType, [command, command, 0x47DE70u, null, false, owner]));
        typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(logical, command);
        var shots = Assert.IsType<Dictionary<string, BattleNativeShotState>>(Field(logical, "nativeShotProcesses").GetValue(logical));
        shots.Add(actor.Id, new(0, target, 20, 20, 1, BattleNativeShotPhase.AwaitingEffect));
        var stock = Assert.IsType<NativeItemStock[]>(Field(logical, "nativeItems").GetValue(logical));
        stock[0] = new(101, 1);
        actor.IsBusy = true;
        actor.CurrentHp = 0;
        actor.NativeStatus0 = 1;
        Invoke(logical, "ReconcileExpandedShot");
        Assert.Empty(shots);

        NativeBattleGraphicsState graphics = process.Graphics;
        BattleModelRoster roster = graphics.ActorModels.Roster;
        HostedShotFrameOwner cancelled = Assert.IsType<HostedShotFrameOwner>(Invoke(logical, "CaptureExpandedHostedShotOwner", roster));
        Assert.Equal(new HostedShotFrameOwner(owner, actor.Id, token, callback, null), cancelled);
        NativeBattleModelLifecycleServices services = new(logical.CaptureModelLifecycleServices(roster).Facts) { Shot = cancelled };
        graphics = graphics with { Descriptors = graphics.Descriptors.Enqueue(10, 128, callback).State };
        SetGraphics(process, graphics);
        Field(process, "hostedFrameServices").SetValue(process, services);
        try
        {
            SetGraphics(process, (NativeBattleGraphicsState)Invoke(process, "AdvanceNativeDescriptors", graphics)!);
        }
        finally
        {
            Field(process, "hostedFrameServices").SetValue(process, null);
        }
        NativeBattleShotReadinessReached receipt = Assert.IsType<NativeBattleShotReadinessReached>(Assert.Single(services.ReadOrderedReceipts()));
        Assert.Empty(process.Graphics.Descriptors.Queue);
        BattleSnapshot before = logical.Capture();
        int events = logical.Events.Count;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Invoke(logical, "ApplyExpandedHostedShotReadiness", services, receipt, token + 1)).InnerException);
        Invoke(logical, "ApplyExpandedHostedShotReadiness", services, receipt, token);
        Assert.Empty(shots);
        Assert.Equal(new NativeItemStock(101, 1), stock[0]);
        Assert.Equal(before.Random, logical.Capture().Random);
        Assert.Equal(events, logical.Events.Count);
        Assert.Same(command, logical.ActiveAction);
    }

    [Fact]
    public void Shot_close_with_no_saved_status_targets_releases_the_widget_without_publishing_a_header()
    {
        var (logical, process) = Fixture(Program());
        BattleCombatant actor = Actor(logical, "ally");
        BattleCombatant enemy = Actor(logical, Enemy);
        enemy.CurrentHp = 0;
        enemy.NativeStatus0 = 1;
        actor.IsBusy = true;
        BattleTargetRequest selected = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference(Enemy)]), BattleTargetEligibility.Living, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        BattleTargetRequest ending = new(new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        var shots = Assert.IsType<Dictionary<string, BattleNativeShotState>>(Field(logical, "nativeShotProcesses").GetValue(logical));
        shots.Add(actor.Id, new(0, selected, 20, -1, 1, BattleNativeShotPhase.Ending));
        var stock = Assert.IsType<NativeItemStock[]>(Field(logical, "nativeItems").GetValue(logical));
        stock[0] = new(101, 1);
        BattleAttackCommand close = new(actor.Id, enemy.Id, ending, BattleAbility.LimitBreak, 238 << 16)
        {
            NativeAction = FixedHit(100) with { Command = 238, ActionId = 0 },
            NativeQueuedCommand = 238,
            NativeLimitCallback = NativeLimitCallback.ShotEnding
        };
        NativeBattleActionPacketsState packets = process.ActionPackets;
        NativeBattleDescriptorState descriptors = process.Graphics.Descriptors;
        int start = logical.Events.Count;
        Invoke(logical, "StartExpandedAction", actor, close);

        Assert.Empty(shots);
        Assert.False(actor.IsBusy);
        Assert.Null(logical.ActiveAction);
        Assert.Null(process.ActiveActionToken);
        Assert.Same(packets, process.ActionPackets);
        Assert.Same(descriptors, process.Graphics.Descriptors);
        Assert.Equal(new NativeItemStock(101, 1), stock[0]);
        Assert.Single(logical.Events.Skip(start), entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_later_command_or_lifecycle_rejection_leaves_the_live_callback_and_formula_state_unchanged(bool lifecycle)
    {
        byte[] second = lifecycle ? [59, 1, 4] : [7, 101, 6];
        byte[] init = [4, 200, 7, 100, 6, 2, 2, 1, 0, 0, 0, 3, 0, 15, 96, 77, .. second, 0];
        var (logical, process) = Fixture(Program(init: init), actions: [FixedHit(100)]);
        BattleSnapshot before = logical.Capture();
        BattleAttackCommand head = Assert.Single(logical.QueuedActions);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleActionPacketsState packets = process.ActionPackets;

        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => Dispatch(logical));

        Assert.IsType<NotSupportedException>(failure.InnerException);
        Assert.Same(head, Assert.Single(logical.QueuedActions));
        Assert.Same(graphics, process.Graphics);
        Assert.Same(packets, process.ActionPackets);
        Assert.Null(process.ActiveActionToken);
        Assert.Null(logical.ActiveAction);
        Assert.Equal(10000, Actor(logical, Enemy).CurrentHp);
        Assert.Equal(before.Random, logical.Capture().Random);
        Assert.Equal(before.NativeReactionOrder, logical.Capture().NativeReactionOrder);
        Assert.Equal(0u, logical.Capture().NativeAiVariables![0]);
    }

    private static NativeBattleAiDefinition Program(byte[]? init = null, byte[]? turn = null, byte[]? death = null, byte[]? immediate = null)
    {
        return new([init ?? [0], turn ?? [0], [0], death ?? [0], immediate ?? [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }

    private static BattleCombatantDefinition Combatant(string id, int model, NativeBattleAiDefinition program)
    {
        return new() { Id = id, Name = id, ModelId = model, MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeAi = program, NativeCombatantFlags = 0x11, NativeStatusResistances = new byte[40] };
    }

    private static BattleNativeAction FixedHit(ushort action)
    {
        return new(8, action, 3, action)
        {
            Name = "Fixed hit", MagicId = 0, AttackType = 35, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 0,
            DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [.. new byte[20]], SupplementalRecord = []
        };
    }

    private static (BattleSession Logical, BattlePresentationProcess Process) Fixture(NativeBattleAiDefinition program, BattleCombatantDefinition[]? enemies = null, BattleNativeAction[]? actions = null, EncounterCompiledDefinition? encounter = null)
    {
        BattleDefinition definition = new()
        {
            EncounterMod = encounter,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeFieldBattleRules = 0,
            NativeGlobalRandom = new(123),
            NativeActions = actions ?? [],
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, Combatant(Enemy, 21, program), .. (enemies ?? [])]
        };
        BattleSession logical = new(definition, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        var bindings = logical.Combatants.ToImmutableDictionary(actor => logical.ReadPresentationModelOwner(actor.Id), actor =>
            new NativeBattleGraphicsBinding(actor.Id, checked((byte)(actor.ModelId + (actor.IsPlayer ? 0 : 16))), new([0, 0])));
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        NativeBattleGraphicsDefinition resources = new(rules, roster.Owners.Select(owner => bindings[owner]).ToImmutableArray(), []);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => new(0) { ModelId4 = bindings[owner].ModelId });
        NativeBattleModelCpuResources cpu = (NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 0, EnemyLinkedSlots = 0, ScratchMaximum = 0 }).WithPresentationRoster(roster, aliases);
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(roster);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelDatSource modelData = NativeBattleEnemyOwnedHeapTests.Source(80);
        int textureOffset = modelData.Section(10);
        NativeBattleEffectHeapAllocation material = new("model.dat", modelData.Bytes.Slice(textureOffset, modelData.Bytes.Length - textureOffset));
        foreach (BattleEnemyModelOwner owner in roster.Enemies)
        {
            NativeBattleGraphicsBinding binding = bindings[owner];
            NativeBattleModelTextureCacheLoad loaded = cache.LoadEnemy(binding.ModelId, material, owner);
            cache = loaded.Cache;
            int row = loaded.ResourceIndex;
            cpu = cpu with { Rows = cpu.Rows.SetItem(row, cpu.Rows[row] with { LinkedPhysicalIndex36 = 255 }) };
            (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, modelData with { ModelId = binding.ModelId }, row, null, false);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, graphics.ActorModels.Get(owner) with { ActiveResourceRow100 = row, OriginalResourceRow104 = row }) };
        }
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(logical, graphics with { EntryReady = true, EntryLifecycle = 4 }, resources);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeModelCpuResources), cpu);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeModelTextureCache), cache);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeFileStaging), staging);
        Field(logical, "expandedPresentationProcess").SetValue(logical, process);
        return (logical, process);
    }

    private static void CompleteEmpty(BattleSession logical, BattlePresentationProcess process, uint callback)
    {
        Assert.Null(process.ActiveActionToken);
        var graphics = process.Graphics;
        NativeBattleDescriptorPacket packet = graphics.Descriptors.Packets[Assert.Single(graphics.Descriptors.Queue)];
        Assert.Equal((ushort)10, packet.Command);
        Assert.Equal(callback, packet.Argument);
        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(graphics.ActorModels.Roster);
        Field(process, "hostedFrameServices").SetValue(process, services);
        try
        {
            SetGraphics(process, (NativeBattleGraphicsState)Invoke(process, "AdvanceNativeDescriptors", graphics)!);
        }
        finally
        {
            Field(process, "hostedFrameServices").SetValue(process, null);
        }
        NativeBattlePresentationCompletionRequested receipt = Assert.IsType<NativeBattlePresentationCompletionRequested>(Assert.Single(services.ReadOrderedReceipts()));
        Assert.Same(receipt, process.PendingCompletion);
        Invoke(logical, "ApplyExpandedHostedFrameReceipts", services, null);
        Assert.True(process.IsCompletionAcknowledged(receipt.Token));
        Assert.Null(logical.ActiveAction);
    }

    private static void Queue(BattleSession session, BattleCombatant actor, int stage) => Invoke(session, "QueueExpandedAiCallback", actor, stage);

    private static void Dispatch(BattleSession session) => Invoke(session, "DispatchAction");

    private static BattleCombatant Actor(BattleSession session, string id) => session.Combatants.Single(actor => actor.Id == id);

    private static object? Invoke(object owner, string method, params object?[] args) => owner.GetType().GetMethod(method, PrivateInstance)!.Invoke(owner, args);

    private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, PrivateInstance)!;

    private static void SetGraphics(BattlePresentationProcess process, NativeBattleGraphicsState graphics) => typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(process, graphics);

    private static void SetHostedOperationIndex(BattleSession battle, BindingFlags flags, int index)
    {
        FieldInfo field = typeof(BattleSession).GetField("expandedOperationCursor", flags)!;
        object cursor = field.GetValue(battle)!;
        cursor.GetType().GetProperty("Current")!.SetValue(cursor, index);
        field.SetValue(battle, cursor);
    }

}
