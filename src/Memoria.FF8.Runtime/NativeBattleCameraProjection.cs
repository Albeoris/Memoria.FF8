using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCameraProjection(NativeBattleActionMatrix? View, ushort? FocalLength, short? OffsetX, short? OffsetY)
{
    public byte ReadPan(NativeBattleVector point)
    {
        NativeBattleActionMatrix view = View ?? throw new NotSupportedException("Native5011B0 requires its actual5033E0 view matrix.");
        view.Validate();
        ushort h = FocalLength ?? throw new NotSupportedException("Native5011B0 requires its actual GTE projection H.");
        short ox = OffsetX ?? throw new NotSupportedException("Native5011B0 requires its actual GTE screen offset X.");
        ImmutableArray<short> r = view.Rotation;
        //45D7F0 stores rotation rows as float Q12 coefficients;45FAA0 rounds
        //the transformed DWORDs with the executable's biased-double conversion.
        int Row(int row) => NativeBattlePoseMatrices.RoundNative((float)(r[row * 3 + 2] / 4096d) * (double)point.Z + (float)(r[row * 3 + 1] / 4096d) * (double)point.Y + (float)(r[row * 3] / 4096d) * (double)point.X);
        int x = unchecked(Row(0) + view.X);
        int z = unchecked(Row(2) + view.Z);
        uint depth = (uint)Math.Clamp(z, 0, 65535);
        uint denominator = Math.Max(depth, (uint)(h >> 1));
        if (denominator == 0)
        {
            throw new InvalidOperationException("Native GTE projection has zero H and depth.");
        }
        int screen = unchecked(NativeBattlePoseMatrices.RoundNative(8d * x * (h / (double)denominator)) + 8 * ox);
        screen = Math.Clamp(screen, -8192, 8184);
        //45E260 publishes the signed low WORD of packed SXY, including eighth-pixels.
        int pan = 96 * (unchecked((short)screen) - 160) / 320 + 128;
        return (byte)Math.Clamp(pan, 80, 175);
    }
}

public sealed partial record NativeBattleCameraState
{
    public NativeBattleCameraProjection? Projection { get; init; }
    public bool ProjectionOffsetsCleared { get; init; }

    public NativeBattleCameraState PublishProjection(uint graphicsFlags)
    {
        NativeBattleCameraViewTranslationScratch scratch = ViewTranslationScratch ?? throw new NotSupportedException("Native5033E0 requires its retained view-translation scratch WORDs; restore does not execute5041E0.");
        (short x, short y, short z) = scratch.Read5033E0();
        if (CurrentPose is null && (x != 0 || y != 0 || z != 0))
        {
            throw new NotSupportedException("Native5033E0 requires the fresh base camera pose before consuming a nonzero view-translation impulse; an old published view cannot be accumulated.");
        }
        NativeBattleActionMatrix? view = CurrentPose is { } pose ? NativeBattlePoseMatrices.CameraView(pose) : Projection?.View;
        if (view is not null)
        {
            view = NativeBattleCameraViewTranslationScratch.Apply5033E0(view, x, y, z);
        }
        bool published = (graphicsFlags & 4) != 0;
        NativeBattleCameraProjection? projection = view is null && Projection is null && !published ? null : new(view, published ? FocalLength is short focal ? unchecked((ushort)focal) : null : Projection?.FocalLength, published ? ProjectionOffsetsCleared ? (short)160 : null : Projection?.OffsetX, published ? ProjectionOffsetsCleared ? (short)108 : null : Projection?.OffsetY);
        //500900 writes the screen offsets, then unconditionally clears their
        //two scratch WORDs. Unknown retained first-frame offsets stay unknown.
        return this with { Projection = projection, ProjectionOffsetsCleared = ProjectionOffsetsCleared || published, ViewTranslationScratch = new(0, 0, 0) };
    }
}

public static partial class NativeBattlePoseMatrices
{
    internal static int RoundNative(double value) => ConvertRounded(value);

    public static NativeBattleActionMatrix CameraView(NativeBattleCameraPose pose)
    {
        NativeBattleCameraPoint eye = pose.Eye;
        NativeBattleCameraPoint target = pose.Target;
        Matrix? roll = pose.Roll == 0 ? null : Rotation(new(0, 0, pose.Roll));
        if (roll is not null)
        {
            (int ex, int ey, int ez) = ApplyRounded(roll, eye.X, eye.Y, eye.Z);
            (int tx, int ty, int tz) = ApplyRounded(roll, target.X, target.Y, target.Z);
            eye = new(unchecked((short)ex), unchecked((short)ey), unchecked((short)ez));
            target = new(unchecked((short)tx), unchecked((short)ty), unchecked((short)tz));
        }
        int x = target.X - eye.X;
        int y = target.Y - eye.Y;
        int z = target.Z - eye.Z;
        while (Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || Math.Abs(z) > 10000)
        {
            x = x / 2 + (x & 1);
            y = y / 2 + (y & 1);
            z = z / 2 + (z & 1);
        }
        int[] forward = Normalize(x, y, z);
        int[] right = Normalize(forward[2], 0, -forward[0]);
        int[] up = Normalize(unchecked((forward[1] * right[2] - forward[2] * right[1]) >> 12), unchecked((forward[2] * right[0] - forward[0] * right[2]) >> 12), unchecked((forward[0] * right[1] - forward[1] * right[0]) >> 12));
        Matrix matrix = new(right.Concat(up).Concat(forward).Select(value => unchecked((short)value)).ToArray());
        (int vx, int vy, int vz) = ApplyRounded(matrix, -eye.X, -eye.Y, -eye.Z);
        if (roll is not null)
        {
            matrix = Multiply(matrix, roll);
        }
        return new(matrix.Rotation.ToImmutableArray(), vx, vy, vz, 4096);
    }

    private static int[] Normalize(int x, int y, int z)
    {
        float fy = y;
        float fz = z;
        double length = Math.Sqrt((double)fz * fz + (double)fy * fy + (double)x * x);
        double factor = length == 0 ? 1 : 4096d / length;
        return [ConvertRounded(x * factor), ConvertRounded((float)(fy * factor)), ConvertRounded((float)(fz * factor))];
    }
}
