namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSnipeLaser302Controller
{
    public static (NativeBattleSnipeLaser302Frame Frame, int Result) AdvanceParticle66D6D0(NativeBattleSnipeLaser302Frame frame, uint address, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), Signed(task, 18), 3072, -768) };
        frame = DrawSprite302(frame, temporary, resources.SpriteB.Address, task.ReadWord(18), inputs);
        frame = Release(frame, 180);
        task = Read(frame, address);
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        task = task.WriteWord(18, unchecked((ushort)phase));
        if (phase >= 16)
        {
            return (Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            ushort velocity = task.ReadWord(20 + axis * 2);
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + velocity)));
            if (phase < 12)
            {
                task = task.WriteWord(20 + axis * 2, unchecked((ushort)(velocity + task.ReadWord(28 + axis * 2))));
            }
        }
        return (Write(frame, address, task), 0);
    }

    public static (NativeBattleSnipeLaser302Frame Frame, int Result) AdvanceSprite66D790(NativeBattleSnipeLaser302Frame frame, uint address, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), Signed(task, 18), 2800, -1024) };
        frame = DrawSprite302(frame, temporary, resources.SpriteA.Address, task.ReadWord(18), inputs);
        frame = Release(frame, 180);
        task = Read(frame, address);
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        frame = Write(frame, address, task.WriteWord(18, unchecked((ushort)phase)));
        return (frame, phase < 16 ? 0 : 2);
    }

    private static NativeBattleSnipeLaser302Frame DrawSprite302(NativeBattleSnipeLaser302Frame frame, uint temporary, uint source, ushort phase, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(phase)).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        return inputs.Execute(frame, new NativeBattleSnipeLaser302DrawSprite180(temporary, source));
    }
}
