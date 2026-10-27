using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216ControllerTests
{
    [Fact]
    public void FirstPassUsesSecondDrawBankAndNormalizesBeforeAnyChildren()
    {
        List<NativeBattleDuel216Operation> operations = [];
        NativeBattleDuel216Frame frame = NativeBattleDuel216Controller.Advance695AB0(Frame(0), new((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }), (_, _, _) => throw new InvalidOperationException());
        Assert.IsType<NativeBattleDuel216NormalizeBillboard>(Assert.Single(operations));
        Assert.Equal(119192, frame.Effect.DrawArenaOffset);
        Assert.Equal(0x20DFAB8u + 225688, U32(frame.Staging, 0x2508878));
        Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((byte)1, frame.Effect.Pools[0].Slots[0].ReadByte(15));
    }

    [Fact]
    public void BirthClears1529DwordsAndSchedulesTheActualFourBanksInTheSamePass()
    {
        List<uint> callbacks = [];
        NativeBattleDuel216Frame frame = NativeBattleDuel216Controller.Advance695AB0(Frame(1), new((current, operation) =>
        {
            if (operation is NativeBattleDuel216PrepareMain preparation)
            {
                Assert.Equal(0, preparation.Slot);
                NativeBattleModulePoolTask task = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel216Controller.Main).Slots[0];
                Assert.Equal(6116, task.Payload.Length);
                Assert.All(task.Payload, value => Assert.Equal((byte)0, value));
            }
            else
            {
                Assert.IsType<NativeBattleDuel216NormalizeBillboard>(operation);
            }
            return current;
        }), (current, pool, slot) =>
        {
            callbacks.Add(pool.Slots[slot].Callback);
            return new(current, pool, 0);
        });
        Assert.Equal(new uint[] { 0x6960E0 }, callbacks);
        Assert.Equal(5, frame.Effect.Pools.Length);
        Assert.Equal(new ushort[] { 16, 6128, 260, 36, 28 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 1, 8, 64, 32 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal((ushort)2, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(6128, 1));
    }

    [Fact]
    public void MainCompletionReleasesSchedulingAndPreservesStillActivePrivatePhysicalBanks()
    {
        NativeBattleDuel216Frame frame = Frame(2);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel216Controller.Main, 0x20DFAB8, 6128, 1);
        List<NativeBattleEffectTaskPool> banks = [main];
        foreach ((uint descriptor, int offset, ushort stride, ushort count) in new (uint, int, ushort, ushort)[] { (NativeBattleDuel216Controller.Secondary, 6128, 260, 8), (NativeBattleDuel216Controller.Particles, 8208, 36, 64), (NativeBattleDuel216Controller.Small, 10512, 28, 32) })
        {
            NativeBattleEffectTaskPool bank = NativeBattleEffectTaskPool.Initialize(descriptor, checked(0x20DFAB8u + (uint)offset), stride, count);
            (bank, int slot) = bank.Append(0x69A200);
            bank = bank with { Slots = bank.Slots.SetItem(slot, bank.Slots[slot].WriteWord(12, (ushort)offset)) };
            banks.Add(bank);
        }
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteByte(14, 1)) };
        frame = frame with { Effect = frame.Effect with { Pools = new[] { root }.Concat(banks).ToImmutableArray() }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, banks.ToImmutableArray()) };
        frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 11408) };
        foreach ((short file, short type) in new (short, short)[] { (360, 1), (361, 1), (362, 0) })
        {
            frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, file, 0x20DFAB8 + 12696, type) };
        }
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(11408, [1, 0, 1, 3, 2]) } };
        List<NativeBattleDuel216Operation> operations = [];
        frame = NativeBattleDuel216Controller.Advance695AB0(frame, new((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }), (current, pool, _) => new(current, pool, 0));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Single(frame.Effect.Pools);
        Assert.IsType<NativeBattleDuel216RestoreScene>(operations.Last());
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(6128, 2)));
        Assert.Equal((ushort)6128, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena.Read(6128 + 12, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena.Read(8208, 2)));
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleDuel216Frame>(JsonSerializer.Serialize(frame))));
    }

    private static NativeBattleDuel216Frame Frame(ushort counter)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel216Controller.Root, 0x25088D8, 16, 1);
        (root, _) = root.Append(0x695AB0);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(12, counter)) };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), EffectIo = new(0x534150, null, null) };
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), new(0, 0, 8, 0, 0, 0), NativeBattleDuel216Controller.Root) { Pools = [root] };
        return new(memory, effect, NativeBattleFileStaging.Unknown().Write(0x25088EC, new byte[4]), NativeCrtRandomState.FromColdProcess());
    }

    private static uint U32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
