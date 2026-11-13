namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestOriginStatus(int HostSlot, uint BattleStatuses);

public sealed partial class Ff7BattleSession
{
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
            Replace(actor with { Actor = actor.Actor with { Statuses = statuses }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, statuses) });
        }
        state = state with { GuestProjectedBattleMask = Ff7GuestBattleStatuses.SupportedOriginBattle };
    }

    public ushort GetGuestOriginSavedStatuses(int slot)
    {
        Ff7BattleGuestVitalsBinding binding = GetGuestBinding(slot) ?? throw new InvalidOperationException("Origin saved statuses require an FFVIII guest binding.");
        return Ff7GuestBattleStatuses.ToOrigin(Actor(slot).Actor.Statuses, binding.OriginStatuses);
    }

    public uint GetGuestOriginBattleStatuses(int slot)
    {
        Ff7BattleGuestVitalsBinding binding = GetGuestBinding(slot) ?? throw new InvalidOperationException("Origin battle statuses require an FFVIII guest binding.");
        uint original = state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == slot)?.BattleStatuses ?? binding.OriginBattleStatuses;
        uint common = Ff7GuestBattleStatuses.ToOriginBattle(Ff7GuestBattleStatuses.SupportedHost);
        return original & ~common | Ff7GuestBattleStatuses.ToOriginBattle(Actor(slot).Actor.Statuses);
    }

    private void ApplyGuestOriginContact(int slot, Ff7BattleActionReceipt receipt)
    {
        uint changed = receipt.AddedOriginBattleStatuses | receipt.RemovedOriginBattleStatuses;
        if (changed == 0)
        {
            return;
        }
        if ((changed & ~Ff7GuestBattleStatuses.OriginMulticast) != 0 || GetGuestBinding(slot) is null)
        {
            throw new InvalidDataException("The source status contact has no compatible guest owner.");
        }
        uint next = (GetGuestOriginBattleStatuses(slot) & ~receipt.RemovedOriginBattleStatuses) | receipt.AddedOriginBattleStatuses;
        Ff7BattleGuestOriginStatus[] statuses = (state.GuestOriginStatuses ?? []).Where(value => value.HostSlot != slot).Append(new(slot, next)).OrderBy(value => value.HostSlot).ToArray();
        state = state with { GuestOriginStatuses = statuses };
    }

    private static void ValidateGuestOriginStatuses(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.GuestOriginStatuses is not { } statuses)
        {
            return;
        }
        IReadOnlyList<Ff7BattleGuestSlotState> slots = snapshot.GetGuestSlotStates();
        if (statuses.Any(value => value is null) || statuses.Select(value => value.HostSlot).Distinct().Count() != statuses.Length || statuses.Any(value => !slots.Any(slot => slot.Binding.HostSlot == value.HostSlot)))
        {
            throw new InvalidDataException("The saved origin statuses have no unique guest identity.");
        }
    }
}
