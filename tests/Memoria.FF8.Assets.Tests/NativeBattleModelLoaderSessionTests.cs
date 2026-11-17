using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        BattleDefinition definition = new()
        {
            EncounterId = 94,
            StageId = encounter.StageId,
            Combatants = actors,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeFlags = 0x80,
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        session.AttachPresentation(new(definition));
        for (int callback = 0; callback < 200 && session.Presentation!.NativeModelLoader?.Task is not { Callback: 0x5077B0, Phase: 1 }; callback++)
        {
            session.Tick();
        }
        Assert.Equal(0x5077B0u, session.Presentation!.NativeModelLoader!.Task!.Callback);
        Assert.Equal((byte)1, session.Presentation!.NativeModelLoader.Task.Phase);
        Assert.Equal(-1, session.Presentation!.NativeEffectIoResult);
        Assert.Equal((byte?)1, ReadOriginalModelCell(session.Presentation!.Graphics, 0).EffectCounter97);
        Assert.Equal(0, ReadOriginalModelCell(session.Presentation!.Graphics, 0).Flags0 & 2);
        BattleSession first = session.ForkForContinuation();
        BattleSession second = session.ForkForContinuation();
        Assert.False(first.NativeEntryReady);
        Assert.False(second.NativeEntryReady);
        Assert.Equal((byte)1, first.Presentation!.NativeModelLoader!.Task!.Phase);
        Assert.Equal((byte)1, second.Presentation!.NativeModelLoader!.Task!.Phase);
        for (int callback = 0; callback < 200 && !first.NativeEntryReady; callback++)
        {
            first.Tick();
            second.Tick();
        }
        Assert.True(first.NativeEntryReady);
        Assert.True(second.NativeEntryReady);
        NativeBattleGraphicsEntryTests.AssertOwnedEnemyResources(first);
        NativeBattleGraphicsEntryTests.AssertOwnedEnemyResources(second);
        NativeBattleGraphicsModel model = ReadOriginalModelCell(first.Presentation!.Graphics, 0);
        Assert.Equal((byte?)2, model.EffectCounter97);
        Assert.Equal(model.ActiveResourceRow100, model.OriginalResourceRow104);
        Assert.NotNull(model.TextureResourceIndex);
        Assert.NotNull(model.LinkedTextureResourceIndex);
        byte[] firstPalette = first.Presentation!.ModelCpuResources.Palettes.Read(0, 1024);
        byte[] secondPalette = second.Presentation!.ModelCpuResources.Palettes.Read(0, 1024);
        Assert.Equal(firstPalette, secondPalette);
        Assert.Equal(first.Capture().NativeGlobalRandomCalls, second.Capture().NativeGlobalRandomCalls);
        Assert.Null(first.Presentation.NativeModelLoader!.Task);
        Assert.Null(second.Presentation.NativeModelLoader!.Task);
    }
}
