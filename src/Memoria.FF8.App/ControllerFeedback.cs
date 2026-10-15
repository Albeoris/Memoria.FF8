using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Host actuator presentation of the retained game-owned vibration requests.</summary>
internal sealed class ControllerFeedback(Func<float, float, bool> setVibration)
{
    public bool Update(NativeVibrationProcessState? process, bool active, bool enabled)
    {
        if (!active || !enabled || process is null)
        {
            return setVibration(0, 0);
        }
        NativeVibrationPort port = process.Ports[0];
        // The PC endpoint was a stub. This host maps the two requested envelopes
        // to SDL's low- and high-frequency motors without changing queue state.
        return setVibration(port.RequestedA / 255f, port.RequestedB / 255f);
    }
}
