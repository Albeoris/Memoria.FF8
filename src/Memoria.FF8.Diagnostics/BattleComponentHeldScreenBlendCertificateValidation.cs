using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class BattleComponentHeldScreenBlendCertificateValidation
{
    public static void Validate(BattleComponentHeldScreenBlendCertificate certificate, ushort expectedEffect, string sourceSha256)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (expectedEffect is < 1 or > 400 || certificate.CompletionKind != "held-native-screen-blend" || !certificate.Completed || certificate.Drained || !certificate.ResidentTasksRemain || !certificate.HeldRenderWorkRemains || certificate.Effect != expectedEffect || !IsHash(sourceSha256) || !IsHash(certificate.SourceSha256) || !StringComparer.OrdinalIgnoreCase.Equals(certificate.SourceSha256, sourceSha256) || certificate.NativeDispatch is null && certificate.NativeAdmission is null || certificate.First is null || certificate.Last is null || certificate.First.Effect != expectedEffect || certificate.Last.Effect != expectedEffect || certificate.First.GraphicsCallback < 0 || certificate.FirstPresentedFrame < 0 || certificate.LastPresentedFrame < certificate.FirstPresentedFrame || certificate.TailPresentedFrames != certificate.LastPresentedFrame - certificate.FirstPresentedFrame || certificate.TailPresentedFrames < 2 || certificate.Last.GraphicsCallback <= certificate.First.GraphicsCallback || certificate.NativeCallbacksAdvanced != certificate.Last.GraphicsCallback - certificate.First.GraphicsCallback || certificate.NativeCallbacksAdvanced < 1)
        {
            throw new InvalidDataException("Component held screen blend completion requires the exact selected source, resident contract and observed presentation/native callback boundaries.");
        }
        if (certificate.NativeDispatch is { } dispatch && (dispatch.Serial <= 0 || dispatch.Callback < 0 || certificate.First.GraphicsCallback < dispatch.Callback || string.IsNullOrWhiteSpace(dispatch.ActorId) || dispatch.RequestedEffect != expectedEffect || dispatch.ExecutedEffect != expectedEffect))
        {
            throw new InvalidDataException("Component held screen blend dispatch differs from the selected live effect.");
        }
        if (certificate.NativeAdmission is { } admission && (admission.Effect != expectedEffect || admission.Actor >= 7 || admission.HeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || admission.Callback < 0 || certificate.First.GraphicsCallback < admission.Callback))
        {
            throw new InvalidDataException("Component held screen blend admission differs from the selected native header owner.");
        }
        ValidateObservation(certificate.First);
        ValidateObservation(certificate.Last);
        if (!StringComparer.OrdinalIgnoreCase.Equals(certificate.First.ResidentFingerprint, certificate.Last.ResidentFingerprint) || certificate.First.Head != certificate.Last.Head || certificate.First.Tail != certificate.Last.Tail || certificate.First.PhysicalResidentCount != certificate.Last.PhysicalResidentCount || certificate.First.Owners.ActionHeaders != certificate.Last.Owners.ActionHeaders || certificate.First.Owners.ContactRows != certificate.Last.Owners.ContactRows || certificate.First.Owners.RetainedActiveHeader != certificate.Last.Owners.RetainedActiveHeader || certificate.First.Owners.RetainedActiveContact != certificate.Last.Owners.RetainedActiveContact || !SameTask(certificate.First.ScreenBlend, certificate.Last.ScreenBlend))
        {
            throw new InvalidDataException("Resident native screen-blend payload or owner predicates changed across the terminal presentation tail.");
        }
    }

    private static void ValidateObservation(BattleComponentHeldScreenBlendObservation observation)
    {
        if (observation.Owners is null || observation.ScreenBlend is null || observation.RawBlockers.IsDefault || observation.Owners.OriginalPools.IsDefault || !observation.IsEligible || !IsHash(observation.ResidentFingerprint))
        {
            throw new InvalidDataException("Held screen blend completion cannot hide another owner or claim full drain while resident native tasks remain.");
        }
    }

    private static bool SameTask(BattleComponentHeldScreenBlendTask first, BattleComponentHeldScreenBlendTask last)
    {
        return first.Slot == last.Slot && first.Flags == last.Flags && first.State == last.State && first.Next == last.Next && first.Kind == last.Kind && first.UnknownByteMask == last.UnknownByteMask && first.HasProgram == last.HasProgram && first.Payload.AsSpan().SequenceEqual(last.Payload.AsSpan());
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
