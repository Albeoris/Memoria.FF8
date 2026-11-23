using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    internal bool FormationTransitionReady => Outcome == Ff7BattleOutcome.Victory && Formation.NextBattleId != -1 && state.TerminalFrames >= TerminalDuration && EconomyTerminalReady;

    private void CarryFormationState(Ff7BattleSnapshot previous)
    {
        foreach (Ff7BattleActorState actor in previous.Actors.Where(actor => actor.Actor.Slot < 3))
        {
            uint layoutConditions = Actor(actor.Actor.Slot).Actor.Conditions & 0x82U;
            Replace(actor with { Actor = actor.Actor with { Conditions = (actor.Actor.Conditions & ~0x82U) | layoutConditions } });
        }
        Ff7BattleCommandQueueState queue = previous.Queue;
        Ff7BattleQueueEntry?[] slots = queue.Slots.Select(entry => entry?.Command.ActorSlot < 4 ? entry : null).ToArray();
        byte[]? globals = previous.AiGlobalMemory?.ToArray();
        if (globals is not null)
        {
            globals[4] = Ff7NativeAtb.BattleTypeForLayout(state.Layout);
            BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x28), checked((ushort)Formation.FormationId));
            BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x2C), Formation.BattleFlags);
        }
        state = state with
        {
            InitialFormationId = previous.InitialFormationId ?? previous.FormationId,
            Queue = queue with { Slots = slots },
            Inventory = previous.Inventory.ToArray(),
            Economy = previous.Economy is null ? null : Ff7BattleEconomy.Copy(previous.Economy) with { TerminalPasses = 0 },
            AiGlobalMemory = globals,
            WithdrawnMask = previous.WithdrawnMask,
            PartyAiAliases = previous.PartyAiAliases?.ToArray() ?? state.PartyAiAliases,
            AiSaveBank1 = previous.AiSaveBank1,
            AiSaveBankDirty = previous.AiSaveBankDirty,
            GlobalStatusClock = previous.GlobalStatusClock
        };

        IReadOnlyList<Ff7BattleGuestSlotState> guests = previous.GetGuestSlotStates();
        if (guests.Count == 0)
        {
            return;
        }

        // Source item and action receipts are dated on the original encounter clock.
        state = state with
        {
            Tick = previous.Tick,
            GuestAttackUsed = previous.GuestAttackUsed,
            GuestOriginStatuses = previous.GuestOriginStatuses?.Where(row => row.HostSlot < 3).Select(row => row with { Timers = row.Timers?.ToArray() }).ToArray(),
            OriginStatusClockRemainder = previous.OriginStatusClockRemainder
        };
        foreach (Ff7BattleGuestSlotState guest in guests)
        {
            SetGuestSlotState(guest);
            int slot = guest.Binding.HostSlot;
            bool Queued(Ff7BattleQueuedCommand? command) => command is { GuestAttack: true, StatusCountdown: false } && command.ActorSlot == slot;
            bool hadGuestCommand = state.Queue.Ready.Any(Queued) || state.Queue.Slots.Any(entry => Queued(entry?.Command));
            if (hadGuestCommand)
            {
                RemoveQueuedGuestProcessActions(slot);
                Ff7BattleActorState actor = Actor(slot);
                Replace(actor with { AwaitingTurn = CanAct(actor) && actor.Gauge == ushort.MaxValue });
            }
        }
        RefreshGuestSpeedDenominator();
    }
}
