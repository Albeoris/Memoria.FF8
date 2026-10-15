using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeGuardianForceLearningRule([property: JsonRequired] byte Requirement, [property: JsonRequired] byte Exclusion, [property: JsonRequired] byte Ability);
public sealed record NativeGuardianForceAbility(int Ability, bool Learned, int LearningIndex, int Progress, int RequiredPoints);

/// <summary>Native4ACB70's twenty-one four-byte GF learning entries and kernel11..17 ability AP bytes.</summary>
public sealed record NativeGuardianForceMenuDefinitions([property: JsonRequired] ImmutableArray<ImmutableArray<NativeGuardianForceLearningRule>> Learning, [property: JsonRequired] ImmutableArray<byte> AbilityPoints, [property: JsonRequired] ImmutableArray<byte> CommandAbilityCommands, [property: JsonRequired] ImmutableArray<byte> PartyAbilityFlags)
{
    public void Validate()
    {
        if (Learning.IsDefault || Learning.Length != 16 || Learning.Any(row => row.IsDefault || row.Length != 21 || row.Any(rule => rule is null || rule.Ability >= 128 || rule.Requirement is >= 101 and <= 121 && rule.Exclusion != 255 && rule.Exclusion >= 21)) || AbilityPoints.IsDefault || AbilityPoints.Length != 116 || CommandAbilityCommands.IsDefault || CommandAbilityCommands.Length != 19 || PartyAbilityFlags.IsDefault || PartyAbilityFlags.Length != 5)
        {
            throw new InvalidDataException("Native GF menu requires the actual sixteen kernel learning tables, AP requirements and command mappings.");
        }
    }
}
