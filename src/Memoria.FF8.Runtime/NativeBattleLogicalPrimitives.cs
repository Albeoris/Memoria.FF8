using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Portable identity of one original primitive arena; heap addresses are deliberately not invented.</summary>
public sealed record NativeBattlePrimitiveReference(string Bank, byte Buffer, int ByteOffset);
public readonly record struct NativeBattlePrimitiveLink(bool Proxy, int Index);
public sealed record NativeBattleLogicalPacket(NativeBattlePrimitiveReference Reference, ImmutableArray<uint> Words, NativeBattlePrimitiveLink Next)
{
    public byte? NextTableBuffer { get; init; }
    public bool RequiresPhysicalRead { get; init; }
}
public sealed record NativeBattleLogicalProxy(NativeBattlePrimitiveReference Packet, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public byte? NextHigh22 { get; init; }
}
public sealed record NativeBattleLogicalCommand(NativeBattlePrimitiveReference Packet, int WordOffset, byte Opcode, ImmutableArray<uint> Words, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public int? OrderingBucket { get; init; }
}
public sealed record NativeBattleLogicalResolvedCommand(NativeBattleLogicalCommand Command, NativeBattlePrimitiveDrawMode DrawMode, byte Pass);

/// <summary>45CC80/45C8E0/45C870 links using logical bank references with the same physical alias and capacity behavior.</summary>
public sealed record NativeBattleLogicalOrdering(ImmutableArray<NativeBattlePrimitiveLink> Table, ImmutableArray<NativeBattleLogicalProxy> Proxies, ImmutableArray<NativeBattleLogicalPacket> Packets)
{
    public int? ProxyCursor { get; init; }
    public byte? TableBuffer { get; init; }
    public ImmutableArray<NativeBattlePrimitiveLink>? OtherTable { get; init; }
    private static ImmutableArray<NativeBattlePrimitiveLink> EmptyRoots() => Enumerable.Range(0, NativeBattlePrimitiveOrdering.BattleTableLength).Select(index => new NativeBattlePrimitiveLink(false, index - 1)).ToImmutableArray();
    public static NativeBattleLogicalOrdering Clear(byte buffer = 0) => new(EmptyRoots(), [], []) { ProxyCursor = 0, TableBuffer = buffer, OtherTable = EmptyRoots() };

    public NativeBattleLogicalOrdering ResetAfterDraw45D080() => this with { ProxyCursor = 0, Packets = Packets.Select(packet => packet with { RequiresPhysicalRead = true }).ToImmutableArray() };
    public NativeBattleLogicalOrdering ClearRoots45D550()
    {
        bool otherEmpty = OtherTable is { } other && other.Length == NativeBattlePrimitiveOrdering.BattleTableLength && other.Select((link, index) => !link.Proxy && link.Index == index - 1).All(empty => empty);
        return this with { Table = EmptyRoots(), Packets = otherEmpty ? [] : Packets };
    }

    public NativeBattleLogicalOrdering SelectTable(byte buffer)
    {
        if (buffer > 1 || TableBuffer is null)
        {
            throw new NotSupportedException("Native OT bank selection requires its actual retained table identity.");
        }
        return buffer == TableBuffer ? this : this with { Table = OtherTable ?? throw new NotSupportedException("Native alternate OT roots are unknown in this snapshot."), OtherTable = Table, TableBuffer = buffer };
    }

    public static byte[] LinkBytes(uint address) => [(byte)address, (byte)(address >> 8), (byte)(address >> 16)];

    private static uint LinkAddress(NativeBattlePrimitiveLink link, byte buffer)
    {
        if (link.Index < 0)
        {
            return uint.MaxValue;
        }
        return checked((link.Proxy ? 0x1C48828u : 0x1D8E058u + 17544u * buffer) + (link.Proxy ? 24u : 4u) * (uint)link.Index);
    }

