using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect76Controller
{
    public static NativeBattleEffect76Step AdvanceMain61FCD0(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Resources resources, NativeBattleEffect76Inputs inputs)
    {
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), ReadPacked(frame, CapturedWorldAddress).ReadMatrix());
        frame = StoreMatrix(frame, ViewAddress, composed);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        if (age == 0)
        {
            frame = BeginCamera63E960(frame, resources);
            frame = inputs.Execute(frame, new NativeBattleEffect76ForceCamera50A730());
            frame = inputs.Execute(frame, new NativeBattleEffect76Vibration4A29A0(VibrationAddress, 1, 128, VibrationGlobal));
        }
        if (age == 40)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76Sequence505C00(actor, 15));
            frame = inputs.Execute(frame, new NativeBattleEffect76ModelFlags(actor, 0xFFFB, 0, true));
            frame = Create61FC40(frame, 0x6201A0).Frame;
        }
        if (age == 50)
        {
            frame = Create61FC40(frame, 0x620530).Frame;
        }
        if (age >= 74 && age < 90)
        {
            int intensity = 4096 + unchecked((34 - (age - 40)) << 12) / 16;
            frame = inputs.Execute(frame, new NativeBattleEffect76Stage5713E0(intensity, 0));
        }
        if (age == 160)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76Sequence505C00(actor, 16));
        }
        if (age >= 90 && age < 180)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76ModelPointY(actor, -55, true));
            if (age == 91)
            {
                frame = Create61FC40(frame, 0x620820).Frame;
            }
            if (age == 150)
            {
                frame = NativeBattleEffect76Lower.Invoke(frame, 0x620BA0, resources, inputs).Frame;
            }
        }
        if (age == 180)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76ModelPointY(actor, unchecked((short)frame.State.ReadWord(SavedPointAddress + 2)), false));
        }
        if (age == 270)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76MusicVolume501B10(0, 127));
            frame = inputs.Execute(frame, new NativeBattleEffect76Text4876F0(actor, 1, 60));
        }
        if (age == 350)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76Text4876F0(actor, 3, 60));
        }
        if (age == 430)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76Text4876F0(actor, 4, 60));
        }
        if (age == 479)
        {
            frame = NativeBattleEffect76Lower.Invoke(frame, 0x620D60, resources, inputs).Frame;
        }
        if (age == 483)
        {
            frame = NativeBattleEffect76Lower.Invoke(frame, 0x620F20, resources, inputs).Frame;
        }
        if (age == 502)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76Flash5712C0(7, 6, 12, 255));
        }
        if (age == 510)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76Sequence505C00(actor, 17));
        }
        if (age == 511)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76Sound501330(SoundAddress, 0, 128));
        }
        if (age == 515)
        {
            frame = BeginWhite6200A0(frame, true, 95).Frame;
            frame = Create61FC40(frame, 0x622560).Frame;
        }
        if (age == 516)
        {
            frame = Create61FC40(frame, 0x6215E0).Frame;
        }
        if (age == 518)
        {
            frame = NativeBattleEffect76Lower.Invoke(frame, 0x6210A0, resources, inputs).Frame;
            frame = Create61FC40(frame, 0x621250).Frame;
        }
        if (age == 605)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect76 requires its current parameters.");
            int contact = inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect76 requires its current first contact pointer.");
            frame = inputs.Execute(frame, new NativeBattleEffect76Contact506690(contact));
        }
        if (age == 662)
        {
            int handle = unchecked((int)frame.State.ReadDword(VibrationGlobal));
            if (inputs.ReadVibrationPlaying4A2900(frame, handle))
            {
                frame = inputs.Execute(frame, new NativeBattleEffect76StopVibration4A2940(handle));
            }
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        if (Signed(Read(frame, address), 12) <= 665)
        {
            return new(frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect76Stage5713E0(0, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect76CaptureCameraRetirement());
        frame = inputs.Execute(frame, new NativeBattleEffect76SetMode4A8480(1));
        frame = inputs.Execute(frame, new NativeBattleEffect76GraphicsFlags(0xFFFFFBFF, 0));
        return new(frame, 2);
    }
}
