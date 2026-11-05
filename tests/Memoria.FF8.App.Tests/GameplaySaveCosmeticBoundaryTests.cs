using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveCosmeticBoundaryTests
{
    [Theory]
    [InlineData("color")]
    [InlineData("fade")]
    [InlineData("sound")]
    public void Player_control_save_settles_visual_waits_and_preserves_loops_without_replaying_rewards(string kind)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(kind)));
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.Events.LoadSoundResource = _ => SoundResourceStatus.Pending;
        source.EnsureField();
        for (int tick = 0; tick < 2; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(source.Events.LastFault);
        Assert.True(source.Events.IsUserControlEnabled);
        Assert.True(source.Events.CanOpenFieldMenu);
        if (kind == "color")
        {
            Assert.False(source.Events.FieldFade!.IsColorComplete);
        }
        else if (kind == "fade")
        {
            Assert.False(source.Events.FieldFade!.IsFadeComplete);
        }
        else
        {
            Assert.Equal(SoundResourceStatus.Pending, source.Events.Scene.LoadedSoundEffects[11].Status);
        }
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.MusicCrossfadeFrames))!.SetValue(source.Events.Scene, 12);
        GameState prepared = sourceSession.PrepareSnapshot();
        string sourceBefore = source.Events.Capture().GetRawText();
        string firstPath = SavePath();
        string secondPath = SavePath();
        try
        {
            GameState beforeWrite = sourceSession.State;
            source.WriteGameplaySave(firstPath);
            Assert.Same(beforeWrite, sourceSession.State);
            Assert.Equal(sourceBefore, source.Events.Capture().GetRawText());
            byte[] originalBytes = File.ReadAllBytes(firstPath);
            using FileStream input = File.OpenRead(firstPath);
            GameplaySaveComponentDocument document = GameplaySaveCodec.Read(input);
            GameplaySaveFieldContinuation continuation = document.Progression!.FieldContinuation!;
            Assert.DoesNotContain(continuation.Scripts.Fibers, fiber => fiber.Wait.ToString() is "Color" or "Fade");
            if (kind == "sound")
            {
                Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.SoundResource);
            }
            Assert.Contains(continuation.Scene.BackgroundAnimations, animation => animation.Active && animation.Loop);
            Assert.NotEmpty(continuation.Scene.Ambient!.Scrolls);
            string documentBefore = JsonSerializer.Serialize(document);
            GameSession firstSession = new(PlayerStateLoader.Load(firstPath, directory, sharedAssets: assets));
            using GameWorld first = new(firstSession, directory, sharedAssets: assets);
            first.Events.LoadSoundResource = _ => SoundResourceStatus.Pending;
            first.EnsureField();
            AssertRestored(source, prepared, first, firstSession, kind);
            first.WriteGameplaySave(secondPath);
            GameSession secondSession = new(PlayerStateLoader.Load(secondPath, directory, sharedAssets: assets));
            using GameWorld second = new(secondSession, directory, sharedAssets: assets);
            second.Events.LoadSoundResource = _ => SoundResourceStatus.Pending;
            second.EnsureField();
            AssertRestored(source, prepared, second, secondSession, kind);
            Assert.Equal(first.Events.FieldFade, second.Events.FieldFade);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first.Events.CaptureGameplaySaveFieldContinuation()), JsonSerializer.SerializeToElement(second.Events.CaptureGameplaySaveFieldContinuation())));
            Assert.Equal(documentBefore, JsonSerializer.Serialize(document));
            Assert.Equal(originalBytes, File.ReadAllBytes(firstPath));
            Assert.Equal(sourceBefore, source.Events.Capture().GetRawText());
            Assert.Equal(12, source.Events.Scene.MusicCrossfadeFrames);
            source.Events.LoadSoundResource = _ => SoundResourceStatus.Ready;
            second.Events.LoadSoundResource = _ => SoundResourceStatus.Ready;
            for (int tick = 0; tick < 40; tick++)
            {
                Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
                Assert.True(second.Apply(new(PlayerActionKind.Wait)).Accepted);
                Assert.Null(second.Events.LastFault);
                Assert.Equal(source.Events.Scene.BackgroundScrollPositions, second.Events.Scene.BackgroundScrollPositions);
                Assert.Equal(source.Events.CaptureGameplaySaveFieldContinuation().Scene.BackgroundAnimations, second.Events.CaptureGameplaySaveFieldContinuation().Scene.BackgroundAnimations);
                if (kind == "sound")
                {
                    Assert.Equal(sourceSession.Economy!.Gil, secondSession.Economy!.Gil);
                    Assert.Equal(sourceSession.EventMemory.Read(600, 4).RequireExact(), secondSession.EventMemory.Read(600, 4).RequireExact());
                    Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(second.Events.CaptureGameplaySaveScripts())));
                }
            }
            Assert.Equal(prepared.Economy!.Gil + 11, sourceSession.Economy!.Gil);
            Assert.Equal(prepared.Economy!.Gil + 11, secondSession.Economy!.Gil);
            Assert.Equal(77, secondSession.EventMemory.Read(600, 4).RequireExact());
            Assert.Equal(originalBytes, File.ReadAllBytes(firstPath));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    private static void AssertRestored(GameWorld source, GameState prepared, GameWorld restored, GameSession session, string kind)
    {
        Assert.True(restored.Events.IsUserControlEnabled);
        Assert.True(restored.Events.CanOpenFieldMenu);
        Assert.Equal(source.Events.Frame, restored.Events.Frame);
        Assert.Equal(prepared.Economy!.Gil, session.Economy!.Gil);
        Assert.Equal(prepared.Playtime, session.State.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, session.ExamTimer!.ClockCalls);
        Assert.Equal(source.Events.FieldFade!.Saved, restored.Events.FieldFade!.Saved);
        Assert.Equal(0, restored.Events.Scene.MusicCrossfadeFrames);
        if (kind == "color")
        {
            Assert.True(restored.Events.FieldFade.IsColorComplete);
            Assert.Equal(80, restored.Events.FieldFade.Physical.Red);
            Assert.Equal(60, restored.Events.FieldFade.Physical.Green);
            Assert.Equal(40, restored.Events.FieldFade.Physical.Blue);
        }
        else if (kind == "fade")
        {
            Assert.True(restored.Events.FieldFade.IsFadeComplete);
        }
        else
        {
            Assert.True(restored.Events.Scene.LoadedSoundEffects.ContainsKey(11));
            Assert.Contains(restored.Events.CaptureGameplaySaveScripts().Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.SoundResource && fiber.SoundResourceId == 11);
        }
    }

    private static byte[] Program(string kind)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.POPM_L, 600)];
        if (kind == "color")
        {
            ordinary.AddRange([(Jsm.Opcode.PSHN_L, 80), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.PSHN_L, 40), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.TCOLADD, 0), (Jsm.Opcode.COLSYNC, 0)]);
        }
        else if (kind == "fade")
        {
            ordinary.AddRange([(Jsm.Opcode.FADEOUT, 0), (Jsm.Opcode.FADESYNC, 0)]);
        }
        else
        {
            ordinary.AddRange([(Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.EFFECTLOAD, 0)]);
        }
        ordinary.AddRange([(Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        (Jsm.Opcode, int)[] background = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.RBGANIMELOOP, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.DSCROLL2, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
        byte[] result = GameplaySaveFieldScriptsTests.Encode([32 << 7, (16 << 7) | 1, 1], [initialize, ordinary.ToArray(), [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)], background, [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 0)]]);
        result[2] = 2;
        result[3] = 1;
        return result;
    }

    private static string SavePath() => Path.Combine(Path.GetTempPath(), "Memoria.FF8-cosmetic-save-" + Guid.NewGuid().ToString("N"));

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
