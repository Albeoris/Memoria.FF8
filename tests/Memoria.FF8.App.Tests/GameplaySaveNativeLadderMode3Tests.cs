using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveNativeLadderMode3Tests
{
    [Theory]
    [InlineData(true, false, 2)]
    [InlineData(false, false, 3)]
    [InlineData(true, false, 6)]
    [InlineData(true, true, 2)]
    [InlineData(false, true, 3)]
    [InlineData(true, false, 1)]
    public void Logical_mode3_rebuilds_current_resources_and_preserves_distinct_body_renderer_phases(bool up, bool controlled, int ticks)
    {
        CheckResume(up, controlled, ticks, false);
    }

    [Fact]
    public void Shorter_current_visual_clip_normalizes_renderer_phase_without_changing_traversal()
    {
        CheckResume(true, false, 2, true);
    }

    private static void CheckResume(bool up, bool controlled, int ticks, bool shorterClip)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        int triangle;
        using (GameAssets original = new(directory))
        {
            FieldAssets field = original.LoadField(165);
            fieldName = field.Name;
            triangle = FieldPartyTraceGeometry.Resolve(field.Walkmesh, -700, -8593, 567)!.Value;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(up, controlled, triangle)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        int objectId = controlled ? 0 : 1;
        int held = up ? 0x1000 : 0x4000;
        Assert.True(source.Events.Apply(new(PlayerActionKind.FieldButtons, controlled ? held : 0)).Accepted);
        for (int tick = 0; tick < ticks; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(source.Events.IsUserControlEnabled);
        ActorState sourceActor = source.Events.Actors[objectId];
        Assert.NotNull(sourceActor.NativeLadderMode3);
        Assert.True(sourceActor.NativeLadderMode3.Active);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveActorLadderMode3 ladder = continuation.Actors.Actors[objectId].LadderMode3!;
        Assert.NotNull(ladder);
        Assert.Equal(4, ladder.Duration);
        Assert.Equal((ushort)(sourceActor.AnimationFrame * 16), ladder.RendererFrame);
        if (!controlled && ticks is 2 or 3)
        {
            Assert.True(continuation.Actors.Actors[objectId].Animation.Frame != ladder.RendererFrame, $"Mode3 phases did not separate: frames={source.Events.AnimationFrameCount!(sourceActor.RenderModelId, sourceActor.AnimationId)}, increment={sourceActor.NativeAnimationHeader!.Increment}, renderer={ladder.RendererFrame}, header={sourceActor.NativeAnimationHeader.Frame}.");
        }
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.ObjectId == objectId && fiber.Position == ladder.Operation && fiber.Wait == GameplaySaveScriptWait.NativeLadderMode3);
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        GameplaySaveFieldActor[] actors = valid.Actors.Actors.ToArray();
        actors[objectId] = actors[objectId] with { LadderMode3 = ladder with { Duration = 5 } };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Actors = valid.Actors with { Actors = actors } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        actors[objectId] = valid.Actors.Actors[objectId] with { LadderMode3 = ladder with { Operation = ladder.Operation + 1 } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        actors[objectId] = valid.Actors.Actors[objectId] with { Control = valid.Actors.Actors[objectId].Control with { LandingTriangle = (ushort)(triangle + 1) } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        if (shorterClip)
        {
            GameplaySaveSceneFacts facts = decoded.Progression.SceneFacts!;
            GameSession initializer = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
            initializer.StartNewGame();
            initializer.Restore(initializer.State with { GameplayPreferences = decoded.Progression.Rules.Preferences, ConfiguredGameplayTuning = decoded.Progression.Rules.ConfiguredTuning });
            using (GameWorld initialWorld = new(initializer, directory, sharedAssets: assets))
            {
                // Constructor-only current template initialization; no field program is loaded or executed.
            }
            GameState selected = GameplaySaveStateRestoration.Restore(decoded, initializer.State) with { FieldLoadContext = NativeFieldLoadContext.FromGameplayContinuation(prepared.FieldId) };
            GameSession selectedSession = new(selected);
            using GameWorld candidate = new(selectedSession, directory, sharedAssets: assets, gameplaySaveFacts: facts);
            candidate.EnsureField();
            Func<int, int, int> frameCount = candidate.Events.AnimationFrameCount!;
            candidate.Events.AnimationFrameCount = (model, animation) => animation == 1 ? 1 : frameCount(model, animation);
            candidate.Events.RestoreGameplaySaveFieldContinuation(valid, facts);
            Assert.Equal(ladder.RendererFrame % 16 / 16f, candidate.Events.Actors[objectId].AnimationFrame);
            Assert.Equal(sourceActor.NativeLadderMode3, candidate.Events.Actors[objectId].NativeLadderMode3);
            Assert.Equal(sourceActor.NativeTurnAccumulator474, candidate.Events.Actors[objectId].NativeTurnAccumulator474);
            Assert.Equal(prepared.CrtRandom, selectedSession.State.CrtRandom);
            Assert.Equal(prepared.Playtime, selectedSession.State.Playtime);
            Assert.Equal(prepared.ExamTimer!.ClockCalls, selectedSession.State.ExamTimer!.ClockCalls);
            Assert.Equal(before, JsonSerializer.Serialize(decoded));
            for (int tick = 0; tick < 7; tick++)
            {
                Assert.True(candidate.Apply(new(PlayerActionKind.Wait)).Accepted);
            }
            Assert.Equal(73, candidate.Events.EventMemory.Read(600, 1).Values[0]);
            Assert.Equal((ushort?)2, candidate.Events.Actors[objectId].NativeMovementCompletion542);
            return;
        }
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(sourceActor.AnimationFrame, restored.Events.Actors[objectId].AnimationFrame);
        Assert.Equal(sourceActor.NativeAnimationHeader!.Frame, restored.Events.Actors[objectId].NativeAnimationHeader!.Frame);
        for (int tick = 0; tick < 9; tick++)
        {
            int input = controlled ? tick == 0 ? 0 : tick == 1 ? up ? 0x4000 : 0x1000 : held : 0;
            Assert.True(source.Events.Apply(new(PlayerActionKind.FieldButtons, input)).Accepted);
            Assert.True(restored.Events.Apply(new(PlayerActionKind.FieldButtons, input)).Accepted);
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            ActorState expected = source.Events.Actors[objectId];
            ActorState actual = restored.Events.Actors[objectId];
            Assert.Equal((expected.X, expected.Y, expected.Z, expected.WalkmeshTriangle, expected.AnimationFrame), (actual.X, actual.Y, actual.Z, actual.WalkmeshTriangle, actual.AnimationFrame));
            Assert.Equal(expected.NativeMovementCompletion542, actual.NativeMovementCompletion542);
            Assert.Equal(expected.NativeLadderMode3, actual.NativeLadderMode3);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            using GameWorld exact = new(new GameSession(restoredSession.PrepareSnapshot()), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(73, restored.Events.EventMemory.Read(600, 1).Values[0]);
        Assert.Equal((ushort?)2, restored.Events.Actors[objectId].NativeMovementCompletion542);
        Assert.Null(restored.Events.CaptureGameplaySaveFieldContinuation().Actors.Actors[objectId].LadderMode3);
    }

    private static byte[] Program(bool up, bool controlled, int triangle)
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, -700), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ladder = [(Jsm.Opcode.LBL, controlled ? 1 : 17), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.ANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 28), ((Jsm.Opcode)372, 0), (Jsm.Opcode.PSHN_L, -672), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 1), (up ? Jsm.Opcode.LADDERUP : Jsm.Opcode.LADDERDOWN, triangle), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, controlled ? 17 : 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 1], controlled ? [player, ladder, actor, idle] : [player, idle, actor, ladder]);
    }

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