    public NativeBattleLogicalOrdering Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
    {
        Builder builder = new(this);
        builder.Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw);
        return builder.ToImmutable();
    }

    private static void ValidateInsertion(int tableLength, int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ImmutableArray<int> vertexPayload4)
    {
        if ((uint)bucket >= tableLength || string.IsNullOrWhiteSpace(reference.Bank) || reference.Buffer > 1 || reference.ByteOffset < 0 || (reference.ByteOffset & 3) != 0 || words.IsDefaultOrEmpty || words.Length < 1 + (words[0] >> 24) || vertexPayload4.IsDefault || vertexPayload4.Length is not (0 or 4))
        {
            throw new InvalidDataException("Native logical primitive insertion lacks its original bounded packet or bank identity.");
        }
    }

    /// <summary>Transient producer pass; published ordering arrays remain immutable and independently owned.</summary>
    internal sealed class Builder
    {
        private readonly ImmutableArray<NativeBattlePrimitiveLink>.Builder table;
        private readonly ImmutableArray<NativeBattleLogicalProxy>.Builder proxies;
        private readonly ImmutableArray<NativeBattleLogicalPacket>.Builder packets;
        private readonly Dictionary<NativeBattlePrimitiveReference, int> packetIndices;
        private readonly Dictionary<(string Bank, byte Buffer), SortedSet<(int Offset, int Index)>> packetRanges = [];
        private int maximumPacketBytes;
        private readonly byte tableBuffer;
        private readonly ImmutableArray<NativeBattlePrimitiveLink>? otherTable;
        private int proxyCursor;

        public Builder(NativeBattleLogicalOrdering source)
        {
            proxyCursor = source.ProxyCursor ?? throw new NotSupportedException("Native proxy allocation requires its actual retained physical cursor; migrate the named snapshot explicitly.");
            tableBuffer = source.TableBuffer ?? throw new NotSupportedException("Native proxy allocation requires its physical OT table identity.");
            otherTable = source.OtherTable;
            if (proxyCursor < 0 || proxyCursor > NativeBattlePrimitiveOrdering.ProxyCapacity || proxyCursor > source.Proxies.Length || tableBuffer > 1)
            {
                throw new InvalidDataException("Native proxy cursor exceeds its retained physical cells.");
            }
            table = source.Table.ToBuilder();
            proxies = source.Proxies.ToBuilder();
            packets = source.Packets.ToBuilder();
            packetIndices = new(source.Packets.Length);
            for (int index = 0; index < source.Packets.Length; index++)
            {
                packetIndices.TryAdd(source.Packets[index].Reference, index);
                RegisterPacketRange(source.Packets[index], index);
            }
        }

        public int ProxyCount => proxyCursor;
        public uint LastHeaderLink { get; private set; }

        public void InvalidatePacketRange(NativeBattlePrimitiveReference reference, int length)
        {
            if (length == 0 || !packetRanges.TryGetValue((reference.Bank, reference.Buffer), out SortedSet<(int Offset, int Index)>? ranges))
            {
                return;
            }
            int minimum = Math.Max(0, reference.ByteOffset - maximumPacketBytes + 1);
            int maximum = checked(reference.ByteOffset + length - 1);
            foreach ((int _, int index) in ranges.GetViewBetween((minimum, int.MinValue), (maximum, int.MaxValue)))
            {
                NativeBattleLogicalPacket packet = packets[index];
                if (!packet.RequiresPhysicalRead && packet.Reference.Bank == reference.Bank && packet.Reference.Buffer == reference.Buffer && packet.Reference.ByteOffset < reference.ByteOffset + length && reference.ByteOffset < packet.Reference.ByteOffset + packet.Words.Length * 4)
                {
                    packets[index] = packet with { RequiresPhysicalRead = true };
                }
            }
        }

        private void RegisterPacketRange(NativeBattleLogicalPacket packet, int index)
        {
            if (packet.RequiresPhysicalRead)
            {
                return;
            }
            var key = (packet.Reference.Bank, packet.Reference.Buffer);
            if (!packetRanges.TryGetValue(key, out SortedSet<(int Offset, int Index)>? ranges))
            {
                ranges = [];
                packetRanges.Add(key, ranges);
            }
            ranges.Add((packet.Reference.ByteOffset, index));
            maximumPacketBytes = Math.Max(maximumPacketBytes, checked(packet.Words.Length * 4));
        }

        public void Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
        {
            ValidateInsertion(table.Count, bucket, reference, words, vertexPayload4);
            if (proxyCursor >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            InvalidatePacketRange(reference, words.Length * 4);
            LastHeaderLink = LinkAddress(table[bucket], tableBuffer);
            NativeBattleLogicalPacket packet = new(reference, words, table[bucket]) { NextTableBuffer = tableBuffer };
            if (packetIndices.TryGetValue(reference, out int existing))
            {
                packets[existing] = packet;
                RegisterPacketRange(packet, existing);
            }
            else
            {
                packetIndices.Add(reference, packets.Count);
                RegisterPacketRange(packet, packets.Count);
                packets.Add(packet);
            }
            NativeBattleLogicalProxy proxy = new(reference, flags20, vertexPayload4, moduleDraw) { NextHigh22 = (byte)(LastHeaderLink >> 24) };
            table[bucket] = new(true, proxyCursor);
            if (proxyCursor < proxies.Count)
            {
                proxies[proxyCursor] = proxy;
            }
            else
            {
                proxies.Add(proxy);
            }
            proxyCursor++;
        }

        public NativeBattleLogicalOrdering ToImmutable() => new(table.ToImmutable(), proxies.ToImmutable(), packets.ToImmutable()) { ProxyCursor = proxyCursor, TableBuffer = tableBuffer, OtherTable = otherTable };
    }

    public ImmutableArray<NativeBattleLogicalCommand> Traverse(ImmutableArray<byte> wordLengths, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        if (Table.IsDefault || Table.Length != NativeBattlePrimitiveOrdering.BattleTableLength || Proxies.IsDefault || Packets.IsDefault || wordLengths.IsDefault || wordLengths.Length != 256)
        {
            throw new InvalidDataException("Native logical OT requires its original4386 roots, proxy bank and command length table.");
        }
        Dictionary<NativeBattlePrimitiveReference, NativeBattleLogicalPacket> memory = Packets.ToDictionary(packet => packet.Reference);
        HashSet<(byte Bank, bool Proxy, int Index)> visited = [];
        ImmutableArray<NativeBattleLogicalCommand>.Builder commands = ImmutableArray.CreateBuilder<NativeBattleLogicalCommand>();
        NativeBattlePrimitiveLink cursor = new(false, Table.Length - 1);
        int bucket = Table.Length - 1;
        byte rootBuffer = TableBuffer ?? throw new NotSupportedException("Native OT traversal requires its retained table bank identity.");
        while (cursor.Index >= 0)
        {
            if (!visited.Add((cursor.Proxy ? byte.MaxValue : rootBuffer, cursor.Proxy, cursor.Index)))
            {
                throw new InvalidDataException("Native logical OT contains a shared-packet link cycle.");
            }
            if (!cursor.Proxy)
            {
                if (cursor.Index >= Table.Length)
                {
                    throw new InvalidDataException("Native logical OT has a missing table root.");
                }
                bucket = cursor.Index;
                ImmutableArray<NativeBattlePrimitiveLink> roots = rootBuffer == TableBuffer ? Table : OtherTable ?? throw new NotSupportedException("Native OT traversal reached an unknown alternate table.");
                cursor = roots[cursor.Index];
                continue;
            }
            if (cursor.Index >= Proxies.Length)
            {
                throw new InvalidDataException("Native logical OT has a missing proxy row.");
            }
            NativeBattleLogicalProxy proxy = Proxies[cursor.Index];
            if (!memory.TryGetValue(proxy.Packet, out NativeBattleLogicalPacket? packet))
            {
                throw new NotSupportedException("Native logical OT reached an unobserved original packet owner.");
            }
            uint nextAddress;
            if (packet.RequiresPhysicalRead)
            {
                Func<NativeBattlePrimitiveReference, int, byte[]> reader = readPhysical ?? throw new NotSupportedException("Native retained packet alias requires its current physical arena bytes.");
                uint header = BinaryPrimitives.ReadUInt32LittleEndian(reader(packet.Reference, 4));
                byte[] bytes = reader(packet.Reference, checked((1 + (int)(header >> 24)) * 4));
                packet = packet with { Words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 * index))).ToImmutableArray() };
                nextAddress = (header & 0xFFFFFFu) | (uint)(proxy.NextHigh22 ?? throw new NotSupportedException("Native retained proxy lacks its actual high link BYTE22.")) << 24;
            }
            else
            {
                uint address = LinkAddress(packet.Next, packet.NextTableBuffer ?? throw new NotSupportedException("Native packet link lacks its original OT bank identity."));
                nextAddress = (address & 0xFFFFFFu) | (uint)(proxy.NextHigh22 ?? throw new NotSupportedException("Native proxy lacks its actual high link BYTE22.")) << 24;
            }
            int remaining = (int)(packet.Words[0] >> 24);
            int word = 1;
            while (remaining > 0)
            {
                byte opcode = (byte)(packet.Words[word] >> 24);
                int count = opcode == 0 ? 1 : Math.Max((int)wordLengths[opcode], 1);
                if (count > remaining || word + count > packet.Words.Length)
                {
                    throw new InvalidDataException("Native logical packet ends inside an original command.");
                }
                if (opcode != 0)
                {
                    commands.Add(new(packet.Reference, word, opcode, packet.Words.Slice(word, count), proxy.Flags20, proxy.VertexPayload4, proxy.ModuleDraw) { OrderingBucket = bucket });
                }
                remaining -= count;
                word += count;
            }
            if ((nextAddress & 0xFFFFFFu) == 0xFFFFFFu)
            {
                cursor = new(false, -1);
            }
            else if (nextAddress >= 0x1C48828u && nextAddress < 0x1CA8828u && (nextAddress - 0x1C48828u) % 24 == 0)
            {
                cursor = new(true, (int)((nextAddress - 0x1C48828u) / 24));
            }
            else if (nextAddress >= 0x1D8E058u && nextAddress < 0x1D96968u && (nextAddress - 0x1D8E058u) % 4 == 0)
            {
                uint relative = nextAddress - 0x1D8E058u;
                rootBuffer = (byte)(relative / 17544);
                cursor = new(false, (int)(relative % 17544 / 4));
            }
            else
            {
                throw new NotSupportedException("Native packet alias produced a link outside the observed proxy and OT banks.");
            }
        }
        return commands.ToImmutable();
    }
}

