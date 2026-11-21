using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardCameraTests
{
    internal static void Run()
    {
        CheckNormalCamera();
        Vector3 position = new(4096, -8192, 12288);
        Vector3 cameraOffset = new(0, 0, 128);
        Matrix4x4 view = Ff7SnowboardCamera.CreateView(position, Vector3.Zero, cameraOffset, 0, 256, 0, 256);
        Near(view.Translation, new(-1, 2, 125));
        Near(Vector3.Transform(position / 4096, view), cameraOffset);

        Vector3 nativeBase = new(3, 5, 7);
        Matrix4x4 rotated = Ff7SnowboardCamera.CreateView(position, nativeBase, cameraOffset, 64, 2048, -96, 256);
        Near(Vector3.Transform(position / 4096 - nativeBase, rotated), cameraOffset);
        Matrix4x4 yaw = Ff7SnowboardCamera.CreateRotation(new(0, MathF.PI / 2, 0));
        Near(Vector3.TransformNormal(Vector3.UnitX, yaw), -Vector3.UnitZ);
        Near(Vector3.TransformNormal(Vector3.UnitZ, yaw), Vector3.UnitX);
        float fullTurn = 6.2831854820251465f;
        if (Ff7SnowboardCamera.WrapYaw(fullTurn) != fullTurn || Ff7SnowboardCamera.WrapYaw(-fullTurn) != 0)
        {
            throw new InvalidOperationException("Snowboard yaw must preserve the native inclusive upper endpoint.");
        }

        Matrix4x4 projection = Ff7SnowboardCamera.CreateProjection(640, 480);
        Vector4 near = Vector4.Transform(new Vector4(0, 0, 64, 1), projection);
        Vector4 far = Vector4.Transform(new Vector4(0, 0, 16384, 1), projection);
        if (MathF.Abs(near.Z / near.W) > 0.00001f || MathF.Abs(far.Z / far.W - 1) > 0.00001f || projection.M22 != -1 || projection.M11 != 0.75f || near.W <= 0)
        {
            throw new InvalidOperationException("Snowboard projection must retain positive-Z depth, native Y direction and clipping distances.");
        }
        if (Ff7SnowboardCamera.CreateProjection(1600, 900).M11 != 0.5625f || Ff7SnowboardCamera.CreateProjection(1600, 900, true).M11 != 0.75f)
        {
            throw new InvalidOperationException("Snowboard projection must distinguish viewport aspect from the original fixed-aspect flag.");
        }
    }

    private static void CheckNormalCamera()
    {
        byte[] tile = new byte[Ff7SnowboardTerrain.TileSize];
        Write(0x3A, -21);
        Write(0x15A, 40);
        Write(0x18A, 60);
        Write(0x36, 100);
        Write(6, 200);
        byte[] original = tile.ToArray();
        Vector3 position = new(40960, 819200, 122880);
        Vector3 nativeBase = new(2, 20, 3);
        Vector3 straight = Ff7SnowboardCamera.ReadNormalTarget(tile, 100, nativeBase, position, 50, -100);
        Near(straight, new(8, 99, 27));
        Write(4, 1);
        Near(Ff7SnowboardCamera.ReadNormalTarget(tile, 100, nativeBase, position, 0, -100), straight);
        Write(4, -1);
        Near(Ff7SnowboardCamera.ReadNormalTarget(tile, 100, nativeBase, position, 99, 100), straight);
        Vector3 capped = Ff7SnowboardCamera.ReadNormalTarget(tile, 100, nativeBase, new(0, -4096, 0), 0, 100);
        if (capped.Y != 43)
        {
            throw new InvalidOperationException("Normal camera target must use native truncated player height plus 64 as its upper bound.");
        }
        Write(4, 0);
        if (!tile.SequenceEqual(original))
        {
            throw new InvalidOperationException("Normal camera target sampling changed its tile.");
        }

        const float unit = 0.0015339808305725455f;
        Vector3 angles = Ff7SnowboardCamera.AdvanceNormalAngles(Vector3.Zero, 0, 0, Vector3.Zero, 2, 0, 0, 0);
        Near(angles, new(4 * unit, 6.2831854820251465f - 4 * unit, 0));
        Vector3 unrelatedClass = Ff7SnowboardCamera.AdvanceNormalAngles(new(100 * unit, 1, 2), 0, 4096, Vector3.One, 3, 8, 20, 99);
        Near(unrelatedClass, new(97 * unit, 1, 0));
        Vector3 airborne = Ff7SnowboardCamera.AdvanceNormalAngles(new(100 * unit, 1, 0), 0, 0, Vector3.Zero, 3, 0, 9, 0);
        Near(airborne, new(104 * unit, 1, 0));
        if (MathF.Abs(Ff7SnowboardCamera.ApproachYaw(0.1f, 6.2f, 0.01f) - 6.21f) > 0.000001f || MathF.Abs(Ff7SnowboardCamera.ApproachYaw(6.2f, 0.1f, 0.01f) - 0.09f) > 0.000001f)
        {
            throw new InvalidOperationException("Normal camera yaw must cross the native angular seam by the shortest signed step.");
        }
        Matrix4x4 view = Ff7SnowboardCamera.CreateNormalView(straight, angles);
        Near(Vector3.Transform(straight, view), new(0, 128, 320));
        Matrix4x4 above = Ff7SnowboardCamera.AdjustNormalScreenY(view, -12, 480);
        Matrix4x4 below = Ff7SnowboardCamera.AdjustNormalScreenY(view, 420, 480);
        Matrix4x4 inside = Ff7SnowboardCamera.AdjustNormalScreenY(view, 200, 480);
        if (above.M42 != view.M42 + 12 || MathF.Abs(below.M42 - (view.M42 - 12)) > 0.0001f || inside != view || above.M41 != view.M41 || below.M43 != view.M43)
        {
            throw new InvalidOperationException("Native normal-camera correction must change only view Y translation outside the viewport band.");
        }

        void Write(int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(tile.AsSpan(offset, 2), value);
        }
    }

    private static void Near(Vector3 actual, Vector3 expected)
    {
        if (Vector3.Distance(actual, expected) > 0.00005f)
        {
            throw new InvalidOperationException($"Native snowboard camera mismatch: {actual} != {expected}.");
        }
    }
}
