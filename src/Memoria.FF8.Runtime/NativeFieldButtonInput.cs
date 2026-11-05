using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeFieldInputOrigin
{
    Sampled,
    GameplayContinuationReset
}

/// <summary>Native raw WORD samples plus their nullable configured projections; pending pulses and physical requests are unsampled input.</summary>
public sealed record NativeFieldButtonInput([property: JsonRequired] int Version, [property: JsonRequired] ushort RawHeld, [property: JsonRequired] ushort RawPressed, [property: JsonRequired] long SampledFrame, [property: JsonRequired] int SampledFieldId, [property: JsonRequired] ushort PhysicalHeld, [property: JsonRequired] ushort PendingPulse, [property: JsonRequired] ushort? Held, [property: JsonRequired] ushort? Pressed)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public NativeFieldInputOrigin Origin { get; init; }

    public void Validate()
    {
        if (Version != 3 || !Enum.IsDefined(Origin) || (RawPressed & ~RawHeld) != 0 || SampledFrame < 0 || SampledFieldId < -1 || SampledFieldId > ushort.MaxValue || (Held is null) != (Pressed is null) || Origin == NativeFieldInputOrigin.GameplayContinuationReset && (SampledFieldId < 0 || RawHeld != 0 || RawPressed != 0 || Held is not (null or 0) || Pressed is not (null or 0)))
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

    /// <summary>Starts a released input epoch at a restored logical frame without claiming an old hardware callback.</summary>
    public static NativeFieldButtonInput FromGameplayContinuation(int fieldId, long frame, NativeInputConfiguration? configuration)
    {
        NativeFieldButtonInput result = new(3, 0, 0, frame, fieldId, 0, 0, configuration?.Map(0), configuration?.Map(0)) { Origin = NativeFieldInputOrigin.GameplayContinuationReset };
        result.ValidateSample(fieldId, frame, configuration);
        return result;
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
