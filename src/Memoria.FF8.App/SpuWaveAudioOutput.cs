using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Presentation of a resolved music cue; it owns no original CD device or simulation clock.</summary>
internal sealed class SpuWaveAudioOutput(string gameDirectory, string toolsDirectory, MusicCueResource resource, nint window, bool audioEnabled) : IDisposable
{
    private PcmAudioOutput? output;
    private PcmAudioOutput.Buffer? buffer;

    public bool HasLiveAudio => buffer is not null;

    public NativeSpuAudioAttempt Prepare()
    {
        resource.Validate();
        Stop();
        using GameFileResources files = new(gameDirectory);
        string path = files.ResolveInstallationFile(resource.RelativePath);
        string hash;
        using (FileStream stream = File.OpenRead(path))
        {
            hash = Convert.ToHexString(SHA256.HashData(stream));
        }
        PcmAudio pcm = PcmAudioDecoder.ReadFile(toolsDirectory, path);
        if (audioEnabled)
        {
            try
            {
                output = new PcmAudioOutput(window);
                buffer = output.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
                buffer.Play(false);
            }
            catch
            {
                Stop();
                throw;
            }
        }
        return new NativeSpuAudioAttempt(resource.Cue.ToString(), hash, pcm.Bytes.LongLength, pcm.SampleRate, pcm.Channels, buffer is not null);
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
