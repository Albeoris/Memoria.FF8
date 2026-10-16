using System.Buffers.Binary;

namespace Albeoris.Games.FF8.Jsm;

/// <summary>
/// A lossless editable representation of a JSM file.
/// </summary>
public sealed class JsmDocument
{
    private readonly Byte[] _source;

    internal JsmDocument(
        Byte[] source,
        IReadOnlyList<Jsm.GameObject> objects,
        IReadOnlyList<Instructions.IJsmInstruction> instructions,
        IReadOnlyList<JsmOperation> operations)
    {
        _source = source;
        Objects = objects;
        Instructions = instructions;
        Operations = operations;
    }

    public IReadOnlyList<Jsm.GameObject> Objects { get; }

    public IReadOnlyList<Instructions.IJsmInstruction> Instructions { get; }

    public IReadOnlyList<JsmOperation> Operations { get; }

    public Byte[] Write()
    {
        Byte[] result = (Byte[])_source.Clone();
        foreach (JsmOperation operation in Operations)
            operation.WriteTo(result.AsSpan(operation.Offset, sizeof(Int32)));

        return result;
    }

    public void Write(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(Write());
    }

    public static IReadOnlyList<JsmOperation> ReadOperations(Byte[] source)
    {
        const Int32 operationsOffsetPosition = 6;
        if (source.Length < operationsOffsetPosition + sizeof(UInt16))
            throw new InvalidDataException("The JSM header is incomplete.");

        Int32 operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(
            source.AsSpan(operationsOffsetPosition, sizeof(UInt16)));
        if (operationsOffset > source.Length)
            throw new InvalidDataException("The JSM operations offset is outside the file.");

        Int32 operationCount = (source.Length - operationsOffset) / sizeof(Int32);
        JsmOperation[] operations = new JsmOperation[operationCount];

        for (Int32 index = 0; index < operations.Length; index++)
        {
            Int32 offset = operationsOffset + index * sizeof(Int32);
            Int32 encodedValue = BinaryPrimitives.ReadInt32LittleEndian(
                source.AsSpan(offset, sizeof(Int32)));
            operations[index] = new JsmOperation(index, offset, encodedValue);
        }

        return operations;
    }
}
