namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNoMercy200TargetInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleModuleAnchor> ReadAnchor, Func<int, short> ReadScale38, Func<int, short> ReadGround36);

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep Begin639780(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, NativeBattleNoMercy200TargetInputs inputs)
    {
        for (uint address = 0xE33D70; address < 0xE33D8C; address += 4)
        {
            frame = frame with { Staging = WriteDword(frame.Staging, address, 1) };
        }
        pool = AppendHeader(frame, pool, inputs.Packets, 0);
        return new(frame, pool, 0);
    }

    public static NativeBattleNoMercy200ChildStep AdvanceTarget(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, uint graphicsFlags, NativeBattleNoMercy200TargetInputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback is not (0x639FD0 or 0x639800))
        {
            throw new InvalidDataException("Native No Mercy target requires its actual callback.");
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleEffectParameters parameters = frame.Memory.NoMercy200?.Parameters ?? throw new NotSupportedException("Native No Mercy target requires its entered action parameters.");
        if (Word(task, 34) == 12)
        {
            int next = Word(task, 14) + 1;
            if (next <= inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17))
            {
                pool = AppendHeader(frame, pool, inputs.Packets, next);
            }
        }
        task = task.WriteWord(34, unchecked((ushort)(task.ReadWord(34) + 1)));
        bool single = task.Callback == 0x639FD0;
        int actor = single ? Word(task, 32) : -1;
        if (single)
        {
            uint address = checked(0xE33D70u + (uint)actor * 4);
            if (ReadDword(frame.Staging, address) == 0 && Word(task, 12) == 0)
            {
                return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task with { Next = pool.Slots[slot].Next }) }, 0);
            }
            if (Word(task, 12) == 0)
            {
                frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
            }
        }
        if (Word(task, 12) == 1)
        {
            if (single)
            {
                Spawn(actor, 4);
            }
            else
            {
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[checked(parameters.HeaderIndex7C + Word(task, 14))];
                int count = header.ReadByte(16);
                if (count > 0)
                {
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native No Mercy requires its actual grouped contact pointer.");
                    for (int index = 0; index < count; index++)
                    {
                        Spawn(inputs.Packets.ReadContact(checked(first + index)).Primary.ActorIndex, 3);
                    }
                }
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        bool complete = Word(task, 12) >= 22;
        if (complete && single)
        {
            frame = frame with { Staging = WriteDword(frame.Staging, checked(0xE33D70u + (uint)actor * 4), 1) };
        }
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task with { Next = pool.Slots[slot].Next }) }, complete ? 2 : 0);

        void Spawn(int target, int count)
        {
            int spread = Math.Min(unchecked(2800 * inputs.ReadScale38(target)) >> 12, 1000);
            if (spread <= 0)
            {
                throw new NotSupportedException("Native No Mercy target spread requires its original positive model scale.");
            }
            NativeBattleModuleAnchor anchor = inputs.ReadAnchor(target);
            for (int index = 0; index < count; index++)
            {
                (pool, int childSlot) = pool.Append(0x639A60);
                NativeBattleModulePoolTask child = pool.Slots[childSlot].WriteWord(12, 0);
                child = child.WriteWord(14, unchecked((ushort)(Rand() % 6 + 2 * index)));
                child = child.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(22, unchecked((ushort)anchor.W));
                child = child.WriteWord(16, unchecked((ushort)(Word(child, 16) + Rand() % spread - (spread >> 1))));
                child = child.WriteWord(18, unchecked((ushort)(Word(child, 18) + Rand() % spread - (spread >> 1))));
                child = child.WriteWord(20, unchecked((ushort)(Word(child, 20) + Rand() % spread - (spread >> 1))));
                short ground = inputs.ReadGround36(target);
                child = child.WriteWord(22, unchecked((ushort)ground));
                if (Word(child, 18) > ground - 550)
                {
                    child = child.WriteWord(18, unchecked((ushort)(Word(child, 18) - 500 - Rand() % 300)));
                }
                child = child.WriteWord(28, unchecked((ushort)(Rand() % 1536 + 3328)));
                pool = pool with { Slots = pool.Slots.SetItem(childSlot, child) };
            }
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static NativeBattleEffectTaskPool AppendHeader(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, NativeBattleActionPacketsState packets, int relative)
    {
        NativeBattleEffectParameters parameters = frame.Memory.NoMercy200?.Parameters ?? throw new NotSupportedException("Native No Mercy target requires its actual parameter block.");
        NativeBattleActionHeaderSlot header = packets.Headers[checked(parameters.HeaderIndex7C + relative)];
        bool single = header.ReadByte(16) <= 1;
        (pool, int slot) = pool.Append(single ? 0x639FD0u : 0x639800u);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)relative)).WriteWord(34, 0);
        if (single)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native No Mercy single target requires its original first contact pointer.");
            task = task.WriteWord(32, packets.ReadContact(first).Primary.ActorIndex);
        }
        return pool with { Slots = pool.Slots.SetItem(slot, task with { Next = pool.Slots[slot].Next }) };
    }
}
