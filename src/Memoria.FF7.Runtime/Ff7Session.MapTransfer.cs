namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private void CompleteWorldMapTransfer(Ff7WorldRuntime source, int destinationWorld)
    {
        if (source.ReadyMapTransfer != destinationWorld)
        {
            throw new InvalidOperationException("FFVII submarine transfer must still own the world handoff.");
        }
        byte[] persistent = source.CaptureNativeWorldExit();
        byte[] party = persistent.AsSpan(0x109, 3).ToArray();
        Ff7WorldRuntime destination = new(installation, persistent, 0, party, destinationWorld);
        try
        {
            destination.PrepareSubmarineMapEntry();
        }
        catch
        {
            destination.Dispose();
            throw;
        }
        World = destination;
        source.Dispose();
        Host.SubmitNativeAudio(new(0xF1));
        Host.ResetNativeSoundPitches();
    }
}
