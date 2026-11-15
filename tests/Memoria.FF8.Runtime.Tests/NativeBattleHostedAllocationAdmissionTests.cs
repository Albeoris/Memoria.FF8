using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedAllocationAdmissionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattlePartyActorModel Ally = new(new("ally"));
    private static readonly BattleEnemyActorModel Old = new(new("enemy"));
    private static readonly BattleEnemyActorModel New = new(new("native-allocation-1"));
    private static readonly NativeBattleModelSlot Slot = NativeBattleModelSlot.Decode(3);

    [Fact]
    public void Prepared_load_keeps_old_alias_until_102_and_rejects_stale_publication()
    {
        BattleSession child = Fixture();
        var load = Admission();
        BattleSession.HostedDispatchControls controls = new([load.Load, new BattleSession.HostedDispatchControl.AllocationStatistics(), new BattleSession.HostedDispatchControl.ActivateOriginal(Slot), new BattleSession.HostedDispatchControl.AllocationGate()], []);
        NativeBattleGraphicsState originalGraphics = child.NativeGraphics!;
        NativeBattleActionPacketsState originalPackets = child.NativeActionPackets;
        Assert.Throws<InvalidDataException>(() => child.PrepareHostedDispatch([], controls: controls));
        Assert.Same(originalGraphics, child.NativeGraphics);
        Assert.Same(originalPackets, child.NativeActionPackets);

        var prepared = child.PrepareHostedDispatch([], controls: controls, allocations: [load]);
        var stale = child.PrepareHostedDispatch([], controls: controls, allocations: [load]);
        Assert.Equal(new ushort[] { 102, 113, 113, 10 }, prepared.Admitted.Descriptors.Queue.Select(slot => prepared.Admitted.Descriptors.Packets[slot].Command));
        Assert.False(prepared.Admitted.ActorModels.Contains(New));
        Assert.Equal(Old, prepared.Packets.ModelAliases.DecodeLiteral(3));
        child.ValidatePreparedHostedDispatch(prepared);
        child.CommitHostedDispatch(prepared);
        Assert.Throws<InvalidOperationException>(() => child.ValidatePreparedHostedDispatch(stale));
        int slot = child.NativeGraphics!.Descriptors.Queue[0];
        var plan = Assert.IsType<NativeBattleDescriptorModelSubject.PlannedLoad>(child.NativeGraphics.Descriptors.Packets[slot].ModelSubject);
        var (reboundReady, rebound) = TryRebind(child, child.NativeGraphics!, plan, slot);
        Assert.True(reboundReady);
        Assert.False(rebound.ActorModels.Contains(Old));
        Assert.True(rebound.ActorModels.Contains(New));
        NativeBattleModelLifecycleServices frame = new(ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts>.Empty
            .Add(Ally, new("ally", true, 1, 0, 0))
            .Add(Old, new("enemy", false, 0, 0, 0))
            .Add(New, new("native-allocation-1", false, 0, 0, 0)));
        frame.Validate(prepared.Packets.Roster, prepared.Admitted);
        frame.ValidateCurrentModels(child.HostedPlannedModelRoster, rebound);
        Assert.True(frame.Facts.ContainsKey(Old));
        Assert.Throws<InvalidDataException>(() => frame.Validate(child.HostedPlannedModelRoster, rebound));
        Assert.Throws<InvalidDataException>(() => frame.ValidateCurrentModels(child.HostedPlannedModelRoster, prepared.Admitted));
        BattleEnemyActorModel future = new(new("native-allocation-2"));
        Assert.Throws<InvalidDataException>(() => frame.ValidateCurrentModels(new([Ally, New, future]), rebound));
        Assert.Equal(New, child.NativeActionPackets.ModelAliases.DecodeLiteral(3));
        Assert.Equal(Ally, child.NativeActionPackets.ModelAliases.DecodeLiteral(0));
    }

    [Fact]
    public void Recompute_prepares_new_generation_after_existing_104_and_10_without_touching_either()
    {
        BattleSession child = Fixture();
        NativeBattleActionHeaderPreparation source = new(Ally, 1, 0, -1, 0, 0, null);
        NativeBattleContactPacket contact = new(new(Old, 0, 1, 0, 0, 0, 0), null);
        var initial = child.PrepareHostedDispatch([new(source, [contact])], dispatchAppended: true, completion: new(0x47DE70, Ally, "ally"));
        long token = child.PublishHostedAction(initial);
        NativeBattleDescriptorState existing = child.NativeGraphics!.Descriptors;
        var load = Admission();
        BattleSession.HostedDispatchHeader nested = new(new(New, 2, 0, -1, 13, 107, null), [new(new(Ally, 0, 1, 0, 0, 3, 0), null)]);

        Assert.Throws<InvalidDataException>(() => child.PrepareHostedContactReplacement(token, [contact], append: [nested]));
        var next = child.PrepareHostedContactReplacement(token, [contact], append: [nested], allocations: [load]);
        Assert.Same(existing, child.NativeGraphics.Descriptors);
        Assert.Equal(Old, child.NativeActionPackets.ModelAliases.DecodeLiteral(3));
        child.PublishHostedContactReplacement(next);
        Assert.Same(existing, child.NativeGraphics.Descriptors);
        Assert.Equal(new ushort[] { 104, 10 }, existing.Queue.Select(slot => existing.Packets[slot].Command));
        Assert.Equal(New, child.NativeActionPackets.ReadHeader(Assert.Single(next.AppendedHeaders)).Source);
        Assert.False(child.NativeGraphics.ActorModels.Contains(New));
        Assert.Equal(Old, child.NativeActionPackets.ModelAliases.DecodeLiteral(3));
        Assert.Throws<InvalidOperationException>(() => child.ValidatePreparedHostedContactReplacement(next));
    }

    [Fact]
    public void Rebind_waits_for_reached_model_work_but_rejects_a_later_reference_to_the_retiring_owner()
    {
        BattleSession child = Fixture();
        var load = Admission();
        var controls = new BattleSession.HostedDispatchControls([load.Load], []);
        var prepared = child.PrepareHostedDispatch([], controls: controls, allocations: [load]);
        child.CommitHostedDispatch(prepared);
        NativeBattleGraphicsState graphics = child.NativeGraphics!;
        int slot = graphics.Descriptors.Queue[0];
        var plan = Assert.IsType<NativeBattleDescriptorModelSubject.PlannedLoad>(graphics.Descriptors.Packets[slot].ModelSubject);
        NativeBattleAuxiliaryState aux = graphics.Auxiliary.AppendModelRing(NativeBattleAuxiliaryKind.ModelStatus50A000, Old, child.NativeActionPackets.ModelAliases).State;
        var (ready, waiting) = TryRebind(child, graphics with { Auxiliary = aux }, plan, slot);
        Assert.False(ready);
        Assert.True(waiting.ActorModels.Contains(Old));
        Assert.Equal(Old, child.NativeActionPackets.ModelAliases.DecodeLiteral(3));

        List<BattleModelOwner> visited = [];
        NativeBattleAuxiliaryState drained = aux.Advance(new NativeBattleAuxiliaryInputs { ApplyModelRing = (_, model, _) => visited.Add(model) }).State;
        Assert.Equal(Old, Assert.Single(visited));
        Assert.Null(drained.Head);
        var (rebindReady, rebound) = TryRebind(child, graphics with { Auxiliary = drained }, plan, slot);
        Assert.True(rebindReady);
        Assert.True(rebound.ActorModels.Contains(New));

        BattleSession future = Fixture();
        var conflicting = load with { LaterOwners = ImmutableHashSet.Create<BattleModelOwner>(Old) };
        var blocked = future.PrepareHostedDispatch([], controls: controls, allocations: [conflicting]);
        future.CommitHostedDispatch(blocked);
        InvalidDataException rejected = Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => TryRebind(future, blocked.Admitted, plan, slot)).InnerException);
        Assert.Contains("later captured work", rejected.Message);
        Assert.Equal(Old, future.NativeActionPackets.ModelAliases.DecodeLiteral(3));
    }

    [Fact]
    public void Authored_slot_activation_clears_current_generation_and_typed_activation_keeps_its_subject()
    {
        NativeBattleAiDefinition idle = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        BattleSession logical = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            UsesOriginalEnemySlots = true,
            NativeClockRules = new(1),
            NativeBattleSpeed = 0,
            Combatants =
            [
                new() { Id = "ally", Name = "Ally", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, NativeAi = idle, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) }
            ]
        }, BattleAtbMode.Normal);
        BattleCombatant prototype = logical.Combatants.Single(actor => actor.Id == "enemy");
        MethodInfo allocate = typeof(BattleSession).GetMethod("AllocateNativeEnemy", Private)!;
        typeof(BattleSession).GetField("producingExpandedAiCallback", Private)!.SetValue(logical, true);
        BattleCombatant prior = Assert.IsType<BattleCombatant>(allocate.Invoke(logical, [prototype, 0, 3, true]));
        BattleCombatant current = Assert.IsType<BattleCombatant>(allocate.Invoke(logical, [prototype, 0, 3, true]));
        Assert.Null(prior.Slot);
        Assert.Equal(BattleRosterSlot.Enemy(0), current.Slot);
        Assert.NotEqual(prior.Id, current.Id);
        prior.NativeCombatantFlags = 0xC2;
        current.NativeCombatantFlags = 0xC3;
        BattleEnemyActorModel priorOwner = new(new(prior.Id));
        NativeBattleModelLifecycleServices services = new(ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts>.Empty.Add(priorOwner, new(prior.Id, false, 0, 0, 0)));
        var activation = new NativeBattleModelParticipantActivated(priorOwner, prior.Id);
        MethodInfo acknowledge = typeof(BattleSession).GetMethod("AcknowledgeHostedNativeActivation", Private)!;
        int initialQueued = logical.QueuedActions.Count;

        acknowledge.Invoke(logical, [services, activation with { OriginalSlot = Slot }]);
        Assert.Equal(0xC2u, prior.NativeCombatantFlags);
        Assert.Equal(1u, current.NativeCombatantFlags);
        BattleAttackCommand initialization = Assert.Single(logical.QueuedActions.Skip(initialQueued));
        Assert.Equal(current.Id, initialization.ExpandedAiCallback?.Actor.ActorId);
        Assert.Equal((byte)0, initialization.ExpandedAiCallback?.Stage);

        current.NativeCombatantFlags = 0xC3;
        acknowledge.Invoke(logical, [services, activation]);
        Assert.Equal(0u, prior.NativeCombatantFlags);
        Assert.Equal(0xC3u, current.NativeCombatantFlags);
        Assert.Single(logical.QueuedActions.Skip(initialQueued));
    }

    private static (bool Ready, NativeBattleGraphicsState Graphics) TryRebind(BattleSession child, NativeBattleGraphicsState graphics, NativeBattleDescriptorModelSubject.PlannedLoad plan, int slot)
    {
        object?[] arguments = [graphics, plan, slot, null];
        object? ready = typeof(BattleSession).GetMethod("TryRebindHostedAllocationModel", Private)!.Invoke(child, arguments);
        return (Assert.IsType<bool>(ready), Assert.IsType<NativeBattleGraphicsState>(arguments[3]));
    }

    private static BattleSession.HostedModelLoadAdmission Admission()
    {
        return new(new(New, Slot, 0, 0), new("native-allocation-1", 16, new([0, 0])) { InitialPosition = new(0, 0, 0) }, ImmutableHashSet<BattleModelOwner>.Empty);
    }

    private static BattleSession Fixture()
    {
        BattleSession child = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        BattleModelRoster roster = new([Ally, Old]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Ally), (Slot, Old)]);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner == Ally ? (byte)1 : (byte)16 });
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(child, graphics with { EntryReady = true });
        typeof(BattleSession).GetField("hostedBattlePresentation", Private)!.SetValue(child, true);
        typeof(BattleSession).GetField("nativeActionPackets", Private)!.SetValue(child, NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases));
        typeof(BattleSession).GetField("hostedModelRegistry", Private)!.SetValue(child, new BattleSession.HostedModelRegistry(roster, ImmutableDictionary<BattleEnemyActorModel, BattleSession.HostedModelLoadAdmission>.Empty));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(child, NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster, aliases));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelTextureCache))!.SetValue(child, NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(roster));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Textures = NativeBattleEffectTextureState.FromColdProcess() with { PresentationTextures = ImmutableDictionary.Create<string, NativeBattlePresentationTextureBank>(StringComparer.Ordinal) },
            PrimitiveProcess = new(null, null, null, 0, null) { TextureWindowCache = NativeBattlePcTextureWindowCache.Reset4647A0(null) }
        };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(child, memory);
        NativeBattleHostedTestFixture.InstallModelBindings(child, new NativeBattleGraphicsBinding("ally", 1, new([0, 0])), new NativeBattleGraphicsBinding("enemy", 16, new([0, 0])));
        return child;
    }
}
