using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlFragments(NativeBattleGraphicsState graphics, NativeBattleGteDepthTrace depthTrace)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C6660 requires its current effect memory.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C6660 lost its module state.");
        NativeBattleEffectTaskPool before = effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleQuezacotlFragmentPool.Descriptor) ?? throw new NotSupportedException("Native6C6660 requires its current EXE task pool.");
        NativeBattleQuezacotlFragmentPool.Validate(memory, before);
        NativeBattleEffectTaskStep stepped = before.Advance((current, index) =>
        {
            if (current.Slots[index].Callback != NativeBattleQuezacotlFragmentPool.Callback)
            {
                throw new NotSupportedException($"Native Quezacotl fragment callback{current.Slots[index].Callback:X} is not implemented.");
            }
            ImmutableArray<byte> raw = NativeBattleQuezacotlFragmentPool.ReadTask(memory, index);
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C6660 requires its prior projection registers.");
            NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlFragmentPackets.Emit6C6660(memory, raw, projection, depthTrace.Push);
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C6660 requires its current ordering table.");
            NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(memory, effect, frame.Pending, batch);
            memory = written.Memory;
            effect = written.Effect;
            bool paused = (memory.QuezacotlPause ?? throw new NotSupportedException("Native6C66F2 requires its retained pause DWORD.")) != 0;
            NativeBattleQuezacotlFragmentMotion moved = NativeBattleQuezacotlFragmentPhysics.Advance6C6707(raw, paused);
            if (!paused)
            {
                memory = NativeBattleQuezacotlFragmentPool.WriteTask(memory, index, moved.Task44);
            }
            current = current with { Slots = current.Slots.SetItem(index, current.Slots[index] with { Payload = moved.Task44.AsSpan().Slice(12).ToArray().ToImmutableArray() }) };
            effect = effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == current.DescriptorAddress ? current : pool).ToImmutableArray() };
            graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = written.Ordering } };
            NativeEffectMemory = memory;
            NativeGraphics = graphics;
            return new(current, moved.Returned);
        });
        NativeBattleEffectTaskPool pool = stepped.Pool;
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            uint address = NativeBattleQuezacotlFragmentPool.Bank + (uint)index * pool.Stride;
            if (before.Slots[index].Flags != pool.Slots[index].Flags)
            {
                byte[] flags = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(flags, pool.Slots[index].Flags);
                memory = memory with { QuezacotlExecutableMemory = memory.QuezacotlExecutableMemory!.Write(address, flags) };
            }
            if (before.Slots[index].Next != pool.Slots[index].Next)
            {
                memory = NativeBattleQuezacotlFragmentPool.WriteDword(memory, address + 4, pool.Slots[index].Next is int next ? NativeBattleQuezacotlFragmentPool.Bank + (uint)next * pool.Stride : 0);
            }
        }
        memory = memory with { QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(4, pool.Head is int head ? NativeBattleQuezacotlFragmentPool.Bank + (uint)head * pool.Stride : 0) };
        effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
        NativeEffectMemory = memory;
        graphics = graphics with { OriginalEffect = effect };
        NativeGraphics = graphics;
        return graphics;
    }
}
