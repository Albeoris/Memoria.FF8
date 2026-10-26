namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect30Controller
{
    private static NativeBattleEffect30TaskStep AdvanceStage(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool entering = task.Callback == NativeBattleEffect30Profile.FadeInCallback;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = Write(frame, address, task.WriteWord(0x1C, entering ? (ushort)0 : (ushort)1024));
                if (entering)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect30Stage(0, true));
                }
                frame = NextPhase(frame, address);
                break;
            case 1:
                short depth = unchecked((short)(Signed(task, 0x1C) + (entering ? 256 : -256)));
                bool complete = entering ? depth >= 1024 : depth <= 0;
                if (complete)
                {
                    depth = entering ? (short)1024 : (short)0;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)depth)));
                frame = inputs.Execute(frame, new NativeBattleEffect30Stage(unchecked((short)depth), false));
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect30 stage phase leaves its original three-entry table.");
        }
        return Finish(frame, address);
    }
}
