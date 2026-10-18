namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97DirectorFrame(NativeBattleModulePoolTask Task, NativeBattleChoco97Context Context);
public sealed record NativeBattleChoco97DirectorStep(NativeBattleChoco97DirectorFrame Frame, bool ReadyToComplete);
public sealed record NativeBattleChoco97DirectorRelease(NativeBattleChoco97DirectorFrame Frame, bool Released);
public abstract record NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97AppendNeutral(uint Source, ushort Delay, ushort Duration, ushort Mode) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97AppendProgram(uint Callback, uint Source, uint ClearBytes, uint Arg5, uint Arg6) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97AppendAux(uint Callback, int Size) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97AppendModel : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97StartVibration : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97LoadModelTim : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97SetFreeze(uint Value) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97StopVibration(short Handle) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco97DirectorInputs(Func<ushort> ReadIoWord166, Func<int, uint> ReadIoDirectory, Func<NativeBattleChoco97DirectorFrame, NativeBattleChoco97DirectorRelease> ReleaseWait, Func<NativeBattleChoco97DirectorFrame, NativeBattleChoco97DirectorOperation, NativeBattleChoco97DirectorFrame> Execute);

public static class NativeBattleChoco97Director
{
    public const uint Callback = 0x72A410;

    public static (uint Primary, uint Secondary) SetFreeze730610(uint primary, uint secondary, uint value)
    {
        if (primary == 1 && value == 0)
        {
            return (0, 0);
        }
        if (primary == 0 && value != 0)
        {
            return (1, 1);
        }
        return (primary, secondary);
    }

    public static NativeBattleChoco97DirectorStep Advance72A410(NativeBattleChoco97DirectorFrame frame, NativeBattleChoco97DirectorInputs inputs)
    {
        return AdvanceFamily(frame, inputs, false);
    }

