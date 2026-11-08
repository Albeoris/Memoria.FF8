using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldAmbientTests
{
    [Theory]
    [InlineData("fade", 165, 2)]
    [InlineData("loop", 165, 2)]
    [InlineData("cosine", 165, 2)]
    [InlineData("linear", 165, 2)]
    [InlineData("direct", 165, 2)]
    [InlineData("linear", 252, 2)]
    [InlineData("linear", 252, 6)]
    public void Background_continuation_preserves_phase_and_one_time_script_effects(string kind, int fieldId, int initialTicks)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(fieldId).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(kind, fieldId == 252 ? 1 : 0)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        if (fieldId == 252)
        {
            initial = initial with { Resume = initial.Resume with { FieldId = fieldId, PreviousFieldId = 249, Field = new(43, 1765, 122, 128) }, Progression = initial.Progression! with { SceneFacts = initial.Progression.SceneFacts! with { FieldId = fieldId }, FieldLocation = null } };
            Assert.NotEqual(0, assets.LoadField(fieldId).Information!.BackgroundWrapMask);
        }
        int layer = fieldId == 252 ? 1 : 0;
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; tick < initialTicks; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(source.Events.LastFault);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        Assert.NotNull(continuation.Scene.Ambient);
        if (kind == "loop")
        {
            Assert.True(continuation.Scene.Ambient.ShadeLoops.ContainsKey(1));
        }
        else if (kind == "fade")
        {
            Assert.True(continuation.Scene.Ambient.Shades.ContainsKey(1));
            Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.BackgroundShade && fiber.ShadeOperation == fiber.Position);
        }
        else
        {
            if (kind != "direct" && initialTicks == 2)
            {
                Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.BackgroundScroll && fiber.Stack.SequenceEqual([layer]));
            }
            Assert.Equal(kind switch { "linear" => 4, "cosine" => 5, _ => 3 }, continuation.Scene.Ambient.Scrolls[layer].Mode);
            if (kind != "direct")
            {
                Assert.True(continuation.Scene.Ambient.Scrolls[layer].Counter > 1);
                Assert.Equal(initialTicks == 2 ? 1 : 2, continuation.Scene.Ambient.Scrolls[layer].Status);
            }
        }
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveResumeDescriptor resume = initial.Resume with { Field = continuation.GetLeaderPosition() };
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, resume);
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        GameplaySaveFieldAmbient ambient = valid.Scene.Ambient!;
        GameplaySaveFieldAmbient invalid = kind is "fade" or "loop"
            ? ambient with { Shades = new() { [999] = new(0, new(0, 0, 0), new(0, 0, 0), valid.Scripts.Frame) } }
            : ambient with { Scrolls = new() { [layer] = ambient.Scrolls[layer] with { Status = 3 } } };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Scene = valid.Scene with { Ambient = invalid } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-ambient-" + Guid.NewGuid().ToString("N") + ".sav");
        GameState restoredState;
        try
        {
            GameplaySaveSessionWriter.Write(path, decoded, directory, sharedAssets: assets);
            restoredState = GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets);
        }
        finally
        {
            File.Delete(path);
        }
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.Playtime, restoredSession.State.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredSession.State.ExamTimer!.ClockCalls);
        Assert.Equal(source.Events.GetBackgroundModuleShades(), restored.Events.GetBackgroundModuleShades());
        for (int tick = 0; tick < 12; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.Null(restored.Events.LastFault);
            Assert.Equal(source.Events.GetBackgroundModuleShades(), restored.Events.GetBackgroundModuleShades());
            Assert.Equal(source.Events.Scene.BackgroundScrollPositions, restored.Events.Scene.BackgroundScrollPositions);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(77, restoredSession.EventMemory.Read(600, 4).RequireExact());
    }

    [Fact]
    public void Selected_field_continuation_roundtrips_through_the_player_save_writer()
    {
        string directory = TestResources.GameDirectory;
        string sourcePath = TestResources.FixturePath("App.Tests.GameplaySaveFieldAmbientTests.1");
        Assert.SkipUnless(File.Exists(sourcePath), "An external semantic field continuation save is required.");
        byte[] original = File.ReadAllBytes(sourcePath);
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Load(sourcePath, directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        GameplaySaveFieldContinuation before = source.Events.CaptureGameplaySaveFieldContinuation();
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-field-continuation-" + Guid.NewGuid().ToString("N") + ".sav");
        try
        {
            source.WriteGameplaySave(path);
            GameSession resumedSession = new(GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets));
            using GameWorld resumed = new(resumedSession, directory, sharedAssets: assets);
            resumed.EnsureField();
            Assert.Equal(session.State.CrtRandom, resumedSession.State.CrtRandom);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.Economy), JsonSerializer.SerializeToElement(resumedSession.State.Economy)));
            Assert.Equal(session.State.Playtime, resumedSession.State.Playtime);
            Assert.Equal(source.Events.Scene.BackgroundScrollPositions, resumed.Events.Scene.BackgroundScrollPositions);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.Scripts), JsonSerializer.SerializeToElement(resumed.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), resumed.Apply(new(PlayerActionKind.Wait)));
            Assert.Null(resumed.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(resumed.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(original, File.ReadAllBytes(sourcePath));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Program(string kind, int layer)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] backgroundInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)];
        List<(Jsm.Opcode, int)> background = [(Jsm.Opcode.LBL, 17)];
        int[] operands = kind switch
        {
            "loop" => [3, 4, 128, 80, 70, 10, 20, 30, 2, 3],
            "cosine" => [0, 10, 20, 4],
            "linear" => [layer, 0, 0, 10, 20, 4],
            "direct" => [0, 10, 20],
            _ => [3, 128, 80, 70, 10, 20, 30]
        };
        background.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
        background.Add((kind switch { "loop" => Jsm.Opcode.RBGSHADELOOP, "cosine" => Jsm.Opcode.CSCROLL2, "linear" => Jsm.Opcode.LSCROLL3, "direct" => Jsm.Opcode.DSCROLL2, _ => Jsm.Opcode.BGSHADE }, 0));
        if (kind is "cosine" or "linear")
        {
            background.AddRange([(Jsm.Opcode.PSHN_L, layer), (Jsm.Opcode.SCROLLSYNC2, 0)]);
        }
        background.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)]);
        byte[] content = GameplaySaveFieldScriptsTests.Encode([32 << 7, (16 << 7) | 1, 1], [initialize, ordinary, backgroundInit, background.ToArray(), [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 0)]]);
        content[2] = 2;
        content[3] = 1;
        return content;
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
