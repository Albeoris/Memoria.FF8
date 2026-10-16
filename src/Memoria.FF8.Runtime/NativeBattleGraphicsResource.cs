using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original model sequence directory/bytecode at graphics resource+12.</summary>
public sealed record NativeBattleGraphicsResource(ImmutableArray<byte> SequenceSection)
{
    public void Validate()
    {
        if (SequenceSection.IsDefault || SequenceSection.Length < 2 || BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan()) * 2 + 2 > SequenceSection.Length)
        {
            throw new InvalidDataException("Native graphics resource lacks its original bounded sequence directory.");
        }
    }

    public bool Contains(byte sequence)
    {
        Validate();
        return sequence != 0 && sequence <= BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan()) && BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan().Slice(sequence * 2)) != 0;
    }
}
