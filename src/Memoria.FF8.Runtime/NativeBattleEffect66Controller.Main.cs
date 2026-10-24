using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Controller
{
    public static NativeBattleEffect66Step AdvanceMain6226E0(NativeBattleEffect66Frame frame, uint address, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), ReadPacked(frame, CapturedWorldAddress).ReadMatrix());
        frame = StoreMatrix(frame, ViewAddress, matrix);
        uint flags = inputs.ReadFlags(frame);
        if ((flags & 0x201) != 0)
        {
            if ((flags & 1) != 0)
            {
                return new(frame, 0);
            }
            NativeBattleEffect66Step step = inputs.ReadStep508500(frame);
            frame = step.Frame;
            if (step.Result < 0)
            {
                return new(frame, 0);
            }
        }
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        int age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            frame = BeginCamera63E960(frame, resources);
            frame = inputs.Execute(frame, new NativeBattleEffect66ForceCamera50A730());
            frame = inputs.Execute(frame, new NativeBattleEffect66ModelMask7C(actor, 3));
            frame = inputs.Execute(frame, new NativeBattleEffect66Vibration4A29A0(VibrationAddress, 1, 128, VibrationGlobal));
            frame = inputs.Execute(frame, new NativeBattleEffect66SetMode4A8480(0));
            frame = inputs.Execute(frame, new NativeBattleEffect66Flags(uint.MaxValue, 0x400));
        }
        switch (age)
        {
            case 0:
                frame = Sequence(frame, actor, 18, inputs);
                frame = BirthWhite622DC0(frame, 0, 53);
                frame = inputs.Execute(frame, new NativeBattleEffect66MusicFade46BBC0(0, 52, 0));
                break;
            case 1:
                frame = Sound(frame, 0xDEA344, inputs);
                break;
            case 53:
                frame = Sequence(frame, actor, 19, inputs);
                frame = CaptureMidpoint(frame, actor, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect66MusicStop46B800(0));
                break;
            case 54:
                frame = inputs.Execute(frame, new NativeBattleEffect66UploadTim505E30(frame.State.ReadDword(InitializerGlobal)));
                break;
            case 55:
                frame = inputs.Execute(frame, new NativeBattleEffect66UploadTim505E30(EmbeddedTimAddress));
                break;
            case 56:
                frame = Spawn622CD0(frame, 0x622EB0).Frame;
                frame = Spawn622CD0(frame, 0x624AE0).Frame;
                break;
            case 181:
                frame = Sequence(frame, actor, 20, inputs);
                break;
            case 191:
                frame = inputs.Execute(frame, new NativeBattleEffect66Text4876F0(actor, 5, 40));
                break;
            case 217:
                frame = Sequence(frame, actor, 21, inputs);
                frame = BirthWhite622DC0(frame, 1, 172);
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x627B90, resources, inputs);
                break;
            case 218:
                frame = Sound(frame, 0xDEA348, inputs);
                break;
            case 222:
                frame = Sequence(frame, actor, 22, inputs);
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x625790, resources, inputs);
                break;
            case 242:
                frame = Spawn622CD0(frame, 0x626690).Frame;
                break;
            case 369:
                frame = inputs.Execute(frame, new NativeBattleEffect66Flash5712C0(8, 11, 15, 255));
                break;
            case 381:
                frame = Sound(frame, 0xDEA34C, inputs);
                break;
            case 382:
                frame = Spawn622CD0(frame, 0x627CF0).Frame;
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x626970, resources, inputs);
                break;
            case 384:
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x626B00, resources, inputs);
                break;
            case 392:
                frame = Spawn622CD0(frame, 0x626F80).Frame;
                frame = Spawn622CD0(frame, 0x627210).Frame;
                break;
            case 442:
                frame = Sound(frame, 0xDEA354, inputs);
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x626DC0, resources, inputs);
                break;
            case 478:
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x6274D0, resources, inputs);
                break;
            case 484:
                frame = Sound(frame, 0xDEA358, inputs);
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x6276F0, resources, inputs);
                break;
            case 494:
                frame = BirthEnvelope622CF0(frame, 7, 20, 32, 255);
                break;
            case 522:
                frame = Sound(frame, 0xDEA350, inputs);
                break;
            case 524:
                frame = NativeBattleEffect66Lower.Invoke(frame, 0x627840, resources, inputs);
                break;
            case 532:
                frame = BirthWhite622DC0(frame, 0, 300);
                break;
            case 802:
                frame = inputs.Execute(frame, new NativeBattleEffect66SoundGain46BEC0(120, 0));
                break;
        }
        if (age <= 32)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect66Stage5713E0(age << 6, 0));
        }
        else if (age is >= 641 and < 657)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect66Stage5713E0((657 - age) << 7, 0));
        }
        if (age == 25)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int header = (frame.Effect.Parameters ?? throw new NotSupportedException("Effect66 requires its current contact header.")).HeaderIndex7C;
            frame = inputs.Execute(frame, new NativeBattleEffect66Contact506690(packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect66 requires its current contact pointer.")));
        }
        if (age == 829)
        {
            int handle = unchecked((int)frame.State.ReadDword(VibrationGlobal));
            if (inputs.ReadVibrationPlaying4A2900(frame, handle))
            {
                frame = inputs.Execute(frame, new NativeBattleEffect66StopVibration4A2940(handle));
            }
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) <= 832)
        {
            return new(frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect66Stage5713E0(4096, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect66CaptureCameraRetirement());
        frame = inputs.Execute(frame, new NativeBattleEffect66StopSound46B450(-1, 0));
        frame = Sequence(frame, actor, 1, inputs);
        return new(frame, 2);
    }

    private static NativeBattleEffect66Frame CaptureMidpoint(NativeBattleEffect66Frame frame, int actor, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleEffect66PointStep first = inputs.ReadBone502170(frame, actor, 1, 2048, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        NativeBattleEffect66PointStep second = inputs.ReadBone502170(first.Frame, actor, 10, 2048, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        NativeBattleVector a = Vector(first.Point8);
        NativeBattleVector b = Vector(second.Point8);
        frame = second.Frame;
        frame = WriteBytes(frame, CenterAddress, PointView(new(unchecked((short)((a.X + b.X) / 2)), unchecked((short)((a.Y + b.Y) / 2)), unchecked((short)((a.Z + b.Z) / 2)))), 6);
        short scale = (inputs.ReadModel(frame, actor).Transform ?? throw new NotSupportedException("Effect66 requires its current caster transform WORD26.")).Scale38;
        return frame with { State = frame.State.WriteWord(CenterAddress + 6, unchecked((ushort)(unchecked(scale * 1450) >> 13))) };
    }

    private static NativeBattleEffect66Frame Sequence(NativeBattleEffect66Frame frame, int actor, byte sequence, NativeBattleEffect66Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect66Sequence505C00(actor, sequence));
    private static NativeBattleEffect66Frame Sound(NativeBattleEffect66Frame frame, uint source, NativeBattleEffect66Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect66Sound501330(source, 1, 128));
}
