using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325LateResources([property: JsonRequired] ImmutableArray<byte> StaticData)
{
    public const uint Address = 0xFDBDF8;
    public const int Length = 0x51BD;

    public void Validate()
    {
        if (StaticData.IsDefault || StaticData.Length != Length)
        {
            throw new InvalidDataException("Native Diablos late effects require their complete original static table region.");
        }
    }

    public ReadOnlySpan<byte> Read(uint address, int length)
    {
        Validate();
        long offset = (long)address - Address;
        if (length < 0 || offset < 0 || offset > StaticData.Length - length)
        {
            throw new InvalidDataException("Native Diablos static table read leaves its retained original resource.");
        }
        return StaticData.AsSpan().Slice((int)offset, length);
    }
}
