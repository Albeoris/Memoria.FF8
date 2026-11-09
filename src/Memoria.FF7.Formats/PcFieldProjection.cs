using System.Numerics;

namespace Memoria.FF7.Formats;

public static class PcFieldProjection
{
    public static Vector3 Project(Vector3 world, PcFieldCamera camera, Vector2 screenOrigin)
    {
        Vector3 view = new(Vector3.Dot(world, camera.AxisX) / 4096 + camera.Translation.X, Vector3.Dot(world, camera.AxisY) / 4096 + camera.Translation.Y, Vector3.Dot(world, camera.AxisZ) / 4096 + camera.Translation.Z);
        return new Vector3(screenOrigin.X + camera.FocalLength * view.X / view.Z, screenOrigin.Y + camera.FocalLength * view.Y / view.Z, view.Z);
    }
}
