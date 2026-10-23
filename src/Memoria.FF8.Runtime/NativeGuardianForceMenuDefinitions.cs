using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeGuardianForceLearningRule([property: JsonRequired] byte Requirement, [property: JsonRequired] byte Exclusion, [property: JsonRequired] byte Ability);
public sealed record NativeGuardianForceAbility(int Ability, bool Learned, int LearningIndex, int Progress, int RequiredPoints);
public sealed record NativeGuardianForceInitialState(NativeGuardianForceLearnedBank LearnedBank, ImmutableArray<byte> AbilityProgress, byte Learning, uint ForgottenAbilities);

/// <summary>Native4ACB70's twenty-one four-byte GF learning entries and kernel11..17 ability AP bytes.</summary>
public sealed record NativeGuardianForceMenuDefinitions([property: JsonRequired] ImmutableArray<ImmutableArray<NativeGuardianForceLearningRule>> Learning, [property: JsonRequired] ImmutableArray<byte> AbilityPoints, [property: JsonRequired] ImmutableArray<byte> CommandAbilityCommands, [property: JsonRequired] ImmutableArray<byte> PartyAbilityFlags)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> Templates { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> SourceTemplates { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<NativeGuardianForceInitialState> InitialStates { get; init; }

    public void Validate()
    {
        if (!InitialStates.IsDefault && (InitialStates.Length != 16 || InitialStates.Any(entry => entry is null || entry.LearnedBank is null || entry.AbilityProgress.IsDefault || entry.AbilityProgress.Length != 21)))
        {
            throw new InvalidDataException("GF initialization baselines require sixteen complete original states.");
        }
        if (!Templates.IsDefault)
        {
            if (Templates.Length != 16 || Templates.Any(row => row.IsDefault || row.Select(entry => entry.Ability).Distinct().Count() != row.Length || row.Any(entry => entry is null || entry.Ability <= 0 || entry.Ability >= 128 || entry.SupportedCondition && entry.Ability >= AbilityPoints.Length || entry.RequiredPoints < 0 || entry.RequiredLevel is < 1 or > 100 || entry.PrerequisiteAbility == entry.Ability || entry.ExcludedAbility == entry.Ability || entry.PrerequisiteAbility is int prerequisite && !row.Any(candidate => candidate.Ability == prerequisite) || entry.ExcludedAbility is int excluded && !row.Any(candidate => candidate.Ability == excluded))))
            {
                throw new InvalidDataException("Managed GF templates require unique supported abilities and valid learning references.");
            }
            foreach (ImmutableArray<GuardianForceAbilityTemplate> row in Templates)
            {
                foreach (GuardianForceAbilityTemplate entry in row)
                {
                    HashSet<int> visited = [];
                    GuardianForceAbilityTemplate current = entry;
                    while (current.PrerequisiteAbility is int prerequisite)
                    {
                        if (!visited.Add(prerequisite))
                        {
                            throw new InvalidDataException("GF learning prerequisites contain a cycle.");
                        }
                        current = row.First(candidate => candidate.Ability == prerequisite);
                    }
                }
            }
        }
        if (Learning.IsDefault || Learning.Length != 16 || Learning.Any(row => row.IsDefault || row.Length != 21 || row.Any(rule => rule is null || rule.Ability >= 128 || rule.Requirement is >= 101 and <= 121 && rule.Exclusion != 255 && rule.Exclusion >= 21)) || AbilityPoints.IsDefault || AbilityPoints.Length != 116 || CommandAbilityCommands.IsDefault || CommandAbilityCommands.Length != 19 || PartyAbilityFlags.IsDefault || PartyAbilityFlags.Length != 5)
        {
            throw new InvalidDataException("Native GF menu requires the actual sixteen kernel learning tables, AP requirements and command mappings.");
        }
    }
}
