using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceMain62A9C0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleEffect326DirectorInputs services = new(inputs.ReadFlags(frame), PublishWorld, Poll, Execute);
        NativeBattleEffect326DirectorStep step = NativeBattleEffect326Director.Advance62A9C0(Read(frame, address), services);
        return new(Write(frame, address, step.Task), step.Result);

        void PublishWorld()
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), ReadPacked(frame, BaseMatrix).ReadMatrix());
            frame = WriteBytes(frame, ViewMatrix, PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 32);
        }

        int Poll()
        {
            NativeBattleEffect326Step poll = inputs.ReadStep508500(frame);
            frame = poll.Frame;
            return poll.Result;
        }

        void Execute(NativeBattleEffect326DirectorOperation operation)
        {
            uint source = operation.SourceBase switch
            {
                NativeBattleEffect326SourceBase.Literal => unchecked((uint)operation.First),
                NativeBattleEffect326SourceBase.Common => checked(CommonAddress + (uint)operation.First),
                NativeBattleEffect326SourceBase.Work => checked(0x209FAB8u + (uint)operation.First),
                _ => throw new NotSupportedException("Effect326 selected an unsupported physical source base.")
            };
            switch (operation.Service)
            {
                case NativeBattleEffect326DirectorService.Camera:
                    frame = BeginCamera63E960(frame, resources);
                    break;
                case NativeBattleEffect326DirectorService.Stream:
                    frame = inputs.Execute(frame, new NativeBattleEffect326PublishStream501860(source, unchecked((uint)operation.Second)));
                    break;
                case NativeBattleEffect326DirectorService.Vibration:
                    frame = inputs.Execute(frame, new NativeBattleEffect326Vibration4A29A0(source, operation.Second, operation.Third, unchecked((uint)operation.Fourth)));
                    break;
                case NativeBattleEffect326DirectorService.Sound:
                    frame = inputs.Execute(frame, new NativeBattleEffect326Sound501330(source, unchecked((uint)operation.Second), checked((byte)operation.Third)));
                    break;
                case NativeBattleEffect326DirectorService.File:
                    uint destination = operation.SourceBase switch
                    {
                        NativeBattleEffect326SourceBase.Literal => unchecked((uint)operation.Second),
                        NativeBattleEffect326SourceBase.Common => checked(CommonAddress + (uint)operation.Second),
                        NativeBattleEffect326SourceBase.Work => checked(0x209FAB8u + (uint)operation.Second),
                        _ => throw new NotSupportedException("Effect326 selected an unsupported file destination base.")
                    };
                    frame = inputs.Execute(frame, new NativeBattleEffect326Queue508480(checked((short)operation.First), destination));
                    break;
                case NativeBattleEffect326DirectorService.Upload:
                    frame = inputs.Execute(frame, new NativeBattleEffect326UploadTim505E30(checked(CommonAddress + (uint)operation.First)));
                    break;
                case NativeBattleEffect326DirectorService.Initialize:
                    frame = operation.First switch
                    {
                        0x62B010 => InitializeOpening62B010(frame),
                        0x62BA60 => InitializeScrollBurst62BA60(frame),
                        0x62BED0 => InitializeActorOverlay62BED0(frame, inputs),
                        0x62C180 => InitializeActorBurst62C180(frame, inputs),
                        _ => throw new NotSupportedException("Effect326 main selected an unsupported native initializer.")
                    };
                    break;
                case NativeBattleEffect326DirectorService.Append:
                    (frame, _) = Spawn62AFB0(frame, unchecked((uint)operation.First));
                    break;
                case NativeBattleEffect326DirectorService.Play:
                    frame = inputs.Execute(frame, new NativeBattleEffect326PlayStream5018C0(unchecked((uint)operation.First), unchecked((uint)operation.Second), unchecked((uint)operation.Third)));
                    break;
                case NativeBattleEffect326DirectorService.StopVibrationIfActive:
                    int handle = unchecked((int)frame.State.ReadDword(0x24F1060));
                    if (inputs.ReadVibrationPlaying4A2900(frame, handle))
                    {
                        frame = inputs.Execute(frame, new NativeBattleEffect326StopVibration4A2940(handle));
                    }
                    break;
                case NativeBattleEffect326DirectorService.Stage:
                    frame = inputs.Execute(frame, new NativeBattleEffect326Stage5713E0(operation.First, 0));
                    break;
                case NativeBattleEffect326DirectorService.StageGroupFlags:
                    frame = inputs.Execute(frame, new NativeBattleEffect326StageGroupFlags(checked((byte)operation.First), checked((byte)operation.Second)));
                    break;
                case NativeBattleEffect326DirectorService.ActorAnimation:
                    frame = inputs.Execute(frame, new NativeBattleEffect326ActorAnimation505C00(checked((int)frame.State.ReadDword(0x24F0C80)), checked((short)operation.First)));
                    break;
                case NativeBattleEffect326DirectorService.Flash:
                    frame = inputs.Execute(frame, new NativeBattleEffect326Flash5712C0(operation.First, operation.Second, operation.Third, operation.Fourth));
                    break;
                case NativeBattleEffect326DirectorService.Contacts:
                    frame = inputs.Execute(frame, new NativeBattleEffect326Contacts506BA0());
                    break;
                default:
                    throw new NotSupportedException("Effect326 main selected an unsupported native service.");
            }
        }
    }

    public static NativeBattleEffect326Step AdvanceTextureRelease62CEA0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect326QueueTextureRelease508630(checked(frame.State.ReadDword(ArenaGlobal) + 0x14800), address + 14));
        }
        task = Read(frame, address);
        int result = task.ReadWord(14) != 0 ? 2 : 0;
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return new(frame, result);
    }
}
