using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Original PC save bytes retained for fields without a runtime owner.</summary>
public sealed record NativePcSaveData([property: JsonRequired] byte[] Block)
{
    public IReadOnlyDictionary<int, string> Names { get; init; } = new Dictionary<int, string>();
    public const int BlockSize = 8192;
    public const int SaveMapOffset = 384;
    public const int MainOffset = 464;
    public const int EventBankOffset = 3808;

    [JsonIgnore]
    public byte[] FieldParty => Block.AsSpan(MainOffset + 3304, 3).ToArray();

    public void Validate()
    {
        if (Block is not { Length: BlockSize } || Block[0] != 'S' || Block[1] != 'C' || BinaryPrimitives.ReadUInt16LittleEndian(Block.AsSpan(SaveMapOffset + 2)) != 0x08FF)
        {
            throw new InvalidDataException("An original PC save requires its complete committed save image.");
        }
    }
}
