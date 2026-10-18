using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private double tripleTriadDelay;
    private TripleTriadController? presentedTripleTriad;
    private TripleTriadAudioPresentation? tripleTriadAudio;
    private TripleTriadMatch? preparedTripleTriadMatch;
    private readonly UiNavigationRepeat tripleTriadVertical = new();
    private readonly UiNavigationRepeat tripleTriadHorizontal = new();

    private bool UpdateTripleTriadUi(TimeSpan elapsed, InputFrame input)
    {
        if (events?.PendingTripleTriad is null)
        {
            if (presentedTripleTriad is not null)
            {
                tripleTriadAudio?.Dispose();
                tripleTriadAudio = null;
                presentedTripleTriad = null;
                if (world?.Field is not null)
                {
                    fieldAudio?.ResumeMusic(session.State.GlobalSoundProcess?.MusicPlayback);
                }
            }
            return false;
        }
        TripleTriadController controller = world!.EnsureTripleTriad()!;
        if (!ReferenceEquals(presentedTripleTriad, controller))
        {
            tripleTriadAudio?.Dispose();
            presentedTripleTriad = controller;
            new TripleTriadDeckHistory(globalSettings.TripleTriadDeckHistoryPath).Connect(controller);
            if (fieldAudio is not null)
            {
                tripleTriadAudio = new TripleTriadAudioPresentation(fieldAudio, controller);
            }
            controller.EnablePresentation();
            uiCanvas!.PrepareTripleTriad(controller);
            preparedTripleTriadMatch = controller.Match;
            tripleTriadDelay = 0;
        }
        controller.AdvancePresentation(elapsed);
        if (UpdatePlayerSaves(input))
        {
            return true;
        }
        UiView view = controller.BuildView();
        TripleTriadPromptState? beforePrompt = session.State.TripleTriadPlay?.Prompt;
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        int vertical = tripleTriadVertical.Update(Math.Abs(input.Vertical) >= 0.5f ? Math.Sign(input.Vertical) : 0, input.MenuVertical, elapsed);
        int horizontal = tripleTriadHorizontal.Update(Math.Abs(input.Horizontal) >= 0.5f ? Math.Sign(input.Horizontal) : 0, input.MenuHorizontal, elapsed);
        if (vertical < 0)
        {
            view.Key(UiKey.Up);
        }
        if (vertical > 0)
        {
            view.Key(UiKey.Down);
        }
        if (horizontal < 0)
        {
            view.Key(UiKey.Left);
        }
        if (horizontal > 0)
        {
            view.Key(UiKey.Right);
        }
        if (input.MenuPage != 0)
        {
            view.Key(input.MenuPage < 0 ? UiKey.PageUp : UiKey.PageDown);
        }
        if (input.Confirm || keyboard.UiConfirmRequested)
        {
            view.Key(UiKey.Confirm);
        }
        if (input.Cancel || keyboard.UiCancelRequested)
        {
            view.Key(UiKey.Cancel);
        }
        MouseState mouse = Mouse.GetState();
        view.Pointer(mouse.X, mouse.Y, IsActive && mouse.LeftButton == ButtonState.Pressed);
        if (!ReferenceEquals(preparedTripleTriadMatch, controller.Match))
        {
            uiCanvas!.PrepareTripleTriadImages(controller.Match?.InitialHands.SelectMany(hand => hand) ?? []);
            preparedTripleTriadMatch = controller.Match;
        }
        if (controller.AwaitingRules && beforePrompt == session.State.TripleTriadPlay?.Prompt)
        {
            world.AdvanceTripleTriadPrompt();
        }
        if (!controller.IsAnimating && controller.Match is { CurrentPlayer: 1, Outcome: TripleTriadOutcome.InProgress })
        {
            tripleTriadDelay += elapsed.TotalSeconds;
            if (tripleTriadDelay >= 0.45 && events.PendingTripleTriad is not null)
            {
                controller.AdvanceOpponent();
                tripleTriadDelay = 0;
            }
        }
        else
        {
            tripleTriadDelay = 0;
        }
        fieldSeconds = 0;
        return true;
    }
}
