using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEnemyTriggerReceipt(string ActorId, long HostCallback, byte NativeCallback, int LevelBand, int HpBefore, int HpPrepared);

public sealed partial class BattleSession
{
    public BattleDiagnosticEnemyTriggerReceipt PrepareDiagnosticEnemyTrigger(string actorId, int model, int sceneSlot, int? levelBand, byte callback, string scriptSha256, int? maximumHpDivisor = null)
    {
        BattleCombatant enemy = GetActor(actorId);
        if (NativeHostCallbacks is not long hostCallback || Outcome != BattleOutcome.Active || !NativeEntryReady || ObserveDiagnosticDrainBlockers().Count != 0 ||
            callback is not (2 or 3) || maximumHpDivisor is < 2 or > 255 || maximumHpDivisor.HasValue && callback != 2 ||
            enemy.IsPlayer || !enemy.IsAlive || !enemy.IsActive || enemy.Untargetable || enemy.ModelId != model || enemy.SceneSlot != sceneSlot || enemy.NativeAi is not { } ai)
        {
            throw new InvalidDataException("The diagnostic trigger requires a drained battle and the live original enemy and callback script.");
        }
        string actualHash = Convert.ToHexString(SHA256.HashData(ai.Scripts[callback]));
        if (levelBand is int requiredBand && ai.LevelBand != requiredBand || !string.Equals(scriptSha256, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Diagnostic enemy trigger resolved level band {ai.LevelBand} and script SHA-256 {actualHash} for its declared callback.");
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
        return new(actorId, hostCallback, callback, ai.LevelBand, before, enemy.CurrentHp);
    }
}
