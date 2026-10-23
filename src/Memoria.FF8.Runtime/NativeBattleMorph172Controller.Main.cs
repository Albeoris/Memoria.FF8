namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMorph172Controller
{
    public static (NativeBattleMorph172Frame Frame, int Result) AdvanceMain(NativeBattleMorph172Frame frame, uint address, NativeBattleMorph172Resources resources, NativeBattleMorph172Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            int caster = checked((int)frame.State.ReadDword(0x239D4F4));
            NativeBattleMorph172PointStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = anchor.Frame;
            frame = Write(frame, address, CopyTask(Read(frame, address), 16, anchor.Point8, 8));
        }
        if (age == 1)
        {
            NativeBattleEffectArena point = TaskView(Read(frame, address), 16, 8);
            foreach ((uint callback, ushort radius) in new (uint, ushort)[] { (0x5D9F20, 0xC00), (0x5DA0F0, 0x1600) })
            {
                (frame, uint child) = RequireAppend(frame, callback);
                frame = Write(frame, child, CopyTask(Read(frame, child), 16, point, 8).WriteWord(12, 0).WriteWord(24, 0).WriteWord(26, 0xFFF6).WriteWord(28, radius));
            }
            frame = InitializeRadii5D9EE0(frame, resources);
            (frame, uint particles) = RequireAppend(frame, 0x5DA2C0);
            frame = Write(frame, particles, Read(frame, particles).WriteWord(12, 0));
        }
        if (age == 8)
        {
            (frame, uint body) = RequireAppend(frame, 0x5DA520);
            frame = Write(frame, body, CopyTask(Read(frame, body), 16, TaskView(Read(frame, address), 16, 8), 8).WriteWord(12, 0));
        }
        if (age == 1)
        {
            int caster = checked((int)frame.State.ReadDword(0x239D4F4));
            NativeBattleMorph172PointStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = inputs.Execute(anchor.Frame, new NativeBattleMorph172Sound5013A0(resources.Sound, 0, anchor.Point8));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, Signed(Read(frame, address), 12) > 60 ? 2 : 0);
    }
}
