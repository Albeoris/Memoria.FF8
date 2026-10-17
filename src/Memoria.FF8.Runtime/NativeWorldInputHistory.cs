using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The current and prior mapped low WORDs from cold process memory or actual world input callbacks.</summary>
public sealed record NativeWorldInputHistory([property: JsonRequired] ushort CurrentButtons, [property: JsonRequired] ushort? PreviousButtons, [property: JsonRequired] long Samples)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? BufferIndex { get; init; }

    [JsonIgnore]
    public ushort? PreviousButtonsAfterWorldLoad => BufferIndex == 0 ? CurrentButtons : BufferIndex == 1 ? PreviousButtons : null;

    public static NativeWorldInputHistory FromColdProcess()
    {
        // Both alternating559240 input buffers start in zero-filled process memory.
        return new NativeWorldInputHistory(0, 0, 0) { BufferIndex = 0 };
    }

    public void Validate()
    {
        if (BufferIndex is > 1 || Samples < 0 || Samples == 0 && (CurrentButtons != 0 || PreviousButtons != 0 || BufferIndex is not (null or 0)) || Samples == 1 && PreviousButtons is not (null or 0) || Samples > 1 && PreviousButtons is null && BufferIndex != 1)
        {
            throw new InvalidDataException("World input history requires cold zero buffers or actual sampled callbacks.");
        }
    }

    public static NativeWorldInputHistory Sample(NativeWorldInputHistory? previous, uint buttons)
    {
        previous?.Validate();
        if (buttons > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(buttons), "World mapped input requires an unsigned WORD.");
        }
        return new NativeWorldInputHistory((ushort)buttons, previous?.CurrentButtons, checked((previous?.Samples ?? 0) + 1)) { BufferIndex = previous?.BufferIndex is byte index ? (byte)(1 - index) : null };
    }

    public static NativeWorldInputHistory SampleAfterWorldLoad(NativeWorldInputHistory? previous, uint buttons)
    {
        previous?.Validate();
        if (buttons > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(buttons), "World mapped input requires an unsigned WORD.");
        }
        //542B90 resets the buffer index only;559240 next writes slot1 and reads slot0.
        return new NativeWorldInputHistory((ushort)buttons, previous?.PreviousButtonsAfterWorldLoad, checked((previous?.Samples ?? 0) + 1)) { BufferIndex = 1 };
    }
}
