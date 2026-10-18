using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97Context([property: JsonRequired] ImmutableArray<byte> Data)
{
    public static NativeBattleChoco97Context Clear72A350() => new(new byte[0x54].ToImmutableArray());

    public void Validate()
    {
        if (Data.IsDefault || Data.Length != 0x54)
        {
            throw new InvalidDataException("Native Choco requires its complete original context owner.");
        }
    }

    public ushort Word(int offset)
    {
        Validate();
        return BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan().Slice(offset, 2));
    }

    public NativeBattleChoco97Context WriteWord(int offset, ushort value)
    {
        Validate();
        byte[] data = Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
        return this with { Data = data.ToImmutableArray() };
    }
}
