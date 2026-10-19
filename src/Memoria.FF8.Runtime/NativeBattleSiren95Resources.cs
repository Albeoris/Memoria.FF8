using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95Resources(ImmutableArray<byte> IoScript, ImmutableArray<NativeBattleEffectFileAsset> Files)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMoogle96VibrationResource? Vibration { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? SoundDescriptors { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? CameraPrograms { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGfAnimationSource? PhaseOneSource { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? PhaseOneSprites { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? PhaseTwoSprites { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFullscreenOverlayProgram? OverlayProgram { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? NodeSchedule { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? StaticNodeSource { get; init; }

    public void Validate()
    {
        Vibration?.Bank.Validate();
        if (IoScript.IsDefault || IoScript.Length != 0x180 || Files.IsDefault || Files.Any(file => file is null || file.Data.IsDefault) || Files.Select(file => file.FileId).Distinct().Count() != Files.Length)
        {
            throw new InvalidDataException("Native Siren resources require the complete IO programme and distinct original archive files.");
        }
    }
}
