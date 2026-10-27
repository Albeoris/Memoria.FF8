using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Presentation of a resolved music cue; it owns no original CD device or simulation clock.</summary>
internal sealed class SpuWaveAudioOutput(string gameDirectory, MusicCueResource resource, bool audioEnabled) : IDisposable
{
    private PcmAudioOutput? output;
    private PcmAudioOutput.Buffer? buffer;

    internal Func<PcmAudioOutput>? OutputFactory { get; init; }
    public string? Error { get; private set; }

    public bool HasLiveAudio
    {
        get
        {
            if (output?.Error is Exception failure)
            {
                ReportFailure(failure);
            }
            return buffer is not null;
        }
    }

    public NativeSpuAudioAttempt Prepare()
    {
        resource.Validate();
        Error = null;
        Stop();
        using GameFileResources files = new(gameDirectory);
        string path = files.ResolveInstallationFile(resource.RelativePath);
        byte[] source = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(source));
        PcmAudio pcm = PcmAudioDecoder.ReadWave(source);
        if (audioEnabled)
        {
            try
            {
                output = OutputFactory?.Invoke() ?? new PcmAudioOutput();
                buffer = output.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
                buffer.Play(false);
            }
            catch (Exception exception) when (exception is IOException or NotSupportedException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Stop();
                ReportFailure(exception);
            }
        }
        return new NativeSpuAudioAttempt(resource.Cue.ToString(), hash, pcm.Bytes.LongLength, pcm.SampleRate, pcm.Channels, buffer is not null);
    }

    private void ReportFailure(Exception exception)
    {
        if (Error != exception.Message)
        {
            Error = exception.Message;
            Console.Error.WriteLine($"spu-audio=Failed {Error}");
        }
    }

    public void Stop()
    {
        try
        {
            buffer?.Dispose();
        }
        finally
        {
            buffer = null;
            output?.Dispose();
            output = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
