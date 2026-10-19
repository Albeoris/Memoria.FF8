namespace Memoria.FF8.Runtime;

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep Begin63D1F0(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool)
    {
        (pool, int slot) = pool.Append(0x63D230);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(16, unchecked((ushort)-310)).WriteWord(18, unchecked((ushort)-200)).WriteWord(20, unchecked((ushort)-350)).WriteWord(28, 1280).WriteWord(30, 1280);
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 0);
    }

    public static NativeBattleNoMercy200ChildStep Begin63DAF0(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, NativeBattleNoMercy200TargetInputs inputs)
    {
        foreach (int actor in SelectedTargets(frame, inputs.Packets))
        {
            (pool, int slot) = pool.Append(0x63DBD0);
            NativeBattleModulePoolTask task = Anchor(pool.Slots[slot].WriteWord(12, 0), inputs.ReadAnchor(actor));
            task = task.WriteWord(24, (ushort)(Rand() % 2048)).WriteWord(26, unchecked((ushort)(Rand() % 1024 - 512)));
            ushort scale = (ushort)((Rand() % 3072 + 4608) / 3);
            task = task.WriteWord(28, scale).WriteWord(30, scale);
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        }
        return new(frame, pool, 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort random) };
            return random;
        }
    }

    public static NativeBattleNoMercy200ChildStep Begin63DD10(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, NativeBattleNoMercy200TargetInputs inputs)
    {
        ushort index = 0;
        foreach (int actor in SelectedTargets(frame, inputs.Packets))
        {
            int displacement = unchecked(2000 * inputs.ReadScale38(actor)) >> 12;
            (pool, int first) = pool.Append(0x63DE90);
            NativeBattleModulePoolTask task = pool.Slots[first].WriteWord(12, 0).WriteWord(14, index).WriteWord(24, unchecked((ushort)displacement)).WriteWord(26, checked((ushort)actor));
            pool = pool with { Slots = pool.Slots.SetItem(first, task) };
            (pool, int second) = pool.Append(0x63E170);
            task = Anchor(pool.Slots[second].WriteWord(12, 0).WriteWord(14, index), inputs.ReadAnchor(actor)).WriteWord(24, unchecked((ushort)displacement)).WriteWord(26, checked((ushort)actor));
            pool = pool with { Slots = pool.Slots.SetItem(second, task) };
            (pool, int third) = pool.Append(0x63E4E0);
            task = CopyPoint(pool.Slots[third].WriteWord(12, 0), task).WriteWord(24, (ushort)(Rand() % 2048)).WriteWord(26, (ushort)(Rand() % 120 + 50));
            ushort scale = (ushort)((Rand() % 2560 + 1536) / 4);
            task = task.WriteWord(28, scale).WriteWord(30, scale).WriteWord(32, (ushort)(Rand() % 1024 + 1280));
            pool = pool with { Slots = pool.Slots.SetItem(third, task) };
            index++;
        }
        return new(frame, pool, 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort random) };
            return random;
        }
    }

    private static IEnumerable<int> SelectedTargets(NativeBattleNoMercy200Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleActionHeaderSlot header = packets.Headers[frame.Parameters.HeaderIndex7C];
        int count = header.ReadByte(16);
        if (count == 0)
        {
            yield break;
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native No Mercy requires its actual selected header contact pointer.");
        for (int index = 0; index < count; index++)
        {
            yield return packets.ReadContact(checked(first + index)).Primary.ActorIndex;
        }
    }

    private static NativeBattleModulePoolTask Anchor(NativeBattleModulePoolTask task, NativeBattleModuleAnchor anchor) => task.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(22, unchecked((ushort)anchor.W));
}
