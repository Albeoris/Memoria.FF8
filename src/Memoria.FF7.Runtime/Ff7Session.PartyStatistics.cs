using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public void PublishNativeMenuPartyStatistics()
    {
        Ff7PartyRecoveryResult published = Ff7NativePartyRecovery.Publish(Progress, Ff7MateriaResources.LoadRecords(installation), executable.NativeMateriaStatModifiers);
        byte flags = World is { } world ? world.ReadPersistentByte(122) : Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 122);
        byte updated = (byte)((flags & 127) | (published.Underwater ? 128 : 0));
        if (World is { } activeWorld)
        {
            activeWorld.WriteGlobalPersistentByte(122, updated);
        }
        else
        {
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 122, updated);
        }
        Progress = PreserveGuestFieldCharacter(Progress, published.Progress);
    }
}
