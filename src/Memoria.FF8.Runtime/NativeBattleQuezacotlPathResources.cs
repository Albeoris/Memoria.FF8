using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleQuezacotlResources
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleQuezacotlPathResources? Paths { get; init; }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? QuezacotlTargetY1774 { get; init; }
}

public sealed record NativeBattleQuezacotlPathResources(uint Address, ImmutableArray<byte> Data)
{
    public void Validate()
    {
        if (Address != 0x12AFE58 || Data.IsDefault || Data.Length != 1944 || ReadWord(Address) != 40)
        {
            throw new InvalidDataException("Native6C76C0 requires its complete original40-path12AFE58 bank.");
        }
        for (int index = 0; index < 40; index++)
        {
            short offset = ReadWord(checked(Address + 2u + (uint)index * 2));
            if (offset < 41 || offset >= Data.Length / 2)
            {
                throw new InvalidDataException("Native6C76C0 path offset leaves its original static bank.");
            }
        }
    }

    public short ReadWord(uint address)
    {
        if (Data.IsDefault || address < Address || address - Address > Data.Length - 2 || ((address - Address) & 1) != 0)
        {
            throw new NotSupportedException("Native6C7800 source pointer leaves its authenticated path WORD bank.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(Data.AsSpan().Slice(checked((int)(address - Address))));
    }
}
