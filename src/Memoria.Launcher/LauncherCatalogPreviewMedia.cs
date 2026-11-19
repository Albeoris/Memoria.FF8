using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Audio;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal static class LauncherCatalogPreviewMedia
{
    internal static ModPreviewSide Decode(string? tools, ModPreviewMediaInput input, bool video, double seconds, CancellationToken token)
    {
        if (tools is null || !File.Exists(MediaToolPaths.ResolveExecutable(tools, "ffmpeg")))
        {
            return new("Set up FFmpeg in the launcher to compare media.");
        }
        string offset = seconds.ToString("0.###", CultureInfo.InvariantCulture);
        token.ThrowIfCancellationRequested();
        string? temporary = null;
        byte[] decoded;
        try
        {
            string path = input.Path ?? (temporary = Path.GetTempFileName());
            if (temporary is not null)
            {
                File.WriteAllBytes(path, input.Read?.Invoke() ?? throw new InvalidDataException("The selected media is unavailable."));
            }
            ProcessStartInfo start = new(MediaToolPaths.ResolveExecutable(tools, "ffmpeg"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            string[] common = ["-nostdin", "-v", "error", "-protocol_whitelist", "file,pipe", "-format_whitelist", "avi,mov,matroska,webm,ogg,wav,mp3,flac,bink", "-ss", offset, "-i", path];
            string[] output = video
                ? ["-an", "-frames:v", "1", "-vf", "scale=640:360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2", "-f", "rawvideo", "-pix_fmt", "rgba", "pipe:1"]
                : ["-vn", "-t", "12", "-f", "s16le", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2", "pipe:1"];
            foreach (string argument in common.Concat(output))
            {
                start.ArgumentList.Add(argument);
            }
            decoded = AudioDecodeProcess.RunAsync(start, null, token, TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
        }
        finally
        {
            if (temporary is not null)
            {
                File.Delete(temporary);
            }
        }
        if (video)
        {
            return new($"{input.Name} · {offset} s", new(640, 360, MemoryMarshal.Cast<byte, Rgba32>(decoded).ToArray()));
        }
        return new($"{input.Name} · audio sample") { AudioPcm = decoded };
    }

    internal sealed class AudioComparison : IDisposable
    {
        private SdlAudioOutput? device;
        private PcmMixer.Voice? voice;

        public void Play(byte[] pcm)
        {
            voice?.Dispose();
            voice = null;
            device ??= new();
            device.ThrowIfFailed();
            voice = device.Mixer.Create(pcm, 44100, 2);
            voice.Play(false);
        }

        public void Dispose()
        {
            voice?.Dispose();
            voice = null;
            device?.Dispose();
            device = null;
        }
    }
}
