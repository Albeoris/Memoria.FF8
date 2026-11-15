using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleStolenAssets(ushort Gil = 0, ushort Item = ushort.MaxValue);

public sealed partial class Ff7BattleSession
{
    private bool EconomyTerminalReady => state.Economy is { TerminalPasses: 16 } economy && economy.Queues.Take(2).All(queue => queue.Read == queue.Write) && economy.RegisteredReturns.All(item => item is null) && economy.AcquisitionRead == economy.AcquisitionWrite;

    private void AdvanceTerminalEconomyPresentation()
    {
        Ff7BattleEscapeInput refresh = Ff7NativeBattleEscape.UpdatePresentation(state.Escape, false, false, false, false, true, random);
        state = state with { Escape = refresh.State };
        PublishEconomyHeldItems();
        if (state.Messages is { } messages)
        {
            state = state with { Messages = Ff7NativeBattleMessage.AdvancePresentation(messages, liveFrame: true, paused: false) };
        }
    }

    private void AdvanceEconomyTerminalPass()
    {
        Ff7BattleEconomyState economy = state.Economy!;
        if (economy.TerminalPasses < 16)
        {
            state = state with { Economy = Ff7BattleEconomy.FlushReturns(economy) with { TerminalPasses = (byte)(economy.TerminalPasses + 1) } };
        }
    }

    private uint BattleGil => BinaryPrimitives.ReadUInt32LittleEndian(state.AiGlobalMemory!.AsSpan(0x38));

    private void PublishBattleGil(uint gil)
    {
        byte[] globals = state.AiGlobalMemory!.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(globals.AsSpan(0x38), gil);
        state = state with { AiGlobalMemory = globals };
    }

    private void RestoreEconomyState()
    {
        Ff7BattleEconomyState economy = state.Economy ?? Ff7BattleEconomy.Create(Ff7NativeBattleInventory.Create(state.Inventory, kernel));
        state = state with { Economy = economy, Inventory = Ff7NativeBattleInventory.Project(economy.Inventory) };
    }

