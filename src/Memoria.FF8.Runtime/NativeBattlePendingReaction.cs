using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Physical participant BYTE199/200 (BD7 stage/BD8 order); fields may independently be unknown in old saves.</summary>
public sealed record NativeBattlePendingReaction([property: JsonRequired] byte? Stage, [property: JsonRequired] byte? Order)
{
    public void Validate()
    {
        if (Stage is not null and not (0 or 2 or 3))
        {
            throw new InvalidDataException("Native pending reaction stage differs from494410's BD7 producer.");
        }
    }
}

public sealed partial class BattleCombatant
{
    internal NativeBattlePendingReaction NativePendingReaction { get; set; } = new(0, 0);
    internal byte? NativeRemainingHits { get; set; }
}
