using System.Numerics;
using System.Runtime.CompilerServices;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>Presentation identity comes from an allocated task and its authored primitive, never its output packet address.</summary>
public readonly record struct NativeBattlePilotVisualIdentity(ushort Effect, uint Bank, int Slot, long Generation, int Primitive);

/// <summary>The authored rectangle stays discrete while its particle transform moves continuously.</summary>
public sealed record NativeBattlePilotSpriteGeometry(Vector3 LocalA, Vector3 LocalB, Vector3 LocalC, Vector3 LocalD, Vector3 Origin, Vector3 AxisX, Vector3 AxisY, Vector3 AxisZ)
{
    public Vector3 LocalCorner(int corner) => corner switch
    {
        0 => LocalA,
        1 => LocalB,
        2 => LocalC,
        3 => LocalD,
        _ => throw new ArgumentOutOfRangeException(nameof(corner))
    };

    public static NativeBattlePilotSpriteGeometry Capture(NativeBattleActionMatrix main, ReadOnlySpan<byte> quad, int width, int height, NativeBattleCameraProjection projection)
    {
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
        float xx = 1;
        float xy = 0;
        float yx = 0;
        float yy = 1;
        if ((flags & 0xC00) != 0)
        {
            short angle = BinaryPrimitives.ReadInt16LittleEndian(quad[12..]);
            int cosine = NativeBattleCameraMath.Cosine(angle);
            int sine = NativeBattleCameraMath.Sine(angle);
            short scaleX = BinaryPrimitives.ReadInt16LittleEndian(quad[16..]);
            short scaleY = BinaryPrimitives.ReadInt16LittleEndian(quad[18..]);
            xx = unchecked((short)(scaleX * cosine >> 12)) / 4096f;
            xy = unchecked((short)(-scaleY * sine >> 12)) / 4096f;
            yx = unchecked((short)(scaleX * sine >> 12)) / 4096f;
            yy = unchecked((short)(scaleY * cosine >> 12)) / 4096f;
        }
        short dx = unchecked((short)(8 * quad[0] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad[8..])));
        short dy = unchecked((short)(8 * quad[2] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad[10..])));
        Vector3 Local(int x, int y) => new(dx + xx * x + xy * y, dy + yx * x + yy * y, 0);
        Vector3 origin = NativeBattlePilotVisualGeometry.CameraToWorld(new(main.X, main.Y, main.Z), projection);
        Vector3 Axis(int column) => NativeBattlePilotVisualGeometry.CameraToWorldDirection(new(main.Rotation[column] / 4096f, main.Rotation[3 + column] / 4096f, main.Rotation[6 + column] / 4096f), projection);
        return new(Local(-width, -height), Local(width, -height), Local(-width, height), Local(width, height), origin, Axis(0), Axis(1), Axis(2));
    }
}

