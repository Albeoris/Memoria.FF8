using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF7.Runtime;

public static class Ff7SnowboardCamera
{
    private const float AngleScale = 0.0015339808305725455f;
    private const float FullTurn = 6.2831854820251465f;

    public static Vector3 ReadNormalTarget(ReadOnlySpan<byte> tile, int segmentBaseY, Vector3 nativeBasePosition, Vector3 riderPositionQ12, short localZ, short turnAngle)
    {
        if (tile.Length < Ff7SnowboardTerrain.TileSize)
        {
            throw new InvalidDataException("Snowboard camera requires a complete native tile.");
        }
        int baseY = (int)nativeBasePosition.Y;
        int first = unchecked(((segmentBaseY + unchecked(segmentBaseY + TileWord(tile, 0x3A))) >> 1) - baseY);
        int second = unchecked(((unchecked(segmentBaseY + TileWord(tile, 0x15A)) + unchecked(segmentBaseY + TileWord(tile, 0x18A))) >> 1) - baseY);
        bool curved = TileWord(tile, 4) != 0;
        int distance = curved ? Math.Abs((int)turnAngle) : localZ;
        int divisor = TileWord(tile, curved ? 6 : 0x36);
        int product = unchecked((second - first) * distance);
        int height = unchecked(first + product / divisor);
        int playerLimit = unchecked((int)(riderPositionQ12.Y * (1.0 / 4096)) - baseY + 64);
        return new Vector3((float)(riderPositionQ12.X * (1.0 / 4096) - nativeBasePosition.X), Math.Min(height, playerLimit), (float)(riderPositionQ12.Z * (1.0 / 4096) - nativeBasePosition.Z));


    }

    private static short TileWord(ReadOnlySpan<byte> tile, int offset) => BinaryPrimitives.ReadInt16LittleEndian(tile.Slice(offset, 2));

    public static Vector3 AdvanceNormalAngles(Vector3 current, short courseDirection, short speed, Vector3 velocity, byte actionClass, byte courseAttributes, short airFrames, short tilePitch)
    {
        if (actionClass is 1 or 2)
        {
            int direction = actionClass == 1 && speed > 128 ? Ff7SnowboardCollisionResponse.Direction(unchecked(-(int)velocity.X), unchecked(-(int)velocity.Z)) & 0xFFF : courseDirection;
            int difference = (direction - courseDirection) & 0xFFF;
            if (difference > 0x400 && difference < 0xC00)
            {
                direction = (direction + 0x800) & 0xFFF;
            }
            difference = (direction - courseDirection) & 0xFFF;
            if (difference > 0x200 && difference < 0x800)
            {
                direction = courseDirection + 0x200;
            }
            else if (difference > 0x800 && difference < 0xE00)
            {
                direction = courseDirection - 0x200;
            }
            float desired = ((0x800 - direction) & 0xFFF) * AngleScale;
            current.Y = ApproachYaw(desired, current.Y, Math.Max(speed >> 4, 4) * AngleScale);
        }
        int desiredPitch = ((courseAttributes & 8) != 0 ? 32 : airFrames > 8 ? 128 : tilePitch * 3) + 64;
        int differencePitch = unchecked(desiredPitch - (int)((double)current.X * 651.8986206054688f));
        current.X = (float)(current.X + Math.Clamp(differencePitch, -4, 4) * (double)AngleScale);
        current.Z = 0;
        return current;
    }

    public static float ApproachYaw(float desired, float current, float step)
    {
        if (!float.IsFinite(step) || step < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }
        float difference = WrapYaw(desired) - WrapYaw(current);
        if (difference < 0)
        {
            difference += FullTurn;
        }
        if (difference < 3.1415927410125732f)
        {
            difference = Math.Min(difference, step);
        }
        else
        {
            difference = Math.Max(-(FullTurn - difference), -step);
        }
        return WrapYaw(current + difference);
    }

