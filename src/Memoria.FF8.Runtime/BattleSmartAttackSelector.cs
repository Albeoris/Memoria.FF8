namespace Memoria.FF8.Runtime;

public sealed record BattleSmartAttackCandidate(BattleActionDefinition Action, int Available, int Stock, int Casts, BattleDamageForecast Forecast, string? DrawSourceId = null, int? DrawSourceIndex = null, bool CanApplyMeltdown = false);

/// <summary>Ranks source-owned Smart Attack choices independently of the battle hosting them.</summary>
public static class BattleSmartAttackSelector
{
    public static BattleSmartAttackPreview Select(BattleSmartAttackPreview physical, IReadOnlyList<BattleSmartAttackCandidate> candidates)
    {
        var drawnKill = candidates.Where(candidate => candidate.DrawSourceId is not null && candidate.Forecast.KillProbability > 0.9).OrderBy(candidate => candidate.Forecast.ExpectedDamage).ThenBy(candidate => candidate.Action.Id).ThenBy(candidate => candidate.DrawSourceIndex).ToArray();
        if (drawnKill.Length > 0)
        {
            return Preview(physical, drawnKill[0]) with { CastCount = 1 };
        }
        foreach (var group in candidates.Where(candidate => candidate.DrawSourceId is null).GroupBy(candidate => candidate.Action.NativeAction!.Element).OrderByDescending(group => group.Sum(candidate => candidate.Available)).ThenByDescending(group => group.Sum(candidate => candidate.Stock)).ThenBy(group => group.Key))
        {
            var sufficient = group.Where(candidate => candidate.Forecast.KillProbability > 0.9).OrderBy(candidate => candidate.Forecast.ExpectedDamage).ThenBy(candidate => candidate.Action.Id).ToArray();
            if (sufficient.Length > 0)
            {
                return Preview(physical, sufficient[0]);
            }
        }
        var meltdown = candidates.Where(candidate => candidate.CanApplyMeltdown).OrderByDescending(candidate => candidate.DrawSourceId is not null).ThenBy(candidate => candidate.DrawSourceIndex).ToArray();
        if (meltdown.Length > 0)
        {
            return Preview(physical, meltdown[0]);
        }
        var strongest = candidates.OrderByDescending(candidate => candidate.Forecast.ExpectedDamage).ThenByDescending(candidate => candidate.DrawSourceId is not null).ThenByDescending(candidate => candidate.Available).ThenBy(candidate => candidate.Action.Id).ThenBy(candidate => candidate.DrawSourceIndex).ToArray();
        if (strongest.Length > 0 && strongest[0].Forecast.ExpectedDamage > 0 && strongest[0].Forecast.ExpectedDamage > physical.ExpectedDamage)
        {
            return Preview(physical, strongest[0]);
        }
        return physical;
    }

    private static BattleSmartAttackPreview Preview(BattleSmartAttackPreview physical, BattleSmartAttackCandidate chosen)
    {
        bool drawn = chosen.DrawSourceId is not null;
        return physical with { Ability = drawn ? BattleAbility.Draw : BattleAbility.Magic, ActionId = chosen.Action.Id, Name = (drawn ? "Draw: " : "") + (chosen.Action.DisplayName ?? chosen.Action.Name), CastCount = chosen.Casts, ExpectedDamage = chosen.Forecast.ExpectedDamage, KillProbability = chosen.Forecast.KillProbability, DrawSourceId = chosen.DrawSourceId, DrawSourceIndex = chosen.DrawSourceIndex };
    }
}
