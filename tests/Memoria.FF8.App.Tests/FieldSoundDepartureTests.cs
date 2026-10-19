using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldSoundDepartureTests
{
    [Fact]
    public void Departure_selects_field_effect_id_words_on_voices_one_through_twenty_eight_without_mutating_owner()
    {
        uint?[] ids = new uint?[32];
        ids[0] = 10103;
        ids[1] = 10000;
        ids[2] = 9999;
        ids[3] = 0;
        ids[4] = uint.MaxValue;
        ids[28] = 10103;
        ids[29] = 10103;
        ids[30] = uint.MaxValue;
        ids[31] = 10103;
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess() with { OriginalEffectIds = ids.ToImmutableArray() };
        var session = new GameSession(new GameState { GlobalSoundProcess = sound });
        var engine = new EventEngine(session);
        List<NativeFieldSoundDeparture> departures = [];
        engine.AudioCommandExecuted += command =>
        {
            NativeFieldSoundDeparture departure = Assert.IsType<NativeFieldSoundDeparture>(command);
            Assert.Equal(141, engine.FieldId);
            Assert.Same(sound, session.GlobalSoundProcess);
            departures.Add(departure);
        };

        engine.LoadField(141, EmptyProgram(), []);
        Assert.Empty(departures);
        engine.LoadField(142, EmptyProgram(), []);

        NativeFieldSoundDeparture actual = Assert.Single(departures);
        Assert.Equal(141, actual.FieldId);
        Assert.Equal(new[] { 1, 4, 28 }, actual.Voices);
        Assert.Same(sound, session.GlobalSoundProcess);
        Assert.Equal(ids, session.GlobalSoundProcess!.OriginalEffectIds.ToArray());
        Assert.Equal(142, engine.FieldId);
    }

    [Fact]
    public void Departure_with_no_field_effects_still_notifies_the_host_with_an_empty_selection()
    {
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        var engine = new EventEngine(new GameSession(new GameState { GlobalSoundProcess = sound }));
        List<NativeFieldSoundDeparture> departures = [];
        engine.AudioCommandExecuted += command => departures.Add(Assert.IsType<NativeFieldSoundDeparture>(command));
        engine.LoadField(141, EmptyProgram(), []);
        engine.LoadField(142, EmptyProgram(), []);

        NativeFieldSoundDeparture departure = Assert.Single(departures);
        Assert.Equal(141, departure.FieldId);
        Assert.Empty(departure.Voices);
    }

    [Fact]
    public void Loading_a_frozen_allocation_does_not_emit_a_field_departure()
    {
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess().RecordEffect(10103, 1, out _);
        var session = new GameSession(new GameState { GlobalSoundProcess = sound });
        var engine = new EventEngine(session);
        List<object> commands = [];
        engine.AudioCommandExecuted += commands.Add;
        engine.LoadField(141, EmptyProgram(), []);
        engine.LoadField(141, EmptyProgram(), [], restoringCheckpoint: true);

        Assert.Empty(commands);
        Assert.Same(sound, session.GlobalSoundProcess);
    }

    [Fact]
    public void Actual_applause_is_selected_at_the_normal_field_departure_and_destination_continues()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.FieldSoundDepartureTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        List<NativeFieldSoundDeparture> departures = [];
        bool applauseStarted = false;
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is GlobalSoundRequest { OriginalEffectId: 10103, Voice: 1 } && world.Events.FieldId == 141)
            {
                applauseStarted = true;
            }
            if (command is NativeFieldSoundDeparture departure)
            {
                Assert.Equal(departure.FieldId, world.Events.FieldId);
                departures.Add(departure);
            }
        };
        world.EnsureField();
        Assert.Empty(departures);

        int navigationFrames = 0;
        int stagnantActions = 0;
        for (int action = 0; action < 4400 && !departures.Any(departure => departure.FieldId == 141); action++)
        {
            int beforeField = world.Events.FieldId;
            ActorState? beforePlayer = world.Events.PlayerActor;
            (float? X, float? Y) beforePosition = (beforePlayer?.X, beforePlayer?.Y);
            PlayerAction next;
            if (world.CurrentDialogue is { CanConfirm: true })
            {
                next = new PlayerAction(PlayerActionKind.Cancel);
            }
            else if (world.NextTrigger.Kind == PlayerTriggerKind.Playable && beforeField is 251 or 137)
            {
                navigationFrames++;
                Assert.True(navigationFrames < 2000, $"Navigation failed to cross the exit in field {beforeField}.");
                ActorState player = Assert.IsType<ActorState>(beforePlayer);
                float targetX = beforeField == 251 ? -470 : 0;
                float targetY = beforeField == 251 ? 650 : -5600;
                float dx = targetX - player.X;
                float dy = targetY - player.Y;
                float length = MathF.Sqrt(dx * dx + dy * dy);
                Assert.True(length > 1, $"Reached the exit target without its scripted transition in field {beforeField}.");
                float rotation = world.Events.InputAxisDirection * MathF.Tau / 256;
                float horizontal = (dx * MathF.Cos(rotation) + dy * MathF.Sin(rotation)) / length;
                float vertical = (-dx * MathF.Sin(rotation) + dy * MathF.Cos(rotation)) / length;
                next = new PlayerAction(PlayerActionKind.Move, Horizontal: Math.Clamp(horizontal, -1, 1), Vertical: Math.Clamp(vertical, -1, 1));
            }
            else
            {
                Assert.True(world.NextTrigger.Kind is PlayerTriggerKind.Waiting or PlayerTriggerKind.Dialogue or PlayerTriggerKind.Playable or PlayerTriggerKind.FieldTransition, $"Unsupported continuation trigger: {world.NextTrigger.Kind} in field {beforeField}.");
                next = new PlayerAction(PlayerActionKind.Wait, Frames: 300, WaitWhilePlayable: true);
            }
            Assert.True(world.Apply(next).Accepted);
            Assert.Null(world.Events.LastFault);
            world.EnsureField();
            ActorState? afterPlayer = world.Events.PlayerActor;
            bool unchanged = world.Events.FieldId == beforeField && beforePosition == (afterPlayer?.X, afterPlayer?.Y) && world.NextTrigger.Kind == PlayerTriggerKind.Playable;
            stagnantActions = unchanged ? stagnantActions + 1 : 0;
            Assert.True(stagnantActions < 30, $"Continuation made no progress in field {world.Events.FieldId}, trigger {world.NextTrigger.Kind}.");
            if (world.Events.FieldId != beforeField)
            {
                navigationFrames = 0;
            }
        }
        Assert.True(applauseStarted, "The field must execute its original applause request before departure.");
        NativeFieldSoundDeparture stopped = Assert.Single(departures, departure => departure.FieldId == 141);
        Assert.Contains(1, stopped.Voices);
        Assert.Equal(223, world.Events.FieldId);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.DoesNotContain("NativeFieldSoundDeparture", world.Events.Capture().GetRawText(), StringComparison.Ordinal);
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_DEPARTURE_CHECKPOINT");
        if (!string.IsNullOrWhiteSpace(checkpoint))
        {
            session.SetEventState(world.Events.Capture());
            session.Save(checkpoint, compact: true, candidate =>
            {
                using var restored = new GameWorld(new GameSession(candidate), directory);
                restored.EnsureField();
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
                Assert.Null(restored.Events.LastFault);
            });
        }
    }

    [Fact]
    public void Existing_destination_checkpoint_restores_and_advances_without_a_fabricated_departure()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.FieldSoundDepartureTests.2")));
        using var world = new GameWorld(session, directory);
        List<NativeFieldSoundDeparture> departures = [];
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeFieldSoundDeparture departure)
            {
                departures.Add(departure);
            }
        };

        world.EnsureField();
        Assert.Equal(241, world.Events.FieldId);
        Assert.Empty(departures);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Empty(departures);
        Assert.Equal(241, world.Events.Capture().GetProperty("FieldId").GetInt32());
    }

    private static byte[] EmptyProgram()
    {
        byte[] bytes = new byte[32];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), (int)Jsm.Opcode.LBL << 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), (int)Jsm.Opcode.RET << 24 | 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), (int)Jsm.Opcode.LBL << 24 | 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), (int)Jsm.Opcode.RET << 24 | 8);
        return bytes;
    }
}
