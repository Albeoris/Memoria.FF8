using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>The expanded native camera view shared by the world-space corners of one sample.</summary>
public readonly struct NativeBattlePilotProjector
{
    private readonly Matrix4x4 view;
    private readonly ushort? focalLength;
    private readonly short offsetX;
    private readonly short offsetY;

    internal NativeBattlePilotProjector(in Matrix4x4 view, ushort? focalLength, short offsetX, short offsetY)
    {
        this.view = view;
        this.focalLength = focalLength;
        this.offsetX = offsetX;
        this.offsetY = offsetY;
    }

    public Vector3 Project(Vector3 world, ushort? focalOverride = null)
    {
        Vector3 camera = Vector3.Transform(world, view);
        float h = focalOverride ?? focalLength ?? throw new InvalidOperationException("Pilot projection requires native H.");
        float factor = h / Math.Max(camera.Z, h / 2);
        return new(camera.X * factor * 8 + offsetX * 8, camera.Y * factor * 8 + offsetY * 8, camera.Z);
    }
}
