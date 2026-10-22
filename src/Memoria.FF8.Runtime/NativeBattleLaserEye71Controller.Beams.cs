namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLaserEye71Controller
{
    private static NativeBattleLaserEye71TaskStep AdvanceBeam(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71Inputs inputs)
    {
        frame = inputs.PrepareBeam148(frame, address);
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = CopyPacked(task, 0x74, 0x1C, 8).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x46, 2048).WriteDword(0x30, 1024).WriteDword(0x34, 1024).WriteDword(0x38, 4096).WriteDword(0x4C, 0x158F688);
                frame = NextPhase(Write(frame, address, task), address);
                break;
            case 1:
                task = CopyPacked(task, 0x74, 0x1C, 8).WriteWord(0x46, 2048);
                if (Signed(task, 0x24) >= 4)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = Write(frame, address, task);
                break;
            case 2:
                short growing = unchecked((short)(task.ReadWord(0x8C) + 1024));
                task = CopyPacked(task, 0x74, 0x1C, 8).WriteWord(0x46, 2048);
                if (growing >= 4096)
                {
                    growing = 4096;
                    task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = Write(frame, address, task.WriteWord(0x8C, unchecked((ushort)growing)));
                break;
            case 3:
                task = CopyPacked(task, 0x74, 0x1C, 8).WriteWord(0x46, 2048);
                if (Signed(task, 0x24) >= 14)
                {
                    task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = Write(frame, address, task);
                break;
            case 4:
                short shrinking = unchecked((short)(task.ReadWord(0x8C) - 2048));
                task = CopyPacked(task, 0x7C, 0x1C, 8).WriteWord(0x46, 0);
                if (shrinking <= 0)
                {
                    shrinking = 0;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = Write(frame, address, task.WriteWord(0x8C, unchecked((ushort)shrinking)));
                break;
            case 5:
                break;
            default:
                throw new InvalidDataException("Laser Eye beam phase leaves its original six-entry table.");
        }
        frame = inputs.DrawBeam148(frame, address);
        frame = inputs.EmitBeamParticles(frame, address);
        return Finish(frame, address);
    }

    private static NativeBattleModulePoolTask CopyPacked(NativeBattleModulePoolTask task, int source, int destination, int length)
    {
        byte[] bytes = task.Payload.AsSpan().Slice(source - 12, length).ToArray();
        var unknown = task.UnknownPayloadRanges;
        task = task.WriteBytes(destination, bytes);
        if (unknown is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion region in ranges)
            {
                int first = Math.Max(source, region.Offset);
                int end = Math.Min(source + length, region.Offset + region.Length);
                if (end > first)
                {
                    task = task.MarkPayloadUnknown(destination + first - source, end - first);
                }
            }
        }
        return task;
    }
}
