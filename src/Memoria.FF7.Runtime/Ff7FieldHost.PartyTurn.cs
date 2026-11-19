using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply FacePartyActor(int entity, FieldActorFaceParty command)
    {
        Ff7Actor? target = ResolveScriptPartyActor(command.PartySlot);
        if (target is null)
        {
            return new(true);
        }
        FieldHostReply reply = FaceActor(entity, new(target.Entity, command.Steps, command.DirectionPolicy, command.Immediate));
        return command.Immediate ? reply with { Value = 1 } : reply;
    }

    private Ff7Actor? ResolveScriptPartyActor(byte partySlot)
    {
        int character;
        if (ReadScriptPartyByte is { } readByte && partySlot <= byte.MaxValue - 9)
        {
            character = readByte(partySlot);
        }
        else
        {
            if (partySlot >= 3)
            {
                throw new NotSupportedException("Native party actor lookup reads beyond its script-party bank.");
            }
            int[] party = ReadScriptParty?.Invoke() ?? throw new NotSupportedException("Native party actor lookup requires the script-party owner.");
            if (party.Length != 3 || party.Any(member => member is < 0 or > 255))
            {
                throw new InvalidDataException("The native script-party byte identities are invalid.");
            }
            character = party[partySlot];
        }
        if (character == 255)
        {
            return null;
        }
        Ff7Actor[] targets = Process.Actors.Where(actor => actor.EntityBound && actor.PartyCharacter == character).ToArray();
        if (targets.Length == 0)
        {
            return null;
        }
        if (targets.Length != 1)
        {
            throw new NotSupportedException("Native party actor lookup with duplicate character bindings requires the current character-to-entity mapping owner.");
        }
        return targets[0];
    }
}
