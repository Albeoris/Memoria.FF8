using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply FacePartyActor(int entity, FieldActorFaceParty command)
    {
        if (command.PartySlot >= 3)
        {
            throw new NotSupportedException("Native party facing requires a valid script-party slot.");
        }
        int[] party = ReadScriptParty?.Invoke() ?? throw new NotSupportedException("Native party facing requires the script-party owner.");
        if (party.Length != 3 || party.Any(character => character is < 0 or > 255))
        {
            throw new InvalidDataException("The native script-party byte identities are invalid.");
        }
        int character = party[command.PartySlot];
        if (character == 255)
        {
            return new(true);
        }
        Ff7Actor[] targets = Process.Actors.Where(actor => actor.EntityBound && actor.PartyCharacter == character).ToArray();
        if (targets.Length == 0)
        {
            return new(true);
        }
        if (targets.Length != 1)
        {
            throw new NotSupportedException("Native party facing with duplicate character bindings requires the current character-to-entity mapping owner.");
        }
        FieldHostReply reply = FaceActor(entity, new(targets[0].Entity, command.Steps, command.DirectionPolicy, command.Immediate));
        return command.Immediate ? reply with { Value = 1 } : reply;
    }
}
