using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Maps the ordered contact occurrences of one action to recipients and temporary model rows.</summary>
public sealed class NativeBattleEffectContactBridge<TRecipient> where TRecipient : notnull
{
    private readonly ImmutableArray<(TRecipient Recipient, NativeBattleContactPacket Packet)> targets;
    private readonly BattleModelRoster? roster;
    private readonly int firstContactIndex;

    public NativeBattleEffectContactBridge(IEnumerable<(TRecipient Recipient, NativeBattleContactHalf Contact)> targets, BattleModelRoster? roster = null, int firstContactIndex = 0)
        : this((targets ?? throw new ArgumentNullException(nameof(targets))).Select(target => (target.Recipient, new NativeBattleContactPacket(target.Contact, new(255, 0, 0, 0, 0, 0, 0)))), roster, firstContactIndex)
    {
    }

    public NativeBattleEffectContactBridge(IEnumerable<(TRecipient Recipient, NativeBattleContactPacket Packet)> targets, BattleModelRoster? roster = null, int firstContactIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(targets);
        roster?.Validate();
        this.roster = roster;
        int capacity = roster is null ? NativeBattleActionPacketsState.ContactCapacity : NativeBattleContactAddress.MaximumRowCount;
        if ((uint)firstContactIndex >= (uint)capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(firstContactIndex));
        }
        this.firstContactIndex = firstContactIndex;
        int remainingCapacity = capacity - firstContactIndex;
        this.targets = targets.Take(remainingCapacity + 1).ToImmutableArray();
        if (this.targets.IsEmpty || this.targets.Length > remainingCapacity)
        {
            throw new InvalidDataException("An effect action requires a nonempty contact stream within its packet capacity.");
        }

        Dictionary<TRecipient, byte> rowsByRecipient = new();
        Dictionary<byte, TRecipient> recipientsByRow = new();
        foreach ((TRecipient recipient, NativeBattleContactPacket packet) in this.targets)
        {
            if (recipient is null || packet is null)
            {
                throw new InvalidDataException("An effect contact requires a recipient and a native packet.");
            }
            ValidatePacket(packet);
            byte row = packet.Primary.Recipient;
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
        if (roster is not null)
        {
            throw new InvalidOperationException("An effect bridge with a presentation roster requires PrepareHosted.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (NativeBattleActionPacketsState prepared, BattleModelTargets selected) = Prepare(packets, command, sequence, action, effect, source);
        return (prepared, selected.NativePhysicalBits);
    }

    public (NativeBattleActionPacketsState Packets, BattleModelTargets Targets) PrepareHosted(byte command, byte sequence, ushort action, ushort effect, byte source = 0)
    {
        BattleModelRoster presentationRoster = roster ?? throw new InvalidOperationException("Preparing hosted effects requires an explicit presentation roster.");
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(presentationRoster, Math.Max(NativeBattleActionPacketsState.ContactCapacity, targets.Length));
        return Prepare(packets, command, sequence, action, effect, source);
    }

    private (NativeBattleActionPacketsState Packets, BattleModelTargets Targets) Prepare(NativeBattleActionPacketsState packets, byte command, byte sequence, ushort action, ushort effect, byte source)
    {
        if (firstContactIndex != 0)
        {
            throw new InvalidOperationException("Preparing a new effect bank requires a contact bridge starting at zero.");
        }
        if (targets.Any(target => target.Packet.Secondary.Recipient != 255 && target.Packet.Secondary.Recipient != source))
        {
            throw new InvalidDataException("The effect action's secondary contact requires its bound source row.");
        }
        (packets, _) = packets.Prepare(new(source, command, sequence, -1, action, effect, null));
        packets = packets.AppendContacts(targets.Select(target => target.Packet));
        BattleModelTargets selected = default;
        foreach (var target in targets)
        {
            selected = selected.Add(target.Packet.Primary.Recipient);
        }
        (packets, _) = packets.Publish(targets.Length);
        return (packets, selected);
    }

    public TRecipient Resolve(NativeBattleContactPacket packet) => ResolveOccurrence(packet).Recipient;

    public (int Index, TRecipient Recipient) ResolveOccurrence(NativeBattleContactPacket packet)
    {
        ValidatePacket(packet);
        if (packet.PhysicalContactIndex is int physicalIndex)
        {
            if (physicalIndex < firstContactIndex || physicalIndex >= firstContactIndex + targets.Length ||
                targets[physicalIndex - firstContactIndex].Packet != packet)
            {
                throw new NotSupportedException("The native effect published a contact that does not match its prepared occurrence.");
            }
            int index = physicalIndex - firstContactIndex;
            return (index, targets[index].Recipient);
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

    private void ValidatePacket(NativeBattleContactPacket packet)
    {
        packet.Validate(roster?.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount);
        if (roster is { } presentationRoster)
        {
            _ = presentationRoster[packet.Primary.Recipient];
            if (packet.Secondary.Recipient != byte.MaxValue)
            {
                _ = presentationRoster[packet.Secondary.Recipient];
            }
        }
    }
}
