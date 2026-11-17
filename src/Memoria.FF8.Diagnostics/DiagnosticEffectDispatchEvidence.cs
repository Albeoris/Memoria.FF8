using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticEffectDispatchEvidence
{
    public static bool Matches(BattleDiagnosticEffectDispatch? left, BattleDiagnosticEffectDispatch? right)
    {
        return ReferenceEquals(left, right) || left is not null && right is not null &&
            JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left), JsonSerializer.SerializeToElement(right));
    }

    internal static bool Matches(ImmutableArray<BattleDiagnosticEffectDispatch> recorded, BattleDiagnosticEffectDispatch[] observed)
    {
        return !recorded.IsDefault && recorded.Length == observed.Length && recorded.Zip(observed).All(pair => Matches(pair.First, pair.Second));
    }
}
