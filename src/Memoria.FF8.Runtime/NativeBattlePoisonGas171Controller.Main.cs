namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoisonGas171Controller
{
    public static (NativeBattlePoisonGas171Frame Frame, int Result) AdvanceMain(NativeBattlePoisonGas171Frame frame, uint address, NativeBattlePoisonGas171Resources resources, NativeBattlePoisonGas171Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        int caster = checked((int)frame.State.ReadDword(0x23AF80C));
        if (age == 1)
        {
            (frame, uint body) = RequireAppend(frame, 0x5DB620);
            ushort saved = (ushort)(inputs.ReadModel(frame, caster).Flags0 & 4);
            frame = Write(frame, body, Read(frame, body).WriteWord(12, 0).WriteWord(26, saved));
            NativeBattlePoisonGas171PointStep bone = inputs.ReadBone502170(frame, caster, 0xF1, 0);
            frame = bone.Frame;
            frame = Write(frame, address, CopyTask(Read(frame, address), 16, bone.Point8, 6));
            NativeBattleEffectArena point = TaskView(Read(frame, address), 16, 8);
            foreach ((uint callback, ushort group, ushort delay) in new (uint, ushort, ushort)[] { (0x5DBED0, 1, 0), (0x5DBED0, 2, 2), (0x5DC270, 1, 4), (0x5DC270, 2, 6) })
            {
                (frame, uint child) = RequireAppend(frame, callback);
                frame = Write(frame, child, CopyTask(Read(frame, child), 16, point, 8).WriteWord(12, 0).WriteWord(14, group).WriteWord(24, delay));
            }
        }
        if (age == 8)
        {
            foreach ((ushort movement, ushort spin) in new (ushort, ushort)[] { (10, 30), (6, 15) })
            {
                (frame, uint child) = RequireAppend(frame, 0x5DC5B0);
                NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(16, 0xFE70).WriteWord(22, movement).WriteWord(28, 0xB00);
                frame = DrawRandom(frame, out ushort angle);
                frame = Write(frame, child, task.WriteWord(24, (ushort)(angle & 0xFFF)).WriteWord(26, spin));
            }
        }
        if (age == 27)
        {
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
            int count = header.ReadByte(16);
            if (count != 0)
            {
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Poison Gas requires its captured physical contact pointer.");
                for (int ordinal = 0; ordinal < count; ordinal++)
                {
                    int actor = inputs.Packets.ReadContact(checked(first + ordinal)).Primary.ActorIndex;
                    (frame, uint child) = RequireAppend(frame, 0x5DC700);
                    frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(4u << (ordinal & 31)))));
                    NativeBattlePoisonGas171PointStep bone = inputs.ReadBone502170(frame, actor, 0xF1, 0);
                    frame = bone.Frame;
                    frame = Write(frame, child, CopyTask(Read(frame, child), 16, bone.Point8, 6));
                }
            }
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattlePoisonGas171Contacts506BA0(Parameters(frame).HeaderIndex7C));
        }
        if (age <= 16)
        {
            frame = inputs.Execute(frame, new NativeBattlePoisonGas171Stage5713E0(age << 7, 0));
        }
        else if (age >= 42)
        {
            frame = inputs.Execute(frame, new NativeBattlePoisonGas171Stage5713E0((50 - age) << 8, 0));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattlePoisonGas171Sound501330(resources.Sound, 0, 128));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return Signed(Read(frame, address), 12) > 50 ? (inputs.Execute(frame, new NativeBattlePoisonGas171Stage5713E0(0, 0)), 2) : (frame, 0);
    }
}
