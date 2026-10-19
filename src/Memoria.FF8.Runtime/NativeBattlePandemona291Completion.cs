using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291CompletionServices(Func<NativeBattlePandemona291Frame, bool> TargetsAwaitResource40, Func<NativeBattlePandemona291Frame, uint, NativeBattlePandemona291Frame> RestoreTargetPositions, Func<NativeBattlePandemona291Frame, NativeBattleEffectFileRequest, NativeBattlePandemona291Frame> QueueFile);

public static class NativeBattlePandemona291Completion
{
    public static NativeBattlePandemona291ChildStep AfterCounter6F0626(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattlePandemona291CompletionServices services)
    {
        int age = unchecked((short)task.ReadWord(0xC));
        if (age <= 480)
        {
            return new(frame, task, 0);
        }
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona completion requires its current module state.");
        frame = frame with { Memory = frame.Memory with { Pandemona291 = state with { Flash25562F0 = 0 } } };
        if (age < 510 && services.TargetsAwaitResource40(frame))
        {
            return new(frame, task, 0);
        }
        // 534300(2) has the same complete-index/ready/delay branch as5342C0.
        NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(frame.Memory, 2);
        frame = frame with { Memory = poll.Memory };
        if (poll.Request is { } request)
        {
            frame = services.QueueFile(frame, request);
        }
        if (!poll.Busy)
        {
            return new(services.RestoreTargetPositions(frame, address), task, 2);
        }
        task = task.WriteWord(0xC, 520);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona completion requires its actual counter bytes.");
        arena = arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(address + 0xC), 2), BitConverter.GetBytes((ushort)520));
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, task, 0);
    }
}
