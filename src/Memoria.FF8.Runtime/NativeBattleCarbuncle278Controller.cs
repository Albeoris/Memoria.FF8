namespace Memoria.FF8.Runtime;

/// <summary>Carbuncle's physical root callback up to its model and mirror construction boundary.</summary>
public static class NativeBattleCarbuncle278Controller
{
    public static NativeBattleCarbuncle278Frame Advance680DF0(NativeBattleCarbuncle278Frame frame, Action<uint, int> stage5713E0)
    {
        NativeBattleCarbuncle278Entry.RequireBinding(frame.Effect);
        ArgumentNullException.ThrowIfNull(stage5713E0);
        if (frame.Effect.Pools.Length != 1 || frame.Effect.Pools[0] is not { DescriptorAddress: NativeBattleCarbuncle278Entry.MainDescriptor, BankAddress: NativeBattleCarbuncle278Entry.MainBank, Stride: 0x14 } pool || pool.Slots.Length != 1)
        {
            throw new InvalidDataException("Native Carbuncle requires its actual single root task bank.");
        }
        NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
        {
            NativeBattleModulePoolTask task = current.Slots[index];
            if (task.Callback != NativeBattleCarbuncle278Entry.MainCallback)
            {
                throw new NotSupportedException("Native Carbuncle root callback was replaced.");
            }
            NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(frame.Memory);
            uint primary = task.ReadDword(0x10) != 0 ? 0x3ECCu : 0x13ECCu;
            state = state with { PacketCursor81BC = unchecked(state.ModelBuffer81F8 + primary), AlternateCursor81C0 = unchecked(state.ModelBuffer81F8 + primary + 0x10000), Flash81D4 = 0 };
            task = task.WriteDword(0x10, task.ReadDword(0x10) != 0 ? 0u : 1u);
            if (task.ReadWord(0xC) == 2 && state.Pause8114 == 0 && task.ReadByte(0xF) == 0)
            {
                throw new NotSupportedException("Native Carbuncle680DF0 reaches model bounds6D6070 and mirror6812E0 construction before its creature callback681630.");
            }
            if (task.ReadByte(0xF) != 0)
            {
                throw new NotSupportedException("Native Carbuncle680DF0 requires its current creature, particle and beam task executors.");
            }
            stage5713E0(state.Flash81D4, 0);
            if (state.Pause8114 == 0)
            {
                task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
            }
            frame = frame with { Memory = frame.Memory with { Carbuncle278 = state } };
            return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
        });
        return frame with { Effect = frame.Effect with { Pools = [step.Pool], ReturnedDescriptor = step.Survivors == 0 ? 0u : NativeBattleCarbuncle278Entry.MainDescriptor } };
    }
}
