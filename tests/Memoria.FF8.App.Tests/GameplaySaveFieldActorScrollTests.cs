using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldActorScrollTests
{
    [Theory]
    [InlineData("follow")]
    [InlineData("linear")]
    [InlineData("cosine")]
    [InlineData("held")]
    [InlineData("reproject")]
    public void Actor_camera_continuation_derives_current_target_and_preserves_phase_without_callbacks(string kind)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(kind)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; tick < (kind == "held" ? 4 : 2); tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(source.Events.LastFault);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveFieldActorScroll scroll = continuation.Scene.ActorScroll!;
        Assert.Equal(1, scroll.ObjectId);
        Assert.Equal(kind == "follow" ? 0 : kind == "cosine" ? 2 : 1, scroll.Mode);
        Assert.Equal(kind == "held" ? 2 : 1, scroll.Status);
        if (kind is "linear" or "cosine" or "reproject")
        {
            Assert.True(scroll.Counter > 1);
            Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.BackgroundScroll);
        }
        if (kind == "held")
        {
            GameplaySaveFieldActor[] actors = continuation.Actors.Actors.ToArray();
            int index = Array.FindIndex(actors, actor => actor.ObjectId == scroll.ObjectId);
            GameplaySaveActorPose pose = actors[index].Pose;
            actors[index] = actors[index] with { Pose = pose with { Position = pose.Position with { X = pose.Position.X + 1 }, FixedPosition = pose.FixedPosition is { } fixedPosition ? fixedPosition with { X = fixedPosition.X + 4096 } : null } };
            continuation = continuation with { Actors = continuation.Actors with { Actors = actors } };
        }
        if (kind == "reproject")
        {
            short oldTarget = checked((short)(scroll.ProjectedTargetX!.Value + 16));
            short oldX = NativeActorScrollPhase.Interpolate(scroll.Mode, scroll.PreviousX!.Value, oldTarget, scroll.Duration, checked((short)(scroll.Counter - 1)));
            continuation = continuation with { Scene = continuation.Scene with { ActorScroll = scroll with { X = oldX, ProjectedTargetX = oldTarget }, LayerZeroCurrent = new(oldX, scroll.Y!.Value) } };
        }
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Scene = valid.Scene with { ActorScroll = scroll with { ObjectId = 999 } } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(continuation.Scripts.Frame, restored.Events.Frame);
        NativeActorBackgroundScroll rebuilt = restored.Events.Scene.ActorBackgroundScroll!;
        Assert.Equal(scroll.ObjectId, rebuilt.ObjectId);
        Assert.Equal(rebuilt.ObjectId, restored.Events.Scene.NativeActorObjectIds[rebuilt.NativeActorId]);
        Assert.Equal(source.Events.Scene.ActorBackgroundScroll, rebuilt);
        Assert.Equal((rebuilt.X, rebuilt.Y), (restored.Events.Scene.NativeLayerZeroScrollCurrent!.X, restored.Events.Scene.NativeLayerZeroScrollCurrent.Y));
        if (kind == "held")
        {
            Assert.Equal(1, restored.Events.Actors[1].X);
            Assert.Equal((scroll.X, scroll.Y), (rebuilt.X, rebuilt.Y));
        }
        for (int tick = 0; tick < 7; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(source.Events.Scene.ActorBackgroundScroll, restored.Events.Scene.ActorBackgroundScroll);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(77, restoredSession.EventMemory.Read(600, 4).RequireExact());
    }

    private static byte[] Program(string kind)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1)];
        if (kind != "follow")
        {
            ordinary.Add((Jsm.Opcode.PSHN_L, kind == "held" ? 2 : 5));
        }
        ordinary.Add((kind switch { "follow" => Jsm.Opcode.DSCROLLA, "cosine" => Jsm.Opcode.CSCROLLA, _ => Jsm.Opcode.LSCROLLA }, 0));
        if (kind != "follow")
        {
            ordinary.Add((Jsm.Opcode.SCROLLSYNC, 0));
        }
        ordinary.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 1], [initialize, ordinary.ToArray(), [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
    }

    private sealed class ScriptOverride(string fieldName, byte[] program) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = program.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
