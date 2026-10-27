namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99AppendSecondary(uint Callback, int Size) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco99AppendSmall(uint Callback, uint Pointer34, ushort Value38) : NativeBattleChoco97DirectorOperation;

public static class NativeBattleChoco99Director
{
    public const uint Callback = 0x718750;
    public const uint SecondaryDescriptor = 0x2569338;
    public const uint SmallDescriptor = 0x256C470;

    public static (uint Primary, uint Secondary) SetFreeze71E840(uint primary, uint secondary, uint value) => NativeBattleChoco97Director.SetFreeze730610(primary, secondary, value);

    public static NativeBattleChoco97DirectorStep Advance718750(NativeBattleChoco97DirectorFrame frame, NativeBattleChoco97DirectorInputs inputs)
    {
        frame.Context.Validate();
        if (frame.Task.Callback != Callback || frame.Task.Payload.IsDefault || frame.Task.PayloadLength != 0x48 - 12)
        {
            throw new InvalidDataException("Native Choco99 director requires its complete original 48-byte task.");
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
        switch (unchecked((sbyte)frame.Task.ReadByte(0x29)))
        {
            case 0:
                Emit(new NativeBattleChoco97StartVibration());
                Emit(new NativeBattleChoco97AppendNeutral(0x166A9F0, 18, 45, 2));
                Emit(new NativeBattleChoco97AppendProgram(0x7189B0, 0x166DC58, 0x4AC, 0, 0));
                Emit(new NativeBattleChoco97AppendAux(NativeBattleChoco99CameraTask.Callback, 0xB0));
                Emit(new NativeBattleChoco97LoadModelTim());
                Next();
                break;
            case 1:
            case 4:
            case 7:
                Release();
                break;
            case 2:
                if (inputs.ReadIoWord166() == 0)
                {
                    Emit(new NativeBattleChoco97SetFreeze(0));
                    Next();
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
                    Next();
                }
                break;
            case 5:
                if (inputs.ReadIoWord166() == 0 && Request(1))
                {
                    Next();
                }
                break;
            case 6:
                if (AtLeast(2))
                {
                    Next();
                }
                break;
            case 8:
            case 13:
                if (inputs.ReadIoWord166() == 0)
                {
                    Next();
                }
                break;
            case 9:
                if (Request(3))
                {
                    Next();
                }
                break;
            case 10:
                if (AtLeast(4))
                {
                    WriteTask(frame.Task.WriteWord(0x44, 16));
                    Next();
                }
                break;
            case 11:
                if (Countdown() <= 0)
                {
                    Emit(new NativeBattleChoco97AppendAux(0x71F310, 0xB0));
                    Next();
                }
                break;
            case 12:
                if (AtLeast(5))
                {
                    Release();
                }
                break;
            case 14:
                if (Request(6))
                {
                    Next();
                }
                break;
            case 15:
                if (AtLeast(9))
                {
                    WriteTask(frame.Task.WriteWord(0x44, 32));
                    Next();
                }
                break;
            case 16:
                if (Countdown() <= 0)
                {
                    WriteTask(frame.Task.WriteWord(0x44, 40));
                    WriteContext(0x48, 1);
                    Next();
                }
                break;
            case 17:
                if (Countdown() <= 0)
                {
                    Next();
                }
                break;
            case 18:
                if (Request(11))
                {
                    WriteTask(frame.Task.WriteByte(0x26, (byte)(frame.Task.ReadByte(0x26) | 1)));
                    Next();
                }
                break;
            case 19:
                if (AtLeast(11))
                {
                    Emit(new NativeBattleChoco97StopVibration(unchecked((short)Context(0x52))));
                    WriteTask(frame.Task.WriteByte(0x26, (byte)(frame.Task.ReadByte(0x26) | 1)));
                    Next();
                }
                break;
            case 20:
                break;
            default:
                throw new NotSupportedException("Native Choco99 director phase leaves its twenty-one original phases.");
        }
        WriteTask(frame.Task.WriteWord(0x24, unchecked((ushort)(frame.Task.ReadWord(0x24) + 1))));
        return new(frame, (frame.Task.ReadByte(0x26) & 1) != 0 && frame.Task.ReadByte(0x28) == 0);

        ushort Context(int offset) => frame.Context.Word(offset);
        void WriteContext(int offset, ushort value) => frame = frame with { Context = frame.Context.WriteWord(offset, value) };
        void WriteTask(NativeBattleModulePoolTask task) => frame = frame with { Task = task };
        void Next() => WriteTask(frame.Task.WriteByte(0x29, unchecked((byte)(frame.Task.ReadByte(0x29) + 1))));
        short Countdown()
        {
            ushort value = unchecked((ushort)(frame.Task.ReadWord(0x44) - 1));
            WriteTask(frame.Task.WriteWord(0x44, value));
            return unchecked((short)value);
        }
        bool AtLeast(short mode) => NativeBattleChoco97CameraTask.ModeAtLeast730E80(Context(0x40), mode);
        bool Request(short mode)
        {
            (ushort requested, bool accepted) = NativeBattleChoco97CameraTask.RequestMode730EC0(Context(0x40), Context(0x44), mode);
            if (accepted)
            {
                WriteContext(0x44, requested);
            }
            return accepted;
        }
        void Release()
        {
            NativeBattleChoco97DirectorRelease release = inputs.ReleaseWait(frame);
            frame = release.Frame;
            if (release.Released)
            {
                Next();
            }
        }
        void Emit(NativeBattleChoco97DirectorOperation operation) => frame = inputs.Execute(frame, operation);
        void Program(uint callback, int directory, uint bytes, uint argument) => Emit(new NativeBattleChoco97AppendProgram(callback, inputs.ReadIoDirectory(directory), bytes, argument, 0));
        void Aux(uint callback) => Emit(new NativeBattleChoco97AppendAux(callback, 0xB0));
        void OnMode()
        {
            switch (unchecked((short)Context(0x40)))
            {
                case 2:
                    Program(0x71F950, 0x128, 0x44C, 1);
                    Aux(0x721010);
                    break;
                case 4:
                    Program(0x71FDC0, 0x12C, 0x170, 2);
                    break;
                case 5:
                    Program(0x71FEC0, 0x130, 0xB0, 3);
                    Aux(0x721100);
                    Aux(0x720500);
                    Aux(0x720790);
                    break;
                case 7:
                    Aux(0x7215A0);
                    break;
                case 8:
                    Emit(new NativeBattleChoco99AppendSecondary(0x7211D0, 0x58));
                    Emit(new NativeBattleChoco99AppendSecondary(0x721390, 0x58));
                    Program(0x71FFB0, 0x134, 0x2C, 4);
                    Program(0x7201F0, 0x138, 0x194, 5);
                    Program(0x720320, 0x13C, 0x30, 6);
                    Program(0x720410, 0x140, 0x1E8, 7);
                    break;
                case 9:
                    Aux(0x721460);
                    break;
                case 10:
                    Emit(new NativeBattleChoco97AppendNeutral(inputs.ReadIoDirectory(0x108), 4, 45, 2));
                    Emit(new NativeBattleChoco99AppendSmall(0x71F880, 0x152AEF0, 8));
                    Emit(new NativeBattleChoco99AppendSmall(0x8DDC30, 0x152AF04, 9));
                    break;
            }
        }
    }
}
