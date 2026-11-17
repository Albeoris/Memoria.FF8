using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original model sequence resource and bounded source metadata.</summary>
public sealed partial class BattleSequenceSet
{
    private readonly byte[] bytes;
    private ImmutableArray<NativeBattleSequenceReference> modelReferences;
    private NativeBattleGraphicsResource? nativeResource;

    public NativeBattleGraphicsResource NativeResource => nativeResource ??= new(bytes.ToImmutableArray());

    public BattleSequenceSet(byte[] bytes)
    {
        if (bytes.Length < 2 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) * 2 + 2 > bytes.Length)
        {
            throw new InvalidDataException("Original battle sequence directory exceeds its section.");
        }
        this.bytes = bytes.ToArray();
    }

    public bool Contains(int sequence) => sequence > 0 && sequence <= BinaryPrimitives.ReadUInt16LittleEndian(bytes) && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(sequence * 2)) != 0;

    public ImmutableArray<NativeBattleSequenceReference> ReadModelReferences()
    {
        if (modelReferences.IsDefault)
        {
            modelReferences = NativeBattleSequenceDependencies.Read(NativeResource);
        }
        return modelReferences;
    }
}
