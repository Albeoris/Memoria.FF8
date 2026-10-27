using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep BeginCamera63E960(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool)
    {
        NativeBattleNoMercy200State state = frame.Memory.NoMercy200 ?? throw new InvalidDataException("Native No Mercy camera requires its entered owner.");
        NativeBattleNoMercy200Resources resources = state.Resources ?? throw new NotSupportedException("Native No Mercy camera requires resources retained by actual entry.");
        NativeBattleGuardianCameraProcess process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native63E960 requires its actual retained paired camera banks.");
        NativeBattleGuardianCameraTask camera = NativeBattleGuardianCameraProgram.Begin63E960(process, resources.Camera.Data, 0);
        (pool, int slot) = pool.Append(0x63E9C0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, 0);
        task = TaskDword(TaskDword(TaskDword(task, 20, 0xE33D8C), 24, 0x24F8D90), 28, checked(0x1D972C0u + (uint)frame.Parameters.Actor78 * 156));
        pool = pool.WriteSlot(slot, task);
        frame = frame with { Memory = frame.Memory with { NoMercy200 = state with { CameraTask = camera }, GuardianCameraProcess = camera.Process } };
        return new(frame, pool, 0);
    }

    public static NativeBattleNoMercy200ChildStep AdvanceCamera63E9C0(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, uint graphicsFlags, NativeBattleCameraPose pose, Func<NativeBattleNoMercy200Frame, NativeBattleGuardianCameraStep, NativeBattleNoMercy200Frame> publish)
    {
        NativeBattleNoMercy200State state = frame.Memory.NoMercy200 ?? throw new InvalidDataException("Native No Mercy camera requires its entered owner.");
        NativeBattleGuardianCameraTask camera = state.CameraTask ?? throw new NotSupportedException("Native No Mercy camera requires its actually appended program task.");
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x63E9C0 || task.ReadWord(12) != camera.Frame || TaskDword(task, 20) != 0xE33D8Cu + camera.Offset || TaskDword(task, 24) != 0x24F8D90 || TaskDword(task, 28) != 0x1D972C0u + (uint)frame.Parameters.Actor78 * 156)
        {
            throw new InvalidDataException("Native No Mercy camera lost its physical task operands.");
        }
        camera = camera with { Process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native63E9C0 requires its actual retained paired camera banks.") };
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 432);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(camera, ReadMatrix(temporary.Staging, 0x24F8D90), pose, (graphicsFlags & 1) != 0, temporary432: NativeBattleTemporaryMemory.ReadView(temporary.Staging, temporary.Address, 432));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(temporary.Staging, temporary.Address, step.Temporary432 ?? throw new InvalidDataException("Native camera lost its actual temporary memory."), 432);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 432) };
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = step.Task.Process, NoMercy200 = state with { CameraTask = step.Task } } };
        task = TaskDword(task.WriteWord(12, step.Task.Frame), 20, checked(0xE33D8Cu + (uint)step.Task.Offset));
        pool = pool.WriteSlot(slot, task);
        if (step.Published)
        {
            frame = publish(frame, step);
        }
        return new(frame, pool, step.Result);
    }

}
