using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldPartyResumeTests
{
    [Fact]
    public void Current_three_party_poses_install_after_native_binding_before_any_ordinary_callback_and_roundtrip()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = ThreePartyDocument(assets);
        string source = JsonSerializer.Serialize(document);
        GameState restored = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
        Assert.Equal(source, JsonSerializer.Serialize(document));
        Assert.Null(restored.NativeFieldLocation);
        Assert.Equal(document.Progression!.Clocks.ExamTimer!.ClockCalls, restored.ExamTimer!.ClockCalls);
        Assert.Equal(document.Progression.Clocks.Playtime, restored.Playtime);
        Assert.Equal(123456u, restored.Economy!.Gil);
        Assert.Equal(123456u, restored.CharacterVitals!.Records[0].Experience);
        Assert.Equal(1, restored.EventState!.Value.GetProperty("Frame").GetInt64());

        GameSession session = new(restored);
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.Equal(new[] { 0, 1, 4 }, world.Events.Scene.Party);
        Assert.Equal(NativeFieldInitializationStage.Completed, world.Events.Scene.NativeFieldInitialization!.Stage);
        Assert.Equal(1, world.Events.Scene.FieldCallbackClock!.InitializationTransactions);
        Assert.Equal(0, world.Events.Scene.FieldCallbackClock.OrdinaryCallbacks);
        Assert.Equal(0, world.Events.Scene.FieldCallbackClock.TimerCalls);
        GameplaySaveFieldLocation poses = world.Events.CaptureGameplaySaveFieldParty();
        NativeFieldLocationPose leader = new((short)document.Resume.Field!.X, (short)document.Resume.Field.Y, (ushort)document.Resume.Field.Triangle, document.Resume.Field.Direction);
        Assert.Equal(leader, poses.PartyPositions[0]);
        Assert.Equal(document.Progression.FieldLocation!.PartyPositions[1], poses.PartyPositions[1]);
        Assert.Equal(document.Progression.FieldLocation.PartyPositions[2], poses.PartyPositions[2]);
        Assert.NotEqual(document.Progression.FieldLocation.PartyPositions[0], poses.PartyPositions[0]);
        Assert.Equal(3, world.Events.Scene.FieldPartyNativeActorIds.Distinct().Count());
        for (int slot = 0; slot < 3; slot++)
        {
            int binding = world.Events.Scene.FieldPartyNativeActorIds[slot];
            ActorState actor = world.Events.Actors[world.Events.Scene.NativeActorObjectIds[binding]];
            Assert.Equal(world.Events.Scene.Party[slot], actor.CharacterId);
            Assert.Equal(poses.PartyPositions[slot]!.X, actor.X);
            Assert.Equal(poses.PartyPositions[slot]!.Y, actor.Y);
            Assert.Equal(poses.PartyPositions[slot]!.Direction, actor.Direction);
        }
        GameplaySaveComponentDocument sampled = document with { Progression = document.Progression with { FieldLocation = poses } };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, sampled);
        encoded.Position = 0;
        GameState semanticReload = GameplaySaveSessionLoader.Restore(GameplaySaveCodec.Read(encoded), directory, sharedAssets: assets);
        GameSession semanticSession = new(semanticReload);
        using GameWorld semanticWorld = new(semanticSession, directory, sharedAssets: assets);
        semanticWorld.EnsureField();
        Assert.Equal(poses.PartyPositions, semanticWorld.Events.CaptureGameplaySaveFieldParty().PartyPositions);
        Assert.Equal(restored.ExamTimer!.ClockCalls, semanticReload.ExamTimer!.ClockCalls);
        GameState snapshot = session.PrepareSnapshot();
        GameSession cold = new(snapshot);
        using GameWorld roundtrip = new(cold, directory, sharedAssets: assets);
        roundtrip.EnsureField();
        Assert.Equal(poses.PartyPositions, roundtrip.Events.CaptureGameplaySaveFieldParty().PartyPositions);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), roundtrip.Events.Capture()));
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), roundtrip.Apply(wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), roundtrip.Events.Capture()));
        Assert.Equal(123456u, cold.State.Economy!.Gil);
        Assert.Throws<InvalidOperationException>(() => roundtrip.Events.InitializeGameplaySaveFieldParty(document.Progression.FieldLocation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_later_follower_pose_or_missing_binding_never_publishes_a_partial_candidate(bool missingBinding)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = ThreePartyDocument(assets);
        GameplaySaveProgression progress = document.Progression!;
        NativeFieldLocationPose?[] poses = progress.FieldLocation!.PartyPositions.ToArray();
        poses[2] = poses[2]! with { Triangle = ushort.MaxValue };
        document = document with { Progression = progress with { FieldLocation = progress.FieldLocation with { PartyPositions = poses } } };
        if (missingBinding)
        {
            document = document with { Progression = document.Progression! with { SceneFacts = document.Progression.SceneFacts! with { FieldParty = [0, 1, 255] } } };
        }
        string before = JsonSerializer.Serialize(document);
        Assert.Throws<NotSupportedException>(() => GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
        Assert.Equal(before, JsonSerializer.Serialize(document));
        Assert.NotNull(assets.LoadField(165));
    }

    [Fact]
    public void Current_actor_sampling_ignores_historical_menu_positions_and_requires_ready_bindings()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = ThreePartyDocument(assets);
        GameState restored = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
        GameSession session = new(restored);
        using GameWorld world = new(session, directory, sharedAssets: assets);
        Assert.Throws<NotSupportedException>(() => world.Events.CaptureGameplaySaveFieldParty());
        world.EnsureField();
        GameplaySaveFieldLocation actual = world.Events.CaptureGameplaySaveFieldParty();
        Assert.Equal(document.Progression!.FieldLocation!.PartyPositions[1], actual.PartyPositions[1]);
        Assert.Null(session.State.NativeFieldLocation);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(restored.ExamTimer), JsonSerializer.SerializeToElement(session.State.ExamTimer)));
    }

    internal static GameplaySaveComponentDocument ThreePartyDocument(GameAssets assets)
    {
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameplaySaveProgression progress = document.Progression!;
        NativeCharacterVital[] records = document.Characters.Records.ToArray();
        records[1] = records[1] with { MemberFlags = (byte)(records[1].MemberFlags | 1) };
        records[4] = records[4] with { MemberFlags = (byte)(records[4].MemberFlags | 1) };
        NativeFieldLocationPose Pose(short x, short y, byte direction)
        {
            int triangle = FieldPartyTraceGeometry.Resolve(assets.LoadField(165).Walkmesh, x, y, 567) ?? throw new InvalidDataException("The selected field has no follower plane.");
            return new(x, y, checked((ushort)triangle), direction);
        }
        NativeFieldLocationPose[] poses = [Pose(-690, -8580, 64), Pose(-710, -8580, 32), Pose(-690, -8600, 192)];
        return document with
        {
            Characters = document.Characters with { Records = records, CombatParty = [0, 1, 4] },
            Progression = progress with { SceneFacts = progress.SceneFacts! with { FieldParty = [0, 1, 4] }, FieldLocation = new(165, 164, poses) }
        };
    }
}
