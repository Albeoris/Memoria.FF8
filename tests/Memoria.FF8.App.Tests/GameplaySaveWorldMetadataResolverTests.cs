using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveWorldMetadataResolverTests
{
    [Fact]
    public void Current_choices_override_departure_preferences_and_lookup_uses_current_raw_coordinates()
    {
        GameplaySaveResumeDescriptor resume = Resume() with { World = Position() with { AnimationVariant = 0, MapDisplayMode = 0 } };
        GameplaySaveWorldData saved = Saved() with { Return = Return(1, 2) };
        int calls = 0;
        GameplaySaveWorldResumeMetadata metadata = GameplaySaveWorldMetadataResolver.ResolvePolicy(resume, saved, (x, rawZ) =>
        {
            calls++;
            Assert.Equal(17151, x);
            Assert.Equal(-28610, rawZ);
            return -47;
        });

        Assert.Equal(new GameplaySaveWorldResumeMetadata(1024, 0, 0, -47), metadata);
        Assert.Equal(1, calls);
        Assert.Equal((ushort)3072, saved.Return!.CameraYaw);
        Assert.Equal((short)91, saved.Return.MapLocation);
    }

    [Fact]
    public void Older_component_position_reads_without_optional_choices_and_uses_explicit_known_preferences()
    {
        GameplaySaveWorldPosition position = JsonSerializer.Deserialize<GameplaySaveWorldPosition>("{\"X\":17151,\"Z\":-28610,\"Height\":123,\"BodyYaw\":1024,\"EntityType\":0}")!;
        Assert.Null(position.AnimationVariant);
        Assert.Null(position.MapDisplayMode);
        GameplaySaveResumeDescriptor resume = Resume() with { World = position };
        GameplaySaveWorldResumeMetadata metadata = GameplaySaveWorldMetadataResolver.ResolvePolicy(resume, Saved() with { Return = Return(1, 2) }, (_, _) => 7);
        Assert.Equal(new GameplaySaveWorldResumeMetadata(1024, 1, 2, 7), metadata);
        Assert.DoesNotContain("AnimationVariant", JsonSerializer.Serialize(position));
        Assert.DoesNotContain("MapDisplayMode", JsonSerializer.Serialize(position));
    }

    [Fact]
    public void Initial_map_preference_can_complete_a_current_animation_choice_without_a_return()
    {
        GameplaySaveResumeDescriptor resume = Resume() with { World = Position() with { AnimationVariant = 1 } };
        GameplaySaveWorldResumeMetadata metadata = GameplaySaveWorldMetadataResolver.ResolvePolicy(resume, Saved() with { InitialMapDisplayMode = 2 }, (_, _) => 7);
        Assert.Equal(new GameplaySaveWorldResumeMetadata(1024, 1, 2, 7), metadata);
    }

    [Fact]
    public void Global_map_preference_can_survive_a_transport_change_but_animation_cannot_be_inferred_from_it()
    {
        GameplaySaveWorldData saved = Saved() with { Return = Return(1, 2) with { EntityType = 1 } };
        Assert.Throws<NotSupportedException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(Resume(), saved, (_, _) => throw new InvalidOperationException("Lookup must not run.")));
        GameplaySaveResumeDescriptor current = Resume() with { World = Position() with { AnimationVariant = 0 } };
        Assert.Equal(new GameplaySaveWorldResumeMetadata(1024, 0, 2, 7), GameplaySaveWorldMetadataResolver.ResolvePolicy(current, saved, (_, _) => 7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Missing_meaningful_preference_rejects_before_resource_lookup(int missing)
    {
        GameplaySaveWorldPosition position = missing == 0 ? Position() with { MapDisplayMode = 0 } : Position() with { AnimationVariant = 0 };
        Assert.Throws<NotSupportedException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(Resume() with { World = position }, Saved(), (_, _) => throw new InvalidOperationException("Lookup must not run.")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(35)]
    [InlineData(36)]
    [InlineData(37)]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(48)]
    [InlineData(50)]
    public void Supported_normal_load_entities_share_the_same_metadata_policy(byte entity)
    {
        GameplaySaveResumeDescriptor resume = Resume() with { World = Position() with { EntityType = entity, AnimationVariant = 1, MapDisplayMode = 2 } };
        Assert.Equal(new GameplaySaveWorldResumeMetadata(1024, 1, 2, -128), GameplaySaveWorldMetadataResolver.ResolvePolicy(resume, Saved(), (_, _) => -128));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 3)]
    [InlineData(255, 255)]
    public void Invalid_current_preferences_are_not_replaced_by_valid_departure_values(byte animation, byte mapDisplay)
    {
        GameplaySaveResumeDescriptor resume = Resume() with { World = Position() with { AnimationVariant = animation, MapDisplayMode = mapDisplay } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(resume, Saved() with { Return = Return(0, 0) }, (_, _) => throw new InvalidOperationException("Lookup must not run.")));
    }

    [Fact]
    public void Invalid_legacy_preferences_unknown_transport_and_out_of_width_lookup_fail_explicitly()
    {
        Assert.Throws<InvalidDataException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(Resume(), Saved() with { Return = Return(2, 0) }, (_, _) => 7));
        Assert.Throws<InvalidDataException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(Resume(), Saved() with { Return = Return(0, 255) }, (_, _) => 7));
        GameplaySaveResumeDescriptor complete = Resume() with { World = Position() with { AnimationVariant = 0, MapDisplayMode = 0 } };
        Assert.Throws<NotSupportedException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(complete with { World = complete.World! with { EntityType = 16 } }, Saved(), (_, _) => throw new InvalidOperationException("Lookup must not run.")));
        Assert.Throws<InvalidDataException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(complete, Saved(), (_, _) => 128));
        Assert.Throws<InvalidDataException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(complete, Saved(), (_, _) => -129));
        Assert.Throws<InvalidOperationException>(() => GameplaySaveWorldMetadataResolver.ResolvePolicy(new(1, GameplaySaveResumeKind.Field, 106, 105, new(0, 0, 0, 0), null), Saved(), (_, _) => 7));
    }

    [Fact]
    public void Configured_installed_lookup_uses_selected_resources_without_allocating_a_scene()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets selected = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameState source = session.State with { Scene = SceneKind.WorldMap, CharacterVitals = selected.ReadNewGameCharacterVitals(), Economy = selected.ReadNewGameEconomy(), LimitProgress = selected.ReadNewGameLimitProgress() };
        GameplaySaveResumeDescriptor resume = Resume() with { FieldId = source.FieldId, World = Position() with { AnimationVariant = 0, MapDisplayMode = 2 } };
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(source, resume);
        GameplaySaveWorldResumeMetadata metadata = GameplaySaveWorldMetadataResolver.Resolve(document, directory, selected.Resources);
        using WorldAssets lookup = new(directory, selected.Resources);
        Assert.Equal(lookup.GetNativeMapLocationId(resume.World!.X, resume.World.Z), metadata.MapLocation);
        Assert.Equal((ushort)1024, metadata.CameraYaw);
        Assert.Equal((byte)0, metadata.AnimationVariant);
        Assert.Equal((byte)2, metadata.MapDisplayMode);
        Assert.Equal(0, lookup.LoadedSegmentCount);
        Assert.Equal(0, lookup.LoadedTextureCount);
        Assert.Null(source.World);
    }

    private static GameplaySaveWorldPosition Position() => new(17151, -28610, 123, 1024, 0);
    private static GameplaySaveResumeDescriptor Resume() => new(1, GameplaySaveResumeKind.World, 106, 105, null, Position());
    private static GameplaySaveWorldReturn Return(byte animation, byte mapDisplay) => new(GameplaySaveWorldReturnKind.Foot, 105, 4321, new(1, 2, 3, 4), 3072, 0, 255, 255, animation, mapDisplay, 91) { FootMode = 0 };
    private static GameplaySaveWorldData Saved() => new() { Slot4 = new(255, new(0, 0, 0, 0)), Slot5 = new(255, new(0, 0, 0, 0)), GardenPose = null, RagnarokPose = null, Return = null, InitialMapDisplayMode = null, FuelDistance = null, EventFlags = null, EventVariable0 = null, EventVariable1 = null, OriginalAngles = null, EntryMode = null, TerrainFlags = null, UnresolvedEntry = null };
}
