using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? nativeLimitSelectionActorId;
    private bool nativeLimitSelectionInitialized;

    private BattleCombatant? NativeShotActor => session.Combatants.FirstOrDefault(actor => session.GetNativeShot(actor.Id) is not null);
    private BattleCombatant? NativeDuelActor => session.Combatants.FirstOrDefault(actor => session.GetNativeDuel(actor.Id) is not null);
    private BattleCombatant? NativeRenzokukenActor => session.Combatants.FirstOrDefault(actor => session.GetNativeRenzokuken(actor.Id) is not null);
    private bool NativeTimedLimitActive => NativeShotActor is not null || NativeDuelActor is not null || NativeRenzokukenActor is not null;
    public Func<ushort, string> NativeButtonBindingHint { get; set; } = new KeyboardInput().NativeButtonBindingHint;
    private IReadOnlyList<string>? NativeDuelChoiceLabels => NativeDuelActor is { } actor ? session.GetNativeDuelChoices(actor.Id).Where(choice => choice.Learned).Select(choice => choice.Name + "  " + string.Join(" ", choice.Buttons.Select(NativeButtonBindingHint))).ToArray() : null;

    private void PresentNativeShot()
    {
        if (session.NativeGuardianBoost is { } boost)
        {
            string window = boost.Phase == 4 ? boost.Window == 0 ? "STOP" : $"{NativeButtonBindingHint(0x80)}: PRESS" : "WAIT";
            Message = $"GF Boost {boost.DisplayPercent}% · {window}";
        }
        else if (NativeShotActor is { } actor && session.GetNativeShot(actor.Id) is { } shot)
        {
            Message = shot.Phase == BattleNativeShotPhase.Ready ? $"Shot · {GunbladeBindingHint}: Fire · Cancel: Finish · {shot.RemainingAmmo} ammo" : "Shot";
        }
        else if (NativeDuelActor is { } duelist)
        {
            Message = session.GetNativeDuel(duelist.Id)!.Phase == BattleNativeDuelPhase.Ready ? "Duel · Enter a button sequence" : "Duel";
        }
        else if (NativeRenzokukenActor is { } gunblade)
        {
            BattleNativeRenzokukenState state = session.GetNativeRenzokuken(gunblade.Id)!;
            Message = session.NativeRenzokukenBarVisible && session.CanTriggerNativeRenzokuken ? $"Renzokuken · {GunbladeBindingHint}: Trigger · Hit {state.Input.ContactIndex + 1}" : "Renzokuken";
        }
    }

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
        if (NativeTimedLimitActive || session.NativeGuardianBoost is not null)
        {
            return [];
        }
        if (SelectedActor is not { } actor)
        {
            return [];
        }
        return session.GetNativeLimitLevel(actor.Id) > 0 ? [.. actor.Abilities, BattleAbility.LimitBreak] : actor.Abilities;
    }

    private void RefreshNativeLimitSelection()
    {
        if (NativeTimedLimitActive || session.NativeGuardianBoost is not null)
        {
            return;
        }
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
