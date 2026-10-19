using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleModuleProjectedVertex(uint Screen, ushort Depth, uint Flags)
{
    // Bulk projected records reserve the low WORD for depth; GTE screen-clamp flags13/14 are separate registers.
    public static implicit operator NativeBattleProjectedVertex(NativeBattleModuleProjectedVertex vertex) => new(vertex.Screen, vertex.Depth | (vertex.Flags & 0xFFFF0000));
}

public readonly record struct NativeBattleModuleDepthCueVertex(NativeBattleModuleProjectedVertex Projected, uint Cue)
{
    public short Ir2 { get; init; }
}

/// <summary>45FE10/45FAA0 module projection, distinct from50F900's bulk body projection.</summary>
public static class NativeBattleModuleProjection
{
    public static NativeBattleModuleProjectedVertex Project45FE10(NativeBattleActionMatrix matrix, NativeBattleVector vertex, NativeBattleCameraProjection projection) => ProjectCore(matrix, vertex, projection).Projected;

    public static NativeBattleModuleProjectedVertex Project45FAA0(NativeBattleActionMatrix matrix, NativeBattleVector vertex, NativeBattleCameraProjection projection, short dqa, int dqb)
    {
        return ProjectWithCue45FAA0(matrix, vertex, projection, dqa, dqb).Projected;
    }

    public static NativeBattleModuleDepthCueVertex ProjectWithCue45FAA0(NativeBattleActionMatrix matrix, NativeBattleVector vertex, NativeBattleCameraProjection projection, short dqa, int dqb)
    {
        (NativeBattleModuleProjectedVertex projected, int x, int y, int z, double factor) = ProjectCore(matrix, vertex, projection);
        uint flags = projected.Flags;
        if (x is < short.MinValue or > short.MaxValue)
        {
            flags |= 0x81000000;
        }
        if (y is < short.MinValue or > short.MaxValue)
        {
            flags |= 0x80800000;
        }
        if (z is < short.MinValue or > short.MaxValue)
        {
            flags |= 0x00400000;
        }
        double cue = (double)dqa * factor;
        cue *= 65536d;
        cue += dqb;
        int roundedCue = NativeBattlePoseMatrices.RoundNative(cue * (1d / 4096d));
        if (roundedCue is < 0 or > 4095)
        {
            flags |= 0x00001000;
        }
        return new(projected with { Flags = flags }, (uint)Math.Clamp(roundedCue, 0, 0xFFF)) { Ir2 = (short)Math.Clamp(y, short.MinValue, short.MaxValue) };
    }

    private static (NativeBattleModuleProjectedVertex Projected, int X, int Y, int Z, double Factor) ProjectCore(NativeBattleActionMatrix matrix, NativeBattleVector vertex, NativeBattleCameraProjection projection)
    {
        matrix.Validate();
        ushort h = projection.FocalLength ?? throw new NotSupportedException("Native45FE10 requires its actual GTE H WORD.");
        short ox = projection.OffsetX ?? throw new NotSupportedException("Native45FE10 requires its actual screen X offset WORD.");
        short oy = projection.OffsetY ?? throw new NotSupportedException("Native45FE10 requires its actual screen Y offset WORD.");
        int Row(int row) => NativeBattlePoseMatrices.RoundNative((float)(matrix.Rotation[row * 3 + 2] / 4096d) * (double)vertex.Z + (float)(matrix.Rotation[row * 3 + 1] / 4096d) * (double)vertex.Y + (float)(matrix.Rotation[row * 3] / 4096d) * (double)vertex.X);
        int x = unchecked(Row(0) + matrix.X);
        int y = unchecked(Row(1) + matrix.Y);
        int z = unchecked(Row(2) + matrix.Z);
        int depth = Math.Clamp(z, 0, 65535);
        uint flags = z != depth ? 0x80040000u : 0;
        if (depth <= h >> 1)
        {
            flags |= 0x80020000;
            if (depth < 1)
            {
                flags |= 0x80040000;
            }
        }
        int denominator = Math.Max(depth, h >> 1);
        if (denominator == 0)
        {
            throw new InvalidDataException("Native45FE10 has neither a positive H/2 nor depth denominator.");
        }
        double factor = h / (double)denominator;
        int sx = unchecked(NativeBattlePoseMatrices.RoundNative(unchecked(x * 8) * factor) + 8 * ox);
        int sy = unchecked(NativeBattlePoseMatrices.RoundNative(unchecked(y * 8) * factor) + 8 * oy);
        if (sx is < -8192 or > 8184)
        {
            flags |= 0x80004000;
        }
        if (sy is < -8192 or > 8184)
        {
            flags |= 0x80002000;
        }
        sx = Math.Clamp(sx, -8192, 8184);
        sy = Math.Clamp(sy, -8192, 8184);
        return (new(unchecked((ushort)sx) | unchecked((uint)sy << 16), (ushort)depth, flags), x, y, z, factor);
    }
}

