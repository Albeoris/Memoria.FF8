using static Memoria.FF8.Runtime.NativeBattleAcid317Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid317Controller
{
    private static (NativeBattleAcid317Frame Frame, int Result) Target(NativeBattleAcid317Frame frame, uint address, NativeBattleAcid317Resources resources, NativeBattleAcid317Inputs inputs)
    {
        NativeBattleModulePoolTask target = Read(frame, address);
        if (target.Callback != TargetCallback)
        {
            throw new NotSupportedException("Acid reached an unsupported target callback.");
        }
        int age = Signed(target, 12);
        byte actor = checked((byte)Actor(target.ReadDword(16)));
        if (age >= 25)
        {
            frame = inputs.Execute(frame, new NativeBattleAcid317AdvanceAct(address));
        }
        int bodyAge = age - 33;
        if ((uint)bodyAge < 40)
        {
            frame = inputs.Execute(frame, new NativeBattleAcid317ActorFlags(actor, 0, 4));
            short fraction = unchecked((short)NativeBattleCameraMath.Sine((bodyAge << 11) / 40));
            frame = inputs.Execute(frame, new NativeBattleAcid317DrawBody132(actor, fraction) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(317, target, 1) : null });
        }
        else
        {
            frame = inputs.Execute(frame, new NativeBattleAcid317ActorFlags(actor, 4, 0));
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleAcid317Sound(resources.Sound, 128));
        }
        if (age == 18)
        {
            for (int angle = 4095; angle != 0; angle -= 1365)
            {
                NativeBattleAcid317PointStep mouth = inputs.ReadMouth(frame);
                frame = mouth.Frame;
                NativeBattleAcid317AnchorStep caster = inputs.ReadAnchor571400(frame, Actor(frame.State.ReadDword(CasterGlobal)));
                frame = caster.Frame;
                frame = RandomBone(frame, actor, inputs, out NativeBattleVector destination);
                frame = StartTrail(frame, mouth.Position, destination, actor, 0, 160, 150, angle, inputs.CapturePresentation);
            }
        }
        if ((uint)(age - 18) < 2)
        {
            for (int index = 0; index < 8; index++)
            {
                (frame, uint child) = Append(frame, 3, 0x660830);
                if (child == 0)
                {
                    continue;
                }
                NativeBattleAcid317PointStep mouth = inputs.ReadMouth(frame);
                frame = mouth.Frame;
                NativeBattleModulePoolTask particle = Position(Read(frame, child).WriteDword(12, 0), mouth.Position);
                frame = DrawRandom(frame, out ushort size);
                particle = particle.WriteWord(0x16, (ushort)((size & 1023) + 1024));
                frame = DrawRandom(frame, out ushort angleRandom);
                short angle = unchecked((short)((angleRandom & 1023) + target.ReadWord(0x14) - 512));
                frame = DrawRandom(frame, out ushort speedRandom);
                int speed = (speedRandom & 63) + 64;
                particle = particle.WriteWord(0x18, unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12))).WriteWord(0x1C, unchecked((ushort)(unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12)));
                frame = DrawRandom(frame, out ushort yRandom);
                frame = Write(frame, child, particle.WriteWord(0x1A, unchecked((ushort)(-32 - (yRandom & 63)))));
            }
        }
        if ((uint)bodyAge < 20 && (bodyAge & 1) == 0)
        {
            (frame, uint child) = Append(frame, 3, 0x6608D0);
            if (child != 0)
            {
                frame = RandomBone(frame, actor, inputs, out NativeBattleVector position);
                NativeBattleModulePoolTask particle = Position(Read(frame, child).WriteDword(12, 0), position);
                frame = DrawRandom(frame, out ushort sizeRandom);
                int scale = (sizeRandom & 2047) + 2048;
                if (bodyAge >= 10)
                {
                    scale = (30 - bodyAge) * scale / 20;
                }
                particle = particle.WriteWord(0x16, unchecked((ushort)scale));
                frame = DrawRandom(frame, out ushort angleRandom);
                short angle = unchecked((short)((angleRandom & 1023) + target.ReadWord(0x14) - 512));
                frame = DrawRandom(frame, out ushort speedRandom);
                int speed = speedRandom & 31;
                particle = particle.WriteWord(0x18, unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12))).WriteWord(0x1C, unchecked((ushort)(unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12)));
                frame = DrawRandom(frame, out ushort yRandom);
                frame = Write(frame, child, particle.WriteWord(0x1A, unchecked((ushort)(-16 - (yRandom & 31)))));
            }
        }
        if (age == 57)
        {
            frame = inputs.Execute(frame, new NativeBattleAcid317Contact(FirstContact(frame, inputs) + Signed(target, 14)));
        }
        if (age >= 73)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleAcid317Frame RandomBone(NativeBattleAcid317Frame frame, int actor, NativeBattleAcid317Inputs inputs, out NativeBattleVector position)
    {
        frame = DrawRandom(frame, out ushort random);
        int count = inputs.ReadBoneCount(actor);
        if ((uint)count > 255 || count == 0)
        {
            throw new InvalidDataException("Acid requires its current native BYTE-sized skeleton count.");
        }
        short selector = unchecked((short)(random * count >> 15));
        NativeBattleAcid317PointStep point = inputs.ReadBone502170(frame, actor, selector, 0);
        position = point.Position;
        return point.Frame;
    }

    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleVector point) => task.WriteWord(0x10, unchecked((ushort)point.X)).WriteWord(0x12, unchecked((ushort)point.Y)).WriteWord(0x14, unchecked((ushort)point.Z));
}
