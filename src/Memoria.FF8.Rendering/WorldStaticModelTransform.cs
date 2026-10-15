using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Native5472C0 allocation angles and547B20 Euler order within the existing floating presentation boundary.</summary>
public static class WorldStaticModelTransform
{
    private const float RadiansPerUnit = 0.0015339808305725455f;

    public static NativeWorldPathAngles AllocationAngles(NativeTrainWorldObject item)
    {
        item.Validate();
        int entity = item.EntityType;
        int yawCorrection = entity is >= 0 and < 10 or 128 || item.ShapeId is 2 or 3 ? -2048 : entity is >= 32 and <= 40 or 132 ? -1024 : entity == 50 ? 2048 : entity == 48 || item.ShapeId == 79 ? 1024 : item.ShapeId == 80 ? 2048 : 0;
        // Native544860's WORD zero followed by the coordinate's packed DWORD
        // becomes native XYZ at20/22/24 through545280. Managed path Pitch
        // denotes native Z slope, while managed Roll denotes native X.
        short nativeX = 0;
        short nativeY = unchecked((short)(item.PackedOrientation + yawCorrection));
        short nativeZ = item.Roll;
        return new NativeWorldPathAngles(nativeZ, nativeY, nativeX);
    }

    public static Matrix4x4 CreateRotation(byte shape, NativeWorldPathAngles angles)
    {
        Matrix4x4 nativeX = Matrix4x4.CreateRotationX(angles.Roll * RadiansPerUnit);
        Matrix4x4 nativeY = Matrix4x4.CreateRotationY(angles.Yaw * RadiansPerUnit);
        Matrix4x4 nativeZ = Matrix4x4.CreateRotationZ(angles.Pitch * RadiansPerUnit);
        // Native56CD50 builds column-vector Rx*Ry*Rz. Model index16
        // (shape80) instead uses56CE30's Ry*Rx*Rz. System.Numerics
        // row-vector composition reverses these multiplication orders.
        return shape == 80 ? nativeZ * nativeX * nativeY : nativeZ * nativeY * nativeX;
    }
}
