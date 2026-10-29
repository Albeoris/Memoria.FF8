using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMusicSessionPresentationTests
{
    private static readonly TimeSpan Callback = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1) + TimeSpan.FromTicks(1);

    [Fact]
    public void Fresh_original_entry_executes_501C60_but_restore_preserves_the_live_pending_command()
    {
        using GameAssets game = OpenGame();
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets);
        Assert.Null(definition.NativeMusicProcess!.Commands);
        Assert.Null(definition.NativeMusicProcess.Buffers.Buffer0);
        Assert.Equal(NativeMusicBufferProducer.Battle52CE30, definition.NativeMusicProcess.Buffers.Buffer1!.Producer);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Assert.Equal(new NativeBattleMusicCommandState(false, null, null, null, 0x1CE075C, false), battle.NativeMusicProcess!.Commands);
        Assert.Equal((byte?)0, battle.NativeMusicProcess.ReturnBank);
        Assert.Same(definition.NativeMusicProcess.Buffers, battle.NativeMusicProcess.Buffers);
        Assert.DoesNotContain(battle.Events, entry => entry.NativeMusicCommand is not null);

        BattleSnapshot source = battle.Capture();
        source = source with { NativeMusicProcess = battle.NativeMusicProcess.QueueGain501B10(60, 92) };
        BattleSession restored = Restore(source);
        Assert.True(restored.NativeMusicProcess!.Commands!.Pending);
        Assert.Equal(60, restored.NativeMusicProcess.Commands.Duration);
        Assert.Equal(92, restored.NativeMusicProcess.Commands.TargetGain);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored.Capture()));
        Assert.DoesNotContain(restored.Events, entry => entry.NativeMusicCommand is not null);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(4u)]
    [InlineData(8u)]
    [InlineData(12u)]
    [InlineData(0x201u)]
    public void Every_admitted_callback_drains_once_independent_of_publication_texture_and_effect_pause_flags(uint flags)
    {
        using GameAssets game = OpenGame();
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession ready = Ready(CreateDefinition(game, assets));
        BattleSnapshot source = ready.Capture();
        source = source with { NativeMusicProcess = ready.NativeMusicProcess!.QueueGain501B10(0, 92), NativeGraphics = source.NativeGraphics! with { Flags = flags } };
        BattleSession first = Restore(source);
        BattleSession independent = Restore(source);
        List<NativeBattleMusicCommandReceipt> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, playMusicCommand: heard.Add);
        audio.Update(first);
        long callback = first.NativeGraphics!.Callback;
        first.AdvanceNativeGraphicsPresentation(Callback);
        independent.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Equal(callback + 1, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(independent.Capture()));
        NativeBattleMusicCommandReceipt receipt = Assert.Single(first.Events, entry => entry.NativeMusicCommand is not null).NativeMusicCommand!;
        Assert.Equal((byte)3, receipt.Command);
        Assert.Equal((ushort?)0, receipt.GainChannel);
        Assert.Equal(92, receipt.Gain);
        Assert.True(receipt.ResumeOutput);
        Assert.Equal(92, first.NativeGlobalSoundProcess!.MusicPlayback!.Gain0);
        Assert.False(first.NativeMusicProcess!.Commands!.Pending);
        audio.Update(first);
        Assert.Same(receipt, Assert.Single(heard));
        audio.Update(first);
        Assert.Single(heard);

        BattleSession restored = Restore(first.Capture());
        BattleAudioPresentation attached = new(_ => { }, playModule: _ => { }, playMusicCommand: _ => Assert.Fail("Restoring a committed receipt must not replay device output."));
        attached.Update(restored);
        restored.AdvanceNativeGraphicsPresentation(Callback);
        attached.Update(restored);
        Assert.Single(restored.Events, entry => entry.NativeMusicCommand is not null);
    }

    [Fact]
    public void Missing_old_music_owner_is_not_initialized_on_restore_or_replaced_by_the_definition_seed()
    {
        using GameAssets game = OpenGame();
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession ready = Ready(CreateDefinition(game, assets));
        BattleSnapshot source = ready.Capture() with { NativeMusicProcess = null };
        BattleSession restored = Restore(source);
        Assert.Null(restored.NativeMusicProcess);
        Assert.Contains("restore does not execute501C60", Assert.Throws<NotSupportedException>(() => restored.AdvanceNativeGraphicsPresentation(Callback)).Message);
        Assert.Null(restored.NativeMusicProcess);
        Assert.DoesNotContain(restored.Events, entry => entry.NativeMusicCommand is not null);
    }

    [Fact]
    public void No_pending_request_and_native_default_command_do_not_require_a_global_sound_owner()
    {
        using GameAssets game = OpenGame();
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession ready = Ready(CreateDefinition(game, assets));
        BattleSnapshot source = ready.Capture() with { NativeGlobalSoundProcess = null };
        BattleSession idle = Restore(source);
        idle.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Null(idle.NativeGlobalSoundProcess);
        Assert.DoesNotContain(idle.Events, entry => entry.NativeMusicCommand is not null);

        NativeBattleMusicProcessState music = source.NativeMusicProcess! with { Commands = new(true, 255, null, null, null, null) };
        BattleSession nativeDefault = Restore(source with { NativeMusicProcess = music });
        nativeDefault.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Null(nativeDefault.NativeGlobalSoundProcess);
        Assert.False(nativeDefault.NativeMusicProcess!.Commands!.Pending);
        Assert.Equal((byte)255, Assert.Single(nativeDefault.Events, entry => entry.NativeMusicCommand is not null).NativeMusicCommand!.Command);
    }

    [Fact]
    public void Unknown_selected_buffer_refuses_without_consuming_the_command_or_publishing_music_output()
    {
        using GameAssets game = OpenGame();
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession ready = Ready(CreateDefinition(game, assets));
        BattleSnapshot source = ready.Capture() with { NativeMusicProcess = ready.NativeMusicProcess!.QueuePlay501A70() };
        BattleSession restored = Restore(source);
        string music = JsonSerializer.Serialize(restored.NativeMusicProcess);
        Assert.Throws<NotSupportedException>(() => restored.AdvanceNativeGraphicsPresentation(Callback));
        Assert.Equal(music, JsonSerializer.Serialize(restored.NativeMusicProcess));
        Assert.DoesNotContain(restored.Events, entry => entry.NativeMusicCommand is not null);
        //Only singleton publication is atomic; unrelated earlier native callback stores may already have executed.
    }

    [Fact]
    public void Fresh_graphics_entry_requires_the_real_return_selector_instead_of_selecting_a_default_buffer()
    {
        using GameAssets game = OpenGame();
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets);
        Assert.Throws<NotSupportedException>(() => new BattleSession(definition with { NativeMusicProcess = null }, BattleAtbMode.TurnBased));
        Assert.Throws<NotSupportedException>(() => new BattleSession(definition with { NativeMusicProcess = definition.NativeMusicProcess! with { ReturnBank = null } }, BattleAtbMode.TurnBased));
    }

    internal static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets)
    {
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        //Controlled sourceBYTE201=0; both old buffer identities and unrelated pending fields remain unknown.
        //Actual52CE30 loads only selected bank1 from original B8EE6C resources and establishes the return selectors.
        NativeMusicProcessState sourceBuffers = new(1, null, null, null, 0, null);
        NativeBattleMusicEntryResources resources = new NativeMusicCatalog(game.GameDirectory).ReadBattleEntryResources();
        NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(94, 0x80, sourceBuffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), resources);
        return new()
        {
            Combatants = actors,
            EncounterId = 94,
            StageId = formation.StageId,
            NativeGlobalSoundProcess = entry.SoundProcess,
            NativeMusicProcess = entry.MusicProcess,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalog(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
    }

    private static BattleSession Ready(BattleDefinition definition)
    {
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "party").IsReady);
        Assert.DoesNotContain(battle.Events, entry => entry.NativeMusicCommand is not null);
        return battle;
    }

    private static BattleSession Restore(BattleSnapshot source)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
    }

    private static GameAssets OpenGame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle entry resources are required.");
        return new(directory);
    }
}
