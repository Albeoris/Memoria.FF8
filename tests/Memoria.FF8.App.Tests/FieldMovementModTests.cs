using System.Text;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldMovementModTests
{
    [Theory]
    [InlineData(0, 1f, 0f)]
    [InlineData(64, 0f, -1f)]
    [InlineData(128, -1f, 0f)]
    [InlineData(184 + 222, -0.8577286f, 0.5141027f)]
    public void FieldGuideConvertsATargetToInputAxesUnderTheCombinedHeading(int inputDirection, float horizontal, float vertical)
    {
        PlayerAction action = HeadlessPlayer.FieldGuideAction(100, 0, inputDirection);
        Assert.Equal(PlayerActionKind.Move, action.Kind);
        Assert.InRange(action.Horizontal, horizontal - 0.00001f, horizontal + 0.00001f);
        Assert.InRange(action.Vertical, vertical - 0.00001f, vertical + 0.00001f);
    }

    [Fact]
    public void FieldGuideRetainsItsWorldTargetUnderTheActiveMovementRotation()
    {
        string game = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.FieldMovementMods.1");
        FieldSceneMods active = FieldSceneMods.Empty;
        using GameWorld world = new(new GameSession(GameSession.Load(source)), game, readSceneMods: () => active);
        world.EnsureField();
        active = Prepare(Patch("Memoria/rotation", world.Field!.Id, world.Field.Name, -47.8125f));
        world.EnsureField();
        ActorState actor = world.Events.PlayerActor!;
        float x = actor.X;
        float y = actor.Y;
        PlayerAction action = HeadlessPlayer.FieldGuideAction(100, 0, world.Events.InputAxisDirection + world.Events.PlayerInputDirectionOffset);
        Assert.True(world.Apply(action).Accepted);
        Assert.True(actor.X > x);
        Assert.InRange(Math.Abs(actor.Y - y), 0, (actor.X - x) * 0.1f);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void RotationChangesNativePlayerInputWithoutChangingTheLoadedSnapshot()
    {
        string game = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.FieldMovementMods.1");
        using GameWorld original = new(new GameSession(GameSession.Load(source)), game);
        original.EnsureField();
        FieldSceneMods active = Prepare(Patch("Memoria/rotation", original.Field!.Id, original.Field.Name, 180));
        using GameWorld rotated = new(new GameSession(GameSession.Load(source)), game, readSceneMods: () => active);
        rotated.EnsureField();
        Assert.Equal((byte)0, original.Events.PlayerInputDirectionOffset);
        Assert.Equal((byte)128, rotated.Events.PlayerInputDirectionOffset);
        Assert.True(JsonElement.DeepEquals(original.Events.Capture(), rotated.Events.Capture()));
        FieldFixedPosition? initialPosition = original.Events.Scene.NativePlayerPosition;

        Assert.True(original.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: -1)).Accepted);
        Assert.True(rotated.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: 1)).Accepted);
        Assert.NotEqual(initialPosition, original.Events.Scene.NativePlayerPosition);
        Assert.Equal(original.Events.Scene.NativePlayerPosition, rotated.Events.Scene.NativePlayerPosition);
        Assert.True(JsonElement.DeepEquals(original.Events.Capture(), rotated.Events.Capture()));
        Assert.Null(rotated.Events.LastFault);
    }

    [Theory]
    [InlineData(45f, 32)]
    [InlineData(-45f, 224)]
    [InlineData(-47.8125f, 222)]
    [InlineData(1f, 1)]
    [InlineData(0.703125f, 0)]
    [InlineData(2.109375f, 2)]
    public void LiveCatalogSelectionUsesFirstMatchingMovementAndRestoresTheNativeHeading(float rotationDegrees, int expectedOffset)
    {
        string game = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.FieldMovementMods.1");
        FieldSceneMods active = FieldSceneMods.Empty;
        using GameWorld world = new(new GameSession(GameSession.Load(source)), game, readSceneMods: () => active);
        world.EnsureField();
        int fieldId = world.Field!.Id;
        string fieldName = world.Field.Name;
        JsonElement before = world.Events.Capture();
        ModFile unrelated = Patch("Memoria/unrelated", fieldId + 1, "other_field", 90);
        active = Prepare(unrelated, Patch("Memoria/first", fieldId, fieldName, rotationDegrees), Patch("Memoria/second", fieldId, fieldName, 90));
        world.Render(TimeSpan.Zero);
        Assert.Equal((byte)expectedOffset, world.Events.PlayerInputDirectionOffset);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        active = Prepare(Patch("Memoria/second", fieldId, fieldName, 90));
        world.Render(TimeSpan.Zero);
        Assert.Equal((byte)64, world.Events.PlayerInputDirectionOffset);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        active = Prepare(unrelated);
        world.Render(TimeSpan.Zero);
        Assert.Equal((byte)0, world.Events.PlayerInputDirectionOffset);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        active = Prepare(Patch("Memoria/first", fieldId, fieldName, rotationDegrees));
        world.Render(TimeSpan.Zero);
        Assert.Equal((byte)expectedOffset, world.Events.PlayerInputDirectionOffset);
        active = FieldSceneMods.Empty;
        world.Render(TimeSpan.Zero);
        Assert.Equal((byte)0, world.Events.PlayerInputDirectionOffset);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }

    [Fact]
    public void BundledReorientationPatchTargetsOnlyTheGgkodo1InputHeading()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Reorientation", "fields", "ggkodo1.json");
        ModFile file = new("Memoria/Reorientation", ModFormat.Memoria, "fields/ggkodo1.json", 1, () => File.ReadAllBytes(path));
        FieldScenePatch patch = Assert.Single(Prepare(file).Patches).Patch;
        Assert.Equal("ggkodo1", patch.FieldName);
        Assert.Empty(patch.Backgrounds);
        Assert.Empty(patch.Actors);
        Assert.Equal(-47.8125f, Assert.IsType<FieldMovementPatch>(patch.Movement).RotationDegrees);
    }

    private static ModFile Patch(string package, int fieldId, string fieldName, float rotationDegrees)
    {
        string json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            fieldId,
            fieldName,
            movement = new { rotationDegrees }
        });
        return new(package, ModFormat.Memoria, $"fields/{fieldName}.json", 1, () => Encoding.UTF8.GetBytes(json));
    }

    private static FieldSceneMods Prepare(params ModFile[] files)
    {
        Assert.True(FieldSceneMods.TryPrepare(new(1, [], files), out FieldSceneMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        return mods;
    }
}
