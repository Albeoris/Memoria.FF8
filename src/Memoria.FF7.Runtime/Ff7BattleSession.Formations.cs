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
    }
}
