using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Accumulated Limit learning and Angelo training; a missing component must be initialized by the coordinator.</summary>
public sealed record GameplaySaveLimitProgress([property: JsonRequired] ushort BlueMagicFlags, [property: JsonRequired] ushort DuelFlags, [property: JsonRequired] byte AngeloFlags, [property: JsonRequired] byte TrainingSelection, [property: JsonRequired] ImmutableArray<byte> TrainingCounters, [property: JsonRequired] byte? SpecialMagicFlags, [property: JsonRequired] byte? PetPalsFlags, [property: JsonRequired] ushort? Options)
{
    public static GameplaySaveLimitProgress Capture(NativeLimitProgress source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        return new(source.BlueMagicFlags, source.DuelFlags, source.AngeloFlags, source.TrainingSelection, source.TrainingCounters, source.SpecialMagicFlags, source.PetPalsFlags, source.Options978);
    }

    public NativeLimitProgress Restore(NativeLimitProgress installed)
    {
        ArgumentNullException.ThrowIfNull(installed);
        installed.Validate();
        Validate();
        NativeLimitProgress result = installed with { BlueMagicFlags = BlueMagicFlags, DuelFlags = DuelFlags, AngeloFlags = AngeloFlags, TrainingSelection = TrainingSelection, TrainingCounters = TrainingCounters, SpecialMagicFlags = SpecialMagicFlags, PetPalsFlags = PetPalsFlags, Options978 = Options };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (TrainingCounters.IsDefault || TrainingCounters.Length != 8)
        {
            throw new InvalidDataException("Gameplay Limit progression requires all eight Angelo counters.");
        }
    }
}
