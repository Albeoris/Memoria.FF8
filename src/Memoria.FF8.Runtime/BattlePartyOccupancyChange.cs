namespace Memoria.FF8.Runtime;

public sealed record BattlePartyOccupancyChange(BattleRosterSlot Slot, byte OutgoingRow, byte? IncomingRow)
{
    internal NativeCharacterVitals ApplySavedBinding(NativeCharacterVitals vitals)
    {
        if (IncomingRow is not byte incoming)
        {
            return vitals;
        }
        byte[] nativeParty = vitals.CombatParty;
        byte[] additionalParty = vitals.AdditionalCombatParty ?? [];
        var minimum = vitals.AdditionalCombatPartyMinimumCapacities;
        if (Slot.Position < NativeBattleSlots.AllyCount)
        {
            if (Slot.Position >= nativeParty.Length || nativeParty[Slot.Position] != OutgoingRow)
            {
                throw new InvalidDataException("Party replacement lost the original saved combat slot.");
            }
            nativeParty = nativeParty.ToArray();
            nativeParty[Slot.Position] = incoming;
            additionalParty = additionalParty.Where(row => row != incoming).ToArray();
            minimum = minimum?.Remove(incoming);
        }
        else
        {
            if (!additionalParty.Contains(OutgoingRow) || nativeParty.Contains(incoming))
            {
                throw new InvalidDataException("Party replacement lost the saved supplemental combat slot.");
            }
            additionalParty = additionalParty.Where(row => row != incoming).Select(row => row == OutgoingRow ? incoming : row).ToArray();
            if (minimum is not null)
            {
                byte outgoingMinimum = minimum.GetValueOrDefault(OutgoingRow);
                minimum = minimum.Remove(OutgoingRow).Remove(incoming);
                if (outgoingMinimum != 0)
                {
                    minimum = minimum.Add(incoming, outgoingMinimum);
                }
            }
        }
        return vitals with
        {
            CombatParty = nativeParty,
            AdditionalCombatParty = additionalParty.Length == 0 ? null : additionalParty,
            AdditionalCombatPartyMinimumCapacities = minimum is { Count: > 0 } ? minimum : null
        };
    }
}

internal enum PartyReplacementStage
{
    Bind,
    Reveal
}

internal sealed record PartyReplacementDescriptor(BattlePartyOccupancyChange Change, string OutgoingActorId, string IncomingActorId, PartyReplacementStage Stage)
{
    internal uint Callback => Stage == PartyReplacementStage.Bind ? 0x4971F0u : 0x497270u;
}

internal sealed record NativeBattlePartyReplacementReached(PartyReplacementDescriptor Descriptor) : NativeBattlePresentationReceipt;
