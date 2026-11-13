using System.Numerics;

namespace Memoria.FF8.Rendering;

internal struct Ff7BattleScreenBounds
{
    private bool hasPoint;
    private Vector2 minimum;
    private Vector2 maximum;

    public readonly Vector4? Value => hasPoint && maximum.X > minimum.X && maximum.Y > minimum.Y
        ? new(minimum.X, minimum.Y, maximum.X, maximum.Y)
        : null;

    public void Add(Vector3 point, int width, int height, float focal)
    {
        if (point.Z < 1)
        {
            return;
        }
        Vector2 position = CameraSpaceTriangleSubmission.Project(new(point, Vector2.Zero), width, height, focal).Position;
        minimum = hasPoint ? Vector2.Min(minimum, position) : position;
        maximum = hasPoint ? Vector2.Max(maximum, position) : position;
        hasPoint = true;
    }
}
