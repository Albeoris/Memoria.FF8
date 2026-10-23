namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static (NativeBattleChainGun190Frame Frame, int Result) AdvanceMain(NativeBattleChainGun190Frame frame, uint address, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        int caster = checked((int)frame.State.ReadDword(0x2283F2C));
        NativeBattleChainGun190PointStep bone = inputs.ReadBone502170(frame, caster, 11, 6144);
        frame = bone.Frame with { State = bone.Frame.State.WriteView(0x2283F38, bone.Point8, 6) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            frame = PrepareShots(frame, inputs);
            frame = Write(frame, address, Read(frame, address).WriteWord(14, 0));
        }
        if (age >= 2 && age <= 25 && (age & 1) != 0)
        {
            (frame, uint child) = RequireAppend(frame, 0x5BB5A0);
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, frame.State.ReadView(0x2283F38, 8), 8).WriteWord(12, 0);
            short rotation = inputs.ReadModel(frame, caster).Rotation14 ?? throw new NotSupportedException("Chain Gun requires its current caster rotationWORD14.");
            task = task.WriteWord(24, unchecked((ushort)rotation));
            frame = DrawRandom(frame, out ushort angle);
            task = task.WriteWord(26, (ushort)(angle & 0x7FF));
            frame = DrawRandom(frame, out ushort scale);
            frame = Write(frame, child, task.WriteWord(28, (ushort)(scale % 0x500 + 0xA00)));
            int index = Signed(Read(frame, address), 14);
            uint record = checked(0x2282E08u + (uint)(index * 16));
            if (frame.State.ReadDword(record) != 0)
            {
                bool positioned = unchecked((int)frame.State.ReadDword(record + 4)) < 0;
                (frame, child) = RequireAppend(frame, positioned ? 0x5BB9B0u : 0x5BBEC0u);
                task = CopyTask(Read(frame, child), 16, frame.State.ReadView(record + 8, 8), 8).WriteWord(12, 0).WriteWord(14, 0);
                if (positioned)
                {
                    frame = DrawRandom(frame, out ushort x);
                    task = task.WriteWord(16, unchecked((ushort)(task.ReadWord(16) + x % 1000 - 500)));
                    frame = DrawRandom(frame, out ushort z);
                    task = task.WriteWord(20, unchecked((ushort)(task.ReadWord(20) + z % 1000 - 500)));
                }
                frame = Write(frame, child, task.WriteWord(24, unchecked((ushort)(index + 1))));
                frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(index + 1))));
            }
        }
        if (age == 2)
        {
            (frame, uint child) = RequireAppend(frame, 0x5BB6F0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleChainGun190Contacts506BA0(Parameters(frame).HeaderIndex7C));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleChainGun190Sound501330(resources.Sound, 0, 128));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, Signed(Read(frame, address), 12) > 36 ? 2 : 0);
    }
}
