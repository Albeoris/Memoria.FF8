using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticEnemyTrigger(string ActorId, int Model, int SceneSlot, int? LevelBand, byte Callback, string ScriptSha256, int? MaximumHpDivisor = null, int MaximumAttempts = 1)
{
    public DiagnosticAllocatedEnemyFocus? AllocatedFocus { get; init; }

    internal void Validate(BattleInput input)
    {
        if (Model is < 0 or > 238 || SceneSlot is < 0 or >= ExpandedBattleFormation.OriginalEnemyPrototypeCount ||
            ActorId != $"enemy-{SceneSlot}" || input.ActorId == ActorId || input.TargetId != ActorId ||
            LevelBand is < 0 or > 2 || Callback is not (2 or 3) || ScriptSha256 is null || ScriptSha256.Length != 64 || !ScriptSha256.All(Uri.IsHexDigit) ||
            MaximumHpDivisor is < 2 or > 255 || MaximumHpDivisor.HasValue && Callback != 2 || MaximumAttempts is < 1 or > 8)
        {
            throw new InvalidDataException("A diagnostic enemy trigger requires the original counter or death script and a distinct attacking player.");
        }
        if (AllocatedFocus is { } focus && (Callback != 3 || MaximumAttempts != 1 || focus.Model == Model ||
            focus.Model is < 0 or > 238 || focus.SceneSlot == SceneSlot || focus.SceneSlot is < 0 or >= ExpandedBattleFormation.OriginalEnemyPrototypeCount ||
            focus.PhysicalIndex is < 3 or > 6 || focus.ScriptSha256 is null || focus.ScriptSha256.Length != 64 || !focus.ScriptSha256.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException("An allocated focal owner requires a single original death trigger, a distinct installed prototype and its initialization script.");
        }
    }

    internal BattleDiagnosticEnemyTriggerReceipt Prepare(BattleSession battle)
    {
        return battle.PrepareDiagnosticEnemyTrigger(ActorId, Model, SceneSlot, LevelBand, Callback, ScriptSha256, MaximumHpDivisor);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticAllocatedEnemyFocus(int Model, int SceneSlot, int PhysicalIndex, string ScriptSha256);

internal sealed record DiagnosticAllocatedEnemyFocusReceipt(long DispatchSerial, long DispatchCallback, BattleEvent Allocation, BattleDiagnosticTarget Target, DiagnosticBattleRosterMember[] MembersAtDispatch);