    private static void ValidateEconomySnapshot(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.Economy is not { } economy)
        {
            if (snapshot.Actors.Any(actor => actor.StolenAssets is not null))
            {
                throw new InvalidDataException("Saved stolen assets require their battle economy owner.");
            }
            return;
        }
        Ff7BattleEconomy.Validate(economy);
        if (snapshot.Actors.Any(actor => actor.StolenAssets is { Item: not ushort.MaxValue and > 511 }))
        {
            throw new InvalidDataException("Saved stolen item identity exceeds its native inventory namespace.");
        }
        Ff7InventorySlot[] working = Ff7NativeBattleInventory.Project(economy.Inventory);
        Ff7InventorySlot[] declared = Ff7NativeInventory.Normalize(snapshot.Inventory);
        if (!working.SequenceEqual(declared))
        {
            throw new InvalidDataException("Saved battle economy conflicts with its inventory or physical actors.");
        }
        foreach (Ff7BattleEconomyQueue queue in economy.Queues)
        {
            int cursor = queue.Read;
            while (cursor != queue.Write)
            {
                Ff7BattleEconomyCommand command = queue.Slots[cursor]!;
                if (!snapshot.Actors.Any(actor => actor.Actor.Slot == command.Source))
                {
                    throw new InvalidDataException("Saved battle economy references an unallocated physical actor.");
                }
                cursor = (cursor + 1) & 127;
            }
        }
    }

    private void ValidateTheftContact(Ff7BattleAttack? record, int targetSlot)
    {
        if (targetSlot < 3 && record is { AdditionalEffect: 0x06 or 0x07 } && systemMessages is null)
        {
            throw new NotSupportedException("FFVII theft requires its original battle system message resources.");
        }
    }

    private void ApplyTheftContact(int sourceSlot, int targetSlot, byte? effect)
    {
        if (effect is null || targetSlot >= 3)
        {
            return;
        }
        Ff7BattleActorState source = Actor(sourceSlot);
        Ff7BattleStolenAssets assets = source.StolenAssets ?? new();
        if (effect == 0x06)
        {
            ushort amount = unchecked((ushort)((source.Actor.Level ?? Stats(source).Level) * 20));
            amount = (ushort)Math.Min(amount, BattleGil);
            if (amount != 0 && assets.Gil + amount < ushort.MaxValue)
            {
                Replace(source with { StolenAssets = assets with { Gil = (ushort)(assets.Gil + amount) } });
                PublishBattleGil(BattleGil - amount);
                PublishEconomyNumberMessage(Ff7BattleSystemMessages.GilStolen, amount);
            }
        }
        else if (effect == 0x07 && assets.Item == ushort.MaxValue)
        {
            state = state with { Economy = Ff7BattleEconomy.Enqueue(state.Economy!, 0, new(0x0B, checked((byte)sourceSlot), 0)) };
        }
        else if (effect != 0x07)
        {
            throw new NotSupportedException($"FFVII theft effect {effect:X2} has no economy handler.");
        }
    }

    private void AdvanceEconomyLogic()
    {
        Ff7BattleEconomyState economy = state.Economy!;
        while (true)
        {
            economy = Ff7BattleEconomy.Dequeue(economy, 0, out Ff7BattleEconomyCommand? command);
            if (command is null)
            {
                break;
            }
            if (command.Command == 0x0B)
            {
                Ff7BattleInventorySlot[] inventory = Ff7NativeBattleInventory.SelectStealable(economy.Inventory, state.Escape.CachedRoll, out ushort item);
                economy = economy with { Inventory = inventory };
                if (item != ushort.MaxValue)
                {
                    economy = Ff7BattleEconomy.Enqueue(economy, 1, new(2, command.Source, item));
                }
            }
            else if (command.Command == 3)
            {
                economy = Ff7BattleEconomy.RegisterReturn(economy, command.Argument);
            }
            else
            {
                throw new NotSupportedException($"FFVII economy queue0 command {command.Command:X2} is unsupported.");
            }
        }
        while (true)
        {
            economy = Ff7BattleEconomy.TakeAcquisition(economy, out ushort? item);
            if (item is null)
            {
                break;
            }
            economy = economy with { Inventory = Ff7NativeBattleInventory.Acquire(economy.Inventory, item.Value, kernel) };
        }
        state = state with { Economy = economy, Inventory = Ff7NativeBattleInventory.Project(economy.Inventory) };
    }

    private void PublishEconomyHeldItems()
    {
        Ff7BattleEconomyState economy = state.Economy!;
        while (true)
        {
            economy = Ff7BattleEconomy.Dequeue(economy, 1, out Ff7BattleEconomyCommand? command);
            if (command is null)
            {
                break;
            }
            if (command.Command != 2)
            {
                throw new NotSupportedException($"FFVII economy queue1 command {command.Command:X2} is unsupported.");
            }
            Ff7BattleActorState actor = Actor(command.Source);
            Replace(actor with { StolenAssets = (actor.StolenAssets ?? new()) with { Item = command.Argument } });
            economy = Ff7BattleEconomy.Enqueue(economy, 2, new(0x14, command.Source, command.Argument));
        }
        state = state with { Economy = economy };
    }

    private void CompleteEconomyStatusPhase()
    {
        Ff7BattleEconomyState economy = state.Economy!;
        while (true)
        {
            economy = Ff7BattleEconomy.Dequeue(economy, 2, out Ff7BattleEconomyCommand? command);
            if (command is null)
            {
                break;
            }
            if (command.Command != 0x14)
            {
                throw new NotSupportedException($"FFVII economy queue2 command {command.Command:X2} is unsupported.");
            }
            PublishEconomyItemMessage(Ff7BattleSystemMessages.ItemStolen, command.Argument);
        }
        foreach (Ff7BattleActorState actor in state.Actors)
        {
            int bit = 1 << actor.Actor.Slot;
            uint added = actor.Actor.Statuses & ~actor.Actor.PreviousStatuses.GetValueOrDefault(actor.Actor.Statuses);
            if ((added & Ff7NativeBattleActionResolver.Death) == 0 || actor.StolenAssets is not { } assets || (state.WithdrawnMask.GetValueOrDefault() & bit) != 0)
            {
                continue;
            }
            Replace(actor with { StolenAssets = null });
            if (assets.Gil != 0)
            {
                PublishBattleGil(unchecked(BattleGil + assets.Gil));
                PublishEconomyNumberMessage(Ff7BattleSystemMessages.GilReceived, assets.Gil);
            }
            if (assets.Item != ushort.MaxValue)
            {
                economy = Ff7BattleEconomy.Enqueue(economy, 0, new(3, checked((byte)actor.Actor.Slot), assets.Item));
                PublishEconomyItemMessage(Ff7BattleSystemMessages.ItemReceived, assets.Item);
            }
        }
        foreach (Ff7BattleActorState actor in state.Actors.ToArray())
        {
            Replace(actor with { Actor = actor.Actor with { PreviousStatuses = actor.Actor.Statuses } });
        }
        state = state with { Economy = Ff7BattleEconomy.FlushReturns(economy) };
    }

    private void PublishEconomyItemMessage(int index, ushort item)
    {
        string text = (systemMessages ?? throw new NotSupportedException("FFVII economy notification requires its original message resources.")).FormatItemMessage(index, item);
        state = state with { Messages = Ff7NativeBattleMessage.Enqueue(state.Messages ?? Ff7BattleMessageState.Empty, text) };
    }

    private void PublishEconomyNumberMessage(int index, ushort number)
    {
        string text = (systemMessages ?? throw new NotSupportedException("FFVII economy notification requires its original message resources.")).FormatNumberMessage(index, number);
        state = state with { Messages = Ff7NativeBattleMessage.Enqueue(state.Messages ?? Ff7BattleMessageState.Empty, text) };
    }
}
