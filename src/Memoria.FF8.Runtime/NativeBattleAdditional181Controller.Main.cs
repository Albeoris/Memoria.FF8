namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional181Controller
{
    public static (NativeBattleAdditional181Frame Frame, int Result) AdvanceMain(NativeBattleAdditional181Frame frame, uint address, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        int caster = checked((int)frame.State.ReadDword(0x2307D84));
        if (age == 0)
        {
            NativeBattleAdditional181PointStep bone = inputs.ReadBone502170(frame, caster, 0xF0, 2560);
            frame = bone.Frame;
            frame = Write(frame, address, CopyTask(Read(frame, address), 16, bone.Point8, 6));
        }
        if (age == 1)
        {
            (frame, uint child) = RequireAppend(frame, 0x5CB9B0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age is 4 or 5)
        {
            frame = SpawnEarlyParticles(frame, address, caster, inputs);
        }
        if (age == 16)
        {
            frame = SpawnRings(frame, address);
        }
        if (age == 14)
        {
            frame = inputs.Execute(frame, new NativeBattleAdditional181Flash5712C0(0, 1, 4, 255));
        }
        if (age == 15)
        {
            NativeBattleAdditional181PointStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = anchor.Frame;
            frame = Write(frame, address, CopyTask(Read(frame, address), 16, anchor.Point8, 8));
            (frame, uint child) = RequireAppend(frame, 0x5CC160);
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, TaskView(Read(frame, address), 16, 8), 8).WriteWord(12, 0);
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, child, task.WriteWord(24, (ushort)(angle & 0xFFF)).WriteWord(30, 0x580).WriteWord(28, 0x580));
            (frame, child) = RequireAppend(frame, 0x5CC2A0);
            frame = Write(frame, child, CopyTask(Read(frame, child), 16, TaskView(Read(frame, address), 16, 8), 8).WriteWord(12, 0));
        }
        if (age == 16)
        {
            (frame, uint child) = RequireAppend(frame, 0x5CC560);
            frame = Write(frame, child, CopyTask(Read(frame, child), 16, TaskView(Read(frame, address), 16, 8), 8).WriteWord(12, 0));
        }
        if (age == 38)
        {
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Additional requires its captured first physical contact pointer.");
            frame = inputs.Execute(frame, new NativeBattleAdditional181Contact506690(first));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleAdditional181Sound501330(resources.Sound, 0, 128));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, Signed(Read(frame, address), 12) > 50 ? 2 : 0);
    }

    private static NativeBattleAdditional181Frame SpawnEarlyParticles(NativeBattleAdditional181Frame frame, uint address, int caster, NativeBattleAdditional181Inputs inputs)
    {
        NativeBattleAdditional181PointStep bone = inputs.ReadBone502170(frame, caster, 0xF0, 2560);
        frame = bone.Frame;
        NativeBattleVector position = (inputs.ReadModel(frame, caster).Transform ?? throw new NotSupportedException("Additional requires its current caster model position.")).Position28;
        short x = BitConverter.ToInt16(bone.Point8.Read(0, 2));
        short z = BitConverter.ToInt16(bone.Point8.Read(4, 2));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(position.X - x, 0, position.Z - z);
        int count = Signed(Read(frame, address), 14);
        for (int index = 0; index < count; index++)
        {
            (frame, uint child) = RequireAppend(frame, 0x5CC720);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(16, unchecked((ushort)x));
            frame = DrawRandom(frame, out ushort y);
            task = task.WriteWord(18, unchecked((ushort)(Signed(Read(frame, address), 18) + y % 10 - 5))).WriteWord(20, unchecked((ushort)z));
            frame = DrawRandom(frame, out ushort speedRandom);
            int speed = speedRandom % 75 + 25;
            task = task.WriteWord(24, unchecked((ushort)(speed * direction.X >> 12))).WriteWord(26, unchecked((ushort)(speed * direction.Z >> 12)));
            frame = DrawRandom(frame, out ushort scale);
            frame = Write(frame, child, task.WriteWord(28, (ushort)((scale & 0xFF) + 0x800)));
        }
        return Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(count - 1))));
    }

    private static NativeBattleAdditional181Frame SpawnRings(NativeBattleAdditional181Frame frame, uint address)
    {
        NativeBattleEffectArena point = TaskView(Read(frame, address), 16, 8);
        (frame, uint child) = RequireAppend(frame, 0x5CC000);
        NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, point, 8).WriteWord(12, 0).WriteWord(14, 0);
        frame = DrawRandom(frame, out ushort angle);
        task = task.WriteWord(24, (ushort)(angle & 0xFFF));
        frame = DrawRandom(frame, out ushort speed);
        frame = Write(frame, child, task.WriteWord(30, 0x1CC).WriteWord(28, 0x1CC).WriteWord(26, (ushort)(speed % 110 + 60)));
        (frame, child) = RequireAppend(frame, 0x5CC000);
        task = CopyTask(Read(frame, child), 16, point, 8).WriteWord(12, 0).WriteWord(14, 0);
        frame = DrawRandom(frame, out angle);
        task = task.WriteWord(24, (ushort)(angle & 0xFFF));
        frame = DrawRandom(frame, out speed);
        return Write(frame, child, task.WriteWord(26, unchecked((ushort)(-80 - speed % 130))).WriteWord(30, 0x366).WriteWord(28, 0x366));
    }
}
