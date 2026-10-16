using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeActorlessFieldEntryTests
{
    [Theory]
    [InlineData(580, 2, 557)]
    [InlineData(580, 5, 558)]
    [InlineData(581, 1, 557)]
    public void OriginalPaintingFieldCompletesUnboundNativeLoadShowsTitleAndReturnsWithIndependentRestoration(int field, int painting, int destination)
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original resources are required.");
        string source = TestResources.FixturePath("App.Tests.NativeActorlessFieldEntryTests.1");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        if (field == 580)
        {
            //Declared original580 loader fixture with its original B320 selector.
            //581 uses the unchanged authentic source;580 has no inferred travel.
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            session.SetEventMemory(session.EventMemory.Write(320, 1, NativeScalarValue.Exact(painting)));
            session.RequestField(new FieldTransition(580, short.MaxValue, short.MaxValue, 0, 0));
            world.EnsureField();
        }
        Assert.Equal(field, world.Events.FieldId);
        Assert.Equal(0, world.Events.Frame);
        PartyTraceSample? firstCarrier = world.Events.Scene.FieldLoadFirstRecordZero;
        PartyTraceSample? secondCarrier = world.Events.Scene.FieldLoadSecondRecordZero;
        JsmProgram raw = JsmProgram.Read(world.Field!.Jsm);
        Assert.Equal(4, raw.NativeActorCount);
        try
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Original{field}: NativeActorCount={raw.NativeActorCount}; NativeObjects={string.Join(',', raw.NativeObjects)}; objects={JsonSerializer.Serialize(raw.Objects.Select(obj => new { obj.Index, obj.Label, obj.BackgroundModuleIndex, obj.IsDoor }))}; actors={JsonSerializer.Serialize(world.Events.Actors.Select(actor => new { actor.ObjectId, actor.Active, actor.ModelId, actor.CharacterId }))}", exception);
        }
        Assert.Equal(1, world.Events.Scene.NativeFieldLoadVersion);
        Assert.Equal(new[] { 1, 2, 3 }, world.Events.Scene.NativeActorObjectIds);
        Assert.All(world.Events.Scene.NativeActorObjectIds, id => Assert.Equal(-1, world.Events.Actors[id].ModelId));
        Assert.Equal(new[] { 255, 255, 255 }, world.Events.Scene.FieldPartyNativeActorIds);
        Assert.Null(world.Events.PlayerActor);
        Assert.Null(world.Events.Scene.NativePlayerPosition);
        Assert.Null(world.Events.Scene.PartyTrace);
        Assert.Equal(0, world.Events.Scene.NativePartyTraceVersion);
        Assert.Equal((byte)0, world.Events.Scene.NativePlayerEarlyRetryState);
        Assert.Equal(firstCarrier, world.Events.Scene.FieldLoadFirstRecordZero);
        Assert.Equal(secondCarrier, world.Events.Scene.FieldLoadSecondRecordZero);
        JsonElement allocated = world.Events.Capture();
        using GameWorld restored = new(new GameSession(session.State with { EventState = allocated }), game);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(allocated, restored.Events.Capture()));
        JsonObject forged = JsonSerializer.SerializeToNode(allocated)!.AsObject();
        forged["Scene"]![nameof(SceneEventState.NativePlayerPosition)] = JsonSerializer.SerializeToNode(new FieldFixedPosition(0, 0, 0));
        Assert.Throws<InvalidDataException>(() => restored.Events.Restore(JsonSerializer.SerializeToElement(forged)));
        Assert.True(JsonElement.DeepEquals(allocated, restored.Events.Capture()));
        bool sawTitle = false;
        List<string> observed = [];
        world.Events.CommandExecuted += command => observed.Add(command.Name);
        for (int callback = 0; callback < 300 && session.State.FieldId == field; callback++)
        {
            DialogueState? dialogue = world.Events.CurrentDialogue;
            sawTitle |= dialogue?.MessageId == (painting == 5 ? 5 : 3);
            PlayerAction action = dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
            Assert.True(restored.Apply(action).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        Assert.True(sawTitle, $"Original{field} did not show its title. Commands: {string.Join(',', observed)}");
        Assert.True(session.State.FieldId == destination, $"Original{field} did not return to its gallery. Destination={session.State.FieldId}; commands={string.Join(',', observed)}; dialogue={world.Events.CurrentDialogue}");
        world.EnsureField();
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(1, world.Events.Scene.NativePartyTraceVersion);
        PartyTraceSample first = Assert.IsType<PartyTraceSample>(world.Events.Scene.PartyTrace!.FirstTrail[0]);
        PartyTraceSample second = Assert.IsType<PartyTraceSample>(world.Events.Scene.PartyTrace.SecondTrail[0]);
        Assert.Equal(firstCarrier, first with { X = firstCarrier!.X, Y = firstCarrier.Y });
        Assert.Equal(secondCarrier, second with { X = secondCarrier!.X, Y = secondCarrier.Y });
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
