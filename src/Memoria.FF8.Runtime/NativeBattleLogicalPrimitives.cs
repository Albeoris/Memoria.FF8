using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Portable identity of one original primitive arena; heap addresses are deliberately not invented.</summary>
public sealed record NativeBattlePrimitiveReference(string Bank, byte Buffer, int ByteOffset);
public readonly record struct NativeBattlePrimitiveLink(bool Proxy, int Index);
public readonly record struct NativeBattleAddressedOrderingRoot(uint NextAddress, int Bucket);
public sealed record NativeBattleLogicalPacket(NativeBattlePrimitiveReference Reference, ImmutableArray<uint> Words, NativeBattlePrimitiveLink Next)
{
    public byte? NextTableBuffer { get; init; }
    public bool RequiresPhysicalRead { get; init; }
    // Presentation coordinates belong to the current physical packet, including all of its proxy aliases.
    public ImmutableArray<BattleSceneScreenPoint>? ProjectedCoordinates { get; init; }
}
public sealed record NativeBattleLogicalProxy(NativeBattlePrimitiveReference Packet, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public byte? NextHigh22 { get; init; }
}
public sealed record NativeBattleLogicalCommand(NativeBattlePrimitiveReference Packet, int WordOffset, byte Opcode, ImmutableArray<uint> Words, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public int? OrderingBucket { get; init; }
    public ImmutableArray<BattleSceneScreenPoint>? ProjectedCoordinates { get; init; }
}
public sealed record NativeBattleLogicalResolvedCommand(NativeBattleLogicalCommand Command, NativeBattlePrimitiveDrawMode DrawMode, byte Pass);

/// <summary>45CC80/45C8E0/45C870 links using logical bank references with the same physical alias and capacity behavior.</summary>
public sealed record NativeBattleLogicalOrdering(ImmutableArray<NativeBattlePrimitiveLink> Table, ImmutableArray<NativeBattleLogicalProxy> Proxies, ImmutableArray<NativeBattleLogicalPacket> Packets)
{
    [JsonIgnore]
    internal NativeBattleLogicalOrderingTransaction? Transaction { get; init; }
    public int? ProxyCursor { get; init; }
    public byte? TableBuffer { get; init; }
    public ImmutableArray<NativeBattlePrimitiveLink>? OtherTable { get; init; }
    private static ImmutableArray<NativeBattlePrimitiveLink> EmptyRoots() => Enumerable.Range(0, NativeBattlePrimitiveOrdering.BattleTableLength).Select(index => new NativeBattlePrimitiveLink(false, index - 1)).ToImmutableArray();
    public static NativeBattleLogicalOrdering Clear(byte buffer = 0) => new(EmptyRoots(), [], []) { ProxyCursor = 0, TableBuffer = buffer, OtherTable = EmptyRoots() };

    public NativeBattleLogicalOrdering ResetAfterDraw45D080()
    {
        ImmutableArray<NativeBattleLogicalPacket>.Builder? packets = null;
        for (int index = 0; index < Packets.Length; index++)
        {
            NativeBattleLogicalPacket packet = Packets[index];
            if (!packet.RequiresPhysicalRead)
            {
                packets ??= Packets.ToBuilder();
                packets[index] = packet with { RequiresPhysicalRead = true };
            }
        }
        if (packets is null && ProxyCursor == 0)
        {
            return this;
        }
        return this with { ProxyCursor = 0, Packets = packets?.MoveToImmutable() ?? Packets };
    }
    public NativeBattleLogicalOrdering ClearRoots45D550()
    {
        bool otherEmpty = OtherTable is { } other && other.Length == NativeBattlePrimitiveOrdering.BattleTableLength && other.Select((link, index) => !link.Proxy && link.Index == index - 1).All(empty => empty);
        return this with { Table = EmptyRoots(), Packets = otherEmpty ? [] : Packets };
    }

