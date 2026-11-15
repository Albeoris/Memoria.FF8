using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Maps the ordered contact occurrences of one action to recipients and bound model owners.</summary>
public sealed class NativeBattleEffectContactBridge<TRecipient> where TRecipient : notnull
{
    private readonly ImmutableArray<(TRecipient Recipient, NativeBattleContactPacket Packet)> targets;
    private readonly BattleModelRoster roster;
    private readonly BattleContactCursor firstContact;

    public NativeBattleEffectContactBridge(IEnumerable<(TRecipient Recipient, NativeBattleContactHalf Contact)> targets, BattleModelRoster roster, BattleContactCursor firstContact = default)
        : this((targets ?? throw new ArgumentNullException(nameof(targets))).Select(target => (target.Recipient, new NativeBattleContactPacket(target.Contact, null))), roster, firstContact)
    {
    }

    public NativeBattleEffectContactBridge(IEnumerable<(TRecipient Recipient, NativeBattleContactPacket Packet)> targets, BattleModelRoster roster, BattleContactCursor firstContact = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(roster);
        this.roster = roster;
        this.firstContact = firstContact;
        this.targets = targets.ToImmutableArray();
        if (this.targets.IsEmpty)
        {
            throw new InvalidDataException("An effect action requires a nonempty contact stream.");
        }
        _ = firstContact.Advance(this.targets.Length);

        Dictionary<TRecipient, BattleModelOwner> ownersByRecipient = new();
        Dictionary<BattleModelOwner, TRecipient> recipientsByOwner = new();
        foreach ((TRecipient recipient, NativeBattleContactPacket packet) in this.targets)
        {
            if (recipient is null || packet is null)
            {
                throw new InvalidDataException("An effect contact requires a recipient and a native packet.");
            }
            ValidatePacket(packet);
            BattleModelOwner owner = packet.Primary.Recipient;
            if (ownersByRecipient.TryGetValue(recipient, out BattleModelOwner? previousOwner) && previousOwner != owner ||
                recipientsByOwner.TryGetValue(owner, out TRecipient? previousRecipient) && !EqualityComparer<TRecipient>.Default.Equals(previousRecipient, recipient))
            {
                throw new InvalidDataException("An effect action requires a one-to-one binding between recipients and their model owners.");
            }
            ownersByRecipient[recipient] = owner;
            recipientsByOwner[owner] = recipient;
        }
    }

    public int Count => targets.Length;

    public (NativeBattleActionPacketsState Packets, BattleModelTargets Targets) Prepare(NativeBattleActionPacketsState packets, byte command, byte sequence, ushort action, ushort effect, BattleModelOwner source)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(source);
        if (firstContact != default || packets.ActionCount != 0 || packets.ContactCount != 0 || !packets.Roster.Owners.SequenceEqual(roster.Owners))
        {
            throw new InvalidOperationException("Preparing an effect requires its empty packet owner and a contact stream starting at zero.");
        }
        if (targets.Any(target => target.Packet.Secondary is { } secondary && secondary.Recipient != source))
        {
            throw new InvalidDataException("The effect action's secondary contact requires its bound source model.");
        }
        (packets, _) = packets.Prepare(new(source, command, sequence, -1, action, effect, null));
        packets = packets.AppendContacts(targets.Select(target => target.Packet));
        BattleModelTargets selected = BattleModelTargets.From(targets.Select(target => target.Packet.Primary.Recipient));
        (packets, _) = packets.Publish(targets.Length);
        return (packets, selected);
    }

    public TRecipient Resolve(NativeBattleContactPacket packet) => ResolveOccurrence(packet).Recipient;

    public (int Index, TRecipient Recipient) ResolveOccurrence(NativeBattleContactPacket packet)
    {
        ValidatePacket(packet);
        if (packet.Occurrence is { } occurrence)
        {
            int relative = occurrence.Ordinal - firstContact.Ordinal;
            if ((uint)relative >= (uint)targets.Length || targets[relative].Packet != packet)
            {
                throw new NotSupportedException("The native effect published a contact that does not match its prepared occurrence.");
            }
            return (relative, targets[relative].Recipient);
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
        ArgumentNullException.ThrowIfNull(packet);
        packet.Validate();
        RequireOwner(packet.Primary);
        if (packet.Secondary is { } secondary)
        {
            RequireOwner(secondary);
        }
    }

    private void RequireOwner(NativeBattleContactHalf half)
    {
        if (!roster.Contains(half.Recipient) || half.CoverSource is { } cover && !roster.Contains(cover))
        {
            throw new InvalidDataException("An effect contact refers to a model outside its roster.");
        }
    }
}