    public static Matrix4x4 CreateNormalView(Vector3 target, Vector3 angles)
    {
        Matrix4x4 view = CreateRotation(angles);
        Vector3 rotated = new(
            (float)((double)target.X * view.M11 + (double)target.Y * view.M21 + (double)target.Z * view.M31),
            (float)((double)target.X * view.M12 + (double)target.Y * view.M22 + (double)target.Z * view.M32),
            (float)((double)target.X * view.M13 + (double)target.Y * view.M23 + (double)target.Z * view.M33));
        view.Translation = new Vector3(0, 128, 320) - rotated;
        return view;
    }

    public static Matrix4x4 AdjustNormalScreenY(Matrix4x4 view, float projectedViewportRelativeY, int viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        }
        float threshold = viewportHeight * 0.8500000238418579f;
        float correction = projectedViewportRelativeY < 0 ? -projectedViewportRelativeY : projectedViewportRelativeY > threshold ? -(projectedViewportRelativeY - threshold) : 0;
        view.M42 += correction;
        return view;
    }

    public static Matrix4x4 CreateView(Vector3 riderPositionQ12, Vector3 nativeBasePosition, Vector3 cameraOffset, short pitch, short yaw, short roll, short courseAngle)
    {
        Vector3 target = riderPositionQ12 * (1f / 4096) - nativeBasePosition;
        Vector3 angles = new(pitch * AngleScale, WrapYaw((yaw - courseAngle) * AngleScale), roll * AngleScale);
        Matrix4x4 view = CreateRotation(angles);
        Vector3 rotated = new(
            (float)((double)target.X * view.M11 + (double)target.Y * view.M21 + (double)target.Z * view.M31),
            (float)((double)target.X * view.M12 + (double)target.Y * view.M22 + (double)target.Z * view.M32),
            (float)((double)target.X * view.M13 + (double)target.Y * view.M23 + (double)target.Z * view.M33));
        view.Translation = cameraOffset - rotated;
        return view;
    }

    public static Matrix4x4 CreateRotation(Vector3 angles)
    {
        float sx = (float)Math.Sin(angles.X);
        float cx = (float)Math.Cos(angles.X);
        float sy = (float)Math.Sin(angles.Y);
        float cy = (float)Math.Cos(angles.Y);
        float sz = (float)Math.Sin(angles.Z);
        float cz = (float)Math.Cos(angles.Z);
        Matrix4x4 native = new(
            (float)((double)cy * cz), (float)(-(double)cy * sz), sy, 0,
            (float)((double)cx * sz + (double)cz * sx * sy), (float)((double)cx * cz - (double)sx * sy * sz), (float)(-(double)cy * sx), 0,
            (float)((double)sx * sz - (double)cx * cz * sy), (float)((double)cz * sx + (double)cx * sy * sz), (float)((double)cx * cy), 0,
            0, 0, 0, 1);
        return Matrix4x4.Transpose(native);
    }

    public static Matrix4x4 CreateProjection(int width, int height, bool fixedOriginalAspect = false)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Snowboard projection requires a positive native viewport.");
        }
        const float near = 64;
        const float far = 16384;
        float radians = (float)(56.150001525878906 * (Math.PI / 180));
        float extent = (float)(near * Math.Tan(radians / 2.0));
        float aspect = fixedOriginalAspect ? 0.75f : (float)height / width;
        return new Matrix4x4(
            aspect, 0, 0, 0,
            0, -1, 0, 0,
            0, 0, (float)((double)extent * far / ((far - near) * near)), extent / near,
            0, 0, (float)(-(double)extent * far / (far - near)), 0);
    }

    public static float WrapYaw(float radians)
    {
        if (!float.IsFinite(radians))
        {
            throw new InvalidDataException("Snowboard camera yaw must be finite.");
        }
        while (radians > FullTurn)
        {
            radians -= FullTurn;
        }
        while (radians < 0)
        {
            radians += FullTurn;
        }
        return radians;
    }
}
