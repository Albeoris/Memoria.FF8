namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeSoundRequest(int Channel, int Sound, int Pan = 64, bool StopStream = false);

public sealed partial class Ff7FieldHost
{
    public Action<Ff7NativeSoundRequest>? NativeSoundRequested { get; set; }

    public void SubmitNativeSound(Ff7NativeSoundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Channel is < 1 or > 5 || request.Sound is < 0 or > 750 || request.Pan is < 0 or > 127)
        {
            throw new InvalidDataException("The native module sound request is invalid.");
        }
        if (request.Sound != 0)
        {
            _ = nativeSoundDescription?.Invoke(request.Sound);
        }
        ApplyNativeSoundStartState(request.Channel, request.Sound, (byte)request.Pan, request.StopStream);
        NativeSoundRequested?.Invoke(request);
        bool[] stopped = (bool[])Process.SoundChannelStoppedByFade.Clone();
        stopped[request.Channel - 1] = false;
        Process = Process with { SoundChannelStoppedByFade = stopped };
    }

    public int ResolveNativeSoundPlaybackPan(Ff7NativeSoundRequest request, bool streamed)
    {
        if (request.Channel is < 1 or > 5 || request.Pan is < 0 or > 127)
        {
            throw new InvalidDataException("The native module sound pan request is invalid.");
        }
        return streamed ? request.Pan : Process.SoundChannelBufferPans[request.Channel - 1]?.Pan ?? Process.SoundChannelPans[request.Channel - 1];
    }
}
