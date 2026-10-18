using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMoogle96ProgramDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static ImmutableArray<byte> Dog => [10, 0, 20, 0, 30, 0, 40, 0];

    [Fact]
    public void Original_Moogle_initializes_exact_workspace_then_draws135_frames_with_retained_mutable_source_and_cold_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource original = game.ReadNativeMoogle96Resources().ProgramSource;
        NativeBattleGfAnimationSource source = original;
        NativeBattleModulePoolTask task = NativeBattleMoogle96ProgramDrawing.InitializeWorkspace734400(Task(source, 0), source, Dog);
        Assert.Equal((ushort)10, task.ReadWord(0x1C));
        Assert.Equal((ushort)0, task.ReadWord(0x1E));
        Assert.Equal((ushort)30, task.ReadWord(0x20));
        Assert.Equal((ushort)40, task.ReadWord(0x22));
        Assert.Equal(4096u, task.ReadDword(0x50));
        Assert.Equal(4096u, task.ReadDword(0x54));
        Assert.Equal(4096u, task.ReadDword(0x58));
        Assert.Equal(0u, task.ReadDword(0x98));
        Assert.Equal((ushort)0xA5A5, task.ReadWord(0x92));
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int packets = 0;
        for (int frame = 0; frame < 135; frame++)
        {
            NativeBattleMoogle96ProgramDraw next = NativeBattleMoogle96ProgramDrawing.Advance734450(task, source, View, Projection, colors, 0);
            Assert.Equal(134 - frame, next.Remaining);
            Assert.Equal((uint)(frame + 1), next.Task.ReadDword(0x98));
            Assert.Equal((ushort)0xA5A5, next.Task.ReadWord(0x92));
            if (frame == 67)
            {
                Shared saved = new(task, task, source);
                Shared restored = JsonSerializer.Deserialize<Shared>(JsonSerializer.Serialize(saved))!;
                NativeBattleMoogle96ProgramDraw cold = NativeBattleMoogle96ProgramDrawing.Advance734450(restored.First, restored.Source, View, Projection, colors, 0);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold));
                Assert.Equal(JsonSerializer.Serialize(NativeBattleMoogle96ProgramDrawing.MutateUv7340A0(source)), JsonSerializer.Serialize(NativeBattleMoogle96ProgramDrawing.MutateUv7340A0(restored.Source)));
                NativeBattleMoogle96ProgramDraw unused86 = NativeBattleMoogle96ProgramDrawing.Advance734450(task.WriteWord(0x86, 0xFF00), source, View, Projection, colors, 0);
                Assert.Equal(JsonSerializer.Serialize(next.Packets), JsonSerializer.Serialize(unused86.Packets));
                NativeBattleMoogle96ProgramDraw frozen = NativeBattleMoogle96ProgramDrawing.Advance734450(task, source, View, Projection, colors, 0x10000);
                Assert.Equal(task.ReadDword(0x98), frozen.Task.ReadDword(0x98));
            }
            packets += next.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            NativeBattleGfAnimationSource changed = NativeBattleMoogle96ProgramDrawing.MutateUv7340A0(source);
            Assert.Equal(source.Address, changed.Address);
            Assert.False(source.Data.AsSpan().SequenceEqual(changed.Data.AsSpan()));
            source = changed;
            task = next.Task;
        }
        Assert.True(packets > 0);
        Assert.False(original.Data.AsSpan().SequenceEqual(source.Data.AsSpan()));
        Assert.Equal(game.ReadNativeMoogle96Resources().ProgramSource.Data.ToArray(), original.Data.ToArray());
    }

    [Fact]
    public void Two_original_program_tasks_share_UV_mutations_in_physical_traversal_order_and_restore_together()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeMoogle96Resources().ProgramSource;
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleModulePoolTask first = NativeBattleMoogle96ProgramDrawing.InitializeWorkspace734400(Task(source, 0), source, Dog);
        NativeBattleModulePoolTask second = NativeBattleMoogle96ProgramDrawing.InitializeWorkspace734400(Task(source, 1), source, Dog);
        bool secondObservedFirst = false;
        Shared state = new(first, second, source);
        for (int frame = 0; frame < 135; frame++)
        {
            Shared next = Step(state, out NativeBattleGfGeometryBatch secondPackets);
            NativeBattleGfGeometryBatch stalePackets = NativeBattleMoogle96ProgramDrawing.Advance734450(state.Second, state.Source, View, Projection, colors, 0).Packets;
            secondObservedFirst |= JsonSerializer.Serialize(stalePackets) != JsonSerializer.Serialize(secondPackets);
            if (frame == 67)
            {
                Shared restored = JsonSerializer.Deserialize<Shared>(JsonSerializer.Serialize(state))!;
                Shared cold = Step(restored, out NativeBattleGfGeometryBatch coldPackets);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold));
                Assert.Equal(JsonSerializer.Serialize(secondPackets), JsonSerializer.Serialize(coldPackets));
            }
            Assert.Equal((uint)(frame + 1), next.First.ReadDword(0x98));
            Assert.Equal((uint)(frame + 1), next.Second.ReadDword(0x98));
            state = next;
        }
        Assert.True(secondObservedFirst);

        Shared Step(Shared current, out NativeBattleGfGeometryBatch secondPackets)
        {
            NativeBattleMoogle96ProgramDraw a = NativeBattleMoogle96ProgramDrawing.Advance734450(current.First, current.Source, View, Projection, colors, 0);
            NativeBattleGfAnimationSource afterFirst = NativeBattleMoogle96ProgramDrawing.MutateUv7340A0(current.Source);
            NativeBattleMoogle96ProgramDraw b = NativeBattleMoogle96ProgramDrawing.Advance734450(current.Second, afterFirst, View, Projection, colors, 0);
            secondPackets = b.Packets;
            return new(a.Task, b.Task, NativeBattleMoogle96ProgramDrawing.MutateUv7340A0(afterFirst));
        }
    }

    private static NativeBattleModulePoolTask Task(NativeBattleGfAnimationSource source, int index) => new NativeBattleModulePoolTask(1, checked((ushort)index), null, 0x733FC0, Enumerable.Repeat((byte)0xA5, 0x51C - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, 0x488).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0).WriteWord(0x86, 0);
    private sealed record Shared(NativeBattleModulePoolTask First, NativeBattleModulePoolTask Second, NativeBattleGfAnimationSource Source);
}
