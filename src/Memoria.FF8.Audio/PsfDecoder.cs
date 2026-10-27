using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Audio;

/// <summary>Runs the separately licensed PSF codec through bounded, backpressured pipes.</summary>
public static class PsfDecoder
{
    public static async Task<PsfPcmStream> OpenAsync(string resourceName, byte[] file, Func<string, string, byte[]> resolveLibrary, byte[] bios, bool repeat, CancellationToken cancellationToken = default, string? decoderPath = null, bool suppressOpeningSilence = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(resolveLibrary);
        if (bios.Length != 524288 || !bios.AsSpan(0x80, 19).SequenceEqual("Highly Experimental"u8))
        {
            throw new InvalidDataException("PSF1 requires an externally supplied 512 KiB Highly Experimental BIOS image.");
        }
        string environment = Encoding.ASCII.GetString(bios, 0x80, 128);
        foreach (string name in new[] { "ps1preboot", "ps2preboot" })
        {
            Match match = Regex.Match(environment, @"(?:^| )" + name + @"=([0-9a-fA-F]{1,8})(?: |\x00|$)");
            if (!match.Success || !uint.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint address) || address >= bios.Length)
            {
                throw new InvalidDataException("PSF BIOS has no supported preboot environment.");
            }
        }
        PsfDocument root = PsfDocument.Read(file);
        var programs = new List<byte[]>();
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        long total = 0;
        void Load(string name, PsfDocument document, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > 16 || ++count > 128 || !active.Add(name))
            {
                throw new InvalidDataException("PSF library graph is cyclic or exceeds supported limits.");
            }
            int primary = document.Metadata.Tags.ContainsKey("_lib") ? 1 : 0;
            void LoadLibrary(string dependency)
            {
                string resolved = ResolveLibraryName(name, dependency);
                byte[] bytes = resolveLibrary(name, dependency);
                total += bytes.Length;
                if (total > 64 * 1024 * 1024)
                {
                    throw new InvalidDataException("PSF library graph exceeds its input budget.");
                }
                Load(resolved, PsfDocument.Read(bytes), depth + 1);
            }
            if (primary != 0)
            {
                LoadLibrary(document.Libraries[0]);
            }
            if (document.Program.Length != 0)
            {
                programs.Add(document.Program);
            }
            foreach (string dependency in document.Libraries.Skip(primary))
            {
                LoadLibrary(dependency);
            }
            active.Remove(name);
        }
        Load(ResolveLibraryName("", resourceName), root, 0);
        if (programs.Count == 0)
        {
            throw new InvalidDataException("PSF contains no executable program.");
        }
        int refresh = Encoding.ASCII.GetString(programs[0], 113, 6) == "Europe" ? 50 : 60;
        if (root.Metadata.Tags.TryGetValue("_refresh", out string? value))
        {
            if (!int.TryParse(value, out refresh) || refresh is not (50 or 60))
            {
                throw new InvalidDataException("PSF refresh must be 50 or 60 Hz.");
            }
        }
        decoderPath ??= Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Memoria.PsfDecoder.exe" : "Memoria.PsfDecoder");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(decoderPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }
        };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("PSF decoder did not start.");
        }
        var stream = new PsfPcmStream(process, root.Metadata, repeat, suppressOpeningSilence);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var abort = timeout.Token.Register(stream.Abort);
            byte[] header = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(header, 0x31534650u);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), bios.Length);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), programs.Count);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), refresh);
            await process.StandardInput.BaseStream.WriteAsync(header, timeout.Token);
            await process.StandardInput.BaseStream.WriteAsync(bios, timeout.Token);
            foreach (byte[] program in programs)
            {
                byte[] size = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(size, program.Length);
                await process.StandardInput.BaseStream.WriteAsync(size, timeout.Token);
                await process.StandardInput.BaseStream.WriteAsync(program, timeout.Token);
            }
            await process.StandardInput.BaseStream.FlushAsync(timeout.Token);
            byte[] response = new byte[4];
            await process.StandardOutput.BaseStream.ReadExactlyAsync(response, timeout.Token);
            if (!response.AsSpan().SequenceEqual("PSF1"u8))
            {
                throw new InvalidDataException("PSF decoder returned an invalid handshake.");
            }
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private static string ResolveLibraryName(string requester, string dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency) || dependency.Contains(':') || dependency.StartsWith('/') || dependency.StartsWith('\\'))
        {
            throw new InvalidDataException("PSF library must use a relative resource name.");
        }
        string normalized = requester.Replace('\\', '/');
        int slash = normalized.LastIndexOf('/');
        string combined = (slash < 0 ? "" : normalized[..(slash + 1)]) + dependency.Replace('\\', '/');
        var parts = new List<string>();
        foreach (string part in combined.Split('/'))
        {
            if (part is "" or ".")
            {
                continue;
            }
            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    throw new InvalidDataException("PSF library escapes its resource root.");
                }
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }
        return string.Join('/', parts);
    }
}

