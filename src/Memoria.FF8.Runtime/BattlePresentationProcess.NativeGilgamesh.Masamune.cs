namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private static NativeBattleModulePoolTask ReadNativeMasamuneChild(NativeBattleGilgameshFrame frame, uint address)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        uint offset = unchecked(address - pool.BankAddress);
        if (!NativeBattleGilgameshAttackStorage.ValidChildBank(frame.Effect, pool) || offset >= (uint)(pool.Slots.Length * pool.Stride) || offset % pool.Stride != 0)
        {
            throw new InvalidDataException("Masamune operation leaves its actual raw child bank.");
        }
        return pool.Slots[checked((int)(offset / 36))];
    }

    private static NativeBattleGilgameshFrame UpdateNativeMasamuneChild(NativeBattleGilgameshFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleModulePoolTask task = ReadNativeMasamuneChild(frame, address);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        int slot = checked((int)((address - pool.BankAddress) / 36));
        int? next = task.Next;
        NativeBattleModulePoolTask updated = update(task);
        updated.SetNext(next);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool.WriteSlot(slot, updated)) } };
    }
}
