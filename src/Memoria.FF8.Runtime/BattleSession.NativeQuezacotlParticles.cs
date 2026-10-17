using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlParticles(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C7EA0 requires its retained effect memory.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C7EA0 lost its current module.");
        NativeBattleEffectTaskPool before = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521718);
        if (before.Head is null)
        {
            return graphics;
        }
        NativeBattleEffectTaskStep stepped = before.Advance((current, index) =>
        {
            if (current.Slots[index].Callback != 0x6C7EA0)
            {
                throw new NotSupportedException($"Native Quezacotl particle callback{current.Slots[index].Callback:X} is not implemented.");
            }
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C7EA0 requires its current ordering table.");
            NativeBattleQuezacotlParticleStep particle = NativeBattleQuezacotlParticleCallbacks.Advance6C7EA0(memory, effect, current, index, frame.Pending, NativeCamera?.Projection ?? throw new NotSupportedException("Native6C7EA0 requires its prior projection."));
            memory = particle.Memory;
            effect = particle.Effect;
            graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = particle.Ordering } };
            NativeEffectMemory = memory;
            NativeGraphics = graphics;
            return new(particle.Pool, particle.Returned);
        });
        NativeBattleEffectTaskPool pool = stepped.Pool;
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            int address = checked((int)(pool.BankAddress - NativeBattleQuezacotlParticleProducer.ArenaAddress) + index * pool.Stride);
            if (before.Slots[index].Flags != pool.Slots[index].Flags)
            {
                byte[] flags = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(flags, pool.Slots[index].Flags);
                memory = memory with { Arena = memory.Arena!.Write(address, flags) };
            }
            if (before.Slots[index].Next != pool.Slots[index].Next)
            {
                byte[] pointer = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(pointer, pool.Slots[index].Next is int next ? checked(pool.BankAddress + (uint)next * pool.Stride) : 0);
                memory = memory with { Arena = memory.Arena!.Write(address + 4, pointer) };
            }
        }
        memory = memory with { QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(1, pool.Head is int head ? checked(pool.BankAddress + (uint)head * pool.Stride) : 0) };
        effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
        NativeEffectMemory = memory;
        graphics = graphics with { OriginalEffect = effect };
        NativeGraphics = graphics;
        return graphics;
    }
}
