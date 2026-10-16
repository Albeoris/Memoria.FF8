using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Portable identity of one original primitive arena; heap addresses are deliberately not invented.</summary>
public sealed record NativeBattlePrimitiveReference(string Bank, byte Buffer, int ByteOffset);
public readonly record struct NativeBattlePrimitiveLink(bool Proxy, int Index);
public sealed record NativeBattleLogicalPacket(NativeBattlePrimitiveReference Reference, ImmutableArray<uint> Words, NativeBattlePrimitiveLink Next);
public sealed record NativeBattleLogicalProxy(NativeBattlePrimitiveReference Packet, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw);
public sealed record NativeBattleLogicalCommand(NativeBattlePrimitiveReference Packet, int WordOffset, byte Opcode, ImmutableArray<uint> Words, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public int? OrderingBucket { get; init; }
}
public sealed record NativeBattleLogicalResolvedCommand(NativeBattleLogicalCommand Command, NativeBattlePrimitiveDrawMode DrawMode, byte Pass);

/// <summary>45CC80/45C8E0/45C870 links using logical bank references with the same physical alias and capacity behavior.</summary>
public sealed record NativeBattleLogicalOrdering(ImmutableArray<NativeBattlePrimitiveLink> Table, ImmutableArray<NativeBattleLogicalProxy> Proxies, ImmutableArray<NativeBattleLogicalPacket> Packets)
{
    public static NativeBattleLogicalOrdering Clear() => new(Enumerable.Range(0, NativeBattlePrimitiveOrdering.BattleTableLength).Select(index => new NativeBattlePrimitiveLink(false, index - 1)).ToImmutableArray(), [], []);

    public NativeBattleLogicalOrdering Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
    {
        if ((uint)bucket >= Table.Length || string.IsNullOrWhiteSpace(reference.Bank) || reference.Buffer > 1 || reference.ByteOffset < 0 || (reference.ByteOffset & 3) != 0 || words.IsDefaultOrEmpty || words.Length < 1 + (words[0] >> 24) || vertexPayload4.IsDefault || vertexPayload4.Length is not (0 or 4))
        {
            throw new InvalidDataException("Native logical primitive insertion lacks its original bounded packet or bank identity.");
        }
        if (Proxies.Length >= NativeBattlePrimitiveOrdering.ProxyCapacity)
        {
            return this;
        }
        NativeBattleLogicalPacket packet = new(reference, words, Table[bucket]);
        int existing = -1;
        for (int index = 0; index < Packets.Length; index++)
        {
            if (Packets[index].Reference == reference)
            {
                existing = index;
                break;
            }
        }
        return this with
        {
            Table = Table.SetItem(bucket, new(true, Proxies.Length)),
            Proxies = Proxies.Add(new(reference, flags20, vertexPayload4, moduleDraw)),
            Packets = existing < 0 ? Packets.Add(packet) : Packets.SetItem(existing, packet)
        };
    }

    public ImmutableArray<NativeBattleLogicalCommand> Traverse(ImmutableArray<byte> wordLengths)
    {
        if (Table.IsDefault || Table.Length != NativeBattlePrimitiveOrdering.BattleTableLength || Proxies.IsDefault || Packets.IsDefault || wordLengths.IsDefault || wordLengths.Length != 256)
        {
            throw new InvalidDataException("Native logical OT requires its original4386 roots, proxy bank and command length table.");
        }
        Dictionary<NativeBattlePrimitiveReference, NativeBattleLogicalPacket> memory = Packets.ToDictionary(packet => packet.Reference);
        HashSet<NativeBattlePrimitiveLink> visited = [];
        ImmutableArray<NativeBattleLogicalCommand>.Builder commands = ImmutableArray.CreateBuilder<NativeBattleLogicalCommand>();
        NativeBattlePrimitiveLink cursor = new(false, Table.Length - 1);
        int bucket = Table.Length - 1;
        while (cursor.Index >= 0)
        {
            if (!visited.Add(cursor))
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
                cursor = Table[cursor.Index];
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
            cursor = packet.Next;
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
    public static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags)
    {
        tables.Validate();
        ImmutableArray<NativeBattleLogicalCommand> commands = ordering.Traverse(tables.WordLengths);
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
                executed.Add(new(command, mode, pass));
            }
        }
        return (mode, executed.ToImmutable());
    }
}
