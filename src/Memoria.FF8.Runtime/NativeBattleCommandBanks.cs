using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCommandLocation(byte Bank, byte Slot);
public sealed record NativeBattleCommandLink([property: JsonRequired] byte Previous, [property: JsonRequired] byte Next, [property: JsonRequired] byte State, [property: JsonRequired] byte? Retained3);
public sealed record NativeBattleCommandPacket([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ushort UnknownByteMask)
{
    public byte ReadByte(int offset)
    {
        if ((uint)offset >= 12 || (UnknownByteMask & (1 << offset)) != 0)
        {
            throw new NotSupportedException($"Native queued command byte{offset} has no observed physical writer.");
        }
        return Bytes[offset];
    }

    public ushort ReadWord(int offset) => (ushort)(ReadByte(offset) | ReadByte(offset + 1) << 8);
}
public sealed record NativeBattleCommandBank([property: JsonRequired] ImmutableArray<NativeBattleCommandPacket> Packets, [property: JsonRequired] ImmutableArray<NativeBattleCommandLink> Links, [property: JsonRequired] byte Tail)
{
    [JsonRequired]
    public ImmutableArray<BattleAttackCommand?> LogicalCommands { get; init; } = ImmutableArray.CreateRange(new BattleAttackCommand?[22]);
}

/// <summary>48C740/482BD0's three eleven-entry banks: callback0, limit1 and ordinary2. Each entry owns two12-byte packets.</summary>
public sealed record NativeBattleCommandBanks([property: JsonRequired] ImmutableArray<NativeBattleCommandBank> Banks)
{
    public static NativeBattleCommandBanks Initialize()
    {
        NativeBattleCommandPacket Empty()
        {
            byte[] bytes = new byte[12];
            bytes[0] = 255;
            //4846E0 sets actorFF and clears bytes6..11; command/flags/action retain their previous storage.
            return new(bytes.ToImmutableArray(), 0x3E);
        }
        return new(Enumerable.Range(0, 3).Select(_ => new NativeBattleCommandBank(Enumerable.Range(0, 22).Select(_ => Empty()).ToImmutableArray(), Enumerable.Range(0, 11).Select(_ => new NativeBattleCommandLink(0, 0, 0, null)).ToImmutableArray(), 255)).ToImmutableArray());
    }

    public (NativeBattleCommandBanks State, NativeBattleCommandLocation Location) Allocate(byte bankIndex)
    {
        if (bankIndex >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(bankIndex));
        }
        NativeBattleCommandBank bank = Banks[bankIndex];
        int slot = -1;
        for (int index = 0; index < 11; index++)
        {
            if (bank.Links[index].Previous == 0 && bank.Links[index].Next == 0)
            {
                slot = index;
                break;
            }
        }
        if (slot < 0)
        {
            //The original falls back to slot0 and corrupts an occupied chain; never cross that unsupported capacity boundary.
            throw new InvalidOperationException("Native command bank exhausted its eleven physical entries.");
        }
        ImmutableArray<NativeBattleCommandLink> links = bank.Links.SetItem(slot, bank.Links[slot] with { Previous = bank.Tail, Next = 255, State = 0 });
        if (bank.Tail != 255)
        {
            links = links.SetItem(bank.Tail, links[bank.Tail] with { Next = checked((byte)slot) });
        }
        return (this with { Banks = Banks.SetItem(bankIndex, bank with { Links = links, Tail = checked((byte)slot) }) }, new(bankIndex, checked((byte)slot)));
    }

    public NativeBattleCommandBanks WriteCommand(NativeBattleCommandLocation location, byte subslot, byte actor, byte command, byte flags2, byte flags3, ushort action, IReadOnlyList<ushort> targets)
    {
        ValidateLocation(location);
        if (subslot >= 2 || actor >= 7 || targets.Count > 3)
        {
            throw new InvalidDataException("Native queued command exceeds its physical actor/subcommand/target fields.");
        }
        NativeBattleCommandBank bank = Banks[location.Bank];
        int index = 2 * location.Slot + subslot;
        NativeBattleCommandPacket packet = bank.Packets[index];
        byte[] bytes = packet.Bytes.ToArray();
        bytes[0] = actor;
        bytes[1] = command;
        bytes[2] = flags2;
        bytes[3] = flags3;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), action);
        ushort unknown = (ushort)(packet.UnknownByteMask & ~0x3F);
        for (int target = 0; target < targets.Count; target++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6 + 2 * target), targets[target]);
            unknown &= (ushort)~(3 << (6 + 2 * target));
        }
        return this with { Banks = Banks.SetItem(location.Bank, bank with { Packets = bank.Packets.SetItem(index, new(bytes.ToImmutableArray(), unknown)) }) };
    }

    public (NativeBattleCommandBanks State, NativeBattleCommandLocation Location) EnqueueCallback(byte bankIndex, byte actor, ushort stage)
    {
        (NativeBattleCommandBanks state, NativeBattleCommandLocation location) = Allocate(bankIndex);
        NativeBattleCommandBank bank = state.Banks[bankIndex];
        int index = 2 * location.Slot;
        NativeBattleCommandPacket packet = bank.Packets[index];
        byte[] bytes = packet.Bytes.ToArray();
        bytes[0] = actor;
        bytes[1] = 255;
        bytes[2] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), stage);
        bytes.AsSpan(6, 6).Clear();
        ushort unknown = (ushort)(packet.UnknownByteMask & ~0xFF7);
        return (state with { Banks = state.Banks.SetItem(bankIndex, bank with { Packets = bank.Packets.SetItem(index, new(bytes.ToImmutableArray(), unknown)) }) }, location);
    }

    public NativeBattleCommandBanks BindCommand(NativeBattleCommandLocation location, byte subslot, BattleAttackCommand command)
    {
        ValidateLocation(location);
        if (subslot >= 2)
        {
            throw new InvalidDataException("Native logical command binding exceeds its two physical subcommands.");
        }
        NativeBattleCommandBank bank = Banks[location.Bank];
        int index = 2 * location.Slot + subslot;
        if (bank.Packets[index].ReadByte(0) == 255)
        {
            throw new InvalidDataException("Native logical command cannot bind an unused physical packet.");
        }
        return this with { Banks = Banks.SetItem(location.Bank, bank with { LogicalCommands = bank.LogicalCommands.SetItem(index, command) }) };
    }

    [JsonIgnore]
    public IEnumerable<BattleAttackCommand> BoundCommands => Banks.SelectMany(bank => bank.LogicalCommands).OfType<BattleAttackCommand>();

    public NativeBattleCommandBanks Release(NativeBattleCommandLocation location)
    {
        ValidateLocation(location);
        NativeBattleCommandBank bank = Banks[location.Bank];
        NativeBattleCommandLink link = bank.Links[location.Slot];
        ImmutableArray<NativeBattleCommandLink> links = bank.Links;
        if (link.Previous != 255)
        {
            links = links.SetItem(link.Previous, links[link.Previous] with { Next = link.Next });
        }
        byte tail = bank.Tail;
        if (link.Next == 255)
        {
            tail = link.Previous;
        }
        else
        {
            links = links.SetItem(link.Next, links[link.Next] with { Previous = link.Previous });
        }
        links = links.SetItem(location.Slot, link with { Previous = 0, Next = 0, State = 0 });
        ImmutableArray<NativeBattleCommandPacket> packets = bank.Packets;
        for (int subslot = 0; subslot < 2; subslot++)
        {
            int index = 2 * location.Slot + subslot;
            byte[] bytes = packets[index].Bytes.ToArray();
            bytes[0] = 255;
            bytes.AsSpan(6, 6).Clear();
            packets = packets.SetItem(index, new(bytes.ToImmutableArray(), (ushort)(packets[index].UnknownByteMask & 0x3E)));
        }
        return this with { Banks = Banks.SetItem(location.Bank, bank with { Links = links, Tail = tail, Packets = packets, LogicalCommands = bank.LogicalCommands.SetItem(2 * location.Slot, null).SetItem(2 * location.Slot + 1, null) }) };
    }

    public int CountActor(byte actor)
    {
        //484600 intentionally scans physical rows, not the linked queue or the second packet's actor.
        return Banks.Skip(1).Sum(bank => Enumerable.Range(0, 11).Count(slot => bank.Packets[2 * slot].ReadByte(0) == actor));
    }

    public NativeBattleCommandBanks RemoveActor(byte actor)
    {
        NativeBattleCommandBanks state = this;
        for (byte bank = 1; bank < 3; bank++)
        {
            for (byte slot = 0; slot < 11; slot++)
            {
                if (state.Banks[bank].Packets[2 * slot].ReadByte(0) == actor)
                {
                    state = state.Release(new(bank, slot));
                }
            }
        }
        return state;
    }

    public NativeBattleCommandLocation? FirstEligible(Func<byte, bool> actorBlocked)
    {
        for (byte bank = 0; bank < 3; bank++)
        {
            int head = -1;
            for (int slot = 0; slot < 11; slot++)
            {
                if (Banks[bank].Links[slot].Previous == 255)
                {
                    head = slot;
                    break;
                }
            }
            for (int next = head; next >= 0 && next != 255; next = Banks[bank].Links[next].Next)
            {
                byte actor = Banks[bank].Packets[2 * next].ReadByte(0);
                if (actor != 255 && (bank == 0 || !actorBlocked(actor)))
                {
                    return new(bank, checked((byte)next));
                }
            }
        }
        return null;
    }

    public void Validate()
    {
        if (Banks.IsDefault || Banks.Length != 3 || Banks.Any(bank => bank is null || bank.Packets.IsDefault || bank.Packets.Length != 22 || bank.Links.IsDefault || bank.Links.Length != 11 || bank.Tail != 255 && bank.Tail >= 11 || bank.Packets.Any(packet => packet is null || packet.Bytes.IsDefault || packet.Bytes.Length != 12 || (packet.UnknownByteMask & ~0xFFF) != 0) || bank.Links.Any(link => link is null || link.Previous != 255 && link.Previous >= 11 || link.Next != 255 && link.Next >= 11)))
        {
            throw new InvalidDataException("Native command queues require three physical eleven-entry banks.");
        }
        foreach (NativeBattleCommandBank bank in Banks)
        {
            if (bank.LogicalCommands.IsDefault || bank.LogicalCommands.Length != 22 || bank.LogicalCommands.Where((command, index) => command is not null && bank.Packets[index].ReadByte(0) == 255).Any())
            {
                throw new InvalidDataException("Native logical command bindings contradict their physical packet actors.");
            }
            HashSet<int> visited = [];
            int next = bank.Tail;
            while (next != 255)
            {
                if (!visited.Add(next) || bank.Links[next].Previous == 0 && bank.Links[next].Next == 0)
                {
                    throw new InvalidDataException("Native command bank contains cyclic or unallocated links.");
                }
                int previous = bank.Links[next].Previous;
                if (previous != 255 && bank.Links[previous].Next != next)
                {
                    throw new InvalidDataException("Native command bank has contradictory previous/next links.");
                }
                next = previous;
            }
            if (bank.Links.Where((link, index) => !visited.Contains(index) && (link.Previous != 0 || link.Next != 0)).Any())
            {
                throw new InvalidDataException("Native command bank has an orphan allocated physical link.");
            }
        }
    }

    private static void ValidateLocation(NativeBattleCommandLocation location)
    {
        if (location.Bank >= 3 || location.Slot >= 11)
        {
            throw new InvalidDataException("Native command location is outside its physical bank.");
        }
    }
}
