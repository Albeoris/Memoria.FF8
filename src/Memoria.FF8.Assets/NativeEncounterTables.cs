using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Verified installed English executable tables consumed by47CA90 and541C80.</summary>
public sealed record NativeEncounterTables(ImmutableArray<byte> FieldRandom, ImmutableArray<byte> WorldRandom, ImmutableArray<byte> WorldWeights)
{
    public static NativeEncounterTables ReadExecutable(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeEncounterTables Read(IGameNativeMemorySource memory)
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(memory.SourceHash))
        {
            throw new NotSupportedException("Native encounter tables require authenticated native memory.");
        }
        return new(memory.Read(0xB80A18, 256).ToImmutableArray(), memory.Read(0xC75D20, 256).ToImmutableArray(), memory.Read(0xC75F10, 8).ToImmutableArray());
    }

    public static NativeFieldEncounterDefinition ReadField(byte[] rate, byte[] formations)
    {
        if (rate.Length < 1 || formations.Length < 8)
        {
            throw new InvalidDataException("Original field encounters require RAT byte0 and four MRT formation WORDs.");
        }
        ImmutableArray<ushort>.Builder values = ImmutableArray.CreateBuilder<ushort>(4);
        for (int index = 0; index < 4; index++)
        {
            values.Add(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(formations.AsSpan(index * 2, 2)));
        }
        return new(rate[0], values.MoveToImmutable());
    }
}