    public NativeBattleLogicalOrdering ClearRange45D550(int first, int count)
    {
        if (Table.IsDefault || first < 0 || count <= 0 || first > Table.Length - count)
        {
            throw new InvalidDataException("Native ordering-table clear requires its actual bounded DWORD range.");
        }
        ImmutableArray<NativeBattlePrimitiveLink>.Builder roots = Table.ToBuilder();
        for (int index = first; index < first + count; index++)
        {
            roots[index] = new(false, index == first ? -1 : index - 1);
        }
        return this with { Table = roots.ToImmutable() };
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

    public NativeBattleLogicalOrdering Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null)
    {
        Builder builder = new(this);
        builder.Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates);
        return builder.ToImmutable();
    }

    public (NativeBattleLogicalOrdering Ordering, uint? ProxyAddress) PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
    {
        Builder builder = new(this);
        int index = builder.ProxyCount;
        builder.PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw);
        return (builder.ToImmutable(), index < NativeBattlePrimitiveOrdering.ProxyCapacity ? 0x1C48828u + (uint)(24 * index) : null);
    }

    private static void ValidateInsertion(int tableLength, int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ImmutableArray<int> vertexPayload4)
    {
        if ((uint)bucket >= tableLength || string.IsNullOrWhiteSpace(reference.Bank) || reference.Buffer > 1 || reference.ByteOffset < 0 || (reference.ByteOffset & 3) != 0 || words.IsDefaultOrEmpty || words.Length < 1 + (words[0] >> 24) || vertexPayload4.IsDefault || vertexPayload4.Length is not (0 or 4))
        {
            throw new InvalidDataException($"Native logical primitive insertion lacks its original bounded packet or bank identity: bucket {bucket}/{tableLength}, bank '{reference.Bank}', buffer {reference.Buffer}, byte offset {reference.ByteOffset}, words {(words.IsDefault ? -1 : words.Length)}, header {(words.IsDefaultOrEmpty ? 0 : words[0]):X8}, vertex payload {(vertexPayload4.IsDefault ? -1 : vertexPayload4.Length)}.");
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
        private readonly NativeBattleLogicalOrderingTransaction? transaction;
        private uint lastHeaderLink;
        private bool checkpointActive;
        private int checkpointProxyCursor;
        private int checkpointProxyCount;
        private int checkpointPacketCount;
        private int checkpointMaximumPacketBytes;
        private uint checkpointLastHeaderLink;
        private readonly List<(int Index, NativeBattlePrimitiveLink Value)> tableUndo = [];
        private readonly List<(int Index, NativeBattleLogicalProxy Value)> proxyUndo = [];
        private readonly List<(int Index, NativeBattleLogicalPacket Value)> packetUndo = [];
        private readonly List<(SortedSet<(int Offset, int Index)> Ranges, (int Offset, int Index) Value)> rangeUndo = [];

        public Builder(NativeBattleLogicalOrdering source)
        {
            transaction = source.Transaction;
            if (transaction is not null)
            {
                transaction.RequireActive();
                Builder shared = transaction.Writer;
                table = shared.table;
                proxies = shared.proxies;
                packets = shared.packets;
                packetIndices = shared.packetIndices;
                packetRanges = shared.packetRanges;
                tableBuffer = shared.tableBuffer;
                otherTable = shared.otherTable;
                return;
            }
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
                packetIndices.TryAdd(NativeBattlePrimitiveProcess.NormalizeCommonReference(source.Packets[index].Reference), index);
                RegisterPacketRange(source.Packets[index], index);
            }
        }

        public int ProxyCount => transaction?.Writer.ProxyCount ?? proxyCursor;
        public uint LastHeaderLink => transaction?.Writer.LastHeaderLink ?? lastHeaderLink;

        public void InvalidatePacketRange(NativeBattlePrimitiveReference reference, int length)
        {
            if (transaction is not null)
            {
                transaction.RequireActive();
                transaction.Writer.InvalidatePacketRange(reference, length);
                return;
            }
            reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(reference);
            if (length == 0 || !packetRanges.TryGetValue((reference.Bank, reference.Buffer), out SortedSet<(int Offset, int Index)>? ranges))
            {
                return;
            }
            int minimum = Math.Max(0, reference.ByteOffset - maximumPacketBytes + 1);
            int maximum = checked(reference.ByteOffset + length - 1);
            foreach ((int _, int index) in ranges.GetViewBetween((minimum, int.MinValue), (maximum, int.MaxValue)))
            {
                NativeBattleLogicalPacket packet = packets[index];
                NativeBattlePrimitiveReference physical = NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference);
                if ((!packet.RequiresPhysicalRead || packet.ProjectedCoordinates is not null) && physical.Bank == reference.Bank && physical.Buffer == reference.Buffer && physical.ByteOffset < reference.ByteOffset + length && reference.ByteOffset < physical.ByteOffset + packet.Words.Length * 4)
                {
                    if (checkpointActive)
                    {
                        packetUndo.Add((index, packet));
                    }
                    packets[index] = packet with { RequiresPhysicalRead = true, ProjectedCoordinates = null };
                }
            }
        }

        private void RegisterPacketRange(NativeBattleLogicalPacket packet, int index)
        {
            if (packet.RequiresPhysicalRead && packet.ProjectedCoordinates is null)
            {
                return;
            }
            NativeBattlePrimitiveReference reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference);
            var key = (reference.Bank, reference.Buffer);
            if (!packetRanges.TryGetValue(key, out SortedSet<(int Offset, int Index)>? ranges))
            {
                ranges = [];
                packetRanges.Add(key, ranges);
            }
            if (ranges.Add((reference.ByteOffset, index)) && checkpointActive)
            {
                rangeUndo.Add((ranges, (reference.ByteOffset, index)));
            }
            maximumPacketBytes = Math.Max(maximumPacketBytes, checked(packet.Words.Length * 4));
        }

        public void Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null)
        {
            if (transaction is not null)
            {
                transaction.RequireActive();
                transaction.Writer.Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates);
                return;
            }
            ValidateInsertion(table.Count, bucket, reference, words, vertexPayload4);
            if (proxyCursor >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            int index = proxyCursor;
            AppendProxy(reference, words, flags20, vertexPayload4, moduleDraw, table[bucket], tableBuffer, LinkAddress(table[bucket], tableBuffer), false, projectedCoordinates);
            if (checkpointActive)
            {
                tableUndo.Add((bucket, table[bucket]));
            }
            table[bucket] = new(true, index);
        }

        public void PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
        {
            if (transaction is not null)
            {
                transaction.RequireActive();
                transaction.Writer.PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw);
                return;
            }
            ValidateInsertion(1, 0, reference, words, vertexPayload4);
            if (proxyCursor >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            AppendProxy(reference, words, flags20, vertexPayload4, moduleDraw, new(false, -1), 0, previousAddress, true);
        }

        private void AppendProxy(NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw, NativeBattlePrimitiveLink next, byte nextBuffer, uint previousAddress, bool physicalRead, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null)
        {
            InvalidatePacketRange(reference, words.Length * 4);
            lastHeaderLink = previousAddress;
            NativeBattleLogicalPacket packet = new(reference, words, next) { NextTableBuffer = nextBuffer, RequiresPhysicalRead = physicalRead, ProjectedCoordinates = projectedCoordinates };
            NativeBattlePrimitiveReference identity = NativeBattlePrimitiveProcess.NormalizeCommonReference(reference);
            if (packetIndices.TryGetValue(identity, out int existing))
            {
                if (checkpointActive)
                {
                    packetUndo.Add((existing, packets[existing]));
                }
                packets[existing] = packet;
                RegisterPacketRange(packet, existing);
            }
            else
            {
                packetIndices.Add(identity, packets.Count);
                RegisterPacketRange(packet, packets.Count);
                packets.Add(packet);
            }
            NativeBattleLogicalProxy proxy = new(reference, flags20, vertexPayload4, moduleDraw) { NextHigh22 = (byte)(LastHeaderLink >> 24) };
            if (proxyCursor < proxies.Count)
            {
                if (checkpointActive)
                {
                    proxyUndo.Add((proxyCursor, proxies[proxyCursor]));
                }
                proxies[proxyCursor] = proxy;
            }
            else
            {
                proxies.Add(proxy);
            }
            proxyCursor++;
        }

        public NativeBattleLogicalOrdering ToImmutable()
        {
            if (transaction is not null)
            {
                transaction.RequireActive();
                return transaction.View;
            }
            return new(table.ToImmutable(), proxies.ToImmutable(), packets.ToImmutable()) { ProxyCursor = proxyCursor, TableBuffer = tableBuffer, OtherTable = otherTable };
        }

        internal void BeginCheckpoint()
        {
            if (checkpointActive || transaction is not null)
            {
                throw new InvalidOperationException("Native ordering callback checkpoints cannot be nested.");
            }
            checkpointActive = true;
            checkpointProxyCursor = proxyCursor;
            checkpointProxyCount = proxies.Count;
            checkpointPacketCount = packets.Count;
            checkpointMaximumPacketBytes = maximumPacketBytes;
            checkpointLastHeaderLink = lastHeaderLink;
        }

        internal void CommitCheckpoint()
        {
            checkpointActive = false;
            tableUndo.Clear();
            proxyUndo.Clear();
            packetUndo.Clear();
            rangeUndo.Clear();
        }

        internal void RollbackCheckpoint()
        {
            if (!checkpointActive)
            {
                throw new InvalidOperationException("Native ordering rollback requires its current callback checkpoint.");
            }
            for (int index = checkpointPacketCount; index < packets.Count; index++)
            {
                packetIndices.Remove(NativeBattlePrimitiveProcess.NormalizeCommonReference(packets[index].Reference));
            }
            for (int index = rangeUndo.Count - 1; index >= 0; index--)
            {
                var entry = rangeUndo[index];
                entry.Ranges.Remove(entry.Value);
            }
            for (int index = packetUndo.Count - 1; index >= 0; index--)
            {
                var entry = packetUndo[index];
                packets[entry.Index] = entry.Value;
            }
            for (int index = proxyUndo.Count - 1; index >= 0; index--)
            {
                var entry = proxyUndo[index];
                proxies[entry.Index] = entry.Value;
            }
            for (int index = tableUndo.Count - 1; index >= 0; index--)
            {
                var entry = tableUndo[index];
                table[entry.Index] = entry.Value;
            }
            packets.Count = checkpointPacketCount;
            proxies.Count = checkpointProxyCount;
            proxyCursor = checkpointProxyCursor;
            maximumPacketBytes = checkpointMaximumPacketBytes;
            lastHeaderLink = checkpointLastHeaderLink;
            CommitCheckpoint();
        }
    }

    private static ImmutableArray<BattleSceneScreenPoint>? MatchingProjectedCoordinates(NativeBattleLogicalPacket packet, int word, int count)
    {
        if (packet.ProjectedCoordinates is not { } coordinates)
        {
            return null;
        }
        byte opcode = (byte)(packet.Words[word] >> 24);
        int corners = (opcode & 8) != 0 ? 4 : 3;
        bool gouraud = (opcode & 16) != 0;
        int stride = (gouraud ? 1 : 0) + 1 + ((opcode & 4) != 0 ? 1 : 0);
        int expected = corners * stride + (gouraud ? 0 : 1);
        if ((opcode & 0xE0) != 0x20 || coordinates.IsDefault || coordinates.Length != corners || word != 1 || count != expected || packet.Words.Length != count + 1 || (packet.Words[0] >> 24) != count)
        {
            return null;
        }
        for (int corner = 0; corner < corners; corner++)
        {
            int coordinateWord = word + 1 + corner * stride;
            BattleSceneScreenPoint point = coordinates[corner];
            uint packed = unchecked((ushort)point.X) | unchecked((uint)point.Y << 16);
            if (packet.Words[coordinateWord] != packed)
            {
                return null;
            }
        }
        return coordinates;
    }

    public ImmutableArray<NativeBattleLogicalCommand> Traverse(ImmutableArray<byte> wordLengths, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null, uint? initialOrderingAddress = null, Func<uint, NativeBattleAddressedOrderingRoot>? readAddressedRoot = null)
    {
        if (Table.IsDefault || Table.Length != NativeBattlePrimitiveOrdering.BattleTableLength || Proxies.IsDefault || Packets.IsDefault || wordLengths.IsDefault || wordLengths.Length != 256)
        {
            throw new InvalidDataException("Native logical OT requires its original4386 roots, proxy bank and command length table.");
        }
        Dictionary<NativeBattlePrimitiveReference, NativeBattleLogicalPacket> memory = Packets.ToDictionary(packet => NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference));
        HashSet<(byte Bank, bool Proxy, int Index)> visited = [];
        ImmutableArray<NativeBattleLogicalCommand>.Builder commands = ImmutableArray.CreateBuilder<NativeBattleLogicalCommand>();
        NativeBattlePrimitiveLink cursor = new(false, initialOrderingAddress is null ? Table.Length - 1 : -1);
        uint? addressedRoot = initialOrderingAddress;
        int bucket = Table.Length - 1;
        byte rootBuffer = TableBuffer ?? throw new NotSupportedException("Native OT traversal requires its retained table bank identity.");
        while (cursor.Index >= 0 || addressedRoot is not null)
        {
            if (addressedRoot is uint rootAddress)
            {
                if (!visited.Add((2, false, unchecked((int)rootAddress))))
                {
                    throw new InvalidDataException("Native logical OT contains an addressed-root link cycle.");
                }
                NativeBattleAddressedOrderingRoot root = (readAddressedRoot ?? throw new NotSupportedException("Native private ordering requires its actual addressed DWORD roots."))(rootAddress);
                bucket = root.Bucket;
                addressedRoot = null;
                Follow(root.NextAddress);
                continue;
            }
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
            if (!memory.TryGetValue(NativeBattlePrimitiveProcess.NormalizeCommonReference(proxy.Packet), out NativeBattleLogicalPacket? packet))
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
                    throw new InvalidDataException($"Native logical packet ends inside an original command: bank={packet.Reference.Bank}, buffer={packet.Reference.Buffer}, offset={packet.Reference.ByteOffset:X}, physical={packet.RequiresPhysicalRead}, bucket={bucket}, word={word}, opcode={opcode:X2}, count={count}, remaining={remaining}, words={string.Join(" ", packet.Words.Select(value => value.ToString("X8")))}.");
                }
                if (opcode != 0)
                {
                    commands.Add(new(packet.Reference, word, opcode, packet.Words.Slice(word, count), proxy.Flags20, proxy.VertexPayload4, proxy.ModuleDraw) { OrderingBucket = bucket, ProjectedCoordinates = MatchingProjectedCoordinates(packet, word, count) });
                }
                remaining -= count;
                word += count;
            }
            Follow(nextAddress);
        }
        return commands.ToImmutable();

        void Follow(uint nextAddress)
        {
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
            else if (readAddressedRoot is not null && nextAddress >= NativeBattleEffectNodeMemory.ArenaAddress && nextAddress - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - 4 && (nextAddress & 3) == 0)
            {
                addressedRoot = nextAddress;
                cursor = new(false, -1);
            }
            else
            {
                throw new NotSupportedException("Native packet alias produced a link outside the observed proxy and OT banks.");
            }
        }
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

