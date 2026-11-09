using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private void QueuePcmConcert(ExternalMusicSelection[] selections, bool preview)
    {
        Decoder.Queue(MusicKind.Midi, async token =>
        {
            List<PcmConcertSource.Part> parts = [];
            List<int?> resumeOffsets = [];
            foreach (ExternalMusicSelection selection in selections)
            {
                byte[] encoded = selection.File.ReadBytes();
                token.ThrowIfCancellationRequested();
                PcmAudio pcm = await DecodeAsync("pipe:0", encoded, token).ConfigureAwait(false);
                ExternalMusicLoop loop = ExternalMusicLoop.Read(encoded, pcm);
                string fingerprint = PcmFingerprint(pcm, loop.StartByte, loop.EndByte);
                int offset = ExternalMusicPcm.OffsetBytes(pcm, selection.OffsetSeconds);
                GameplaySaveAudioPosition? saved = ResumeMusicPosition(0, 43, parts.Count, "pcm16-v1", fingerprint);
                resumeOffsets.Add(ValidPcmPosition(saved, pcm, loop.StartByte, loop.EndByte) ? checked((int)(saved!.Position * pcm.Channels * sizeof(short))) : null);
                if (preview)
                {
                    offset = SkipConcertOpeningSilence(pcm, offset, loop.EndByte);
                }
                if (offset >= loop.EndByte)
                {
                    throw new InvalidDataException("External concert offset exceeds its loop end.");
                }
                parts.Add(new(pcm, loop.StartByte, loop.EndByte, offset, fingerprint, selection.Volume));
            }
            if (resumeOffsets.All(offset => offset.HasValue))
            {
                for (int index = 0; index < parts.Count; index++)
                {
                    parts[index] = parts[index] with { StartByte = resumeOffsets[index]!.Value };
                }
            }
            return new PcmConcertSource(parts);
        }, prepared => StartPcmConcert(selections, prepared), exception => MusicDecodeFailed(0, exception), discard: prepared => prepared.Dispose());
    }

    private static int SkipConcertOpeningSilence(PcmAudio pcm, int offset, int end)
    {
        int frameBytes = pcm.Channels * sizeof(short);
        while (offset < end && pcm.Bytes.AsSpan(offset, frameBytes).IndexOfAnyExcept((byte)0) < 0)
        {
            offset += frameBytes;
        }
        if (offset == end)
        {
            throw new InvalidDataException("The selected external instrument preview contains no sound after its configured offset.");
        }
        return offset;
    }

    private void StartPcmConcert(ExternalMusicSelection[] selections, PcmConcertSource prepared)
    {
        PcmConcertOutput output = new(Sound, prepared);
        try
        {
            output.SetGain(ExternalGain(MusicKind.Midi, Math.Clamp((int)midiVolume, 0, 127), 100));
            output.Pause(focusPaused || nativeMidiPaused);
            music?.Stop();
            output.Start();
        }
        catch
        {
            output.Dispose();
            throw;
        }
        externalMusic[(int)MusicKind.Midi]?.Dispose();
        externalMusic[(int)MusicKind.Midi] = output;
        DiscardMusicResumePosition(0);
        musicPositionCaptures[0] = prepared.CapturePositions;
        musicKinds[0] = MusicKind.Midi;
        musicIds[0] = 43;
        Console.WriteLine($"field-music=Playing track=43 slot=0 format=PCM resources={string.Join(',', selections.Select(selection => selection.File.PackageId + ':' + selection.File.RelativePath))}");
    }

    private sealed class PcmConcertOutput(PcmAudioOutput output, PcmConcertSource source) : IExternalMusicOutput
    {
        private bool attached;
        private bool disposed;
        public int Volume => 100;

        public void Start()
        {
            output.AddSource(source);
            attached = true;
        }

        public void Update()
        {
        }

        public void SetGain(int gain)
        {
            source.SetGain(gain);
        }

        public void Pause(bool paused)
        {
            source.Pause(paused);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (attached)
            {
                output.RemoveSource(source);
            }
            source.Dispose();
        }
    }
}