    internal static NativeBattleChoco97DirectorStep AdvanceFamily(NativeBattleChoco97DirectorFrame frame, NativeBattleChoco97DirectorInputs inputs, bool choco98)
    {
        frame.Context.Validate();
        if (frame.Task.Callback != (choco98 ? NativeBattleChoco98Director.Callback : Callback) || frame.Task.Payload.IsDefault || frame.Task.Payload.Length != 0x48 - 12)
        {
            throw new InvalidDataException("Native Choco director requires its complete original target task.");
        }
        WriteContext(0x46, unchecked((ushort)(Context(0x46) + 1)));
        if (Context(0x40) != Context(0x42))
        {
            WriteContext(0x40, Context(0x42));
            WriteContext(0x46, 0);
            OnMode();
        }
        if (Context(0x42) != Context(0x44))
        {
            WriteContext(0x42, Context(0x44));
        }
        int phase = unchecked((sbyte)frame.Task.ReadByte(0x29));
        if (choco98 && phase == 12)
        {
            if (Countdown() <= 0)
            {
                Emit(new NativeBattleChoco97AppendProgram(0x728DA0, inputs.ReadIoDirectory(0x134), 0x7D0, 4, 0));
                NextPhase();
            }
        }
        else
        {
            switch (choco98 && phase >= 13 ? phase - 1 : phase)
            {
                case 0:
                    Emit(new NativeBattleChoco97StartVibration());
                    Emit(new NativeBattleChoco97AppendNeutral(choco98 ? 0x167D934u : 0x168C654u, 18, 45, 2));
                    Emit(new NativeBattleChoco97AppendProgram(choco98 ? 0x722460u : 0x72A660u, choco98 ? 0x1680B9Cu : 0x168F8BCu, 0x4AC, 0, 0));
                    Emit(new NativeBattleChoco97AppendAux(choco98 ? 0x7276F0u : NativeBattleChoco97CameraTask.Callback, 0x70));
                    Emit(new NativeBattleChoco97LoadModelTim());
                    NextPhase();
                    break;
                case 1:
                case 4:
                case 7:
                    NativeBattleChoco97DirectorRelease release = inputs.ReleaseWait(frame);
                    frame = release.Frame;
                    if (release.Released && (!choco98 || phase != 4 || RequestMode(1)))
                    {
                        NextPhase();
                    }
                    break;
                case 2:
                    if (inputs.ReadIoWord166() == 0)
                    {
                        Emit(new NativeBattleChoco97SetFreeze(0));
                        NextPhase();
                    }
                    else if (unchecked((short)frame.Task.ReadWord(0x24)) >= 27)
                    {
                        Emit(new NativeBattleChoco97SetFreeze(1));
                    }
                    break;
                case 3:
                    if (unchecked((short)frame.Task.ReadWord(0x24)) >= 28)
                    {
                        Emit(new NativeBattleChoco97AppendModel());
                        NextPhase();
                    }
                    break;
                case 5:
                    if (inputs.ReadIoWord166() == 0 && (choco98 || RequestMode(1)))
                    {
                        NextPhase();
                    }
                    break;
                case 6:
                    if (AtLeast(2))
                    {
                        NextPhase();
                    }
                    break;
                case 8:
                    if (inputs.ReadIoWord166() == 0)
                    {
                        NextPhase();
                    }
                    break;
                case 9:
                    if (RequestMode(3))
                    {
                        NextPhase();
                    }
                    break;
                case 10:
                    if (AtLeast(4))
                    {
                        WriteTask(frame.Task.WriteWord(0x44, 16));
                        NextPhase();
                    }
                    break;
                case 11:
                    if (Countdown() <= 0)
                    {
                        Emit(new NativeBattleChoco97AppendProgram(choco98 ? 0x728C60u : 0x731100u, inputs.ReadIoDirectory(0x130), choco98 ? 0x494u : 0x290u, 3, 0));
                        if (choco98)
                        {
                            WriteTask(frame.Task.WriteWord(0x44, 24));
                        }
                        NextPhase();
                    }
                    break;
                case 12:
                    if (RequestMode(5))
                    {
                        WriteTask(frame.Task.WriteWord(0x44, choco98 ? (ushort)44 : (ushort)32));
                        NextPhase();
                    }
                    break;
                case 13:
                    if (Countdown() <= (choco98 ? 24 : 18))
                    {
                        WriteContext(0x48, 1);
                        NextPhase();
                    }
                    break;
                case 14:
                    if (Countdown() <= (choco98 ? 8 : 16))
                    {
                        Emit(new NativeBattleChoco97AppendAux(choco98 ? 0x7292D0u : 0x731630u, 0x70));
                        NextPhase();
                    }
                    break;
                case 15:
                    if (Countdown() <= 0)
                    {
                        NextPhase();
                    }
                    break;
                case 16:
                    if (RequestMode(6))
                    {
                        Emit(new NativeBattleChoco97StopVibration(unchecked((short)Context(0x52))));
                        WriteTask(frame.Task.WriteByte(0x26, (byte)(frame.Task.ReadByte(0x26) | 1)));
                        NextPhase();
                    }
                    break;
                case 17:
                    break;
                default:
                    throw new NotSupportedException("Native Choco director phase leaves its admitted callback profile.");
            }
        }
        WriteTask(frame.Task.WriteWord(0x24, unchecked((ushort)(frame.Task.ReadWord(0x24) + 1))));
        return new(frame, (frame.Task.ReadByte(0x26) & 1) != 0 && frame.Task.ReadByte(0x28) == 0);

        ushort Context(int offset) => frame.Context.Word(offset);

        void WriteContext(int offset, ushort value)
        {
            frame = frame with { Context = frame.Context.WriteWord(offset, value) };
        }

        void WriteTask(NativeBattleModulePoolTask task)
        {
            frame = frame with { Task = task };
        }

        void NextPhase()
        {
            WriteTask(frame.Task.WriteByte(0x29, unchecked((byte)(frame.Task.ReadByte(0x29) + 1))));
        }

        short Countdown()
        {
            ushort value = unchecked((ushort)(frame.Task.ReadWord(0x44) - 1));
            WriteTask(frame.Task.WriteWord(0x44, value));
            return unchecked((short)value);
        }

        bool AtLeast(short mode) => NativeBattleChoco97CameraTask.ModeAtLeast730E80(Context(0x40), mode);

        bool RequestMode(short mode)
        {
            (ushort requested, bool accepted) = NativeBattleChoco97CameraTask.RequestMode730EC0(Context(0x40), Context(0x44), mode);
            if (accepted)
            {
                WriteContext(0x44, requested);
            }
            return accepted;
        }

        void Emit(NativeBattleChoco97DirectorOperation operation)
        {
            frame = inputs.Execute(frame, operation);
        }

        void OnMode()
        {
            switch (unchecked((short)Context(0x40)))
            {
                case 2:
                    Emit(new NativeBattleChoco97AppendProgram(choco98 ? 0x7294C0u : 0x731840u, inputs.ReadIoDirectory(0x128), 0x44C, 1, 0));
                    Emit(new NativeBattleChoco97AppendAux(choco98 ? 0x7296D0u : 0x731A50u, 0x70));
                    break;
                case 4:
                    Emit(new NativeBattleChoco97AppendProgram(choco98 ? 0x7295D0u : 0x731950u, inputs.ReadIoDirectory(0x12C), 0x170, 2, 0));
                    if (!choco98)
                    {
                        Emit(new NativeBattleChoco97AppendNeutral(inputs.ReadIoDirectory(0x108), 0, 45, 2));
                    }
                    break;
                case 5:
                    Emit(new NativeBattleChoco97AppendAux(choco98 ? 0x7297C0u : 0x731B40u, 0x70));
                    break;
            }
        }
    }
}
