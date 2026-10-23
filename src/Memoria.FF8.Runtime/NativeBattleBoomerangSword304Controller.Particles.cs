namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBoomerangSword304Controller
{
    public static (NativeBattleBoomerangSword304Frame Frame, int Result) AdvanceParticle66A330(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleBoomerangSword304Resources resources, NativeBattleBoomerangSword304Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), Signed(task, 18), 2048, -512) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(resources.Sprite.Address)).Write(4, BitConverter.GetBytes(task.ReadWord(18))).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleBoomerangSword304DrawSprite180(temporary, resources.Sprite.Address));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        task = task.WriteWord(18, unchecked((ushort)phase));
        if (phase >= 10)
        {
            return (Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            ushort velocity = task.ReadWord(20 + axis * 2);
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + velocity))).WriteWord(20 + axis * 2, unchecked((ushort)(velocity + task.ReadWord(28 + axis * 2))));
        }
        return (Write(frame, address, task), 0);
    }
}
