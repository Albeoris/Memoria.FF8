using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97ProgramResourcesTests
{
    [Fact]
    public void Original_thirty_frame_program_preserves_actor_Y_morph_padding_and_independent_restore()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97ProgramResources resources = game.ReadNativeChoco97ProgramResources();
        NativeBattleGfAnimationSource source = resources.ProgramSource;
        Assert.Equal(0x168F8BCu, source.Address);
        Assert.Equal(0x2884, source.Data.Length);
        NativeBattleModulePoolTask initial = new NativeBattleModulePoolTask(1, 0, null, 0x72A660, Enumerable.Repeat((byte)0xA5, 0x540 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, 0x4AC).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0);
        NativeBattleModulePoolTask task = NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(initial, source, [10, 0, 20, 0, 30, 0, 40, 0]);
        Assert.Equal((ushort)20, task.ReadWord(0x1E));
        Assert.Equal((ushort)40, task.ReadWord(0x22));
        Assert.Equal(initial.ReadDword(0x90), task.ReadDword(0x90));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x3C0).ToImmutableArray();
        int primitives = 0;
        bool morphed = false;
        int remaining = -1;
        for (int frame = 0; frame < 30; frame++)
        {
            Saved saved = new(task, source, scratch);
            NativeBattleChoco97ProgramDraw next = NativeBattleChoco97ProgramDrawing.Advance72A720(task, source, scratch, view, projection, colors, 0);
            Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(saved))!;
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(NativeBattleChoco97ProgramDrawing.Advance72A720(restored.Task, restored.Source, restored.Scratch, view, projection, colors, 0)));
            NativeBattleChoco97ProgramDraw frozen = NativeBattleChoco97ProgramDrawing.Advance72A720(task, source, scratch, view, projection, colors, 0x10000);
            Assert.Equal(task.ReadDword(0x98), frozen.Task.ReadDword(0x98));
            Assert.Equal((uint)(frame + 1), next.Task.ReadDword(0x98));
            for (int offset = 6; offset < scratch.Length; offset += 8)
            {
                Assert.Equal((byte)0xA5, next.MorphScratch[offset]);
                Assert.Equal((byte)0xA5, next.MorphScratch[offset + 1]);
            }
            Assert.All(next.MorphScratch.Skip(0x280), value => Assert.Equal((byte)0xA5, value));
            morphed |= !scratch.AsSpan().SequenceEqual(next.MorphScratch.AsSpan());
            primitives += next.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = next.Task;
            scratch = next.MorphScratch;
            remaining = next.Remaining;
        }
        Assert.Equal(0, remaining);
        Assert.True(morphed);
        Assert.True(primitives > 0);
        Assert.Same(resources, game.ReadNativeChoco97ProgramResources());
        Assert.Equal(resources.ProgramSource.Data.ToArray(), source.Data.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97ProgramDrawing.Advance72A720(task, source, default, view, projection, colors, 0));
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleGfAnimationSource Source, ImmutableArray<byte> Scratch);
}
