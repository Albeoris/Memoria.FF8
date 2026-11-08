using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class EffectComponentHeldScreenBlendTracker
{
    private BattleComponentHeldScreenBlendObservation? first;
    private BattleComponentHeldScreenBlendObservation? last;
    private long firstFrame;
    private long lastFrame;

    internal long? BoundaryTimestamp { get; private set; }

    internal bool Observe(BattleComponentHeldScreenBlendObservation? observation, long presentedFrame, long timestamp)
    {
        if (observation is null || !observation.IsEligible)
        {
            first = null;
            last = null;
            BoundaryTimestamp = null;
            return false;
        }
        if (first is null || last is null || first.Effect != observation.Effect || first.ResidentFingerprint != observation.ResidentFingerprint || first.Owners.ActionHeaders != observation.Owners.ActionHeaders || first.Owners.ContactRows != observation.Owners.ContactRows || first.Owners.RetainedActiveHeader != observation.Owners.RetainedActiveHeader || first.Owners.RetainedActiveContact != observation.Owners.RetainedActiveContact)
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
            throw new InvalidDataException("Component held-screen observations cannot move backwards.");
        }
        last = observation;
        lastFrame = presentedFrame;
        return lastFrame - firstFrame >= 2 && last.GraphicsCallback - first.GraphicsCallback >= 1;
    }

    internal BattleComponentHeldScreenBlendCertificate Certificate(ushort effect, string sourceSha256, BattleDiagnosticEffectDispatch? dispatch, BattleComponentAdmissionIdentity? admission)
    {
        if (first is null || last is null || first.Effect != effect || last.Effect != effect || lastFrame - firstFrame < 2 || last.GraphicsCallback - first.GraphicsCallback < 1)
        {
            throw new InvalidOperationException("The component has no stable held-screen presentation tail.");
        }
        return new("held-native-screen-blend", true, false, true, true, effect, sourceSha256, dispatch, admission, first, last, firstFrame, lastFrame, lastFrame - firstFrame, last.GraphicsCallback - first.GraphicsCallback);
    }
}
