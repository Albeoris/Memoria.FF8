namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestOriginStatus(int HostSlot, uint BattleStatuses)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public short[]? Timers { get; init; }
}

public sealed partial class Ff7BattleSession
{
    private Func<Ff7BattleDamageActor, Ff7BattleDamageActor>? originStatusStats;

    public void BindOriginStatusStats(Func<Ff7BattleDamageActor, Ff7BattleDamageActor> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        if (originStatusStats is not null)
        {
            throw new InvalidOperationException("The battle already owns its origin status stat rules.");
        }
        originStatusStats = resolve;
    }

    private Ff7BattleDamageActor ProjectGuestSourceStatusStats(int slot, Ff7BattleDamageActor stats)
    {
        if (originStatusStats is null)
        {
            return stats;
        }
        return originStatusStats(stats with { OriginSavedStatuses = GetOriginSavedStatuses(slot), OriginBattleStatuses = GetOriginBattleStatuses(slot) });
    }

    private void RestoreGuestStatusProjection()
    {
        const uint originalProjection = 0x6F;
        uint added = Ff7GuestBattleStatuses.SupportedOriginBattle & ~(state.GuestProjectedBattleMask ?? originalProjection);
        foreach (Ff7BattleGuestSlotState guest in state.GetGuestSlotStates())
        {
            int slot = guest.Binding.HostSlot;
            uint origin = state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == slot)?.BattleStatuses ?? guest.Binding.OriginBattleStatuses;
            Ff7BattleActorState actor = Actor(slot);
            uint statuses = actor.Actor.Statuses | Ff7GuestBattleStatuses.ToHostStatuses(0, origin & added);
            Ff7BattleParameterOperation? parameter = actor.ParameterStatus;
            if (!actor.ParameterStatusInitialized && parameter is null && (origin & Ff7GuestBattleStatuses.OriginDefenseReduction) != 0)
            {
                parameter = new(Ff7GuestBattleStatuses.OriginDefenseReduction, 32, 100, 100);
            }
            Replace(actor with { Actor = actor.Actor with { Statuses = statuses }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, statuses), ParameterStatus = parameter, ParameterStatusInitialized = true });
        }
        state = state with { GuestProjectedBattleMask = Ff7GuestBattleStatuses.SupportedOriginBattle };
    }

    public ushort GetGuestOriginSavedStatuses(int slot)
    {
        if (GetGuestBinding(slot) is null)
        {
            throw new InvalidOperationException("Origin saved statuses require an FFVIII guest binding.");
        }
        return GetOriginSavedStatuses(slot);
    }

    public ushort GetOriginSavedStatuses(int slot)
    {
        Ff7BattleActorState actor = Actor(slot);
        ushort original = GetGuestBinding(slot)?.OriginStatuses ?? 0;
        ushort owned = actor.OriginSavedStatuses ?? (ushort)(original & Ff7OriginSavedStatusRules.Supported);
        return Ff7GuestBattleStatuses.ToOrigin(actor.Actor.Statuses, (ushort)((original & ~Ff7OriginSavedStatusRules.Supported) | owned));
    }

    private Ff7BattleActorState NormalizeOriginKnockout(Ff7BattleActorState actor)
    {
        if ((actor.Actor.Statuses & Ff7NativeBattleActionResolver.Death) == 0)
        {
            return actor;
        }
        const ushort savedRemoval = 0x37E;
        const uint battleRemoval = 0x38E7FFF;
        ushort owned = (ushort)((actor.OriginSavedStatuses ?? GetOriginSavedStatuses(actor.Actor.Slot)) & ~savedRemoval & Ff7OriginSavedStatusRules.Supported);
        Ff7BattleParameterOperation? parameter = actor.ParameterStatus;
        if (parameter is not null && (parameter.SourceStatusMask & battleRemoval) != 0)
        {
            parameter = null;
        }
        if (state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == actor.Actor.Slot) is not null || GetGuestBinding(actor.Actor.Slot) is not null)
        {
            uint current = state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == actor.Actor.Slot)?.BattleStatuses ?? GetGuestBinding(actor.Actor.Slot)?.OriginBattleStatuses ?? 0;
            uint remaining = current & ~RemovableOriginBattleStatuses(actor.Actor.Slot, battleRemoval);
            if (current != remaining)
            {
                StoreOriginBattleStatuses(actor.Actor.Slot, remaining, current & ~remaining);
            }
        }
        return actor with { OriginSavedStatuses = owned, ParameterStatus = parameter, ParameterStatusInitialized = true };
    }

    public uint GetGuestOriginBattleStatuses(int slot)
    {
        if (GetGuestBinding(slot) is null)
        {
            throw new InvalidOperationException("Origin battle statuses require an FFVIII guest binding.");
        }
        return GetOriginBattleStatuses(slot);
    }

    public uint GetOriginBattleStatuses(int slot)
    {
        uint original = state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == slot)?.BattleStatuses ?? GetGuestBinding(slot)?.OriginBattleStatuses ?? 0;
        uint common = Ff7GuestBattleStatuses.ToOriginBattle(Ff7GuestBattleStatuses.SupportedHost);
        Ff7BattleActorState actor = Actor(slot);
        return original & ~(common | Ff7GuestBattleStatuses.OriginDefenseReduction) | Ff7GuestBattleStatuses.ToOriginBattle(actor.Actor.Statuses) | (actor.ParameterStatus?.SourceStatusMask ?? 0);
    }

    private void ApplyGuestOriginContact(int slot, Ff7BattleActionReceipt receipt)
    {
        uint changed = receipt.AddedOriginBattleStatuses | receipt.RemovedOriginBattleStatuses;
        if (changed == 0)
        {
            return;
        }
        uint allowedRemoval = Ff7GuestBattleStatuses.SupportedOwnedOriginBattle | ((receipt.AddedOriginBattleStatuses & Ff7GuestBattleStatuses.OriginAura) != 0 ? Ff7GuestBattleStatuses.OriginCurse : 0);
        if ((receipt.AddedOriginBattleStatuses & ~Ff7GuestBattleStatuses.SupportedOwnedOriginBattle) != 0 || (receipt.RemovedOriginBattleStatuses & ~allowedRemoval) != 0)
        {
            throw new InvalidDataException("The source status contact contains an unsupported operation.");
        }
        uint removed = RemovableOriginBattleStatuses(slot, receipt.RemovedOriginBattleStatuses);
        uint next = (GetOriginBattleStatuses(slot) & ~removed) | receipt.AddedOriginBattleStatuses;
        StoreOriginBattleStatuses(slot, next, removed, receipt.AddedOriginBattleStatuses);
    }

    private static void ValidateGuestOriginStatuses(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.GuestOriginStatuses is not { } statuses)
        {
            return;
        }
        if (statuses.Any(value => value is null) || statuses.Select(value => value.HostSlot).Distinct().Count() != statuses.Length || statuses.Any(value => !snapshot.Actors.Any(actor => actor.Actor.Slot == value.HostSlot) || value.Timers is { Length: not 14 }))
        {
            throw new InvalidDataException("The saved origin statuses have no unique actor identity.");
        }
    }
}
