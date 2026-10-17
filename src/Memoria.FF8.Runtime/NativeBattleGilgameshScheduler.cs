namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGuardianPoolCallback(NativeBattleGuardianEffectPools Pools, int Result);
public sealed record NativeBattleGuardianPoolStep(NativeBattleGuardianEffectPools Pools, int Survivors);

/// <summary>The original 596B70 returned-task scheduler over the retained Gilgamesh pools.</summary>
public static class NativeBattleGilgameshScheduler
{
    public static NativeBattleGuardianPoolStep Advance(NativeBattleGuardianEffectPools pools, Func<NativeBattleGuardianEffectPools, uint, int, NativeBattleGuardianPoolCallback> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Validate(pools);
        NativeBattleGuardianEffectPools current = pools;
        NativeBattleEffectTaskStep main = pools.Main.Advance((mainPool, slot) =>
        {
            current = current with { Main = mainPool };
            if (mainPool.Slots[slot].Callback != 0x596B70)
            {
                throw new NotSupportedException("Native Gilgamesh returned task requires its original596B70 callback.");
            }
            NativeBattleEffectTaskStep children = current.Children.Advance((childPool, childSlot) => Invoke(childPool, childSlot, false));
            current = current with { Children = children.Pool };
            NativeBattleEffectTaskStep particles = current.Particles!.Advance((particlePool, particleSlot) => Invoke(particlePool, particleSlot, true));
            current = current with { Particles = particles.Pool };
            NativeBattleModulePoolTask task = current.Main.Slots[slot];
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            current = current with { Main = current.Main with { Slots = current.Main.Slots.SetItem(slot, task) } };
            return new(current.Main, (children.Survivors | particles.Survivors) == 0 ? 2 : 0);
        });
        current = current with { Main = main.Pool };
        Validate(current);
        return new(current, main.Survivors);

        NativeBattleEffectTaskCallback Invoke(NativeBattleEffectTaskPool pool, int slot, bool particle)
        {
            current = particle ? current with { Particles = pool } : current with { Children = pool };
            NativeBattleGuardianPoolCallback result = callback(current, pool.DescriptorAddress, slot);
            current = result.Pools;
            // The enclosing508420 pass validates its own linked bank after callback return.
            RequireShape(current);
            return new(particle ? current.Particles! : current.Children, result.Result);
        }
    }

    private static void Validate(NativeBattleGuardianEffectPools pools)
    {
        RequireShape(pools);
        pools.Main.Validate();
        pools.Children.Validate();
        pools.Particles!.Validate();
    }

    private static void RequireShape(NativeBattleGuardianEffectPools pools)
    {
        if (!Matches(pools.Main, 0x21FF4A8, 0x21FF468, 16, 2) || !Matches(pools.Children, 0x21FFD58, 0x21FFD68, 36, 100) || !Matches(pools.Particles, 0x21FF458, 0x21FF3C8, 36, 4))
        {
            throw new InvalidDataException("Native596B70 requires its original returned, child and particle pool owners.");
        }
    }

    private static bool Matches(NativeBattleEffectTaskPool? pool, uint descriptor, uint bank, ushort stride, int count)
    {
        return pool is not null && pool.DescriptorAddress == descriptor && pool.BankAddress == bank && pool.Stride == stride && !pool.Slots.IsDefault && pool.Slots.Length == count;
    }
}