public interface INativeBattleLogicalExecutionContext
{
    uint LiveMode { get; }
    uint LiveDebug { get; }
    bool HardwareEnabled { get; }
    NativeSoftwarePrimitiveTable SoftwareTable { get; }
    NativeBattlePcTextureWindowCache? Cache { get; set; }
    void ExecuteSoftware(NativeBattleLogicalCommand command);
    void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass);
    NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode);
    NativeBattleLogicalResolvedCommand RetainTexturePixels(NativeBattleLogicalResolvedCommand command) => command;
    void BeginPass(byte pass)
    {
    }
    void BeginOrdering()
    {
    }
    void FlushSoftwareImage()
    {
    }
    void EndBatch();
}

public static class NativeBattleLogicalPasses
{
    public static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache? Cache) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags, NativeBattlePcTextureWindowCache? cache = null, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        return ExecuteCore(ordering, tables, initial, drawFlags, cache, null, readPhysical);
    }

    public static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache? Cache) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, INativeBattleLogicalExecutionContext context, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExecuteCore(ordering, tables, initial, 0, context.Cache, context, readPhysical);
    }

    private static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache? Cache) ExecuteCore(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags, NativeBattlePcTextureWindowCache? cache, INativeBattleLogicalExecutionContext? context, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical)
    {
        tables.Validate();
        if (ordering.ProxyCursor >= NativeBattlePrimitiveOrdering.ProxyCapacity)
        {
            context?.EndBatch();
            return (initial, [], context is null ? cache : context.Cache);
        }
        context?.BeginOrdering();
        ImmutableArray<NativeBattleLogicalCommand> commands = ordering.Traverse(tables.WordLengths, readPhysical);
        NativeBattlePrimitiveDrawMode mode = context?.SynchronizeDrawMode(initial) ?? initial;
        ImmutableArray<NativeBattleLogicalResolvedCommand>.Builder executed = ImmutableArray.CreateBuilder<NativeBattleLogicalResolvedCommand>();
        ImmutableArray<uint>[] passes = [tables.PreprocessingHandlers, tables.FirstHandlers, tables.SecondHandlers];
        for (byte pass = 0; pass < passes.Length; pass++)
        {
            if (pass == 1 && context?.HardwareEnabled == false)
            {
                break;
            }
            context?.BeginPass(pass);
            SetCache(BeginTexturePass(GetCache(), pass));
            foreach (NativeBattleLogicalCommand command in commands)
            {
                if (pass == 0 && context is not null && (context.LiveMode & 2) != 0 && context.SoftwareTable.Handler(command.Opcode) != 0)
                {
                    context.ExecuteSoftware(command);
                    mode = context.SynchronizeDrawMode(mode);
                }
                bool category = tables.FlaggedCommands[command.Opcode] != 0;
                uint liveMode = context?.LiveMode ?? drawFlags;
                bool hardware = pass != 0 || context?.HardwareEnabled != false;
                if (passes[pass][command.Opcode] != 0 && (!category || (liveMode & 1) != 0 && hardware))
                {
                    mode = mode.ExecuteWords(command.Opcode, command.Words);
                    mode = ClearTextureSelection(mode);
                    if (context is not null)
                    {
                        context.ExecuteNormal(command, mode, pass);
                        mode = ClearTextureSelection(context.SynchronizeDrawMode(mode));
                    }
                    NativeBattleLogicalResolvedCommand resolved = new(command, mode, pass);
                    if (GetCache() is { } currentCache)
                    {
                        var selected = SelectTextureCommand(resolved, currentCache);
                        resolved = selected.Command;
                        SetCache(selected.Cache);
                    }
                    executed.Add(context?.RetainTexturePixels(resolved) ?? resolved);
                }
                if (pass == 0 && category && (liveMode & 1) != 0 && context is not null)
                {
                    for (int delivery = 0; delivery < 2; delivery++)
                    {
                        if (context.LiveDebug != 0 && context.SoftwareTable.Handler(command.Opcode) != 0)
                        {
                            context.ExecuteSoftware(command);
                            mode = context.SynchronizeDrawMode(mode);
                        }
                    }
                }
            }
            SetCache(EndTexturePass(GetCache(), pass));
        }
        context?.EndBatch();
        return (mode, executed.ToImmutable(), GetCache());

        NativeBattlePcTextureWindowCache? GetCache() => context is null ? cache : context.Cache;
        void SetCache(NativeBattlePcTextureWindowCache? value)
        {
            if (context is null)
            {
                cache = value;
            }
            else
            {
                context.Cache = value;
            }
        }
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
            cache = BeginTexturePass(cache, pass)!;
            foreach (NativeBattleLogicalResolvedCommand command in commands.Where(command => command.Pass == pass))
            {
                var item = SelectTextureCommand(command, cache);
                cache = item.Cache;
                selected.Add(item.Command);
            }
            cache = EndTexturePass(cache, pass)!;
        }
        return (selected.ToImmutable(), cache);
    }

    private static NativeBattlePcTextureWindowCache? BeginTexturePass(NativeBattlePcTextureWindowCache? cache, byte pass) => pass == 1 ? cache?.Prepare464BD0().Cache : cache;
    private static NativeBattlePcTextureWindowCache? EndTexturePass(NativeBattlePcTextureWindowCache? cache, byte pass) => pass == 1 ? cache?.Flush465930() : cache;

    private static (NativeBattleLogicalResolvedCommand Command, NativeBattlePcTextureWindowCache Cache) SelectTextureCommand(NativeBattleLogicalResolvedCommand command, NativeBattlePcTextureWindowCache cache)
    {
        NativeBattlePrimitiveDrawMode mode = ClearTextureSelection(command.DrawMode);
        if ((command.Command.Opcode & 0xE4) == 0x24)
        {
            ushort page = mode.TexturePage ?? throw new NotSupportedException("Native textured pass requires its executed texture page.");
            ushort clut = unchecked((ushort)(command.Command.Words[2] >> 16));
            if (command.Pass == 0)
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
        return (ReferenceEquals(command.DrawMode, mode) ? command : command with { DrawMode = mode }, cache);
    }

    private static NativeBattlePrimitiveDrawMode ClearTextureSelection(NativeBattlePrimitiveDrawMode mode)
    {
        return mode.TextureSelection is null && mode.TexturePixelSnapshot is null ? mode : mode with { TextureSelection = null, TexturePixelSnapshot = null };
    }
}
