using Memoria.FF8.Audio;
using Memoria.FF8.Runtime;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    internal Func<ExternalMusicSelection, CancellationToken, Task<Stream>>? PsfStreamOverride { get; init; }

    private void QueuePsfMusic(ExternalMusicSelection selection, MusicKind kind, int slot, int track)
    {
        QueuePsfMusic([selection], kind, slot, track, suppressOpeningSilence: false);
    }

    private bool TryPlayExternalConcert(uint mask)
    {
        IReadOnlyList<string> tracks = NativeConcertSelection.Tracks(mask);
        if (tracks.Count == 0)
        {
            return true;
        }
        ModCatalogSnapshot? snapshot = modCatalog?.Invoke();
        if (snapshot is null)
        {
            return false;
        }
        ExternalMusicResolver resolver = new(snapshot);
        ExternalMusicSelection?[] selections = tracks.Select(track => resolver.Resolve(track)).ToArray();
        if (selections.All(selection => selection is null))
        {
            return false;
        }
        if (selections.Any(selection => selection is null || !IsPsf(selection.File.RelativePath)))
        {
            throw new NotSupportedException("Concert output requires a PSF replacement for every selected instrument.");
        }
        nativeMidiPaused = false;
        musicKinds[0] = MusicKind.Midi;
        musicIds[0] = 43;
        QueuePsfMusic(selections.Select(selection => selection!).ToArray(), MusicKind.Midi, 0, 43, NativeConcertSelection.IsPreview(mask));
        return true;
    }

    private static bool IsPsf(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".minipsf", StringComparison.OrdinalIgnoreCase) || extension.Equals(".psf", StringComparison.OrdinalIgnoreCase);
    }

    private void QueuePsfMusic(ExternalMusicSelection[] selections, MusicKind kind, int slot, int track, bool suppressOpeningSilence)
    {
        Decoder.Queue(kind, async token =>
        {
            List<PcmStreamSource> sources = [];
            List<string> fingerprints = [];
            try
            {
                foreach (ExternalMusicSelection selection in selections)
                {
                    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    hash.AppendData(Encoding.UTF8.GetBytes($"psf-pcm16-v1:{selection.OffsetSeconds:R}:{suppressOpeningSilence}:"));
                    byte[] root = selection.File.ReadBytes();
                    hash.AppendData(SHA256.HashData(root));
                    byte[]? bios = selection.BiosFile?.ReadBytes();
                    if (bios is not null)
                    {
                        hash.AppendData(SHA256.HashData(bios));
                    }
                    string decoder = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Memoria.PsfDecoder.exe" : "Memoria.PsfDecoder");
                    if (File.Exists(decoder))
                    {
                        hash.AppendData(SHA256.HashData(File.ReadAllBytes(decoder)));
                    }
                    bool fingerprinting = true;
                    async Task<Stream> Open(CancellationToken cancellation)
                    {
                        if (PsfStreamOverride is { } open)
                        {
                            return await open(selection, cancellation).ConfigureAwait(false);
                        }
                        return await PsfDecoder.OpenAsync(selection.File.RelativePath, root, (requester, dependency) =>
                        {
                            byte[] bytes = selection.ResolveDependency(requester, dependency).ReadBytes();
                            if (fingerprinting)
                            {
                                hash.AppendData(SHA256.HashData(bytes));
                            }
                            return bytes;
                        }, bios ?? throw new InvalidDataException("PSF music requires an explicitly selected BIOS resource."), repeat: true, cancellation, suppressOpeningSilence: suppressOpeningSilence).ConfigureAwait(false);
                    }
                    Stream stream = await Open(token).ConfigureAwait(false);
                    string fingerprint = Convert.ToHexString(hash.GetHashAndReset());
                    fingerprinting = false;
                    fingerprints.Add(fingerprint);
                    GameplaySaveAudioPosition? saved = ResumeMusicPosition(slot, track, sources.Count, "psf-pcm16-v1", fingerprint);
                    bool resume = saved is { SampleRate: 44100, Channels: 2, LoopStart: 0, LoopEnd: 0 } && saved.Position <= long.MaxValue / 4;
                    double offset = resume ? saved!.Position / 44100d : selection.OffsetSeconds;
                    try
                    {
                        using CancellationTokenSource seekBudget = CancellationTokenSource.CreateLinkedTokenSource(token);
                        if (resume)
                        {
                            seekBudget.CancelAfter(TimeSpan.FromSeconds(5));
                        }
                        sources.Add(resume
                            ? await PcmStreamSource.CreateAtFrameAsync(stream, saved!.Position, seekBudget.Token, amplitude: 3.2).ConfigureAwait(false)
                            : await PcmStreamSource.CreateAsync(stream, offset, seekBudget.Token, amplitude: 3.2).ConfigureAwait(false));
                    }
                    catch (Exception exception) when (resume && !token.IsCancellationRequested && exception is OperationCanceledException or EndOfStreamException or ArgumentOutOfRangeException)
                    {
                        stream.Dispose();
                        stream = await Open(token).ConfigureAwait(false);
                        sources.Add(await PcmStreamSource.CreateAsync(stream, selection.OffsetSeconds, token, amplitude: 3.2).ConfigureAwait(false));
                    }
                    catch
                    {
                        stream.Dispose();
                        throw;
                    }
                }
                return new PreparedPsfMusic(selections, sources.ToArray(), fingerprints.ToArray());
            }
            catch
            {
                foreach (PcmStreamSource source in sources)
                {
                    source.Dispose();
                }
                throw;
            }
        }, prepared => StartPsfMusic(kind, slot, track, prepared), exception => MusicDecodeFailed(slot, exception), discard: prepared => prepared.Dispose());
    }

    private void StartPsfMusic(MusicKind kind, int slot, int track, PreparedPsfMusic prepared)
    {
        StreamingExternalMusicOutput output = new(Sound, prepared);
        try
        {
            int volume = Math.Clamp((int)(kind == MusicKind.Midi ? midiVolume : streamVolume), 0, 127);
            output.SetGain(ExternalGain(kind, volume, output.Volume));
            output.Pause(focusPaused || nativeMidiPaused);
            if (kind == MusicKind.Midi)
            {
                music?.Stop();
            }
            else
            {
                streamedMusic?.Dispose();
                streamedMusic = null;
            }
            output.Start();
        }
        catch
        {
            output.Dispose();
            throw;
        }
        externalMusic[(int)kind]?.Dispose();
        externalMusic[(int)kind] = output;
        DiscardMusicResumePosition(slot);
        musicPositionCaptures[slot] = queued => prepared.Sources.Select((source, index) => new GameplaySaveAudioPosition("psf-pcm16-v1", prepared.Fingerprints[index], "source-frames", source.PositionBeforeQueuedFrames(queued), 44100, 2, 0, 0)).ToArray();
        musicKinds[slot] = kind;
        musicIds[slot] = track;
        Console.WriteLine($"field-music=Playing track={track} slot={slot} format=PSF resources={string.Join(',', prepared.Selections.Select(selection => selection.File.PackageId + ':' + selection.File.RelativePath))}");
    }

    private sealed record PreparedPsfMusic(ExternalMusicSelection[] Selections, PcmStreamSource[] Sources, string[] Fingerprints) : IAudioSource, IDisposable
    {
        private bool paused;

        public void Pause(bool value)
        {
            Volatile.Write(ref paused, value);
        }

        public void RenderFrame(out double left, out double right)
        {
            left = 0;
            right = 0;
            if (Volatile.Read(ref paused) || Sources.Any(source => !source.HasBufferedFrame && !source.IsCompleted))
            {
                foreach (PcmStreamSource source in Sources)
                {
                    source.RecordSilentFrame();
                }
                return;
            }
            foreach (PcmStreamSource source in Sources)
            {
                source.RenderFrame(out double l, out double r);
                left += l;
                right += r;
            }
        }

        public void Dispose()
        {
            foreach (PcmStreamSource source in Sources)
            {
                source.Dispose();
            }
        }
    }

    private sealed class StreamingExternalMusicOutput(PcmAudioOutput output, PreparedPsfMusic prepared) : IExternalMusicOutput
    {
        private bool attached;
        private bool disposed;
        public int Volume => 100;

        public void Start()
        {
            output.AddSource(prepared);
            attached = true;
        }

        public void Update()
        {
            foreach (PcmStreamSource source in prepared.Sources)
            {
                source.ThrowIfFailed();
            }
        }

        public void SetGain(int gain)
        {
            for (int index = 0; index < prepared.Sources.Length; index++)
            {
                prepared.Sources[index].Volume(OutputGain(gain, prepared.Selections[index].Volume));
            }
        }

        public void Pause(bool paused)
        {
            prepared.Pause(paused);
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
                output.RemoveSource(prepared);
            }
            prepared.Dispose();
        }
    }
}
