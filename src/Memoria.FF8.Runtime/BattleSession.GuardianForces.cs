namespace Memoria.FF8.Runtime;

public sealed record BattleGuardianForceVital(int Id, ushort HitPoints, ushort MaximumHitPoints, bool Acquired = true)
{
    public ushort? KillCount { get; init; }
    public ushort? DeathCount { get; init; }
}
public sealed record BattleGuardianForceCast(string ActorId, BattleAttackCommand Command, ushort RemainingTicks, ushort InitialTicks);
public sealed record BattleGuardianForceChange(int Id, ushort HitPointsBefore, ushort HitPointsAfter);
public sealed record BattleGuardianForceCompatibilityChange(byte Row, ushort[] Before, ushort[] After);

public sealed partial class BattleSession
{
    private readonly Dictionary<int, BattleGuardianForceVital> guardianForces = [];
    private readonly Dictionary<string, BattleGuardianForceCast> guardianForceCasts = new(StringComparer.Ordinal);
    private BattleNativeAction? nativeAffinityAction;
    public IReadOnlyList<BattleGuardianForceVital> GuardianForces => guardianForces.Values.OrderBy(gf => gf.Id).ToArray();
    public IReadOnlyList<BattleGuardianForceCast> GuardianForceCasts => guardianForceCasts.Values.ToArray();

    private void AcquireGuardianForceInBattle(int id)
    {
        BattleGuardianForceVital gf = guardianForces.GetValueOrDefault(id) ?? throw new NotSupportedException("Native GF acquisition requires its original saved HP row.");
        //47E480 changes only the acquisition flag;495D80 belongs to later explicit GF refresh/healing producers.
        guardianForces[id] = gf with { Acquired = true };
    }

    private void QueueGuardianForce(BattleCombatant actor, BattleAttackCommand command)
    {
        BattleGuardianForceVital gf = guardianForces.GetValueOrDefault(command.ActionId) ?? throw new NotSupportedException("Native GF summoning requires its canonical saved HP owner.");
        if (!gf.Acquired || gf.HitPoints == 0 || guardianForceCasts.Values.Any(cast => cast.Command.ActionId == gf.Id) || guardianForms?.Values.Any(form => form.GuardianForceId == gf.Id && form.ActorId != actor.Id) == true)
        {
            throw new InvalidOperationException("A defeated or already summoning GF cannot be summoned.");
        }
        IReadOnlyList<ushort> compatibility = actor.GuardianForceCompatibility is { Count: 16 } bank ? bank : throw new NotSupportedException("Native GF charge requires all sixteen saved character compatibility WORDs.");
        byte speed = definition.NativeBattleSpeed ?? throw new NotSupportedException("Native GF charge requires the saved battle speed BYTE.");
        //48D200 stores the compatibility/speed product as a WORD;4842B0 decrements it by2,3or1.
        ushort ticks = unchecked((ushort)(4 * ((speed + 1) * compatibility[gf.Id] / 35)));
        byte flags = FindAction(BattleAbility.GuardianForce, gf.Id).NativeAction?.TargetFlags ?? throw new NotSupportedException("Native GF summoning requires its original target flags BYTE.");
        ushort mask = NativeBattleLimitRules.PlayerTargetMask(flags, GetActor(command.TargetId).NativeIndex);
        EnqueueNativeGuardianScriptTask(actor, mask, checked((byte)(64 + gf.Id)));
        guardianForceCasts.Add(actor.Id, new(actor.Id, command, diagnosticInstantGuardianForceCharge ? (ushort)0 : ticks, ticks));
        BeginNativePresentedGuardianForce(actor, gf);
        actor.NativeStatus1 |= 0x80000000;
        Log("GuardianForceCharging", actor.Id, actor.Id, ticks, $"{actor.Name}: {ActionName(command.Ability, command.ActionId)} charging.");
        if (diagnosticInstantGuardianForceCharge)
        {
            Log("DiagnosticGuardianForceChargeSkipped", actor.Id, actor.Id, ticks, "Diagnostic scheduling completed only the GF charge wait; native task admission and effect callbacks remain unchanged.");
        }
    }

