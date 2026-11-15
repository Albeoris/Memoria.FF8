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
        StepDiagnostic(ticks, readKeys, true);
    }

    public void StepAutoPlay(int ticks, bool stopAtTransition = false)
    {
        if (!AutoPlayRunning || AutoPlayPaused)
        {
            throw new InvalidOperationException("Start or resume automatic play before advancing it.");
        }
        // Control-step requests intentionally stop simulation between bounded batches.
        autoPlay?.ResetIdleDeadline();
        StepDiagnostic(ticks, () => new KeyboardState(), false, stopAtTransition);
    }

    private void StepDiagnostic(int ticks, Func<KeyboardState> readKeys, bool resetIdleDeadline, bool stopAtTransition = false)
    {
        if (ticks is < 1 or > 900)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }
        if (resetIdleDeadline)
        {
            autoPlay?.ResetIdleDeadline();
        }
        var sourceSession = Session;
        long? sourceGeneration = sourceSession?.FieldGeneration;
        var sourceWorld = sourceSession?.World;
        for (int tick = 0; tick < ticks; tick++)
        {
            Update(new GameTime(TimeSpan.Zero, TimeSpan.FromTicks((long)Math.Ceiling(TimeSpan.TicksPerSecond / 30d))), readKeys(), default, default, inputActive: true);
            if (stopAtTransition && (!ReferenceEquals(Session, sourceSession) || Session?.FieldGeneration != sourceGeneration || !ReferenceEquals(Session?.World, sourceWorld)))
            {
                break;
            }
            if (!resetIdleDeadline && (!AutoPlayRunning || AutoPlayPaused || Error is not null))
            {
                break;
            }
        }
    }
}
