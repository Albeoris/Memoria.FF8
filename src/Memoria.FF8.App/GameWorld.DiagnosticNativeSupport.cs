using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private ImmutableArray<DiagnosticPreparationChange> PrepareDiagnosticNativeSupport(DiagnosticBattleDriverRecipe? recipe)
    {
        if (recipe is not { PassiveNativeSupport: true, ExpectedNativeCommand: 245 })
        {
            return [];
        }

        NativeSavedBattleFlags before = session.State.SavedBattleFlags ?? throw new NotSupportedException("Automatic support requires its initialized saved battle flags.");
        NativeSavedBattleFlags after = session.PrepareDiagnosticAutomaticGuardian(recipe.ExpectedNativeAction == 0);
        return [new("automatic-guardian-flags", 0, JsonSerializer.SerializeToElement(before), JsonSerializer.SerializeToElement(after))];
    }
}
