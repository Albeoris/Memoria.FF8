using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The original allocation operand and full sixteen-byte coordinate, without dropping packed orientation words.</summary>
public sealed record NativeTrainWorldObject([property: JsonRequired] byte ShapeId, [property: JsonRequired] byte CoordinateIndex, [property: JsonRequired] NativeSavedWorldPose Pose, [property: JsonRequired] ushort RawOperand, [property: JsonRequired] ImmutableArray<byte> CoordinateBytes)
{
    public int EntityType => ClassifyShape(ShapeId);
    public NativeWorldPathPosition Position => new(ReadCoordinate(0), ReadCoordinate(4), ReadCoordinate(8));
    public int PackedOrientation => ReadCoordinate(12);
    public short Roll => unchecked((short)(PackedOrientation >> 16));

    public void Validate()
    {
        if (RawOperand != ShapeId + (CoordinateIndex << 8) || CoordinateIndex >= 128 || CoordinateBytes.IsDefault || CoordinateBytes.Length != 16 || Pose is null)
        {
            throw new InvalidDataException("Native world allocation lost its original operand or complete coordinate record.");
        }
        if (ShapeId is 1 or 3 or 64 or 65)
        {
            throw new NotSupportedException("Native world allocation requires an unrepresented saved-vehicle or conditional shape producer.");
        }
        NativeWorldPathPosition position = Position;
        if (position.Y is < short.MinValue or > short.MaxValue || position.Z == int.MinValue || Pose != new NativeSavedWorldPose(position.X, -position.Z, (short)position.Y, unchecked((ushort)PackedOrientation)))
        {
            throw new InvalidDataException("Native world allocation saved-pose view differs from its unmodified original coordinate.");
        }
    }

    private int ReadCoordinate(int offset)
    {
        if (CoordinateBytes.IsDefault || CoordinateBytes.Length != 16)
        {
            throw new InvalidDataException("Native world allocation coordinate is not a complete sixteen-byte record.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(CoordinateBytes.AsSpan()[offset..]);
    }

    /// <summary>Original546F90; unclassified shapes remain -1, including the reached decorative shape95.</summary>
    public static int ClassifyShape(byte shape)
    {
        return shape switch
        {
            0 => 0,
            1 => 50,
            2 or 3 => 49,
            4 => 1,
            5 => 2,
            6 => 6,
            64 or 65 => 48,
            70 => 131,
            73 => 33,
            74 or 75 => 32,
            77 => 65,
            78 => 66,
            81 => 34,
            82 => 35,
            83 => 36,
            84 => 37,
            85 => 38,
            86 => 39,
            87 => 40,
            _ => -1
        };
    }
}

/// <summary>The selected simple-range WMSET allocation, with all records retained in native allocation order.</summary>
public sealed record NativeTrainWorldAllocation([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string CoordinateHash, [property: JsonRequired] string ClauseHash, [property: JsonRequired] int ClauseIndex, [property: JsonRequired] ushort MinimumStory, [property: JsonRequired] ushort MaximumStory, [property: JsonRequired] ushort Story, [property: JsonRequired] byte EventFlags103, [property: JsonRequired] ImmutableArray<ushort> ClauseWords, [property: JsonRequired] ImmutableArray<NativeTrainWorldObject> Objects, [property: JsonRequired] int ControlledObjectIndex)
{
    public const string AllocationRule = "ff8-en-wmset9-545f10-544860-full-coordinate-allocation-v1";

    public void Validate()
    {
        if (Version != 1 || Rule != AllocationRule || !IsHash(ProgramHash) || !IsHash(CoordinateHash) || !IsHash(ClauseHash) || ClauseIndex < 0 || MinimumStory >= MaximumStory || Story < MinimumStory || Story >= MaximumStory || Objects.IsDefaultOrEmpty || Objects.Length > 64 || Objects.Any(item => item is null) || ClauseWords.IsDefault || ClauseWords.Length != 10 + Objects.Length * 2 || ControlledObjectIndex < 0 || ControlledObjectIndex >= Objects.Length)
        {
            throw new InvalidDataException("Native world allocation lost its selected original program, range or record order.");
        }
        ushort[] header = [0xFF01, 0, 0xFF02, MinimumStory, 0xFF03, MaximumStory, 0xFF04, 0];
        if (!ClauseWords.AsSpan()[..8].SequenceEqual(header) || ClauseWords[^2] != 0xFF05 || ClauseWords[^1] != 0)
        {
            throw new InvalidDataException("Native world allocation does not retain its proved simple-range command sequence.");
        }
        int firstControlled = -1;
        for (int index = 0; index < Objects.Length; index++)
        {
            NativeTrainWorldObject item = Objects[index];
            item.Validate();
            if (ClauseWords[8 + index * 2] != 0xFF13 || ClauseWords[9 + index * 2] != item.RawOperand)
            {
                throw new InvalidDataException("Native world allocation record differs from its original instruction operand.");
            }
            if (firstControlled < 0 && (item.EntityType is >= 0 and <= 9 or 128))
            {
                firstControlled = index;
            }
        }
        if (ControlledObjectIndex != firstControlled)
        {
            throw new InvalidDataException("Native world controlled record is not the first original546F90 foot-class allocation.");
        }
    }

    private static bool IsHash(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }
}