    private void DispatchGuardianForceCasts()
    {
        foreach (BattleGuardianForceCast cast in guardianForceCasts.Values.ToArray())
        {
            BattleCombatant actor = byId[cast.ActorId];
            BattleGuardianForceVital gf = guardianForces[cast.Command.ActionId];
            if (gf.HitPoints == 0 || (actor.NativeStatus0 & 0x35) != 0 || (actor.NativeStatus1 & 0x14000) != 0 || !actor.IsActive)
            {
                actor.NativeStatus1 &= 0x7FFFFFFF;
                actor.NativeCombatantFlags &= ~0x400u;
                continue;
            }
            if ((actor.NativeCombatantFlags & 0x400) != 0 || actor.Suspended || (actor.NativeStatus1 & 9) != 0)
            {
                continue;
            }
            int decrement = (actor.NativeStatus1 & 2) != 0 ? 3 : 2;
            if ((actor.NativeStatus1 & 4) != 0)
            {
                decrement = 1;
            }
            ushort remaining = (ushort)Math.Max(0, cast.RemainingTicks - decrement);
            guardianForceCasts[actor.Id] = cast with { RemainingTicks = remaining };
        }
    }

    private bool PollNativeGuardianScriptTask(NativeBattleScriptTask task)
    {
        byte index = task.ReadByte(12);
        byte nativeId = task.ReadByte(14);
        BattleCombatant actor = combatants.Single(actor => actor.NativeIndex == index);
        BattleGuardianForceCast cast = guardianForceCasts.GetValueOrDefault(actor.Id) ?? throw new InvalidDataException("Native48E620 lacks its character-owned GF charge record.");
        if (nativeId != cast.Command.ActionId + 64 || task.ReadByte(13) != 254)
        {
            throw new InvalidDataException("Native48E620 task differs from its character GF charge owner.");
        }
        if ((actor.NativeCombatantFlags & 0x400) != 0)
        {
            return false;
        }
        bool summoning = (actor.NativeStatus1 & 0x80000000) != 0;
        if (summoning && cast.RemainingTicks != 0)
        {
            return false;
        }
        if (summoning && cast.Command.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.EntryOnly } && (ActiveAction is not null || nativeTimedAction is not null || QueuedActions.Any(command => command.NativeHeaderIndex is not null) || NativeGraphics is { } activeGraphics && NativeOriginalEffectBusy(activeGraphics)))
        {
            return false;
        }
        if (summoning)
        {
            if (BeginCompletedGuardianFormCharge(actor, cast))
            {
                guardianForceCasts.Remove(actor.Id);
                actor.NativeStatus1 &= 0x7FFFFFFF;
                actor.NativeCombatantFlags &= ~0x400u;
                return true;
            }
            byte targetFlags = FindAction(BattleAbility.GuardianForce, cast.Command.ActionId).NativeAction?.TargetFlags ?? throw new NotSupportedException("Native48F2C0 GF completion requires its original target flags BYTE.");
            ushort mask = NativeAutomaticActionTargetMask(targetFlags);
            EnqueueNativeInputCommand(cast.Command with { NativeTargetMask = mask });
            Log("GuardianForceReady", actor.Id, cast.Command.TargetId, cast.Command.ActionId, "GF charge completed.");
        }
        else
        {
            if ((actor.NativeStatus0 & 1) == 0)
            {
                actor.Atb = 0;
                actor.NativeCombatantFlags &= ~0xCu;
                if (NativeGraphics is { } graphics)
                {
                    NativeGraphics = QueueNativeDescriptor(graphics, 114, 240, index);
                }
            }
            actor.IsBusy = false;
            if (cast.Command.GuardianFormInvocation is not null)
            {
                ReturnGuardianForm(actor, !actor.IsAlive);
            }
            Log("GuardianForceCancelled", actor.Id, actor.Id, cast.Command.ActionId, "GF summoning interrupted.");
        }
        guardianForceCasts.Remove(actor.Id);
        actor.NativeStatus1 &= 0x7FFFFFFF;
        actor.NativeCombatantFlags &= ~0x400u;
        return true;
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
        MarkNativeContactFlags(0, 0x20);
        int hp = Math.Max(0, gf.HitPoints - amount);
        guardianForces[gf.Id] = NativeGuardianForceShieldResult(gf, (ushort)hp);
        Log("GuardianForceDamage", actor.Id, actor.Id, gf.HitPoints - hp, $"Summoning GF: {gf.HitPoints - hp} HP.");
        return true;
    }

    private void ResolveMoogleDance(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command)
    {
        if ((recipient.NativeStatus0 & 1) != 0)
        {
            return;
        }
        NativePocketStationState pocketStation = definition.NativePocketStation ?? throw new NotSupportedException("Moogle Dance requires its original PocketStation saved block.");
        pocketStation.Validate();
        byte level = pocketStation.Level;
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

    private void CompleteGuardianForceAffinity(BattleCombatant actor, BattleActionPresentation command)
    {
        if (command.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry } || !actor.IsPlayer || !guardianForces.Values.Any(gf => gf.Acquired))
        {
            return;
        }
        BattleNativeAction? action = nativeAffinityAction;
        if (action is null || action.Command is not (2 or 6 or 254))
        {
            return;
        }
        int offset = action.KernelSection == 1 ? 42 : action.KernelSection == 2 ? 112 : throw new InvalidDataException("GF affinity source differs from the original spell/GF invocation owner.");
        if (action.RawRecord.Length < offset + 16 || actor.MutableGuardianForceCompatibility is not { Length: 16 } compatibility)
        {
            throw new NotSupportedException("GF affinity requires its saved compatibility WORDs and original sixteen source bytes.");
        }
        foreach (BattleGuardianForceVital gf in guardianForces.Values.Where(gf => gf.Acquired))
        {
            ushort updated = unchecked((ushort)(compatibility[gf.Id] + action.RawRecord[offset + gf.Id] - 100));
            compatibility[gf.Id] = Math.Clamp(updated, (ushort)1000, (ushort)6000);
        }
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
            bool acquired = initial.Acquired || saved.NativeScriptState.AcquiredGuardianForces.Contains((byte)gf.Id);
            if ((gf.KillCount is null) != (initial.KillCount is null) || (gf.DeathCount is null) != (initial.DeathCount is null) || gf.MaximumHitPoints != initial.MaximumHitPoints || gf.Acquired != acquired || gf.HitPoints > Math.Max(initial.HitPoints, initial.MaximumHitPoints) || !guardianForces.TryAdd(gf.Id, gf))
            {
                throw new InvalidDataException("Invalid saved GF HP state.");
            }
        }
        guardianForceCasts.Clear();
        foreach (BattleGuardianForceCast cast in saved.GuardianForceCasts)
        {
            BattleCombatant actor = GetActor(cast.ActorId);
            //4842B0 decrements the GF charge WORD separately from growing the participant's ATB DWORD.
            //RestoreNativeAtb already checked the raw gauge and its HUD projection; components retain their zero-gauge invariant.
            bool retainedRawGauge = saved.NativeGraphics is not null && actor.NativeAtb is not null;
            if (saved.Outcome != BattleOutcome.Active || cast.Command.ActorId != actor.Id || cast.Command.Ability != BattleAbility.GuardianForce || !guardianForces.ContainsKey(cast.Command.ActionId) || !actor.EquipmentIds.Contains(cast.Command.ActionId) || cast.RemainingTicks > cast.InitialTicks || !guardianForceCasts.TryAdd(actor.Id, cast) || !retainedRawGauge && actor.Atb != 0)
            {
                throw new InvalidDataException("Invalid saved GF cast owner.");
            }
            NativeBattleScriptTasks tasks = saved.NativeScriptState.Tasks;
            int callbacks = Enumerable.Range(0, 16).Count(index => tasks.Allocated(index) && tasks.Slots[index].ReadDword(0) == 0x48E620 && tasks.Slots[index].ReadByte(12) == actor.NativeIndex && tasks.Slots[index].ReadByte(13) == 254 && tasks.Slots[index].ReadByte(14) == 64 + cast.Command.ActionId);
            if (callbacks != 1)
            {
                throw new InvalidDataException("Saved GF charge requires its single canonical48E620 callback record.");
            }
            actor.IsBusy = true;
        }
    }

    private void ValidateNativeGuardianInputBinding(BattleAttackCommand command, NativeBattleCommandPacket physical, int bankIndex)
    {
        BattleCombatant actor = GetActor(command.ActorId);
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, command.ActionId);
        BattleNativeAction? native = action.NativeAction;
        BattleGuardianForceVital? gf = guardianForces.GetValueOrDefault(command.ActionId);
        if (command.NativeQueuedCommand is not (3 or 254) || bankIndex != (command.NativeQueuedCommand == 3 ? 2 : 1) || command.NativeHeaderIndex is not null || command.NativeAction is not null || !command.NativeMagicTargets.IsDefaultOrEmpty || guardianForceCasts.ContainsKey(actor.Id) || !actor.IsPlayer || !actor.EquipmentIds.Contains(command.ActionId) || gf is not { Acquired: true, HitPoints: > 0 } || !action.Available || native is null || native.Command != 254 || native.ActionId != command.ActionId + 64)
        {
            throw new InvalidDataException("Saved GF input lacks its equipped living GF and original3/FE command owner.");
        }
        ushort mask = command.NativeTargetMask != 0 ? command.NativeTargetMask : checked((ushort)(1 << GetActor(command.TargetId).NativeIndex));
        if (physical.ReadWord(4) != command.ActionId + 64 || physical.ReadByte(2) != 0 || physical.ReadByte(3) != 0 || physical.ReadWord(6) != mask)
        {
            throw new InvalidDataException("Saved GF input contradicts its original action WORD, target WORD or flag bytes.");
        }
    }
}
