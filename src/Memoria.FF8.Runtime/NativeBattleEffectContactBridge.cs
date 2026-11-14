using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Maps the ordered contact occurrences of one action to recipients and temporary model rows.</summary>
public sealed class NativeBattleEffectContactBridge<TRecipient> where TRecipient : notnull
{
    private readonly ImmutableArray<(TRecipient Recipient, NativeBattleContactPacket Packet)> targets;

    public NativeBattleEffectContactBridge(IEnumerable<(TRecipient Recipient, NativeBattleContactHalf Contact)> targets)
        : this((targets ?? throw new ArgumentNullException(nameof(targets))).Select(target => (target.Recipient, new NativeBattleContactPacket(target.Contact, new(255, 0, 0, 0, 0, 0, 0)))))
    {
    }

    public NativeBattleEffectContactBridge(IEnumerable<(TRecipient Recipient, NativeBattleContactPacket Packet)> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        this.targets = targets.Take(NativeBattleActionPacketsState.ContactCapacity + 1).ToImmutableArray();
        if (this.targets.IsEmpty || this.targets.Length > NativeBattleActionPacketsState.ContactCapacity)
        {
            throw new InvalidDataException("An effect action requires a nonempty contact stream within the native packet capacity.");
        }

        Dictionary<TRecipient, byte> rowsByRecipient = new();
        Dictionary<byte, TRecipient> recipientsByRow = new();
        foreach ((TRecipient recipient, NativeBattleContactPacket packet) in this.targets)
        {
            if (recipient is null || packet is null)
            {
                throw new InvalidDataException("An effect contact requires a recipient and a native packet.");
            }
            packet.Validate();
            byte row = packet.Primary.ActorIndex;
            if (rowsByRecipient.TryGetValue(recipient, out byte previousRow) && previousRow != row ||
                recipientsByRow.TryGetValue(row, out TRecipient? previousRecipient) && !EqualityComparer<TRecipient>.Default.Equals(previousRecipient, recipient))
            {
                throw new InvalidDataException("An effect action requires a one-to-one binding between recipients and native model rows.");
            }
            rowsByRecipient[recipient] = row;
            recipientsByRow[row] = recipient;
        }
    }

    public int Count => targets.Length;

    public (NativeBattleActionPacketsState Packets, ushort TargetMask) Prepare(byte command, byte sequence, ushort action, ushort effect, byte source = 0)
    {
        if (targets.Any(target => target.Packet.Secondary.ActorIndex != 255 && target.Packet.Secondary.ActorIndex != source))
        {
            throw new InvalidDataException("The effect action's secondary contact requires its bound source row.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (packets, _) = packets.Prepare(new(source, command, sequence, -1, action, effect, null));
        NativeBattlePhysicalTargets mask = default;
        foreach (var target in targets)
        {
            (packets, _) = packets.AppendContact(target.Packet);
            mask = mask.Add(NativeBattleSlots.Decode(target.Packet.Primary.ActorIndex));
        }
        (packets, _) = packets.Publish(checked((byte)targets.Length));
        return (packets, mask.Bits);
    }

    public TRecipient Resolve(NativeBattleContactPacket packet) => ResolveOccurrence(packet).Recipient;

    public (int Index, TRecipient Recipient) ResolveOccurrence(NativeBattleContactPacket packet)
    {
        packet.Validate();
        if (packet.PhysicalContactIndex is int physicalIndex)
        {
            if ((uint)physicalIndex >= (uint)targets.Length || targets[physicalIndex].Packet != packet)
            {
                throw new NotSupportedException("The native effect published a contact that does not match its prepared occurrence.");
            }
            return (physicalIndex, targets[physicalIndex].Recipient);
        }

        int match = -1;
        for (int index = 0; index < targets.Length; index++)
        {
            if (targets[index].Packet != packet)
            {
                continue;
            }
            if (match >= 0)
            {
                throw new NotSupportedException("The native effect must identify its occurrence when multiple prepared contacts have the same payload.");
            }
            match = index;
        }
        if (match < 0)
        {
            throw new NotSupportedException("The native effect published a contact outside the prepared action.");
        }
        return (match, targets[match].Recipient);
    }
}
