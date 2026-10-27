using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Separate process-retained depth workspace, view backup and depth-cue controls.</summary>
public sealed record NativeBattleScriptedGraphicsMemory([property: JsonRequired] NativeBattleEffectArena Depths, [property: JsonRequired] ImmutableArray<byte> ModuleViewBackup, [property: JsonRequired] short Dqa, [property: JsonRequired] int Dqb)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? Zsf3 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? Zsf4 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? DepthScaleBits1877DA8 { get; init; }

    public NativeBattleScriptedGraphicsMemory InitializeGeom56BAA0() => this with { Zsf3 = 0x155, Zsf4 = 0x100, Dqa = 0, Dqb = 0 };

    public const uint DepthAddress = 0x2798C18;
    // The remaining mapped process window bounds retained depth writes; it is not an array allocation size.
    public const int DepthLength = 0x63E8;
    public const uint ViewBackupAddress = 0x2796F90;

    public static NativeBattleScriptedGraphicsMemory FromColdProcess() => new(NativeBattleEffectArena.Clear(), new byte[32].ToImmutableArray(), 0, 0) { Zsf3 = 0, Zsf4 = 0, DepthScaleBits1877DA8 = 0x3F800000 };

    public void Validate()
    {
        if (Depths is null)
        {
            throw new InvalidDataException("Native scripted graphics requires its retained depth workspace.");
        }
        Depths.Validate();
        Depths.ValidateExtent(DepthLength);
        if (ModuleViewBackup.IsDefault || ModuleViewBackup.Length != 32)
        {
            throw new InvalidDataException("Native scripted graphics requires its bounded depth workspace and complete module view backup.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScriptedGraphicsMemory? ScriptedGraphics { get; init; }
}
