using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticSupportingInvocationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Dispatch_observes_the_actor_matched_bound_callback_and_actual_script(int stage)
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

    [Fact]
    public void Actor_matched_active_action_retains_its_actual_callback_provenance()
    {
        BattleSession session = Create();
        BattleActionPresentation action = new("enemy", "party", BattleActionPhase.Windup, 0, BattleAbility.EnemyAbility, 7, false, 0) { NativeAiCallbackStage = 3, NativeAction = NativeAction(154) };
        Field("resolvingNativeAction").SetValue(session, action);
        BattleDiagnosticEffectDispatch observed = Observe(session);
        Assert.Equal((byte)3, observed.NativeAiCallbackStage);
        Assert.Equal(session.ObserveDiagnosticCombatants()[1].NativeAiScriptSha256![3], observed.NativeAiScriptSha256);
    }

    [Fact]
    public void Action_without_callback_provenance_can_use_its_actor_and_effect_matched_bank_binding()
    {
        BattleSession session = Create();
        Bind(session, "enemy", 154, 2);
        BattleActionPresentation action = new("enemy", "party", BattleActionPhase.Windup, 0, BattleAbility.EnemyAbility, 7, false, 0) { NativeAction = NativeAction(154) };
        Field("resolvingNativeAction").SetValue(session, action);
        BattleDiagnosticEffectDispatch observed = Observe(session);
        Assert.Equal((byte)2, observed.NativeAiCallbackStage);
        Assert.Equal(session.ObserveDiagnosticCombatants()[1].NativeAiScriptSha256![2], observed.NativeAiScriptSha256);
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
        BattleAttackCommand command = new(actor, "party", BattleAbility.EnemyAbility, 7) { NativeAiCallbackStage = stage, NativeAction = NativeAction(effect) };
        NativeBattleCommandBanks banks = NativeBattleCommandBanks.Initialize();
        (NativeBattleCommandBanks allocated, NativeBattleCommandLocation location) = banks.Allocate(0);
        banks = allocated.WriteCommand(location, 0, actor == "enemy" ? (byte)3 : (byte)0, 2, 0, 0, 7, [1]).BindCommand(location, 0, command);
        Field("nativeCommandBanks").SetValue(session, banks);
    }

    private static BattleDiagnosticEffectDispatch Observe(BattleSession session)
    {
        BattleDiagnosticEffectDispatch? result = null;
        session.DiagnosticEffectDispatched += dispatch => result = dispatch;
        NativeBattleGraphicsState graphics = new(ImmutableArray<NativeBattleGraphicsModel>.Empty, ["party", null, null, "enemy", null, null, null], 0, 42);
        NativeBattleOriginalEffectState effect = new(154, 153, new(154, 0, 0), new(3, 0, 1, -1, 0, 0), null);
        typeof(BattleSession).GetMethod("ObserveDiagnosticEffectDispatch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, effect]);
        return Assert.IsType<BattleDiagnosticEffectDispatch>(result);
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
