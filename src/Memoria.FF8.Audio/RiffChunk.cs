using System.Buffers.Binary;
using System.Text;

namespace Memoria.FF8.Audio;

public sealed record RiffChunk(string Id, string? Type, int Offset, ReadOnlyMemory<byte> Data, IReadOnlyList<RiffChunk> Children)
{
    public static RiffChunk Read(byte[] bytes, string type)
    {
        IReadOnlyList<RiffChunk> chunks = ReadChildren(bytes, 0, bytes.Length);
        if (chunks.Count != 1 || chunks[0].Id != "RIFF" || chunks[0].Type != type)
        {
            throw new InvalidDataException($"Expected a single RIFF {type} form.");
        }
        return chunks[0];
    }

    public RiffChunk Required(string id)
    {
        return Children.SingleOrDefault(chunk => chunk.Id == id) ?? throw new InvalidDataException($"Missing {id} chunk in {Type ?? Id}.");
    }

    public IEnumerable<RiffChunk> Descendants()
    {
        foreach (RiffChunk child in Children)
        {
            yield return child;
            foreach (RiffChunk descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    internal static IReadOnlyList<RiffChunk> ReadChildren(ReadOnlyMemory<byte> bytes, int start, int end)
    {
        List<RiffChunk> chunks = [];
        for (int offset = start; offset < end;)
        {
            if (end - offset < 8)
            {
                throw new InvalidDataException("Truncated RIFF chunk header.");
            }
            string id = Encoding.ASCII.GetString(bytes.Span.Slice(offset, 4));
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span[(offset + 4)..]);
            long limit = (long)offset + 8 + length;
            if (limit > end)
            {
                throw new InvalidDataException($"RIFF {id} exceeds its containing chunk.");
            }
            bool container = id is "LIST" or "RIFF";
            if (container && length < 4)
            {
                throw new InvalidDataException("RIFF form is missing its type.");
            }
            string? type = container ? Encoding.ASCII.GetString(bytes.Span.Slice(offset + 8, 4)) : null;
            chunks.Add(new RiffChunk(id, type, offset, bytes.Slice(offset + 8, (int)length), container ? ReadChildren(bytes, offset + 12, (int)limit) : []));
            offset = checked((int)(limit + (length & 1)));
            if (offset > end)
            {
                throw new InvalidDataException("RIFF chunk is missing word padding.");
            }
        }
        return chunks;
    }
}
