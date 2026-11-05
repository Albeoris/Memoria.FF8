using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldLoopSoundTests
{
    [Fact]
    public void Actual_footstep_contact_replaces_its_selected_loop_voice_without_changing_allocation_or_notification()
    {
        string directory = TestResources.GameDirectory;
        string fieldName = FieldName(directory);
        using GameAssets assets = Assets(directory, fieldName, "footstep", false);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        AdvanceToWait(world);
        Assert.Equal(new GameplaySaveFieldLoopSound[] { new(2, 3, 70, 16), new(23, 1, 90, 64) }, Loops(world));
        ActorState actor = world.Events.Actors[0];
        Assert.True(actor.FootstepsEnabled);
        NativeFootstepSound sound = new(actor.ObjectId, 1, 2, 128, 119, 0x400000);
        world.Events.ResolveFootstepSound = (_, mode, _) => sound with { Mode = mode };
        NativeGlobalSoundProcess expected = session.GlobalSoundProcess!.RecordEffect(sound.EffectId, sound.Flags, out NativeGlobalSoundAllocation allocation);
        Assert.Equal(23, allocation.Voice);
        List<object> commands = [];
        world.Events.AudioCommandExecuted += commands.Add;
        long frame = world.Events.Frame;
        long clock = session.ExamTimer!.ClockCalls;
        NativePlaytimeState? playtime = session.State.Playtime;
        string random = RandomOwners(session.State);
        int history = world.Events.Scene.GlobalSoundRequests.Count;
        MethodInfo contact = typeof(EventEngine).GetMethod("StepNativeFootstepCrossings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        contact.Invoke(world.Events, [actor, (ushort)127, (ushort)129, (ushort)512, (ushort)16, false]);
        Assert.Equal(new GameplaySaveFieldLoopSound[] { new(2, 3, 70, 16) }, Loops(world));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.GlobalSoundProcess));
        Assert.Equal(sound, Assert.IsType<NativeFootstepSound>(Assert.Single(commands)));
        Assert.Equal(frame, world.Events.Frame);
        Assert.Equal(clock, session.ExamTimer.ClockCalls);
        Assert.Equal(playtime, session.State.Playtime);
        Assert.Equal(random, RandomOwners(session.State));
        Assert.Equal(history, world.Events.Scene.GlobalSoundRequests.Count);
    }

    [Theory]
    [InlineData("play")]
    [InlineData("volume")]
    [InlineData("volume-transition")]
    [InlineData("position")]
    [InlineData("position-transition")]
    [InlineData("all-volume")]
    [InlineData("all-volume-transition")]
    [InlineData("all-position")]
    [InlineData("all-position-transition")]
    public void Actual_loop_commands_and_terminal_controls_survive_two_cold_saves_without_execution_replay(string control)
    {
        string directory = TestResources.GameDirectory;
        string fieldName = FieldName(directory);
        using GameAssets assets = Assets(directory, fieldName, control, false);
        using GameAssets current = Assets(directory, fieldName, control, true);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        session.SetEventMemory(session.EventMemory.Write(600, 4, NativeScalarValue.Exact(0)));
        AdvanceToWait(source);
        GameplaySaveFieldLoopSound[] expected = Expected(control);
        Assert.Equal(expected, Loops(source));
        Assert.Equal(3, source.Events.Scene.GlobalSoundRequests.Count);
        Assert.DoesNotContain(Loops(source), sound => sound.EffectId == 2);
        GameState prepared = session.PrepareSnapshot();
        long frame = source.Events.Frame;
        string sourceBefore = source.Events.Capture().GetRawText();
        string firstPath = SavePath();
        string secondPath = SavePath();
        try
        {
            GameState beforeWrite = session.State;
            source.WriteGameplaySave(firstPath);
            Assert.Same(beforeWrite, session.State);
            byte[] originalBytes = File.ReadAllBytes(firstPath);
            GameplaySaveComponentDocument document = Read(firstPath);
            Assert.Equal(expected, document.Progression!.FieldContinuation!.Scene.LoopSounds);
            GameSession firstSession = new(PlayerStateLoader.Load(firstPath, directory, sharedAssets: current));
            using GameWorld first = new(firstSession, directory, sharedAssets: current);
            first.EnsureField();
            AssertRestored(source, prepared, first, firstSession, frame, expected);
            AssertPublication(first, firstSession, current, expected, true);
            first.WriteGameplaySave(secondPath);
            GameSession secondSession = new(PlayerStateLoader.Load(secondPath, directory, sharedAssets: current));
            using GameWorld second = new(secondSession, directory, sharedAssets: current);
            second.EnsureField();
            AssertRestored(source, prepared, second, secondSession, frame, expected);
            AssertPublication(second, secondSession, current, expected, true);
            Assert.Equal(originalBytes, File.ReadAllBytes(firstPath));
            Assert.Equal(sourceBefore, source.Events.Capture().GetRawText());
            for (int tick = 0; tick < 12; tick++)
            {
                Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), second.Apply(new(PlayerActionKind.Wait)));
                Assert.Null(second.Events.LastFault);
                Assert.Equal(session.Economy!.Gil, secondSession.Economy!.Gil);
                Assert.Equal(RandomOwners(session.State), RandomOwners(secondSession.State));
                Assert.Equal(session.ExamTimer!.ClockCalls, secondSession.ExamTimer!.ClockCalls);
                Assert.Equal(session.EventMemory.Read(600, 4).RequireExact(), secondSession.EventMemory.Read(600, 4).RequireExact());
            }
            Assert.Equal(prepared.Economy!.Gil + 11, secondSession.Economy!.Gil);
            Assert.Equal(77, secondSession.EventMemory.Read(600, 4).RequireExact());
            Assert.Equal(expected, Loops(second));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("replace")]
    [InlineData("initialize")]
    public void Actual_stops_one_shot_replacement_and_initialization_remove_only_their_loop_owners(string control)
    {
        string directory = TestResources.GameDirectory;
        string fieldName = FieldName(directory);
        using GameAssets assets = Assets(directory, fieldName, control, false);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        AdvanceToWait(source);
        GameplaySaveFieldLoopSound[] expected = control == "initialize" ? [] : [new(2, 3, 70, 16)];
        Assert.Equal(expected, Loops(source));
        string path = SavePath();
        try
        {
            source.WriteGameplaySave(path);
            GameSession resumedSession = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets));
            using GameWorld resumed = new(resumedSession, directory, sharedAssets: assets);
            resumed.EnsureField();
            Assert.Equal(expected, Loops(resumed));
            AssertPublication(resumed, resumedSession, assets, expected, false);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Older_missing_loop_owner_starts_empty_and_current_nonlooping_descriptors_do_not_replay_as_loops(bool missingOwner)
    {
        string directory = TestResources.GameDirectory;
        string fieldName = FieldName(directory);
        using GameAssets assets = Assets(directory, fieldName, "play", false);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        AdvanceToWait(source);
        string path = SavePath();
        try
        {
            source.WriteGameplaySave(path);
            GameplaySaveComponentDocument document = Read(path);
            GameplaySaveFieldContinuation continuation = document.Progression!.FieldContinuation!;
            if (missingOwner)
            {
                document = document with { Progression = document.Progression with { FieldContinuation = continuation with { Scene = continuation.Scene with { LoopSounds = null } } } };
                Assert.DoesNotContain("LoopSounds", JsonSerializer.Serialize(document), StringComparison.Ordinal);
                using FileStream output = File.Create(path);
                GameplaySaveCodec.Write(output, document);
            }
            byte[] originalBytes = File.ReadAllBytes(path);
            using GameAssets current = Assets(directory, fieldName, "play", true, true);
            GameSession resumedSession = new(PlayerStateLoader.Load(path, directory, sharedAssets: current));
            using GameWorld resumed = new(resumedSession, directory, sharedAssets: current);
            resumed.EnsureField();
            Assert.Equal(missingOwner ? [] : Expected("play"), Loops(resumed));
            string randomBefore = RandomOwners(resumedSession.State);
            long frame = resumed.Events.Frame;
            List<object> publications = [];
            resumed.Events.AudioCommandExecuted += publications.Add;
            resumed.Events.ResumeGameplayLoopSounds();
            Assert.Empty(publications);
            Assert.Empty(resumed.Events.Scene.GlobalSoundRequests);
            Assert.Equal(frame, resumed.Events.Frame);
            Assert.Equal(randomBefore, RandomOwners(resumedSession.State));
            Assert.Equal(originalBytes, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertRestored(GameWorld source, GameState prepared, GameWorld resumed, GameSession session, long frame, GameplaySaveFieldLoopSound[] expected)
    {
        Assert.Equal(frame, resumed.Events.Frame);
        Assert.Equal(expected, Loops(resumed));
        Assert.Equal(prepared.Playtime, session.State.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, session.ExamTimer!.ClockCalls);
        Assert.Equal(prepared.Economy!.Gil, session.Economy!.Gil);
        Assert.Equal(RandomOwners(prepared), RandomOwners(session.State));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(resumed.Events.CaptureGameplaySaveScripts())));
        Assert.Empty(resumed.Events.Scene.GlobalSoundRequests);
        Assert.Empty(resumed.Events.Scene.NativeFieldSoundRequests);
        Assert.Null(resumed.Events.Scene.SoundVolumeTransition);
        Assert.Null(resumed.Events.Scene.AllSoundVolumeTransition);
    }

    private static void AssertPublication(GameWorld world, GameSession session, GameAssets current, GameplaySaveFieldLoopSound[] expected, bool changedResources)
    {
        string before = world.Events.Capture().GetRawText();
        string randomBefore = RandomOwners(session.State);
        NativePlaytimeState? playtime = session.State.Playtime;
        long timer = session.ExamTimer!.ClockCalls;
        NativeGlobalSoundProcess? allocator = session.GlobalSoundProcess;
        List<object> publications = [];
        world.Events.AudioCommandExecuted += publications.Add;
        world.Events.ResumeGameplayLoopSounds();
        world.Events.AudioCommandExecuted -= publications.Add;
        Assert.Equal(expected.Length, publications.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            GameplayLoopSoundPlayback playback = Assert.IsType<GameplayLoopSoundPlayback>(publications[index]);
            Assert.Equal(expected[index], playback.Sound);
            Assert.Equal(GameWorld.ResolveGlobalSound(current.Resources, EventEngine.MapGlobalSoundSample(expected[index].EffectId)), playback.Resource);
            Assert.Equal(changedResources ? 44100u : 22050u, playback.Resource.LoopPolicy.SampleRate);
        }
        Assert.Equal(before, world.Events.Capture().GetRawText());
        Assert.Equal(randomBefore, RandomOwners(session.State));
        Assert.Equal(playtime, session.State.Playtime);
        Assert.Equal(timer, session.ExamTimer!.ClockCalls);
        Assert.Same(allocator, session.GlobalSoundProcess);
    }

    private static void AdvanceToWait(GameWorld world)
    {
        for (int tick = 0; tick < 10; tick++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
            if (world.Events.CaptureGameplaySaveScripts().Fibers.Any(fiber => fiber.Wait == GameplaySaveScriptWait.Frames))
            {
                return;
            }
        }
        Assert.Fail("Audio fixture did not reach its actual post-control wait.");
    }

    private static GameplaySaveFieldLoopSound[] Loops(GameWorld world) => world.Events.Scene.GameplayLoopSounds.Values.OrderBy(sound => sound.Voice).ToArray();

    private static GameplaySaveFieldLoopSound[] Expected(string control)
    {
        byte volume = control switch { "volume" => 37, "volume-transition" => 41, "all-volume" => 29, "all-volume-transition" => 90, _ => 90 };
        byte otherVolume = control switch { "all-volume" => 29, "all-volume-transition" => 90, _ => 70 };
        byte pan = control switch { "position" => 101, "position-transition" => 111, "all-position" => 51, "all-position-transition" => 39, _ => 64 };
        byte otherPan = control switch { "all-position" => 51, "all-position-transition" => 39, _ => 16 };
        return [new(1, 1, volume, pan), new(2, 3, otherVolume, otherPan)];
    }

    private static string FieldName(string directory)
    {
        using GameAssets assets = new(directory);
        return assets.LoadField(165).Name;
    }

    private static GameAssets Assets(string directory, string fieldName, string control, bool changed, bool oneShotsOnly = false)
    {
        GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(control)));
        using MemoryStream format = new();
        using (BinaryWriter writer = new(format, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)3);
            for (int sample = 0; sample <= 3; sample++)
            {
                writer.Write(8u);
                writer.Write(0u);
                writer.Write(sample == 2 || oneShotsOnly ? 0u : 1u);
                writer.Write(changed && !oneShotsOnly && sample != 2 ? 2u : 0u);
                writer.Write(changed && !oneShotsOnly && sample != 2 ? 8u : 0u);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
                uint rate = changed ? 44100u : 22050u;
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((ushort)2);
                writer.Write((ushort)16);
                writer.Write((ushort)0);
            }
        }
        assets.Resources.Prepare(null, encodedResources: [new(new("files/sound/audio.fmt"), "bin", 1, "fixture", format.ToArray()), new(new("files/sound/audio.dat"), "bin", 1, "fixture", new byte[8])]).Commit();
        return assets;
    }

    private static byte[] Program(string control)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1)];
        void Command(Jsm.Opcode opcode, params int[] operands)
        {
            ordinary.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
            ordinary.Add((opcode, 0));
        }
        Command(Jsm.Opcode.EFFECTPLAY, 1, control == "footstep" ? 0x400000 : 1, 128, 90);
        Command(Jsm.Opcode.EFFECTPLAY, 3, 2, 32, 70);
        Command(Jsm.Opcode.EFFECTPLAY, 2, 8, 128, 80);
        switch (control)
        {
            case "volume":
                Command(Jsm.Opcode.SEVOL, 1, 37);
                break;
            case "volume-transition":
                Command(Jsm.Opcode.SEVOLTRANS, 1, 7, 41);
                break;
            case "position":
                Command(Jsm.Opcode.SEPOS, 1, 202);
                break;
            case "position-transition":
                Command(Jsm.Opcode.SEPOSTRANS, 1, 7, 222);
                break;
            case "all-volume":
                Command(Jsm.Opcode.ALLSEVOL, 29);
                break;
            case "all-volume-transition":
                Command(Jsm.Opcode.ALLSEVOLTRANS, 7, 45);
                break;
            case "all-position":
                Command(Jsm.Opcode.ALLSEPOS, 102);
                break;
            case "all-position-transition":
                Command(Jsm.Opcode.ALLSEPOSTRANS, 7, 39);
                break;
            case "stop":
                Command(Jsm.Opcode.SESTOP, 1);
                break;
            case "replace":
                Command(Jsm.Opcode.EFFECTPLAY, 2, 1, 128, 40);
                break;
            case "initialize":
                Command(Jsm.Opcode.INITSOUND);
                break;
        }
        ordinary.AddRange([(Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary.ToArray()]);
    }

    private static GameplaySaveComponentDocument Read(string path)
    {
        using FileStream input = File.OpenRead(path);
        return GameplaySaveCodec.Read(input);
    }

    private static string SavePath() => Path.Combine(Path.GetTempPath(), "Memoria.FF8-loop-sound-" + Guid.NewGuid().ToString("N"));

    private static string RandomOwners(GameState state) => JsonSerializer.Serialize(new { state.CrtRandom, state.EventRandom, state.MenuRandom, state.GlobalRandom, state.RandomEncounters });

    private sealed class ScriptOverride(string fieldName, byte[] script) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = script.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
