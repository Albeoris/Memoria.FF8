using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    public void StepDiagnostic(int ticks, KeyboardState keys)
    {
        StepDiagnostic(ticks, () => keys);
    }

    public void StepDiagnostic(int ticks, Func<KeyboardState> readKeys)
    {
        if (ticks is < 1 or > 900)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }
        for (int tick = 0; tick < ticks; tick++)
        {
            Update(new GameTime(TimeSpan.Zero, TimeSpan.FromTicks((long)Math.Ceiling(TimeSpan.TicksPerSecond / 30d))), readKeys());
        }
    }
}
