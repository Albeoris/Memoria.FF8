using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelProtocolTests
{
    [Theory]
    [InlineData(3, 2, BattleNativeDuelPhase.AwaitingEffect)]
    [InlineData(17, 7, BattleNativeDuelPhase.Finisher)]
    public void UiPublishesOnlyTheSelectionByteUntilTheNativePoll(byte moveSet, byte action, BattleNativeDuelPhase phase)
    {
        BattleSession session = Fixture();
        Dictionary<string, BattleNativeDuelState> states = States(session);
        BattleNativeDuelState original = new(0, Target(), 400, 279, BattleNativeDuelPhase.Ready) { Selection1D28DEF = 254 };
        states.Add("party", original);
        Invoke(session, "QueueNativeDuelChoice", "party", new BattleNativeDuelChoice(moveSet, action, "Move", true, action >= 6, [1]));
        Assert.Equal(original with { MoveSetId = moveSet, Selection1D28DEF = moveSet, Phase = phase }, states["party"] with { Ui = null });
        Assert.Equal(action >= 6 ? BattleTimedInputUiPhase.Closing : BattleTimedInputUiPhase.Active, session.GetDuelUi("party")!.Phase);
        Assert.Empty(session.QueuedActions);
        Assert.Equal((byte)0, session.NativeActionPackets.ActionCount);
        Assert.Throws<TargetInvocationException>(() => Invoke(session, "QueueNativeDuelChoice", "party", new BattleNativeDuelChoice(moveSet, action, "Move", true, action >= 6, [1])));
        BattleNativeDuelState restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(states["party"]));
        Assert.Equal(states["party"].Selection1D28DEF, restored.Selection1D28DEF);
    }

    [Fact]
    public void TimeoutPublishesFFWithoutRemovingTheProcessOrInventingAClosingHeader()
    {
        BattleNativeDuelState active = new(0, Target(), 400, 1, BattleNativeDuelPhase.Ready) { Selection1D28DEF = 254, WidgetPhase = 4, WidgetRevealQ12 = 4096, PublicationLatch = 4 };
        BattleNativeDuelState state = NativeBattleDuelRules.AdvanceWidget(active, false, 0, false, [], static () => throw new InvalidOperationException());
        Assert.Equal((byte)255, state.Selection1D28DEF);
        Assert.Equal(BattleNativeDuelPhase.Finisher, state.Phase);
        Assert.Equal((short)0, state.RemainingTimer);
        Assert.False(state.WidgetClosed);
    }

    [Fact]
    public void AnUnknownSelectionDoesNotBecomeAnIdlePollByDefault()
    {
        BattleSession session = Fixture();
        States(session).Add("party", new(0, Target(), 400, 279, BattleNativeDuelPhase.Ready));
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Invoke(session, "RunNativeDuelSelection4852B0", session.Presentation!.Graphics));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.DoesNotContain("Selection1D28DEF", JsonSerializer.Serialize(States(session)["party"]));
    }

    [Theory]
    [InlineData(65530, false)]
    [InlineData(65532, true)]
    public void ClosingControlsRetainTheActualPrimaryAndTimeoutActorSecondary(ushort actionId, bool secondary)
    {
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeCombatantFlags = 0x4000 }] }, BattleAtbMode.Normal);
        BattleCombatant actor = session.Combatants.Single(candidate => candidate.Id == "party");
        BattleCombatant target = session.Combatants.Single(candidate => candidate.Id == "enemy");
        NativeBattleActionScratch scratch = session.Capture().NativeActionScratch! with { Command = 241, Action = actionId, EventActorB0F = 0, EventLane = 1 };
        typeof(BattleSession).GetField("nativeActionScratch", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, scratch);
        BattleNativeAction native = new(241, actionId, -1, -1) { MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, Target(), BattleAbility.EnemyAbility, actionId) { NativeAction = native, NativeQueuedCommand = 241 };
        BattleActionDefinition action = new(BattleAbility.EnemyAbility, actionId, "Duel", BattleActionEffect.Native, 0, false) { NativeAction = native };
        NativeBattleContactPacket packet = (NativeBattleContactPacket)Invoke(session, "ResolveInstalledNativeHitWithContact", actor, target, presentation, action, native, null!, false)!;
        Assert.Equal((byte)1, packet.Primary.Flags2);
        Assert.Equal((ushort)0, packet.Primary.Amount6);
        Assert.Equal(0u, target.NativeCombatantFlags & 0x4000u);
        if (secondary)
        {
            Assert.Equal(new BattlePartyActorModel(new(actor.Id)), packet.Secondary!.Recipient);
            Assert.Equal((byte)1, packet.Secondary.Flags2);
        }
        else
        {
            Assert.Null(packet.Secondary);
        }
        BattleNativeAction metadata = NativeBattleDuelRules.Control(actionId, actionId == 65530 ? (ushort)6 : (ushort)0);
        Assert.Equal(packet.Secondary is not null, metadata.PresentationSecondarySource);
        Assert.Equal(actionId != 65532, session.Capture().NativeActionScratch!.DeferredEffectsE00);
    }

    [Theory]
    [InlineData(65530, 6)]
    [InlineData(65530, 7)]
    [InlineData(65530, 8)]
    [InlineData(65530, 9)]
    [InlineData(65532, 0)]
    public void SavedClosingControlRequiresExactMetadataAndItsProducedPhysicalHeader(ushort actionId, ushort effect)
    {
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, NativeLimit = new(1, 11, 0, 0, Enumerable.Repeat((byte)0, 32).ToImmutableArray()) }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        BattleNativeAction native = (BattleNativeAction)typeof(BattleSession).GetMethod("CreateNativeDuelControl241", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [actionId, effect])!;
        native = JsonSerializer.Deserialize<BattleNativeAction>(JsonSerializer.Serialize(native))!;
        InstallHeader(effect);
        BattleActionHeaderReference reference = new(0);
        Validate(native, reference);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Validate(native with { Power = 1 }, reference)).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Validate(native, null)).InnerException);
        InstallHeader(checked((ushort)(effect + 1)));
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Validate(native, reference)).InnerException);

        void InstallHeader(ushort selectedEffect)
        {
            NativeBattleActionPacketsState current = session.NativeActionPackets;
            var packets = NativeBattleActionPacketsState.Create(current.Roster, NativeBattleRetainedActionPackets.Fresh(), current.ModelAliases);
            (packets, _) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(0), 241, 0, -1, actionId, selectedEffect, null));
            (packets, _) = packets.Publish(0);
            typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(session, packets);
        }

        void Validate(BattleNativeAction candidate, BattleActionHeaderReference? header)
        {
            Invoke(session, "ValidateSavedAction", "party", "enemy", new HashSet<string>(), BattleAbility.EnemyAbility, (int)actionId, false, candidate, Target(), null!, null!, null!, header!, false, null!, null!, null!, null!, null!);
        }
    }

    [Fact]
    public void ClosingControlMirrorCannotRestoreWithoutItsActualClosingInputOwner()
    {
        BattleSession session = Fixture();
        BattleNativeAction control = (BattleNativeAction)typeof(BattleSession).GetMethod("CreateNativeDuelControl241", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [(ushort)65532, (ushort)0])!;
        Queue<BattleAttackCommand> queue = (Queue<BattleAttackCommand>)typeof(BattleSession).GetField("actions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        queue.Enqueue(new("party", "enemy", Target(), BattleAbility.EnemyAbility, 65532) { NativeAction = control, NativeQueuedCommand = 241, NativeHeader = new BattleActionHeaderReference(0) });
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Invoke(session, "ValidateNativeDuelControlOwners")).InnerException);
        States(session).Add("party", new(0, Target(), 400, 0, BattleNativeDuelPhase.Finisher) { Selection1D28DEF = 254 });
        Invoke(session, "ValidateNativeDuelControlOwners");
        States(session)["party"] = States(session)["party"] with { Phase = BattleNativeDuelPhase.Ready };
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Invoke(session, "ValidateNativeDuelControlOwners")).InnerException);
    }

    [Theory]
    [InlineData(BattleNativeDuelPhase.Ready, 254, 0x4852B0u)]
    [InlineData(BattleNativeDuelPhase.Finisher, 17, 0x4852B0u)]
    [InlineData(BattleNativeDuelPhase.Finisher, 254, 0x47DE70u)]
    [InlineData(BattleNativeDuelPhase.Finisher, 254, 0x47E120u)]
    public void SavedSelectionMustRetainItsActualPendingPollOrClosingEvent(BattleNativeDuelPhase phase, byte selection, uint callback)
    {
        BattleSession session = Fixture();
        BattleNativeDuelState state = new(0, Target(), 400, 279, phase) { Selection1D28DEF = selection };
        MethodInfo method = typeof(BattleSession).GetMethod("ValidateNativeDuelSelectionDescriptors", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.NotNull(method);
        NativeBattleGraphicsState graphics = session.Presentation!.Graphics;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [state, graphics])).InnerException);
        graphics = graphics with { Descriptors = graphics.Descriptors.Enqueue(10, 128, callback).State };
        method.Invoke(null, [state, graphics]);
        NativeBattleGraphicsState wrong = graphics with { Descriptors = NativeBattleDescriptorState.Clear().Enqueue(10, 128, 0x47DD90).State };
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [state, wrong])).InnerException);
    }

    private static BattleSession Fixture()
    {
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleGraphicsState graphics = OriginalGraphics([]);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(session.Presentation!, graphics);
        return session;
    }

    private static BattleTargetRequest Target()
    {
        return NativeBattleTargetRequestAdapter.Decode(8, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: true);
    }

    private static Dictionary<string, BattleNativeDuelState> States(BattleSession session)
    {
        return (Dictionary<string, BattleNativeDuelState>)typeof(BattleSession).GetField("nativeDuelProcesses", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    }

    private static object? Invoke(BattleSession session, string method, params object[] arguments)
    {
        return typeof(BattleSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(session, arguments);
    }
}
