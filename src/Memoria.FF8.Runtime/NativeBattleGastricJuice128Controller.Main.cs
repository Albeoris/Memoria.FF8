using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGastricJuice128Controller
{
    public static (NativeBattleGastricJuice128Frame Frame, int Result) AdvanceMain(NativeBattleGastricJuice128Frame frame, uint address, NativeBattleGastricJuice128Resources resources, NativeBattleGastricJuice128Inputs inputs)
    {
        NativeBattleModulePoolTask target = Read(frame, address);
        if (target.Callback != MainCallback)
        {
            throw new NotSupportedException("Gastric Juice reached an unsupported target callback.");
        }
        int age = Signed(target, 12);
        byte actor = checked((byte)Actor(target.ReadDword(16)));
        if (age >= 21)
        {
            frame = inputs.Execute(frame, new NativeBattleGastricJuice128AdvanceAct(address));
        }
        int bodyAge = age - 29;
        if ((uint)bodyAge < 40)
        {
            frame = inputs.Execute(frame, new NativeBattleGastricJuice128ActorFlags(actor, 0, 4));
            short fraction = unchecked((short)NativeBattleCameraMath.Sine((bodyAge << 11) / 40));
            frame = inputs.Execute(frame, new NativeBattleGastricJuice128DrawBody132(actor, fraction) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(128, Read(frame, address)) with { ResourceGeneration = 0 } });
        }
        else
        {
            frame = inputs.Execute(frame, new NativeBattleGastricJuice128ActorFlags(actor, 4, 0));
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleGastricJuice128Sound(resources.Sound, 128));
        }
        if (age == 14)
        {
            for (int angle = 4095; angle != 0; angle -= 1365)
            {
                NativeBattleGastricJuice128PointStep mouth = inputs.ReadBone502170(frame, Actor(frame.State.ReadDword(CasterGlobal)), 3, 4095);
                frame = mouth.Frame;
                NativeBattleGastricJuice128AnchorStep caster = inputs.ReadAnchor571400(frame, Actor(frame.State.ReadDword(CasterGlobal)));
                frame = caster.Frame;
                frame = RandomBone(frame, actor, inputs, out NativeBattleVector destination);
                (frame, uint head) = StartTrail(frame, mouth.Position, destination, actor, 0, 256, 150, angle);
                if (head != 0)
                {
                    NativeBattleEffectArena parameters = frame.State.ReadView(CurveParametersGlobal, 8);
                    frame = frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(parameters, 0, Arena(frame), Offset(head + 8, 8), 8) } };
                }
            }
        }
        if ((uint)(age - 14) < 2)
        {
            for (int index = 0; index < 8; index++)
            {
                (frame, uint child) = Append(frame, 3, 0x6B8B20);
                if (child == 0)
                {
                    continue;
                }
                NativeBattleGastricJuice128PointStep mouth = inputs.ReadBone502170(frame, Actor(frame.State.ReadDword(CasterGlobal)), 20, 0);
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
                frame = Write(frame, child, particle.WriteWord(0x1A, unchecked((ushort)(-(yRandom & 63)))));
            }
        }
        if ((uint)bodyAge < 20 && (bodyAge & 1) == 0)
        {
            (frame, uint child) = Append(frame, 3, 0x6B8BC0);
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
        if (age == 53)
        {
            frame = inputs.Execute(frame, new NativeBattleGastricJuice128Contact(FirstContact(frame, inputs) + Signed(target, 14)));
        }
        if (age >= 69)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleGastricJuice128Frame RandomBone(NativeBattleGastricJuice128Frame frame, int actor, NativeBattleGastricJuice128Inputs inputs, out NativeBattleVector position)
    {
        frame = DrawRandom(frame, out ushort random);
        int count = inputs.ReadBoneCount(actor);
        if ((uint)count > 255 || count == 0)
        {
            throw new InvalidDataException("Gastric Juice requires its current native BYTE-sized skeleton count.");
        }
        short selector = unchecked((short)(random * count >> 15));
        NativeBattleGastricJuice128PointStep point = inputs.ReadBone502170(frame, actor, selector, 0);
        position = point.Position;
        return point.Frame;
    }

    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleVector point) => task.WriteWord(0x10, unchecked((ushort)point.X)).WriteWord(0x12, unchecked((ushort)point.Y)).WriteWord(0x14, unchecked((ushort)point.Z));
}
