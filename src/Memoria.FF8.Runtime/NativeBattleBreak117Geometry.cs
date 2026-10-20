namespace Memoria.FF8.Runtime;

public static class NativeBattleBreak117Geometry
{
    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleVector anchor, int scale, NativeBattleActionMatrix view, NativeBattleActionMatrix effectCamera)
    {
        NativeBattleActionMatrix rotation = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale);
        NativeBattleVector offset = new(unchecked((short)(unchecked(node.Vector8.X * scale) >> 12)), unchecked((short)(unchecked(node.Vector8.Y * scale) >> 12)), unchecked((short)(unchecked(node.Vector8.Z * scale) >> 12)));
        int x;
        int y;
        int z;
        if ((node.Header & 0x1000) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            (int dx, int dy, int dz) = NativeBattleGfMatrices.Rotate56C3D0(effectCamera, offset);
            x = unchecked(x + dx);
            y = unchecked(y + dy);
            z = unchecked(z + dz);
            rotation = NativeBattleGfMatrices.Multiply56C090(effectCamera, rotation);
        }
        else
        {
            offset = new(unchecked((short)(offset.X + anchor.X)), unchecked((short)(offset.Y + anchor.Y)), unchecked((short)(offset.Z + anchor.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, offset);
            rotation = NativeBattleGfMatrices.Multiply56C090(view, rotation);
        }
        NativeBattleVector stretch = node.Vector24;
        if (stretch is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(rotation, new([stretch.X, 0, 0, 0, stretch.Y, 0, 0, 0, stretch.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(rotation, stretch.X, stretch.Y, stretch.Z);
        }
        return rotation with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }

    public static NativeBattleModulePoolTask DebrisMove(NativeBattleModulePoolTask task)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(32 + axis * 2, unchecked((ushort)(Signed(task, 32 + axis * 2) + Signed(task, 40 + axis * 2))));
        }
        task = task.WriteWord(26, unchecked((ushort)(Signed(task, 26) + 10)));
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(16 + axis * 2, unchecked((ushort)(Signed(task, 16 + axis * 2) + Signed(task, 24 + axis * 2))));
        }
        if (Signed(task, 18) > 0)
        {
            short speed = Signed(task, 26);
            task = task.WriteWord(18, 0).WriteWord(26, unchecked((ushort)((speed >> 2) - speed)));
        }
        return task;
    }

    public static NativeBattleModulePoolTask SparkMove(NativeBattleModulePoolTask task)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(24 + axis * 2, unchecked((ushort)(Signed(task, 24 + axis * 2) + Signed(task, 32 + axis * 2))));
            task = task.WriteWord(16 + axis * 2, unchecked((ushort)(Signed(task, 16 + axis * 2) + Signed(task, 24 + axis * 2))));
        }
        return task;
    }
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