public static partial class NativeBattlePoseMatrices
{
    public static NativeBattleActionMatrix ModuleMeshCamera(NativeBattleModuleDraw draw, NativeBattleActionMatrix camera)
    {
        NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new InvalidDataException("Native mesh lacks its original translation.");
        NativeBattleVector rotation = draw.MeshRotation ?? throw new InvalidDataException("Native mesh lacks its original rotation.");
        NativeBattleVector scale = draw.MeshScale ?? throw new InvalidDataException("Native mesh lacks its original scale.");
        Matrix world = Scale(Rotation(rotation), [scale.X, scale.Y, scale.Z]) with { X = anchor.X, Y = anchor.Y, Z = anchor.Z };
        Matrix result = Compose(new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z), world);
        return new(result.Rotation.ToImmutableArray(), result.X, result.Y, result.Z, 4096);
    }

    public static NativeBattleActionMatrix ModuleSpriteCamera(NativeBattleModuleDraw draw, NativeBattleActionMatrix camera, short cameraRoll)
    {
        camera.Validate();
        NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new InvalidDataException("Native sprite lacks its original translation.");
        if ((draw.SpriteFlags & ~0x3FF) != 0 || !draw.SpriteWorldRotation.IsEmpty && draw.SpriteWorldRotation.Length != 9 || (draw.SpriteFlags & 2) != 0 && draw.Direction is not (4096 or -4096))
        {
            throw new NotSupportedException("Native sprite setup requires its original matrix and supported571C80 flags.");
        }
        Matrix view = new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z);
        (int x, int y, int z) = ApplyRounded(view, anchor.X, anchor.Y, anchor.Z);
        x = unchecked(x + camera.X);
        y = unchecked(y + camera.Y);
        z = unchecked(z + camera.Z + draw.SpriteCameraZOffset);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        x = unchecked(x + (draw.DepthOffset * direction.X >> 12));
        y = unchecked(y + (draw.DepthOffset * direction.Y >> 12));
        z = unchecked(z + (draw.DepthOffset * direction.Z >> 12));
        x = unchecked(x + draw.SpriteCameraXOffset);
        y = unchecked(y + draw.SpriteCameraYOffset);
        Matrix main = draw.SpriteWorldRotation.IsEmpty ? new([unchecked((short)draw.Scale), 0, 0, 0, unchecked((short)draw.Scale), 0, 0, 0, unchecked((short)draw.Scale)]) : Multiply(view, new(draw.SpriteWorldRotation.ToArray()));
        int angle = (draw.SpriteFlags & 1) != 0 ? draw.Rotation - cameraRoll : (draw.SpriteFlags & 0x200) == 0 ? -cameraRoll : 0;
        if (angle != 0)
        {
            main = Multiply(Rotation(new(0, 0, unchecked((short)angle))), main);
        }
        if ((draw.SpriteFlags & 2) != 0)
        {
            short[] words = main.Rotation.ToArray();
            for (int column = 0; column < 3; column++)
            {
                words[column] = unchecked((short)(words[column] * draw.Direction >> 12));
            }
            main = main with { Rotation = words };
        }
        return new(main.Rotation.ToImmutableArray(), x, y, z, 4096);
    }

    public static NativeBattleActionMatrix ModuleSpriteQuadCamera(NativeBattleActionMatrix main, ReadOnlySpan<byte> quad)
    {
        if (quad.Length != 20)
        {
            throw new InvalidDataException("Native571DC0 requires its original twenty-byte quad.");
        }
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
        short[] local = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        if ((flags & 0xC00) != 0)
        {
            short angle = BinaryPrimitives.ReadInt16LittleEndian(quad[12..]);
            int cosine = NativeBattleCameraMath.Cosine(angle);
            int sine = NativeBattleCameraMath.Sine(angle);
            short scaleX = BinaryPrimitives.ReadInt16LittleEndian(quad[16..]);
            short scaleY = BinaryPrimitives.ReadInt16LittleEndian(quad[18..]);
            local[0] = unchecked((short)(scaleX * cosine >> 12));
            local[3] = unchecked((short)(scaleX * sine >> 12));
            local[1] = unchecked((short)(-scaleY * sine >> 12));
            local[4] = unchecked((short)(scaleY * cosine >> 12));
        }
        NativeBattleActionMatrix multiplied = MultiplyGteRotation45E470(main, new(local.ToImmutableArray(), 0, 0, 0, 4096));
        short dx = unchecked((short)(8 * quad[0] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad[8..])));
        short dy = unchecked((short)(8 * quad[2] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad[10..])));
        (int x, int y, int z) = ApplyRounded(new(main.Rotation.ToArray()), dx, dy, 0);
        return new(multiplied.Rotation, unchecked(main.X + x), unchecked(main.Y + y), unchecked(main.Z + z), 4096);
    }

    /// <summary>45DE00/45E180/460820/45E470 multiply rotation columns with PC rounding and saturated IR stores; translation is retained.</summary>
    public static NativeBattleActionMatrix MultiplyGteRotation45E470(NativeBattleActionMatrix left, NativeBattleActionMatrix right)
    {
        left.Validate();
        right.Validate();
        short[] rotation = new short[9];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                double value = left.Rotation[row * 3 + 2] / 4096d * right.Rotation[6 + column] + left.Rotation[row * 3 + 1] / 4096d * right.Rotation[3 + column] + left.Rotation[row * 3] / 4096d * right.Rotation[column];
                rotation[row * 3 + column] = (short)Math.Clamp(RoundNative(value), short.MinValue, short.MaxValue);
            }
        }
        return left with { Rotation = rotation.ToImmutableArray() };
    }
}
