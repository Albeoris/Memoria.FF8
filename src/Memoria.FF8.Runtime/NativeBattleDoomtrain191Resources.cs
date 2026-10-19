using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191Resources(NativeBattleEffectHeapAllocation ModelTim, NativeBattleEffectHeapAllocation CameraProgram, NativeBattleMoogle96VibrationResource Vibration, NativeBattleEffectHeapAllocation Sounds, int InitialStream, ImmutableArray<NativeBattleEffectFileAsset> Files)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191RigResources? OpeningRigs { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191SkyResources? Sky { get; init; }

    public void Validate()
    {
        Vibration.Bank.Validate();
        if (ModelTim.Data.IsDefaultOrEmpty || CameraProgram.Data.IsDefaultOrEmpty || (CameraProgram.Data.Length & 1) != 0 || Sounds.Data.IsDefault || Sounds.Data.Length != 20 || Files.IsDefault || Files.Any(file => file is null || file.Data.IsDefaultOrEmpty) || Files.Select(file => file.FileId).Distinct().Count() != Files.Length)
        {
            throw new InvalidDataException("Native Doomtrain requires its original TIM, camera program, vibration, sound descriptors and distinct archive files.");
        }
    }
}
