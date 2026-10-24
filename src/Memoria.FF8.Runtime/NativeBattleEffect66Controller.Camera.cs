using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Controller
{
    public static NativeBattleEffect66Frame BeginCamera63E960(NativeBattleEffect66Frame frame, NativeBattleEffect66Resources resources)
    {
        NativeBattleGuardianCameraProcess process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Effect66 requires its retained camera process.");
        NativeBattleGuardianCameraTask camera = NativeBattleGuardianCameraProgram.Begin63E960(process, resources.Camera.Data, 0);
        (frame, uint address) = Append(frame, ChildDescriptor, 0x63E9C0);
        if (address == 0)
        {
            throw new InvalidDataException("Effect66 exhausted its mandatory camera task bank.");
        }
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteDword(20, CameraAddress).WriteDword(24, CapturedWorldAddress).WriteDword(28, ModelAddress(actor));
        frame = Write(frame, address, task);
        return frame with { Memory = frame.Memory with { GuardianCameraProcess = camera.Process }, State = frame.State with { CameraTasks = frame.State.CameraTasks.Add(address, camera) } };
    }

    public static NativeBattleEffect66Step AdvanceCamera63E9C0(NativeBattleEffect66Frame frame, uint address, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleGuardianCameraTask camera = frame.State.CameraTasks.TryGetValue(address, out NativeBattleGuardianCameraTask? taskState) ? taskState : throw new NotSupportedException("Effect66 camera requires its actually appended task.");
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        if (task.Callback != 0x63E9C0 || task.ReadWord(12) != camera.Frame || task.ReadDword(20) != CameraAddress + (uint)camera.Offset || task.ReadDword(24) != CapturedWorldAddress || task.ReadDword(28) != ModelAddress(actor))
        {
            throw new InvalidDataException("Effect66 camera task lost its current physical operands.");
        }
        camera = camera with { Process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Effect66 camera requires its current paired process.") };
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(camera, ReadPacked(frame, CapturedWorldAddress).ReadMatrix(), inputs.ReadCameraPose(frame), (inputs.ReadFlags(frame) & 1) != 0);
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = step.Task.Process }, State = frame.State with { CameraTasks = step.Result == 2 ? frame.State.CameraTasks.Remove(address) : frame.State.CameraTasks.SetItem(address, step.Task) } };
        frame = Write(frame, address, Read(frame, address).WriteWord(12, step.Task.Frame).WriteDword(20, checked(CameraAddress + (uint)step.Task.Offset)));
        if (step.Published)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect66PublishCameraPose(step.Pose));
        }
        return new(frame, step.Result);
    }

    private static NativeBattlePackedMatrix32 ReadPacked(NativeBattleEffect66Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = ReadBytes(frame, address, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffect66Frame StoreMatrix(NativeBattleEffect66Frame frame, uint address, NativeBattleActionMatrix matrix)
    {
        NativeBattleEffectArena target = ReadBytes(frame, address, 32);
        NativeBattleEffectArena source = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        target = CopyBytes(source, 0, target, 0, 18);
        target = CopyBytes(source, 20, target, 20, 12);
        return WriteBytes(frame, address, target, 32);
    }
}
