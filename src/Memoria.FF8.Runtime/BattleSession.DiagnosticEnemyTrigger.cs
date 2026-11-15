using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEnemyTriggerReceipt(string ActorId, long HostCallback, byte NativeCallback, int HpBefore, int HpPrepared);

public sealed partial class BattleSession
{
    public BattleDiagnosticEnemyTriggerReceipt PrepareDiagnosticEnemyTrigger(string actorId, int model, int sceneSlot, int levelBand, byte callback, string scriptSha256, int? maximumHpDivisor = null)
    {
        BattleCombatant enemy = GetActor(actorId);
        if (NativeHostCallbacks is not long hostCallback || Outcome != BattleOutcome.Active || !NativeEntryReady || ObserveDiagnosticDrainBlockers().Count != 0 ||
            callback is not (2 or 3) || maximumHpDivisor is < 2 or > 255 || maximumHpDivisor.HasValue && callback != 2 ||
            enemy.IsPlayer || !enemy.IsAlive || !enemy.IsActive || enemy.Untargetable || enemy.ModelId != model ||
            enemy.SceneSlot != sceneSlot || enemy.NativeAi is not { } ai || ai.LevelBand != levelBand ||
            !string.Equals(scriptSha256, Convert.ToHexString(SHA256.HashData(ai.Scripts[callback])), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The diagnostic trigger requires a drained battle and the live original enemy and callback script.");
        }
        int before = enemy.CurrentHp;
        if (callback == 3 && before > 1)
        {
            // The player's real contact still applies KO and invokes the native enemy script.
            enemy.CurrentHp = 1;
        }
        else if (maximumHpDivisor is int divisor)
        {
            enemy.CurrentHp = Math.Max(1, enemy.MaximumHp / divisor);
        }
        return new(actorId, hostCallback, callback, before, enemy.CurrentHp);
    }
}
