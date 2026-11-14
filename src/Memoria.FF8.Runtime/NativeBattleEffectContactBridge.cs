using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Maps the ordered recipients of one action to temporary rows in its effect presentation.</summary>
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
        this.targets = targets.ToImmutableArray();
        if (this.targets.IsEmpty || this.targets.Any(target => target.Recipient is null || target.Packet?.Primary is null || !NativeBattleSlots.Contains(target.Packet.Primary.ActorIndex) || target.Packet.Secondary is null || target.Packet.Secondary.ActorIndex != 255 && !NativeBattleSlots.Contains(target.Packet.Secondary.ActorIndex)) ||
            this.targets.Select(target => target.Recipient).Distinct().Count() != this.targets.Length ||
            this.targets.Select(target => target.Packet.Primary.ActorIndex).Distinct().Count() != this.targets.Length)
        {
            throw new InvalidDataException("An effect action requires a distinct recipient for each of its bound native contact rows.");
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

    public TRecipient Resolve(NativeBattleContactPacket packet)
    {
        packet.Validate();
        foreach (var target in targets)
        {
            if (target.Packet == packet)
            {
                return target.Recipient;
            }
        }
        throw new NotSupportedException("The native effect published a contact outside the prepared action.");
    }
}
