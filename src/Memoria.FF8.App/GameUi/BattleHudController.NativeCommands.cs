using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? nativeLimitSelectionActorId;
    private bool nativeLimitSelectionInitialized;

    private IReadOnlyList<BattleAbility> GetPlayerCommandChoices()
    {
        if (SelectedActor is not { } actor)
        {
            return [];
        }
        return session.GetNativeLimitLevel(actor.Id) > 0 ? [.. actor.Abilities, BattleAbility.LimitBreak] : actor.Abilities;
    }

    private void RefreshNativeLimitSelection()
    {
        BattleCombatant? actor = SelectedActor;
        if (actor?.Id == nativeLimitSelectionActorId)
        {
            return;
        }
        bool restoredSelection = !nativeLimitSelectionInitialized && actor is not null && session.HasNativeLimitSelection(actor.Id);
        nativeLimitSelectionInitialized = true;
        nativeLimitSelectionActorId = actor?.Id;
        if (actor?.NativeLimit is not null && !restoredSelection)
        {
            session.RefreshNativeLimit(actor.Id);
        }
    }
}
