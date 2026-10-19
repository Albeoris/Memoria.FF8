namespace Memoria.FF8.Events;

public sealed record NativeFieldSoundDeparture(int FieldId, int[] Voices);

public sealed partial class EventEngine
{
    /// <summary>Transient host output of executed commands and animation contacts; device state never participates in VM waits or saves.</summary>
    public event Action<object>? AudioCommandExecuted;

    private void StopDepartingFieldSounds()
    {
        //46B450 treats low24 FFFFFF as the field-ID sentinel; other masks select physical voices.
        uint mask = Scene.NativeFieldSoundStopMask & 0xFFFFFF;
        int[] selected = mask == 0xFFFFFF ? services.GlobalSoundProcess is { } process ? Enumerable.Range(1, 28).Where(voice => process.OriginalEffectIds[voice] is >= 10000).ToArray() : [] : mask == 0 ? [] : Enumerable.Range(1, 28).Where(voice => (mask & (1u << (voice - 1))) != 0).ToArray();
        AudioCommandExecuted?.Invoke(new NativeFieldSoundDeparture(FieldId, selected));
    }
}
