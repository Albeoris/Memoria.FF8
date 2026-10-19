using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTonberry90Resources(uint StaticAddress, NativeBattleEffectHeapAllocation StaticData, NativeBattleMoogle96VibrationResource Vibration, ImmutableArray<NativeBattleEffectFileAsset> Files)
{
    public ImmutableArray<NativeBattleEffectInitializerAsset> InitializerTims { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGfAnimationSource? PhaseOneSource { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectOwnedModelResource? CreatureModel { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? CreatureEntrySound { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? CreatureParticleSchedules { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGfAnimationSource? CreatureProgramSource { get; init; }

    public void Validate()
    {
        if (StaticData is null || StaticData.Data.IsDefaultOrEmpty || Vibration is null || Vibration.Address != 0x16D7CD0 || Files.IsDefault || Files.Any(file => file is null || file.Data.IsDefault) || Files.Select(file => file.FileId).Distinct().Count() != Files.Length)
        {
            throw new InvalidDataException("Native Tonberry resources require their static owner, vibration bank and distinct archive files.");
        }
        Vibration.Bank.Validate();
        ReadStatic(0x15474D0, 0x40);
    }

    public ReadOnlySpan<byte> ReadStatic(uint address, int length)
    {
        long offset = (long)address - StaticAddress;
        if (StaticData is null || StaticData.Data.IsDefault || length < 0 || offset < 0 || offset > StaticData.Data.Length - (long)length)
        {
            throw new NotSupportedException("Native Tonberry static read leaves its supplied owner.");
        }
        return StaticData.Data.AsSpan().Slice((int)offset, length);
    }
}
