using Memoria.FF8.Runtime;

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
        int[] selected;
        if (mask == 0xFFFFFF)
        {
            selected = services.GlobalSoundProcess is { } process ? Enumerable.Range(1, 28).Where(voice => process.OriginalEffectIds[voice] is >= 10000).ToArray() : [];
        }
        else if (mask != 0)
        {
            selected = Enumerable.Range(1, 28).Where(voice => (mask & (1u << (voice - 1))) != 0).ToArray();
        }
        else
        {
            //46B450(-2,0) uses46B240's first-match lookup after unsigned-WORD conversion and46B120 mapping.
            NativeGlobalSoundProcess process = services.GlobalSoundProcess ?? throw new NotSupportedException("Zero-mask field sound departure requires its retained voice identities.");
            process.Validate();
            int sample = MapGlobalSoundSample(unchecked((ushort)-2));
            selected = [];
            for (int voice = 0; voice < 30; voice++)
            {
                uint identity = process.OriginalEffectIds[voice] ?? throw new NotSupportedException("Zero-mask field sound lookup encountered an unknown earlier voice identity.");
                if (identity == sample)
                {
                    selected = [voice];
                    break;
                }
            }
        }
        AudioCommandExecuted?.Invoke(new NativeFieldSoundDeparture(FieldId, selected));
    }
}
