using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame BeginGuardianCamera63E960(NativeBattleEffect218Frame frame, NativeBattleEffect218Resources resources)
    {
        NativeBattleGuardianCameraTask begun = NativeBattleGuardianCameraProgram.Begin63E960(frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Effect218 requires its actual retained guardian camera banks."), resources.Camera.Data, 0);
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = begun.Process } };
        (frame, uint address) = AppendRequired(frame, MainDescriptor, 0x63E9C0);
        return Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteDword(20, 0xE25970).WriteDword(24, CameraBasis).WriteDword(28, StreamedModelAddress));
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceGuardianCamera63E9C0(NativeBattleEffect218Frame frame, uint address, NativeBattleEffect218Resources resources, NativeBattleEffect218RuntimeInputs inputs)
    {
        NativeBattleModulePoolTask poolTask = Read(frame, address);
        if (poolTask.ReadDword(24) != CameraBasis || poolTask.ReadDword(28) != StreamedModelAddress)
        {
            throw new InvalidDataException("Effect218 guardian camera requires its actual basis and cinematic model pointers.");
        }
        NativeBattleGuardianCameraTask task = new(frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Effect218 guardian camera requires its retained banks."), resources.Camera.Data, checked((int)(poolTask.ReadDword(20) - 0xE25970)), poolTask.ReadWord(12), false);
        (frame, uint temporary) = Allocate(frame, 432);
        NativeBattleActionMatrix basis = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CameraBasis, 32), 0);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, basis, inputs.ReadCameraPose(frame), (inputs.ReadGraphicsFlags(frame) & 0x201) != 0, temporary432: Context(frame, temporary, 432));
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = step.Task.Process } };
        frame = Write(frame, address, Read(frame, address).WriteWord(12, step.Task.Frame).WriteDword(20, checked(0xE25970u + (uint)step.Task.Offset)));
        frame = PublishContext(frame, temporary, step.Temporary432 ?? throw new InvalidDataException("Effect218 guardian camera lost its actual temporary storage."), 432);
        if (step.Published)
        {
            frame = inputs.PublishGuardianCamera(frame, step);
        }
        return (Release(frame, 432), step.Result);
    }
}
