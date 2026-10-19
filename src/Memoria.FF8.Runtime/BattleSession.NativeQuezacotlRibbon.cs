using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlRibbon(NativeBattleGraphicsState graphics, NativeBattleGteDepthTrace depthTrace)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C8850 requires its retained arena/process owners.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C8850 lost its module state.");
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521728);
        if (pool.Head is null)
        {
            return graphics;
        }
        NativeBattleQuezacotlRibbonResources resources = effect.Binding.NativeQuezacotlResources?.Ribbon ?? throw new NotSupportedException("Native6C8850 requires its authenticated ribbon control and lookup resources; explicitly migrate this standalone's resource binding.");
        resources.Validate();
        NativeBattleStackFrame? callbackStack = memory.ExecutionStack is { } stack ? NativeBattleQuezacotlRibbon.ResolveEntryStack6C8850(stack, graphics.EntryLifecycle) : null;
        //6C8A9B writes this caller-local pointer into the slot later reused as6C8C50 direction scratch.
        uint? incomingDirection = callbackStack?.AddressAtOffset(-0x38);
        NativeBattleQuezacotlRigWrite composed = NativeBattleQuezacotlRig86.Compose5095B0(memory, effect);
        memory = composed.Memory;
        effect = composed.Effect;
        NativeEffectMemory = memory;
        graphics = graphics with { OriginalEffect = effect };
        NativeGraphics = graphics;
        NativeBattleEffectTaskPool before = pool;
        using NativeBattleEffectArenaTransaction transaction = new(memory.Arena ?? throw new NotSupportedException("Native ribbon pool requires its retained arena."));
        using NativeBattleLogicalOrderingTransaction ordering = new((graphics.PrimitiveFrame ?? throw new NotSupportedException("Native ribbon pool requires its retained ordering frame.")).Pending);
        using NativeBattleCommonPacketTransaction common = new(memory.PrimitiveProcess ?? throw new NotSupportedException("Native ribbon pool requires its retained common packet owner."));
        memory = memory with { Arena = transaction.View, PrimitiveProcess = common.View };
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.View } };
        NativeBattleEffectMemory committedMemory = memory;
        NativeBattleGraphicsState committedGraphics = graphics;
        bool checkpointActive = false;
        try
        {
            NativeBattleEffectTaskStep stepped = pool.Advance((current, index) =>
            {
                transaction.BeginCheckpoint();
                ordering.BeginCheckpoint();
                common.BeginCheckpoint();
                checkpointActive = true;
                if (current.Slots[index].Callback != 0x6C8850)
                {
                    throw new NotSupportedException($"Native Quezacotl ribbon pool callback{current.Slots[index].Callback:X} is not implemented.");
                }
                int address = checked((int)(current.BankAddress - 0x20DFAB8) + index * current.Stride);
                ImmutableArray<byte> task = memory.Arena!.Read(address, current.Stride).ToImmutableArray();
                uint pair = BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan().Slice(0x14));
                uint controls = BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan().Slice(0x18));
                uint directory = BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan().Slice(0x1C));
                if (directory != 0x20DFB7C)
                {
                    throw new NotSupportedException("Native6C8850 requires its actual bound control rows and current rig directory pointer.");
                }
                NativeBattleQuezacotlRibbonBank bank = resources.ResolveBank(pair, controls);
                byte[] envelopeBytes = memory.Arena.Read(0x3C25C, 32);
                ImmutableArray<short> envelope = Enumerable.Range(0, 16).Select(point => BinaryPrimitives.ReadInt16LittleEndian(envelopeBytes.AsSpan(point * 2))).ToImmutableArray();
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C8850 requires its prior camera projection.");
                NativeBattleQuezacotlRibbonStep ribbon = NativeBattleQuezacotlRibbon.Advance6C8850(task, resources, bank, (int)((pair - bank.PairAddress) / 8), selector => NativeBattleQuezacotlRig86.ReadBoneMatrix(memory, selector), envelope, effect.Quezacotl!.Random17A0, memory.QuezacotlPause != 0, projection.View ?? throw new NotSupportedException("Native6C8850 requires its prior view matrix."), projection, depthTrace.Push, incomingDirection);
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C8850 requires its current ordering table.");
                NativeBattleQuezacotlSkinBatch packets = new(ribbon.CommonPackets.Attempts.Select(attempt => new NativeBattleQuezacotlSkinWrite(NativeBattleQuezacotlSkinBank.Common, attempt)).ToImmutableArray(), ribbon.CommonPackets.AllocationBytes, 0);
                NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlSkinArena.Append(memory, effect, frame.Pending, packets);
                memory = written.Memory;
                effect = written.Effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = ribbon.Random17A0 } };
                NativeBattleEffectPacketWrite taskWrite = NativeBattleEffectPacketArena.Append(memory, address, written.Ordering, new([ribbon.TaskWrites], 0));
                memory = taskWrite.Memory;
                current = current with { Slots = current.Slots.SetItem(index, current.Slots[index] with { Payload = ribbon.Task128.AsSpan().Slice(12).ToArray().ToImmutableArray() }) };
                effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == current.DescriptorAddress ? current : existing).ToImmutableArray() };
                graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = taskWrite.Ordering } };
                transaction.CommitCheckpoint();
                ordering.CommitCheckpoint();
                common.CommitCheckpoint();
                checkpointActive = false;
                committedMemory = memory;
                committedGraphics = graphics;
                return new(current, ribbon.Returned);
            });
            transaction.BeginCheckpoint();
            ordering.BeginCheckpoint();
            common.BeginCheckpoint();
            checkpointActive = true;
            pool = stepped.Pool;
            for (int index = 0; index < pool.Slots.Length; index++)
            {
                int address = checked((int)(pool.BankAddress - 0x20DFAB8) + index * pool.Stride);
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
            memory = memory with { QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(0, pool.Head is int head ? checked(pool.BankAddress + (uint)head * pool.Stride) : 0) };
            effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
            NativeBattleQuezacotlRigWrite restored = NativeBattleQuezacotlRig86.Restore508C90(memory, effect);
            memory = restored.Memory;
            graphics = graphics with { OriginalEffect = restored.Effect };
            transaction.CommitCheckpoint();
            ordering.CommitCheckpoint();
            common.CommitCheckpoint();
            checkpointActive = false;
        }
        catch
        {
            if (checkpointActive)
            {
                transaction.RollbackCheckpoint();
                ordering.RollbackCheckpoint();
                common.RollbackCheckpoint();
            }
            memory = committedMemory;
            graphics = committedGraphics;
            throw;
        }
        finally
        {
            NativeEffectMemory = memory with { Arena = transaction.Freeze(), PrimitiveProcess = common.Freeze(memory.PrimitiveProcess!) };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.Freeze() } };
            NativeGraphics = graphics;
        }
        return graphics;
    }
}
