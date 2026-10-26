using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceMain6474C0(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleEffect187DirectorInputs services = new(inputs.ReadFlags(frame), PublishWorld, Poll, () => frame.State.Read(0x24FD6D0, 1)[0] != 0, Execute);
        NativeBattleEffect187DirectorStep step = NativeBattleEffect187Director.Advance6474C0(Read(frame, address), services);
        return new(Write(frame, address, step.Task), step.Result);

        void PublishWorld()
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), ReadPacked(frame, BaseMatrix).ReadMatrix());
            frame = WriteBytes(frame, ViewMatrix, PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 32);
        }

        int Poll()
        {
            NativeBattleEffect187Step poll = inputs.ReadStep508500(frame);
            frame = poll.Frame;
            return poll.Result;
        }

        void Execute(NativeBattleEffect187DirectorOperation operation)
        {
            uint source = operation.UseWorkBuffer ? checked(0x209FAB8u + (uint)operation.First) : unchecked((uint)operation.First);
            switch (operation.Service)
            {
                case NativeBattleEffect187DirectorService.Camera:
                    frame = BeginCamera63E960(frame, resources);
                    break;
                case NativeBattleEffect187DirectorService.Stream:
                    frame = inputs.Execute(frame, new NativeBattleEffect187PublishStream501860(source, unchecked((uint)operation.Second)));
                    break;
                case NativeBattleEffect187DirectorService.Vibration:
                    frame = inputs.Execute(frame, new NativeBattleEffect187Vibration4A29A0(source, operation.Second, operation.Third, unchecked((uint)operation.Fourth)));
                    break;
                case NativeBattleEffect187DirectorService.Sound:
                    frame = inputs.Execute(frame, new NativeBattleEffect187Sound501330(source, unchecked((uint)operation.Second), checked((byte)operation.Third)));
                    break;
                case NativeBattleEffect187DirectorService.File:
                    uint destination = checked((operation.UseWorkBuffer ? 0x209FAB8u : CommonAddress) + (uint)operation.Second);
                    frame = inputs.Execute(frame, new NativeBattleEffect187Queue508480(checked((short)operation.First), destination));
                    break;
                case NativeBattleEffect187DirectorService.Upload:
                    frame = inputs.Execute(frame, new NativeBattleEffect187UploadTim505E30(checked(CommonAddress + (uint)operation.First)));
                    break;
                case NativeBattleEffect187DirectorService.Initialize:
                    frame = operation.First switch
                    {
                        0x647960 => InitializeOpening647960(frame),
                        0x6483B0 => InitializeScrollBurst6483B0(frame),
                        0x648820 => InitializeActorOverlay648820(frame, inputs),
                        _ => throw new NotSupportedException("Effect187 main selected an unsupported native initializer.")
                    };
                    break;
                case NativeBattleEffect187DirectorService.Append:
                    (frame, _) = Spawn647900(frame, unchecked((uint)operation.First));
                    break;
                case NativeBattleEffect187DirectorService.Play:
                    frame = inputs.Execute(frame, new NativeBattleEffect187PlayStream5018C0(unchecked((uint)operation.First), unchecked((uint)operation.Second), unchecked((uint)operation.Third)));
                    break;
                case NativeBattleEffect187DirectorService.StopVibrationIfActive:
                    int handle = unchecked((int)frame.State.ReadDword(0x24FD6D4));
                    if (inputs.ReadVibrationPlaying4A2900(frame, handle))
                    {
                        frame = inputs.Execute(frame, new NativeBattleEffect187StopVibration4A2940(handle));
                    }
                    break;
                case NativeBattleEffect187DirectorService.Stage:
                    frame = inputs.Execute(frame, new NativeBattleEffect187Stage5713E0(operation.First, 0));
                    break;
                case NativeBattleEffect187DirectorService.StageGroupFlags:
                    frame = inputs.Execute(frame, new NativeBattleEffect187StageGroupFlags(checked((byte)operation.First), checked((byte)operation.Second)));
                    break;
                case NativeBattleEffect187DirectorService.Contacts:
                    frame = inputs.Execute(frame, new NativeBattleEffect187Contacts506BA0());
                    break;
                default:
                    throw new NotSupportedException("Effect187 main selected an unsupported native service.");
            }
        }
    }

    public static NativeBattleEffect187Step AdvanceTextureRelease649CC0(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect187QueueTextureRelease508630(checked(frame.State.ReadDword(ArenaGlobal) + 0x14000), address + 14));
        }
        task = Read(frame, address);
        int result = task.ReadWord(14) != 0 ? 2 : 0;
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return new(frame, result);
    }
}