public sealed record NativeBattlePrimitiveTables(ImmutableArray<byte> WordLengths, ImmutableArray<byte> FlaggedCommands, ImmutableArray<uint> PreprocessingHandlers, ImmutableArray<uint> FirstHandlers, ImmutableArray<uint> SecondHandlers)
{
    public void Validate()
    {
        if (WordLengths.IsDefault || WordLengths.Length != 256 || FlaggedCommands.IsDefault || FlaggedCommands.Length != 256 || PreprocessingHandlers.IsDefault || PreprocessingHandlers.Length != 256 || FirstHandlers.IsDefault || FirstHandlers.Length != 256 || SecondHandlers.IsDefault || SecondHandlers.Length != 256)
        {
            throw new InvalidDataException("Native45D080 requires its five original256-entry command/handler tables.");
        }
    }
}

public static class NativeBattleLogicalPasses
{
    public static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache? Cache) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags, NativeBattlePcTextureWindowCache? cache = null, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        tables.Validate();
        if (ordering.ProxyCursor == NativeBattlePrimitiveOrdering.ProxyCapacity)
        {
            return (initial, [], cache);
        }
        ImmutableArray<NativeBattleLogicalCommand> commands = ordering.Traverse(tables.WordLengths, readPhysical);
        NativeBattlePrimitiveDrawMode mode = initial;
        ImmutableArray<NativeBattleLogicalResolvedCommand>.Builder executed = ImmutableArray.CreateBuilder<NativeBattleLogicalResolvedCommand>();
        ImmutableArray<uint>[] passes = [tables.PreprocessingHandlers, tables.FirstHandlers, tables.SecondHandlers];
        for (byte pass = 0; pass < passes.Length; pass++)
        {
            foreach (NativeBattleLogicalCommand command in commands)
            {
                if (passes[pass][command.Opcode] == 0 || (drawFlags & 1) == 0 && tables.FlaggedCommands[command.Opcode] != 0)
                {
                    continue;
                }
                mode = mode.ExecuteWords(command.Opcode, command.Words);
                mode = mode with { TextureSelection = null };
                executed.Add(new(command, mode, pass));
            }
        }
        if (cache is null)
        {
            return (mode, executed.ToImmutable(), null);
        }
        var selected = SelectTextures(executed.ToImmutable(), cache);
        return (mode, selected.Commands, selected.Cache);
    }

    /// <summary>Apply PC resource preparation and selection to an executed OT; also supports a declared cache-reset migration without advancing simulation.</summary>
    public static (ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache Cache) SelectTextures(ImmutableArray<NativeBattleLogicalResolvedCommand> commands, NativeBattlePcTextureWindowCache cache)
    {
        if (commands.IsDefault || commands.Any(command => command.Pass > 2))
        {
            throw new InvalidDataException("Native texture selection requires the three actual executed OT passes.");
        }
        ImmutableArray<NativeBattleLogicalResolvedCommand>.Builder selected = ImmutableArray.CreateBuilder<NativeBattleLogicalResolvedCommand>();
        for (byte pass = 0; pass < 3; pass++)
        {
            if (pass == 1)
            {
                cache = cache.Prepare464BD0().Cache;
            }
            foreach (NativeBattleLogicalResolvedCommand command in commands.Where(command => command.Pass == pass))
            {
                NativeBattlePrimitiveDrawMode mode = command.DrawMode with { TextureSelection = null };
                if ((command.Command.Opcode & 0xE4) == 0x24)
                {
                    ushort page = mode.TexturePage ?? throw new NotSupportedException("Native textured pass requires its executed texture page.");
                    ushort clut = unchecked((ushort)(command.Command.Words[2] >> 16));
                    if (pass == 0)
                    {
                        cache = cache.Preprocess462F90(page, clut);
                    }
                    else
                    {
                        uint window = mode.TextureWindowBits ?? throw new NotSupportedException("Native465CE0 requires its retained E2 window bits.");
                        NativeBattlePcTextureWindowResolve resolved = cache.Resolve465CE0(page, clut, window);
                        cache = resolved.Cache;
                        mode = mode with { TextureSelection = resolved.Selection };
                    }
                }
                selected.Add(command with { DrawMode = mode });
            }
            if (pass == 1)
            {
                cache = cache.Flush465930();
            }
        }
        return (selected.ToImmutable(), cache);
    }
}
