using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class LegacyGameplayProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cross_edition_player_load_preserves_disabled_control_script_phase_and_progress_without_replaying_init(bool reverse)
    {
        string classic = TestResources.GameDirectory;
        string? remastered = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(Directory.Exists(remastered), "Select optional Remastered resources for cross-edition legacy continuation.");
        string sourceDirectory = reverse ? remastered! : classic;
        string targetDirectory = reverse ? classic : remastered!;
        string fieldName;
        using (GameAssets original = new(sourceDirectory))
        {
            fieldName = original.LoadField(165).Name;
        }
        byte[] script = Script(false, false);
        string path = Path.Combine(Path.GetTempPath(), "Memoria-legacy-projection-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            long frame;
            uint gil;
            using (GameAssets sourceAssets = CreateAssets(sourceDirectory, fieldName, script))
            {
                GameSession source = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(sourceAssets, false), sourceDirectory, sharedAssets: sourceAssets));
                using GameWorld sourceWorld = new(source, sourceDirectory, sharedAssets: sourceAssets);
                sourceWorld.EnsureField();
                sourceWorld.Apply(new(PlayerActionKind.Wait));
                sourceWorld.Apply(new(PlayerActionKind.Wait));
                Assert.False(sourceWorld.Events.IsUserControlEnabled);
                frame = sourceWorld.Events.Frame;
                gil = source.State.Economy!.Gil;
                source.Save(path);
            }
            byte[] before = SHA256.HashData(File.ReadAllBytes(path));
            using GameAssets selected = CreateAssets(targetDirectory, fieldName, script);
            GameSession loaded = new(PlayerStateLoader.Load(path, targetDirectory, sharedAssets: selected));
            using GameWorld resumed = new(loaded, targetDirectory, sharedAssets: selected);
            resumed.EnsureField();
            Assert.Equal(frame, resumed.Events.Frame);
            Assert.False(resumed.Events.IsUserControlEnabled);
            Assert.Equal(selected.NativeRuntimeProfile!.ExecutableHash, resumed.Events.Scene.NativeFieldInitialization!.ExecutableHash);
            Assert.Equal(NativeFieldInitializationOrigin.GameplayContinuation, resumed.Events.Scene.NativeFieldInitialization.Origin);
            Assert.Equal(gil, loaded.State.Economy!.Gil);
            for (int tick = 0; tick < 12; tick++)
            {
                resumed.Apply(new(PlayerActionKind.Wait));
            }
            Assert.Equal(gil + 11, loaded.State.Economy!.Gil);
            Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Semantic_last_result_survives_exact_cold_load_and_is_replaced_only_by_a_new_completed_battle(bool requestBattle)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = CreateAssets(directory, fieldName, Script(requestBattle));
        GameSession initial = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(initial, directory, sharedAssets: assets);
        source.EnsureField();
        source.Apply(new(PlayerActionKind.Wait));
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameState prepared = initial.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, new(1, GameplaySaveResumeKind.Field, 165, 164, continuation.GetLeaderPosition(), null));
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation with { Scene = continuation.Scene with { BattleResult = 2 } }) };
        GameSession restored = new(GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
        using GameWorld first = new(restored, directory, sharedAssets: assets);
        first.EnsureField();
        Assert.Equal((byte)2, first.Events.Scene.NativeBattleResult);
        Assert.Equal(GameplayBattleResultOrigin.RestoredLastResult, first.Events.Scene.GameplayBattleResultOrigin);
        GameSession coldSession = new(restored.PrepareSnapshot());
        using GameWorld cold = new(coldSession, directory, sharedAssets: assets);
        cold.EnsureField();
        EventEngine engine = RestoreEngine(coldSession, assets, cold.Events.Capture(), cold.Events);
        engine.UseInteractiveBattle = (_, _) => false;
        engine.HostAutomaticBattles = true;
        coldSession.SetWorldControls(coldSession.State.WorldControls! with { EventFlags104 = coldSession.State.WorldControls!.EventFlags104 & ~0x20u });
        uint gil = coldSession.State.Economy!.Gil;
        bool observedPending = false;
        for (int tick = 0; tick < 12; tick++)
        {
            engine.Step();
            if (engine.Scene.GameplayBattleResultOrigin == GameplayBattleResultOrigin.RetainedWhilePending)
            {
                observedPending = true;
                Assert.Equal((byte)2, engine.Scene.NativeBattleResult);
                EventEngine exactPending = RestoreEngine(new GameSession(coldSession.State), assets, engine.Capture(), cold.Events);
                Assert.Equal(GameplayBattleResultOrigin.RetainedWhilePending, exactPending.Scene.GameplayBattleResultOrigin);
                engine.ReturnFromHostedBattle(0);
            }
        }
        Assert.Equal(requestBattle, observedPending);
        Assert.Equal(gil + 11, coldSession.State.Economy!.Gil);
        Assert.Equal(requestBattle ? (byte)4 : (byte)2, engine.Scene.NativeBattleResult);
        Assert.Equal(requestBattle ? GameplayBattleResultOrigin.None : GameplayBattleResultOrigin.RestoredLastResult, engine.Scene.GameplayBattleResultOrigin);
        JsonElement fiber = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && item.GetProperty("ScriptId").GetInt32() == 1);
        Assert.Equal(requestBattle ? 4 : 2, fiber.GetProperty("Results").GetProperty("0").GetInt32());
    }

    private static byte[] Script(bool requestBattle, bool readResult = true)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0)];
        if (requestBattle)
        {
            ordinary.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLE, 0)]);
        }
        if (readResult)
        {
            ordinary.Add((Jsm.Opcode.BATTLERESULT, 0));
        }
        ordinary.AddRange([(Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary.ToArray()]);
    }

    private static EventEngine RestoreEngine(GameSession session, GameAssets assets, JsonElement checkpoint, EventEngine template)
    {
        var field = assets.LoadField(165);
        EventEngine engine = new(session)
        {
            ReadNativeModelParts = template.ReadNativeModelParts,
            ReadNativeModelRegistrationParts = template.ReadNativeModelRegistrationParts,
            AnimationFrameCount = template.AnimationFrameCount,
            DrawPointParticleTables = template.DrawPointParticleTables,
            ResolveNativeDrawPointDirection = template.ResolveNativeDrawPointDirection,
            ShakeTables = template.ShakeTables,
            CaptureFootstepModelOrigin = template.CaptureFootstepModelOrigin
        };
        engine.LoadField(165, field.Jsm, field.Messages, nativeRuntimeProfile: assets.NativeRuntimeProfile, restoringCheckpoint: true);
        engine.Restore(checkpoint);
        return engine;
    }

    private static GameAssets CreateAssets(string directory, string fieldName, byte[] script)
    {
        GameAssets assets = new(directory);
        assets.Resources.Prepare(null, encodedResources: [new EncodedResource(new ResourceKey("field/" + fieldName + "/" + fieldName + ".jsm"), "jsm", 1, "test", script)]).Commit();
        return assets;
    }
}
