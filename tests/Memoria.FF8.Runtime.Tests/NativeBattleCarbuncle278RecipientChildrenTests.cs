using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278RecipientChildrenTests
{
    [Fact]
    public void Initial_birth_uses_ordered_shared_random_draws_physical_parent_and_exact_clears()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278ChildBirth result = NativeBattleCarbuncle278RecipientChildren.Spawn6823F0(fixture.Frame, fixture.Parent, 0x20E1A64, fixture.Bounds);
        NativeBattleSharedEffectRandomState random = fixture.Frame.Memory.SharedEffectRandom!.Draw7059E0(out ushort angle);
        NativeBattleEffectTaskPool rays = result.Frame.Effect.Pools[0];
        for (int index = 0; index < 8; index++)
        {
            random = random.Draw7059E0(out ushort direction);
            random = random.Draw7059E0(out ushort drift);
            NativeBattleModulePoolTask task = rays.Slots[index];
            Assert.Equal(0x683760u, task.Callback);
            Assert.Equal((ushort)0, task.ReadWord(0xC));
            Assert.Equal((ushort)1024, task.ReadWord(0xE));
            Assert.Equal(unchecked((ushort)(angle + index * 512)), task.ReadWord(0x10));
            Assert.Equal((direction & 1) != 0 ? unchecked((ushort)-16) : (ushort)16, task.ReadWord(0x12));
            Assert.Equal(unchecked((ushort)(-37 - (drift & 31))), task.ReadWord(0x14));
            Assert.Equal((ushort)0, task.ReadWord(0x16));
            Assert.Equal(0x20E1A64u, task.ReadDword(0x18));
            Assert.Equal(task.Payload, result.Frame.Memory.Arena!.Read(0x1C04 + index * 0x1C + 12, 16));
        }
        random = random.Draw7059E0(out ushort seed);
        NativeBattleEffectTaskPool shards = result.Frame.Effect.Pools[1];
        for (int index = 0; index < 8; index++)
        {
            random = random.Draw7059E0(out ushort scale);
            NativeBattleModulePoolTask task = shards.Slots[index];
            Assert.Equal(0x683330u, task.Callback);
            Assert.Equal(unchecked((ushort)(seed + index * 0x155)), task.ReadWord(0xE));
            Assert.Equal((ushort)((scale & 1023) + 512), task.ReadWord(0x10));
            Assert.Equal((ushort)0xA5A5, task.ReadWord(0x16));
            Assert.Equal(0x20E1A64u, task.ReadDword(0x18));
        }
        Assert.Equal(random, result.Frame.Memory.SharedEffectRandom);
        Assert.Equal((ushort)0, result.Parent.ReadWord(0x54));
        Assert.Equal(unchecked((ushort)-100), result.Parent.ReadWord(0x56));
        Assert.Equal((ushort)200, result.Parent.ReadWord(0x58));
        Assert.All(fixture.Frame.Effect.Pools.SelectMany(pool => pool.Slots), task => Assert.Equal((ushort)0, task.Flags));
        NativeBattleCarbuncle278Frame independent = JsonSerializer.Deserialize<NativeBattleCarbuncle278Frame>(JsonSerializer.Serialize(fixture.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCarbuncle278RecipientChildren.Spawn6823F0(independent, fixture.Parent, 0x20E1A64, fixture.Bounds)));
    }

    [Fact]
    public void Paused_noninitial_callback_skips_shards_after_its_unconditional_seed_draw()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame paused = fixture.Frame with { Memory = fixture.Frame.Memory with { Carbuncle278 = fixture.Frame.Memory.Carbuncle278! with { Pause8114 = 1 } } };
        NativeBattleCarbuncle278ChildBirth result = NativeBattleCarbuncle278RecipientChildren.Spawn6823F0(paused, fixture.Parent.WriteWord(0xC, 16), 1, fixture.Bounds);
        Assert.All(result.Frame.Effect.Pools.SelectMany(pool => pool.Slots), task => Assert.Equal((ushort)0, task.Flags));
        Assert.Equal(paused.Memory.SharedEffectRandom!.Draw7059E0(out _), result.Frame.Memory.SharedEffectRandom);
        Assert.Equal(unchecked((ushort)-93), result.Parent.ReadWord(0x56));
        Assert.Equal((ushort)215, result.Parent.ReadWord(0x58));
        NativeBattleModulePoolTask earlier = fixture.Parent.WriteWord(0xC, 14);
        NativeBattleCarbuncle278ChildBirth skipped = NativeBattleCarbuncle278RecipientChildren.Spawn6823F0(fixture.Frame, earlier, 1, fixture.Bounds);
        Assert.Same(earlier, skipped.Parent);
        Assert.Same(fixture.Frame, skipped.Frame);
    }

    [Fact]
    public void Polar_reads_exact_wrapped_table_lanes_and_retains_signed_imul_word_stores()
    {
        byte[] bytes = new byte[16384];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), -4096);
        Assert.Equal(new NativeBattleVector(0, 50, -7), NativeBattleCarbuncle278RecipientChildren.Polar6836F0(new(-20, -40, -7), 4099, bytes.ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278RecipientChildren.Polar6836F0(new(0, 0, 0), 0, []));
    }

    [Theory]
    [InlineData(false, 16, 2)]
    [InlineData(false, 15, 0)]
    [InlineData(true, 16, 0)]
    public void Ray_finishes_after_draw_and_pause_preserves_all_task_bytes(bool paused, ushort age, int expected)
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x683760, new byte[16].ToImmutableArray());
        task = task.WriteWord(0xC, age).WriteWord(0xE, 1).WriteWord(0x10, 2).WriteWord(0x12, unchecked((ushort)-16)).WriteWord(0x14, unchecked((ushort)-37));
        NativeBattleCarbuncle278ChildStep result = NativeBattleCarbuncle278RecipientChildren.Finish683760(task, paused);
        Assert.Equal(expected, result.Result);
        if (paused || expected == 2)
        {
            Assert.Same(task, result.Task);
        }
        else
        {
            Assert.Equal((ushort)16, result.Task.ReadWord(0xC));
            Assert.Equal(unchecked((ushort)-14), result.Task.ReadWord(0x10));
            Assert.Equal(unchecked((ushort)-36), result.Task.ReadWord(0xE));
        }
        Assert.Equal(paused ? 0 : 2, NativeBattleCarbuncle278RecipientChildren.Finish683330(task, paused).Result);
    }

    private static (NativeBattleCarbuncle278Frame Frame, NativeBattleModulePoolTask Parent, NativeBattleStaticMeshResource Bounds) Fixture()
    {
        NativeBattleEffectTaskPool rays = NativeBattleEffectTaskPool.Initialize(0x2508138, 0x20DFAB8 + 0x1C04, 0x1C, 24);
        NativeBattleEffectTaskPool shards = NativeBattleEffectTaskPool.Initialize(0x2508128, 0x20DFAB8 + 4, 0x1C, 256);
        shards = shards with { Slots = shards.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 16).ToImmutableArray() }).ToImmutableArray() };
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60), new(0, 0, 3, 0, 0, 0), null) { Pools = [rays, shards] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleModulePoolTask parent = new(1, 0, null, 0x6822F0, new byte[256].ToImmutableArray());
        byte[] bounds = new byte[8 + 16 * 8];
        for (int vertex = 0; vertex < 16; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(8 + vertex * 8 + 2), (short)(-100 + vertex * 10));
            BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(8 + vertex * 8 + 4), (short)(200 + vertex * 20));
        }
        return (new(memory, effect, NativeCrtRandomState.FromColdProcess()), parent.WriteWord(0xC, 15), new(0, 0x10BD488, bounds.ToImmutableArray()));
    }
}
