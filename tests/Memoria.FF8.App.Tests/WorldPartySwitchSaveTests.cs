using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldPartySwitchSaveTests
{
    [Fact]
    public void World_party_switch_publishes_the_native_trio_and_round_trips_battle_only_members()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = CreateWorldSession(assets);
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        world.Events.Scene.Names[0] = "Leader";
        world.Events.Scene.UnlockedTutorials.Add(17);
        Assert.Equal(new[] { 0, 1, 3 }, world.Events.Scene.Party);
        JsonElement before = world.Events.CaptureWorldEventState();
        long frame = session.State.World!.Frame;

        Switch(world, 255, 4);
        Assert.Equal(new byte[] { 0, 1, 3, 4 }, session.CharacterVitals!.GetBattlePartyRows(4));
        Assert.True(JsonElement.DeepEquals(before, world.Events.CaptureWorldEventState()));
        Switch(world, 4, 255);
        Assert.Equal(new byte[] { 0, 1, 3 }, session.CharacterVitals!.GetBattlePartyRows(4));
        Assert.True(JsonElement.DeepEquals(before, world.Events.CaptureWorldEventState()));

        Switch(world, 1, 5);
        Switch(world, 255, 4);
        Assert.Equal(new[] { 0, 5, 3 }, world.Events.Scene.Party);
        Assert.Equal(new byte[] { 0, 5, 3 }, NativeWorldSaveCompatibility.InitialParty(session.State));
        Assert.Equal(new byte[] { 0, 5, 3, 4 }, session.CharacterVitals!.GetBattlePartyRows(4));
        Assert.Equal(frame, session.State.World!.Frame);
        JsonElement after = world.Events.CaptureWorldEventState().GetProperty("Scene");
        foreach (JsonProperty property in before.GetProperty("Scene").EnumerateObject().Where(property => property.Name != "Party"))
        {
            Assert.True(JsonElement.DeepEquals(property.Value, after.GetProperty(property.Name)), property.Name);
        }

        string path = TestResources.ArtifactFile("world-party-switch.ff8");
        world.WriteGameplaySave(path);
        using (FileStream source = File.OpenRead(path))
        {
            GameplaySaveComponentDocument saved = GameplaySaveCodec.Read(source);
            Assert.Equal(new byte[] { 0, 5, 3 }, saved.Progression!.SceneFacts!.FieldParty);
            Assert.Equal(new byte[] { 4 }, saved.Characters.AdditionalCombatParty);
        }
        GameSession reloaded = new(GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets));
        using GameWorld cold = new(reloaded, directory, sharedAssets: assets);
        cold.EnsureField();
        Assert.Equal(new[] { 0, 5, 3 }, cold.Events.Scene.Party);
        Assert.Equal(new byte[] { 0, 5, 3, 4 }, reloaded.CharacterVitals!.GetBattlePartyRows(4));
        Assert.Equal("Leader", cold.Events.Scene.Names[0]);
        Assert.Contains(17, cold.Events.Scene.UnlockedTutorials);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void World_load_reconciles_only_an_unambiguous_normal_party(bool switchEnabled, bool forcedFieldMember)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = CreateWorldSession(assets);
        session.SetWorldControls(session.WorldControls! with { Menu209 = switchEnabled ? (byte)2 : (byte)0 });
        if (forcedFieldMember)
        {
            NativeCharacterVitals vitals = session.CharacterVitals!.DeepCopy();
            vitals.Records[1] = vitals.Records[1] with { MemberFlags = 7 };
            session.SetCharacterVitals(vitals);
        }
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        world.Events.Scene.Names[0] = "Leader";
        world.Events.Scene.UnlockedTutorials.Add(17);
        GameplaySaveComponentDocument current = world.CaptureGameplaySave();
        GameplaySaveComponentDocument stale = current with { Characters = current.Characters with { CombatParty = [5, 0, 3] } };
        Assert.Equal(new byte[] { 0, 1, 3 }, stale.Progression!.SceneFacts!.FieldParty);
        Assert.Equal((ushort)5, session.CharacterVitals!.Records[0].MemberFlags);

        string path = TestResources.ArtifactFile($"world-stale-party-{switchEnabled}-{forcedFieldMember}.ff8");
        using (FileStream destination = File.Create(path))
        {
            GameplaySaveCodec.Write(destination, stale);
        }
        byte[] original = File.ReadAllBytes(path);
        GameSession restored = new(GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets));
        using GameWorld cold = new(restored, directory, sharedAssets: assets);
        cold.EnsureField();

        int[] fieldParty = switchEnabled && !forcedFieldMember ? [0, 5, 3] : [0, 1, 3];
        Assert.Equal(fieldParty, cold.Events.Scene.Party);
        Assert.Equal(fieldParty.Select(identity => (byte)identity), NativeWorldSaveCompatibility.InitialParty(restored.State));
        Assert.Equal(new byte[] { 5, 0, 3 }, restored.CharacterVitals!.GetBattlePartyRows(4));
        Assert.Equal("Leader", cold.Events.Scene.Names[0]);
        Assert.Contains(17, cold.Events.Scene.UnlockedTutorials);
        Assert.Equal(session.State.CrtRandom, restored.State.CrtRandom);
        Assert.Equal(session.State.Economy!.Gil, restored.State.Economy!.Gil);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Legacy_world_checkpoint_loads_before_first_presentation_callback_and_repairs_party()
    {
        string directory = TestResources.GameDirectory;
        GameSession source = WorldGardenCameraSceneTests.Fixture(46);
        source.SetExamTimer(source.ExamTimer! with { Event182Mask4 = false, SaveGlobalMask4 = false, Event183Mask1 = false });
        NativeCharacterVitals vitals = source.CharacterVitals!.DeepCopy();
        vitals.Records[0] = vitals.Records[0] with { MemberFlags = 5 };
        foreach (int member in new[] { 1, 3, 5 })
        {
            vitals.Records[member] = vitals.Records[member] with { MemberFlags = 1 };
        }
        source.SetCharacterVitals((vitals with { CombatParty = [5, 0, 3] }).RebuildParty());
        source.SetWorldControls(source.WorldControls! with { Menu209 = 2 });

        string path = TestResources.ArtifactFile("world-pre-presentation-party.ff8save");
        using (GameWorld original = new(source, directory, validateWorldPresentation: false))
        {
            original.EnsureField();
            Assert.True(WorldPresentationReadiness.IsDeferred(source.State.World!));
            original.Events.Scene.Party.Clear();
            original.Events.Scene.Party.AddRange([0, 1, 3]);
            source.Save(path, compact: true);
        }

        GameSession restored = new(PlayerStateLoader.Load(path, directory));
        using GameWorld world = new(restored, directory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(0, restored.State.World!.Frame);
        Assert.True(WorldPresentationReadiness.IsDeferred(restored.State.World));
        Assert.Equal(new[] { 0, 5, 3 }, world.Events.Scene.Party);
        Assert.Equal(new byte[] { 5, 0, 3 }, restored.CharacterVitals!.CombatParty);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(1, restored.State.World!.Frame);
    }

    private static GameSession CreateWorldSession(GameAssets assets)
    {
        GameSession session = GameWorld.CreateColdProcess(assets);
        session.StartNewGame();
        using (GameWorld initialization = new(session, assets.GameDirectory, sharedAssets: assets))
        {
            // Select native resource owners without allocating the opening field or replaying gameplay.
        }
        NativeCharacterVitals vitals = session.CharacterVitals!.DeepCopy();
        foreach (int member in new[] { 1, 3, 4, 5 })
        {
            vitals.Records[member] = vitals.Records[member] with { MemberFlags = 1 };
        }
        vitals.Records[0] = vitals.Records[0] with { MemberFlags = 5 };
        session.Restore(session.State with
        {
            Scene = SceneKind.WorldMap,
            FieldLoadContext = null,
            WorldArrival = new WorldTransition(0, 0, -1),
            WorldControls = session.WorldControls! with { Menu209 = 2 },
            GameplayPreferences = session.State.GameplayPreferences with { BattleAllyCount = 4 },
            CharacterVitals = (vitals with { CombatParty = [0, 1, 3] }).RebuildParty()
        });
        return session;
    }

    private static void Switch(GameWorld world, int source, int target)
    {
        PlayerActionResult result = world.Apply(new(PlayerActionKind.SwitchPartyMembers, source, Quantity: target));
        Assert.True(result.Accepted, result.Detail);
        Assert.Equal(0, result.FramesAdvanced);
    }
}
