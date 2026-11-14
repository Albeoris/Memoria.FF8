using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEnemyWithdrawalTests
{
    public static void Run()
    {
        Ff7BattleAttack record = new(255, 255, 0, 0, 65535, 65535, 65535, 0, 255, 0, 0, 255, 255, 0x1B, 255, uint.MaxValue, 0, 0xFF7F);
        Ff7BattleDamageActor stats = new(10, 20, 20, 20, 20, 0, 0, 0, 0);
        Ff7BattleActionVitals vitals = new(100, 100, 10, 10, 0);
        Ff7BattleRng affected = new(new byte[3988], new(new byte[8]));
        Ff7BattleRng ordinary = new(new byte[3988], new(new byte[8]));
        Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(record), record, affected);
        Ff7BattleAttack noEffect = record with { AdditionalEffect = 255 };
        Ff7BattleActionReceipt baseline = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(noEffect), noEffect, ordinary);
        Check(receipt.WithdrawTarget && receipt.Source == vitals && receipt.Target == vitals && receipt.Damage.Damage == 0 && !receipt.Damage.Missed, "Phase4 withdrawal must publish intent without changing target/source vitals.");
        Check((receipt with { WithdrawTarget = false }) == baseline && JsonSerializer.Serialize(affected.Capture()) == JsonSerializer.Serialize(ordinary.Capture()), "Withdrawal must preserve the ordinary transaction and add no RNG draws.");
        Check(!JsonSerializer.Serialize(baseline).Contains("WithdrawTarget", StringComparison.Ordinal), "An absent withdrawal intent must retain the previous serialized receipt shape.");
        Ff7BattleAttack physical = record with { DamageCalculation = 0x11, Power = 16, Elements = 8, SpecialFlags = 65531 };
        Ff7BattleActionReceipt lateMiss = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats with { NativeConditions = 0x100 }, vitals, Ff7BattleDamageAction.FromAttack(physical), physical, new(new byte[3988], new(new byte[8])));
        Check(lateMiss.Damage.Missed && lateMiss.WithdrawTarget, "Phase4 publication must precede the later condition-immunity pending-output cleanup.");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original enemy withdrawal actions: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneFormation formation = formations.Single(value => value.FormationId == 56);
        SceneBattleData data = SceneBattleData.Read(formation);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1);
        Ff7BattleSnapshot start = initial.Capture();
        int birdSlot = start.Actors.Single(actor => actor.Identity == 339).Actor.Slot;
        SceneFormation sessionFormation = formation;
        Ff7BattleSession Restore(Ff7BattleSnapshot snapshot) => new(sessionFormation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Check(start.WithdrawnMask == 0 && Restore(start).Capture().WithdrawnMask == 0 && Restore(start with { WithdrawnMask = null }).Capture().WithdrawnMask == 0, "Fresh, JSON and absent legacy withdrawal state must retain the native zero mask.");
        foreach (ushort attackId in new ushort[] { 0x35A, 0x35B })
        {
            SceneBattleAttack attack = data.Attacks.Single(value => value.Id == attackId);
            Check(attack.Data.AdditionalEffect == 0x1B && attack.Data.DamageCalculation == 0, "Original Escape records must route through their native phase4 effect.");
            foreach (int targetSlot in new[] { birdSlot, 0, 5 })
            {
                sessionFormation = formation;
                if (targetSlot != birdSlot)
                {
                    byte[] authored = formation.RawScene.ToArray();
                    authored[0x4C0 + attack.Slot * Ff7BattleAttack.RecordSize + 12] = 1;
                    sessionFormation = formation with { RawScene = authored };
                }
                string fixture = targetSlot == birdSlot ? "original self-target" : "authored selectable-target variant";
                Ff7BattleSnapshot queued = start with { Queue = Ff7BattleCommandQueue.Enqueue(start.Queue, new(2, birdSlot, 0x20, (ushort)attack.Slot, (ushort)(1 << targetSlot), attackId)) };
                Ff7BattleSession warm = Restore(queued);
                Ff7BattleSession cold = Restore(queued);
                Ff7BattleAiActor before = start.Actors.Single(actor => actor.Actor.Slot == targetSlot).Actor;
                byte[] beforeMemory = start.Actors.Single(actor => actor.Actor.Slot == birdSlot).AiMemory;
                bool prepared = false;
                bool applied = false;
                for (int tick = 0; tick < 2000; tick++)
                {
                    warm.Advance(waitForCommand: true);
                    cold.Advance(waitForCommand: true);
                    if (!prepared && warm.Action is { Applied: false })
                    {
                        Check(warm.Capture().WithdrawnMask == 0, "Withdrawal may not publish before the action contact.");
                        cold = Restore(warm.Capture());
                        prepared = true;
                    }
                    applied |= warm.LastHit is { } hit && hit.AttackId == attackId;
                    if (applied && warm.Action is null)
                    {
                        break;
                    }
                }
                Ff7BattleSnapshot completed = warm.Capture();
                Ff7BattleAiActor after = completed.Actors.Single(actor => actor.Actor.Slot == targetSlot).Actor;
                ushort expectedMask = targetSlot >= 4 ? (ushort)(1 << targetSlot) : (ushort)0;
                Check(prepared && applied && warm.Action is null && completed.WithdrawnMask == expectedMask, $"Escape{attackId:X} {fixture} target{targetSlot} did not publish exactly its native enemy-only mask: prepared={prepared}, applied={applied}, action={warm.Action?.Command}, lastHit={warm.LastHit}, withdrawn={completed.WithdrawnMask:X}, expected={expectedMask:X}, tick={warm.Tick}, outcome={warm.Outcome}, queue={JsonSerializer.Serialize(completed.Queue)}.");
                Check(after.HitPoints == before.HitPoints && after.MagicPoints == before.MagicPoints && after.Statuses == before.Statuses && after.Conditions == before.Conditions, "The withdrawal effect must not fabricate HP, death, status or condition changes.");
                Check((after.Conditions & 0x2000) == 0 && completed.Actors.Single(actor => actor.Actor.Slot == birdSlot).AiMemory.SequenceEqual(beforeMemory), "Withdrawal must suppress target counters without running a death callback.");
                Equal(warm, cold);
                cold = Restore(completed);
                for (int tick = 0; tick < 8; tick++)
                {
                    warm.Advance(waitForCommand: true);
                    cold.Advance(waitForCommand: true);
                }
                Equal(warm, cold);
                Ff7BattleSession successor = new(formations.Single(value => value.FormationId == 58), progress, kernel, executable.NativeMateriaStatModifiers, 2, previousFormation: completed);
                Check(successor.Capture().WithdrawnMask == expectedMask, "A successor must retain the native encounter-global withdrawal mask, including bits outside its new allocation.");
                Ff7BattleSnapshot successorSnapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(successor.Capture()))!;
                Ff7BattleSession successorCold = new(formations.Single(value => value.FormationId == 58), progress, kernel, executable.NativeMateriaStatModifiers, 2, snapshot: successorSnapshot);
                Equal(successor, successorCold);
                successor.Advance(waitForCommand: true);
                successorCold.Advance(waitForCommand: true);
                Equal(successor, successorCold);
                if (targetSlot >= 4)
                {
                    Ff7BattleSnapshot finished = completed with { Outcome = Ff7BattleOutcome.Victory, Actors = completed.Actors.Select(actor => actor.Actor.Slot >= 4 ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 } } : actor).ToArray() };
                    Ff7BattleSession victory = Restore(finished);
                    Ff7VictoryProjection first = victory.ProjectVictory(progress, 123);
                    Ff7VictoryProjection second = victory.ProjectVictory(first.Progress, 123);
                    uint gil = data.Enemies.Where(enemy => enemy.FormationSlot + 4 != targetSlot).Aggregate(0U, (sum, enemy) => sum + enemy.Rewards.Gil);
                    uint experience = data.Enemies.Where(enemy => enemy.FormationSlot + 4 != targetSlot).Aggregate(0U, (sum, enemy) => sum + enemy.Rewards.Experience);
                    uint abilityPoints = data.Enemies.Where(enemy => enemy.FormationSlot + 4 != targetSlot).Aggregate(0U, (sum, enemy) => sum + enemy.Rewards.AbilityPoints);
                    Check(first.Reward.Gil == gil && first.Reward.Experience == experience && first.Reward.AbilityPoints == abilityPoints, "Withdrawn actors must remain excluded from rewards even if a later recorded death fact is present.");
                    Check(JsonSerializer.Serialize(first.Progress) == JsonSerializer.Serialize(second.Progress) && first.Progress.CompletedBattles.Length == progress.CompletedBattles.Length + 1, "Withdrawal-filtered rewards must settle once.");
                }
            }
        }
        Console.WriteLine("Native phase4 enemy withdrawal vitals/RNG, original Escape035A/035B publication/counters/JSON and once-only reward suppression passed.");
    }

    private static void Equal(Ff7BattleSession warm, Ff7BattleSession cold)
    {
        Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Withdrawal continuation changed through JSON restore.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
