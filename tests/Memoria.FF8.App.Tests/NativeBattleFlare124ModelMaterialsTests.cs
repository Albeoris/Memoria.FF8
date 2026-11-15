using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFlare124ModelMaterialsTests
{
    private const string Caster = "party-5";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OriginalFlareCompletesWithLoadedModelMaterials(bool partyRecipient)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleEncounter formation = assets.LoadEncounter(116);
        BattleEnemyPlacement enemy = Assert.Single(formation.Enemies, slot => slot.Enabled);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 2 && row.ActionId == 15 && row.MagicId == 124);
        BattleCombatantDefinition[] actors =
        [
            Party("party-4", "Rinoa", 4, 0),
            Party("party-0", "Squall", 0, 0),
            Party(Caster, "Irvine", 2, 879) with { Abilities = [BattleAbility.Attack, BattleAbility.Magic], MagicStocks = [new(action.ActionId, 2)], Magic = 30, InitialAtb = BattleSession.AtbMaximum },
            new() { Id = "enemy-0", Name = assets.ReadEnemyName(enemy.ModelId), ModelId = enemy.ModelId, SceneSlot = enemy.Slot, MaximumHp = 10000, CurrentHp = 7862, Spirit = 5, Speed = 1, NativeStatusResistances = new byte[40], NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(enemy.ModelId) }
        ];

        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            ActionDefinitions = BattlePartyProjection.CreateActions(game.ReadNewGameCharacterVitals(), assets.ActionDefinitions),
            NativeActions = assets.NativeActions.ToArray(),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeBattleSpeed = 0,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
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
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([action.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed,
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeScannedModels = game.ReadNewGameScannedModels(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleSnapshot fresh = battle.Capture();
        battle = BattleSession.Restore(fresh with { NativeGraphics = fresh.NativeGraphics! with { NativeOutcome = new NativeBattleOutcomeState() } });
        for (int tick = 0; tick < 400 && !battle.NativeEntryReady; tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);

        string target = partyRecipient ? Caster : "enemy-0";
        int index = battle.NativeGraphics!.ActorIds.IndexOf(target);
        Assert.True(index >= 0);
        if (partyRecipient)
        {
            NativeBattleGraphicsModel model = battle.NativeGraphics.Models[index];
            NativeBattleGraphicsResource resource = Assert.Single(definition.NativeGraphics!.Bindings, binding => binding.ActorId == target).Resource;
            Assert.True(model.ModelId4 < 16);
            Assert.Null(model.ActiveStagedResourceRow100);
            Assert.Null(model.ResourceMagicId);
            Assert.Null(model.ResourceRenzokukenTargetBank);
            Assert.False(resource.GeometryRelocated507550);
            NativeBattleModelTextureBinding binding = Assert.IsType<NativeBattleModelTextureBinding>(model.BodyTextureSlots);
            NativeBattleRawPolygon[] once = resource.Geometry!.Value.SelectMany(mesh => mesh.Polygons)
                .Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, binding) }).ToArray();
            Assert.Contains(once, polygon => Record.Exception(() => NativeBattleRawGeometryPackets.Relocate507550(polygon, binding)) is NotSupportedException);
        }
        BattleCombatant combatant = battle.Combatants.Single(actor => actor.Id == target);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == Caster);
        Assert.Equal(2, Stock());
        int initialHp = combatant.CurrentHp;
        battle.QueueAction(Caster, target, BattleAbility.Magic, action.ActionId);

        bool selected = false;
        bool frameCompared = false;
        bool passedMaterialDraw = false;
        int contacts = 0;
        for (int tick = 0; tick < 600; tick++)
        {
            battle.Tick();
            NativeBattleGraphicsState graphics = battle.NativeGraphics!;
            NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
            selected |= effect is { Binding.Effect: 124, ReturnedDescriptor: > 0 };
            contacts = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == Caster && item.TargetId == target);
            if (effect is { Binding.Effect: 124, ReturnedDescriptor: > 0 } && (graphics.Models[index].Flags0 & 4) != 0)
            {
                passedMaterialDraw |= graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                if (!frameCompared)
                {
                    BattleSession independent = BattleSession.Restore(battle.Capture());
                    independent.Tick();
                    battle.Tick();
                    Assert.Equal(battle.NativeGraphics!.Callback, independent.NativeGraphics!.Callback);
                    Assert.Equal(battle.NativeGraphics.PrimitiveFrame!.LastCommands.Select(command => command.Command.Packet), independent.NativeGraphics.PrimitiveFrame!.LastCommands.Select(command => command.Command.Packet));
                    frameCompared = true;
                }
            }
            if (effect is { Binding.Effect: 124, ReturnedDescriptor: 0 } && !caster.IsBusy)
            {
                break;
            }
        }

        Assert.True(selected);
        Assert.True(passedMaterialDraw);
        Assert.True(frameCompared);
        Assert.Equal(1, contacts);
        Assert.Equal(1, Stock());
        Assert.True(combatant.CurrentHp < initialHp);
        Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(0, battle.NativeGraphics.Models[index].Flags0 & 4);
        Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == Caster);

        int Stock() => caster.NativeMagicInventory.Single(stock => stock.SpellId == action.ActionId).Count;

        BattleCombatantDefinition Party(string id, string name, int model, int hp)
        {
            return new()
            {
                Id = id,
                Name = name,
                IsPlayer = true,
                ModelId = model,
                WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == model).Id,
                MaximumHp = 1000,
                CurrentHp = hp,
                NativeDeathCount = 0,
                NativeStatusResistances = new byte[40],
                NativeStatus0 = hp == 0 ? (ushort)1 : (ushort)0
            };
        }
    }
}
