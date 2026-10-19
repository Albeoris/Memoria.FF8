using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325StreamStep(NativeBattleDiablos325Frame Frame, NativeBattleModulePoolTask Task);

public static class NativeBattleDiablos325Stream
{
    public static NativeBattleDiablos325StreamStep Initialize655488(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos stream publication requires its actual file buffer.");
        uint source = checked(state.ModelBuffer5208 + 0x1685C);
        task = task.WriteByte(0xF, 0);
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(source, 4);
        int number = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
        NativeBattleSummonStreamState stream = (frame.Memory.SummonStream ?? NativeBattleSummonStreamState.Unknown()).Publish501860(source, number);
        frame = frame with { Memory = frame.Memory with { SummonStream = stream } };
        return new(frame, task.WriteByte(0xF, 1));
    }
}
