using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Maps the ordered recipients of one action to temporary rows in its effect presentation.</summary>
public sealed class NativeBattleEffectContactBridge<TRecipient> where TRecipient : notnull
{
    private readonly ImmutableArray<(TRecipient Recipient, NativeBattleContactHalf Contact)> targets;

    public NativeBattleEffectContactBridge(IEnumerable<(TRecipient Recipient, NativeBattleContactHalf Contact)> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        this.targets = targets.ToImmutableArray();
        if (this.targets.IsEmpty || this.targets.Any(target => target.Recipient is null || target.Contact is null || !NativeBattleSlots.Contains(target.Contact.ActorIndex)) ||
            this.targets.Select(target => target.Recipient).Distinct().Count() != this.targets.Length ||
            this.targets.Select(target => target.Contact.ActorIndex).Distinct().Count() != this.targets.Length)
        {
            throw new InvalidDataException("An effect action requires a distinct recipient for each of its bound native contact rows.");
        }
    }

    public int Count => targets.Length;

    public (NativeBattleActionPacketsState Packets, ushort TargetMask) Prepare(byte command, byte sequence, ushort action, ushort effect)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (packets, _) = packets.Prepare(new(0, command, sequence, -1, action, effect, null));
        NativeBattlePhysicalTargets mask = default;
        foreach (var target in targets)
        {
            (packets, _) = packets.AppendContact(new(target.Contact, new(255, 0, 0, 0, 0, 0, 0)));
            mask = mask.Add(NativeBattleSlots.Decode(target.Contact.ActorIndex));
        }
        (packets, _) = packets.Publish(checked((byte)targets.Length));
        return (packets, mask.Bits);
    }

    public TRecipient Resolve(NativeBattleContactPacket packet)
    {
        packet.Validate();
        if (packet.Secondary.ActorIndex != 255)
        {
            throw new NotSupportedException("This effect bridge requires a separately represented secondary contact.");
        }
        foreach (var target in targets)
        {
            if (target.Contact == packet.Primary)
            {
                return target.Recipient;
            }
        }
        throw new NotSupportedException("The native effect published a contact outside the prepared action.");
    }
}
