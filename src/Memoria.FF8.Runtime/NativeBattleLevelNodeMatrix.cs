namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleLevelScale(int X, int Y, int Z);

public static class NativeBattleLevelNodeMatrix
{
    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleLevelScale scale, bool scaleEnabled, NativeBattleActionMatrix view, NativeBattleActionMatrix billboard)
    {
        NativeBattleActionMatrix rotation = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = node.Vector8;
        if (scaleEnabled)
        {
            offset = new(unchecked((short)(unchecked(offset.X * scale.X) >> 12)), unchecked((short)(unchecked(offset.Y * scale.Y) >> 12)), unchecked((short)(unchecked(offset.Z * scale.Z) >> 12)));
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        int x;
        int y;
        int z;
        if ((node.Header & 0x1000) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            (int dx, int dy, int dz) = NativeBattleGfMatrices.Rotate56C3D0(billboard, offset);
            x = unchecked(x + dx);
            y = unchecked(y + dy);
            z = unchecked(z + dz);
            rotation = NativeBattleGfMatrices.Multiply56C090(billboard, rotation);
        }
        else if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            x = unchecked(x + offset.X);
            y = unchecked(y + offset.Y);
            z = unchecked(z + offset.Z);
        }
        else
        {
            NativeBattleVector translated = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, translated);
            rotation = NativeBattleGfMatrices.Multiply56C090(view, rotation);
        }
        NativeBattleVector local = node.Vector24;
        if (local is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, local.X, local.Y, local.Z);
        }
        return rotation with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }

    public static NativeBattleLevelScale BoundsScale(NativeBattleVector minimum, NativeBattleVector maximum)
    {
        int x = maximum.X - minimum.X;
        int y = maximum.Y - minimum.Y;
        int z = maximum.Z - minimum.Z;
        if (x < z)
        {
            (x, z) = (z, x);
        }
        if (y > x)
        {
            x = y;
            z = y;
        }
        return new(x, y, z);
    }
}
