using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModulePoolTask
{
    internal NativeBattleModulePoolTask WriteOrderedContactRecipients(int offset, int originalCells, NativeBattleActionPacketsState packets, NativeBattleActionHeaderSlot header, IReadOnlyList<int>? traversal = null)
    {
        int count = header.ReadContactCount();
        if (traversal is not null && traversal.Count != count)
        {
            throw new InvalidDataException("The prescribed contact traversal must cover the whole published header.");
        }
        var recipients = ImmutableArray.CreateBuilder<(BattleModelOwner Owner, BattleContactOccurrence Occurrence)>(count);
        ImmutableArray<byte>? first = [0, 0, 0, 0];
        NativeBattleModulePoolTask task = this;
        for (int index = 0; index < count; index++)
        {
            NativeBattleContactPacket packet = packets.ReadContact(header.FirstContact.Advance(traversal?[index] ?? index));
            BattleModelOwner owner = packet.Primary.Recipient;
            BattleContactOccurrence occurrence = packet.Occurrence ?? throw new InvalidDataException("The selected contact has no retained occurrence.");
            recipients.Add((owner, occurrence));
            if (index == 0)
            {
                first = packets.ModelAliases.EncodeAddress(owner);
            }
            else if (index < originalCells)
            {
                int address = checked(offset + 4 * index);
                task = packets.ModelAliases.EncodeAddress(owner) is { } encoded ? task.WriteBytes(address, encoded.AsSpan()) : task.MarkPayloadUnknown(address, 4);
            }
        }
        task.WriteExecutionReference(offset, 4, new BattleExecutionReference.OrderedContactRecipients(recipients.MoveToImmutable()), first);
        return task;
    }
}
