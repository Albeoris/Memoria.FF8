using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class EffectComponentTerminalTextureTracker
{
    private BattleComponentTextureQuiescenceObservation? first;
    private BattleComponentTextureQuiescenceObservation? last;
    private long firstFrame;
    private long lastFrame;

    internal long? BoundaryTimestamp { get; private set; }

    internal bool Observe(BattleComponentTextureQuiescenceObservation? observation, long presentedFrame, long timestamp)
    {
        if (observation is null || !observation.IsEligible)
        {
            first = null;
            last = null;
            BoundaryTimestamp = null;
            return false;
        }
        if (first is null || last is null || first.Effect != observation.Effect || !string.Equals(first.ResidentFingerprint, observation.ResidentFingerprint, StringComparison.Ordinal))
        {
            first = observation;
            last = observation;
            firstFrame = presentedFrame;
            lastFrame = presentedFrame;
            BoundaryTimestamp = timestamp;
            return false;
        }
        if (presentedFrame < lastFrame || observation.GraphicsCallback < last.GraphicsCallback)
        {
            throw new InvalidDataException("Component terminal-texture observations cannot move backwards.");
        }
        last = observation;
        lastFrame = presentedFrame;
        return lastFrame - firstFrame >= 2 && last.GraphicsCallback - first.GraphicsCallback >= 1;
    }

    internal BattleComponentTerminalTextureCertificate Certificate(ushort effect, string sourceSha256, BattleDiagnosticEffectDispatch? dispatch, BattleComponentAdmissionIdentity? admission)
    {
        if (first is null || last is null || lastFrame - firstFrame < 2 || last.GraphicsCallback - first.GraphicsCallback < 1 || first.Effect != effect || last.Effect != effect)
        {
            throw new InvalidOperationException("The component has no stable terminal-texture presentation tail.");
        }
        return new("terminal-texture-quiescence", true, false, true, effect, sourceSha256, dispatch, admission, first, last, firstFrame, lastFrame, lastFrame - firstFrame, last.GraphicsCallback - first.GraphicsCallback);
    }
}
