using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

/// <summary>Controlled post-module50B2A0 protocol, not a claim that the GF animation completed.</summary>
public sealed class NativeBattleGuardianCompletionProtocolTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void Original_resource_phase7_restoration_then_low_camera_byte_gate_and_phase9_receipts(byte parameter81, bool waitForLowCameraByte)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession session = ControlledPostModule(game, assets, parameter81);
        BattleSnapshot protocol = session.Capture();
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(protocol))!);
        AdvanceBoth(ref session, ref clone);
        Assert.Equal((byte)8, ActionTask(session).Phase);
        NativeBattleGraphicsModel caster = session.NativeGraphics!.Models[0];
        NativeBattleGraphicsModel neighbor = session.NativeGraphics.Models[1];
        Assert.Equal(2, caster.Flags0 & 0xE);
        Assert.Equal(4, caster.Flags2 & 4);
        Assert.Equal(4, neighbor.Flags2 & 4);
        Assert.Equal(0, neighbor.Flags0 & 4);
        //The neighbor's original sequence argument includes1000 and bypasses DWORD8 bits18; the caster's argument does not.
        Assert.Equal((byte)2, neighbor.Sequence);
        Assert.Equal((ushort)1, neighbor.ResourceFlags44);
        Assert.Equal((byte)11, caster.Sequence);
        if (parameter81 == 2)
        {
            Assert.Equal(session.NativeStage!.ModelColors!.Color44, neighbor.Color44);
            Assert.Equal((byte)0, neighbor.Kind5);
            Assert.Equal((short)0, caster.SequenceState!.Header40);
            Assert.Equal((byte)2, caster.SequenceState.Pending3);
        }
        else
        {
            Assert.Equal((byte)12, neighbor.Kind5);
            Assert.Equal((short)123, caster.SequenceState!.Header40);
            Assert.Equal((byte)0, caster.SequenceState.Pending3);
        }
        Assert.Equal(0x10u, session.NativeGraphics.Flags & 0x10);
        Assert.Equal((byte)1, session.NativeEffectMemory!.Active);
        Assert.DoesNotContain(session.Events, item => item.NativeModuleSound is { Sound: 0x87 or 0x88 });
        Assert.DoesNotContain(session.NativeGraphics.Effects.Slots, task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0);
        SetCameraGate(ref session, waitForLowCameraByte ? (ushort)0x101 : (ushort)0x100);
        SetCameraGate(ref clone, waitForLowCameraByte ? (ushort)0x101 : (ushort)0x100);
        AdvanceBoth(ref session, ref clone);
        if (waitForLowCameraByte)
        {
            Assert.Equal((byte)8, ActionTask(session).Phase);
            Assert.Equal((ushort)0x101, session.NativeCamera!.Mask);
            Assert.Equal((byte)1, session.NativeEffectMemory!.Active);
            SetCameraGate(ref session, 0x100);
            SetCameraGate(ref clone, 0x100);
            AdvanceBoth(ref session, ref clone);
        }
        Assert.Equal((byte)9, ActionTask(session).Phase);
        Assert.Equal((ushort)0, session.NativeCamera!.Mask);
        Assert.False(session.NativeCamera.ForceCamera);
        Assert.Equal((byte)1, session.NativeEffectMemory!.Active);
        Assert.Equal(0x10u, session.NativeGraphics!.Flags & 0x10);
        AdvanceBoth(ref session, ref clone);
        Assert.DoesNotContain(session.NativeGraphics!.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104);
        Assert.Equal(0u, session.NativeGraphics.Flags & 0x10);
        Assert.Equal((byte)0, session.NativeEffectMemory!.Active);
        NativeBattleModuleSound[] sounds = session.Events.Where(item => item.NativeModuleSound is { Sound: 0x87 or 0x88 }).Select(item => item.NativeModuleSound!).ToArray();
        Assert.Equal(parameter81 == 3 ? new uint[] { 0x87, 0x88 } : [], sounds.Select(sound => sound.Sound).ToArray());
        Assert.All(sounds, sound =>
        {
            Assert.Equal(0u, sound.Flags);
            Assert.Equal((byte)128, sound.Pan);
            Assert.Null(sound.Anchor);
        });
        int numberTasks = session.NativeGraphics.Effects.Slots.Count(task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0);
        Assert.Equal(parameter81 == 3 ? 1 : 0, numberTasks);
        Assert.Equal(protocol.Combatants.Select(actor => actor.CurrentHp), session.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(protocol.NativeCrtRandom, session.Capture().NativeCrtRandom);
        Assert.Equal(protocol.NativeGlobalRandomCalls, session.Capture().NativeGlobalRandomCalls);
        AdvanceBoth(ref session, ref clone);
        Assert.DoesNotContain(session.NativeGraphics!.Descriptors.Queue, slot => session.NativeGraphics!.Descriptors.Packets[slot].Command == 104);
        Assert.Equal(sounds.Length, session.Events.Count(item => item.NativeModuleSound is { Sound: 0x87 or 0x88 }));
    }

    private static BattleSession ControlledPostModule(GameAssets game, BattleAssets assets, byte parameter81)
    {
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        int neighborWeapon = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id;
        //Two distinct original party resource families isolate the service protocol without aliasing load-source identities.
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "caster", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "neighbor", Name = "Zell", IsPlayer = true, ModelId = 1, WeaponId = neighborWeapon, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, encounter.Id);
        BattleDefinition definition = new()
        {
            EncounterId = 94,
            StageId = encounter.StageId,
            Combatants = actors,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeBattleUiRandom = new(0),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([116]),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 300 && !session.NativeEntryReady; callback++)
        {
            session.Tick();
        }
        Assert.True(session.NativeEntryReady);
        BattleSnapshot entry = session.Capture();
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        (packets, int header) = packets.Prepare(new(0, 0xFE, 14, -1, 64, 116, null));
        (packets, _) = packets.AppendContact(new(new(3, 5, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = entry.NativeGraphics!.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + 20u * (uint)header));
        session = BattleSession.Restore(entry with { NativeActionPackets = packets, NativeGraphics = entry.NativeGraphics with { Descriptors = descriptors } });
        //Run actual50AFC0/phase1 preparation once; the module entry has not been invoked at phase2.
        for (int callback = 0; callback < 200 && !session.NativeGraphics!.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 2); callback++)
        {
            session.Tick();
        }
        Assert.Equal((byte)2, ActionTask(session).Phase);
        BattleSnapshot prepared = session.Capture();
        NativeBattleGraphicsState graphics = prepared.NativeGraphics!;
        Assert.NotNull(graphics.ActionContext!.Matrices);
        Assert.Null(session.NativeModelLoader!.Task);
        int taskSlot = Enumerable.Range(0, 4).Single(index => (graphics.Descriptors.Tasks[index].Flags & 1) != 0 && graphics.Descriptors.Tasks[index].Kind == NativeBattleDescriptorTaskKind.Action104);
        NativeBattleDescriptorTask action = graphics.Descriptors.Tasks[taskSlot];
        descriptors = graphics.Descriptors with { Tasks = graphics.Descriptors.Tasks.SetItem(taskSlot, action with { Payload = action.Payload.SetItem(1, 7) }) };
        NativeBattleGraphicsModel caster = NativeBattleGraphicsModels.SelectSequence(graphics.Models[0], 11, true);
        NativeBattleGraphicsModel neighbor = NativeBattleGraphicsModels.SelectSequence(graphics.Models[1], 11, true);
        caster = caster with { Flags0 = (ushort)((caster.Flags0 & ~0xE) | 8), Flags8 = 0x19, ResourceFlags44 = 0, SequenceState = caster.SequenceState! with { Header40 = 123, Pending3 = 0 } };
        neighbor = neighbor with { Flags0 = (ushort)(neighbor.Flags0 | 4), Flags8 = 0x19, ResourceFlags44 = 0, Kind5 = 0, Phase6 = 0, Color44 = 0 };
        NativeBattleActionGraphicsState context = graphics.ActionGraphics! with { Guardian = graphics.ActionGraphics!.Guardian! with { Neighbor70 = 1 } };
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect! with { Parameters = graphics.OriginalEffect!.Parameters! with { Parameter81 = parameter81 }, ReturnedDescriptor = 0, Pools = [] };
        graphics = graphics with { Descriptors = descriptors, Models = graphics.Models.SetItem(0, caster).SetItem(1, neighbor), ActionGraphics = context, OriginalEffect = effect, ModuleEffects = NativeBattleModuleState.Clear(), Effects = NativeBattleEffectsState.Clear(), Auxiliary = NativeBattleAuxiliaryState.Clear(), Flags = graphics.Flags | 0x11 };
        NativeBattleCameraState camera = prepared.NativeCamera! with { Tasks = [null, null], Order = [], Mask = 0, ControlCursor = null, BlendProgress = 0 };
        return BattleSession.Restore(prepared with { NativeGraphics = graphics, NativeCamera = camera, NativeEffectMemory = prepared.NativeEffectMemory! with { Active = 1 } });
    }

    private static void SetCameraGate(ref BattleSession session, ushort mask)
    {
        BattleSnapshot saved = session.Capture();
        session = BattleSession.Restore(saved with { NativeCamera = saved.NativeCamera! with { Tasks = [null, null], Order = [], Mask = mask, ControlCursor = null, BlendProgress = 0 } });
    }

    private static void AdvanceBoth(ref BattleSession first, ref BattleSession second)
    {
        first.Tick();
        second.Tick();
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
    }

    private static NativeBattleDescriptorTask ActionTask(BattleSession session) => Assert.Single(session.NativeGraphics!.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104);
}
