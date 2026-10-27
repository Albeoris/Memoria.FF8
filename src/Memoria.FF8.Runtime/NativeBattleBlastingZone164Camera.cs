using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep BeginCamera63E960(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool)
    {
        NativeBattleBlastingZone164State state = frame.Memory.BlastingZone164 ?? throw new InvalidDataException("Native Blasting Zone camera requires its entered owner.");
        NativeBattleBlastingZone164Resources resources = state.RetainedResources ?? throw new NotSupportedException("Native Blasting Zone camera requires resources retained by actual entry.");
        NativeBattleGuardianCameraProcess process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native63E960 requires its actual retained paired camera banks.");
        NativeBattleGuardianCameraTask camera = NativeBattleGuardianCameraProgram.Begin63E960(process, resources.Read(0xD99C70, 0x148).ToImmutableArray(), 0);
        (pool, int slot) = pool.Append(0x63E9C0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, 0);
        task = TaskDword(TaskDword(TaskDword(task, 20, 0xD99C70), 24, 0x23C8F20), 28, checked(0x1D972C0u + (uint)frame.Parameters.Actor78 * 156));
        pool = pool.WriteSlot(slot, task);
        frame = frame with { Memory = frame.Memory with { BlastingZone164 = state with { CameraTask = camera }, GuardianCameraProcess = camera.Process } };
        return new(frame, pool, 0);
    }

    public static NativeBattleBlastingZone164ChildStep AdvanceCamera63E9C0(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, uint graphicsFlags, NativeBattleCameraPose pose, Func<NativeBattleBlastingZone164Frame, NativeBattleGuardianCameraStep, NativeBattleBlastingZone164Frame> publish)
    {
        NativeBattleBlastingZone164State state = frame.Memory.BlastingZone164 ?? throw new InvalidDataException("Native Blasting Zone camera requires its entered owner.");
        NativeBattleGuardianCameraTask camera = state.CameraTask ?? throw new NotSupportedException("Native Blasting Zone camera requires its actually appended program task.");
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x63E9C0 || task.ReadWord(12) != camera.Frame || TaskDword(task, 20) != 0xD99C70u + camera.Offset || TaskDword(task, 24) != 0x23C8F20 || TaskDword(task, 28) != 0x1D972C0u + (uint)frame.Parameters.Actor78 * 156)
        {
            throw new InvalidDataException("Native Blasting Zone camera lost its physical task operands.");
        }
        camera = camera with { Process = frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native63E9C0 requires its actual retained paired camera banks.") };
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(camera, ReadMatrix(frame.Staging, 0x23C8F20), pose, (graphicsFlags & 1) != 0);
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = step.Task.Process, BlastingZone164 = state with { CameraTask = step.Task } } };
        task = TaskDword(task.WriteWord(12, step.Task.Frame), 20, checked(0xD99C70u + (uint)step.Task.Offset));
        pool = pool.WriteSlot(slot, task);
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
