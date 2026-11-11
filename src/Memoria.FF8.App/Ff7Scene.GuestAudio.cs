using System.Text.Json;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestPcmSnapshot(int Version, SessionGame Origin, PresentationSoundVoice[] Voices)
{
    public void Validate()
    {
        if (Version != 1 || Origin != SessionGame.Ff8 || Voices is null || Voices.Length > 32 || Voices.Any(voice => voice is null || voice.Voice is < 1 or > 31 || voice.SampleIndex < 0 || voice.Volume > 127 || voice.Pan > 127 || voice.Position is null || voice.Position.Backend != "pcm16-v1" || voice.Position.Unit != "source-frames" || voice.Position.Fingerprint is not { Length: 64 } || voice.Position.Fingerprint.Any(character => !Uri.IsHexDigit(character)) || voice.Position.SampleRate <= 0 || !double.IsFinite(voice.Position.FractionalFrame) || voice.Position.FractionalFrame is < 0 or >= 1 || voice.Position.Channels is not (1 or 2) || voice.Position.Position < 0 || voice.Position.LoopStart < 0 || voice.Position.LoopEnd <= voice.Position.LoopStart || voice.Position.Position >= voice.Position.LoopEnd || voice.Position.StartPoint is not (0 or 1)) || Voices.Select(voice => voice.Voice).Distinct().Count() != Voices.Length)
        {
            throw new InvalidDataException("Guest PCM presentation has invalid source identity or voice positions.");
        }
    }
}

internal sealed partial class Ff7Scene
{
    private FieldAudioPresentation? guestSoundOutput;
    private byte[]? pendingGuestSounds;

    private FieldAudioPresentation EnsureGuestSoundOutput()
    {
        if (guestSoundOutput is null)
        {
            string directory = identityFf8Directory ?? throw new InvalidOperationException("Select the FFVIII installation in the launcher to play guest sounds.");
            guestSoundOutput = new(directory, mediaToolsDirectory) { OutputFactory = () => audioOutput ??= new PcmAudioOutput(), OwnsSoundOutput = false };
            guestSoundOutput.SetFocusPaused(audioPaused);
        }
        return guestSoundOutput;
    }

    private void DeliverGuestBattleSounds(bool discard = false)
    {
        if (mixedCombat is not { } mixed)
        {
            return;
        }
        foreach (CrossoverGuestAudioReceipt receipt in mixed.PeekSoundRequests())
        {
            if (audioEnabled && !discard)
            {
                EnsureGuestSoundOutput().PlayBattleModuleSoundStrict(receipt.Sound);
            }
            mixed.AcknowledgeSoundRequests(receipt.Ordinal);
        }
    }

    private byte[]? CaptureGuestSounds(int queuedFrames)
    {
        if (pendingGuestSounds is not null)
        {
            return pendingGuestSounds.ToArray();
        }
        PresentationSoundVoice[]? voices = guestSoundOutput?.CapturePresentationSounds(queuedFrames);
        return voices is not { Length: > 0 } ? null : JsonSerializer.SerializeToUtf8Bytes(new CrossoverGuestPcmSnapshot(1, SessionGame.Ff8, voices));
    }

    private void RestoreGuestSounds()
    {
        if (!audioEnabled || pendingGuestSounds is null)
        {
            return;
        }
        CrossoverGuestPcmSnapshot saved = JsonSerializer.Deserialize<CrossoverGuestPcmSnapshot>(pendingGuestSounds) ?? throw new InvalidDataException("The saved guest PCM presentation is empty.");
        if (saved.Version != 1)
        {
            Console.Error.WriteLine("Audio compatibility: an unsupported optional guest presentation was reset.");
            pendingGuestSounds = null;
            return;
        }
        saved.Validate();
        if (identityFf8Directory is null)
        {
            Console.Error.WriteLine("Audio compatibility: saved guest voices were reset because FFVIII is not selected in the launcher.");
            pendingGuestSounds = null;
            return;
        }
        try
        {
            EnsureGuestSoundOutput().RestorePresentationSounds(saved.Voices);
        }
        catch
        {
            guestSoundOutput?.Dispose();
            guestSoundOutput = null;
            throw;
        }
        pendingGuestSounds = null;
    }
}
