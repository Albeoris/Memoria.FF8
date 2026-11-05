using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private bool TryDeclineCardGame(GameWorld world)
    {
        if (world.Events.PendingTripleTriad is not { } request)
        {
            return false;
        }
        var cards = world.EnsureTripleTriad() ?? throw new InvalidDataException("A pending CARDGAME has no native card-module owner.");
        NativeTripleTriadPlayState before = session.State.TripleTriadPlay ?? throw new InvalidDataException("A pending CARDGAME has no saved prompt state.");
        bool replay = cards.AwaitingReplay;
        if (replay)
        {
            Apply(new(PlayerActionKind.Cancel));
        }
        else
        {
            if (!cards.AwaitingRules || before.RulesAccepted || before.Match is not null || before.SelectedCards.Length != 0)
            {
                throw new NotSupportedException("Automatic exploration can decline an unaccepted card invitation or replay prompt; an accepted match requires its actual ordinary card-game continuation.");
            }
            switch (before.Prompt.Phase)
            {
                case TripleTriadPromptPhase.AwaitingChoice:
                    Apply(new(PlayerActionKind.Cancel));
                    break;
                case TripleTriadPromptPhase.FadingIn:
                case TripleTriadPromptPhase.FadingOut:
                    Apply(new(PlayerActionKind.Wait, Frames: 15));
                    break;
                default:
                    throw new NotSupportedException("The native card invitation is outside its supported choice or fade callback phases.");
            }
        }
        if (world.Events.PendingTripleTriad is not { } after || GameWorld.TripleTriadIdentity(after) != GameWorld.TripleTriadIdentity(request) || session.State.TripleTriadPlay?.Prompt.Phase != before.Prompt.Phase || cards.AwaitingReplay != replay)
        {
            ResetIdleDeadline();
        }
        return true;
    }
}
