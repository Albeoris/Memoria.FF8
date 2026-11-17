using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleXatmDeath276PrimGeometry
{
    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleVector origin, NativeBattleActionMatrix view, NativeBattleActionMatrix effectCamera)
    {
        uint flags = node.Header;
        NativeBattleActionMatrix rotation = (flags & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x;
        int y;
        int z;
        if ((flags & 0x1000) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, origin);
            (int ox, int oy, int oz) = NativeBattleGfMatrices.Rotate56C3D0(effectCamera, node.Vector8);
            x = unchecked(x + ox);
            y = unchecked(y + oy);
            z = unchecked(z + oz);
            rotation = NativeBattleGfMatrices.Multiply56C090(effectCamera, rotation);
        }
        else if ((flags & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, origin);
            x = unchecked(x + node.Vector8.X);
            y = unchecked(y + node.Vector8.Y);
            z = unchecked(z + node.Vector8.Z);
        }
        else
        {
            NativeBattleVector translated = new(unchecked((short)(origin.X + node.Vector8.X)), unchecked((short)(origin.Y + node.Vector8.Y)), unchecked((short)(origin.Z + node.Vector8.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, translated);
            rotation = NativeBattleGfMatrices.Multiply56C090(view, rotation);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (flags & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        return rotation with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };
    }
}
