using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSaliva138Controller
{
    public static (NativeBattleSaliva138Frame Frame, int Result) AdvanceSimpleSprite(NativeBattleSaliva138Frame frame, uint address, NativeBattleSaliva138Resources resources, NativeBattleSaliva138Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 12), Signed(task, 18), 3072, -768) };
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { Staging = NativeBattleModuleSpriteTemporary.Begin571C80(RequireStaging(frame), temporary, SpriteBAddress, 0).Write(temporary + 4, BitConverter.GetBytes(task.ReadWord(18))) };
        frame = inputs.Execute(frame, new NativeBattleSaliva138DrawSprite180(temporary, SpriteBAddress));
        frame = Release(frame, 180);
        task = Read(frame, address).WriteWord(18, unchecked((ushort)(Read(frame, address).ReadWord(18) + 1)));
        return (Write(frame, address, task), Signed(task, 18) >= 13 ? 2 : 0);
    }

    public static (NativeBattleSaliva138Frame Frame, int Result) AdvanceMovingSprite(NativeBattleSaliva138Frame frame, uint address, NativeBattleSaliva138Resources resources, NativeBattleSaliva138Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = Signed(task, 22);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 16), scale, scale, unchecked((short)-(scale >> 2))) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(RequireStaging(frame), temporary, SpriteAAddress, 1).Write(temporary + 4, BitConverter.GetBytes(task.ReadWord(30))).Write(temporary + 8, BitConverter.GetBytes((int)Signed(task, 12)));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleSaliva138DrawSprite180(temporary, SpriteAAddress));
        frame = Release(frame, 180);
        task = Read(frame, address);
        task = Vector(task, 16, new(unchecked((short)(Signed(task, 16) + Signed(task, 24))), unchecked((short)(Signed(task, 18) + Signed(task, 26))), unchecked((short)(Signed(task, 20) + Signed(task, 28)))));
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + task.ReadWord(14)))).WriteWord(30, unchecked((ushort)(task.ReadWord(30) + 1)));
        return (Write(frame, address, task), Signed(task, 30) >= 10 ? 2 : 0);
    }
}
