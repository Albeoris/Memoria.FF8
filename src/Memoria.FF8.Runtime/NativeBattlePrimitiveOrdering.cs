using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePrimitivePacket([property: JsonRequired] uint Address, [property: JsonRequired] ImmutableArray<uint> Words);
public sealed partial record NativeBattlePrimitiveProxy([property: JsonRequired] uint PacketAddress, [property: JsonRequired] byte PreviousHighByte, [property: JsonRequired] ushort Flags20);
public sealed record NativeBattlePrimitiveCommand(uint PacketAddress, int WordOffset, byte Opcode, ImmutableArray<uint> Words, ushort ProxyFlags20);
public sealed record NativeBattlePrimitiveResolvedCommand(NativeBattlePrimitiveCommand Command, NativeBattlePrimitiveDrawMode DrawMode);
public sealed record NativeBattlePrimitivePassResult(NativeBattlePrimitiveDrawMode DrawMode, ImmutableArray<NativeBattlePrimitiveResolvedCommand> Commands);
public sealed record NativeBattlePrimitiveDrawMode([property: JsonRequired] ushort? TexturePage)
{
    public uint? TextureWindowBits { get; init; }
    public NativeBattlePcTextureWindowSelection? TextureSelection { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TexturePixelSnapshot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoftwareNormalViewportDerived? NormalViewport { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoftwareViewportInputs? ViewportInputs { get; init; }
    public int? BlendMode => TexturePage is ushort page ? (page >> 5) & 3 : null;

    public NativeBattlePrimitivePassResult ExecutePass(ImmutableArray<NativeBattlePrimitiveCommand> commands, ImmutableArray<uint> nativeHandlers)
    {
        if (commands.IsDefault || nativeHandlers.IsDefault || nativeHandlers.Length != 256)
        {
            throw new InvalidDataException("Native45D310 requires its ordered commands and original256-entry render dispatch table.");
        }
        NativeBattlePrimitiveDrawMode mode = this;
        ImmutableArray<NativeBattlePrimitiveResolvedCommand>.Builder executed = ImmutableArray.CreateBuilder<NativeBattlePrimitiveResolvedCommand>();
        foreach (NativeBattlePrimitiveCommand command in commands)
        {
            if (nativeHandlers[command.Opcode] != 0)
            {
                mode = mode.Execute(command);
                executed.Add(new(command, mode));
            }
        }
        return new(mode, executed.ToImmutable());
    }

    /// <summary>Apply only commands whose native dispatch handler executes in this pass.</summary>
    public NativeBattlePrimitiveDrawMode Execute(NativeBattlePrimitiveCommand command)
    {
        return ExecuteWords(command.Opcode, command.Words);
    }

    public NativeBattlePrimitiveDrawMode ExecuteWords(byte opcode, ImmutableArray<uint> words)
    {
        if (opcode == 0xE2)
        {
            if (words.Length != 1)
            {
                throw new InvalidDataException("Native45BC60 requires the original single E2 command DWORD.");
            }
            uint window = words[0] & 0xFFFFFF;
            return TextureWindowBits == window ? this : this with { TextureWindowBits = window };
        }
        int pageWord = opcode == 0xE1 ? 0 : (opcode & 0xFC) switch
        {
            0x24 or 0x2C => 4,
            0x34 or 0x3C => 5,
            _ => -1
        };
        if (pageWord < 0)
        {
            return this;
        }
        if (pageWord >= words.Length)
        {
            throw new InvalidDataException("Native textured primitive lacks its physical texture-page WORD.");
        }
        //E1 handlers462EB0/463210 and textured-polygon helper461A30 call45B960.
        ushort page = opcode == 0xE1 ? unchecked((ushort)words[pageWord]) : unchecked((ushort)(words[pageWord] >> 16));
        return TexturePage == page ? this : this with { TexturePage = page };
    }
}

/// <summary>45CC80's physical OT,45C8E0 proxy links and45D310's packet traversal; rendering dispatch remains explicit.</summary>
public sealed partial record NativeBattlePrimitiveOrdering([property: JsonRequired] uint TableAddress, [property: JsonRequired] ImmutableArray<uint> Table, [property: JsonRequired] ImmutableArray<NativeBattlePrimitiveProxy> Proxies, [property: JsonRequired] ImmutableArray<NativeBattlePrimitivePacket> Packets)
{
    public const uint ProxyBankAddress = 0x1C48828;
    public const int ProxyCapacity = 0x60000 / 24;
    public const int BattleTableLength = 4386;

    public static NativeBattlePrimitiveOrdering InitializeBattle(byte buffer)
    {
        if (buffer > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer));
        }
        return Initialize(0x1D8E058u + 17544u * buffer, BattleTableLength);
    }

