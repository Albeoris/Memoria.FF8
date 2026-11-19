using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Save-owned effective reward multipliers, expressed in exact tenths.</summary>
public sealed record NewGamePlusRewardRules
{
    public const int CurrentSchemaVersion = 1;
    [JsonRequired]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonRequired]
    public int ExperienceTenths { get; init; } = 10;
    [JsonRequired]
    public int AbilityPointTenths { get; init; } = 10;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported New Game+ reward policy version {SchemaVersion}.");
        }
        if (ExperienceTenths is < 0 or > 100 || AbilityPointTenths is < 0 or > 100)
        {
            throw new InvalidDataException("New Game+ reward multipliers must be between 0.0 and 10.0 in exact tenths.");
        }
    }

    public ushort ScaleExperience(int subtotal)
    {
        Validate();
        return Scale(subtotal, ExperienceTenths);
    }

    public ushort ScaleAbilityPoints(int subtotal)
    {
        Validate();
        return Scale(subtotal, AbilityPointTenths);
    }

    private static ushort Scale(int subtotal, int tenths)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(subtotal);
        long scaled = ((long)subtotal * tenths + 5L) / 10L;
        return checked((ushort)Math.Min(ushort.MaxValue, scaled));
    }
}
