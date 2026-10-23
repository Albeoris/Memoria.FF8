using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGeometryRegisters([property: JsonRequired] ImmutableArray<byte?> Rotation20, [property: JsonRequired] ImmutableArray<int?> Translation, [property: JsonRequired] ImmutableArray<byte?> V1, [property: JsonRequired] ImmutableArray<byte?> V2, [property: JsonRequired] ImmutableArray<uint?> Sxy, [property: JsonRequired] ImmutableArray<uint?> Sz, [property: JsonRequired] uint? Mac0, [property: JsonRequired] uint? Otz, [property: JsonRequired] ImmutableArray<byte?> AnchorScratch8)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? Lzcs { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? Lzcr { get; init; }
    public static NativeBattleGeometryRegisters Unknown() => new(new byte?[20].ToImmutableArray(), [null, null, null], new byte?[8].ToImmutableArray(), new byte?[8].ToImmutableArray(), [null, null, null], [null, null, null, null], null, null, new byte?[8].ToImmutableArray());

    public void Validate()
    {
        if (Rotation20.IsDefault || Rotation20.Length != 20 || Translation.IsDefault || Translation.Length != 3 || V1.IsDefault || V1.Length != 8 || V2.IsDefault || V2.Length != 8 || Sxy.IsDefault || Sxy.Length != 3 || Sz.IsDefault || Sz.Length != 4 || AnchorScratch8.IsDefault || AnchorScratch8.Length != 8)
        {
            throw new InvalidDataException("Native geometry requires its exact retained control, input, FIFO and scratch widths.");
        }
    }
}

public sealed record NativeBattleGeometryRegisterStep(NativeBattleGeometryRegisters Geometry, NativeBattleScriptedColorRegisters Colors);

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGeometryRegisters? GeometryRegisters { get; init; }
}
