using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97LoadedProgramTests
{
    [Theory]
    [InlineData(NativeBattleChoco97LoadedProgram.File445, 50, -364, 30, 40)]
    [InlineData(NativeBattleChoco97LoadedProgram.File449, 20, -1004, -354, 40)]
    [InlineData(NativeBattleChoco97LoadedProgram.File450, 30, 60, 70, 80)]
    public void Original_loaded_programs_preserve_partial_clear_and_draw_before_mutation_through_completion(NativeBattleChoco97LoadedProgram profile, int duration, int originY, int originZ, int originW)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco97LoadedProgramResources resources = Assert.Single(game.ReadNativeChoco97LoadedProgramResources(), item => item.Profile == profile);
        var definition = NativeBattleChoco97ProgramDrawing.LoadedProfile(profile);
        NativeBattleGfAnimationSource source = resources.ProgramSource;
        Assert.Equal(definition.Address, source.Address);
        Assert.Equal(definition.Length, NativeBattleStorageSnapshots.Capture(source).Data.Length);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, definition.Callback, Enumerable.Repeat((byte)0xA5, 0x540 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)definition.Clear).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0).WriteWord(0x24, 0xFFFE).WriteByte(0x26, 0).WriteByte(0x28, 0).WriteByte(0x29, 0);
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x3C0).ToImmutableArray();
        ImmutableArray<byte> actor = [10, 0, 20, 0, 30, 0, 40, 0];
        ImmutableArray<byte> target = [50, 0, 60, 0, 70, 0, 80, 0];
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int calls = 0;
        int primitives = 0;
        bool mutated = false;
        bool ready = false;
        while (!ready && calls < 100)
        {
            Saved saved = new(task, source, scratch);
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(saved))!;
            ImmutableArray<byte> sourceBefore = NativeBattleStorageSnapshots.Capture(source).Data;
            NativeBattleChoco97LoadedProgramDraw next = Step(saved);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Step(restored)));
            if (calls == 0)
            {
                Assert.Equal(unchecked((ushort)originY), next.Task.ReadWord(0x1E));
                Assert.Equal(unchecked((ushort)originZ), next.Task.ReadWord(0x20));
                Assert.Equal((ushort)originW, next.Task.ReadWord(0x22));
                Assert.Equal(profile == NativeBattleChoco97LoadedProgram.File449 ? 0u : 1u, next.Task.ReadDword(0x98));
                Assert.All(NativeBattleStorageSnapshots.Capture(next.Task).Payload.Skip(0x94 - 12 + definition.Clear), value => Assert.Equal((byte)0xA5, value));
                if (profile != NativeBattleChoco97LoadedProgram.File449)
                {
                    NativeBattleModulePoolTask initialized = NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(task, source, profile == NativeBattleChoco97LoadedProgram.File450 ? target : actor);
                    initialized = initialized.WriteWord(0x1E, next.Task.ReadWord(0x1E)).WriteWord(0x20, next.Task.ReadWord(0x20));
                    NativeBattleChoco97ProgramDraw beforeMutation = NativeBattleChoco97ProgramDrawing.Advance72A720(initialized, source, scratch, view, projection, colors, 0);
                    Assert.Equal(NativeBattleSnapshotJson.Serialize(beforeMutation.Packets), NativeBattleSnapshotJson.Serialize(next.Packets));
                }
                else
                {
                    Assert.Empty(next.Packets.Attempts);
                }
            }
            for (int offset = 6; offset < scratch.Length; offset += 8)
            {
                Assert.Equal((byte)0xA5, next.MorphScratch[offset]);
                Assert.Equal((byte)0xA5, next.MorphScratch[offset + 1]);
            }
            mutated |= !sourceBefore.AsSpan().SequenceEqual(NativeBattleStorageSnapshots.Capture(next.Source).Data.AsSpan());
            primitives += next.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = next.Task;
            source = next.Source;
            scratch = next.MorphScratch;
            ready = next.ReadyToComplete;
            calls++;
        }
        Assert.True(ready);
        Assert.Equal(profile == NativeBattleChoco97LoadedProgram.File449 ? duration + 1 : duration, calls);
        Assert.Equal((uint)duration, task.ReadDword(0x98));
        Assert.Equal(unchecked((ushort)(0xFFFE + calls)), task.ReadWord(0x24));
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.True(mutated);
        Assert.True(primitives > 0);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(resources.ProgramSource).Data.ToArray(), NativeBattleStorageSnapshots.Capture(Assert.Single(game.ReadNativeChoco97LoadedProgramResources(), item => item.Profile == profile).ProgramSource).Data.ToArray());
        ImmutableArray<byte> heldSourceBefore = NativeBattleStorageSnapshots.Capture(source).Data;
        ushort heldCounter = task.ReadWord(0x24);
        NativeBattleChoco97LoadedProgramDraw held = Step(new(task.WriteByte(0x28, 1), source, scratch));
        Assert.False(held.ReadyToComplete);
        Assert.Null(held.Remaining);
        Assert.Empty(held.Packets.Attempts);
        Assert.False(heldSourceBefore.AsSpan().SequenceEqual(NativeBattleStorageSnapshots.Capture(held.Source).Data.AsSpan()));
        Assert.Equal(unchecked((ushort)(heldCounter + 1)), held.Task.ReadWord(0x24));
        Assert.Throws<InvalidDataException>(() => Step(new(task.WriteDword(0x78, 0x4AC), source, scratch)));
        Assert.Throws<NotSupportedException>(() => Step(new(task, source, new byte[0x280].ToImmutableArray())));

        NativeBattleChoco97LoadedProgramDraw Step(Saved saved) => NativeBattleChoco97ProgramDrawing.AdvanceLoaded(profile, saved.Task, saved.Source, actor, target, saved.Scratch, view, projection, colors, 0);
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleGfAnimationSource Source, ImmutableArray<byte> Scratch);
}
