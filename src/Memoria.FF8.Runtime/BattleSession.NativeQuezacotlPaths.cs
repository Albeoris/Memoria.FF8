using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlPaths(NativeBattleGraphicsState graphics, NativeBattleGteDepthTrace depthTrace)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C7800 requires its retained effect memory.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C7800 lost its current module.");
        NativeBattleEffectTaskPool before = effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleQuezacotlPathProducer.Descriptor);
        if (before.Head is null)
        {
            return graphics;
        }
        NativeBattleEffectTaskStep stepped = before.Advance((current, index) =>
        {
            if (current.Slots[index].Callback != 0x6C7800)
            {
                throw new NotSupportedException($"Native Quezacotl path callback{current.Slots[index].Callback:X} is not implemented.");
            }
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C7800 requires its current ordering table.");
            NativeBattleQuezacotlPathStep path = NativeBattleQuezacotlPathCallbacks.Advance6C7800(memory, effect, current, index, frame.Pending, NativeCamera?.Projection ?? throw new NotSupportedException("Native6C7800 requires its prior projection registers."), depthTrace.DepthFifo4);
            memory = path.Memory;
            effect = path.Effect;
            depthTrace.ObserveFinal(path.DepthFifo4);
            graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = path.Ordering } };
            NativeEffectMemory = memory;
            NativeGraphics = graphics;
            return new(path.Pool, path.Returned);
        });
        NativeBattleEffectTaskPool pool = stepped.Pool;
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            int address = checked((int)(pool.BankAddress - NativeBattleQuezacotlPathProducer.ArenaAddress) + index * pool.Stride);
            if (before.Slots[index].Flags != pool.Slots[index].Flags)
            {
                byte[] flags = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(flags, pool.Slots[index].Flags);
                memory = memory with { Arena = memory.Arena!.Write(address, flags) };
            }
            if (before.Slots[index].Next != pool.Slots[index].Next)
            {
                memory = memory with { Arena = NativeBattleQuezacotlParticleProducer.Dword(memory.Arena!, address + 4, pool.Slots[index].Next is int next ? checked(pool.BankAddress + (uint)next * pool.Stride) : 0) };
            }
        }
        memory = memory with { QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(2, pool.Head is int head ? checked(pool.BankAddress + (uint)head * pool.Stride) : 0) };
        effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
        NativeEffectMemory = memory;
        return graphics with { OriginalEffect = effect };
    }
}
