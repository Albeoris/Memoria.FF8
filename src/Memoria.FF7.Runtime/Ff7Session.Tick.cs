namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private Ff7TickContinuation? tickContinuation;
    private bool tickTailInProgress;
    private (ushort Held, ushort Pressed)? deferredButtons;
    private (int Horizontal, int Vertical, bool Run)? deferredMovement;

    public bool HasPendingTick => tickContinuation is not null;

    private void ApplyDeferredInput()
    {
        if (deferredButtons is { } buttons)
        {
            Machine.SetInput(buttons.Held, buttons.Pressed);
            deferredButtons = null;
        }
        if (deferredMovement is { } movement)
        {
            Machine.SetMovementInput(movement.Horizontal, movement.Vertical, movement.Run);
            deferredMovement = null;
        }
    }
}
