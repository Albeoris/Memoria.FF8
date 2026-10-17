using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModelLoaderSessionTests
{
    [Fact]
    public void Actual_party_file_wait_restores_its_singleton_IO_CPU_and_resource_state_without_reinitialization()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, encounter.Id);
        BattleSession session = new(new()
        {
            EncounterId = 94,
            StageId = encounter.StageId,
            Combatants = actors,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        }, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && session.NativeModelLoader?.Task is not { Callback: 0x5077B0, Phase: 1 }; callback++)
        {
            session.Tick();
        }
        Assert.Equal(0x5077B0u, session.NativeModelLoader!.Task!.Callback);
        Assert.Equal((byte)1, session.NativeModelLoader.Task.Phase);
        Assert.Equal(-1, session.NativeEffectIoResult);
        Assert.Equal((byte?)1, session.NativeGraphics!.Models[0].EffectCounter97);
        Assert.Equal(0, session.NativeGraphics.Models[0].Flags0 & 2);
        BattleSnapshot pending = session.Capture();
        string json = JsonSerializer.Serialize(pending);
        BattleSession first = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(first.Capture()));
        for (int callback = 0; callback < 200 && !first.NativeEntryReady; callback++)
        {
            first.Tick();
            second.Tick();
        }
        Assert.True(first.NativeEntryReady);
        Assert.True(second.NativeEntryReady);
        NativeBattleGraphicsModel model = first.NativeGraphics!.Models[0];
        Assert.Equal((byte?)2, model.EffectCounter97);
        Assert.Equal(model.ActiveResourceRow100, model.OriginalResourceRow104);
        Assert.NotNull(model.TextureResourceIndex);
        Assert.NotNull(model.LinkedTextureResourceIndex);
        Assert.Equal(1024, first.NativeModelCpuResources!.Palettes.Read(0, 1024).Length);
        Assert.Equal(first.Capture().NativeGlobalRandomCalls, second.Capture().NativeGlobalRandomCalls);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        ExportPendingSnapshot(pending);
    }

    [Fact]
    public void Resume_the_named_model_load_checkpoint_at_its_current_boundary()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_LOAD_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set MEMORIA_FF8_MODEL_LOAD_SNAPSHOT to the original standalone pending-load snapshot.");
        BattleSnapshot pending = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!)) ?? throw new InvalidDataException("Named model-load checkpoint is empty.");
        Assert.Equal((byte)1, pending.NativeModelLoader?.Task?.Phase);
        string json = JsonSerializer.Serialize(pending);
        BattleSession first = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.False(first.NativeEntryReady);
        Assert.Equal(json, JsonSerializer.Serialize(first.Capture()));

        first.Tick();
        second.Tick();
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        for (int callback = 0; callback < 200 && !first.NativeEntryReady; callback++)
        {
            first.Tick();
            second.Tick();
        }
        Assert.True(first.NativeEntryReady);
        Assert.True(second.NativeEntryReady);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Null(first.NativeModelLoader!.Task);
        ExportPendingSnapshot(pending);
    }

    private static void ExportPendingSnapshot(BattleSnapshot pending)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_LOAD_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string name = $"native-model-load-pending-frame{pending.Frame}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}.battle.json";
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, JsonSerializer.Serialize(pending));
        Console.WriteLine($"Native model-load standalone: {path}");
    }
}
