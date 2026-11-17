using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticSupportingInvocationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Dispatch_observes_the_actor_matched_active_callback_and_actual_script(int stage)
    {
        BattleSession session = Create();
        Bind(session, "enemy", 154, (byte)stage);
        BattleDiagnosticEffectDispatch observed = Observe(session);
        Assert.Equal((byte)stage, observed.NativeAiCallbackStage);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(new byte[] { (byte)stage, 0 })), observed.NativeAiScriptSha256);
        Assert.Equal(session.ObserveDiagnosticCombatants()[1].NativeAiScriptSha256![stage], observed.NativeAiScriptSha256);
        Assert.Equal("enemy", observed.ActorId);
        Assert.Equal((byte)2, observed.NativeCommand);
        Assert.Equal(7, observed.ActionId);
    }

    [Theory]
    [InlineData("enemy", 154, null)]
    [InlineData("party", 154, 2)]
    [InlineData("enemy", 155, 2)]
    public void Dispatch_does_not_infer_unknown_or_unmatched_callback_provenance(string actor, int effect, int? stage)
    {
        BattleSession session = Create();
        Bind(session, actor, (ushort)effect, stage is int value ? (byte)value : null);
        BattleDiagnosticEffectDispatch observed = Observe(session);
        Assert.Null(observed.NativeAiCallbackStage);
        Assert.Null(observed.NativeAiScriptSha256);
    }

    [Fact]
    public void Unknown_script_does_not_acquire_a_hash_from_another_callback()
    {
        BattleSession session = Create();
        Bind(session, "enemy", 154, 9);
        BattleDiagnosticEffectDispatch observed = Observe(session);
        Assert.Equal((byte)9, observed.NativeAiCallbackStage);
        Assert.Null(observed.NativeAiScriptSha256);
    }

    [Fact]
    public void Supporting_declaration_requires_bounded_reaction_identity_and_full_script_hash()
    {
        BattleDiagnosticSupportingInvocation valid = new("enemy", 2, 7, 154, 2, new string('A', 64));
        valid.Validate();
        (valid with { CallbackStage = 3 }).Validate();
        BattleDiagnosticSupportingInvocation[] invalid =
        [
            valid with { ActorId = " " },
            valid with { ActorId = new string('x', 129) },
            valid with { NativeCommand = 0 },
            valid with { ActionId = 0 },
            valid with { EffectId = 0 },
            valid with { CallbackStage = 0 },
            valid with { CallbackStage = 1 },
            valid with { CallbackStage = 4 },
            valid with { ScriptSha256 = "ABC" },
            valid with { ScriptSha256 = new string('G', 64) }
        ];
        foreach (BattleDiagnosticSupportingInvocation declaration in invalid)
        {
            Assert.Throws<InvalidDataException>(declaration.Validate);
        }
    }

    private static BattleSession Create()
    {
        NativeBattleAiDefinition ai = new([[0], [1, 0], [2, 0], [3, 0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeCombatantFlags = 1, NativeAi = ai }]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction NativeAction(ushort effect)
    {
        return new(2, 7, 1, 7)
        {
            MagicId = effect,
            AttackType = 0,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
    }

    private static void Bind(BattleSession session, string actor, ushort effect, byte? stage)
    {
        BattleActionPresentation action = new(actor, "party", BattleActionPhase.Windup, new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattlePartyActorReference("party")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), BattleAbility.EnemyAbility, 7, false) { NativeAiCallbackStage = stage, NativeAction = NativeAction(effect) };
        Field("resolvingNativeAction").SetValue(session, action);
    }

    private static BattleDiagnosticEffectDispatch Observe(BattleSession session)
    {
        BattleDiagnosticEffectDispatch? result = null;
        session.DiagnosticEffectDispatched += dispatch => result = dispatch;
        BattleModelOwner actor = OriginalOwner(3);
        BattleModelOwner recipient = OriginalOwner(0);
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, recipient, new(3)), (3, actor, new(3))], callback: 42);
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(session, graphics);
        NativeBattleActionPacketsState packets = process.ActionPackets;
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(actor, 2, 0, -1, 7, 154, null));
        NativeBattleHostedTestFixture.SetField(process, "nativeActionPackets", packets);
        NativeBattleEffectParameters parameters = new(actor, 0, BattleModelTargets.From([recipient]), header, 0, 0);
        NativeBattleOriginalEffectState effect = new(154, 153, new(154, 0, 0), parameters.CaptureOriginalWindow(packets.ModelAliases), null);
        BattlePresentationEffectObservation entry = new(effect, packets.ReadHeader(header), "enemy", 42, 3, 1, 0, null);
        typeof(BattleSession).GetMethod("ObservePresentationEffect", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [process, entry]);
        return Assert.IsType<BattleDiagnosticEffectDispatch>(result);
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
