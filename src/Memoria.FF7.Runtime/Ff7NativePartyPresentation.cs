using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static class Ff7NativePartyPresentation
{
    public static byte[] Create(int lastSlot, IReadOnlyList<int> scriptParty, IReadOnlyList<FieldEntitySnapshot> entities, IReadOnlyList<Ff7Actor> actors)
    {
        ArgumentNullException.ThrowIfNull(scriptParty);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(actors);
        if (lastSlot is < 0 or > 255 || scriptParty.Count != 3 || scriptParty.Any(character => character is < 0 or > 8 && character != 255))
        {
            throw new InvalidDataException("Native party presentation requires its original slot range and script party.");
        }
        byte[] mask = new byte[lastSlot + 1];
        foreach (int character in scriptParty)
        {
            if (character == 255)
            {
                continue;
            }
            int entity = -1;
            for (int index = 0; index < entities.Count; index++)
            {
                if (entities[index].ModelBound && entities[index].PartyCharacter == character)
                {
                    entity = index;
                    break;
                }
            }
            Ff7Actor? actor = entity < 0 ? null : actors.LastOrDefault(actor => actor.EntityBound && actor.Entity == entity);
            if (actor is not null && (uint)actor.Slot <= lastSlot)
            {
                mask[actor.Slot] = 1;
            }
        }
        return mask;
    }
}

public sealed partial class Ff7FieldHost
{
    internal void PublishNativePartyPresentationMask(byte[] mask)
    {
        if (mask.Length == 0 || mask.Any(value => value > 1))
        {
            throw new InvalidDataException("Native party presentation contains an invalid mask publication.");
        }
        Process = Process with { NativePartyPresentationMask = mask.ToArray() };
    }
}
