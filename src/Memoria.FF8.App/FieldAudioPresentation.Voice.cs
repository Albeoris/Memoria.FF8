using System.Buffers.Binary;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private readonly Dictionary<int, PcmAudioOutput.Buffer> dialogueVoices = [];
    private FieldVoicePresentation? voicePresentation;
    private int voicePreference = 100;
    private bool voiceUiPaused;

    public void SynchronizeVoices(string? field, object? owner, IReadOnlyList<FieldVoiceDialogue> dialogues, ResourceService resources, ExternalVoiceResolver? resolver, int? gameMoment = null)
    {
        if (focusPaused || voiceUiPaused)
        {
            return;
        }
        voicePresentation ??= new FieldVoicePresentation((name, channel, message, page, choice) => currentVoiceResolver?.Resolve(name, channel, message, page, choice, currentVoiceGameMoment), (channel, key, gain) => PlayDialogueVoice(channel, resources.Get(key), gain), StopDialogueVoice, channel => pendingDialogueDecodes.Contains(channel) || IsDialogueVoicePlaying(channel));
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
        foreach (PcmAudioOutput.Buffer buffer in dialogueVoices.Values)
        {
            TryOutput(() => SetBufferPaused(buffer, focusPaused || voiceUiPaused));
        }
    }

    internal int DialogueVoiceCount => dialogueVoices.Count;
    internal bool IsDialogueVoicePlaying(int channel) => dialogueVoices.TryGetValue(channel, out PcmAudioOutput.Buffer? buffer) && buffer.IsPlaying;

    private ExternalVoiceResolver? currentVoiceResolver;
    private int? currentVoiceGameMoment;

    public bool CanVoiceAutoAdvance(int channel) => voicePresentation?.CanAutoAdvance(channel) == true;

    internal bool PlayDialogueVoice(int channel, ResourceContent resource, double gain)
    {
        try
        {
            Decoder.Queue(new DialogueDecodeOwner(channel), async token =>
            {
                using Stream input = resource.OpenRead();
                using MemoryStream encoded = new();
                await input.CopyToAsync(encoded, token).ConfigureAwait(false);
                PcmAudio pcm = await DecodeAsync("pipe:0", encoded.ToArray(), token).ConfigureAwait(false);
                ApplyVoiceGain(pcm.Bytes, gain);
                return pcm;
            }, pcm =>
            {
                pendingDialogueDecodes.Remove(channel);
                PcmAudioOutput.Buffer next = Sound.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
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
            }, exception =>
            {
                pendingDialogueDecodes.Remove(channel);
                voicePresentation?.PlaybackFailed(channel);
                ReportOutputFailure(exception);
            });
            pendingDialogueDecodes.Add(channel);
            return true;
        }
        catch
        {
            pendingDialogueDecodes.Remove(channel);
            throw;
        }
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

    internal void StopDialogueVoice(int channel)
    {
        decodeQueue?.Cancel(new DialogueDecodeOwner(channel));
        pendingDialogueDecodes.Remove(channel);
        if (dialogueVoices.Remove(channel, out PcmAudioOutput.Buffer? buffer))
        {
            buffer.Dispose();
        }
    }

    private void StopDialogueVoices()
    {
        voicePresentation?.Clear();
        foreach (int channel in pendingDialogueDecodes.Concat(dialogueVoices.Keys).Distinct().ToArray())
        {
            StopDialogueVoice(channel);
        }
    }
}
