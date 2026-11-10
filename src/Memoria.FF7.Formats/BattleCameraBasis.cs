using System.Numerics;

namespace Memoria.FF7.Formats;

public readonly record struct BattleCameraBasis(Vector3 Position, Vector3 Right, Vector3 Up, Vector3 Forward)
{
    public static BattleCameraBasis Create(SceneCameraPlacement camera)
    {
        Vector3 forward = camera.Focus - camera.Position;
        Vector3 right = Vector3.Cross(forward, -Vector3.UnitY);
        if (right.LengthSquared() == 0)
        {
            throw new InvalidDataException("FFVII battle camera requires a nonvertical view direction.");
        }
        forward = Vector3.Normalize(forward);
        right = Vector3.Normalize(right);
        return new(camera.Position, right, Vector3.Cross(right, forward), forward);
    }

    public Vector3 Transform(Vector3 world)
    {
        Vector3 relative = world - Position;
        return new(Vector3.Dot(relative, Right), Vector3.Dot(relative, Up), Vector3.Dot(relative, Forward));
    }
}
