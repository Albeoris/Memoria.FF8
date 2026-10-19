using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePrimitiveTemporaryLanes([property: JsonRequired] ushort? Word2E, [property: JsonRequired] ushort? Word44, [property: JsonRequired] ushort? Word46)
{
    public static NativeBattlePrimitiveTemporaryLanes FromColdProcess() => new(0, 0, 0);

    [JsonIgnore]
    public uint Dword44 => Word44 is ushort low && Word46 is ushort high ? low | (uint)high << 16 : throw new NotSupportedException("Native temporary DWORD44 has no complete retained physical writer.");

    public NativeBattlePrimitiveTemporaryLanes WriteDword44(uint value) => this with { Word44 = unchecked((ushort)value), Word46 = unchecked((ushort)(value >> 16)) };
}

public sealed partial record NativeBattlePrimitiveProcess
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePrimitiveTemporaryLanes? Temporary5082B0Lanes { get; init; }
}