    public static NativeBattlePrimitiveOrdering Initialize(uint tableAddress, int count)
    {
        if ((tableAddress & 3) != 0 || tableAddress == 0 || count <= 0 || (ulong)tableAddress + 4ul * (uint)count > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(tableAddress));
        }
        ImmutableArray<uint>.Builder table = ImmutableArray.CreateBuilder<uint>(count);
        table.Add(uint.MaxValue);
        for (int index = 1; index < count; index++)
        {
            table.Add(tableAddress + 4u * (uint)(index - 1));
        }
        return new(tableAddress, table.MoveToImmutable(), [], []);
    }

    public void Validate()
    {
        if (Table.IsDefaultOrEmpty || (TableAddress & 3) != 0 || TableAddress == 0 || Proxies.IsDefault || Proxies.Length > ProxyCapacity || Proxies.Any(proxy => proxy is null) || Packets.IsDefault || Packets.Any(packet => packet is null || packet.Address == 0 || (packet.Address & 3) != 0 || packet.Words.IsDefaultOrEmpty || packet.Words.Length < 1 + (packet.Words[0] >> 24)) || Packets.Select(packet => packet.Address).Distinct().Count() != Packets.Length)
        {
            throw new InvalidDataException("Native primitive ordering requires its physical table,24-byte proxy bank and unique packet memory owners.");
        }
    }

    public NativeBattlePrimitiveOrdering Prepend(int bucket, NativeBattlePrimitivePacket packet, ushort proxyFlags20 = 1)
    {
        Validate();
        if ((uint)bucket >= Table.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bucket));
        }
        if (Proxies.Length == ProxyCapacity)
        {
            //45C8E0 returns the unchanged end cursor and performs no writes when the physical bank is full.
            return this;
        }
        if (packet is null || packet.Address == 0 || (packet.Address & 3) != 0 || packet.Words.IsDefaultOrEmpty || packet.Words.Length < 1 + (packet.Words[0] >> 24))
        {
            throw new InvalidDataException("Native primitive insertion requires a bounded original packet.");
        }
        uint previous = Table[bucket];
        NativeBattlePrimitiveProxy proxy = new(packet.Address, (byte)(previous >> 24), proxyFlags20);
        uint tag = (packet.Words[0] & 0xFF000000) | (previous & 0xFFFFFF);
        packet = packet with { Words = packet.Words.SetItem(0, tag) };
        int existing = -1;
        for (int index = 0; index < Packets.Length; index++)
        {
            if (Packets[index].Address == packet.Address)
            {
                existing = index;
                break;
            }
        }
        ImmutableArray<NativeBattlePrimitivePacket> packets = existing < 0 ? Packets.Add(packet) : Packets.SetItem(existing, packet);
        return this with { Table = Table.SetItem(bucket, ProxyBankAddress + 24u * (uint)Proxies.Length), Proxies = Proxies.Add(proxy), Packets = packets };
    }

    public ImmutableArray<NativeBattlePrimitiveCommand> Traverse(ImmutableArray<byte> commandWordLengths)
    {
        Validate();
        if (commandWordLengths.IsDefault || commandWordLengths.Length != 256)
        {
            throw new InvalidDataException("Native45D310 requires the original256-byte B7CC28 command-length table.");
        }
        Dictionary<uint, NativeBattlePrimitivePacket> packets = Packets.ToDictionary(packet => packet.Address);
        ImmutableArray<NativeBattlePrimitiveCommand>.Builder commands = ImmutableArray.CreateBuilder<NativeBattlePrimitiveCommand>();
        HashSet<uint> visited = [];
        uint cursor = TableAddress + 4u * (uint)(Table.Length - 1);
        while (cursor != 0)
        {
            if (!visited.Add(cursor))
            {
                throw new InvalidDataException("Native ordering-table links contain a cycle.");
            }
            if (cursor >= ProxyBankAddress && cursor < ProxyBankAddress + 24u * (uint)Proxies.Length)
            {
                if ((cursor - ProxyBankAddress) % 24 != 0)
                {
                    throw new InvalidDataException("Native primitive link is not a physical24-byte proxy.");
                }
                NativeBattlePrimitiveProxy proxy = Proxies[(int)((cursor - ProxyBankAddress) / 24)];
                if (!packets.TryGetValue(proxy.PacketAddress, out NativeBattlePrimitivePacket? packet))
                {
                    throw new NotSupportedException("Native ordering traversal reached packet memory without its original owner.");
                }
                int remaining = (int)(packet.Words[0] >> 24);
                int word = 1;
                while (remaining > 0)
                {
                    byte opcode = (byte)(packet.Words[word] >> 24);
                    int length = opcode == 0 ? 1 : Math.Max((int)commandWordLengths[opcode], 1);
                    if (length > remaining || word + length > packet.Words.Length)
                    {
                        throw new InvalidDataException("Native primitive packet ends inside a command.");
                    }
                    if (opcode != 0)
                    {
                        commands.Add(new(packet.Address, word, opcode, packet.Words.Slice(word, length), proxy.Flags20));
                    }
                    remaining -= length;
                    word += length;
                }
                uint low = packet.Words[0] & 0xFFFFFF;
                if (low == 0xFFFFFF)
                {
                    break;
                }
                cursor = low | (uint)proxy.PreviousHighByte << 24;
            }
            else if (cursor >= TableAddress && (ulong)cursor < (ulong)TableAddress + 4ul * (uint)Table.Length && (cursor - TableAddress) % 4 == 0)
            {
                cursor = Table[(int)((cursor - TableAddress) / 4)];
                if ((cursor & 0xFFFFFF) == 0xFFFFFF)
                {
                    break;
                }
            }
            else
            {
                throw new NotSupportedException($"Native ordering traversal requires the original table or proxy owner at{cursor:X}.");
            }
        }
        return commands.ToImmutable();
    }
}
