using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExpandedModelLifecycleTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void IdleDispatcherUsesTheAttachedRosterWithoutAnInvocationContactRoster()
    {
        BattleSession session = Fixture();
        NativeBattleGraphicsResource resource = new([0, 0])
        {
            AnimationFrames = [1],
            ModelLoadSources = [new(0, false, 0, 1, "body", [])]
        };
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        ImmutableArray<NativeBattleGraphicsBinding> bindings = session.Combatants.Select(actor => new NativeBattleGraphicsBinding(actor.Id, actor.IsPlayer ? (byte)0 : (byte)16, resource) { InitialPosition = new(0, 0, 0) }).ToImmutableArray();
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess();
        BattleDefinition resources = new()
        {
            StageId = 0,
            NativeStageSource = new(0, [], [], new("stage", [])) { PhysicalSource = new(1, 0, []) },
            NativeStageCameraBank = new([]),
            NativeEntryWaveFile = new(0, "entry", []),
            NativeMusicProcess = new(new(1, null, null, null, 0, null), 0, null),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeGlobalRandom = new(123),
            NativeRetainedMemory = retained with { EffectMemory = NativeBattleEffectMemory.FromColdProcess() with { Textures = NativeBattleEffectTextureState.FromColdProcess(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() } }
        };
        session.AttachPresentation(new(resources, NativeBattleHostedTestFixture.PrimitiveExecutionGraphics(new(rules, bindings, []))));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(session.Presentation);
        Assert.Null(process.ActiveActionToken);
        Assert.Null(Field("expandedInvocationProgress").GetValue(session));
        BattleCombatant party = Actor(session, "ally-10");
        BattleCombatant enemy = Actor(session, "enemy-13");
        party.NativeCombatantFlags = 0x11;
        enemy.NativeCombatantFlags = 0x11;
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(session.ReadBattlePresentationRoster());
        services.Emit(new NativeBattleModelParticipantReleased(session.ReadPresentationModelOwner(party.Id), party.Id));
        services.Emit(new NativeBattleModelParticipantSuspended(session.ReadPresentationModelOwner(enemy.Id), enemy.Id, 5));

        typeof(BattleSession).GetMethod("ApplyExpandedHostedFrameReceipts", PrivateInstance)!.Invoke(session, [services, null]);

        Assert.Equal(0x50u, party.NativeCombatantFlags);
        Assert.Equal(0x51u, enemy.NativeCombatantFlags);
        Assert.Equal(1, party.NativeStatus0 & 1);
        Assert.Equal(1, enemy.NativeStatus0 & 1);
    }

    [Theory]
    [InlineData("ally-10", true, 0x50u)]
    [InlineData("enemy-13", false, 0x51u)]
    public void ParentAppliesOnlyTheNativeLifecycleFlagsForExtendedOwners(string identity, bool release, uint flags)
    {
        BattleSession session = Fixture();
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        BattleCombatant actor = Actor(session, identity);
        actor.NativeCombatantFlags = 0x11;
        actor.NativeStatus0 = 8;
        var before = (actor.CurrentHp, actor.NativeStatus1, actor.IsBusy, actor.Atb, session.NativeGlobalRandomChange);

        if (release)
        {
            Invoke(session, "AcknowledgeExpandedModelRelease", roster, new NativeBattleModelParticipantReleased(session.ReadPresentationModelOwner(identity), identity));
        }
        else
        {
            Invoke(session, "AcknowledgeExpandedModelSuspension", roster, new NativeBattleModelParticipantSuspended(session.ReadPresentationModelOwner(identity), identity, 5));
        }

        Assert.Equal(flags, actor.NativeCombatantFlags);
        Assert.Equal(9, actor.NativeStatus0);
        Assert.Equal(!release, actor.IsActive);
        Assert.Equal(before, (actor.CurrentHp, actor.NativeStatus1, actor.IsBusy, actor.Atb, session.NativeGlobalRandomChange));
        Assert.Empty(session.EnemyDefeats);
    }

    [Fact]
    public void WrongReceiptIdentityOrStaleAllocationRejectsBeforeAnyActorMutation()
    {
        BattleSession session = Fixture();
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        BattleCombatant actor = Actor(session, "ally-10");
        BattleModelOwner owner = session.ReadPresentationModelOwner(actor.Id);
        var before = (actor.NativeCombatantFlags, actor.NativeStatus0);
        AssertInvalid(() => Invoke(session, "AcknowledgeExpandedModelRelease", roster, new NativeBattleModelParticipantReleased(owner, "ally-9")));
        actor.BindRosterSlot(null, bindNative: false);
        AssertInvalid(() => Invoke(session, "AcknowledgeExpandedModelSuspension", roster, new NativeBattleModelParticipantSuspended(owner, actor.Id, 5)));
        Assert.Equal(before, (actor.NativeCombatantFlags, actor.NativeStatus0));
    }

    [Theory]
    [InlineData("enemy-0", "enemy-0", 207, 15, 15, true, true, false)]
    [InlineData("enemy-0", "enemy-0", 264, 17, 17, true, true, false)]
    [InlineData("enemy-13", "enemy-13", 207, 15, 15, true, false, false)]
    [InlineData("enemy-0", "enemy-13", 207, 15, 15, false, false, false)]
    [InlineData("enemy-0", "enemy-0", 207, 15, 17, true, false, false)]
    [InlineData("enemy-0", "enemy-0", 205, 15, 15, true, false, false)]
    [InlineData("enemy-0", "enemy-0", 207, 15, 15, false, false, true)]
    public void CueTimeHeaderSurvivesPacketReplacementAndOnlyOriginalMatchingWitnessCompletes(string actorId, string headerActorId, ushort actionId, byte headerSequence, byte cueSequence, bool captured, bool terminal, bool mismatchedIndex)
    {
        BattleSession session = Fixture(witness: true);
        var (roster, aliases) = session.ReadBattlePresentationPacketBindings();
        BattleModelOwner owner = session.ReadPresentationModelOwner(actorId);
        BattleModelOwner headerActor = session.ReadPresentationModelOwner(headerActorId);
        NativeBattleActionPacketsState bank = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (bank, BattleActionHeaderReference header) = bank.Prepare(new(headerActor, 8, headerSequence, -1, actionId, 0, null));
        (bank, _) = bank.Publish(0);
        bank = bank.ActivateHeader(header);
        NativeBattleHostedTestFixture.BindProcess(session);
        NativeBattleHostedTestFixture.SetField(session.Presentation!, "nativeActionPackets", bank);
        NativeBattleActionGraphicsState action = new(mismatchedIndex ? header.Advance(1) : header, BattleRetainedModelReference.Capture(headerActor, aliases), new BattleContactCursor(0), BattleRetainedModelSelection.Empty, BattleRetainedModelSelection.Empty, null, null, 0, null, 0);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null)) with { ActionGraphics = action };
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(session.ReadBattlePresentationRoster());
        NativeBattleModelActorFacts facts = new(actorId, owner.Side == BattleSide.Allies, 0, 0, 0);

        Invoke(session, "PublishHostedModelLifecycleCue", graphics, owner, graphics.ActorModels.Get(owner), facts, new NativeBattleSequenceCue(NativeBattleSequenceCueKind.SuspendParticipant, cueSequence), services);

        NativeBattleModelParticipantSuspended receipt = Assert.IsType<NativeBattleModelParticipantSuspended>(Assert.Single(services.ReadOrderedReceipts()));
        Assert.Same(captured ? bank.ReadHeader(header) : null, receipt.ActiveHeader);
        NativeBattleHostedTestFixture.SetField(session.Presentation!, "nativeActionPackets", OriginalPackets());
        Invoke(session, "AcknowledgeExpandedModelSuspension", roster, receipt);
        BattleEncounterPolicyState? state = (BattleEncounterPolicyState?)Field("encounterPolicies").GetValue(session);
        Assert.Equal(terminal, state?.TerminalReached == true);
        Assert.Equal(100, Actor(session, actorId).CurrentHp);
    }

    private static BattleSession Fixture(bool witness = false)
    {
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 14).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = "Enemy", Information246 = 0 }).ToArray();
        BattleDefinition definition = new() { Combatants = [.. party, .. enemies], NativeGlobalRandom = new(123), NativeClockRules = new(10), NativeBattleSpeed = 2 };
        if (witness)
        {
            definition = definition with
            {
                EncounterMod = new(EncounterBindingResolver.Disc1Edea, "Classic", "generation", "enemy-0", 11,
                    ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, EncounterCompiledTemplate>.Empty,
                    ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty, ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty,
                    ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty, ImmutableDictionary<string, double>.Empty, new("enemy-0", 0, 2, 3, 1, 10, 4, new([], [], [], [])))
            };
        }
        return new BattleSession(definition, BattleAtbMode.TurnBased);
    }

    private static BattleCombatant Actor(BattleSession session, string id) => session.Combatants.Single(actor => actor.Id == id);
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
    private static object? Invoke(BattleSession session, string name, params object[] args) => typeof(BattleSession).GetMethod(name, PrivateInstance)!.Invoke(session, args);
    private static void AssertInvalid(Action action) => Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(action).InnerException);
}