public sealed class PsfPcmStream : Stream
{
    private readonly Process process;
    private readonly Task<string> stderr;
    private readonly bool repeat;
    private readonly bool suppressOpeningSilence;
    private bool openingSilenceSuppressed;
    private long openingSilenceFrames;
    private long position;
    private bool disposed;
    public const int SampleRate = 44100;
    public const int Channels = 2;
    public PsfMetadata Metadata { get; }
    public override bool CanRead => !disposed;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => position * 4;
        set => throw new NotSupportedException();
    }

    internal PsfPcmStream(Process process, PsfMetadata metadata, bool repeat, bool suppressOpeningSilence)
    {
        this.process = process;
        this.repeat = repeat;
        this.suppressOpeningSilence = suppressOpeningSilence;
        Metadata = metadata;
        stderr = process.StandardError.ReadToEndAsync();
    }

    internal void Abort()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Reads at most 4096 interleaved stereo frames. Calls must be serialized.</summary>
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (destination.Length % 4 != 0)
        {
            throw new ArgumentException("PSF output must be aligned to stereo PCM16 frames.", nameof(destination));
        }
        int frames = Math.Min(destination.Length / 4, 4096);
        long length = (long)(Metadata.Length.TotalSeconds * SampleRate);
        long fade = (long)(Metadata.Fade.TotalSeconds * SampleRate);
        if (!repeat)
        {
            frames = (int)Math.Min(frames, Math.Max(0, length + fade - position));
        }
        if (frames == 0)
        {
            return 0;
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var abort = timeout.Token.Register(Abort);
        try
        {
            byte[] request = new byte[4];
            if (suppressOpeningSilence && !openingSilenceSuppressed)
            {
                frames = Math.Min(frames, 1024);
            }
            while (true)
            {
                BinaryPrimitives.WriteInt32LittleEndian(request, frames);
                await process.StandardInput.BaseStream.WriteAsync(request, timeout.Token);
                await process.StandardInput.BaseStream.FlushAsync(timeout.Token);
                await process.StandardOutput.BaseStream.ReadExactlyAsync(destination[..(frames * 4)], timeout.Token);
                if (suppressOpeningSilence && !openingSilenceSuppressed)
                {
                    int prefix = CountSilentFrames(destination.Span[..(frames * 4)]);
                    if (prefix == frames)
                    {
                        openingSilenceFrames += frames;
                        if (openingSilenceFrames >= 30L * SampleRate)
                        {
                            throw new InvalidDataException("PSF opening silence exceeds 30 seconds.");
                        }
                        continue;
                    }
                    destination.Span.Slice(prefix * 4, (frames - prefix) * 4).CopyTo(destination.Span);
                    frames -= prefix;
                    openingSilenceSuppressed = true;
                }
                break;
            }
            ApplyGain(destination.Span[..(frames * 4)], frames, length, fade);
            position += frames;
            return frames * 4;
        }
        catch
        {
            Abort();
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private static int CountSilentFrames(ReadOnlySpan<byte> pcm)
    {
        int frame = 0;
        while (frame < pcm.Length / 4 && BinaryPrimitives.ReadUInt32LittleEndian(pcm[(frame * 4)..]) == 0)
        {
            frame++;
        }
        return frame;
    }

    private void ApplyGain(Span<byte> pcm, int frames, long length, long fade)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            double gain = Metadata.Volume;
            if (!repeat && position + frame >= length)
            {
                gain *= fade == 0 ? 0 : (double)(length + fade - position - frame) / fade;
            }
            for (int channel = 0; channel < 2; channel++)
            {
                Span<byte> sample = pcm.Slice(frame * 4 + channel * 2, 2);
                short original = BinaryPrimitives.ReadInt16LittleEndian(sample);
                BinaryPrimitives.WriteInt16LittleEndian(sample, (short)Math.Clamp((int)(original * gain), short.MinValue, short.MaxValue));
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override void Flush()
    {
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            Abort();
            process.WaitForExit();
            stderr.GetAwaiter().GetResult();
            process.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Abort();
        await process.WaitForExitAsync();
        await stderr;
        process.Dispose();
        GC.SuppressFinalize(this);
    }
}
