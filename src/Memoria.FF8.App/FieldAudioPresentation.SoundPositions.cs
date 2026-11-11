using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record PresentationSoundVoice(int Voice, int SampleIndex, uint Volume, uint Pan, GameplaySaveAudioPosition Position);

internal sealed partial class FieldAudioPresentation
{
    internal PresentationSoundVoice[] CapturePresentationSounds(int queuedFrames)
    {
        return voices.OrderBy(pair => pair.Key).Select(pair =>
        {
            Voice voice = pair.Value;
            GameplaySaveAudioPosition? position = voice.PlaybackPosition?.Invoke(queuedFrames);
            return position is null || position.Position >= position.LoopEnd ? null : new PresentationSoundVoice(pair.Key, voice.SampleIndex, (uint)voice.Volume, (uint)voice.Pan, position);
        }).OfType<PresentationSoundVoice>().ToArray();
    }

    internal void RestorePresentationSounds(IReadOnlyList<PresentationSoundVoice> saved)
    {
        foreach (PresentationSoundVoice voice in saved)
        {
            GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(gameFiles.Resources, voice.SampleIndex)) ?? throw new NotSupportedException("The selected guest sound requires an unsupported audio format.");
            PlaySoundSample(voice.SampleIndex, voice.Voice, voice.Volume, voice.Pan, resource.LoopPolicy, voice.Position);
        }
    }
}
