using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99ProgramResourcesTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static ImmutableArray<byte> Actor => [10, 0, 20, 0, 30, 0, 40, 0];

    [Theory]
    [InlineData(0, 30, 20, 30)]
    [InlineData(471, 50, -364, 30)]
    [InlineData(474, 20, -1004, -354)]
    public void Original_admitted_callers_finish_with_retained_tail_and_cold_next(int file, int duration, int y, int z)
    {
        using GameAssets game = Installed();
        NativeBattleChoco99ProgramResources resources = game.ReadNativeChoco99ProgramResources();
        Assert.Equal(7, resources.Loaded.Length);
        NativeBattleGfAnimationSource source = file == 0 ? resources.ProgramSource : Assert.Single(resources.Loaded, item => item.FileId == file).ProgramSource;
        NativeBattleChoco99LoadedProgram profile = file == 471 ? NativeBattleChoco99LoadedProgram.File471 : NativeBattleChoco99LoadedProgram.File474;
        var definition = file == 0 ? (Address: 0x166DC58u, Length: 0x2884, Callback: 0x7189B0u, Clear: 0x4AC) : NativeBattleChoco99ProgramDrawing.LoadedProfile(profile);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, definition.Callback, Enumerable.Repeat((byte)0xA5, 0x548 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)definition.Clear).WriteWord(0x24, 0).WriteByte(0x26, 0).WriteByte(0x28, 0).WriteByte(0x29, 0).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0);
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x760).ToImmutableArray();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int accepted = 0;
        int calls = 0;
        List<uint> sounds = [];
        bool ready = false;
        while (!ready && calls < 70)
        {
            Saved saved = new(task, source, scratch);
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(saved))!;
            NativeBattleChoco99ProgramDraw step = Step(saved);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(Step(restored)));
            if (calls == 0)
            {
                Assert.Equal(unchecked((ushort)y), step.Draw.Task.ReadWord(0x1E));
                Assert.Equal(unchecked((ushort)z), step.Draw.Task.ReadWord(0x20));
                Assert.Equal((ushort)40, step.Draw.Task.ReadWord(0x22));
                Assert.Equal(file == 474 ? 0u : 1u, step.Draw.Task.ReadDword(0x98));
                Assert.All(NativeBattleStorageSnapshots.Capture(step.Draw.Task).Payload.Skip(0x94 - 12 + definition.Clear), value => Assert.Equal((byte)0xA5, value));
                ImmutableArray<byte> expected = NativeBattleStorageSnapshots.Capture(source).Data;
                foreach ((int geometry, int delta) in file == 471 ? new[] { (0, 12), (3, 12) } : file == 474 ? new[] { (2, 18) } : Array.Empty<(int, int)>())
                {
                    int offset = BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(8 + geometry * 4));
                    expected = NativeBattleChoco97Uv.Scroll7311C0(expected, offset / 4 * 4, delta);
                }
                Assert.Equal(expected.ToArray(), NativeBattleStorageSnapshots.Capture(step.Draw.Source).Data.ToArray());
            }
            if (step.SoundDescriptor is uint sound)
            {
                sounds.Add(sound);
            }
            for (int offset = 6; offset < scratch.Length; offset += 8)
            {
                Assert.Equal((byte)0xA5, step.Draw.MorphScratch[offset]);
                Assert.Equal((byte)0xA5, step.Draw.MorphScratch[offset + 1]);
            }
            accepted += step.Draw.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = step.Draw.Task;
            source = step.Draw.Source;
            scratch = step.Draw.MorphScratch;
            ready = step.Draw.ReadyToComplete;
            calls++;
        }
        Assert.True(ready && accepted > 0);
        Assert.Equal(file == 474 ? duration + 1 : duration, calls);
        Assert.Equal((uint)duration, task.ReadDword(0x98));
        Assert.Equal(file == 0 ? new uint[] { 0x1529FA4 } : [], sounds.ToArray());
        NativeBattleChoco99ProgramDraw held = Step(new(task.WriteByte(0x28, 1), source, scratch));
        Assert.False(held.Draw.ReadyToComplete);
        Assert.Empty(held.Draw.Packets.Attempts);
        Assert.Null(held.SoundDescriptor);
        Assert.Equal(file == 0, NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().SequenceEqual(NativeBattleStorageSnapshots.Capture(held.Draw.Source).Data.AsSpan()));
        Assert.Throws<NotSupportedException>(() => Step(new(task, source, new byte[0x3C0].ToImmutableArray())));

        NativeBattleChoco99ProgramDraw Step(Saved saved) => file == 0 ? NativeBattleChoco99ProgramDrawing.AdvanceInitial(saved.Task, saved.Source, Actor, saved.Scratch, View, Projection, colors, 0) : NativeBattleChoco99ProgramDrawing.AdvanceLoaded(profile, saved.Task, saved.Source, Actor, saved.Scratch, View, Projection, colors, 0);
    }

    [Theory]
    [InlineData(NativeBattleChoco99LoadedProgram.File475, 475, 80, 8)]
    [InlineData(NativeBattleChoco99LoadedProgram.File482, 482, 40, 0)]
    [InlineData(NativeBattleChoco99LoadedProgram.File483, 483, 60, 10)]
    [InlineData(NativeBattleChoco99LoadedProgram.File484, 484, 40, 10)]
    public void Original_custom_callers_retain_physical_fields_and_use_their_actual_termination(NativeBattleChoco99LoadedProgram profile, int file, int duration, short terminalMode)
    {
        using GameAssets game = Installed();
        NativeBattleGfAnimationSource source = Assert.Single(game.ReadNativeChoco99ProgramResources().Loaded, item => item.FileId == file).ProgramSource;
        var definition = NativeBattleChoco99ProgramDrawing.LoadedProfile(profile);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, definition.Callback, Enumerable.Repeat((byte)0xA5, 0x548 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)definition.Clear).WriteWord(0x24, 0).WriteByte(0x26, 0).WriteByte(0x28, 0).WriteByte(0x29, 0).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0).WriteDword(0x50, 0).WriteDword(0x54, 0).WriteDword(0x58, 0);
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x760).ToImmutableArray();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int accepted = 0;
        int calls = profile == NativeBattleChoco99LoadedProgram.File482 ? duration : duration + 3;
        for (int call = 0; call < calls; call++)
        {
            Saved saved = new(task, source, scratch);
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(saved))!;
            Saved? independentInput = profile == NativeBattleChoco99LoadedProgram.File482 ? NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(saved)) : null;
            NativeBattleChoco99ProgramDraw step = Step(saved, 0);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(Step(restored, 0)));
            Assert.Equal(NativeBattleStorageSnapshots.Capture(source).Data.ToArray(), NativeBattleStorageSnapshots.Capture(step.Draw.Source).Data.ToArray());
            Assert.Equal((ushort)0xA5A5, step.Draw.Task.ReadWord(0x22));
            Assert.Equal(0xA5A5A5A5u, step.Draw.Task.ReadDword(0x540));
            Assert.Equal(0xA5A5A5A5u, step.Draw.Task.ReadDword(0x544));
            Assert.Null(step.SoundDescriptor);
            if (call == 0)
            {
                Assert.Equal((ushort)0, step.Draw.Task.ReadWord(0x1C));
                Assert.Equal(profile is NativeBattleChoco99LoadedProgram.File483 or NativeBattleChoco99LoadedProgram.File484 ? (ushort)1536 : (ushort)0, step.Draw.Task.ReadWord(0x1E));
                Assert.Equal(profile == NativeBattleChoco99LoadedProgram.File482 ? (ushort)864 : (ushort)0, step.Draw.Task.ReadWord(0x20));
                Assert.Equal(profile == NativeBattleChoco99LoadedProgram.File482 ? 0u : 4096u, step.Draw.Task.ReadDword(0x50));
                Assert.All(NativeBattleStorageSnapshots.Capture(step.Draw.Task).Payload.Skip(0x94 - 12 + definition.Clear), value => Assert.Equal((byte)0xA5, value));
            }
            if (profile == NativeBattleChoco99LoadedProgram.File482)
            {
                NativeBattleActionMatrix otherView = new([0, 4096, 0, 4096, 0, 0, 0, 0, -4096], 321, 654, -7000, 4096);
                NativeBattleModulePoolTask changed = independentInput!.Task.WriteDword(0x50, 123).WriteDword(0x54, 456).WriteDword(0x58, 789).WriteWord(0x60, 999);
                NativeBattleChoco99ProgramDraw independent = NativeBattleChoco99ProgramDrawing.AdvanceHeldOrStatic(profile, changed, independentInput.Source, 0, independentInput.Scratch, otherView, Projection, colors, 0);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(step.Draw.Packets), NativeBattleSnapshotJson.Serialize(independent.Draw.Packets));
                Assert.Equal(step.Draw.MorphScratch.ToArray(), independent.Draw.MorphScratch.ToArray());
            }
            accepted += step.Draw.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = step.Draw.Task;
            scratch = step.Draw.MorphScratch;
        }
        Assert.True(accepted > 0);
        if (profile == NativeBattleChoco99LoadedProgram.File482)
        {
            Assert.True(Step(new(task, source, scratch), 0).Draw.ReadyToComplete);
        }
        else
        {
            Assert.Equal((byte)1, task.ReadByte(0x29));
            Assert.Equal((byte)0, task.ReadByte(0x26));
            NativeBattleChoco99ProgramDraw below = Step(new(task, source, scratch), (short)(terminalMode - 1));
            Assert.False(below.Draw.ReadyToComplete);
            Assert.Equal((byte)1, below.Draw.Task.ReadByte(0x29));
            NativeBattleChoco99ProgramDraw terminal = Step(new(task, source, scratch), terminalMode);
            Assert.True(terminal.Draw.ReadyToComplete);
            Assert.Null(terminal.Draw.Remaining);
            Assert.Empty(terminal.Draw.Packets.Attempts);
            Assert.Equal(task.ReadDword(0x98), terminal.Draw.Task.ReadDword(0x98));
            Assert.Equal(scratch.ToArray(), terminal.Draw.MorphScratch.ToArray());
            Assert.False(Step(new(task.WriteByte(0x28, 1), source, scratch), terminalMode).Draw.ReadyToComplete);
        }

        NativeBattleChoco99ProgramDraw Step(Saved saved, short mode) => NativeBattleChoco99ProgramDrawing.AdvanceHeldOrStatic(profile, saved.Task, saved.Source, mode, saved.Scratch, View, Projection, colors, 0);
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleGfAnimationSource Source, ImmutableArray<byte> Scratch);
}
