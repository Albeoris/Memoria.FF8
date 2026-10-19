namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep Advance5F16A0(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        if (counter < 34 && counter % 4 == 1)
        {
            Spawn(0x5F1800, 853, 1365, 50, 40, true);
        }
        if (counter < 36 && counter % 4 == 1)
        {
            Spawn(0x5F1970, 1843, 896, 65, 25, false);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))) with { Next = pool.Slots[slot].Next };
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, task.ReadWord(12) >= 48 ? 2 : 0);

        void Spawn(uint callback, int scale, int height, int divisor, int minimum, bool verticalSpeed)
        {
            (pool, int created) = pool.Append(callback);
            NativeBattleModulePoolTask child = pool.Slots[created].WriteWord(12, 0).WriteWord(16, unchecked((ushort)ReadWord(frame.Staging, 0x23C9D98))).WriteWord(18, 0).WriteWord(20, unchecked((ushort)ReadWord(frame.Staging, 0x23C9D9C)));
            frame = frame with { Random = frame.Random.Draw(out ushort angle) };
            child = child.WriteWord(24, (ushort)(angle % 4096));
            frame = frame with { Random = frame.Random.Draw(out ushort speed) };
            child = child.WriteWord(26, (ushort)(speed % divisor + minimum)).WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale).WriteWord(32, (ushort)height);
            if (verticalSpeed)
            {
                child = child.WriteWord(34, (ushort)height);
            }
            pool = pool with { Slots = pool.Slots.SetItem(created, child) };
        }
    }

    public static NativeBattleBlastingZone164ChildStep AdvanceExpandingMesh(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool first = task.Callback switch
        {
            0x5F1800 => true,
            0x5F1970 => false,
            _ => throw new NotSupportedException("Unsupported native expanding mesh callback.")
        };
        short counter = Word(task, 12);
        int fadeStart = first ? 6 : 4;
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Word(task, 28), Word(task, 32), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        frame = inputs.Execute(frame, new NativeBattleBlastingZone164MeshDraw(0xD948C4, matrix, new(counter >= fadeStart ? 0xF3u : 0x33u, 0, counter >= fadeStart ? (counter - fadeStart) << 9 : null, null, null, null, 17, 2), false));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
        task = Damp(task, 28, 30, first ? 8 : 12);
        if (first)
        {
            task = Damp(task, 32, 34, 10);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, task.ReadWord(12) >= (first ? 14 : 12) ? 2 : 0);
    }
}
