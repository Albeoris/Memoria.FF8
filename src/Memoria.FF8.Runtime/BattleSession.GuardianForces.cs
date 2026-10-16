namespace Memoria.FF8.Runtime;

public sealed record BattleGuardianForceVital(int Id, ushort HitPoints, ushort MaximumHitPoints, bool Acquired = true);
public sealed record BattleGuardianForceCast(string ActorId, BattleAttackCommand Command, ushort RemainingTicks, ushort InitialTicks);
public sealed record BattleGuardianForceChange(int Id, ushort HitPointsBefore, ushort HitPointsAfter);

public sealed partial class BattleSession
{
    private readonly Dictionary<int, BattleGuardianForceVital> guardianForces = [];
    private readonly Dictionary<string, BattleGuardianForceCast> guardianForceCasts = new(StringComparer.Ordinal);
    public IReadOnlyList<BattleGuardianForceVital> GuardianForces => guardianForces.Values.OrderBy(gf => gf.Id).ToArray();
    public IReadOnlyList<BattleGuardianForceCast> GuardianForceCasts => guardianForceCasts.Values.ToArray();

    private void QueueGuardianForce(BattleCombatant actor, BattleAttackCommand command)
    {
        BattleGuardianForceVital gf = guardianForces.GetValueOrDefault(command.ActionId) ?? throw new NotSupportedException("Native GF summoning requires its canonical saved HP owner.");
        if (!gf.Acquired || gf.HitPoints == 0 || guardianForceCasts.Values.Any(cast => cast.Command.ActionId == gf.Id))
        {
            throw new InvalidOperationException("A defeated or already summoning GF cannot be summoned.");
        }
        IReadOnlyList<ushort> compatibility = actor.GuardianForceCompatibility is { Count: 16 } bank ? bank : throw new NotSupportedException("Native GF charge requires all sixteen saved character compatibility WORDs.");
        byte speed = definition.NativeBattleSpeed ?? throw new NotSupportedException("Native GF charge requires the saved battle speed BYTE.");
        //48D200 stores the compatibility/speed product as a WORD;4842B0 decrements it by2,3or1.
        ushort ticks = unchecked((ushort)(4 * ((speed + 1) * compatibility[gf.Id] / 35)));
        guardianForceCasts.Add(actor.Id, new(actor.Id, command, ticks, ticks));
        actor.NativeStatus1 |= 0x80000000;
        Log("GuardianForceCharging", actor.Id, actor.Id, ticks, $"{actor.Name}: {ActionName(command.Ability, command.ActionId)} charging.");
    }

    private void DispatchGuardianForceCasts()
    {
        // TODO(TD-009): Use the current battle callback projection until47CCB0 owns its native pause gates
        foreach (BattleGuardianForceCast cast in guardianForceCasts.Values.ToArray())
        {
            BattleCombatant actor = byId[cast.ActorId];
            BattleGuardianForceVital gf = guardianForces[cast.Command.ActionId];
            if (gf.HitPoints == 0 || (actor.NativeStatus0 & 0x35) != 0 || (actor.NativeStatus1 & 0x14000) != 0 || !actor.IsActive)
            {
                guardianForceCasts.Remove(actor.Id);
                actor.NativeStatus1 &= 0x7FFFFFFF;
                actor.IsBusy = false;
                Log("GuardianForceCancelled", actor.Id, actor.Id, gf.Id, "GF summoning interrupted.");
                continue;
            }
            if (actor.Suspended || (actor.NativeStatus1 & 9) != 0)
            {
                continue;
            }
            int decrement = (actor.NativeStatus1 & 2) != 0 ? 3 : 2;
            if ((actor.NativeStatus1 & 4) != 0)
            {
                decrement = 1;
            }
            ushort remaining = (ushort)Math.Max(0, cast.RemainingTicks - decrement);
            if (remaining != 0)
            {
                guardianForceCasts[actor.Id] = cast with { RemainingTicks = remaining };
                continue;
            }
            guardianForceCasts.Remove(actor.Id);
            actor.NativeStatus1 &= 0x7FFFFFFF;
            actions.Enqueue(cast.Command);
            Log("GuardianForceReady", actor.Id, cast.Command.TargetId, gf.Id, "GF charge completed.");
        }
    }

