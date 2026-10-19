namespace Memoria.FF8.Runtime;

public static class NativeBattleSlow123Geometry
{
    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, NativeBattleActionMatrix effectCamera, int actorScale)
    {
        bool scaled = effectCamera.Rotation[3] != 0;
        NativeBattleVector offset = node.Vector8;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16);
        if (scaled)
        {
            offset = new(unchecked((short)(offset.X * actorScale >> 12)), unchecked((short)(offset.Y * actorScale >> 12)), unchecked((short)(offset.Z * actorScale >> 12)));
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, actorScale, actorScale, actorScale);
        }
        bool billboard = (node.Header & 0x1000) != 0;
        bool direct = !billboard && (node.Header & 0x200) != 0;
        NativeBattleActionMatrix basis = billboard ? effectCamera : context;
        (int x, int y, int z) = direct ? (offset.X, offset.Y, offset.Z) : NativeBattleGfMatrices.Rotate56C3D0(basis, offset);
        if (!direct && (node.Header & 0x8000) == 0)
        {
            matrix = NativeBattleGfMatrices.Multiply56C090(basis, matrix);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(x + context.X), Y = unchecked(y + context.Y), Z = unchecked(z + context.Z) };
    }

    public static ushort BlinkFlags(ushort flags, int counter, bool completed)
    {
        int elapsed = counter - 20;
        if (completed)
        {
            return (ushort)(flags & 0xFFF7);
        }
        if ((uint)elapsed < 40)
        {
            bool hidden = elapsed != 0 && (uint)elapsed % (uint)(40 - elapsed) == 0;
            return hidden ? (ushort)(flags | 8) : (ushort)(flags & 0xFFF7);
        }
        return flags;
    }
}
