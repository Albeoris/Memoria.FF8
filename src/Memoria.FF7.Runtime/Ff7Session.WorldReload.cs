using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private void CompleteWorldReload(Ff7WorldRuntime source, Ff7WorldFieldDeparture request)
    {
        if (source.ReadyFieldDeparture != request || request.Operation != WorldOperation.FieldTransitionSelector)
        {
            throw new InvalidOperationException("FFVII world reload must still own the world handoff.");
        }

        // State 8 publishes its selector to the next World initialization.
        Ff7WorldRuntime destination = PrepareWorldReload(source, request.Selector);
        World = destination;
        source.Dispose();
        Host.SubmitNativeAudio(new(0xF1));
        Host.ResetNativeSoundPitches();
    }

    private Ff7WorldRuntime PrepareWorldReload(Ff7WorldRuntime source, ushort selector)
    {
        byte[] persistent = source.CaptureNativeWorldExit();
        byte[] party = persistent.AsSpan(0x109, 3).ToArray();
        return new(installation, persistent, selector, party, Ff7WorldRuntime.ResolveWorldEntryMap(selector));
    }
}
