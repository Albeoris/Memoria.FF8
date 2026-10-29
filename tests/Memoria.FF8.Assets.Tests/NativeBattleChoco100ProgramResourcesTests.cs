using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100ProgramResourcesTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static ImmutableArray<byte> Actor => [10, 0, 20, 0, 30, 0, 40, 0];
    private static ImmutableArray<byte> Target => [50, 0, 60, 0, 70, 0, 80, 0];

    [Theory]
    [InlineData(0, 30)]
    [InlineData(492, 50)]
    [InlineData(496, 20)]
    [InlineData(502, 30)]
    public void Original_programs_keep_actual_source_mutation_and_morph_owners_through_every_JSON_next(int file, int duration)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco100ProgramResources resources = game.ReadNativeChoco100ProgramResources();
        NativeBattleChoco100LoadedProgram profile = file == 492 ? NativeBattleChoco100LoadedProgram.File492 : file == 496 ? NativeBattleChoco100LoadedProgram.File496 : NativeBattleChoco100LoadedProgram.File502;
        NativeBattleGfAnimationSource source = file == 0 ? resources.ProgramSource : Assert.Single(resources.Loaded, loaded => loaded.Profile == profile).ProgramSource;
        NativeBattleGfAnimationSource original = source;
        var definition = file == 0 ? (Address: 0x1663154u, Length: 0x2884, Callback: 0x70F5F0u, Clear: 0x4AC) : NativeBattleChoco100ProgramDrawing.LoadedProfile(profile);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, definition.Callback, Enumerable.Repeat((byte)0xA5, 0x540 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)definition.Clear).WriteWord(0x24, 0).WriteByte(0x26, 0).WriteByte(0x28, 0).WriteByte(0x29, 0).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0).WriteWord(0x86, 0);
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x120).ToImmutableArray();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int calls = 0;
        int attempts = 0;
        int accepted = 0;
        bool morphed = false;
        List<uint> sounds = [];
        bool held = file is 496 or 502;
        int maximum = held ? duration + 4 : duration;
        for (int call = 0; call < maximum; call++)
        {
            Saved saved = new(task, source, scratch);
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(saved))!;
            NativeBattleChoco99ProgramDraw next = Step(saved, 0);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Step(restored, 0)));
            if (call == 0)
            {
                Assert.Equal(unchecked((ushort)(file == 502 ? 60 + 0xA800 : file == 496 ? 20 - 1024 : file == 492 ? 20 - 384 : 20)), next.Draw.Task.ReadWord(0x1E));
                Assert.Equal(unchecked((ushort)(file == 502 ? 70 + 0x400 : file == 496 ? 30 - 384 : 30)), next.Draw.Task.ReadWord(0x20));
                Assert.Equal((ushort)(file == 502 ? 80 : 40), next.Draw.Task.ReadWord(0x22));
                Assert.Equal(file == 502 ? 0x2800u : 4096u, next.Draw.Task.ReadDword(0x58));
                Assert.Equal((ushort)(file == 502 ? 0xF000 : 0), next.Draw.Task.ReadWord(0x86));
                Assert.Equal(file == 496 ? 0u : 1u, next.Draw.Task.ReadDword(0x98));
                Assert.All(NativeBattleStorageSnapshots.Capture(next.Draw.Task).Payload.Skip(0x94 - 12 + definition.Clear), value => Assert.Equal((byte)0xA5, value));
                ImmutableArray<byte> expected = Scroll(NativeBattleStorageSnapshots.Capture(source).Data);
                Assert.Equal(expected.ToArray(), NativeBattleStorageSnapshots.Capture(next.Draw.Source).Data.ToArray());
            }
            for (int offset = 6; offset < scratch.Length; offset += 8)
            {
                Assert.Equal((byte)0xA5, next.Draw.MorphScratch[offset]);
                Assert.Equal((byte)0xA5, next.Draw.MorphScratch[offset + 1]);
            }
            morphed |= !scratch.AsSpan().SequenceEqual(next.Draw.MorphScratch.AsSpan());
            attempts += next.Draw.Packets.Attempts.Length;
            accepted += next.Draw.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            if (next.SoundDescriptor is uint sound)
            {
                sounds.Add(sound);
            }
            task = next.Draw.Task;
            source = next.Draw.Source;
            scratch = next.Draw.MorphScratch;
            calls++;
        }
        Assert.True(attempts > 0);
        if (file != 502)
        {
            Assert.True(accepted > 0);
        }
        Assert.Equal(file == 0, morphed);
        Assert.Equal(file == 0 ? new uint[] { 0x15297F4 } : [], sounds.ToArray());
        Assert.Equal(calls, task.ReadWord(0x24));
        if (held)
        {
            Assert.Equal((byte)1, task.ReadByte(0x29));
            Assert.Equal((byte)0, task.ReadByte(0x26));
            ImmutableArray<byte> terminalSourceBefore = NativeBattleStorageSnapshots.Capture(source).Data;
            NativeBattleChoco99ProgramDraw terminal = Step(new(task, source, scratch), (short)(file == 496 ? 5 : 9));
            Assert.True(terminal.Draw.ReadyToComplete);
            Assert.Null(terminal.Draw.Remaining);
            Assert.Empty(terminal.Draw.Packets.Attempts);
            Assert.Equal(task.ReadDword(0x98), terminal.Draw.Task.ReadDword(0x98));
            Assert.Equal(Scroll(terminalSourceBefore).ToArray(), NativeBattleStorageSnapshots.Capture(terminal.Draw.Source).Data.ToArray());
            ImmutableArray<byte> retainedSourceBefore = NativeBattleStorageSnapshots.Capture(terminal.Draw.Source).Data;
            NativeBattleChoco99ProgramDraw retainedTerminal = Step(new(terminal.Draw.Task, terminal.Draw.Source, terminal.Draw.MorphScratch), (short)(file == 496 ? 5 : 9));
            Assert.Empty(retainedTerminal.Draw.Packets.Attempts);
            Assert.Equal(Scroll(retainedSourceBefore).ToArray(), NativeBattleStorageSnapshots.Capture(retainedTerminal.Draw.Source).Data.ToArray());
            Assert.False(Step(new(task.WriteByte(0x28, 1), source, scratch), (short)(file == 496 ? 5 : 9)).Draw.ReadyToComplete);
        }
        else
        {
            Assert.True(Step(new(task, source, scratch), 0).Draw.ReadyToComplete);
            Assert.Equal((uint)duration, task.ReadDword(0x98));
        }
        Assert.Equal(NativeBattleStorageSnapshots.Capture(original).Data.ToArray(), file == 0 ? NativeBattleStorageSnapshots.Capture(game.ReadNativeChoco100ProgramResources().ProgramSource).Data.ToArray() : NativeBattleStorageSnapshots.Capture(Assert.Single(game.ReadNativeChoco100ProgramResources().Loaded, loaded => loaded.Profile == profile).ProgramSource).Data.ToArray());
        Assert.Throws<InvalidDataException>(() => Step(new(task, source, new byte[0x760].ToImmutableArray()), 0));

        NativeBattleChoco99ProgramDraw Step(Saved saved, short mode) => file == 0 ? NativeBattleChoco100ProgramDrawing.AdvanceInitial(saved.Task, saved.Source, Actor, saved.Scratch, View, Projection, colors, 0) : NativeBattleChoco100ProgramDrawing.AdvanceLoaded(profile, saved.Task, saved.Source, Actor, Target, mode, saved.Scratch, View, Projection, colors, 0);

        ImmutableArray<byte> Scroll(ImmutableArray<byte> bytes)
        {
            foreach ((int geometry, int delta) in file == 492 ? new[] { (0, 12), (3, 12) } : file == 496 ? new[] { (2, 18) } : Array.Empty<(int, int)>())
            {
                int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(8 + geometry * 4));
                bytes = NativeBattleChoco97Uv.Scroll7311C0(bytes, offset / 4 * 4, delta);
            }
            return bytes;
        }
    }

    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleGfAnimationSource Source, ImmutableArray<byte> Scratch);
}
