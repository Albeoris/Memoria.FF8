namespace Memoria.FF8.Runtime;

/// <summary>50F900's physical eight-byte projected vertex, including clip flags and low unsigned depth WORD.</summary>
public readonly record struct NativeBattleProjectedVertex(uint Screen, uint DepthAndClip)
{
    public ushort Depth => unchecked((ushort)DepthAndClip);
}

public readonly record struct NativeBattleGeometryViewport(short X, short Y, short Width, short Height);
public readonly record struct BattleSceneScreenPoint(int X, int Y);

public static class NativeBattleGeometryProjection
{
    public static NativeBattleProjectedVertex Project50F900(NativeBattleActionMatrix matrix, NativeBattleVector vertex, NativeBattleCameraProjection projection, NativeBattleGeometryViewport viewport)
    {
        var projected = ProjectUnpacked50F900(matrix, vertex, projection, viewport);
        return new(unchecked((ushort)projected.Position.X) | unchecked((uint)projected.Position.Y << 16), projected.DepthAndClip);
    }

    public static (BattleSceneScreenPoint Position, uint DepthAndClip) ProjectUnpacked50F900(NativeBattleActionMatrix matrix, NativeBattleVector vertex, NativeBattleCameraProjection projection, NativeBattleGeometryViewport viewport)
    {
        matrix.Validate();
        ushort h = projection.FocalLength ?? throw new NotSupportedException("Native50F900 requires the actual projection H WORD.");
        short ox = projection.OffsetX ?? throw new NotSupportedException("Native50F900 requires the actual screen offset X WORD.");
        short oy = projection.OffsetY ?? throw new NotSupportedException("Native50F900 requires the actual screen offset Y WORD.");
        if (viewport.Width < 0 || viewport.Height < 0)
        {
            throw new InvalidDataException("Native50F900 viewport bounds are invalid.");
        }
        //45D7F0 stores Q12 rotation as float.50F900 uses eighth-pixel XY, but multiplies its Z coefficients by1/8 first.
        float Coefficient(int index) => (float)(matrix.Rotation[index] / 4096d);
        double Row(int row) => Coefficient(row * 3 + 2) * (double)(8 * vertex.Z) + Coefficient(row * 3 + 1) * (double)(8 * vertex.Y) + Coefficient(row * 3) * (double)(8 * vertex.X);
        float cameraX = (float)(Row(0) + (float)(matrix.X * 8d));
        float cameraY = (float)(Row(1) + (float)(matrix.Y * 8d));
        double cameraZ = (float)(Coefficient(8) * 0.125d) * (double)(8 * vertex.Z) + (float)(Coefficient(7) * 0.125d) * (double)(8 * vertex.Y) + (float)(Coefficient(6) * 0.125d) * (double)(8 * vertex.X) + (float)matrix.Z;
        int roundedDepth = NativeBattlePoseMatrices.RoundNative(cameraZ);
        bool near = roundedDepth <= h >> 1;
        uint clip = near ? 0x20000u : 0;
        int x = unchecked(8 * ox + (near ? 2 * NativeBattlePoseMatrices.RoundNative(cameraX) : NativeBattlePoseMatrices.RoundNative(h / cameraZ * cameraX)));
        int y = unchecked(8 * oy + (near ? 2 * NativeBattlePoseMatrices.RoundNative(cameraY) : NativeBattlePoseMatrices.RoundNative(h / cameraZ * cameraY)));
        int left = 8 * viewport.X;
        int top = 8 * viewport.Y;
        int right = left + 8 * viewport.Width;
        int bottom = top + 8 * viewport.Height;
        if (left == 0 && top == 0)
        {
            if ((uint)x > (uint)right)
            {
                clip |= 0x1000000;
            }
            if ((uint)y > (uint)bottom)
            {
                clip |= 0x2000000;
            }
        }
        else
        {
            if (x < left)
            {
                clip |= 0x1100000;
            }
            else if (x > right)
            {
                clip |= 0x1200000;
            }
            if (y < top)
            {
                clip |= 0x2400000;
            }
            else if (y > bottom)
            {
                clip |= 0x2800000;
            }
        }
        return (new(x, y), clip | unchecked((ushort)roundedDepth));
    }
}
