using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect321Controller
{
    public static NativeBattleEffect321Step AdvanceMain597AB0(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            uint caster = ModelAddress(checked((int)frame.State.ReadDword(CasterGlobal)));
            NativeBattleEffect321PointStep point = inputs.Anchor571400(frame, caster, TaskView(Read(frame, address), 16, 8));
            frame = point.Frame;
            short yaw = BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, caster).Read(14, 2));
            NativeBattleVector anchor = Vector(point.Output8);
            NativeBattleEffectArena output = PutPoint(point.Output8, 0, new(unchecked((short)(anchor.X + (unchecked(-2800 * NativeBattleCameraMath.Sine(yaw)) >> 12))), 0, unchecked((short)(anchor.Z + (unchecked(-2800 * NativeBattleCameraMath.Cosine(yaw)) >> 12)))));
            frame = Write(frame, address, CopyTask(Read(frame, address), 16, output, 8));
            frame = inputs.Execute(frame, new NativeBattleEffect321VibrationStart4A29A0(VibrationAddress, 1, 128, VibrationGlobal));
        }
        if (age == 24)
        {
            frame = BirthChildren(frame, address, inputs);
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect321Sound501330(FirstSoundAddress, 0, 128));
        }
        if (age == 92)
        {
            ushort mask = frame.Effect.Parameters?.TargetMask7A ?? throw new NotSupportedException("Effect321 requires its actual target mask.");
            NativeBattleEffect321PointStep point = inputs.TargetCenter5020A0(frame, mask, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = inputs.Execute(point.Frame, new NativeBattleEffect321PositionalSound5013A0(SecondSoundAddress, 0, point.Output8));
        }
        if (age == 97)
        {
            int handle = unchecked((int)frame.State.ReadDword(VibrationGlobal));
            NativeBattleEffect321BoolStep active = inputs.VibrationActive4A2900(frame, handle);
            frame = active.Frame;
            if (active.Active)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect321VibrationStop4A2940(handle));
            }
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 100 ? 2 : 0);
    }

    public static NativeBattleEffect321Frame BirthChildren(NativeBattleEffect321Frame frame, uint main, NativeBattleEffect321Inputs inputs)
    {
        NativeBattleEffectArena point = TaskView(Read(frame, main), 16, 8);
        for (int index = 0; index < 3; index++)
        {
            (frame, uint child) = AppendRequired(frame, 0x597E30);
            frame = Write(frame, child, CopyTask(Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(index * 3)).WriteWord(28, 1484).WriteWord(30, 1484), 16, point, 8));
        }
        foreach (uint callback in new uint[] { 0x597F90, 0x598230 })
        {
            (frame, uint child) = AppendRequired(frame, callback);
            frame = Write(frame, child, CopyTask(Read(frame, child).WriteWord(12, 0), 16, point, 8));
        }
        (frame, uint flash) = AppendRequired(frame, 0x598570);
        frame = Write(frame, flash, Read(frame, flash).WriteWord(12, 0));
        for (int index = 0; index < CurrentHeader(frame, inputs.ReadPackets(frame)).ReadByte(16); index++)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int first = CurrentHeader(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Effect321 requires its current contact pointer.");
            int actor = packets.ReadContact(first + index).Primary.ActorIndex;
            (frame, uint target) = AppendRequired(frame, 0x5985C0);
            frame = Write(frame, target, Read(frame, target).WriteWord(12, 0).WriteWord(14, (ushort)actor));
            NativeBattleEffect321PointStep anchor = inputs.Anchor571400(frame, ModelAddress(actor), TaskView(Read(frame, target), 16, 8));
            frame = Write(anchor.Frame, target, CopyTask(Read(anchor.Frame, target), 16, anchor.Output8, 8));
            int turn = Roll(ref frame);
            frame = Write(frame, target, Read(frame, target).WriteWord(24, (ushort)(((turn % 3) + 1) << 12)).WriteWord(26, (ushort)index));
            int altitude = Roll(ref frame);
            frame = Write(frame, target, Read(frame, target).WriteWord(32, unchecked((ushort)(-6000 - altitude % 2500))));
            (frame, uint particle) = AppendRequired(frame, 0x598B90);
            NativeBattleModulePoolTask source = Read(frame, target);
            frame = Write(frame, particle, Read(frame, particle).WriteWord(12, 0).WriteWord(14, 1).WriteWord(16, source.ReadWord(16)).WriteWord(18, 0).WriteWord(20, source.ReadWord(20)));
            int angle = Roll(ref frame);
            frame = Write(frame, particle, Read(frame, particle).WriteWord(24, (ushort)(angle & 4095)));
            int scale = Roll(ref frame);
            frame = Write(frame, particle, Read(frame, particle).WriteWord(28, (ushort)(scale % 640 + 2304)));
            int radius = ((Roll(ref frame) & 2047) + 6144) / 8;
            frame = Write(frame, particle, Read(frame, particle).WriteWord(32, (ushort)radius).WriteWord(34, (ushort)radius));
        }
        return frame;
    }

    private static int Roll(ref NativeBattleEffect321Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort value) };
        return value;
    }
}
