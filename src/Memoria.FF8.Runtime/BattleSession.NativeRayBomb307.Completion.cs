namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeRayBomb307Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - NativeBattleRayBomb307Profile.RootBank);
        if (relative >= 16 * 2 || relative % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleRayBomb307Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = Write(detached) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleRayBomb307Profile.Entry } current ? current : throw new NotSupportedException("Ray-Bomb texture completion lost its actual task owner.");
            updated = graphics with { OriginalEffect = Write(effect) };
        }
        return true;

        NativeBattleOriginalEffectState Write(NativeBattleOriginalEffectState effect)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[0];
            int slot = checked((int)(relative / 16));
            if (pool.BankAddress != NativeBattleRayBomb307Profile.RootBank || pool.Slots[slot].Callback != NativeBattleRayBomb307Profile.TextureCallback)
            {
                throw new InvalidDataException("Ray-Bomb completion requires its original texture callback.");
            }
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteByte(14, value));
            return effect with { Pools = effect.Pools.SetItem(0, pool) };
        }
    }
}
