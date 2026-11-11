using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Gives the player time to see an arrival before held movement can cross another exit.</summary>
internal sealed class LocationMovementGate
{
    private object? location;
    private double age;
    private double visibleAge;
    private bool released;

    public bool Blocked => !released;

    public void Observe(object identity, TimeSpan elapsed, bool fading)
    {
        if (!Equals(location, identity))
        {
            location = identity;
            age = 0;
            visibleAge = 0;
            released = false;
            return;
        }
        if (released)
        {
            return;
        }
        // A slow load or a suspended host must not consume the player's reaction window.
        double seconds = Math.Clamp(elapsed.TotalSeconds, 0, 0.1);
        age += seconds;
        visibleAge = fading ? 0 : visibleAge + seconds;
        released = visibleAge >= 0.35 || age >= 2;
    }

    public InputFrame Filter(InputFrame input) => Blocked ? input with { Horizontal = 0, Vertical = 0, SecondaryHorizontal = 0, SecondaryVertical = 0 } : input;

    public ushort FilterButtons(ushort buttons) => Blocked ? (ushort)(buttons & ~0xF000) : buttons;
}
