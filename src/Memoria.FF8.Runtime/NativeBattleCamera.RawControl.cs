using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleCameraState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? Control05 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? ModeHigh19 { get; init; }

    /// <summary>50A730 retains the other control bits and writes the complete two-byte camera mode owner.</summary>
    public NativeBattleCameraState Apply50A730()
    {
        byte control = Control05 ?? throw new NotSupportedException("Native50A730 requires its retained camera control byte before OR80.");
        return this with { Control05 = (byte)(control | 0x80), Mode = 0, ModeHigh19 = 0 };
    }
}
