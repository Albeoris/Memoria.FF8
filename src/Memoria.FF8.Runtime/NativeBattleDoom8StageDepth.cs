namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8StageDepthWrite(NativeBattleStageFadeWrite Write) : NativeBattleDoom8Operation;

public static class NativeBattleDoom8StageDepth
{
    public static NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame, uint address, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        frame.State.Validate();
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || task.Callback is not (0x8CC1E0 or 0x8CC2D0))
        {
            throw new InvalidDataException("Doom stage depth requires its actual module callback.");
        }
        NativeBattleCureFamilyPool expected = NativeBattleDoom8State.Pools[5];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity || address < pool.BankAddress || address >= pool.BankAddress + pool.Stride * pool.Slots.Length)
        {
            throw new InvalidDataException("Doom stage depth requires its physical task bank.");
        }
        bool fadeIn = task.Callback == 0x8CC1E0;
        if (fadeIn || unchecked((sbyte)task.ReadByte(0x29)) != 0 || unchecked((short)task.ReadWord(0x24)) > 45)
        {
            NativeBattleStageFadeStep advanced = NativeBattleStageFadeTask.Advance(task, fadeIn, fadeIn ? (short)160 : (short)256, 1536);
            frame = NativeBattleDoom8Controller.Write(frame, address, advanced.Task);
            if (advanced.Write is { } write)
            {
                frame = execute(frame, new NativeBattleDoom8StageDepthWrite(write));
            }
        }
        return NativeBattleDoom8Controller.Finish(frame, address);
    }
}
