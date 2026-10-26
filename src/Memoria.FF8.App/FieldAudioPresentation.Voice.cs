using System.Buffers.Binary;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private readonly Dictionary<int, DirectSoundOutput.Buffer> dialogueVoices = [];
    private FieldVoicePresentation? voicePresentation;
    private int voicePreference = 100;
    private bool voiceUiPaused;

    public void SynchronizeVoices(string? field, object? owner, IReadOnlyList<FieldVoiceDialogue> dialogues, ResourceService resources, ExternalVoiceResolver? resolver, int? gameMoment = null)
    {
        if (focusPaused || voiceUiPaused)
        {
            return;
        }
        voicePresentation ??= new FieldVoicePresentation((name, channel, message, page, choice) => currentVoiceResolver?.Resolve(name, channel, message, page, choice, currentVoiceGameMoment), (channel, key, gain) => PlayDialogueVoice(channel, resources.Get(key), gain), StopDialogueVoice, channel => dialogueVoices.TryGetValue(channel, out DirectSoundOutput.Buffer? buffer) && buffer.IsPlaying);
        currentVoiceResolver = resolver;
        currentVoiceGameMoment = gameMoment;
        TryOutput(() => voicePresentation.Synchronize(field, owner, dialogues));
    }

    public void SetVoicePresentationPaused(bool paused)
    {
        if (voiceUiPaused == paused)
        {
            return;
        }
        voiceUiPaused = paused;
        foreach (DirectSoundOutput.Buffer buffer in dialogueVoices.Values)
        {
            TryOutput(() => SetBufferPaused(buffer, focusPaused || voiceUiPaused));
        }
    }

    private ExternalVoiceResolver? currentVoiceResolver;
    private int? currentVoiceGameMoment;

    public bool CanVoiceAutoAdvance(int channel) => voicePresentation?.CanAutoAdvance(channel) == true;

    private bool PlayDialogueVoice(int channel, ResourceContent resource, double gain)
    {
        using Stream input = resource.OpenRead();
        using MemoryStream encoded = new();
        input.CopyTo(encoded);
        byte[] pcm = PcmAudioDecoder.Decode(toolsDirectory, "pipe:0", encoded.ToArray(), 44100, 2);
        ApplyVoiceGain(pcm, gain);
        DirectSoundOutput.Buffer next = Sound.Create(pcm, 44100, 2);
        try
        {
            next.Volume(OutputGain(0, voicePreference));
            next.Play(false);
            if (focusPaused || voiceUiPaused)
            {
                next.Pause();
            }
        }
        catch
        {
            next.Dispose();
            throw;
        }
        StopDialogueVoice(channel);
        dialogueVoices[channel] = next;
        Console.WriteLine($"field-voice=Playing channel={channel} resource={resource.Key} owner={resource.Provenance}");
        return true;
    }

    internal static void ApplyVoiceGain(byte[] pcm, double gain)
    {
        if (!double.IsFinite(gain) || gain < 0 || pcm.Length % 2 != 0)
        {
            throw new InvalidDataException("Voice output requires aligned PCM16 and a finite nonnegative gain.");
        }
        for (int offset = 0; offset < pcm.Length; offset += 2)
        {
            short sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(offset));
            double value = Math.Round(sample * gain);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset), (short)Math.Clamp(value, short.MinValue, short.MaxValue));
        }
    }

    private void StopDialogueVoice(int channel)
    {
        if (dialogueVoices.Remove(channel, out DirectSoundOutput.Buffer? buffer))
        {
            buffer.Dispose();
        }
    }

    private void StopDialogueVoices()
    {
        voicePresentation?.Clear();
        foreach (int channel in dialogueVoices.Keys.ToArray())
        {
            StopDialogueVoice(channel);
        }
    }
}
