using System.Diagnostics;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldMusicTests
{
    [Theory]
    [InlineData(41, NativeWorldMusicOperation.Play, 41)]
    [InlineData(24, NativeWorldMusicOperation.Play, 24)]
    [InlineData(89, NativeWorldMusicOperation.Play, 41)]
    [InlineData(81, NativeWorldMusicOperation.Play, 41)]
    [InlineData(0, NativeWorldMusicOperation.Stop, -1)]
    [InlineData(41, NativeWorldMusicOperation.Keep, 41)]
    [InlineData(26, null, 26)]
    public void NativePreloadedSelectionStopAndKeepPreserveSavedVolumeAndRejectMalformedAuthority(int selected, NativeWorldMusicOperation? expected, int actualMusic)
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original game corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var fixtureWorld = new GameWorld(session, game, persistReplayJournal: false);
        for (int input = 0; input < 100; input++)
        {
            fixtureWorld.EnsureField();
            if (fixtureWorld.Events.FieldId == 180)
            {
                break;
            }
            Assert.True(fixtureWorld.Apply(Automatic(fixtureWorld, session)).Accepted);
        }
        Assert.Equal(180, fixtureWorld.Events.FieldId);
        EventEngine engine = fixtureWorld.Events;
        engine.LoadField(100, MusicProgram(selected, expected == NativeWorldMusicOperation.Keep ? selected : 26), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        Assert.Null(engine.LastFault);
        Assert.Equal((byte)selected, engine.Scene.WorldMusicSelection!.Music);
        using var assets = new WorldAssets(game);
        var packet = new WorldTransition(0, 0, -1);
        if (expected is null)
        {
            JsonElement beforeAsyncRefusal = engine.Capture();
            Assert.Throws<NotSupportedException>(() => engine.PrepareWorldMusic(packet, assets.ReadMusicResource));
            Assert.True(JsonElement.DeepEquals(beforeAsyncRefusal, engine.Capture()));
            return;
        }
        Action commit = engine.PrepareWorldMusic(packet, assets.ReadMusicResource);
        session.RequestWorld(packet);
        commit();
        Assert.All(new[] { 201, 202, 213, 214 }, address => Assert.False(session.State.Variables.ContainsKey(address)));
        session.SetEventMemory(session.State.EventMemory);
        NativeWorldMusicLoad loaded = Assert.IsType<NativeWorldMusicLoad>(engine.Scene.WorldMusicLoad);
        Assert.Equal(expected, loaded.Operation);
        Assert.Equal(actualMusic < 0 ? null : (byte?)actualMusic, loaded.Music);
        Assert.Equal((byte?)64, engine.Scene.SoundMusicVolume197);
        Assert.Equal(0, engine.Scene.MusicChannel108!.ChannelId);
        Assert.Equal(actualMusic < 0 ? (byte)255 : (byte)actualMusic, engine.Scene.MusicChannel108.MusicId);
        Assert.Equal(expected == NativeWorldMusicOperation.Play ? 0 : 1, loaded.Bank);
        if (selected == 41)
        {
            using var audio = new FieldAudioPresentation(GetDesktopWindow(), game, new MovieClock(session, game).ToolsDirectory);
            audio.SetOutputPreferences(0, 0);
            audio.ResumeMusic(engine.Scene, engine.FieldId, worldMap: true);
            Assert.Null(audio.Error);
            Assert.Equal(41, audio.PrimaryMusicId);
            Assert.Equal(127, audio.PrimaryMusicVolume);
        }
        JsonElement before = engine.Capture();
        foreach (string property in new[] { "Bank", "RequestedMusic", "DurationMilliseconds" })
        {
            JsonNode invalid = JsonNode.Parse(before.GetRawText())!;
            invalid["Scene"]!["WorldMusicLoad"]![property] = 255;
            Assert.Throws<InvalidDataException>(() => engine.Restore(JsonSerializer.SerializeToElement(invalid)));
            Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        }
        Assert.Throws<InvalidOperationException>(commit);
    }

    [Fact]
    public void AuthenticGardenDepartureChangesNativeMusicAndDeviceOutputAndRecreates()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original game corpus is required.");
        string repository = Repository();
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, game, persistReplayJournal: false);
        ReachGardenGate(world, session);
        world.EnsureField();
        world.SetRenderSize(320, 224);
        Assert.Equal(159, world.Events.FieldId);
        Assert.Equal(41, world.Events.Scene.WorldMusicSelection!.Music);
        byte? storedVolume = world.Events.Scene.SoundMusicVolume197;
        byte? bank = world.Events.Scene.MusicProcess!.Bank201;
        using var audio = new FieldAudioPresentation(GetDesktopWindow(), game, new MovieClock(session, game).ToolsDirectory);
        audio.SetOutputPreferences(0, 0);
        audio.Attach(world.Events);
        audio.ResumeMusic(world.Events.Scene, world.Events.FieldId);
        Assert.Equal(26, audio.PrimaryMusicId);
        List<NativeWorldMusicLoad> requests = [];
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeWorldMusicLoad request)
            {
                requests.Add(request);
            }
        };
        IfritCausalRecreationTests.WalkGateway(world, session, 0, worldExit: true);
        world.EnsureField();
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        NativeWorldMusicLoad loaded = Assert.Single(requests);
        Assert.Equal(NativeWorldMusicOperation.Play, loaded.Operation);
        Assert.Equal((byte?)41, loaded.Music);
        Assert.Equal(42, loaded.Resource!.Section);
        Assert.Equal((0, 127, 60), (loaded.InitialVolume, loaded.TargetVolume, loaded.DurationMilliseconds));
        Assert.Equal(bank ^ 1, loaded.Bank);
        Assert.Equal(storedVolume, world.Events.Scene.SoundMusicVolume197);
        Assert.Equal(new NativeMusicChannelState(0, 41), world.Events.Scene.MusicChannel108);
        var deadline = Stopwatch.StartNew();
        while ((!audio.IsMidiPlaying || deadline.ElapsedMilliseconds < loaded.DurationMilliseconds + 10) && deadline.ElapsedMilliseconds < 3000)
        {
            audio.Update();
            Thread.Sleep(20);
        }
        audio.Update();
        Assert.Null(audio.Error);
        Assert.Equal(41, audio.PrimaryMusicId);
        Assert.Equal(127, audio.PrimaryMusicVolume);
        Assert.True(audio.IsMidiPlaying);
        session.SetEventState(world.Events.Capture());
        GameState current = session.State;
        string path = Path.Combine(repository, "../fixtures/external-fixture.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        session.Save(path, registerJournalReference: false);
        GameState ordinaryLoaded = GameSession.Load(path);
        using (var ordinaryWorld = new GameWorld(new GameSession(ordinaryLoaded), game, persistReplayJournal: false))
        {
            ordinaryWorld.EnsureField();
            Assert.Null(ordinaryWorld.Events.LastFault);
        }
        GameState recreated = GameWorld.RecreateSave(GameSession.LoadReplaySource(path), game);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current with { ReplayJournal = null }), JsonSerializer.SerializeToElement(recreated with { ReplayJournal = null })));
        using var restored = new GameWorld(new GameSession(recreated), game, persistReplayJournal: false);
        restored.EnsureField();
        restored.SetRenderSize(320, 224);
        using (var resumedAudio = new FieldAudioPresentation(GetDesktopWindow(), game, new MovieClock(new GameSession(recreated), game).ToolsDirectory))
        {
            resumedAudio.SetOutputPreferences(0, 0);
            resumedAudio.Attach(restored.Events);
            resumedAudio.ResumeMusic(restored.Events.Scene, restored.Events.FieldId, worldMap: true);
            var resumeDeadline = Stopwatch.StartNew();
            while (!resumedAudio.IsMidiPlaying && resumeDeadline.ElapsedMilliseconds < 3000)
            {
                Thread.Sleep(20);
            }
            Assert.Null(resumedAudio.Error);
            Assert.Equal(41, resumedAudio.PrimaryMusicId);
            Assert.Equal(127, resumedAudio.PrimaryMusicVolume);
            Assert.True(resumedAudio.IsMidiPlaying);
        }
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), restored.Apply(wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Console.WriteLine($"Native world music selected{loaded.RequestedMusic}, resource{loaded.Resource.Section}/track{loaded.Music}, bank{loaded.PriorBank}->{loaded.Bank}, backend0->127/60ms; cold state/pixels/nextWait and actual DirectMusic verified.");
    }

    internal static void ReachGardenGate(GameWorld world, GameSession session)
    {
        int centering = 0;
        for (int input = 0; input < 8000; input++)
        {
            world.EnsureField();
            if (world.Field!.Id == 232 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            PlayerAction next = world.Events.FieldId == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable ? centering++ < 2 ? new PlayerAction(PlayerActionKind.Move, Horizontal: -1) : new PlayerAction(PlayerActionKind.Move, Vertical: 1) : Automatic(world, session);
            PlayerActionResult result = world.Apply(next);
            Assert.True(result.Accepted, $"Fresh world-music route {next} at{world.Events.FieldId}/{world.Events.Frame}: {result.Detail}, trigger{world.Events.NextTrigger}.");
        }
        Assert.Equal(232, world.Field!.Id);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 90)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1, Frames: 30)).Accepted);
        FinishAutomatic(world, session);
        for (int input = 0; input < 3000 && world.Events.Actors.Where(actor => actor.ObjectId is 14 or 15 or 16).Any(actor => actor.NativeScriptMovement is { Active: true } || actor.MovementTarget is not null); input++)
        {
            Assert.True(world.Apply(Automatic(world, session)).Accepted);
        }
        IfritCausalRecreationTests.WalkAdvisoryPoint(world, session, new Vector2(500, world.Events.PlayerActor!.Y));
        IfritCausalRecreationTests.WalkAdvisoryPoint(world, session, new Vector2(650, -2850));
        IfritCausalRecreationTests.WalkDirectToInteraction(world, session, 11);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Interact, 11)).Accepted);
        FinishAutomatic(world, session);
        foreach (int destination in new[] { 139, 137, 172, 165, 163, 162, 161, 160, 159 })
        {
            IfritCausalRecreationTests.WalkGateway(world, session, destination);
        }
        FinishAutomatic(world, session);
    }

    private static void FinishAutomatic(GameWorld world, GameSession session)
    {
        for (int input = 0; input < 3000; input++)
        {
            world.EnsureField();
            if (world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                return;
            }
            Assert.True(world.Apply(Automatic(world, session)).Accepted);
        }
        Assert.Fail("Ordinary automatic dialogue/field callbacks exceeded the bounded route.");
    }

    private static PlayerAction Automatic(GameWorld world, GameSession session)
    {
        DialogueState? dialogue = world.Events.CurrentDialogue;
        return session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int first ? new PlayerAction(PlayerActionKind.Choose, first) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "recreate-checkpoint.md")))
        {
            directory = directory.Parent;
        }
        return directory!.FullName;
    }

    private static byte[] MusicProgram(int selection, int initialMusic = 26)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, initialMusic), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.PSHN_L, selection), (Jsm.Opcode.POPM_B, 268), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.POPM_B, 197), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
