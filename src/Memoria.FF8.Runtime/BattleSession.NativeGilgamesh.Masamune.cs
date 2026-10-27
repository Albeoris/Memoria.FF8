namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleModulePoolTask ReadNativeMasamuneChild(NativeBattleGilgameshFrame frame, uint address)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        uint offset = unchecked(address - pool.BankAddress);
        if (pool.DescriptorAddress != 0x21FFD58 || pool.BankAddress != 0x21FFD68 || pool.Stride != 36 || offset >= 100 * 36 || offset % 36 != 0)
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
