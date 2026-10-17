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
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }];
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeCrtRandom = new(0), NativeModuleResources = game.ReadNativeBattleModuleResources(), NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(), NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter), NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, Combatants = actors };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        for (int frame = 0; frame < 100 && !session.NativeGraphics!.EntryReady; frame++)
        {
            session.Tick();
        }
        Assert.True(session.NativeGraphics!.EntryReady);
        Assert.True(session.Combatants[0].IsReady);
        AssertEntrySoundAllocation(session);
        BattleSnapshot saved = session.Capture();
        session = BattleSession.Restore(saved with { NativeGraphics = saved.NativeGraphics! with { ModuleEffects = saved.NativeGraphics.ModuleEffects.AppendModelResource(0, definition.NativeModuleResources[39], 0x100027) } });
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(session);
        NativeBattleCameraProjection projection = session.NativeCamera!.Projection!;
        for (int callback = 0; callback < 3; callback++)
        {
            session.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
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
        audio.Update(BattleSession.Restore(session.Capture()));
        Assert.Single(heard);
    }

    [Fact]
    public void Ejection_contact_commits_both_positioned_sounds_once_and_restore_does_not_replay_them()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }];
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeCrtRandom = new(0), NativeModuleResources = game.ReadNativeBattleModuleResources(), NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(), NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter), NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, Combatants = actors };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        for (int frame = 0; frame < 100 && !session.NativeGraphics!.EntryReady; frame++)
        {
            session.Tick();
        }
        Assert.True(session.NativeGraphics!.EntryReady);
        AssertEntrySoundAllocation(session);
        BattleSnapshot saved = session.Capture();
        int index = saved.NativeGraphics!.ActorIds.IndexOf("ifrit");
        NativeBattleVector position = saved.NativeGraphics.Models[index].Transform!.Position28;
        byte pan = saved.NativeCamera!.Projection!.ReadPan(position);
        NativeBattleContactPacket contact = new(new(checked((byte)index), 5, 0x40, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));
        session = BattleSession.Restore(saved with { NativeGraphics = saved.NativeGraphics with { PendingContacts = [contact] } });
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        audio.Update(session);

        session.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
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
        Assert.Empty(session.NativeGraphics!.PendingContacts);
        session.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
        audio.Update(session);
        Assert.Equal(2, heard.Count);
        audio.Update(BattleSession.Restore(session.Capture()));
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

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 0.5f)]
    [InlineData(5, 1, 2)]
    [InlineData(2, 2, 1)]
    public void Raw_pose_uses_the_decoded_frame_counter_including_slow_and_fast_modes(byte counter, byte flags, float expected)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel body = assets.LoadPlayer(0);
        BattleAnimationPresentation animation = new(body, null, new() { Id = "squall", IsPlayer = true });
        NativeBattleGraphicsModel row = new(0, 3) { Flags2 = 0, SequenceState = new() { Pose = 0, PoseFrame114 = counter, PoseFlags109 = flags } };

        BattleAnimationSample sample = animation.SampleNativeModel(row);

        Assert.Equal(0, sample.Animation);
        Assert.Equal(expected, sample.Frame);
        Assert.False(sample.Loop);
        Assert.Equal(Vector3.Zero, sample.Offset);
        Assert.Equal(0, animation.SampleNativeModel(row with { Flags0 = 7 }).Opacity);
        Assert.Equal(0, animation.SampleNativeModel(row with { SequenceState = new() }).Opacity);
    }

    [Fact]
    public void Installed_entry_pose_changes_pixels_while_gameplay_waits_and_restore_renders_identically()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors = new[] { 0, 3, 4 }.Select(character => new BattleCombatantDefinition { Id = $"party-{character}", Name = $"Character{character}", IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id, InitialAtb = BattleSession.AtbMaximum }).Append(new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }).ToArray();
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter), NativeStageSource = stage.NativeSource, NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, Combatants = actors };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        byte[] initial = scene.Render(session, 320, 224).ToArray();
        for (int callback = 0; callback < 100 && !session.NativeGraphics!.PrimitiveFrame!.LastCommands.Any(command => command.Pass > 0 && (command.Command.Opcode & 0xE0) == 0x20); callback++)
        {
            session.Tick();
        }
        Assert.Contains(session.NativeGraphics!.PrimitiveFrame!.LastCommands, command => command.Pass > 0 && (command.Command.Opcode & 0xE0) == 0x20);
        string beforeRender = JsonSerializer.Serialize(session.Capture());

        byte[] current = scene.Render(session, 320, 224).ToArray();

        Assert.False(initial.SequenceEqual(current));
        Assert.Equal(0, session.Frame);
        Assert.Equal(beforeRender, JsonSerializer.Serialize(session.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(beforeRender)!);
        Assert.Equal(current, new BattleScenePresentation(assets, definition, true).Render(restored, 320, 224));
    }
}
