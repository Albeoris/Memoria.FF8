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
        // Native544860's WORD zero followed by the coordinate's packed DWORD
        // becomes native XYZ at20/22/24 through545280. Managed path Pitch
        // denotes native Z slope, while managed Roll denotes native X.
        return AllocationAngles(item.ShapeId, 0, unchecked((short)item.PackedOrientation), item.Roll);
    }

    /// <summary>5472C0's original byte17 animation mode, using the last executed physical draw counter.</summary>
    public static NativeWorldPathAngles AnimatedAllocationAngles(NativeTrainWorldObject item, uint? mainLoopCounter)
    {
        item.Validate();
        if (mainLoopCounter is null || item.Flags == 0)
        {
            return AllocationAngles(item);
        }
        if (item.Flags != 4)
        {
            throw new NotSupportedException("Native allocation animation requires its executed original mode state.");
        }
        return AllocationAngles(item.ShapeId, 786, unchecked((short)item.PackedOrientation), unchecked((short)(8 * mainLoopCounter.Value)));
    }

    public static NativeWorldPathAngles AnimatedAllocationAngles(NativeGardenWorldObject item, uint? mainLoopCounter)
    {
        if (mainLoopCounter is null || item.Flags == 0)
        {
            return AllocationAngles(item.ShapeId, item.RotationX, item.RotationY, item.RotationZ);
        }
        return item.Flags switch
        {
            1 => AllocationAngles(item.ShapeId, item.RotationX, item.RotationY, unchecked((short)(16 * mainLoopCounter.Value))),
            2 => AllocationAngles(item.ShapeId, item.RotationX, unchecked((short)(8 * mainLoopCounter.Value)), item.RotationZ),
            4 => AllocationAngles(item.ShapeId, 786, item.RotationY, unchecked((short)(8 * mainLoopCounter.Value))),
            _ => throw new NotSupportedException("Native allocation animation requires its executed original mode state.")
        };
    }

    public static NativeWorldPathAngles AnimatedAllocationAngles(NativeRagnarokWorldObject item, uint? mainLoopCounter)
    {
        return AnimatedAllocationAngles(new NativeGardenWorldObject(item.ShapeId, item.Flags, item.Position, item.Pitch, item.Yaw, item.Roll), mainLoopCounter);
    }

    public static NativeWorldPathAngles AllocationAngles(byte shape, short nativeX, short nativeY, short nativeZ)
    {
        int entity = NativeTrainWorldObject.ClassifyShape(shape);
        int yawCorrection = entity is >= 0 and < 10 or 128 || shape is 2 or 3 ? -2048 : entity is >= 32 and <= 40 or 132 ? -1024 : entity == 50 ? 2048 : entity == 48 || shape == 79 ? 1024 : shape == 80 ? 2048 : 0;
        return new NativeWorldPathAngles(nativeZ, unchecked((short)(nativeY + yawCorrection)), nativeX);
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
