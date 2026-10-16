using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianPresentationTests
{
    [Fact]
    public void Installed_Quezacotl_runs_original_guardian_service_entry_and_stops_at_its_unported_private_child()
    {
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        //Declared fresh installed-resource component, not a reconstructed gameplay save. Its explicit playtime-derived UI seed is0.
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            ActionDefinitions = BattlePartyProjection.CreateActions(game.ReadNewGameCharacterVitals(), assets.ActionDefinitions),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
            NativeBattleUiRandom = new(0),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([116]),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleActionDefinition guardian = Assert.Single(definition.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 0);
        Assert.Equal((ushort)116, guardian.NativeAction!.MagicId);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot before = battle.Capture();
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        //Native FE/action64 selects sequence14 through495070;116 is the original kernel GF effect ID.
        (packets, int header) = packets.Prepare(new(0, 0xFE, 14, -1, 64, 116, null));
        (packets, _) = packets.AppendContact(new(new(3, 5, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        battle = BattleSession.Restore(before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } });
        TimeSpan frame = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        bool selected = false;
        bool restoredBeforeEntry = false;
        bool sawEntry = false;
        NotSupportedException? boundary = null;
        for (int callback = 0; callback < 360; callback++)
        {
            if (!restoredBeforeEntry && battle.NativeGraphics!.Descriptors.Tasks.Any(task => task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 2))
            {
                BattleSnapshot pending = battle.Capture();
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(pending))!);
                battle.AdvanceNativeGraphicsPresentation(frame);
                restored.AdvanceNativeGraphicsPresentation(frame);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                restoredBeforeEntry = true;
            }
            try
            {
                battle.AdvanceNativeGraphicsPresentation(frame);
            }
            catch (NotSupportedException exception)
            {
                boundary = exception;
                break;
            }
            selected |= battle.NativeGraphics!.ActionGraphics?.Callback == 0x50B2A0;
            sawEntry |= battle.NativeGraphics.OriginalEffect?.ReturnedDescriptor == 0x2521748;
        }
        NativeBattleGraphicsModel finalCaster = battle.NativeGraphics!.Models[0];
        string diagnostic = $"callback={battle.NativeGraphics.Callback}; actionPhase={battle.NativeGraphics.Descriptors.Tasks.FirstOrDefault(task => task.Kind == NativeBattleDescriptorTaskKind.Action104)?.Phase}; casterFlags0={finalCaster.Flags0:X}; sequence={finalCaster.Sequence}; cursor={finalCaster.SequenceState?.Offset46}; resource44={finalCaster.ResourceFlags44:X}; IO={battle.NativeEffectIoResult}; cameraMask={battle.NativeCamera?.Mask}; descriptor={battle.NativeGraphics.OriginalEffect?.ReturnedDescriptor:X}; boundary={boundary}";
        Assert.True(selected);
        Assert.True(restoredBeforeEntry);
        Assert.True(boundary is not null, diagnostic);
        Assert.True(sawEntry, diagnostic);
        Assert.NotNull(boundary);
        Assert.Contains("6C3940", boundary.Message);
        Assert.Contains("7016B0", boundary.Message);
        Assert.Equal((ushort)116, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(0x2521748u, battle.NativeGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Contains(battle.NativeGraphics.OriginalEffect.Pools, pool => pool.DescriptorAddress == 0x2521738 && pool.Stride == 0x8D0 && pool.Slots.Single().Callback == 0x6C3940);
        Assert.Equal((byte?)0, battle.NativeEffectMemory!.GuardianCasterBank);
        Assert.Equal((ushort)1, battle.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.NotEqual(0, battle.NativeGraphics.Models[3].Flags0 & 4);
        Assert.Contains(battle.Events, entry => entry.NativeModuleSound is { Sound: 240066, Flags: 1, Pan: 128 });
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_GF_EVIDENCE");
        if (!string.IsNullOrEmpty(evidence))
        {
            Directory.CreateDirectory(evidence);
            string name = "quezacotl116-prefix-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
            using FileStream output = new(Path.Combine(evidence, name + ".battle.json"), FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(output, battle.Capture());
        }
    }
}
