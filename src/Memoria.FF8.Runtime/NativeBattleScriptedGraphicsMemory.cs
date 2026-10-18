using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Separate process-retained depth workspace, view backup and depth-cue controls.</summary>
public sealed record NativeBattleScriptedGraphicsMemory([property: JsonRequired] NativeBattleEffectArena Depths, [property: JsonRequired] ImmutableArray<byte> ModuleViewBackup, [property: JsonRequired] short Dqa, [property: JsonRequired] int Dqb)
{
    public const uint DepthAddress = 0x2798C18;
    public const int DepthLength = 0x2A8;
    public const uint ViewBackupAddress = 0x2796F90;

    public static NativeBattleScriptedGraphicsMemory FromColdProcess() => new(NativeBattleEffectArena.Clear(), new byte[32].ToImmutableArray(), 0, 0);

    public void Validate()
    {
        if (Depths is null)
        {
            throw new InvalidDataException("Native scripted graphics requires its retained depth workspace.");
        }
        Depths.Validate();
        if (Depths.Regions.Any(region => region.Offset > DepthLength - region.Data.Length) || Depths.UnknownRegions.Any(region => region.Offset > DepthLength - region.Length) || ModuleViewBackup.IsDefault || ModuleViewBackup.Length != 32)
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
