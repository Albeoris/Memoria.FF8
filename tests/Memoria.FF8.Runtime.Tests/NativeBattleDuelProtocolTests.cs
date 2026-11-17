using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelProtocolTests
{
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
    public void Saved_closing_control_requires_exact_installed_metadata(ushort actionId, ushort effect)
    {
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, NativeLimit = new(1, 11, 0, 0, Enumerable.Repeat((byte)0, 32).ToImmutableArray()) }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        BattleNativeAction native = NativeBattleDuelRules.Control(actionId, effect);
        native = JsonSerializer.Deserialize<BattleNativeAction>(JsonSerializer.Serialize(native))!;
        Validate(native);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Validate(native with { Power = 1 })).InnerException);

        void Validate(BattleNativeAction candidate)
        {
            Invoke(session, "ValidateSavedAction", "party", "enemy", new HashSet<string>(), BattleAbility.EnemyAbility, (int)actionId, false, candidate, Target(), null!, null!, null!, null!, null!, (byte)241, null!, null!);
        }
    }

    [Fact]
    public void ClosingControlMirrorCannotRestoreWithoutItsActualClosingInputOwner()
    {
        BattleSession session = Fixture();
        BattleNativeAction control = (BattleNativeAction)typeof(BattleSession).GetMethod("CreateNativeDuelControl241", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [(ushort)65532, (ushort)0])!;
        Queue<BattleAttackCommand> queue = (Queue<BattleAttackCommand>)typeof(BattleSession).GetField("actions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        queue.Enqueue(new("party", "enemy", Target(), BattleAbility.EnemyAbility, 65532) { NativeAction = control, NativeQueuedCommand = 241 });
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Invoke(session, "ValidateNativeDuelControlOwners")).InnerException);
        States(session).Add("party", new(0, Target(), 400, 0, BattleNativeDuelPhase.Finisher) { Selection1D28DEF = 254 });
        Invoke(session, "ValidateNativeDuelControlOwners");
        States(session)["party"] = States(session)["party"] with { Phase = BattleNativeDuelPhase.Ready };
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Invoke(session, "ValidateNativeDuelControlOwners")).InnerException);
    }

    private static BattleSession Fixture()
    {
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleGraphicsState graphics = OriginalGraphics([]);
        NativeBattleHostedTestFixture.BindProcess(session, graphics);
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
