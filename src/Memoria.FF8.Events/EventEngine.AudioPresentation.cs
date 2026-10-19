namespace Memoria.FF8.Events;

public sealed record NativeFieldSoundDeparture(int FieldId, int[] Voices);

public sealed partial class EventEngine
{
    /// <summary>Transient host output of executed commands and animation contacts; device state never participates in VM waits or saves.</summary>
    public event Action<object>? AudioCommandExecuted;

    private void StopDepartingFieldSounds()
    {
        // Native471F70 calls46B450(-2, FFFFFF) before the next field loader.
        // Unknown14, which changes that mask, remains unsupported by the VM.
        // The default path stops field effect IDs on voices1..28 without clearing their identities.
        int[] voices = services.GlobalSoundProcess is { } process ? Enumerable.Range(1, 28).Where(voice => process.OriginalEffectIds[voice] is >= 10000).ToArray() : [];
        AudioCommandExecuted?.Invoke(new NativeFieldSoundDeparture(FieldId, voices));
    }
}
