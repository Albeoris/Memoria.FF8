using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class BattleComponentTerminalTextureCertificateValidation
{
    public static void Validate(BattleComponentTerminalTextureCertificate certificate, ushort expectedEffect, string sourceSha256)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (expectedEffect is < 1 or > 400 || certificate.CompletionKind != "terminal-texture-quiescence" || !certificate.Completed || certificate.Drained || !certificate.ResidentTasksRemain || certificate.Effect != expectedEffect || !IsHash(sourceSha256) || !IsHash(certificate.SourceSha256) || !StringComparer.OrdinalIgnoreCase.Equals(certificate.SourceSha256, sourceSha256) || certificate.NativeDispatch is null && certificate.NativeAdmission is null || certificate.First is null || certificate.Last is null || certificate.First.Effect != expectedEffect || certificate.Last.Effect != expectedEffect || certificate.First.GraphicsCallback < 0 || certificate.FirstPresentedFrame < 0 || certificate.LastPresentedFrame < certificate.FirstPresentedFrame || certificate.TailPresentedFrames != certificate.LastPresentedFrame - certificate.FirstPresentedFrame || certificate.TailPresentedFrames < 2 || certificate.Last.GraphicsCallback <= certificate.First.GraphicsCallback || certificate.NativeCallbacksAdvanced != certificate.Last.GraphicsCallback - certificate.First.GraphicsCallback || certificate.NativeCallbacksAdvanced < 1)
        {
            throw new InvalidDataException("Component terminal texture completion requires the exact selected source, resident contract and observed presentation/native callback boundaries.");
        }
        if (certificate.NativeDispatch is { } dispatch && (dispatch.Serial <= 0 || dispatch.Callback < 0 || certificate.First.GraphicsCallback < dispatch.Callback || string.IsNullOrWhiteSpace(dispatch.ActorId) || dispatch.RequestedEffect != expectedEffect || dispatch.ExecutedEffect != expectedEffect))
        {
            throw new InvalidDataException("Component terminal texture dispatch differs from the selected live effect.");
        }
        if (certificate.NativeAdmission is { } admission && (admission.Effect != expectedEffect || admission.Actor >= 7 || admission.HeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || admission.Callback < 0 || certificate.First.GraphicsCallback < admission.Callback))
        {
            throw new InvalidDataException("Component terminal texture admission differs from the selected native header owner.");
        }
        ValidateObservation(certificate.First);
        ValidateObservation(certificate.Last);
        if (!StringComparer.OrdinalIgnoreCase.Equals(certificate.First.ResidentFingerprint, certificate.Last.ResidentFingerprint) || certificate.First.Textures.Head != certificate.Last.Textures.Head || certificate.First.Textures.Tail != certificate.Last.Textures.Tail || certificate.First.Textures.Capacity != certificate.Last.Textures.Capacity || certificate.First.Textures.PhysicalActiveCount != certificate.Last.Textures.PhysicalActiveCount || certificate.First.Owners.ActionHeaders != certificate.Last.Owners.ActionHeaders || certificate.First.Owners.ContactRows != certificate.Last.Owners.ContactRows || certificate.First.Owners.RetainedActiveHeader != certificate.Last.Owners.RetainedActiveHeader || certificate.First.Owners.RetainedActiveContact != certificate.Last.Owners.RetainedActiveContact || !certificate.First.Textures.Tasks.Zip(certificate.Last.Textures.Tasks).All(pair => SameTask(pair.First, pair.Second)))
        {
            throw new InvalidDataException("Resident native task, programme or model predicates changed across the terminal presentation tail.");
        }
    }

    private static void ValidateObservation(BattleComponentTextureQuiescenceObservation observation)
    {
        if (observation.Owners is null || observation.Textures is null || observation.RawBlockers.IsDefault || observation.Owners.OriginalPools.IsDefault || !observation.IsEligible || !IsHash(observation.ResidentFingerprint))
        {
            throw new InvalidDataException("Terminal texture completion cannot hide another owner or claim full drain while resident native tasks remain.");
        }
        DiagnosticTextureQuiescenceValidation.Validate(observation.Textures);
    }

    private static bool SameTask(BattleDiagnosticTextureTask first, BattleDiagnosticTextureTask last)
    {
        return first.Slot == last.Slot && first.Flags == last.Flags && first.State == last.State && first.Next == last.Next && first.Kind == last.Kind && first.UnknownByteMask == last.UnknownByteMask && first.PayloadLength == last.PayloadLength && first.ModelPointer == last.ModelPointer && first.CapturedSequence == last.CapturedSequence && first.ProgramOffset == last.ProgramOffset && first.ProgramLength == last.ProgramLength && first.Deadline == last.Deadline && first.ModelIndex == last.ModelIndex && (first.ModelFlags0 & 2) == (last.ModelFlags0 & 2) && (first.ResourceFlags44 & 0x40) == (last.ResourceFlags44 & 0x40) && first.ModelSequence == last.ModelSequence;
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
