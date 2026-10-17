namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect311ParticleInitialization(NativeBattleEffectTaskPool Pool, NativeCrtRandomState Random);

public static class NativeBattleEffect311Particles
{
    public static NativeBattleEffect311ParticleInitialization Initialize665848(NativeBattleEffectTaskPool pool, NativeCrtRandomState random)
    {
        pool.Validate();
        if (pool.DescriptorAddress != 0x25073D8 || pool.Stride != 28 || pool.Slots.Length != 64)
        {
            throw new InvalidDataException("Native665848 requires its original64-row28-byte particle bank.");
        }
        for (int index = 0; index < 64; index++)
        {
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                continue;
            }
            (pool, int slot) = pool.Append(0x665C30);
            NativeBattleModulePoolTask child = pool.Slots[slot];
            child = child.WriteWord(12, 0).WriteWord(14, (ushort)(50 - (Roll() & 7))).WriteWord(16, Roll()).WriteWord(20, Roll()).WriteWord(22, Roll()).WriteWord(24, Roll()).WriteWord(18, (ushort)((Roll() & 1023) + 1500));
            pool = pool with { Slots = pool.Slots.SetItem(slot, child) };
        }
        return new(pool, random);

        ushort Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }
}
