using System.Text.Json;
using System.Text.Json.Nodes;
using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveNativeLadderTests
{
    [Theory]
    [InlineData(true, 3, false, false)]
    [InlineData(false, 1, false, false)]
    [InlineData(true, 4, false, false)]
    [InlineData(false, 6, false, false)]
    [InlineData(true, 5, false, false)]
    [InlineData(true, 1, true, false)]
    [InlineData(true, 3, false, true)]
    public void Mode4_route_and_follower_timing_survive_cold_load_independently_of_current_visual_clips(bool up, int phase, bool automatic, bool shorterClip)
    {
        CheckResume(up, phase, automatic, shorterClip, false);
    }

    [Fact]
    public void Later_executed_ladder_animation_command_rederives_timing_from_current_resources()
    {
        CheckResume(true, 3, false, true, true);
    }

    [Fact]
    public void Drain_entry_uses_logical_transition_duration_after_visual_clip_changes()
    {
        CheckResume(false, 6, false, true, false, 0);
    }

    private static void CheckResume(bool up, int phase, bool automatic, bool shorterClip, bool changeSelectors, int minimumElapsed = 1)
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
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(up, automatic, triangle, changeSelectors)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        int forward = up ? 0x1000 : 0x4000;
        for (int tick = 0; tick < 160 && !(source.Events.Actors[0].NativeLadder is { Active: true } && source.Events.Actors[0].NativeMovementCompletion542 == phase && source.Events.Actors[0].NativeTurnAccumulator474 >= minimumElapsed); tick++)
        {
            int input = phase == 5 && source.Events.Actors[0].NativeMovementCompletion542 == 1 ? up ? 0x4000 : 0x1000 : forward;
            Assert.True(source.Events.Apply(new(PlayerActionKind.FieldButtons, input)).Accepted);
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        ActorState leader = source.Events.Actors[0];
        Assert.Equal((ushort?)phase, leader.NativeMovementCompletion542);
        Assert.True(leader.NativeLadder!.Active);
        Assert.Equal(!automatic, source.Events.IsUserControlEnabled);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveActorLadder savedLadder = continuation.Actors.Actors[0].Ladder!;
        Assert.NotNull(savedLadder);
        Assert.Equal(automatic ? 1 : 3, source.Events.Scene.FieldPartyNativeActorIds.Count(id => id != 255));
        Assert.DoesNotContain("LadderRenderer", JsonSerializer.Serialize(continuation), StringComparison.Ordinal);
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.ObjectId == 0 && fiber.Position == savedLadder.Operation && fiber.Wait == GameplaySaveScriptWait.NativeLadder);
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
        actors[0] = actors[0] with { Ladder = savedLadder with { Operation = savedLadder.Operation + 1 } };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Actors = valid.Actors with { Actors = actors } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        actors[0] = valid.Actors.Actors[0];
        actors[0] = valid.Actors.Actors[0] with { Ladder = savedLadder with { StartSelector = 255 } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        actors[0] = valid.Actors.Actors[0] with { Ladder = savedLadder with { EndDuration = -2 } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        GameplaySaveSceneFacts facts = decoded.Progression.SceneFacts!;
        GameState restoredState;
        if (shorterClip)
        {
            GameSession initializer = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
            initializer.StartNewGame();
            using (GameWorld initialWorld = new(initializer, directory, sharedAssets: assets))
            {
            }
            restoredState = GameplaySaveStateRestoration.Restore(decoded, initializer.State) with { FieldLoadContext = NativeFieldLoadContext.FromGameplayContinuation(prepared.FieldId) };
        }
        else
        {
            restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        }
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets, gameplaySaveFacts: shorterClip ? facts : null);
        restored.EnsureField();
        if (shorterClip)
        {
            Func<int, int, int> frameCount = restored.Events.AnimationFrameCount!;
            restored.Events.AnimationFrameCount = (model, animation) => animation == 1 ? 2 : frameCount(model, animation);
            restored.Events.RestoreGameplaySaveFieldContinuation(valid, facts);
        }
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredSession.State.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredSession.State.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredSession.State.ExamTimer!.ClockCalls);
        Assert.NotNull(restored.Events.Actors[0].NativeLadder!.GameplayTiming);
        int callbacks = 0;
        do
        {
            // A released-input pause followed by the same real input epoch on both processes.
            int input = callbacks == 0 ? 0 : forward;
            Assert.True(source.Events.Apply(new(PlayerActionKind.FieldButtons, input)).Accepted);
            Assert.True(restored.Events.Apply(new(PlayerActionKind.FieldButtons, input)).Accepted);
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            for (int objectId = 0; objectId < 3; objectId++)
            {
                ActorState expected = source.Events.Actors[objectId];
                ActorState actual = restored.Events.Actors[objectId];
                Assert.Equal((expected.X, expected.Y, expected.Z, expected.WalkmeshTriangle, expected.Direction, expected.NativeMovementCompletion542, expected.NativeTurnAccumulator474), (actual.X, actual.Y, actual.Z, actual.WalkmeshTriangle, actual.Direction, actual.NativeMovementCompletion542, actual.NativeTurnAccumulator474));
            }
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.Scene.PartyTrace), JsonSerializer.SerializeToElement(restored.Events.Scene.PartyTrace)));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            if (!shorterClip && callbacks < 4)
            {
                using GameWorld exact = new(new GameSession(restoredSession.PrepareSnapshot()), directory, sharedAssets: assets);
                exact.EnsureField();
                Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
            }
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
            if (changeSelectors && restored.Events.EventMemory.Read(601, 1).Values[0] == 1)
            {
                Assert.Null(restored.Events.Actors[0].NativeLadder!.GameplayTiming);
            }
            callbacks++;
        }
        while (source.Events.Actors[0].NativeLadder is { Active: true } && callbacks < 160);
        Assert.InRange(callbacks, 1, 159);
        Assert.Equal(73, restored.Events.EventMemory.Read(600, 1).Values[0]);
        Assert.Equal((ushort?)2, restored.Events.Actors[0].NativeMovementCompletion542);
        Assert.Null(restored.Events.CaptureGameplaySaveFieldContinuation().Actors.Actors[0].Ladder);
        if (changeSelectors)
        {
            Assert.Equal(1, restored.Events.EventMemory.Read(601, 1).Values[0]);
        }
        JsonNode native = JsonNode.Parse(restored.Events.Capture().GetRawText())!;
        native["Actors"]![0]!["GameplayReconstruction"] = null;
        Assert.Throws<InvalidDataException>(() => restored.Events.Restore(JsonSerializer.SerializeToElement(native)));
    }

    private static byte[] Program(bool up, bool automatic, int triangle, bool changeSelectors)
    {
        List<(Jsm.Opcode, int)[]> scripts = [];
        for (int slot = 0; slot < 3; slot++)
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, slot * 16)];
            if (slot == 0)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0)]);
            }
            init.AddRange([(Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, -700), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.LADDERANIME, 1), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.ANIMESPEED, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add(init.ToArray());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, slot * 16 + 1)];
            if (slot == 0)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0)]);
                main.AddRange([(Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UCOFF, 0)]);
                if (!automatic)
                {
                    main.Add((Jsm.Opcode.UCON, 0));
                }
                main.Add((Jsm.Opcode.INITTRACE, 0));
                main.AddRange([(Jsm.Opcode.PSHN_L, 28), ((Jsm.Opcode)372, 0)]);
                foreach (int value in new[] { -700, -8593, 567, -672, -8593, 567, -668, -8593, 567 })
                {
                    main.Add((Jsm.Opcode.PSHN_L, value));
                }
                main.AddRange([(up ? Jsm.Opcode.LADDERUP2 : Jsm.Opcode.LADDERDOWN2, triangle), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 600)]);
            }
            if (slot == 1 && changeSelectors)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0)]);
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add(main.ToArray());
            if (slot == 0 && changeSelectors)
            {
                scripts.Add([(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.LADDERANIME, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 601), (Jsm.Opcode.RET, 8)]);
            }
        }
        byte[] program = GameplaySaveFieldScriptsTests.Encode([(ushort)(changeSelectors ? 2 : 1), (16 << 7) | 1, (32 << 7) | 1], scripts);
        for (int offset = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(6)); offset < program.Length; offset += 4)
        {
            int encoded = BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan(offset));
            if (encoded == (int)Jsm.Opcode.REQ || encoded == (int)Jsm.Opcode.LADDERANIME)
            {
                BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(offset), encoded << 24);
            }
        }
        return program;
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
