using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native raw WORD samples plus their nullable configured projections; pending pulses and physical requests are unsampled input.</summary>
public sealed record NativeFieldButtonInput([property: JsonRequired] int Version, [property: JsonRequired] ushort RawHeld, [property: JsonRequired] ushort RawPressed, [property: JsonRequired] long SampledFrame, [property: JsonRequired] int SampledFieldId, [property: JsonRequired] ushort PhysicalHeld, [property: JsonRequired] ushort PendingPulse, [property: JsonRequired] ushort? Held, [property: JsonRequired] ushort? Pressed)
{
    public void Validate()
    {
        if (Version != 3 || (RawPressed & ~RawHeld) != 0 || SampledFrame < 0 || SampledFieldId < -1 || SampledFieldId > ushort.MaxValue || (Held is null) != (Pressed is null))
        {
            throw new InvalidDataException("Native input samples have invalid raw words, projections or callback frame.");
        }
        if (SampledFieldId == -1 && (SampledFrame != 0 || RawHeld != 0 || RawPressed != 0 || Held is not null))
        {
            throw new InvalidDataException("An unsampled native input process cannot contain invented callback samples.");
        }
    }

    public static NativeFieldButtonInput FromColdProcess()
    {
        //The deterministic managed input provider has no submitted physical requests at its explicit cold creation.
        return new NativeFieldButtonInput(3, 0, 0, 0, -1, 0, 0, null, null);
    }

    [JsonIgnore]
    public bool IsColdProcess => this == FromColdProcess();

    public void ValidateSample(int fieldId, long frame, NativeInputConfiguration? configuration)
    {
        Validate();
        if (SampledFieldId != fieldId || SampledFrame != frame)
        {
            throw new InvalidDataException("Native input has not been sampled for this actual field callback.");
        }
        if (configuration is null)
        {
            if (Held is not null || PendingPulse != 0)
            {
                throw new InvalidDataException("Unknown native input options cannot supply mapped samples or logical pulses.");
            }
        }
        else if (Held != configuration.Map(RawHeld) || Pressed != configuration.Map(RawPressed) || (PendingPulse & ~configuration.RawForMappedPulse(0x50)) != 0)
        {
            throw new InvalidDataException("Native mapped input disagrees with its actual saved options and raw samples.");
        }
    }
}
