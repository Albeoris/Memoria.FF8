namespace Memoria.FF8.Runtime;

public sealed record BattlePartyOccupancyChange([property: System.Text.Json.Serialization.JsonRequired] BattleRosterSlot Slot, [property: System.Text.Json.Serialization.JsonRequired] byte OutgoingRow, [property: System.Text.Json.Serialization.JsonRequired] byte? IncomingRow)
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
        int originalBinding = Array.IndexOf(nativeParty, OutgoingRow);
        if (originalBinding >= 0)
        {
            nativeParty = nativeParty.ToArray();
            nativeParty[originalBinding] = incoming;
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

internal sealed record PartyReplacementDescriptor(BattlePartyOccupancyChange Change, string OutgoingActorId, string IncomingActorId, NativeBattleModelSlot? OriginalSlot, PartyReplacementStage Stage)
{
    internal uint Callback => Stage == PartyReplacementStage.Bind ? 0x4971F0u : 0x497270u;
}

internal sealed record NativeBattlePartyReplacementReached(PartyReplacementDescriptor Descriptor) : NativeBattlePresentationReceipt;
