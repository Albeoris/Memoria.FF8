using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattlePrimitiveProcess
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? Temporary5082B0Word4 { get; init; }

    public NativeBattlePrimitiveProcess PublishTemporary5082B0Matrix(NativeBattleActionMatrix matrix)
    {
        return this with { Temporary5082B0Word4 = unchecked((ushort)matrix.Rotation[2]) | unchecked((uint)(ushort)matrix.Rotation[3] << 16) };
    }
}
