using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticTarget(string ActorId, int? Stage, int Model, int? SceneSlot, int PhysicalIndex, uint Flags, int HitPoints, bool Alive, bool StatisticsReady, string InitializationScriptSha256, long Callback);

public sealed partial class BattleSession
{
    /// <summary>Reads live participant identity without capturing or changing any battle owner.</summary>
    public BattleDiagnosticTarget ObserveDiagnosticTarget(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        byte[] startup = actor.NativeAi?.Scripts[0] ?? throw new NotSupportedException("Diagnostic target verification requires its original initialization script.");
        return new(actor.Id, definition.StageId, actor.ModelId, actor.SceneSlot, actor.NativeIndex, actor.NativeCombatantFlags, actor.CurrentHp, actor.IsAlive, !actor.NativeAllocationPendingStatistics && actor.ResolvedNativeLevel is not null, Convert.ToHexString(SHA256.HashData(startup)).ToLowerInvariant(), NativeHostCallbacks ?? Frame);
    }
}
