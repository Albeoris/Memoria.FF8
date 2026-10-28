using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattlePrelude([property: JsonRequired] BattleInput Input, [property: JsonRequired] int DefeatedModel, [property: JsonRequired] int DefeatedSceneSlot, [property: JsonRequired] string DeathScriptSha256, [property: JsonRequired] bool RequireNativeAllocation)
{
    public int MaximumAttacks { get; init; } = 256;
    public int MaximumCallbacks { get; init; } = 18000;

    internal void Validate(DiagnosticBattleRequest request)
    {
        Input.Validate();
        if (!request.PrepareEntryBeforePresentation || request.AtbMode != BattleAtbMode.Normal || request.Target is null || request.Driver?.EnemyAbility is null || request.Fixture?.HoldEnemyCommands != true || Input.Kind != BattleInputKind.Action || Input.Ability != BattleAbility.Attack || Input.ActionId != 0 || Input.DrawCast || !Input.MagicTargets.IsEmpty || Input.SmartAttackPreview is not null || Input.ActorId == Input.TargetId || Input.ActorId == request.Driver.Input.ActorId || DefeatedModel is < 0 or > 238 || DefeatedSceneSlot is < 0 or > 7 || MaximumAttacks is < 1 or > 1024 || MaximumCallbacks is < 1 or > 18000 || DeathScriptSha256 is null || DeathScriptSha256.Length != 64 || DeathScriptSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("An ordinary attack prelude requires explicit held-enemy Normal-mode setup, a distinct original opponent, a declared enemy invocation and finite attack/callback bounds.");
        }
        if (request.Target.Model != request.Driver.EnemyAbility.Model || request.Target.SceneSlot != request.Driver.EnemyAbility.SceneSlot || request.Target.ActorId != request.Driver.Input.ActorId)
        {
            throw new InvalidDataException("Prelude target and declared focal enemy must identify the same original resource owner.");
        }
    }
}

internal sealed record DiagnosticBattlePreludeReceipt(DiagnosticBattlePrelude Declaration, Guid BattleInstanceId, Guid LifecycleId, long StartCallback, long EndCallback, long RestoreCount, long ResetCount, int Attacks, double Milliseconds, int PredecessorPhysicalIndex, int PredecessorHitPointsBefore, int PredecessorHitPointsAfter, BattleInput OriginalInput, BattleInput ResolvedInput, DiagnosticTargetExpectation OriginalTarget, DiagnosticTargetExpectation ResolvedTarget, BattleDiagnosticTarget Target, BattleEvent[] Events, BattleDiagnosticEffectDispatch[] Dispatches, string Scope);
