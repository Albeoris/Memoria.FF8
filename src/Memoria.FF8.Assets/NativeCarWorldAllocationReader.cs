using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>544860's selected saved-car record, optional94 and264-controlled79/80 pairs, after section9.</summary>
public static class NativeCarWorldAllocationReader
{
    public static ImmutableArray<NativeTrainWorldObject> ReadAppended(ReadOnlySpan<byte> coordinates, NativeSavedWorldVehicleSlot selectedCar, byte replacementFlags264, byte guardianForce13Flags, NativeWorldPathPosition cactuarLoadPosition)
    {
        if (!NativeCarWorldRuntimeState.IsSupportedEntity(NativeTrainWorldObject.ClassifyShape(selectedCar.ShapeId)))
        {
            throw new NotSupportedException("Native car allocation requires the actual selected original saved-car shape.");
        }
        var objects = ImmutableArray.CreateBuilder<NativeTrainWorldObject>();
        NativeSavedWorldPose pose = selectedCar.Pose;
        objects.Add(FromPose(selectedCar.ShapeId, new(pose.X, pose.Height, -pose.RawZ), pose.Yaw));
        if ((guardianForce13Flags & 1) == 0)
        {
            objects.Add(FromPose(94, cactuarLoadPosition, 0));
        }
        for (int group = 0; group < 3; group++)
        {
            if ((replacementFlags264 & (1 << group)) == 0)
            {
                continue;
            }
            byte coordinate = checked((byte)(31 + group));
            int offset = coordinate * 16;
            if (offset > coordinates.Length - 16)
            {
                throw new InvalidDataException("Native264-controlled car objects require their complete original coordinate row.");
            }
            ImmutableArray<byte> raw = ImmutableArray.CreateRange(coordinates.Slice(offset, 16).ToArray());
            foreach (byte shape in new byte[] { 79, 80 })
            {
                NativeWorldPathPosition position = new(BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan()[4..]), BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan()[8..]));
                int orientation = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan()[12..]);
                var item = new NativeTrainWorldObject(shape, coordinate, new(position.X, -position.Z, checked((short)position.Y), unchecked((ushort)orientation)), checked((ushort)(shape | coordinate << 8)), raw) { Flags = shape == 80 ? (byte)4 : (byte)0 };
                item.Validate();
                objects.Add(item);
            }
        }
        return objects.ToImmutable();
    }

    public static NativeTrainWorldObject FromPose(byte shape, NativeWorldPathPosition position, int packedOrientation)
    {
        byte[] raw = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(raw, position.X);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4), position.Y);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(8), position.Z);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(12), packedOrientation);
        var item = new NativeTrainWorldObject(shape, 0, new(position.X, -position.Z, checked((short)position.Y), unchecked((ushort)packedOrientation)), shape, ImmutableArray.CreateRange(raw));
        item.Validate();
        return item;
    }
}

public sealed partial class WorldAssets
{
    public ImmutableArray<NativeTrainWorldObject> ReadCarAppendedAllocation(NativeSavedWorldVehicleSlot selectedCar, byte replacementFlags264, byte guardianForce13Flags)
    {
        return NativeCarWorldAllocationReader.ReadAppended(wmset.AsSpan(sections[10], sections[11] - sections[10]), selectedCar, replacementFlags264, guardianForce13Flags, ReadFootType2CactuarLoadPosition());
    }
}
