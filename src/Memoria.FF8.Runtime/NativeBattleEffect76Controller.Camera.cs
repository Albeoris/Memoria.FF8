using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect76Controller
{
    public static NativeBattleEffect76Frame BeginCamera63E960(NativeBattleEffect76Frame frame, NativeBattleEffect76Resources resources)
    {
        NativeBattleGuardianCameraProcess process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Effect76 requires its retained camera process.");
        NativeBattleGuardianCameraTask camera = NativeBattleGuardianCameraProgram.Begin63E960(process, resources.Camera.Data, 0);
        (frame, uint address) = Append(frame, ChildDescriptor, 0x63E9C0);
        if (address == 0)
        {
            throw new NotSupportedException("Effect76 exhausted its required camera task bank.");
        }
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteDword(20, CameraAddress).WriteDword(24, CapturedWorldAddress).WriteDword(28, ModelAddress(actor)));
        return frame with { Memory = frame.Memory with { GuardianCameraProcess = camera.Process }, State = frame.State with { CameraTasks = frame.State.CameraTasks.Add(address, camera) } };
    }

    public static NativeBattleEffect76Step AdvanceCamera63E9C0(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Inputs inputs)
    {
        NativeBattleGuardianCameraTask camera = frame.State.CameraTasks.TryGetValue(address, out NativeBattleGuardianCameraTask? current) ? current : throw new NotSupportedException("Effect76 requires its actually appended camera task.");
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x63E9C0 || task.ReadWord(12) != camera.Frame || task.ReadDword(20) != CameraAddress + (uint)camera.Offset || task.ReadDword(24) != CapturedWorldAddress || task.ReadDword(28) != ModelAddress(checked((int)frame.State.ReadDword(CasterGlobal))))
        {
            throw new InvalidDataException("Effect76 camera lost its actual task operands.");
        }
        camera = camera with { Process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Effect76 requires its current paired camera process.") };
        (frame, uint temporaryAddress) = Allocate(frame, 432);
        NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 432);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(camera, ReadPacked(frame, CapturedWorldAddress).ReadMatrix(), inputs.ReadCameraPose(frame), (inputs.ReadFlags(frame) & 1) != 0, temporary432: temporary, currentEyePadding: inputs.ReadEyePadding(frame), currentTargetPadding: inputs.ReadTargetPadding(frame));
        temporary = step.Temporary432 ?? throw new InvalidDataException("Effect76 camera lost its current temporary writes.");
        frame = Release(PublishContext(frame, temporaryAddress, temporary, 432), 432);
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = step.Task.Process }, State = frame.State with { CameraTasks = step.Result == 2 ? frame.State.CameraTasks.Remove(address) : frame.State.CameraTasks.SetItem(address, step.Task) } };
        frame = Write(frame, address, Read(frame, address).WriteWord(12, step.Task.Frame).WriteDword(20, checked(CameraAddress + (uint)step.Task.Offset)));
        if (step.Published)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect76PublishCameraPose(step.Pose, Padding(temporary, 6), Padding(temporary, 14)));
        }
        return new(frame, step.Result);
    }

    private static short? Padding(NativeBattleEffectArena source, int offset)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }

    private static NativeBattlePackedMatrix32 ReadPacked(NativeBattleEffect76Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = ReadBytes(frame, address, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffect76Frame StoreMatrix(NativeBattleEffect76Frame frame, uint address, NativeBattleActionMatrix matrix)
    {
        NativeBattleEffectArena target = ReadBytes(frame, address, 32);
        NativeBattleEffectArena source = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        target = CopyBytes(source, 0, target, 0, 18);
        target = CopyBytes(source, 20, target, 20, 12);
        return WriteBytes(frame, address, target, 32);
    }
}
