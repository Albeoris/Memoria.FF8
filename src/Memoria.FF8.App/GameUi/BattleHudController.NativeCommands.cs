using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? nativeLimitSelectionActorId;
    private bool nativeLimitSelectionInitialized;

    private void PrepareNativeLimitChoices()
    {
        if (SelectedCommand == BattleAbility.LimitBreak && SelectedActor is { NativeLimit.Command: 16 } actor && session.GetNativeSlotsRoll(actor.Id) is null)
        {
            session.RollNativeSlots(actor.Id);
        }
    }

    public void RerollNativeSlots()
    {
        if (!SelectingAction || SelectingTarget || SelectedCommand != BattleAbility.LimitBreak || SelectedActor is not { NativeLimit.Command: 16 } actor)
        {
            throw new InvalidOperationException("Do over requires the active Slots choice menu.");
        }
        session.RollNativeSlots(actor.Id);
        Message = "Choose magic. Page Left/Right: Do over.";
        SoundRequested?.Invoke(MenuSound.Confirm);
    }

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
