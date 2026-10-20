namespace Memoria.FF8.Runtime;

public static class NativeBattleFloat115Geometry
{
    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, int actorScale)
    {
        NativeBattleVector offset = node.Vector8;
        offset = new(unchecked((short)(offset.X * actorScale >> 12)), unchecked((short)(offset.Y * actorScale >> 12)), unchecked((short)(offset.Z * actorScale >> 12)));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler701310(node.Vector16), actorScale, actorScale, actorScale);
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(context, offset);
        if ((node.Header & 0x8000) == 0)
        {
            matrix = NativeBattleGfMatrices.Multiply56C090(context, matrix);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(x + context.X), Y = unchecked(y + context.Y), Z = unchecked(z + context.Z) };
    }

    public static int UpperDepth(NativeBattleActionMatrix context, NativeBattleVector point, short actorSize)
    {
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(context, point);
        return unchecked((actorSize + 2048) * rotated.Z) >> 15;
    }
}
