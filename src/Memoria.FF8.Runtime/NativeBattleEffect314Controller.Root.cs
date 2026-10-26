using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Step AdvanceRoot662970(NativeBattleEffect314Frame frame, uint address, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint work = frame.State.ReadDword(WorkGlobal);
        bool toggle = root.ReadDword(0x10) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(work + (uint)(toggle ? FirstPacketOffset : SecondPacketOffset))).WriteDword(AmplitudeGlobal, 0) };
        frame = Write(frame, address, root.WriteDword(0x10, toggle ? 0u : 1u));
        if (root.ReadWord(0xC) == 1 && root.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(0xF, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect314Music47E3C0(0xF0));
            frame = InitializeChildBanks(frame);
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect314 exhausted its mandatory main task.");
            }
            frame = ClearDwords6DA380(frame, main + 0xC, 0x1307);
            frame = inputs.Execute(frame, new NativeBattleEffect314Vibration4A29A0(VibrationAddress, 1, 128, main + 0x10));
            uint model = frame.State.ReadDword(CasterGlobal);
            NativeBattleGraphicsModel caster = inputs.ReadModel(frame, model);
            NativeBattleGraphicsModel companion = inputs.ReadModel(frame, model + 156);
            NativeBattleModulePoolTask task = Read(frame, main).WriteDword(0x14, caster.Color44 ?? throw new NotSupportedException("Effect314 requires its current caster Color44.")).WriteDword(0x18, companion.Color44 ?? throw new NotSupportedException("Effect314 requires its current companion Color44.")).WriteDword(0x1C, companion.Color40 ?? throw new NotSupportedException("Effect314 requires its current companion Color40.")).WriteDword(0xF20, main + SparkOffset).WriteDword(0x4724, main + FragmentOffset);
            frame = Write(frame, main, task);
            frame = inputs.Execute(frame, new NativeBattleEffect314InitializeIo534110(work + 0x56A8));
            for (int index = 0; index < 64; index++)
            {
                short Angle()
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort value) };
                    return unchecked((short)((value & 0x1FF) - 0x100));
                }
                NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(Angle(), Angle(), Angle()));
                frame = frame with { Random = frame.Random.Draw(out ushort scaleRandom) };
                int scale = 0xF00 - (scaleRandom & 0x1FF);
                NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale);
                frame = WriteBytes(frame, main + 0x4728u + (uint)(index * 20), PackedArena(NativeBattlePackedMatrix32.FromMatrix(scaled, null)), 18);
            }
        }
        root = Read(frame, address);
        short age = unchecked((short)root.ReadWord(0xC));
        if (root.ReadByte(0xF) != 0 && age < 0xE4)
        {
            (frame, _) = AdvancePool(frame, 1, resources, inputs);
            frame = NativeBattleEffect314Body.DrawCaster662C00(frame, inputs, frame.State.ReadDword(CasterGlobal), work + 0xAA0);
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect314Restore508C90(frame.State.ReadDword(CasterGlobal)));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect314Stage5713E0(unchecked((int)frame.State.ReadDword(AmplitudeGlobal)), 0));
        if (age == 0xE4)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect314QueueIo5341D0(0x168, work + FirstPacketOffset, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect314QueueIo5341D0(0x169, work + FirstPacketOffset, 0));
        }
        if (Read(frame, address).ReadByte(0xF) != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect314AdvanceIo534210());
        }
        if (age > 0xE4)
        {
            NativeBattleEffect314IoStep poll = inputs.PollIo(frame, 1);
            return new(poll.Frame, poll.Busy ? 0 : 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1)))), 0);
    }
}
