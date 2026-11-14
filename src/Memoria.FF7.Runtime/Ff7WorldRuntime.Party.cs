namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldPartyChange(int Joined0 = 0, int Joined1 = 0, int Left0 = 0, int Left1 = 0);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldPartyChange? partyChange;

    internal bool RequiresMenuPartySynchronization(IReadOnlyList<int> party)
    {
        return PartyMask(party) != PartyMask(Enumerable.Range(0, 3).Select(index => (int)persistent[0x109 + index]));
    }

    public void SynchronizeMenuParty(IReadOnlyList<int> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Count != 3 || party.Any(character => character != 255 && character is < 0 or > 8))
        {
            throw new ArgumentException("FFVII world party synchronization requires three native character slots.", nameof(party));
        }
        int[] scriptParty = Enumerable.Range(0, 3).Select(index => (int)persistent[0x109 + index]).ToArray();
        uint oldMask = PartyMask(scriptParty);
        uint newMask = PartyMask(party);
        uint left = oldMask & ~newMask;
        uint joined = ~oldMask & newMask;
        if (Resources.World != 2)
        {
            int[] newCharacters = ChangedModels(joined);
            int[] oldCharacters = ChangedModels(left);
            partyChange = joined == 0 && left == 0 ? null : new(newCharacters[0], newCharacters[1], oldCharacters[0], oldCharacters[1]);
        }
        for (int index = 0; index < 3; index++)
        {
            if ((left & (1u << scriptParty[index])) != 0)
            {
                scriptParty[index] = 255;
            }
        }
        int slot = 0;
        for (int character = 0; character < 32; character++)
        {
            if ((joined & (1u << character)) == 0)
            {
                continue;
            }
            while (slot < 3 && scriptParty[slot] != 255)
            {
                slot++;
            }
            if (slot == 3)
            {
                break;
            }
            scriptParty[slot++] = character;
        }
        for (int index = 0; index < 3; index++)
        {
            persistent[0x109 + index] = checked((byte)scriptParty[index]);
        }
        int previousPlayer = playerModel;
        playerModel = ResolvePartyPlayer(scriptParty);
        if (Resources.World == 2 || previousPlayer == playerModel)
        {
            return;
        }
        if (vehicle is { RideStack.Length: > 0 })
        {
            int[] stack = vehicle.RideStack.ToArray();
            stack[0] = playerModel;
            vehicle = vehicle with { RideStack = stack };
            return;
        }
        if (controlledModel >= 0)
        {
            Ff7WorldEntity previous = RequireEntity(controlledModel);
            Machine.RenameResidentModel(previous.Model, playerModel);
            entities.Remove(previous.Model);
            entities.Add(playerModel, previous with { Model = playerModel, PresentationEpoch = previous.PresentationEpoch + 1, PresentationAnimationEpoch = previous.PresentationAnimationEpoch + 1 });
            controlledModel = playerModel;
            PresentationCameraEpoch++;
        }
    }

    private static int ResolvePartyPlayer(IEnumerable<int> party)
    {
        int[] members = party.ToArray();
        return members.Contains(0) ? 0 : members.Contains(2) ? 1 : members.Contains(8) ? 2 : 0;
    }

    private static uint PartyMask(IEnumerable<int> party)
    {
        uint mask = 0;
        foreach (int character in party)
        {
            mask |= 1u << character;
        }
        return mask;
    }

    private static int[] ChangedModels(uint mask)
    {
        List<int> changed = [];
        if ((mask & (1u << 6)) != 0)
        {
            changed.Add(0x26);
            mask &= ~(1u << 6);
        }
        for (int character = 0; character < 32 && changed.Count < 2; character++)
        {
            if ((mask & (1u << character)) != 0)
            {
                changed.Add(character + 0x20);
            }
        }
        return changed.Concat(Enumerable.Repeat(0, 2)).Take(2).ToArray();
    }
}
