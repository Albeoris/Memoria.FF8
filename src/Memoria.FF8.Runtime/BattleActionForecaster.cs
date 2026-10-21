namespace Memoria.FF8.Runtime;

public sealed record BattleDamageForecast(double ExpectedDamage, double KillProbability, IReadOnlyDictionary<int, double> Distribution);

/// <summary>Read-only byte-weighted immediate damage distributions, including repeated casts.</summary>
public static class BattleActionForecaster
{
    public static BattleDamageForecast FromNativePhases(Func<Func<byte>, int> simulate, int hitPoints)
    {
        return FromSamples(Enumerable.Range(0, 256).Select(phase =>
        {
            byte isolated = (byte)phase;
            byte Draw()
            {
                return NativeBattleRandomState.DrawForecast(ref isolated);
            }
            return (simulate(Draw), 1.0);
        }), hitPoints);
    }

    public static BattleDamageForecast FromSamples(IEnumerable<(int Damage, double Weight)> samples, int hitPoints, int casts = 1)
    {
        if (hitPoints <= 0 || casts is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(casts));
        }
        Dictionary<int, double> single = [];
        foreach ((int damage, double weight) in samples)
        {
            if (weight < 0 || !double.IsFinite(weight))
            {
                throw new ArgumentOutOfRangeException(nameof(samples));
            }
            single[damage] = single.GetValueOrDefault(damage) + weight;
        }
        double total = single.Values.Sum();
        if (total <= 0)
        {
            throw new ArgumentException("Forecast samples require positive total probability.", nameof(samples));
        }
        single = single.ToDictionary(pair => pair.Key, pair => pair.Value / total);
        Dictionary<int, double> combined = new() { [0] = 1 };
        for (int cast = 0; cast < casts; cast++)
        {
            Dictionary<int, double> next = [];
            foreach ((int accumulated, double probability) in combined)
            {
                foreach ((int damage, double weight) in single)
                {
                    int value = accumulated + damage;
                    next[value] = next.GetValueOrDefault(value) + probability * weight;
                }
            }
            combined = next;
        }
        return new(combined.Sum(pair => pair.Key * pair.Value), combined.Where(pair => pair.Key >= hitPoints).Sum(pair => pair.Value), combined);
    }
}
