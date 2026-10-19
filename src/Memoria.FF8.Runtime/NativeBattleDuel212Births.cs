namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel212Controller
{
    public static NativeBattleDuel212Step Spawn6A3400(NativeBattleDuel212Frame frame, NativeBattleEffectTaskPool pool, int count, NativeBattleVector origin, int lifeSpread, byte lifeBase, int radiusSpread, int radiusBase, int scaleSpread, short scaleBase, uint callback)
    {
        do
        {
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                break;
            }
            (pool, int slot) = pool.Append(callback);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteDword(24, 0).WriteByte(25, unchecked((byte)-20));
            task = task.WriteWord(32, unchecked((ushort)(scaleBase + ((scaleSpread * Rand()) >> 15))));
            int radius = radiusBase + ((radiusSpread * Rand()) >> 15);
            int angle = Rand();
            task = task.WriteWord(12, unchecked((ushort)(origin.X + ((radius * NativeBattleCameraMath.Cosine(angle)) >> 12))));
            task = task.WriteWord(14, unchecked((ushort)origin.Y));
            task = task.WriteWord(16, unchecked((ushort)(origin.Z + ((radius * NativeBattleCameraMath.Sine(angle)) >> 12))));
            task = task.WriteWord(18, 0).WriteWord(20, 0).WriteWord(22, 0);
            task = task.WriteByte(27, unchecked((byte)(lifeBase + ((lifeSpread * Rand()) >> 15))));
            task = task.WriteByte(28, unchecked((byte)Rand())).WriteByte(29, unchecked((byte)Rand())).WriteByte(30, unchecked((byte)Rand())).WriteByte(31, unchecked((byte)Rand()));
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        }
        while (count-- != 0);
        return new(frame, pool, 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleDuel212Step Spawn6A3510(NativeBattleDuel212Frame frame, NativeBattleEffectTaskPool pool, int workspaceFrame, NativeBattleGfAnimationNode node, Func<int, NativeBattleVector> readSelectedVertex, NativeBattleVector origin)
    {
        if (workspaceFrame >= 32 || pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return new(frame, pool, 0);
        }
        NativeBattleDuel212Resources resources = Resources(frame);
        int table = 32 * unchecked((short)node.GeometryIndex);
        int first = resources.SelectorTable[checked(table + node.Word40)];
        int second = resources.SelectorTable[checked(table + node.Word42)];
        NativeBattleVector a = readSelectedVertex(first * 8);
        NativeBattleVector b = readSelectedVertex(second * 8);
        short ratio = node.Scalar38;
        NativeBattleVector point = NativeBattleQuezacotlRibbonMath.Blend(a, b, 4096 - ratio, ratio);
        (pool, int slot) = pool.Append(0x6A3630);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        task = task.WriteWord(12, unchecked((ushort)(origin.X - point.X))).WriteWord(14, 0).WriteWord(16, unchecked((ushort)(origin.Z - point.Z)));
        task = task.WriteDword(24, 0).WriteByte(25, 30).WriteWord(20, 0).WriteWord(22, 0);
        int speed = Rand() & 0x3F;
        task = task.WriteWord(18, unchecked((ushort)(node.GeometryIndex != 0 ? speed + 90 : -90 - speed)));
        task = task.WriteByte(27, 10).WriteWord(32, unchecked((ushort)(((Rand() << 12) >> 15) + 2048)));
        task = task.WriteByte(28, unchecked((byte)Rand())).WriteByte(29, unchecked((byte)Rand())).WriteByte(30, unchecked((byte)Rand())).WriteByte(31, unchecked((byte)Rand()));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
