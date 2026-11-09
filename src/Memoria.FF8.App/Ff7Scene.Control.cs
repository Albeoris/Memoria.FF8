using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    public KeyboardState KeysForActions(IEnumerable<ControlAction> actions)
    {
        Keys[] keys = actions.SelectMany(action =>
        {
            InputChord? chord = sharedInput.Profile.GetBindings(action, ControlDevice.Keyboard).FirstOrDefault() ?? ControlsProfile.GetPermanentBindings(action, ControlDevice.Keyboard).FirstOrDefault();
            return chord?.Keys ?? throw new InvalidOperationException($"Action {action} requires a configured keyboard binding for diagnostic input.");
        }).Distinct().ToArray();
        return new KeyboardState(keys);
    }

    public void StepActions(int ticks, IEnumerable<ControlAction> actions)
    {
        StepDiagnostic(ticks, KeysForActions(actions));
    }

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
        autoPlay?.ResetIdleDeadline();
        for (int tick = 0; tick < ticks; tick++)
        {
            Update(new GameTime(TimeSpan.Zero, TimeSpan.FromTicks((long)Math.Ceiling(TimeSpan.TicksPerSecond / 30d))), readKeys());
        }
    }
}
