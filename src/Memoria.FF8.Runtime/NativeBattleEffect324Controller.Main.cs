using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect324Controller
{
    public static NativeBattleEffect324Step AdvanceMain596C70(NativeBattleEffect324Frame frame, uint address, NativeBattleEffect324Inputs inputs)
    {
        uint model = ModelAddress(checked((int)frame.State.ReadDword(CasterGlobal)));
        NativeBattleEffect324PointStep point = inputs.Point502170(frame, model, 0xF0, 4096, frame.State.ReadView(PointAddress, 8));
        frame = point.Frame with { State = point.Frame.State.WriteView(PointAddress, point.Output8, 8) };
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect324VibrationStart4A29A0(VibrationAddress, 1, 128, VibrationGlobal));
        }
        if (age == 12)
        {
            frame = BirthTask(frame, ChildDescriptor, 0x596E30, out _);
            frame = Write(frame, address, Read(frame, address).WriteWord(14, 0));
            frame = BirthTask(frame, ChildDescriptor, 0x597130, out _);
        }
        if (age == 8)
        {
            frame = BirthTask(frame, ChildDescriptor, 0x597410, out uint child);
            frame = Write(frame, child, Read(frame, child).WriteWord(14, unchecked((ushort)frame.State.ReadDword(CasterGlobal))));
        }
        if (age == 18)
        {
            frame = BirthTask(frame, ChildDescriptor, 0x597520, out _);
        }
        if (age >= 125 && age <= 135 && age % 2 == 1)
        {
            frame = BirthTask(frame, ChildDescriptor, 0x597870, out uint child);
            ushort phase = Read(frame, address).ReadWord(14);
            frame = Write(frame, child, Read(frame, child).WriteWord(14, (ushort)(phase & 1)).WriteWord(28, 2048));
            frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(phase + 1))));
        }
        if (age == 130)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect324Contact506690(FirstContact(frame, inputs.ReadPackets(frame))));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect324Sound5013A0(SoundAddress, 0, frame.State.ReadView(PointAddress, 8)));
        }
        if (age == 197)
        {
            NativeBattleEffect324BoolStep vibration = inputs.VibrationQuery4A2900(frame, unchecked((int)frame.State.ReadDword(VibrationGlobal)));
            frame = vibration.Frame;
            if (vibration.Result)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect324VibrationStop4A2940(unchecked((int)frame.State.ReadDword(VibrationGlobal))));
            }
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, Signed(Read(frame, address), 12) > 200 ? 2 : 0);
    }
}
