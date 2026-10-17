using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlPathControllerTests
{
    private const int Row = NativeBattleQuezacotlPathProducer.Rows;
    private const int Bank = 0x3F200;
    private static readonly NativeBattleActionMatrix Captured = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4000, 4096);

    [Fact]
    public void ProducerInitializesAllTaskBytesButPreservesUnwrittenRowBytesAndCurrentFlags()
    {
        var (memory, effect, paths) = Fixture();
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Flags = 0xA500 }) };
        effect = effect with { Pools = [pool] };
        memory = memory with { Arena = memory.Arena!.MarkUnknown(Bank, 32).MarkUnknown(Row + 4, 16) };
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        Assert.Equal(0, spawned.Slot);
        Assert.Equal(Row, spawned.Row);
        byte[] raw = spawned.Memory.Arena!.Read(Bank, 32);
        Assert.Equal((ushort)0xA501, BinaryPrimitives.ReadUInt16LittleEndian(raw));
        Assert.Equal(new byte[6], raw.AsSpan(2, 6).ToArray());
        Assert.Equal(0x6C7800u, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));
        Assert.Equal((short)1, I16(spawned.Memory, Bank + 12));
        Assert.Equal((short)260, I16(spawned.Memory, Bank + 14));
        ushort random = 1;
        int intensity = (NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random) & 1023) + (NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random) & 511) + 256;
        Assert.Equal(random, spawned.Effect.Quezacotl!.Random17A0);
        Assert.Equal((short)intensity, I16(spawned.Memory, Bank + 16));
        Assert.Equal((short)-(intensity >> 3), I16(spawned.Memory, Bank + 18));
        Assert.Equal(paths.Address + 84, U32(spawned.Memory, Bank + 20));
        Assert.Throws<NotSupportedException>(() => spawned.Memory.Arena.Read(Row + 4, 16));
        Assert.Equal(raw.AsSpan(12).ToArray(), spawned.Effect.Pools[0].Slots[0].Payload.ToArray());
    }

    [Fact]
    public void FullTaskPoolLeaksReservedRowAndStillConsumesTwoPrivateDraws()
    {
        var (memory, effect, paths) = Fixture();
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        for (int index = 0; index < 128; index++)
        {
            (pool, _) = pool.Append(0x6C7800);
        }
        NativeBattleQuezacotlPathSpawn failed = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect with { Pools = [pool] }, paths, 0);
        Assert.Null(failed.Slot);
        Assert.Equal(Row, failed.Row);
        Assert.Equal((short)0, I16(failed.Memory, Row));
        Assert.Equal((short)260, I16(failed.Memory, Row + 2));
        Assert.Equal(0u, failed.Memory.QuezacotlSecondaryHeads!.Value[2]);
        ushort expected = 1;
        _ = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref expected);
        _ = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref expected);
        Assert.Equal(expected, failed.Effect.Quezacotl!.Random17A0);
    }

    [Fact]
    public void FourAttemptProducerConsumesTwelveDrawsOnFullRowsButPauseConsumesNone()
    {
        var (memory, effect, _) = Fixture();
        byte[] full = new byte[512 * 28];
        memory = memory with { Arena = memory.Arena!.Write(Row, full) };
        NativeBattleQuezacotlPathSpawn failed = NativeBattleQuezacotlPathProducer.SpawnFour6C45AA(memory, effect);
        Assert.Null(failed.Row);
        Assert.Null(failed.Slot);
        ushort expected = 1;
        for (int index = 0; index < 12; index++)
        {
            _ = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref expected);
        }
        Assert.Equal(expected, failed.Effect.Quezacotl!.Random17A0);
        NativeBattleQuezacotlPathSpawn paused = NativeBattleQuezacotlPathProducer.SpawnFour6C45AA(memory with { QuezacotlPause = 1 }, effect with { Binding = effect.Binding with { NativeQuezacotlResources = null } });
        Assert.Equal((ushort)1, paused.Effect.Quezacotl!.Random17A0);
        Assert.Equal(memory.Arena, paused.Memory.Arena);
    }

    [Fact]
    public void CachedRowPrecedesFirstFreeWithoutReadingItsOldProjectedBytes()
    {
        var (memory, _, _) = Fixture();
        int selected = Row + 5 * 28;
        memory = memory with { Arena = Dword(memory.Arena!, NativeBattleQuezacotlPathProducer.CachedRow, NativeBattleQuezacotlPathProducer.ArenaAddress + (uint)selected).MarkUnknown(selected + 4, 16) };
        NativeBattleQuezacotlParticleRow allocated = NativeBattleQuezacotlPathProducer.Allocate6C7790(memory, -2, -3, 1000, -99);
        Assert.Equal(selected, allocated.Row);
        Assert.Equal((short)-2, I16(allocated.Memory, selected));
        Assert.Equal((short)-3, I16(allocated.Memory, selected + 2));
        Assert.Equal((short)1000, I16(allocated.Memory, selected + 20));
        Assert.Equal((short)-99, I16(allocated.Memory, selected + 22));
        Assert.Equal(0u, U32(allocated.Memory, selected + 24));
        Assert.Equal(0u, U32(allocated.Memory, NativeBattleQuezacotlPathProducer.CachedRow));
        Assert.Throws<NotSupportedException>(() => allocated.Memory.Arena!.Read(selected + 4, 16));
        Assert.Equal((short)-1, I16(allocated.Memory, Row));
    }

    [Fact]
    public void CallbackAddsFourRowsDecaysThenDrawsUsingCapturedMatrixAndActualProjectionFifo()
    {
        var (memory, effect, paths) = Fixture();
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        NativeBattleQuezacotlPathStep step = Step(spawned.Memory, spawned.Effect);
        Assert.Equal(0, step.Returned);
        Assert.Equal((ushort)5, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(paths.Address + 92, U32(step.Memory, Bank + 20));
        Assert.Equal((short)(I16(spawned.Memory, Row + 20) + I16(spawned.Memory, Row + 22)), I16(step.Memory, Row + 20));
        Assert.Equal(4, step.Ordering.Packets.Length);
        Assert.All(step.DepthFifo4, depth => Assert.Equal((int?)5000, depth));
        Assert.All(step.Ordering.Packets, packet => Assert.Equal(0x3Eu, packet.Words[1] >> 24));
        NativeBattleQuezacotlPathStep restored = JsonSerializer.Deserialize<NativeBattleQuezacotlPathStep>(JsonSerializer.Serialize(step))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void PauseDrawsCurrentRowsButDoesNotReadPathMetadataOrAdvanceRandomAndDecay()
    {
        var (memory, effect, paths) = Fixture();
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        NativeBattleQuezacotlPathStep first = Step(spawned.Memory, spawned.Effect);
        NativeBattleOriginalEffectState pausedEffect = first.Effect with { Binding = first.Effect.Binding with { NativeQuezacotlResources = first.Effect.Binding.NativeQuezacotlResources! with { Paths = null } } };
        NativeBattleQuezacotlPathStep paused = Step(first.Memory with { QuezacotlPause = 1 }, pausedEffect);
        Assert.Equal(first.Effect.Quezacotl!.Random17A0, paused.Effect.Quezacotl!.Random17A0);
        Assert.Equal(first.Pool.Slots[0].Payload.ToArray(), paused.Pool.Slots[0].Payload.ToArray());
        Assert.Equal(I16(first.Memory, Row + 20), I16(paused.Memory, Row + 20));
        Assert.NotEmpty(paused.Ordering.Packets);
    }

    [Fact]
    public void ShortSentinelPathFreesItsWholeChainBeforeDrawingOrTakingWidthRandom()
    {
        var (memory, effect, paths) = Fixture();
        byte[] data = paths.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(86), short.MaxValue);
        paths = paths with { Data = data.ToImmutableArray() };
        effect = effect with { Binding = effect.Binding with { NativeQuezacotlResources = effect.Binding.NativeQuezacotlResources! with { Paths = paths } } };
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        NativeBattleQuezacotlPathStep released = Step(spawned.Memory, spawned.Effect);
        Assert.Equal(2, released.Returned);
        Assert.Equal((short)-1, I16(released.Memory, Row));
        Assert.Equal(spawned.Effect.Quezacotl!.Random17A0, released.Effect.Quezacotl!.Random17A0);
        Assert.Empty(released.Ordering.Packets);
        Assert.Equal(NativeBattleQuezacotlPathProducer.ArenaAddress + (uint)Row, U32(released.Memory, NativeBattleQuezacotlPathProducer.CachedRow));
    }

    [Fact]
    public void RowExhaustionDuringCallbackFreesCurrentChainAndReturnsTwo()
    {
        var (memory, effect, paths) = Fixture();
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        byte[] full = new byte[511 * 28];
        memory = spawned.Memory with { Arena = spawned.Memory.Arena!.Write(Row + 28, full) };
        NativeBattleQuezacotlPathStep failed = Step(memory, spawned.Effect);
        Assert.Equal(2, failed.Returned);
        Assert.Equal((short)-1, I16(failed.Memory, Row));
        Assert.NotEqual(spawned.Effect.Quezacotl!.Random17A0, failed.Effect.Quezacotl!.Random17A0);
        Assert.Empty(failed.Ordering.Packets);
        Assert.Equal(0x006C78B8u, failed.FinalOutlineDirectionDword);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Branch_call_saves_EBX_two_in_outline_scratch_even_when_producer_fails_or_current_task_releases_before_drawing(bool exhaustRows)
    {
        var (memory, effect, paths) = Fixture();
        byte[] data = paths.Data.ToArray();
        short[] branch = [-2, 5, short.MaxValue, short.MaxValue];
        for (int index = 0; index < branch.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(84 + index * 2), branch[index]);
        }
        paths = paths with { Data = data.ToImmutableArray() };
        effect = effect with { Binding = effect.Binding with { NativeQuezacotlResources = effect.Binding.NativeQuezacotlResources! with { Paths = paths } } };
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        memory = exhaustRows ? spawned.Memory with { Arena = spawned.Memory.Arena!.Write(Row + 28, new byte[511 * 28]) } : spawned.Memory;
        NativeBattleQuezacotlPathStep released = NativeBattleQuezacotlPathCallbacks.Advance6C7800(memory, spawned.Effect, spawned.Effect.Pools[0], 0, NativeBattleLogicalOrdering.Clear(), new(null, 216, 160, 108), default, 0xDEADBEEF);
        Assert.Equal(2, released.Returned);
        Assert.Equal(2u, released.FinalOutlineDirectionDword);
        Assert.Empty(released.Ordering.Packets);
    }

    [Fact]
    public void Allocator_return_address_replaces_branch_saved_direction_before_degenerate_outline_and_trim_preserves_it()
    {
        var (memory, effect, paths) = Fixture();
        byte[] data = paths.Data.ToArray();
        short[] branch = [-4, 5, 6, short.MaxValue, 1, 2, 3, 4, 5, 6];
        for (int index = 0; index < branch.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(84 + index * 2), branch[index]);
        }
        paths = paths with { Data = data.ToImmutableArray() };
        NativeBattleQuezacotlResources resources = effect.Binding.NativeQuezacotlResources!;
        effect = effect with { Binding = effect.Binding with { NativeQuezacotlResources = resources with { Paths = paths, Ribbon = resources.Ribbon! with { DistanceBytes16384 = new byte[16384].ToImmutableArray() } } } };
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        NativeBattleQuezacotlPathStep step = Step(spawned.Memory, spawned.Effect);
        Assert.Equal(0x006C78B8u, step.FinalOutlineDirectionDword);
        Assert.Equal((ushort)5, step.Pool.Slots[0].ReadWord(12));
        Assert.NotEmpty(step.Ordering.Packets);
    }

    [Fact]
    public void Paused_path_without_any_prior_scratch_write_stays_strict_and_reports_exact_task_context()
    {
        var (memory, effect, paths) = Fixture();
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        NativeBattleQuezacotlPathStep first = Step(spawned.Memory, spawned.Effect);
        NativeBattleQuezacotlResources resources = first.Effect.Binding.NativeQuezacotlResources!;
        effect = first.Effect with { Binding = first.Effect.Binding with { NativeQuezacotlResources = resources with { Ribbon = resources.Ribbon! with { DistanceBytes16384 = new byte[16384].ToImmutableArray() } } } };
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Step(first.Memory with { QuezacotlPause = 1 }, effect));
        Assert.Contains("task0", error.Message);
        Assert.Contains("count5", error.Message);
        Assert.Contains("directionunknown", error.Message);
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    [Fact]
    public void NegativeControlSpawnsItsChildAndTheSharedTraversalRunsThatChildInTheSamePass()
    {
        var (memory, effect, paths) = Fixture();
        byte[] data = paths.Data.ToArray();
        short[] branch = [-4, 5, 6, short.MaxValue, 1, 2, 3, 4, 5, 6];
        for (int index = 0; index < branch.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(84 + index * 2), branch[index]);
        }
        paths = paths with { Data = data.ToImmutableArray() };
        effect = effect with { Binding = effect.Binding with { NativeQuezacotlResources = effect.Binding.NativeQuezacotlResources! with { Paths = paths } } };
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        memory = spawned.Memory;
        effect = spawned.Effect;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ImmutableArray<int?> fifo = default;
        List<int> visited = [];
        NativeBattleEffectTaskStep stepped = effect.Pools[0].Advance((pool, index) =>
        {
            visited.Add(index);
            NativeBattleQuezacotlPathStep result = NativeBattleQuezacotlPathCallbacks.Advance6C7800(memory, effect, pool, index, ordering, new(null, 216, 160, 108), fifo);
            memory = result.Memory;
            effect = result.Effect;
            ordering = result.Ordering;
            fifo = result.DepthFifo4;
            return new(result.Pool, result.Returned);
        });
        Assert.Equal(new[] { 0, 1 }, visited);
        Assert.Equal((ushort)0, stepped.Pool.Slots[1].Flags);
        Assert.Equal((ushort)5, stepped.Pool.Slots[0].ReadWord(12));
        Assert.Equal((short)-1, I16(memory, Row + 28));
        Assert.Equal((short)0, I16(memory, Row));
        ushort expected = 1;
        for (int index = 0; index < 8; index++)
        {
            _ = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref expected);
        }
        Assert.Equal(expected, effect.Quezacotl!.Random17A0);
    }

    [Fact]
    public void Retained1774HasOnlyAnExplicitColdProducerAndSurvivesEffectArenaLoads()
    {
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.Equal(0, cold.QuezacotlTargetY1774);
        Assert.Equal(-4096, (cold with { QuezacotlTargetY1774 = -4096 }).BeginLoad(116).QuezacotlTargetY1774);
        NativeBattleEffectMemory unknown = cold with { QuezacotlTargetY1774 = null };
        string json = JsonSerializer.Serialize(unknown);
        Assert.DoesNotContain("QuezacotlTargetY1774", json);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!.BeginLoad(116).QuezacotlTargetY1774);
    }

    [Fact]
    public void ExpiredHeadTrimmingReturnsTwoAtOneRemainingRowWithoutFreeingThatFinalRow()
    {
        var (memory, effect, paths) = Fixture();
        NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76C0(memory, effect, paths, 0);
        NativeBattleQuezacotlPathStep first = Step(spawned.Memory, spawned.Effect);
        NativeBattleEffectArena arena = Dword(first.Memory.Arena!, Bank + 20, 0);
        for (int index = 0; index < 5; index++)
        {
            arena = Word(arena, Row + index * 28 + 20, 0);
        }
        NativeBattleEffectTaskPool pool = first.Pool with { Slots = first.Pool.Slots.SetItem(0, first.Pool.Slots[0] with { Payload = arena.Read(Bank + 12, 20).ToImmutableArray() }) };
        NativeBattleQuezacotlPathStep trimmed = Step(first.Memory with { Arena = arena }, first.Effect with { Pools = [pool] });
        Assert.Equal(2, trimmed.Returned);
        Assert.Equal((ushort)1, trimmed.Pool.Slots[0].ReadWord(12));
        Assert.Equal(NativeBattleQuezacotlPathProducer.ArenaAddress + (uint)(Row + 4 * 28), U32(trimmed.Memory, Bank + 24));
        Assert.Equal(NativeBattleQuezacotlPathProducer.ArenaAddress + (uint)(Row + 3 * 28), U32(trimmed.Memory, NativeBattleQuezacotlPathProducer.CachedRow));
        Assert.Equal((short)4, I16(trimmed.Memory, Row + 4 * 28));
        Assert.Equal((short)-1, I16(trimmed.Memory, Row + 3 * 28));
    }

    internal static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleQuezacotlPathResources Paths) Fixture()
    {
        byte[] data = new byte[1944];
        BinaryPrimitives.WriteInt16LittleEndian(data, 40);
        for (int index = 0; index < 40; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2 + index * 2), 41);
        }
        for (short index = 0; index < 20; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(82 + index * 2), index);
        }
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(122), short.MaxValue);
        NativeBattleQuezacotlPathResources paths = new(0x12AFE58, data.ToImmutableArray());
        paths.Validate();
        byte[] rows = new byte[512 * 28];
        for (int index = 0; index < 512; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(index * 28), -1);
        }
        byte[] vertices = new byte[NativeBattleQuezacotlExecutableMemory.Length];
        for (short index = 0; index < 20; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(8 + index * 8), (short)(index * 100));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(10 + index * 8), (short)(index * 50));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(12 + index * 8), 1000);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { Arena = memory.Arena!.Write(Row, rows), QuezacotlExecutableMemory = new(vertices.ToImmutableArray()), QuezacotlCapturedMatrix1780 = Captured };
        NativeBattleQuezacotlResources resources = new(new("mag115.tim", []), new("test0", []), new("test1", []), new("test", [], [])) { Paths = paths, Ribbon = new([], [], [], Enumerable.Repeat((byte)100, 16384).ToImmutableArray()) };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleQuezacotlPathProducer.Descriptor, NativeBattleQuezacotlPathProducer.BankAddress, 0x20, 128);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3640, 0x6C3560) { NativeQuezacotlResources = resources }, null, null) { Pools = [pool], Quezacotl = new(1, 0, 3690), DrawArenaOffset = 0x2E50 };
        return (memory, effect, paths);
    }

    private static NativeBattleQuezacotlPathStep Step(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        return NativeBattleQuezacotlPathCallbacks.Advance6C7800(memory, effect, effect.Pools[0], 0, NativeBattleLogicalOrdering.Clear(), new(null, 216, 160, 108));
    }

    private static short I16(NativeBattleEffectMemory memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Arena!.Read(offset, 2));
    private static uint U32(NativeBattleEffectMemory memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Arena!.Read(offset, 4));

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, short value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
