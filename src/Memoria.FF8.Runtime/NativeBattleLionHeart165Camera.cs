using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep BeginCamera63E960(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool)
    {
        NativeBattleLionHeart165State state = frame.Memory.LionHeart165 ?? throw new InvalidDataException("Native Lion Heart camera requires its entered owner.");
        NativeBattleLionHeart165Resources resources = state.RetainedResources ?? throw new NotSupportedException("Native Lion Heart camera requires resources retained by actual entry.");
        NativeBattleGuardianCameraProcess process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native63E960 requires its actual retained paired camera banks.");
        NativeBattleGuardianCameraTask camera = NativeBattleGuardianCameraProgram.Begin63E960(process, resources.Read(0xD88930, 0x170).ToImmutableArray(), 0);
        (pool, int slot) = pool.Append(0x63E9C0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, 0);
        task = TaskDword(TaskDword(TaskDword(task, 20, 0xD88930), 24, 0x23C58E0), 28, checked(0x1D972C0u + (uint)frame.Parameters.Actor78 * 156));
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Memory = frame.Memory with { LionHeart165 = state with { CameraTask = camera }, GuardianCameraProcess = camera.Process } };
        return new(frame, pool, 0);
    }

    public static NativeBattleLionHeart165ChildStep AdvanceCamera63E9C0(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, uint graphicsFlags, NativeBattleCameraPose pose, Func<NativeBattleLionHeart165Frame, NativeBattleGuardianCameraStep, NativeBattleLionHeart165Frame> publish)
    {
        NativeBattleLionHeart165State state = frame.Memory.LionHeart165 ?? throw new InvalidDataException("Native Lion Heart camera requires its entered owner.");
        NativeBattleGuardianCameraTask camera = state.CameraTask ?? throw new NotSupportedException("Native Lion Heart camera requires its actually appended program task.");
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x63E9C0 || task.ReadWord(12) != camera.Frame || TaskDword(task, 20) != 0xD88930u + camera.Offset || TaskDword(task, 24) != 0x23C58E0 || TaskDword(task, 28) != 0x1D972C0u + (uint)frame.Parameters.Actor78 * 156)
        {
            throw new InvalidDataException("Native Lion Heart camera lost its physical task operands.");
        }
        camera = camera with { Process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native63E9C0 requires its actual retained paired camera banks.") };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 432);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 432);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(camera, ReadMatrix(frame.Staging, 0x23C58E0), pose, (graphicsFlags & 1) != 0, temporary432: temporary);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, step.Temporary432 ?? throw new InvalidDataException("Native Lion Heart camera lost its actual temporary writes."), 432);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 432) };
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = step.Task.Process, LionHeart165 = state with { CameraTask = step.Task } } };
        task = TaskDword(task.WriteWord(12, step.Task.Frame), 20, checked(0xD88930u + (uint)step.Task.Offset));
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        if (step.Published)
        {
            frame = publish(frame, step);
        }
        return new(frame, pool, step.Result);
    }

    private static uint TaskDword(NativeBattleModulePoolTask task, int offset) => (uint)task.ReadWord(offset) | ((uint)task.ReadWord(offset + 2) << 16);
    private static NativeBattleModulePoolTask TaskDword(NativeBattleModulePoolTask task, int offset, uint value) => task.WriteWord(offset, unchecked((ushort)value)).WriteWord(offset + 2, unchecked((ushort)(value >> 16)));

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleFileStaging staging, uint address)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => ReadWord(staging, address + (uint)index * 2)).ToImmutableArray();
        return new(rotation, ReadDword(staging, address + 20), ReadDword(staging, address + 24), ReadDword(staging, address + 28), 4096);
    }
}
