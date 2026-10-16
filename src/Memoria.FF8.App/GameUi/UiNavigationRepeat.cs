namespace Memoria.FF8.GameUi;

/// <summary>Immediate taps plus bounded key repeat, independent of rendering frame rate.</summary>
internal sealed class UiNavigationRepeat
{
    private int direction;
    private double remaining;

    public int Update(int heldDirection, int pressedDirection, TimeSpan elapsed)
    {
        heldDirection = Math.Sign(heldDirection);
        if (heldDirection == 0)
        {
            direction = 0;
            remaining = 0;
            return Math.Sign(pressedDirection);
        }
        if (heldDirection != direction || pressedDirection != 0)
        {
            direction = heldDirection;
            remaining = 0.25;
            return pressedDirection == 0 ? direction : Math.Sign(pressedDirection);
        }
        remaining -= elapsed.TotalSeconds;
        if (remaining > 0)
        {
            return 0;
        }
        remaining = 0.065;
        return direction;
    }
}
