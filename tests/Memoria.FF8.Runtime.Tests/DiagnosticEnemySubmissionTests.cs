using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticEnemySubmissionTests
{
    private static readonly BattleDiagnosticEnemyAbility Selection = new(1, 0, 0, 0, 2, 7, 1, 201);

    [Fact]
    public void Submission_records_the_actual_native_selection_and_queued_physical_target_mask()
    {
        BattleSession session = Create();
        long frame = session.Frame;
        long? callback = session.NativeHostCallbacks;
        session.SubmitDiagnosticEnemyAbility("enemy", Selection, 3);
        BattleDiagnosticEnemySubmission receipt = Assert.IsType<BattleDiagnosticEnemySubmission>(session.LastDiagnosticEnemySubmission);
        BattleAttackCommand queued = Assert.Single(session.Capture().QueuedActions);
        Assert.Equal(session.DiagnosticLifecycleId, receipt.LifecycleId);
        Assert.Equal(frame, receipt.Frame);
        Assert.Equal(callback, receipt.Callback);
        Assert.Equal("enemy", receipt.ActorId);
        Assert.Equal(3, receipt.ActorIndex);
        Assert.Equal(Selection, receipt.Selection);
        Assert.Equal((ushort)3, receipt.ExpectedEffect);
        Assert.Equal(NativeBattleTargetRequestAdapter.ProjectOriginalWord(queued.TargetRequest, _ => null), receipt.TargetMask);
        Assert.Equal((ushort)1, receipt.TargetMask);
        Assert.DoesNotContain("LastDiagnosticEnemySubmission", JsonSerializer.Serialize(session.Capture()));
        BattleDiagnosticEnemySubmission restored = JsonSerializer.Deserialize<BattleDiagnosticEnemySubmission>(JsonSerializer.Serialize(receipt))!;
        Assert.Equal(receipt, restored);
    }

    [Fact]
    public void Unknown_native_callback_remains_unknown_even_when_the_engine_frame_is_known()
    {
        BattleSession session = Create();
        typeof(BattleSession).GetProperty(nameof(BattleSession.Frame))!.SetValue(session, 42L);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(session, null);
        session.SubmitDiagnosticEnemyAbility("enemy", Selection, 3);
        Assert.Equal(42, session.LastDiagnosticEnemySubmission!.Frame);
        Assert.Null(session.LastDiagnosticEnemySubmission.Callback);
    }

    [Fact]
    public void Turn_script_submission_records_its_selection_without_inventing_a_target_mask()
    {
        BattleSession session = Create();
        BattleDiagnosticEnemyAbility selection = Selection with { UseTurnScript = true, AbilitySlot = -1 };
        session.SubmitDiagnosticEnemyAbility("enemy", selection, 3);
        BattleDiagnosticEnemySubmission receipt = Assert.IsType<BattleDiagnosticEnemySubmission>(session.LastDiagnosticEnemySubmission);
        Assert.Equal(selection, receipt.Selection);
        Assert.Null(receipt.TargetMask);
        Assert.Contains(session.Capture().QueuedActions, command => command.ActorId == receipt.ActorId && command.ExpandedAiCallback?.Stage == 1);
    }

    [Fact]
    public void Failed_submission_never_publishes_a_new_receipt()
    {
        BattleSession session = Create();
        Assert.Throws<InvalidOperationException>(() => session.SubmitDiagnosticEnemyAbility("enemy", Selection with { Model = 2 }, 3));
        Assert.Null(session.LastDiagnosticEnemySubmission);
        Assert.Throws<InvalidOperationException>(() => session.SubmitDiagnosticEnemyAbility("enemy", Selection, 4));
        Assert.Null(session.LastDiagnosticEnemySubmission);
        session.SubmitDiagnosticEnemyAbility("enemy", Selection, 3);
        BattleDiagnosticEnemySubmission original = session.LastDiagnosticEnemySubmission!;
        Assert.Throws<InvalidOperationException>(() => session.SubmitDiagnosticEnemyAbility("enemy", Selection, 3));
        Assert.Same(original, session.LastDiagnosticEnemySubmission);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dispatch_target_mask_is_only_the_actual_entered_effect_parameter(bool hasParameters)
    {
        BattleSession session = Create();
        BattleDiagnosticEffectDispatch? observed = null;
        session.DiagnosticEffectDispatched += dispatch => observed = dispatch;
        BattleModelOwner party = session.Presentation!.NativeActionPackets.ModelAliases.DecodeLiteral(0);
        BattleModelOwner enemy = session.Presentation!.NativeActionPackets.ModelAliases.DecodeLiteral(3);
        BattleModelOwner recipient = new BattleForeignPartyModel(new("observed-target"));
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, party, new(2)), (2, recipient, new(2)), (3, enemy, new(2))], callback: 42);
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        NativeBattleActionPacketsState packets = session.Presentation!.NativeActionPackets.WithRoster(graphics.ActorModels.Roster, aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(enemy, 2, 0, -1, 7, 3, null));
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        NativeBattleEffectParameters? parameters = hasParameters ? new NativeBattleEffectParameters(enemy, 0, BattleModelTargets.From([recipient]), header, 0, 0).CaptureOriginalWindow(aliases) : null;
        NativeBattleOriginalEffectState effect = new(3, 2, new(3, 0, 0), parameters, null);
        typeof(BattleSession).GetMethod("ObserveDiagnosticEffectDispatch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, effect]);
        Assert.Equal(hasParameters ? (ushort?)4 : null, observed!.NativeTargetMask);
    }

    private static BattleSession Create()
    {
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(2, 1, 7), 48).ToArray(), 0, []);
        BattleNativeAction action = new(2, 7, 1, 7) { MagicId = 3, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [action],
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0 }, new() { Id = "enemy", Name = "Enemy", ModelId = 1, SceneSlot = 0, NativeCombatantFlags = 1, NativeAi = ai, InitialAtb = BattleSession.AtbMaximum }]
        }, BattleAtbMode.Normal);
        session.ConfigureDiagnosticScheduling(true);
        return session;
    }
}
