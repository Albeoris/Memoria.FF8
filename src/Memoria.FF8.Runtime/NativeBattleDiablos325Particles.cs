using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDiablos325Particles
{
    public const uint Descriptor = 0x2505190;
    public const uint Callback = 0x659960;
    public const ushort Stride = 0xE8;
    public const ushort Capacity = 16;

    public static NativeBattleDiablos325Frame Initialize6548B9(NativeBattleDiablos325Frame frame)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        uint bank = checked(state.ModelBuffer5208 + 0x3505C);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(Descriptor, bank, Stride, Capacity);
        NativeBattleEffectArena arena = (frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos particles require their current arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(bank, Stride * Capacity), new byte[Stride * Capacity]);
        frame = Replace(frame with { Memory = frame.Memory with { Arena = arena } }, pool);
        short[] x = [-500, -200, 0, 200, 500];
        uint[] sources = [0x4AFAC, 0x4BBC4, 0x4B558, 0x4BBC4, 0x4AFAC];
        for (int index = 0; index < x.Length; index++)
        {
            frame = Spawn659900(frame, new(x[index], -8492, unchecked((short)state.FarZ51DC)), -128, checked(state.ModelBuffer5208 + sources[index]));
        }
        return frame;
    }

    public static NativeBattleDiablos325Frame Spawn659900(NativeBattleDiablos325Frame frame, NativeBattleVector position, short parameter, uint programme)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(item => item.DescriptorAddress == Descriptor);
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int index) = pool.Append(Callback);
        NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, unchecked((ushort)position.X)).WriteWord(0xE, unchecked((ushort)position.Y)).WriteWord(0x10, unchecked((ushort)position.Z)).WriteWord(0x12, unchecked((ushort)parameter));
        NativeBattleGfAnimationSource source = ReadSource(frame, programme);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x18 - 12), 0xD0);
        workspace = workspace.Initialize7016B0(source, 0xD0);
        pool = pool.WriteSlot(index, workspace.CopyTo(task, 12 + (0x18 - 12), retainUnknown: true));
        return Synchronize(Replace(frame, pool), pool);
    }

    public static NativeBattleDiablos325Frame Advance659960(NativeBattleDiablos325Frame frame, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleDiablos325Frame, NativeBattleGfGeometryBatch, NativeBattleDiablos325Frame> submit)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(item => item.DescriptorAddress == Descriptor);
        if (pool.Stride != Stride || pool.Slots.Length != Capacity)
        {
            throw new InvalidDataException("Native Diablos particles require their original physical task dimensions.");
        }
        NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Normalize67AAE0(projection.View ?? throw new NotSupportedException("Native Diablos particle preparation requires its current camera basis."));
        NativeBattleDiablos325Resources resources = frame.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos particles require their original selector tables.");
        ImmutableArray<byte>? primitives = resources.ParticlePrimitiveKinds;
        ImmutableArray<byte>? textures = resources.ParticleTextureKinds;
        NativeBattleEffectTaskStep advanced = pool.Advance((current, index) =>
        {
            NativeBattleModulePoolTask task = current.Slots[index];
            if (task.Callback == 0x659860)
            {
                return Advance659860(frame, current, index, projection, billboard, colors, submit, next => frame = next);
            }
            if (task.Callback != Callback)
            {
                throw new NotSupportedException("Native Diablos particle callback was replaced.");
            }
            uint pause = NativeBattleDiablos325Entry.RequireState(frame.Memory).Pause5180;
            NativeBattleVector position = new(unchecked((short)task.ReadWord(0xC)), unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)));
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x18 - 12), 0xD0);
            NativeBattleGfAnimationSource source = ReadSource(frame, workspace.ResourceAddress);
            NativeBattleGfAnimationAdvance programme = workspace.Advance701970(source, unchecked((int)pause), (cache, node) =>
            {
                NativeBattleDiablos325NodeDraw drawn = NativeBattleDiablos325Programme.Emit656B70(frame.Memory, source, node, projection, colors, position, unchecked((short)task.ReadWord(0x12)), 0, primitives, textures, billboard);
                frame = submit(frame with { Memory = drawn.Memory }, drawn.Packets);
                return cache;
            });
            task = programme.Workspace.CopyTo(task, 12 + (0x18 - 12), retainUnknown: true);
            if (programme.Remaining != 0 && pause == 0)
            {
                task = task.WriteWord(0xE, unchecked((ushort)(task.ReadWord(0xE) + 24)));
            }
            return new(current.WriteSlot(index, task), programme.Remaining == 0 ? 2 : 0);
        });
        return Synchronize(Replace(frame, advanced.Pool), advanced.Pool);
    }

    private static NativeBattleGfAnimationSource ReadSource(NativeBattleDiablos325Frame frame, uint address)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleEffectFileAsset file = frame.Effect.Binding.Diablos325Resources?.Files.SingleOrDefault(item => item.FileId == 730) ?? throw new NotSupportedException("Native Diablos particle programme requires its original streamed resource owner.");
        uint start = checked(state.ModelBuffer5208 + 0x3505C);
        uint end = checked(start + (uint)file.Data.Length);
        if (address < start || address >= end)
        {
            throw new InvalidDataException("Native Diablos particle programme leaves its current streamed resource.");
        }
        int length = checked((int)(end - address));
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos particle programme requires its current arena bytes.");
        return NativeBattleGfAnimationSource.Borrow(address, arena, NativeBattleWaterExecutionFields.ArenaOffset(address, length), length);
    }

    private static NativeBattleDiablos325Frame Replace(NativeBattleDiablos325Frame frame, NativeBattleEffectTaskPool pool)
    {
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools.Any(item => item.DescriptorAddress == pool.DescriptorAddress) ? frame.Effect.Pools.Select(item => item.DescriptorAddress == pool.DescriptorAddress ? pool : item).ToImmutableArray() : frame.Effect.Pools.Add(pool);
        return frame with { Effect = frame.Effect with { Pools = pools } };
    }

    private static NativeBattleDiablos325Frame Synchronize(NativeBattleDiablos325Frame frame, NativeBattleEffectTaskPool pool)
    {
        using NativeBattleEffectArenaWriter writer = new(frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos particle bank requires its current arena."));
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            NativeBattleModulePoolTask task = pool.Slots[index];
            byte[] bytes = new byte[pool.Stride];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, task.Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), task.State);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), task.Next is int next ? checked(pool.BankAddress + (uint)(next * pool.Stride)) : 0);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), task.Callback);
            task.ReadRawPayloadSpan().CopyTo(bytes.AsSpan(12));
            writer.Write(NativeBattleWaterExecutionFields.ArenaOffset(checked(pool.BankAddress + (uint)(index * pool.Stride)), bytes.Length), bytes);
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() } };
    }
}
