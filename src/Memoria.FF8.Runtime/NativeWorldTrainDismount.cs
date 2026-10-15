using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The complete twelve-byte station record read by548AF0 from20402DC, bound to WMSET section12 by542DA0.</summary>
public sealed record NativeWorldTrainStation([property: JsonRequired] int Index, [property: JsonRequired] string SectionHash, [property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public int X => ReadInt32(0);
    public int RawZ => ReadInt32(4);
    public short NativeHeight => BinaryPrimitives.ReadInt16LittleEndian(RequireBytes()[8..]);

    public void Validate()
    {
        if (Index is < 0 or > 2 || SectionHash is not { Length: 64 } || !SectionHash.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Native train station lost its original section identity or index.");
        }
        _ = RequireBytes();
        if (X is < -131072 or >= 131072 || RawZ is < -98304 or >= 98304)
        {
            throw new InvalidDataException("Native train station position lies outside the original centered world spans.");
        }
    }

    private int ReadInt32(int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(RequireBytes()[offset..]);
    }

    private ReadOnlySpan<byte> RequireBytes()
    {
        if (Bytes.IsDefault || Bytes.Length != 12)
        {
            throw new InvalidDataException("Native train station must retain the complete original twelve-byte record.");
        }
        return Bytes.AsSpan();
    }
}

/// <summary>Executed5484B0 choice0/548AF0 transition, without a world reload or new ground/camera initialization.</summary>
public sealed record NativeWorldTrainDismountReceipt([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] long SourceCallback, [property: JsonRequired] NativeWorldTrainStation Station, [property: JsonRequired] int ControlledObjectIndex)
{
    public const string DismountRule = "ff8-en-train-station-choice0-5484b0-548af0-v1";

    public static NativeWorldTrainDismountReceipt Create(NativeTrainWorldRuntimeState train, NativeWorldTrainStation station)
    {
        ArgumentNullException.ThrowIfNull(train);
        ArgumentNullException.ThrowIfNull(station);
        train.Validate();
        station.Validate();
        int selectedStation = train.Primary.Status is not (1 or 2) ? -1 : train.Primary.StopCounter == 1 ? train.Primary.TrackIndex == 0 ? 0 : 2 : train.Primary.TrackIndex == 0 && train.Primary.Direction == -1 && train.Primary.StopCounter == 0 ? 1 : -1;
        if (selectedStation != station.Index || train.Primary.Pause != -1)
        {
            throw new InvalidOperationException("Native train dismount requires the current5484B0 station choice.");
        }
        var receipt = new NativeWorldTrainDismountReceipt(1, DismountRule, train.Callback, station, train.Load.Allocation.ControlledObjectIndex);
        receipt.Validate(train);
        return receipt;
    }

    public void Validate(NativeTrainWorldRuntimeState trains)
    {
        ArgumentNullException.ThrowIfNull(trains);
        Station?.Validate();
        if (Version != 1 || Rule != DismountRule || SourceCallback < 1 || SourceCallback > trains.Callback || Station is null || ControlledObjectIndex != trains.Load.Allocation.ControlledObjectIndex || trains.ControlledObjectEntityType is not (0 or 1))
        {
            throw new InvalidDataException("Native train dismount receipt lost its executed source, station or selected foot record.");
        }
    }
}