    private bool ApplyGuardianForceShield(BattleCombatant actor, int amount)
    {
        if (!guardianForceCasts.TryGetValue(actor.Id, out BattleGuardianForceCast? cast) || cast.RemainingTicks == 0 || (actor.NativeStatus1 & 0x80000000) == 0 || (actor.NativeStatus0 & 1) != 0)
        {
            return false;
        }
        BattleGuardianForceVital gf = guardianForces[cast.Command.ActionId];
        if (gf.HitPoints == 0)
        {
            return false;
        }
        int hp = Math.Max(0, gf.HitPoints - amount);
        guardianForces[gf.Id] = gf with { HitPoints = (ushort)hp };
        Log("GuardianForceDamage", actor.Id, actor.Id, gf.HitPoints - hp, $"Summoning GF: {gf.HitPoints - hp} HP.");
        return true;
    }

    private void ResolveMoogleDance(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command)
    {
        if ((recipient.NativeStatus0 & 1) != 0)
        {
            return;
        }
        byte level = definition.NativeIndependentGuardianForceLevel ?? throw new NotSupportedException("Moogle Dance requires its original PocketStation level BYTE.");
        int percentage = level == 0 ? 1 : level;
        foreach (int id in recipient.EquipmentIds)
        {
            BattleGuardianForceVital gf = guardianForces.GetValueOrDefault(id) ?? throw new InvalidDataException("Junctioned GF lacks its canonical saved HP owner.");
            ushort hp = (ushort)Math.Min(gf.MaximumHitPoints, unchecked((ushort)(gf.HitPoints + percentage * gf.MaximumHitPoints / 100)));
            guardianForces[id] = gf with { HitPoints = hp };
            Log("GuardianForceHealing", actor.Id, recipient.Id, hp - gf.HitPoints, $"Moogle Dance: GF {id} recovers {hp - gf.HitPoints} HP.");
        }
        RecordNativeActionMemory(actor, recipient, command);
    }

    private void RestoreGuardianForces(BattleSnapshot saved)
    {
        if (saved.GuardianForces is null || saved.GuardianForceCasts is null || !saved.GuardianForces.Select(gf => gf.Id).Order().SequenceEqual(definition.GuardianForces.Select(gf => gf.Id).Order()))
        {
            throw new InvalidDataException("Saved GF state differs from its battle entry owners.");
        }
        guardianForces.Clear();
        foreach (BattleGuardianForceVital gf in saved.GuardianForces)
        {
            BattleGuardianForceVital initial = definition.GuardianForces.Single(entry => entry.Id == gf.Id);
            if (gf.MaximumHitPoints != initial.MaximumHitPoints || gf.Acquired != initial.Acquired || gf.Acquired && gf.HitPoints > gf.MaximumHitPoints || !guardianForces.TryAdd(gf.Id, gf))
            {
                throw new InvalidDataException("Invalid saved GF HP state.");
            }
        }
        guardianForceCasts.Clear();
        foreach (BattleGuardianForceCast cast in saved.GuardianForceCasts)
        {
            BattleCombatant actor = GetActor(cast.ActorId);
            if (saved.Outcome != BattleOutcome.Active || cast.Command.ActorId != actor.Id || cast.Command.Ability != BattleAbility.GuardianForce || !guardianForces.ContainsKey(cast.Command.ActionId) || !actor.EquipmentIds.Contains(cast.Command.ActionId) || cast.RemainingTicks > cast.InitialTicks || !guardianForceCasts.TryAdd(actor.Id, cast) || actor.Atb != 0 || (actor.NativeStatus1 & 0x80000000) == 0)
            {
                throw new InvalidDataException("Invalid saved GF cast owner.");
            }
            actor.IsBusy = true;
        }
    }
}
