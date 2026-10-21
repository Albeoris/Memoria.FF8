using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained MAC, IR, RGB FIFO and flag registers touched by the cinematic normal-color instruction.</summary>
public sealed record NativeBattleScriptedColorRegisters([property: JsonRequired] ImmutableArray<int?> Mac, [property: JsonRequired] ImmutableArray<int?> Ir, [property: JsonRequired] ImmutableArray<uint?> Rgb, [property: JsonRequired] uint? Flags)
{
    public ImmutableArray<byte>? NormalInput { get; init; }
    public uint? Material { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? Ir0 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<bool>? NormalInputKnown { get; init; }

    public static NativeBattleScriptedColorRegisters Unknown() => new([null, null, null], [null, null, null], [null, null, null], null);

    public void Validate()
    {
        if (NormalInput is { } normal && (normal.IsDefault || normal.Length != 8))
        {
            throw new InvalidDataException("Native normal color input requires both actual packed V0 DWORDs.");
        }
        if (NormalInputKnown is { } known && (NormalInput is null || known.IsDefault || known.Length != 8))
        {
            throw new InvalidDataException("Native normal color requires the actual known mask for its packed input.");
        }
        if (Mac.IsDefault || Ir.IsDefault || Rgb.IsDefault || Mac.Length != 3 || Ir.Length != 3 || Rgb.Length != 3)
        {
            throw new InvalidDataException("Native normal color requires its three MAC, IR and RGB FIFO register lanes.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScriptedColorRegisters? ScriptedColorRegisters { get; init; }
}
