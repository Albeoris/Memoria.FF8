using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7SnowboardCollisionSound(int Channel, int Sound, int Pan);

public sealed record Ff7SnowboardCollisionResult(Vector3 Velocity, short Action, int ReflectionAngle, Ff7SnowboardCollisionSound? Sound);

public sealed class Ff7SnowboardCollisionResponse
{
    private readonly Ff7SnowboardNativeData data;

    public Ff7SnowboardCollisionResponse(Ff7SnowboardNativeData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.CollisionActions.Count != 32 * 9 || data.TileGeometry.Count != 76 * Ff7SnowboardTerrain.TileSize || data.CollisionActions.Any(value => value >= 32 && value is not (0xFE or 0xFF)))
        {
            throw new InvalidDataException("Snowboard collision response requires complete native geometry and action tables.");
        }
        this.data = data;
    }

    public Ff7SnowboardCollisionResult Apply(Ff7SnowboardCourseNavigation navigation, Ff7SnowboardCourseCollision collision, Vector3 velocity, short action, short speed, short direction)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(collision);
        if ((action & 255) == 2 || speed < 128)
        {
            return new(new(0, velocity.Y, 0), action, 0, null);
        }
        if (collision.Kind is < 1 or > 4)
        {
            throw new NotSupportedException("The snowboard collision has no admitted native response heading.");
        }
        int heading = Direction(TruncateInt(velocity.X), TruncateInt(velocity.Z)) & 4095;
        int boundary;
        if (collision.Kind == 1)
        {
            Ff7SnowboardCourseData course = data.Courses[navigation.CurrentCourse];
            int tile = course.Tiles[navigation.CurrentSegment];
            int offset = tile * Ff7SnowboardTerrain.TileSize + 0x4E + navigation.CurrentTriangle / 6 * 8;
            short localHeading = unchecked((short)(data.TileGeometry[offset] | data.TileGeometry[offset + 1] << 8));
            boundary = localHeading + course.Angles[navigation.CurrentSegment] + 2048;
        }
        else
        {
            boundary = direction + 1024;
        }
        int reflection = unchecked((2048 - (heading - (boundary & 2047))) << 1) & 4095;
        float radians = reflection * 0.0015339808305725455f;
        float sine = (float)Math.Sin(radians);
        float cosine = (float)Math.Cos(radians);
        Vector3 reflected = new((float)((double)cosine * velocity.X + (double)sine * velocity.Z), velocity.Y, (float)(-(double)sine * velocity.X + (double)cosine * velocity.Z));
        int angle = reflection > 2048 ? 4096 - reflection : reflection;
        int index = (action & 255) * 9;
        if (index >= data.CollisionActions.Count)
        {
            throw new NotSupportedException("The snowboard actor action has no native collision response row.");
        }
        if (speed > 512)
        {
            index++;
        }
        if (speed > 1280)
        {
            index++;
        }
        if (angle > 512)
        {
            index += 3;
        }
        if (angle > 1024)
        {
            index += 3;
        }
        int response = data.CollisionActions[index];
        if (response == 0xFF)
        {
            return new(reflected, action, reflection, null);
        }
        if (response == 0xFE)
        {
            reflected.X = (float)(reflected.X - (double)reflected.X / 4.0);
            reflected.Z = (float)(reflected.Z - (double)reflected.Z / 4.0);
            return new(reflected, action, reflection, new(3, 309, 0));
        }
        if (response == 8 && navigation.CurrentTriangle % 12 > 6)
        {
            response++;
        }
        return new(reflected, (short)response, reflection, new(3, 239, 0));
    }

    internal static int Direction(int x, int z)
    {
        float radians = (float)Math.Atan2(x, z);
        return (int)(radians * 57.29577951307855 * 11.377778053283691);
    }

    private static int TruncateInt(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f ? int.MinValue : (int)value;
}
