using System.Diagnostics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record DiagnosticPreparationProgress(string Phase, long Callback, long Frame, bool EntryReady, int Attacks, long OrdinaryDamage, string? PredecessorId, int? PredecessorHitPoints, int? PredecessorMaximumHitPoints, uint? PredecessorFlags, bool? AttackerReady, bool? AttackerBusy, string[] DrainBlockers);

internal sealed class DiagnosticPreparationProgressObserver(BattleSession battle, Action<DiagnosticPreparationProgress>? publish)
{
    private long lastCallback = -1;
    private long lastTimestamp;

    internal void Report(string phase, int attacks = 0, long ordinaryDamage = 0, BattleCombatant? predecessor = null, BattleCombatant? attacker = null, bool boundary = false)
    {
        if (publish is null)
        {
            return;
        }
        long callback = battle.NativeHostCallbacks ?? throw new NotSupportedException("Preparation progress requires the original native callback ledger.");
        if (callback <= lastCallback || !boundary && lastCallback >= 0 && Stopwatch.GetElapsedTime(lastTimestamp).TotalSeconds < 1)
        {
            return;
        }
        lastCallback = callback;
        lastTimestamp = Stopwatch.GetTimestamp();
        publish(new(phase, callback, battle.Frame, battle.NativeEntryReady, attacks, ordinaryDamage, predecessor?.Id, predecessor?.CurrentHp, predecessor?.MaximumHp, predecessor?.NativeCombatantFlags, attacker is null ? null : battle.CanChoosePlayerCommand(attacker.Id), attacker?.IsBusy, battle.ObserveDiagnosticDrainBlockers().ToArray()));
    }
}
