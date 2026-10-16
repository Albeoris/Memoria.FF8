using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private double tripleTriadDelay;

    private bool UpdateTripleTriadUi(TimeSpan elapsed, InputFrame input)
    {
        if (events?.PendingTripleTriad is null)
        {
            return false;
        }
        TripleTriadController controller = world!.EnsureTripleTriad()!;
        if (UpdatePlayerSaves(input))
        {
            return true;
        }
        UiView view = controller.BuildView();
        TripleTriadPromptState? beforePrompt = session.State.TripleTriadPlay?.Prompt;
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        if (input.MenuVertical < 0)
        {
            view.Key(UiKey.Up);
        }
        if (input.MenuVertical > 0)
        {
            view.Key(UiKey.Down);
        }
        if (input.MenuHorizontal < 0)
        {
            view.Key(UiKey.Left);
        }
        if (input.MenuHorizontal > 0)
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
        if (controller.AwaitingRules && beforePrompt == session.State.TripleTriadPlay?.Prompt)
        {
            world.AdvanceTripleTriadPrompt();
        }
        tripleTriadDelay += elapsed.TotalSeconds;
        if (tripleTriadDelay >= 0.45 && events.PendingTripleTriad is not null)
        {
            controller.AdvanceOpponent();
            tripleTriadDelay = 0;
        }
        fieldSeconds = 0;
        return true;
    }
}
