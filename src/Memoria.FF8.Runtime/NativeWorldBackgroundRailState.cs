using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>544860/545330 ordinary-mode rail allocation independent of the selected world body and dense object family.</summary>
public sealed record NativeWorldBackgroundRailState([property: JsonRequired] int Version, [property: JsonRequired] byte LoadFlags265, [property: JsonRequired] long Callback, [property: JsonRequired] NativeTrainRailBank? Primary, [property: JsonRequired] NativeTrainRailBank? Alternate, [property: JsonRequired] NativeWorldFootCameraState Camera, [property: JsonRequired] uint? PrimaryDistanceSquared, [property: JsonRequired] uint? AlternateDistanceSquared)
{
    public int ProjectionCurveThreshold { get; init; } = 5216;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldPathPosition? DistancePosition { get; init; }
    [JsonIgnore]
    public bool HasBanks => Primary is not null || Alternate is not null;
    [JsonIgnore]
    public NativeTrainRailBank[] Banks => new[] { Primary, Alternate }.OfType<NativeTrainRailBank>().ToArray();

    public void Validate()
    {
        Camera.Validate();
        bool absent = (LoadFlags265 & 1) != 0;
        if (ProjectionCurveThreshold is < 0 or > 8192 || Version != 1 || Callback < 0 || Primary is null != absent || Alternate is null != absent || Primary is not null && Primary.TrackIndex != 0 || Alternate is not null && Alternate.TrackIndex != 1 || Primary is null != (PrimaryDistanceSquared is null) || Alternate is null != (AlternateDistanceSquared is null) || Callback == 0 && DistancePosition is not null)
        {
            throw new InvalidDataException("Native background rails lost their executed load flags, banks, callback camera or distance producer.");
        }
        if (Callback == 0 && (PrimaryDistanceSquared is not null and not uint.MaxValue || AlternateDistanceSquared is not null and not uint.MaxValue || Banks.Any(bank => bank.Pause != 30 || bank.Speed != 0 || bank.Status is not null || bank.StopCounter != 0 || bank.Direction != 1)))
        {
            throw new InvalidDataException("Native background rails differ from the ordinary545330 initial words.");
        }
    }
}
