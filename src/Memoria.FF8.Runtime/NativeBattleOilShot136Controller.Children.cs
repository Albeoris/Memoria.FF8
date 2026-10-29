using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleOilShot136Controller
{
    public static (NativeBattleOilShot136Frame Frame, int Result) AdvanceSimpleSprite(NativeBattleOilShot136Frame frame, uint address, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 12), Signed(task, 18), 4096, -1024) };
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { Staging = NativeBattleModuleSpriteTemporary.Begin571C80(RequireStaging(frame), temporary, SpriteBAddress, 0).Write(temporary + 4, BitConverter.GetBytes(task.ReadWord(18))) };
        frame = inputs.Execute(frame, new NativeBattleOilShot136DrawSprite180(temporary, SpriteBAddress) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(136, Read(frame, address)) with { ResourceGeneration = SpriteBAddress } });
        frame = Release(frame, 180);
        task = Read(frame, address).WriteWord(18, unchecked((ushort)(Read(frame, address).ReadWord(18) + 1)));
        return (Write(frame, address, task), Signed(task, 18) >= 16 ? 2 : 0);
    }

    public static (NativeBattleOilShot136Frame Frame, int Result) AdvanceMovingSprite(NativeBattleOilShot136Frame frame, uint address, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = Signed(task, 22);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 16), scale, scale, unchecked((short)-(scale >> 2))) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(RequireStaging(frame), temporary, SpriteAAddress, 1).Write(temporary + 4, BitConverter.GetBytes(task.ReadWord(30))).Write(temporary + 8, BitConverter.GetBytes((int)Signed(task, 12)));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleOilShot136DrawSprite180(temporary, SpriteAAddress) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(136, Read(frame, address)) with { ResourceGeneration = SpriteAAddress } });
        frame = Release(frame, 180);
        task = Read(frame, address);
        task = Vector(task, 16, new(unchecked((short)(Signed(task, 16) + Signed(task, 24))), unchecked((short)(Signed(task, 18) + Signed(task, 26))), unchecked((short)(Signed(task, 20) + Signed(task, 28)))));
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + task.ReadWord(14)))).WriteWord(30, unchecked((ushort)(task.ReadWord(30) + 1)));
        return (Write(frame, address, task), Signed(task, 30) >= 9 ? 2 : 0);
    }
    public static (NativeBattleOilShot136Frame Frame, int Result) AdvanceEmitter(NativeBattleOilShot136Frame frame, uint address, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, NativeBattlePackedMatrix32 matrix) = CameraPoint(frame, inputs, Vector(task, 12), Signed(task, 18));
        NativeBattleOilShot136ActStep step = inputs.AdvanceAct(frame, address, resources.EmitterAct, 0x1C, 0xA8, ActContext(frame, matrix, 0, 2));
        frame = step.Frame;
        if (step.Remaining == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Move(Read(frame, address))), 0);
    }

    public static (NativeBattleOilShot136Frame Frame, int Result) AdvanceLargeParticle(NativeBattleOilShot136Frame frame, uint address, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool impact = task.ReadWord(18) != 0;
        (frame, NativeBattlePackedMatrix32 matrix) = CameraPoint(frame, inputs, Vector(task, 12), Signed(task, 18));
        NativeBattleOilShot136ActStep step = inputs.AdvanceAct(frame, address, impact ? resources.LargeImpactAct : resources.LargeParticleAct, 0x1C, impact ? 0xA8 : 0x50, ActContext(frame, matrix, 0, impact ? 1 : 2));
        frame = step.Frame;
        task = Read(frame, address);
        if (step.Remaining == 0 && task.ReadWord(18) == 1)
        {
            return (frame, 2);
        }
        if (task.ReadWord(18) == 0)
        {
            task = Move(task.WriteWord(22, unchecked((ushort)(task.ReadWord(22) + 16))));
            if (Signed(task, 14) >= 0)
            {
                task = InitializeWorkspace(task.WriteWord(14, 0).WriteWord(18, 1), 0x1C, 0xA8, resources.LargeImpactAct);
            }
            frame = Write(frame, address, task);
        }
        return (frame, 0);
    }

    private static NativeBattleModulePoolTask Move(NativeBattleModulePoolTask task) => Vector(task, 12, new(unchecked((short)(Signed(task, 12) + Signed(task, 20))), unchecked((short)(Signed(task, 14) + Signed(task, 22))), unchecked((short)(Signed(task, 16) + Signed(task, 24)))));
}
