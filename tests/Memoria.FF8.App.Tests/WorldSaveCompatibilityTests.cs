using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldSaveCompatibilityTests
{
    [Fact]
    public void MissingProcessUsesFirstInitializationDefaultsAndRetainsExplicitPersistentBytes()
    {
        GameState source = GameSession.Load(TestResources.FixturePath("WorldSaveCompatibilityTests.Source"));
        NativeEventMemory memory = source.EventMemory.Write(196, 4, NativeScalarValue.Exact(0x12345678)).Write(268, 4, NativeScalarValue.Exact(0x12345629));
        GameSession session = new(source with { EventState = null, DepartingFieldState = null, OriginalSave = null, WorldEventState = null, EventMemory = memory });
        using GameWorld host = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        Assert.False(host.Events.HasFieldProgram);
        Assert.Null(host.Events.Scene.MusicProcess!.Pending);
        Assert.Equal((byte?)0, host.Events.Scene.MusicProcess.Bank201);
        Assert.Equal((byte?)255, host.Events.Scene.MusicProcess.Bank202);
        Assert.Null(host.Events.Scene.MusicProcess.Buffer0);
        Assert.Null(host.Events.Scene.MusicProcess.Buffer1);
        Assert.Equal(-1, host.Events.Scene.MusicChannel108!.ChannelId);
        Assert.Equal(-1, host.Events.Scene.MusicChannel112!.ChannelId);
        Assert.Equal((byte?)0x56, host.Events.Scene.SoundMusicVolume197);
        Assert.Equal((byte?)0x34, host.Events.Scene.SoundMusicVolume198);
        Assert.Equal((byte)41, host.Events.Scene.WorldMusicSelection!.Music);
        Assert.Equal(0x78, session.EventMemory.Read(196, 1).RequireExact());
        Assert.Equal(memory.Read(269, 4).RequireExact(), session.EventMemory.Read(269, 4).RequireExact());
        Assert.False(session.EventMemory.Overlaps(197, 2));
        Assert.False(session.EventMemory.Overlaps(268, 1));
        host.Events.Scene.Names[0] = "Published process";
        host.PublishEventProcess();
        Assert.Null(session.State.EventState);
        Assert.Equal("Published process", session.State.WorldEventState!.Value.GetProperty("Scene").GetProperty("Names").GetProperty("0").GetString());
        using GameWorld restored = new(new GameSession(session.State), TestResources.GameDirectory, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.Equal((byte)41, restored.Events.Scene.WorldMusicSelection!.Music);
        Assert.Equal((byte?)0x56, restored.Events.Scene.SoundMusicVolume197);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingWorldVmInitializesWithoutResettingProgressAndPersistsAcrossTwoColdLoads(bool rinoa)
    {
        string directory = TestResources.GameDirectory;
        string sourcePath = TestResources.FixturePath("WorldSaveCompatibilityTests.Source");
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);
        GameState source = GameSession.Load(sourcePath);
        NativeCharacterVitals vitals = source.CharacterVitals!;
        byte identity = rinoa ? (byte)4 : (byte)5;
        vitals = vitals with { CombatParty = [vitals.FindPhysicalRow(identity), vitals.FindPhysicalRow(1), vitals.FindPhysicalRow(2)] };
        NativeLimitProgress limits = source.LimitProgress! with { TrainingSelection = 3, AngeloFlags = 0, TrainingCounters = ImmutableArray.CreateRange(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }) };
        GameState historical = source with
        {
            EventState = null,
            DepartingFieldState = null,
            OriginalSave = null,
            WorldEventState = null,
            CharacterVitals = vitals,
            LimitProgress = limits,
            EventMemory = source.EventMemory.Write(244, 4, NativeScalarValue.Exact(591))
        };
        GameSession session = new(historical);
        using GameWorld host = new(session, directory, validateWorldPresentation: false);
        Assert.NotNull(session.State.WorldEventState);
        Assert.Null(session.State.EventState);
        Assert.Null(session.State.DepartingFieldState);
        Assert.Equal(JsonSerializer.Serialize(historical), JsonSerializer.Serialize(session.State with { WorldEventState = null }));
        Assert.Equal(new[] { (int)identity, 1, 2 }, host.Events.Scene.Party);
        host.EnsureField();
        host.Events.Scene.Names[0] = "First save";
        host.Events.Scene.UnlockedTutorials.Add(17);
        uint previous = unchecked((uint)session.State.EventMemory.Read(244, 4).RequireExact());
        for (int callback = 0; callback < 8 && unchecked((uint)session.State.EventMemory.Read(244, 4).RequireExact()) >= previous; callback++)
        {
            Assert.True(host.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(unchecked((uint)session.State.EventMemory.Read(244, 4).RequireExact()) < previous, "The retained moving world must cross the Angelo movement threshold.");
        Assert.Equal(rinoa ? (byte)0 : (byte)1, session.State.LimitProgress!.TrainingCounters[3]);
        string firstPath = Path.Combine(Path.GetTempPath(), "ff8-world-process-" + Guid.NewGuid().ToString("N") + ".ff8");
        string secondPath = Path.Combine(Path.GetTempPath(), "ff8-world-process-" + Guid.NewGuid().ToString("N") + ".ff8");
        try
        {
            session.Save(firstPath);
            GameSession firstSession = new(GameSession.Load(firstPath));
            using GameWorld first = new(firstSession, directory, validateWorldPresentation: false);
            first.EnsureField();
            Assert.Equal("First save", first.Events.Scene.Names[0]);
            Assert.Contains(17, first.Events.Scene.UnlockedTutorials);
            Assert.Equal(host.Events.Scene.Party, first.Events.Scene.Party);
            AssertWorldAndLimitsEqual(session, firstSession);
            PlayerAction next = new(PlayerActionKind.Wait);
            Assert.Equal(host.Apply(next), first.Apply(next));
            AssertWorldAndLimitsEqual(session, firstSession);
            first.Events.Scene.Names[0] = "Second save";
            first.Events.Scene.UnlockedTutorials.Add(18);
            firstSession.Save(secondPath);
            GameSession secondSession = new(GameSession.Load(secondPath));
            using GameWorld second = new(secondSession, directory, validateWorldPresentation: false);
            second.EnsureField();
            Assert.Equal("Second save", second.Events.Scene.Names[0]);
            Assert.Contains(17, second.Events.Scene.UnlockedTutorials);
            Assert.Contains(18, second.Events.Scene.UnlockedTutorials);
            Assert.Equal(first.Events.Scene.Party, second.Events.Scene.Party);
            AssertWorldAndLimitsEqual(firstSession, secondSession);
            Assert.Equal(first.Apply(next), second.Apply(next));
            AssertWorldAndLimitsEqual(firstSession, secondSession);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
    }

    [Fact]
    public void CompleteWorldDepartureKeepsItsAuthenticVmAndDoesNotInitializeCompatibility()
    {
        GameState source = GameSession.Load(TestResources.FixturePath("WorldSaveCompatibilityTests.Source"));
        JsonElement expected = Assert.IsType<JsonElement>(source.DepartingFieldState ?? source.EventState);
        GameSession session = new(source);
        using GameWorld host = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        Assert.Null(session.PrepareSnapshot().WorldEventState);
        host.EnsureField();
        Assert.Null(session.State.WorldEventState);
        Assert.True(JsonElement.DeepEquals(expected, Assert.IsType<JsonElement>(session.State.EventState)));
        Assert.Equal(expected.GetProperty("Scene").GetProperty("Party").EnumerateArray().Select(value => value.GetInt32()), host.Events.Scene.Party);
        Assert.Equal(JsonSerializer.Serialize(source.LimitProgress), JsonSerializer.Serialize(session.State.LimitProgress));
    }

    [Fact]
    public void MalformedRetainedWorldProcessRefusesInsteadOfReinitializingIt()
    {
        GameState source = GameSession.Load(TestResources.FixturePath("WorldSaveCompatibilityTests.Source")) with { EventState = null, DepartingFieldState = null, OriginalSave = null, WorldEventState = null };
        GameSession session = new(source);
        using (GameWorld host = new(session, TestResources.GameDirectory, validateWorldPresentation: false))
        {
            host.EnsureField();
        }
        JsonNode malformed = JsonNode.Parse(session.State.WorldEventState!.Value.GetRawText())!;
        malformed["Scene"]!["Party"] = new JsonArray(4, 255);
        GameSession rejected = new(session.State with { WorldEventState = JsonSerializer.SerializeToElement(malformed) });
        Assert.Throws<InvalidDataException>(() => new GameWorld(rejected, TestResources.GameDirectory, validateWorldPresentation: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetainedWorldProcessRejectsMissingOrContradictoryBattleModeHistory(bool missing)
    {
        GameState source = GameSession.Load(TestResources.FixturePath("WorldSaveCompatibilityTests.Source")) with { EventState = null, DepartingFieldState = null, OriginalSave = null, WorldEventState = null };
        GameSession session = new(source);
        using (GameWorld host = new(session, TestResources.GameDirectory, validateWorldPresentation: false))
        {
            host.EnsureField();
        }
        JsonNode malformed = JsonNode.Parse(session.State.WorldEventState!.Value.GetRawText())!;
        if (missing)
        {
            malformed["Scene"]!["NativeBattleFlagWrites"] = null;
        }
        else
        {
            JsonNode writer = malformed["Scene"]!["NativeBattleFlagWrites"]![0]!;
            writer["PersistentFlags"] = writer["PersistentFlags"]!.GetValue<int>() ^ 4;
        }
        GameSession rejected = new(session.State with { WorldEventState = JsonSerializer.SerializeToElement(malformed) });
        Assert.Throws<InvalidDataException>(() => new GameWorld(rejected, TestResources.GameDirectory, validateWorldPresentation: false));
    }

    private static void AssertWorldAndLimitsEqual(GameSession expected, GameSession actual)
    {
        Assert.Equal(JsonSerializer.Serialize(expected.State.World), JsonSerializer.Serialize(actual.State.World));
        Assert.Equal(JsonSerializer.Serialize(expected.State.LimitProgress), JsonSerializer.Serialize(actual.State.LimitProgress));
        Assert.Equal(JsonSerializer.Serialize(expected.State.EventMemory), JsonSerializer.Serialize(actual.State.EventMemory));
    }

    [Fact]
    public void PendingNativeWorldReturnColdLoadRetainsItsUnallocatedEventProcess()
    {
        GameState source = GameSession.Load(TestResources.FixturePath("WorldSaveCompatibilityTests.Source")) with { EventState = null, DepartingFieldState = null, OriginalSave = null, WorldEventState = null };
        GameSession session = new(source);
        using GameWorld host = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        host.EnsureField();
        host.Events.Scene.Names[0] = "Pending return";
        host.Events.Scene.UnlockedTutorials.Add(19);
        session.SetWorldEventState(host.Events.CaptureWorldEventState());
        int[] expectedParty = host.Events.Scene.Party.ToArray();
        GameState departure = session.State;
        WorldState world = departure.World!;
        NativeSavedWorldCarReturn receipt = NativeSavedWorldCarReturn.Create(world, checked((ushort)departure.FieldId), departure.WorldEntryMemory!.StoryWord, host.WorldScene!.PlaceId);
        // Exercise the native committed-return boundary without replaying earlier travel.
        FieldTransition transition = new(106, 0, 0, 0, 0);
        session.CommitPreparedCarFieldEntry(departure, world, transition, receipt, NativeWorldSalaryState.Begin(departure), _ => { });
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Null(session.State.EventState);
        Assert.Null(session.State.DepartingFieldState);
        string path = Path.Combine(Path.GetTempPath(), "ff8-world-pending-return-" + Guid.NewGuid().ToString("N") + ".ff8");
        try
        {
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory, validateWorldPresentation: false);
            Assert.Equal(expectedParty, restored.Events.Scene.Party);
            Assert.Equal("Pending return", restored.Events.Scene.Names[0]);
            Assert.Contains(19, restored.Events.Scene.UnlockedTutorials);
            Assert.NotNull(restored.Events.Scene.MusicProcess);
            Assert.Equal(transition, restoredSession.State.FieldArrival);
            restored.EnsureField();
            Assert.True(restored.Events.HasFieldProgram);
            Assert.Equal(expectedParty, restored.Events.Scene.Party);
            Assert.Equal("Pending return", restored.Events.Scene.Names[0]);
            restored.PublishEventProcess();
            Assert.Null(restoredSession.State.WorldEventState);
            restoredSession.Save(path);
            using GameWorld fieldReload = new(new GameSession(GameSession.Load(path)), TestResources.GameDirectory, validateWorldPresentation: false);
            fieldReload.EnsureField();
            Assert.True(fieldReload.Events.HasFieldProgram);
            Assert.Equal(expectedParty, fieldReload.Events.Scene.Party);
            Assert.Equal("Pending return", fieldReload.Events.Scene.Names[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
