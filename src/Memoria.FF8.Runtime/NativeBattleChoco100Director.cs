using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100AppendLoadedModel(uint ResourceAddress, ushort InitializationArgument) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco100AppendContactSecondary(byte Actor) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco100DirectorSound(uint Descriptor, uint Flags, byte Pan) : NativeBattleChoco97DirectorOperation;
public sealed record NativeBattleChoco100DirectorInputs(NativeBattleChoco97DirectorInputs Services, Func<NativeBattleChoco97DirectorFrame, ImmutableArray<NativeBattleContactPacket>> ReadSelectedContacts);

public static class NativeBattleChoco100Director
{
    public const uint Callback = 0x70F370;
    public const uint LoadedModelCallback = 0x7175F0;
    public const int LoadedModelTaskBytes = 0x140;
    public const uint ContactSecondaryCallback = 0x716C20;
    public const int ContactSecondaryTaskBytes = 0xD0;
    public const uint ModelDescriptor = 0x25656B8;
    public const uint SecondaryDescriptor = 0x2561890;
    public const uint SmallDescriptor = 0x2564158;

    public static (uint Primary, uint Secondary) SetFreeze7154B0(uint primary, uint secondary, uint value) => NativeBattleChoco97Director.SetFreeze730610(primary, secondary, value);

    public static NativeBattleChoco97DirectorStep Advance70F370(NativeBattleChoco97DirectorFrame frame, NativeBattleChoco100DirectorInputs inputs)
    {
        frame.Context.Validate();
        if (frame.Task.Callback != Callback || frame.Task.PayloadLength != 0x48 - 12)
        {
            throw new InvalidDataException("Native Choco100 director requires its complete actual 48-byte task.");
        }
        NativeBattleChoco97DirectorInputs services = inputs.Services;
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
                Emit(new NativeBattleChoco97AppendNeutral(0x165FEEC, 18, 45, 2));
                Emit(new NativeBattleChoco97AppendProgram(0x70F5F0, 0x1663154, 0x4AC, 0, 0));
                Emit(new NativeBattleChoco97AppendAux(NativeBattleChoco100CameraTask.Callback, 0x70));
                Emit(new NativeBattleChoco97LoadModelTim());
                Next();
                break;
            case 1:
            case 4:
            case 7:
            case 11:
            case 15:
                NativeBattleChoco97DirectorRelease release = services.ReleaseWait(frame);
                frame = release.Frame;
                if (release.Released)
                {
                    Next();
                }
                break;
            case 2:
                if (services.ReadIoWord166() == 0)
                {
                    Emit(new NativeBattleChoco97SetFreeze(0));
                    Next();
                }
                else if (Word(0x24) >= 27)
                {
                    Emit(new NativeBattleChoco97SetFreeze(1));
                }
                break;
            case 3:
                if (Word(0x24) >= 28)
                {
                    Emit(new NativeBattleChoco97AppendModel());
                    Next();
                }
                break;
            case 5:
                if (services.ReadIoWord166() == 0 && Request(1))
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
            case 12:
            case 16:
                if (services.ReadIoWord166() == 0)
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
                    Next();
                }
                break;
            case 13:
                if (Request(6))
                {
                    Next();
                }
                break;
            case 14:
                if (AtLeast(6))
                {
                    WriteTask(frame.Task.WriteWord(0x44, frame.Task.ReadWord(0x24)));
                    Emit(new NativeBattleChoco100DirectorSound(0x15297FC, 0, 128));
                    Next();
                }
                break;
            case 17:
                if (Request(7))
                {
                    Next();
                }
                break;
            case 18:
                if ((int)Word(0x24) - Word(0x44) >= 20)
                {
                    Next();
                }
                break;
            case 19:
                if (AtLeast(8))
                {
                    WriteTask(frame.Task.WriteWord(0x44, 100));
                    Next();
                }
                break;
            case 20:
                if (Countdown() <= 18)
                {
                    WriteContext(0x48, 1);
                    Next();
                }
                break;
            case 21:
                if (Countdown() <= 0)
                {
                    Next();
                }
                break;
            case 22:
                if (Request(10))
                {
                    Emit(new NativeBattleChoco97StopVibration(unchecked((short)Context(0x52))));
                    WriteTask(frame.Task.WriteByte(0x26, (byte)(frame.Task.ReadByte(0x26) | 1)));
                    Next();
                }
                break;
            case 23:
                break;
            default:
                throw new NotSupportedException("Native Choco100 director phase leaves its twenty-four actual phases.");
        }
        WriteTask(frame.Task.WriteWord(0x24, unchecked((ushort)(frame.Task.ReadWord(0x24) + 1))));
        return new(frame, (frame.Task.ReadByte(0x26) & 1) != 0 && frame.Task.ReadByte(0x28) == 0);

        ushort Context(int offset) => frame.Context.Word(offset);
        short Word(int offset) => unchecked((short)frame.Task.ReadWord(offset));
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
        void Emit(NativeBattleChoco97DirectorOperation operation) => frame = services.Execute(frame, operation);
        void OnMode()
        {
            switch (unchecked((short)Context(0x40)))
            {
                case 2:
                    Emit(new NativeBattleChoco97AppendProgram(0x7163D0, services.ReadIoDirectory(0x128), 0x44C, 1, 0));
                    Emit(new NativeBattleChoco97AppendAux(0x716A60, 0x70));
                    break;
                case 4:
                    Emit(new NativeBattleChoco97AppendProgram(0x716840, services.ReadIoDirectory(0x12C), 0x170, 2, 0));
                    Emit(new NativeBattleChoco97AppendAux(0x716B50, 0x70));
                    break;
                case 6:
                    Emit(new NativeBattleChoco97AppendNeutral(services.ReadIoDirectory(0x108), 0, 45, 2));
                    Emit(new NativeBattleChoco100AppendLoadedModel(services.ReadIoDirectory(0x118), 0x3C));
                    break;
                case 8:
                    Emit(new NativeBattleChoco97AppendProgram(0x716950, services.ReadIoDirectory(0x130), 0x32C, 3, 0));
                    Emit(new NativeBattleChoco99AppendSmall(0x716300, 0x1529F70, 15));
                    Emit(new NativeBattleChoco97AppendNeutral(services.ReadIoDirectory(0x10C), 15, 45, 2));
                    break;
                case 9:
                    ImmutableArray<NativeBattleContactPacket> contacts = inputs.ReadSelectedContacts(frame);
                    if (contacts.IsDefault)
                    {
                        throw new NotSupportedException("Native Choco100 mode9 requires its actual selected header contact records.");
                    }
                    foreach (NativeBattleContactPacket contact in contacts)
                    {
                        contact.Validate();
                        Emit(new NativeBattleChoco100AppendContactSecondary(contact.Primary.ActorIndex));
                    }
                    break;
            }
        }
    }
}
