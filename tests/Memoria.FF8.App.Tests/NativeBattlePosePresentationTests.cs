using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePosePresentationTests
{
    [Fact]
    public void Native_module_sound_is_committed_at_its_callback_and_played_once_without_an_actor_substitute()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeInputConfiguration input = game.ReadNewGameInputConfiguration();
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }];
        NativeBattleGraphicsDefinition graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter);
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeSavedOptions = input.Options, NativeInputButtonMap = input.ButtonMap, LimitProgress = game.ReadNewGameLimitProgress(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeCrtRandom = new(0), NativeModuleResources = game.ReadNativeBattleModuleResources(), NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(), NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, Combatants = actors };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession session = NativeBattleTypedFixture.CreateBattle(definition with { NativeClockRules = definition.NativeClockRules ?? assets.ReadNativeClockRules(), NativeBattleSpeed = definition.NativeBattleSpeed ?? game.ReadNewGameInputConfiguration().NativeBattleSpeedByte }, BattleAtbMode.TurnBased, graphicsResources);
        for (int frame = 0; frame < 100 && !session.Presentation!.Graphics.EntryReady; frame++)
        {
            session.Tick();
        }
        Assert.True(session.Presentation!.Graphics.EntryReady);
        Assert.True(session.Combatants[0].IsReady);
        AssertEntrySoundAllocation(session);
        BattleSnapshot saved = session.CaptureForContinuation();
        session = NativeBattleTypedFixture.RestoreBattle(saved.WithFixturePresentation(facts => facts with { NativeGraphics = saved.ReadFixturePresentation().NativeGraphics! with { ModuleEffects = NativeBattleStorageSnapshots.Restore(saved.ReadFixturePresentation().NativeGraphics!.ModuleEffects).AppendModelResource(session.ReadPresentationModelOwner("party"), definition.NativeModuleResources[39], 0x100027, session.ReadBattlePresentationPacketBindings().Aliases) } }));
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = BattleAudioTestOutput.Create(playModule: heard.Add);
        audio.Update(session);
        NativeBattleCameraProjection projection = session.Presentation!.Camera.Projection!;
        for (int callback = 0; callback < 3; callback++)
        {
            session.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            audio.Update(session);
            audio.Update(session);
        }
        NativeBattleModuleSound sound = Assert.Single(heard);
        Assert.Equal(0, session.Frame);
        Assert.Null(session.ActiveAction);
        Assert.Empty(session.QueuedActions);
        Assert.Equal(0x72u, sound.Sound);
        Assert.Equal((ushort)0x8000, sound.Flags);
        NativeBattleModuleAnchor anchor = Assert.IsType<NativeBattleModuleAnchor>(sound.Anchor);
        Assert.Equal(projection.ReadPan(new(anchor.X, anchor.Y, anchor.Z)), sound.Pan);
        Assert.Equal((uint)sound.Pan!.Value >> 1, FieldAudioPresentation.ResolveNativeModuleSoundPan(sound));
        BattleEvent receipt = Assert.Single(session.Events, item => item.NativeModuleSound?.Sound == 0x72);
        Assert.Equal(2, session.Events.Count(item => item.NativeModuleSound is not null));
        Assert.Null(receipt.ActorId);
        Assert.Equal((byte)16, sound.ReadCommittedVoice());
        Assert.Equal((byte)5, session.NativeGlobalSoundProcess!.Rotation);
        Assert.Equal(0x72u, session.NativeGlobalSoundProcess.OriginalEffectIds[16]);
        AssertEntrySoundAllocation(session);
        audio.Update(session.ForkForContinuation());
        Assert.Single(heard);
    }

    [Fact]
    public void Ejection_contact_commits_both_positioned_sounds_once_and_restore_does_not_replay_them()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeInputConfiguration input = game.ReadNewGameInputConfiguration();
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }];
        NativeBattleGraphicsDefinition graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter);
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeSavedOptions = input.Options, NativeInputButtonMap = input.ButtonMap, LimitProgress = game.ReadNewGameLimitProgress(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeCrtRandom = new(0), NativeModuleResources = game.ReadNativeBattleModuleResources(), NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(), NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, Combatants = actors };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession session = NativeBattleTypedFixture.CreateBattle(definition with { NativeClockRules = definition.NativeClockRules ?? assets.ReadNativeClockRules(), NativeBattleSpeed = definition.NativeBattleSpeed ?? game.ReadNewGameInputConfiguration().NativeBattleSpeedByte }, BattleAtbMode.TurnBased, graphicsResources);
        for (int frame = 0; frame < 100 && !session.Presentation!.Graphics.EntryReady; frame++)
        {
            session.Tick();
        }
        Assert.True(session.Presentation!.Graphics.EntryReady);
        AssertEntrySoundAllocation(session);
        BattleSnapshot saved = session.CaptureForContinuation();
        int index = FindOriginalModelSlot(saved.ReadFixturePresentation().NativeGraphics!, "ifrit");
        NativeBattleVector position = ReadOriginalModelCell(saved.ReadFixturePresentation().NativeGraphics!, index).Transform!.Position28;
        byte pan = saved.ReadFixturePresentation().NativeCamera!.Projection!.ReadPan(position);
        NativeBattleContactPacket contact = new(new(session.ReadPresentationModelOwner("ifrit"), 5, 0x40, 0, 0, 0, 0), null);
        session = NativeBattleTypedFixture.RestoreBattle(saved.WithFixturePresentation(facts => facts with { NativeGraphics = Assert.IsType<NativeBattleGraphicsState>(saved.ReadFixturePresentation().NativeGraphics) with { PendingContacts = [contact] } }));
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = BattleAudioTestOutput.Create(playModule: heard.Add);
        audio.Update(session);

        session.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        audio.Update(session);
        audio.Update(session);

        Assert.Equal(new uint[] { 0x0D, 0x0E }, heard.Select(sound => sound.Sound));
        Assert.Equal(new uint[] { 8, 16 }, heard.Select(sound => sound.Flags));
        Assert.All(heard, sound =>
        {
            Assert.Equal(new NativeBattleModuleAnchor(position.X, position.Y, position.Z, 0), sound.Anchor);
            Assert.Equal(pan, sound.Pan);
            Assert.Equal((uint)pan >> 1, FieldAudioPresentation.ResolveNativeModuleSoundPan(sound));
        });
        Assert.Equal(3, session.Events.Count(entry => entry.NativeModuleSound is not null));
        Assert.Equal(2, session.Events.Count(entry => entry.NativeModuleSound?.Sound is 0x0D or 0x0E));
        Assert.Equal(new byte[] { 4, 5 }, heard.Select(sound => sound.ReadCommittedVoice()));
        Assert.Equal((byte)5, session.NativeGlobalSoundProcess!.Rotation);
        Assert.Equal(0x0Du, session.NativeGlobalSoundProcess.OriginalEffectIds[4]);
        Assert.Equal(0x0Eu, session.NativeGlobalSoundProcess.OriginalEffectIds[5]);
        AssertEntrySoundAllocation(session);
        Assert.Empty(session.Presentation!.Graphics.PendingContacts);
        session.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        audio.Update(session);
        Assert.Equal(2, heard.Count);
        audio.Update(session.ForkForContinuation());
        Assert.Equal(2, heard.Count);
    }

    private static void AssertEntrySoundAllocation(BattleSession session)
    {
        BattleEvent entry = Assert.Single(session.Events, item => item.NativeSound is not null);
        NativeBattleModuleSound sound = Assert.IsType<NativeBattleModuleSound>(entry.NativeModuleSound);
        Assert.Equal(4u, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        //52CE30 first allocates its three encounter SFX, moving the cold rotation1 to4 before entry sound4.
        Assert.Equal((byte)29, sound.ReadCommittedVoice());
        Assert.Equal(new NativeGlobalSoundAllocation(4, 5, 29), sound.Allocation);
        Assert.Equal(4u, session.NativeGlobalSoundProcess!.OriginalEffectIds[29]);
    }

    [Fact]
    public void Installed_entry_pose_changes_pixels_while_gameplay_waits_and_restore_renders_identically()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeInputConfiguration input = game.ReadNewGameInputConfiguration();
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors = new[] { 0, 3, 4 }.Select(character => new BattleCombatantDefinition { Id = $"party-{character}", Name = $"Character{character}", IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id, InitialAtb = BattleSession.AtbMaximum }).Append(new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }).ToArray();
        NativeBattleGraphicsDefinition graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter);
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeSavedOptions = input.Options, NativeInputButtonMap = input.ButtonMap, LimitProgress = game.ReadNewGameLimitProgress(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, Combatants = actors };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession session = NativeBattleTypedFixture.CreateBattle(definition with { NativeClockRules = definition.NativeClockRules ?? assets.ReadNativeClockRules(), NativeBattleSpeed = definition.NativeBattleSpeed ?? game.ReadNewGameInputConfiguration().NativeBattleSpeedByte }, BattleAtbMode.TurnBased, graphicsResources);
        BattleScenePresentation scene = new(assets, definition);
        byte[] initial = scene.Render(session, 320, 224).ToArray();
        for (int callback = 0; callback < 100 && !session.Presentation!.Graphics.PrimitiveFrame!.LastCommands.Any(command => command.Pass > 0 && (command.Command.Opcode & 0xE0) == 0x20); callback++)
        {
            session.Tick();
        }
        Assert.Contains(session.Presentation!.Graphics.PrimitiveFrame!.LastCommands, command => command.Pass > 0 && (command.Command.Opcode & 0xE0) == 0x20);
        string beforeRender = JsonSerializer.Serialize(session.Capture());

        byte[] current = scene.Render(session, 320, 224).ToArray();

        Assert.False(initial.SequenceEqual(current));
        Assert.Equal(0, session.Frame);
        Assert.Equal(beforeRender, JsonSerializer.Serialize(session.Capture()));
        BattleSession restored = session.ForkForContinuation();
        Assert.Equal(current, new BattleScenePresentation(assets, definition).Render(restored, 320, 224));
    }
}
