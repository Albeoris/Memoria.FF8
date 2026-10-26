using static Memoria.FF8.Runtime.NativeBattleEffect30Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect30Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleEffect30Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    public static NativeBattleEffect30Frame Write(NativeBattleEffect30Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };

    public static (NativeBattleEffect30Frame Frame, uint Address) Append(NativeBattleEffect30Frame frame, int pool, uint callback, uint parent, NativeBattleEffect30Inputs inputs)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, owner.Stride, parent, inputs.SelectActors);
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Effect30 exhausted its native hierarchical task pool.");
        }
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    public static NativeBattleEffect30TaskStep Finish(NativeBattleEffect30Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        byte flags = task.ReadByte(0x26);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        if ((flags & 1) == 0 || task.ReadByte(0x28) != 0)
        {
            return new(frame, 0);
        }
        return new(frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } }, 2);
    }

    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    public static NativeBattleEffect30Frame NextPhase(NativeBattleEffect30Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    public static int ArenaOffset(uint address, int length)
    {
        if (address < ArenaAddress || length < 0 || address - ArenaAddress > NativeBattleEffectArena.Length - length)
        {
            throw new InvalidDataException("Effect30 working pointer leaves its current effect arena.");
        }
        return checked((int)(address - ArenaAddress));
    }
}
