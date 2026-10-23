using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private SlotsCardController? slotsCards;
    private Guid slotsDealId;
    private bool slotsAwaitRelease;
    public bool CardSlotsOwnsInput => slotsCards is not null || session.PendingSlotsDeal is not null || slotsAwaitRelease;
    public event Action<TripleTriadSound>? SlotsSoundRequested;
    public string SlotsNavigationHint { get; set; } = "Navigate";
    public string SlotsConfirmHint { get; set; } = new KeyboardInput().BindingPrompt(ControlAction.Confirm);
    internal SlotsCardController? SlotsCards => slotsCards;

    private bool TryBeginCardSlots(BattleCombatant actor)
    {
        if (!session.SelphieCardSlots || SelectedCommand != BattleAbility.LimitBreak || actor.NativeLimit?.Command != 16 || SelectingAction || SelectingTarget)
        {
            return false;
        }
        session.BeginNativeSlotsDeal(actor.Id);
        SelectingAction = true;
        AttachSlotsPresentation(false);
        return true;
    }

    private void AttachSlotsPresentation(bool restored)
    {
        if (session.PendingSlotsDeal is not { } deal || slotsCards is not null && slotsDealId == deal.Id)
        {
            return;
        }
        slotsDealId = deal.Id;
        slotsCards = new SlotsCardController(deal.Candidates.Length, deal.RevealMask, deal.SelectedIndex, restored);
        slotsCards.SoundRequested += sound => SlotsSoundRequested?.Invoke(sound);
        if (!restored)
        {
            slotsCards.Begin();
        }
    }

    public void AdvanceSlotsPresentation(TimeSpan elapsed, bool confirmHeld, bool mouseHeld)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        if (slotsAwaitRelease && !confirmHeld && !mouseHeld)
        {
            slotsAwaitRelease = false;
        }
        session.RefreshNativeSlotsDeal();
        AttachSlotsPresentation(true);
        if (slotsCards is not { } cards)
        {
            return;
        }
        if (session.PendingSlotsDeal is not { } deal || deal.Id != slotsDealId)
        {
            ReleaseSlotsPresentation(confirmHeld || mouseHeld);
            return;
        }
        cards.Advance(elapsed, confirmHeld, mouseHeld);
        if (cards.Finished)
        {
            try
            {
                session.SubmitNativeSlotsCard(slotsDealId);
            }
            catch (Exception error) when (error is InvalidOperationException or NotSupportedException or InvalidDataException)
            {
                ShowCommandError(error.Message);
            }
            finally
            {
                ReleaseSlotsPresentation(confirmHeld || mouseHeld);
            }
        }
    }

    private void ReleaseSlotsPresentation(bool waitForRelease)
    {
        slotsCards = null;
        slotsAwaitRelease = waitForRelease;
        SelectingAction = false;
        SelectingTarget = false;
        nativeLimitCommandSelected = false;
        nativeLimitSelectionActorId = null;
        selectedActorId = null;
    }

    private void NavigateCardSlots(MenuInput input)
    {
        if (slotsCards is not { } cards)
        {
            return;
        }
        if (input == MenuInput.Confirm)
        {
            ConfirmCardSlots(cards.FocusIndex, false);
        }
        else
        {
            cards.Navigate(input);
        }
    }

    private void ConfirmCardSlots(int index, bool click)
    {
        if (slotsCards is not { } cards || !cards.CanConfirm(click))
        {
            return;
        }
        if (session.SelectNativeSlotsCard(slotsDealId, index))
        {
            cards.Select(index);
        }
    }

    internal void SlotsPointer(float x, float y, UiRect bounds, bool click)
    {
        if (slotsCards is not { } cards)
        {
            return;
        }
        int index = -1;
        for (int candidate = 0; candidate < cards.Count; candidate++)
        {
            if (SlotsCardView.CardBounds(bounds, candidate, 1).Contains(x, y))
            {
                index = candidate;
                break;
            }
        }
        if (click)
        {
            if (index >= 0)
            {
                ConfirmCardSlots(index, true);
            }
        }
        else
        {
            cards.Hover(x, y, index);
        }
    }
}
