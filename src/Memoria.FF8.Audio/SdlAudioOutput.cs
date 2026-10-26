using System.Runtime.InteropServices;

namespace Memoria.FF8.Audio;

/// <summary>SDL3 owns device conversion and hot-plug routing; one background task supplies 10 ms PCM blocks.</summary>
public sealed class SdlAudioOutput : IDisposable
{
    private readonly CancellationTokenSource stopping = new();
    private readonly Task worker;
    private Exception? error;
    private int disposed;
    public PcmMixer Mixer { get; } = new();
    public Exception? Error => Volatile.Read(ref error);

    public SdlAudioOutput()
    {
        try
        {
            if (!SDL_InitSubSystem(0x10))
            {
                throw Failure("Initialize audio");
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new IOException("The matching x64 SDL3 audio runtime is unavailable. Repair the installation's native dependencies and restart audio.", exception);
        }
        AudioSpec spec = new() { Format = 0x8010, Channels = 2, Frequency = Mixer.SampleRate };
        nint stream = SDL_OpenAudioDeviceStream(uint.MaxValue, ref spec, 0, 0);
        if (stream == 0)
        {
            SDL_QuitSubSystem(0x10);
            throw Failure("Open the default playback device. Select or reconnect an audio device and restart audio");
        }
        worker = Task.Run(() => Run(stream));
    }

    public void ThrowIfFailed()
    {
        if (Error is Exception failure)
        {
            throw new IOException("Audio device playback failed. Reconnect the device and restart audio.", failure);
        }
    }

    private async Task Run(nint stream)
    {
        short[] block = new short[882];
        try
        {
            if (!SDL_ResumeAudioStreamDevice(stream))
            {
                throw Failure("Resume audio device");
            }
            while (!stopping.IsCancellationRequested)
            {
                int queued = SDL_GetAudioStreamQueued(stream);
                if (queued < 0)
                {
                    throw Failure("Query audio stream");
                }
                if (queued < 4410)
                {
                    Mixer.Render(block);
                    if (!SDL_PutAudioStreamData(stream, block, block.Length * 2))
                    {
                        throw Failure("Queue audio samples");
                    }
                }
                else
                {
                    await Task.Delay(5, stopping.Token);
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Volatile.Write(ref error, exception);
        }
        finally
        {
            SDL_DestroyAudioStream(stream);
            SDL_QuitSubSystem(0x10);
        }
    }

    private static IOException Failure(string action)
    {
        return new IOException($"{action}: {Marshal.PtrToStringUTF8(SDL_GetError())}");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        stopping.Cancel();
        worker.GetAwaiter().GetResult();
        stopping.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioSpec
    {
        public int Format;
        public int Channels;
        public int Frequency;
    }

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_InitSubSystem(uint flags);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_QuitSubSystem(uint flags);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SDL_GetError();
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint SDL_OpenAudioDeviceStream(uint device, ref AudioSpec spec, nint callback, nint userdata);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_ResumeAudioStreamDevice(nint stream);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_GetAudioStreamQueued(nint stream);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_PutAudioStreamData(nint stream, short[] data, int length);
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_DestroyAudioStream(nint stream);
}
