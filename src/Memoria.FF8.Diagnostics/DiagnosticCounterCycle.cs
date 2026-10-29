using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

/// <summary>Declares a bounded ordinary attack protocol whose focal action must be a real native counter.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticCounterCycleDeclaration([property: JsonRequired] BattleDiagnosticSupportingInvocation Focal, [property: JsonRequired] int MaxTriggerAttempts)
{
    // This is a diagnostic resource limit per focal cycle, not a prediction of native counter frequency.
    public const int MaximumTriggerAttempts = 256;
    public const string SupportedScriptSha256 = "72FF21C2324E5A9828C693DA6223E420F39D9B44B23E5C8E7241CAABFBF6B0A3";

    public void Validate(BattleInput input, ushort effect, int? nativeAction, byte? nativeCommand)
    {
        if (input is null || Focal is null)
        {
            throw new InvalidDataException("Counter cycles require an ordinary semantic trigger and a distinct focal reaction descriptor.");
        }
        input.Validate();
        Focal.Validate();
        if (MaxTriggerAttempts is < 1 or > MaximumTriggerAttempts)
        {
            throw new InvalidDataException("Counter cycles require one through 256 trigger attempts per focal cycle.");
        }
        if (Focal.NativeCommand != 8 || Focal.ActionId != 230 || Focal.EffectId != 178 || Focal.CallbackStage != 2 || !string.Equals(Focal.ScriptSha256, SupportedScriptSha256, StringComparison.OrdinalIgnoreCase) || effect != Focal.EffectId || nativeAction != Focal.ActionId || nativeCommand != Focal.NativeCommand)
        {
            throw new InvalidDataException("Counter cycles require the supported command8/action230/effect178 callback2 script and exact focal invocation identity.");
        }
        if (input.Kind != BattleInputKind.Action || input.Ability != BattleAbility.Attack || input.ActionId != 0 || string.IsNullOrWhiteSpace(input.ActorId) || string.Equals(input.ActorId, Focal.ActorId, StringComparison.Ordinal) || !string.Equals(input.TargetId, Focal.ActorId, StringComparison.Ordinal) || input.DrawCast || input.DrawCastTargetId is not null || !input.MagicTargets.IsEmpty || input.SmartAttackPreview is not null)
        {
            throw new InvalidDataException("Counter cycles require ordinary Action/Attack0 from another actor to the declared focal counter owner.");
        }
    }

    public void ValidateComposition(BattleInput input, ushort effect, int? nativeAction, byte? nativeCommand, bool hasShotCycle, bool hasEnemyAbility, bool hasLimitSelection, IReadOnlyList<ushort> supportingEffects, IReadOnlyList<BattleDiagnosticSupportingInvocation> supportingInvocations)
    {
        Validate(input, effect, nativeAction, nativeCommand);
        if (hasShotCycle || hasEnemyAbility || hasLimitSelection)
        {
            throw new InvalidDataException("Counter cycles cannot compose Shot, direct enemy submission or Limit selection coordinators.");
        }
        if (supportingEffects is null || supportingEffects.Any(supportingEffect => supportingEffect == 0 || supportingEffect == effect) || supportingEffects.Distinct().Count() != supportingEffects.Count)
        {
            throw new InvalidDataException("Counter supporting effects must remain distinct positive nonfocal effects.");
        }
        if (supportingInvocations is null || supportingInvocations.Count > 32 || supportingInvocations.Any(invocation => invocation is null) || supportingInvocations.Distinct().Count() != supportingInvocations.Count)
        {
            throw new InvalidDataException("Counter supporting invocations require distinct bounded original reaction descriptors.");
        }
        foreach (BattleDiagnosticSupportingInvocation invocation in supportingInvocations)
        {
            invocation.Validate();
            if (invocation.EffectId == effect || string.Equals(invocation.ActorId, input.ActorId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Counter supporting invocations cannot replace the focal effect or belong to the control actor.");
            }
        }
    }
}
