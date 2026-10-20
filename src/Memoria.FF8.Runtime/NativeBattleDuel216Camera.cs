using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216Extents(NativeBattleVector Minimum, NativeBattleVector Maximum);
public sealed record NativeBattleDuel216CameraTransform(NativeBattleActionMatrix Matrix, NativeBattleCameraProjection Projection, short PreviousX, short PreviousY);

public static partial class NativeBattleDuel216Camera
{
    public static NativeBattleDuel216Extents Extents69A700(ImmutableArray<byte> geometry)
    {
        if (geometry.IsDefault || geometry.Length < 28)
        {
            throw new InvalidDataException("Native69A700 requires its actual geometry vertices.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(geometry.AsSpan().Slice(4));
        if (count < 2 || 12L + count * 8L > geometry.Length)
        {
            throw new InvalidDataException("Native69A700 requires its original multi-vertex geometry extent.");
        }
        short[] minimum = new short[3];
        short[] maximum = new short[3];
        for (int component = 0; component < 3; component++)
        {
            minimum[component] = maximum[component] = BinaryPrimitives.ReadInt16LittleEndian(geometry.AsSpan().Slice(12 + component * 2));
        }
        for (int vertex = 1; vertex < count; vertex++)
        {
            for (int component = 0; component < 3; component++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(geometry.AsSpan().Slice(12 + vertex * 8 + component * 2));
                minimum[component] = Math.Min(minimum[component], value);
                maximum[component] = Math.Max(maximum[component], value);
            }
        }
        return new(new(minimum[0], minimum[1], minimum[2]), new(maximum[0], maximum[1], maximum[2]));
    }

    public static NativeBattleDuel216Extents Scale695AB0(NativeBattleDuel216Extents bounds, short scale)
    {
        NativeBattleVector Scale(NativeBattleVector point) => new(unchecked((short)(scale * point.X >> 12)), unchecked((short)(scale * point.Y >> 12)), unchecked((short)(scale * point.Z >> 12)));
        return new(Scale(bounds.Minimum), Scale(bounds.Maximum));
    }

    public static NativeBattleDuel216CameraTransform Transform69A790(NativeBattleModuleAnchor point, NativeBattleDuel216Extents bounds, NativeBattleCameraProjection projection, short focal)
    {
        int minimumX = bounds.Minimum.X;
        int maximumX = bounds.Maximum.X;
        int minimumZ = bounds.Minimum.Z;
        int maximumZ = bounds.Maximum.Z;
        int spanX = maximumX - minimumX;
        int spanZ = maximumZ - minimumZ;
        int divisorX = spanX / 128;
        int divisorZ = spanZ / 128;
        if (divisorX % 128 != 0)
        {
            divisorX++;
        }
        if (divisorZ % 128 != 0)
        {
            divisorZ++;
        }
        if (divisorX == 0 || divisorZ == 0 || (spanX << 12) / 128 == 0 || (spanZ << 12) / 128 == 0)
        {
            throw new InvalidDataException("Native69A790 camera geometry produces an actual zero divisor.");
        }
        NativeBattleActionMatrix rotation = new([4096, 0, 0, 0, 0, -4096, 0, 4096, 0], 0, 0, 0, 4096);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(rotation, 0x1000000 / ((spanX << 12) / 128), 0, 0x1000000 / ((spanZ << 12) / 128)) with
        {
            X = -((int)point.X / divisorX) + (-minimumX - maximumX) / 2 / divisorX,
            Y = (int)point.Z / divisorZ - (-minimumZ - maximumZ) / 2 / divisorZ,
            Z = focal
        };
        return new(matrix, projection with { View = matrix, OffsetX = 64, OffsetY = 64 }, projection.OffsetX ?? throw new NotSupportedException("Native69A790 requires its actual prior screen X offset."), projection.OffsetY ?? throw new NotSupportedException("Native69A790 requires its actual prior screen Y offset."));
    }
}