/// <summary>Four bounded world-space corners retained independently of mutable native task storage.</summary>
public sealed record NativeBattlePilotVisualGeometry(NativeBattlePilotVisualIdentity Identity, Vector3 A, Vector3 B, Vector3 C, Vector3 D, byte CornerCount, bool Continuous = true)
{
    public NativeBattlePilotSpriteGeometry? Sprite { get; init; }
    public Vector3 Corner(int index) => index switch
    {
        0 => A,
        1 => B,
        2 => C,
        3 when CornerCount == 4 => D,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static NativeBattlePilotVisualGeometry Sample(NativeBattlePilotVisualGeometry earlier, NativeBattlePilotVisualGeometry later, float alpha)
    {
        if (alpha <= 0)
        {
            return earlier;
        }
        if (alpha >= 1)
        {
            return later;
        }
        if (earlier.Identity != later.Identity || earlier.CornerCount != later.CornerCount || !earlier.Continuous || !later.Continuous)
        {
            return earlier;
        }
        alpha = Math.Clamp(alpha, 0, 1);
        return earlier with { A = SampleCorner(earlier, later, 0, alpha), B = SampleCorner(earlier, later, 1, alpha), C = SampleCorner(earlier, later, 2, alpha), D = earlier.CornerCount == 4 ? SampleCorner(earlier, later, 3, alpha) : default };
    }

    public static Vector3 SampleCorner(NativeBattlePilotVisualGeometry earlier, NativeBattlePilotVisualGeometry later, int corner, float alpha)
    {
        if (alpha >= 1)
        {
            return later.Corner(corner);
        }
        if (alpha <= 0 || earlier.Identity != later.Identity || earlier.CornerCount != later.CornerCount || !earlier.Continuous || !later.Continuous)
        {
            return earlier.Corner(corner);
        }
        if (earlier.Sprite is { } a && later.Sprite is { } b)
        {
            Vector3 local = a.LocalCorner(corner);
            return Vector3.Lerp(a.Origin, b.Origin, alpha) + local.X * Vector3.Lerp(a.AxisX, b.AxisX, alpha) + local.Y * Vector3.Lerp(a.AxisY, b.AxisY, alpha) + local.Z * Vector3.Lerp(a.AxisZ, b.AxisZ, alpha);
        }
        return Vector3.Lerp(earlier.Corner(corner), later.Corner(corner), alpha);
    }

    private sealed record InverseView(Matrix4x4 Matrix);
    private static readonly ConditionalWeakTable<NativeBattleCameraProjection, InverseView> inverseViews = new();

    public static Vector3 CameraToWorld(Vector3 point, NativeBattleCameraProjection projection)
    {
        return Vector3.Transform(point, InverseProjection(projection).Matrix);
    }

    public static Vector3 CameraToWorldDirection(Vector3 direction, NativeBattleCameraProjection projection)
    {
        return Vector3.TransformNormal(direction, InverseProjection(projection).Matrix);
    }

    private static InverseView InverseProjection(NativeBattleCameraProjection projection)
    {
        return inverseViews.GetValue(projection, value =>
        {
            NativeBattleActionMatrix view = value.View ?? throw new InvalidOperationException("Pilot geometry requires the emitted native view.");
            if (!Matrix4x4.Invert(ViewMatrix(view), out Matrix4x4 matrix))
            {
                throw new InvalidOperationException("Pilot geometry requires an invertible native view.");
            }
            return new(matrix);
        });
    }

    public static Vector3 Transform(NativeBattleActionMatrix matrix, NativeBattleVector point)
    {
        return Vector3.Transform(new(point.X, point.Y, point.Z), ViewMatrix(matrix));
    }

    /// <summary>Returns eighth-pixel XY and unclamped camera depth; material and native admission remain discrete.</summary>
    public static Vector3 Project(Vector3 world, NativeBattleCameraProjection projection)
    {
        Vector3 camera = Vector3.Transform(world, ViewMatrix(projection.View ?? throw new InvalidOperationException("Pilot projection requires a native view.")));
        float h = projection.FocalLength ?? throw new InvalidOperationException("Pilot projection requires native H.");
        float factor = h / Math.Max(camera.Z, h / 2);
        return new(camera.X * factor * 8 + (projection.OffsetX ?? 160) * 8, camera.Y * factor * 8 + (projection.OffsetY ?? 108) * 8, camera.Z);
    }

    public static NativeBattleCameraPose SampleCamera(NativeBattleCameraPose earlier, NativeBattleCameraPose later, float alpha)
    {
        if (alpha <= 0)
        {
            return earlier;
        }
        if (alpha >= 1)
        {
            return later;
        }
        short Lerp(short a, short b) => checked((short)MathF.Round(a + (b - a) * alpha));
        NativeBattleCameraPoint Point(NativeBattleCameraPoint a, NativeBattleCameraPoint b) => new(Lerp(a.X, b.X), Lerp(a.Y, b.Y), Lerp(a.Z, b.Z));
        int rollDelta = ((later.Roll - earlier.Roll + 2048) & 4095) - 2048;
        return new(Point(earlier.Eye, later.Eye), Point(earlier.Target, later.Target), earlier.FocalLength is short a && later.FocalLength is short b ? Lerp(a, b) : earlier.FocalLength, unchecked((short)(earlier.Roll + MathF.Round(rollDelta * alpha))));
    }

    public static NativeBattleCameraProjection SampleProjection(NativeBattleCameraProjection earlier, NativeBattleCameraProjection later, NativeBattleCameraPose earlierPose, NativeBattleCameraPose laterPose, float alpha)
    {
        if (alpha <= 0)
        {
            return earlier;
        }
        if (alpha >= 1)
        {
            return later;
        }
        short Lerp(short a, short b) => checked((short)MathF.Round(a + (b - a) * alpha));
        NativeBattleCameraPose pose = SampleCamera(earlierPose, laterPose, alpha);
        NativeBattleActionMatrix view = NativeBattlePoseMatrices.CameraView(pose);
        NativeBattleActionMatrix baseA = NativeBattlePoseMatrices.CameraView(earlierPose);
        NativeBattleActionMatrix baseB = NativeBattlePoseMatrices.CameraView(laterPose);
        NativeBattleActionMatrix actualA = earlier.View!;
        NativeBattleActionMatrix actualB = later.View!;
        int Offset(int a, int b) => (int)MathF.Round(a + (b - a) * alpha);
        view = view with { X = view.X + Offset(actualA.X - baseA.X, actualB.X - baseB.X), Y = view.Y + Offset(actualA.Y - baseA.Y, actualB.Y - baseB.Y), Z = view.Z + Offset(actualA.Z - baseA.Z, actualB.Z - baseB.Z) };
        return new(view, (ushort)Offset(earlier.FocalLength!.Value, later.FocalLength!.Value), Lerp(earlier.OffsetX!.Value, later.OffsetX!.Value), Lerp(earlier.OffsetY!.Value, later.OffsetY!.Value));
    }

    private static Matrix4x4 ViewMatrix(NativeBattleActionMatrix matrix)
    {
        var r = matrix.Rotation;
        return new(r[0] / 4096f, r[3] / 4096f, r[6] / 4096f, 0, r[1] / 4096f, r[4] / 4096f, r[7] / 4096f, 0, r[2] / 4096f, r[5] / 4096f, r[8] / 4096f, 0, matrix.X, matrix.Y, matrix.Z, 1);
    }
}

internal static class NativeBattlePilotVisualLifetimes
{
    private static readonly ConditionalWeakTable<NativeBattleTaskBankOwner, long[]> generations = new();
    private static readonly ConditionalWeakTable<object, Epoch> epochs = new();
    private sealed record Epoch(long Value);

    internal static long EpochFor(object owner) => epochs.GetValue(owner, _ => new(Interlocked.Increment(ref nextGeneration))).Value;
    private static long nextGeneration;

    internal static void Allocated(NativeBattleEffectTaskPool pool, int slot)
    {
        if (pool.DescriptorAddress is not (0x24CA838 or 0x24BD788 or 0x2557088))
        {
            return;
        }
        long[] slots = generations.GetValue(pool.RequireOwner(), current => new long[current.Tasks.Length]);
        slots[slot] = Interlocked.Increment(ref nextGeneration);
    }

    internal static long Generation(NativeBattleModulePoolTask task, int slot)
    {
        NativeBattleTaskBankOwner owner = task.RequireOwner();
        long[] slots = generations.GetValue(owner, current => new long[current.Tasks.Length]);
        if (slots[slot] == 0)
        {
            slots[slot] = Interlocked.Increment(ref nextGeneration);
        }
        return slots[slot];
    }
}
