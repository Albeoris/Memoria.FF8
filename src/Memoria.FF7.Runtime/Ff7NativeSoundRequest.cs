namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeSoundRequest(int Channel, int Sound, int Pan = 64, bool StopStream = false);

public sealed partial class Ff7FieldHost
{
    public Action<Ff7NativeSoundRequest>? NativeSoundRequested { get; set; }

    public void InitializeNativeMenuAudio()
    {
        SetSoundVolume(5, 0, 127);
    }

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

    public int PrepareBattleSoundPlayback(Ff7BattleSoundReceipt receipt, bool streamed)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Channel is < 1 or > 5 || receipt.Sound is < 1 or > 750 || !receipt.RawPan && receipt.Pan > 127)
        {
            throw new InvalidDataException("The native battle sound request is invalid.");
        }

        Ff7NativeSoundRequest request = new(receipt.Channel, receipt.Sound, receipt.Pan, StopStream: true);
        ApplyNativeSoundStartState(request.Channel, request.Sound, (byte)request.Pan, request.StopStream);
        return ResolveNativeSoundPlaybackPan(request, streamed);
    }

    public int ResolveNativeSoundPlaybackPan(Ff7NativeSoundRequest request, bool streamed)
    {
        if (request.Channel is < 1 or > 5 || request.Pan is < 0 or > 255)
        {
            throw new InvalidDataException("The native module sound pan request is invalid.");
        }
        if (streamed && request.Pan > 127)
        {
            throw new NotSupportedException("FFVII streamed sound panning exceeds the admitted native table.");
        }
        return streamed ? request.Pan : Process.SoundChannelBufferPans[request.Channel - 1]?.Pan ?? Process.SoundChannelPans[request.Channel - 1];
    }
}
