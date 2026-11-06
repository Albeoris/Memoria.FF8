using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Current salary popup phase and formatted logical values; committed money and rank have separate durable owners.</summary>
public sealed record GameplaySaveSalaryPresentation([property: JsonRequired] short Phase, [property: JsonRequired] short RankBlend98, [property: JsonRequired] short Scale9A, [property: JsonRequired] byte OldRank9C, [property: JsonRequired] byte NewRank9D, [property: JsonRequired] byte Delay9E, [property: JsonRequired] byte LeadingBlank9F, [property: JsonRequired] byte[] OldRankGlyphs, [property: JsonRequired] byte[] NewRankGlyphs, [property: JsonRequired] byte[] OldPayGlyphs, [property: JsonRequired] byte[] NewPayGlyphs)
{
    public static GameplaySaveSalaryPresentation? Capture(NativeSalaryPresentation? salary)
    {
        salary?.Validate();
        return salary is null || salary.IsColdProcess ? null : new(salary.Phase, salary.RankBlend98, salary.Scale9A, salary.OldRank9C, salary.NewRank9D, salary.Delay9E, salary.LeadingBlank9F, salary.OldRankGlyphs.ToArray(), salary.NewRankGlyphs.ToArray(), salary.OldPayGlyphs.ToArray(), salary.NewPayGlyphs.ToArray());
    }

    public NativeSalaryPresentation Restore(NativeSalaryPresentation initialized)
    {
        if (OldRankGlyphs is null || NewRankGlyphs is null || OldPayGlyphs is null || NewPayGlyphs is null)
        {
            throw new InvalidDataException("Gameplay salary presentation requires its current formatted logical values.");
        }
        NativeSalaryPresentation result = initialized with { Phase = Phase, RankBlend98 = RankBlend98, Scale9A = Scale9A, OldRank9C = OldRank9C, NewRank9D = NewRank9D, Delay9E = Delay9E, LeadingBlank9F = LeadingBlank9F, OldRankGlyphs = OldRankGlyphs.ToImmutableArray(), NewRankGlyphs = NewRankGlyphs.ToImmutableArray(), OldPayGlyphs = OldPayGlyphs.ToImmutableArray(), NewPayGlyphs = NewPayGlyphs.ToImmutableArray() };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        _ = Restore(NativeSalaryPresentation.FromColdProcess());
    }
}
