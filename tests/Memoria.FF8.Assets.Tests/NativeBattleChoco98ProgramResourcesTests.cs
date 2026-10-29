using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco98ProgramResourcesTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static ImmutableArray<byte> Actor => [10, 0, 20, 0, 30, 0, 40, 0];
    private static ImmutableArray<byte> Target => [50, 0, 60, 0, 70, 0, 80, 0];

    [Fact]
    public void Original_initial_program_uses_larger_task_with_partial_clear_and_separate_morph_owner()
    {
        using GameAssets game = Installed();
        NativeBattleChoco98ProgramResources resources = game.ReadNativeChoco98ProgramResources();
        NativeBattleGfAnimationSource source = resources.ProgramSource;
        Assert.Equal(0x1680B9Cu, source.Address);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(game.ReadNativeChoco97ProgramResources().ProgramSource).Data.ToArray(), NativeBattleStorageSnapshots.Capture(source).Data.ToArray());
        NativeBattleModulePoolTask task = NativeBattleChoco98ProgramDrawing.InitializeWorkspace7224C0(Task(source, 0x722460, 0x4AC), source, Actor);
        Assert.All(NativeBattleStorageSnapshots.Capture(task).Payload.Skip(0x94 - 12 + 0x4AC), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((ushort)20, task.ReadWord(0x1E));
        ImmutableArray<byte> scratch = Scratch();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int accepted = 0;
        bool morphed = false;
        int remaining = -1;
        for (int frame = 0; frame < 30; frame++)
        {
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(new Saved(task, source, scratch)))!;
            NativeBattleChoco97ProgramDraw next = NativeBattleChoco98ProgramDrawing.Advance722520(task, source, scratch, View, Projection, colors, 0);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleChoco98ProgramDrawing.Advance722520(restored.Task, restored.Source, restored.Scratch, View, Projection, colors, 0)));
            Assert.Equal((uint)(frame + 1), next.Task.ReadDword(0x98));
            Padding(next.MorphScratch);
            Assert.All(next.MorphScratch.Skip(36 * 8), value => Assert.Equal((byte)0xA5, value));
            morphed |= !scratch.AsSpan().SequenceEqual(next.MorphScratch.AsSpan());
            accepted += next.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = next.Task;
            scratch = next.MorphScratch;
            remaining = next.Remaining;
        }
        Assert.True(morphed && accepted > 0);
        Assert.Equal(0, remaining);
    }

    [Theory]
    [InlineData(NativeBattleChoco98LoadedProgram.File457, 50, -364, 30, 40)]
    [InlineData(NativeBattleChoco98LoadedProgram.File461, 20, -1004, -354, 40)]
    [InlineData(NativeBattleChoco98LoadedProgram.File462, 24, -440, 70, 80)]
    [InlineData(NativeBattleChoco98LoadedProgram.File463, 60, 60, 70, 80)]
    public void Original_loaded_profiles_finish_with_ordered_V_and_exact_scratch_and_cold_next(NativeBattleChoco98LoadedProgram profile, int duration, int y, int z, int fourth)
    {
        using GameAssets game = Installed();
        NativeBattleChoco98ProgramResources resources = game.ReadNativeChoco98ProgramResources();
        NativeBattleGfAnimationSource original = Assert.Single(resources.Loaded, loaded => loaded.Profile == profile).ProgramSource;
        var definition = NativeBattleChoco98ProgramDrawing.LoadedProfile(profile);
        NativeBattleModulePoolTask task = Task(original, definition.Callback, definition.Clear);
        NativeBattleGfAnimationSource source = original;
        ImmutableArray<byte> scratch = Scratch();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int calls = 0;
        int accepted = 0;
        bool morphed = false;
        bool ready = false;
        List<uint> sounds = [];
        while (!ready && calls < 100)
        {
            Saved saved = new(task, source, scratch);
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(saved))!;
            NativeBattleChoco98LoadedProgramDraw next = Step(saved, 0);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Step(restored, 0)));
            if (calls == 0)
            {
                Assert.Equal(unchecked((ushort)y), next.Draw.Task.ReadWord(0x1E));
                Assert.Equal(unchecked((ushort)z), next.Draw.Task.ReadWord(0x20));
                Assert.Equal((ushort)fourth, next.Draw.Task.ReadWord(0x22));
                Assert.Equal(profile == NativeBattleChoco98LoadedProgram.File461 ? 0u : 1u, next.Draw.Task.ReadDword(0x98));
                Assert.All(NativeBattleStorageSnapshots.Capture(next.Draw.Task).Payload.Skip(0x94 - 12 + definition.Clear), value => Assert.Equal((byte)0xA5, value));
                (int Geometry, int Delta)[] scrolls = profile switch
                {
                    NativeBattleChoco98LoadedProgram.File457 => [(0, 12), (3, 12)],
                    NativeBattleChoco98LoadedProgram.File461 => [(2, 18)],
                    NativeBattleChoco98LoadedProgram.File462 => [],
                    NativeBattleChoco98LoadedProgram.File463 => [(0, 6), (1, 4), (2, 6), (5, 8)],
                    _ => throw new InvalidDataException("Absent test profile.")
                };
                ImmutableArray<byte> expectedSource = NativeBattleStorageSnapshots.Capture(source).Data;
                foreach ((int geometry, int delta) in scrolls)
                {
                    int offset = BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(8 + geometry * 4));
                    expectedSource = NativeBattleChoco97Uv.Scroll7311C0(expectedSource, offset / 4 * 4, delta);
                }
                Assert.Equal(expectedSource.ToArray(), NativeBattleStorageSnapshots.Capture(next.Draw.Source).Data.ToArray());
                if (profile == NativeBattleChoco98LoadedProgram.File461)
                {
                    Assert.Empty(next.Draw.Packets.Attempts);
                }
                else
                {
                    NativeBattleModulePoolTask initialized = NativeBattleChoco98ProgramDrawing.InitializeWorkspace7224C0(task, source, profile is NativeBattleChoco98LoadedProgram.File462 or NativeBattleChoco98LoadedProgram.File463 ? Target : Actor).WriteWord(0x1E, next.Draw.Task.ReadWord(0x1E)).WriteWord(0x20, next.Draw.Task.ReadWord(0x20));
                    NativeBattleChoco97ProgramDraw beforeV = NativeBattleChoco98ProgramDrawing.Advance722520(initialized, source, scratch, View, Projection, colors, 0);
                    Assert.Equal(NativeBattleSnapshotJson.Serialize(beforeV.Packets), NativeBattleSnapshotJson.Serialize(next.Draw.Packets));
                }
            }
            if (next.SoundDescriptor is uint sound)
            {
                sounds.Add(sound);
            }
            Padding(next.Draw.MorphScratch);
            morphed |= !scratch.AsSpan().SequenceEqual(next.Draw.MorphScratch.AsSpan());
            accepted += next.Draw.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = next.Draw.Task;
            source = next.Draw.Source;
            scratch = next.Draw.MorphScratch;
            ready = next.Draw.ReadyToComplete;
            calls++;
        }
        Assert.True(ready && accepted > 0);
        Assert.Equal(profile == NativeBattleChoco98LoadedProgram.File461 ? duration + 1 : duration, calls);
        Assert.Equal((uint)duration, task.ReadDword(0x98));
        Assert.Equal(profile == NativeBattleChoco98LoadedProgram.File463, morphed);
        Assert.Equal(profile == NativeBattleChoco98LoadedProgram.File462 ? new uint[] { 0x152AF3C } : [], sounds.ToArray());
        Assert.Equal(profile == NativeBattleChoco98LoadedProgram.File462, NativeBattleStorageSnapshots.Capture(original).Data.AsSpan().SequenceEqual(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan()));
        ImmutableArray<byte> heldSourceBefore = NativeBattleStorageSnapshots.Capture(source).Data;
        NativeBattleChoco98LoadedProgramDraw held = Step(new(task.WriteByte(0x28, 1), source, scratch), 1);
        Assert.False(held.Draw.ReadyToComplete);
        Assert.Empty(held.Draw.Packets.Attempts);
        Assert.Null(held.SoundDescriptor);
        Assert.Equal(profile == NativeBattleChoco98LoadedProgram.File462, heldSourceBefore.AsSpan().SequenceEqual(NativeBattleStorageSnapshots.Capture(held.Draw.Source).Data.AsSpan()));
        Assert.Throws<NotSupportedException>(() => Step(new(task, source, new byte[0x3C0].ToImmutableArray()), 0));
        Assert.Equal(NativeBattleStorageSnapshots.Capture(original).Data.ToArray(), NativeBattleStorageSnapshots.Capture(Assert.Single(game.ReadNativeChoco98ProgramResources().Loaded, loaded => loaded.Profile == profile).ProgramSource).Data.ToArray());

        NativeBattleChoco98LoadedProgramDraw Step(Saved saved, uint freeze) => NativeBattleChoco98ProgramDrawing.AdvanceLoaded(profile, saved.Task, saved.Source, Actor, Target, saved.Scratch, View, Projection, colors, freeze);
    }

    private static NativeBattleModulePoolTask Task(NativeBattleGfAnimationSource source, uint callback, int clear) => new NativeBattleModulePoolTask(1, 0, null, callback, Enumerable.Repeat((byte)0xA5, 0x864 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)clear).WriteWord(0x24, 0).WriteByte(0x26, 0).WriteByte(0x28, 0).WriteByte(0x29, 0).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0);
    private static ImmutableArray<byte> Scratch() => Enumerable.Repeat((byte)0xA5, 0x168).ToImmutableArray();
    private static void Padding(ImmutableArray<byte> scratch)
    {
        Assert.Equal(0x168, scratch.Length);
        for (int offset = 6; offset < scratch.Length; offset += 8)
        {
            Assert.Equal((byte)0xA5, scratch[offset]);
            Assert.Equal((byte)0xA5, scratch[offset + 1]);
        }
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleGfAnimationSource Source, ImmutableArray<byte> Scratch);
}
