using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Fresh544860/545330 ordinary-foot allocation, rail banks and the prior54E650 proximity words. No riding or dismount history is implied.</summary>
public sealed record NativeNormalFootObjectProcess([property: JsonRequired] int Version, [property: JsonRequired] NativeTrainWorldAllocation Allocation, [property: JsonRequired] long Callback, [property: JsonRequired] NativeTrainRailBank? Primary, [property: JsonRequired] NativeTrainRailBank? Alternate, [property: JsonRequired] NativeWorldFootCameraState Camera, [property: JsonRequired] uint? PrimaryDistanceSquared, [property: JsonRequired] uint? AlternateDistanceSquared)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTrainWorldRuntimeState? RidingTrainSource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldPathPosition? CarDistancePosition { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ForcedAnimationC76628 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? GuardianForce13Flags { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldPathPosition? CactuarLoadPosition { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeGardenWorldObject? CactuarBody { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeCactuarWorldState? Cactuar { get; init; }

    public void Validate()
    {
        Allocation.Validate();
        Camera.Validate();
        bool railsAbsent = (Allocation.EventFlags103 & 1) != 0;
        ushort[] operands = Allocation.ClauseIndex switch
        {
            3 when Allocation.MinimumStory == 290 && Allocation.MaximumStory == 345 => [0, 0x034B, 0x054B, 0x064B, 0x0A4B, 0x0B4B, 0x225F, 0x235F, 0x245F],
            4 when Allocation.MinimumStory == 345 && Allocation.MaximumStory == 348 => [0, 0x0C44, 0x0D45, 0x3F45, 0x0E44, 0x034B, 0x054B, 0x064B, 0x0A4B, 0x0B4B, 0x225F, 0x235F, 0x245F],
            5 when Allocation.MinimumStory == 348 && Allocation.MaximumStory == 392 => [0],
            12 when Allocation.MinimumStory == 2140 && Allocation.MaximumStory == 2500 => [5, 0x1C59],
            _ => throw new NotSupportedException("Native normal foot object allocation reached an unimplemented original clause.")
        };
        if (Allocation.ClauseIndex == 12 && (GuardianForce13Flags is null || CactuarLoadPosition is null || (GuardianForce13Flags.Value & 1) == 0 != (CactuarBody is not null) || (GuardianForce13Flags.Value & 1) == 0 != (Cactuar is not null) || CactuarBody is not null && (CactuarBody.ShapeId != 94 || CactuarBody.Flags != 0)) || ForcedAnimationC76628 is not (null or -1 or 0 or 4))
        {
            throw new InvalidDataException("Native type2 allocation lost its original Cactuar or retained forced-animation owner.");
        }
        Cactuar?.Validate();
        if (Version != 1 || Callback < 0 || Allocation.ControlledObjectIndex != 0 || Allocation.Objects.Length != operands.Length || Allocation.Objects.Where((item, index) => item.RawOperand != operands[index]).Any() || Primary is null != railsAbsent || Alternate is null != railsAbsent || Primary is not null && Primary.TrackIndex != 0 || Alternate is not null && Alternate.TrackIndex != 1 || Primary is null != (PrimaryDistanceSquared is null) || Alternate is null != (AlternateDistanceSquared is null))
        {
            throw new InvalidDataException("Native ordinary foot object process lost its selected original clause allocation, track selection or previous proximity words.");
        }
        ValidateRailState();
    }

    /// <summary>The same544860/545330 ordinary object process with a selected car body.</summary>
    public void ValidateCar()
    {
        Allocation.Validate();
        Camera.Validate();
        bool railsAbsent = (Allocation.EventFlags103 & 1) != 0;
        bool hasTrack0 = !railsAbsent;
        bool hasTrack1 = !railsAbsent;
        if (RidingTrainSource is { } riding)
        {
            riding.Validate();
            if (riding.Callback > Callback || riding.Load.Allocation.ProgramHash != Allocation.ProgramHash || riding.Load.Allocation.CoordinateHash != Allocation.CoordinateHash || riding.Load.Allocation.Story != Allocation.Story || riding.Load.Allocation.EventFlags103 != Allocation.EventFlags103 || !riding.Load.Allocation.ClauseWords.SequenceEqual(Allocation.ClauseWords))
            {
                throw new InvalidDataException("Native car retained train process differs from its executed riding allocation.");
            }
            hasTrack0 = riding.Primary.TrackIndex == 0 || riding.Alternate?.TrackIndex == 0;
            hasTrack1 = riding.Primary.TrackIndex == 1 || riding.Alternate?.TrackIndex == 1;
        }
        if (Version != 1 || Callback < 0 || (Primary is not null) != hasTrack0 || (Alternate is not null) != hasTrack1 || Primary is not null && Primary.TrackIndex != 0 || Alternate is not null && Alternate.TrackIndex != 1 || Primary is null != (PrimaryDistanceSquared is null) || Alternate is null != (AlternateDistanceSquared is null) || ForcedAnimationC76628 is not null || GuardianForce13Flags is not null || CactuarLoadPosition is not null || CactuarBody is not null || Cactuar is not null)
        {
            throw new InvalidDataException("Native car object process lost its original rail selection or prior proximity words.");
        }
        ValidateRailState();
    }

    private void ValidateRailState()
    {
        if (Callback == 0 && (PrimaryDistanceSquared is not null and not uint.MaxValue || AlternateDistanceSquared is not null and not uint.MaxValue || Primary is not null && (Primary.Pause != 30 || Primary.Speed != 0 || Primary.Status is not null || Primary.StopCounter != 0 || Primary.Direction != 1) || Alternate is not null && (Alternate.Pause != 30 || Alternate.Speed != 0 || Alternate.Status is not null || Alternate.StopCounter != 0 || Alternate.Direction != 1)))
        {
            throw new InvalidDataException("Native ordinary foot loader differs from545330's actual initial rail and distance words.");
        }
    }

    /// <summary>545480 ordinary mode0 branch, after pause decrement, before the common physical rail walker.</summary>
    public static int? SelectSpeed(NativeTrainRailBank bank, uint precedingDistanceSquared, ushort playerRadius)
    {
        if (bank.Pause is < 0 or > 1 || precedingDistanceSquared >= 0x800000)
        {
            return null;
        }
        uint radiusSquared = (uint)playerRadius * playerRadius;
        if (radiusSquared >= 0x800000)
        {
            throw new NotSupportedException("Native ordinary train speed requires the reached bounded foot radius.");
        }
        int accelerated = (bank.Speed == 0 ? 16 : bank.Speed) + 4;
        uint difference = precedingDistanceSquared > radiusSquared ? precedingDistanceSquared - radiusSquared : 0;
        int desired = (int)Math.Min(100u, 8 * (unchecked(difference * 100) / (0x800000 - radiusSquared)));
        return accelerated < 32 ? 32 : Math.Min(accelerated, desired);
    }
}
