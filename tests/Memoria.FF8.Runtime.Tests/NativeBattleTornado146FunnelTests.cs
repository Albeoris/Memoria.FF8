using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTornado146FunnelTests
{
    [Fact]
    public void Initialization_preserves_packet_padding_and_builds_both_physical_texture_banks()
    {
        NativeBattleTornado146Work seed = new(NativeBattleTornado146State.FromColdProcess());
        seed.WriteWord(0x2495230 + 30, 0xABCD);
        seed.WriteWord(0x2497A30 + 38, 0x1234);
        NativeBattleTornado146Work work = new(NativeBattleTornado146Funnel.Initialize6152E0(seed.Freeze()));
        Assert.Equal((ushort)0xABCD, work.Word(0x2495230 + 30));
        Assert.Equal((ushort)0x1234, work.Word(0x2497A30 + 38));
        Assert.Equal(0x09000000u, work.Dword(0x249A208));
        Assert.Equal((ushort)0x3F94, work.Word(0x2495230 + 14));
        Assert.Equal((ushort)0x3FD4, work.Word(0x2497A30 + 14));
        Assert.Equal(new byte[] { 0, 224 }, work.Read(0x2495230 + 12, 2));
        Assert.Equal(new byte[] { 63, 224 }, work.Read(0x2495230 + 20, 2));
        Assert.Equal((byte)0, work.Byte(0x2495230 + 7 * 32 * 40 + 13));
        Assert.Equal((ushort)80, work.Word(0x2491CE8 + 16 * 20 + 2));
    }

    [Fact]
    public void Motion_consumes_exactly_three_draws_per_ring_and_keeps_released_scratch_padding_unknown()
    {
        NativeCrtRandomState expected = new(17);
        for (int draw = 0; draw < 51; draw++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleTornado146FunnelFrame frame = Advance(Initial(), Task(0), new(17), Staging(), 0);
        Assert.Equal(expected, frame.Random);
        Assert.Equal((ushort)1, frame.Task.ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Throws<NotSupportedException>(() => frame.Staging.Read(NativeBattleTemporaryMemory.Address + 30, 2));
        Assert.NotEmpty(frame.Ordering.Proxies);
    }

    [Fact]
    public void Draw_only_callback_preserves_motion_age_and_random_but_publishes_task_translation()
    {
        NativeBattleTornado146State initial = Initial();
        NativeBattleTornado146FunnelFrame frame = Advance(initial, Task(40).WriteWord(16, 300), new(29), Staging(), 0x201);
        Assert.Equal(new NativeCrtRandomState(29), frame.Random);
        Assert.Equal((ushort)40, frame.Task.ReadWord(12));
        Assert.Equal(new NativeBattleVector(300, 0, 0), frame.State.FunnelTranslation);
        Assert.Equal(new NativeBattleTornado146Work(initial).Read(0x2491CE8, 340), new NativeBattleTornado146Work(frame.State).Read(0x2491CE8, 340));
        Assert.Equal(0, frame.Result);
    }

    [Fact]
    public void Final_callback_releases_at76_and_independent_JSON_next_keeps_all_physical_owners()
    {
        NativeBattleTornado146FunnelFrame before = Advance(Initial(), Task(74), new(31), Staging(), 0);
        NativeBattleTornado146FunnelFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleTornado146FunnelFrame>(NativeBattleTestSnapshots.Json(before))!;
        NativeBattleTornado146FunnelFrame expected = Advance(before.State, before.Task, before.Random, before.Staging, 0);
        NativeBattleTornado146FunnelFrame actual = Advance(restored.State, restored.Task, restored.Random, restored.Staging, 0);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)76, actual.Task.ReadWord(12));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    private static NativeBattleTornado146State Initial() => NativeBattleTornado146Funnel.Initialize6152E0(NativeBattleTornado146State.FromColdProcess());
    private static NativeBattleModulePoolTask Task(ushort age) => new NativeBattleModulePoolTask(1, 0, null, 0x6171F0, new byte[12].ToImmutableArray()).WriteWord(12, age);
    private static NativeBattleFileStaging Staging() => NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
    private static NativeBattleTornado146FunnelFrame Advance(NativeBattleTornado146State state, NativeBattleModulePoolTask task, NativeCrtRandomState random, NativeBattleFileStaging staging, uint flags) => NativeBattleTornado146Funnel.Advance6171F0(state, task, random, new(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { Z = 16000 }, 256, 160, 120), NativeBattleLogicalOrdering.Clear(), flags, staging);
}
