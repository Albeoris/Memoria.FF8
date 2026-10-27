using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>Reads native map-addressed Information headers and their two bounded strings.</summary>
internal static class MngrpTextBlockCodec
{
    private const Int32 BlockHeaderLength = 8;
    private sealed record OriginalLayout(Byte[] Body, Byte[] State, List<Int32> Starts, Byte[] Leading);
    private static readonly ConditionalWeakTable<MngrpTextBlockSection, OriginalLayout> OriginalLayouts = new();

    public static MngrpTextBlockSection Read(Int32 slotIndex, ReadOnlySpan<Byte> body, Encoding encoding, IReadOnlyList<Int32> referencedOffsets, Int32 referenceCount, out List<Int32> blockStarts)
    {
        MngrpTextBlockSection section = new(slotIndex);
        blockStarts = referencedOffsets.Distinct().Order().ToList();
        Byte[] leading = blockStarts.Count > 0 ? body[..Math.Clamp(blockStarts[0], 0, body.Length)].ToArray() : [];
        Int32 lastEnd = 0;
        for (Int32 index = 0; index < blockStarts.Count; index++)
        {
            Int32 start = blockStarts[index];
            if (start < 0 || start > body.Length - BlockHeaderLength)
            {
                throw new InvalidDataException($"Mapped text block {start} has no complete native header.");
            }
            Int32 titleEnd = body[(start + BlockHeaderLength)..].IndexOf((Byte)0);
            if (titleEnd < 0)
            {
                throw new InvalidDataException("Mapped Information title has no parent-bounded terminator.");
            }
            titleEnd += start + BlockHeaderLength;
            Int32 bodyEnd = body[(titleEnd + 1)..].IndexOf((Byte)0);
            if (bodyEnd < 0)
            {
                throw new InvalidDataException("Mapped Information body has no parent-bounded terminator.");
            }
            bodyEnd += titleEnd + 1;
            MngrpTextBlock block = new()
            {
                OriginId = BinaryPrimitives.ReadUInt16LittleEndian(body[start..]),
                LeftId = BinaryPrimitives.ReadUInt16LittleEndian(body[(start + 2)..]),
                RightId = BinaryPrimitives.ReadUInt16LittleEndian(body[(start + 4)..])
            };
            if (new[] { block.OriginId, block.LeftId, block.RightId }.Any(link => link != MngrpTextBlock.NoLink && link >= referenceCount))
            {
                throw new InvalidDataException("Mapped Information navigation selects an absent logical node.");
            }
            block.Texts.Add(MngrpTextSpanCodec.ReadText(body[(start + BlockHeaderLength)..titleEnd], encoding));
            block.Texts.Add(MngrpTextSpanCodec.ReadText(body[(titleEnd + 1)..bodyEnd], encoding));
            Int32 storageEnd = index + 1 < blockStarts.Count ? blockStarts[index + 1] : bodyEnd + 1;
            if (storageEnd > body.Length)
            {
                throw new InvalidDataException("Mapped text block storage exceeds its parent resource.");
            }
            if (storageEnd > bodyEnd + 1)
            {
                block.TrailingBytes = body[(bodyEnd + 1)..storageEnd].ToArray();
            }
            section.Blocks.Add(block);
            lastEnd = Math.Max(lastEnd, bodyEnd + 1);
        }
        section.TrailingData = MngrpFormat.TrimTrailingZeros(body[lastEnd..]).ToArray();
        OriginalLayouts.Add(section, new OriginalLayout(body.ToArray(), State(section, encoding), blockStarts.ToList(), leading));
        return section;
    }

    public static void Write(MngrpTextBlockSection section, Encoding encoding, MngrpByteWriter writer)
    {
        if (OriginalLayouts.TryGetValue(section, out OriginalLayout? original))
        {
            if (State(section, encoding).AsSpan().SequenceEqual(original.State))
            {
                writer.WriteBytes(original.Body);
                return;
            }
            writer.WriteBytes(original.Leading);
        }
        foreach (MngrpTextBlock block in section.Blocks)
        {
            if (block.Texts.Count != 2)
            {
                throw new InvalidDataException("Native Information blocks require a title and a body.");
            }
            writer.WriteUInt16(block.OriginId);
            writer.WriteUInt16(block.LeftId);
            writer.WriteUInt16(block.RightId);
            writer.WriteUInt16(MngrpFormat.ToUInt16(MeasureBlock(block, encoding), "text block length"));
            foreach (MngrpText text in block.Texts)
            {
                writer.WriteBytes(MngrpTextSpanCodec.GetBytes(text, encoding).Span);
                writer.WriteZeros(1);
            }
            writer.WriteBytes(block.TrailingBytes);
        }
        writer.WriteBytes(section.TrailingData);
    }

    public static List<Int32> ComputeBlockStarts(MngrpTextBlockSection section, Encoding encoding)
    {
        Int32 position = 0;
        if (OriginalLayouts.TryGetValue(section, out OriginalLayout? original))
        {
            if (State(section, encoding).AsSpan().SequenceEqual(original.State))
            {
                return original.Starts.ToList();
            }
            position = original.Leading.Length;
        }
        List<Int32> starts = new(section.Blocks.Count);
        foreach (MngrpTextBlock block in section.Blocks)
        {
            starts.Add(position);
            position = checked(position + MeasureBlock(block, encoding));
        }
        return starts;
    }

    private static Int32 MeasureBlock(MngrpTextBlock block, Encoding encoding) => BlockHeaderLength + block.Texts.Sum(text => MngrpTextSpanCodec.GetBytes(text, encoding).Length + 1) + block.TrailingBytes.Length;

    private static Byte[] State(MngrpTextBlockSection section, Encoding encoding)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(section.Blocks.Count);
        foreach (MngrpTextBlock block in section.Blocks)
        {
            writer.Write(block.OriginId);
            writer.Write(block.LeftId);
            writer.Write(block.RightId);
            writer.Write(block.Texts.Count);
            foreach (MngrpText text in block.Texts)
            {
                ReadOnlyMemory<Byte> bytes = MngrpTextSpanCodec.GetBytes(text, encoding);
                writer.Write(bytes.Length);
                writer.Write(bytes.Span);
            }
            writer.Write(block.TrailingBytes.Length);
            writer.Write(block.TrailingBytes);
        }
        writer.Write(section.TrailingData.Length);
        writer.Write(section.TrailingData);
        return stream.ToArray();
    }
}
