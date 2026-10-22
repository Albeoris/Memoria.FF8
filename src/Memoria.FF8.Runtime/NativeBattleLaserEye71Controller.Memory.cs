namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLaserEye71Controller
{
    public const uint RootCallback = 0x7D62A0;
    public const uint TargetCallback = 0x7D65D0;
    public const uint NodeCallback = 0x7D66D0;
    public const uint BindingCallback = 0x7DB470;
    public const uint BeamCallback = 0x7DB5C0;
    public const uint ParticleCallback = 0x7DBC10;
    public const uint StageInCallback = 0x7D64B0;
    public const uint StageOutCallback = 0x7DC050;

    public static NativeBattleModulePoolTask Read(NativeBattleLaserEye71Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    public static NativeBattleLaserEye71Frame Write(NativeBattleLaserEye71Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    public static (NativeBattleLaserEye71Frame Frame, uint Address) Append(NativeBattleLaserEye71Frame frame, int pool, uint callback, uint parent, NativeBattleLaserEye71Inputs inputs)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, owner.Stride, parent, inputs.SelectActors);
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Laser Eye exhausted its native hierarchical task pool.");
        }
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    public static NativeBattleLaserEye71TaskStep Finish(NativeBattleLaserEye71Frame frame, uint address)
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

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleLaserEye71Frame NextPhase(NativeBattleLaserEye71Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }
    private static int ArenaOffset(uint address, int length)
    {
        if (address < NativeBattleLaserEye71Profile.ArenaAddress || length < 0 || address - NativeBattleLaserEye71Profile.ArenaAddress > NativeBattleEffectArena.Length - length)
        {
            throw new InvalidDataException("Laser Eye working pointer leaves its canonical effect arena.");
        }
        return checked((int)(address - NativeBattleLaserEye71Profile.ArenaAddress));
    }
}
