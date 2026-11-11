using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public void PublishNativeMenuPartyStatistics()
    {
        PublishNativeMenuPartyStatistics(Progress);
    }

    private void PublishNativeMenuPartyStatistics(Ff7Progress candidate, int? changedCharacterId = null, byte materiaFlags = 0)
    {
        Ff7PartyRecoveryResult published = Ff7NativePartyRecovery.Publish(candidate, Ff7MateriaResources.LoadRecords(installation), executable.NativeMateriaStatModifiers, GuestFieldVitals?.StoryCharacters);
        Ff7Progress adjusted = published.Progress;
        if (changedCharacterId is int characterId)
        {
            Ff7CharacterState selected = adjusted.Characters.Single(character => character.Id == characterId);
            adjusted = candidate with { Characters = candidate.Characters.Select(character => character.Id == characterId ? selected : character).ToArray() };
        }
        byte flags = World is { } world ? world.ReadPersistentByte(122) : Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 122);
        byte updated = (byte)((flags & 127) | (published.Underwater ? 128 : 0));
        if (World is { } activeWorld)
        {
            activeWorld.WriteGlobalPersistentByte(122, updated);
            if (materiaFlags != 0)
            {
                activeWorld.WriteGlobalPersistentByte(75, (byte)(activeWorld.ReadPersistentByte(75) | materiaFlags));
            }
        }
        else
        {
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 122, updated);
            if (materiaFlags != 0)
            {
                byte obtained = Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 75);
                Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 75, (byte)(obtained | materiaFlags));
            }
        }
        Progress = adjusted;
    }
}
