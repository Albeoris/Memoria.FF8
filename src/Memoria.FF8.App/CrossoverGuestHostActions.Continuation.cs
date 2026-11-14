using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverSelectedActionContinuation(BattleNativeAction Action, ushort Targets, CrossoverNativeRenzokukenProgramState Source, CrossoverGunbladeHostPlacement Placement);

internal sealed partial class CrossoverGuestHostActions
{
    private CrossoverSelectedActionContinuation? selectedContinuation;

    private CrossoverGuestAbility? SelectedContinuationAbility => selectedContinuation is { } continuation ? new(CrossoverGuestAbilityCatalog.Key(continuation.Action.Command, continuation.Action.ActionId), continuation.Action.Command, "Limit", continuation.Action.Name!, continuation.Action, 1, null) : null;

    private bool IsSelectedContinuationTarget(ushort ability, int target)
    {
        return SelectedContinuationAbility?.Key == ability && selectedContinuation is { } continuation && (continuation.Targets & (1 << target)) != 0;
    }
}
