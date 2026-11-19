using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBio22ModelMaterialsTests
{
    private const string Caster = "party-5";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OriginalBioRetainsTargetMaterialThroughDeformationAndCompletion(bool partyRecipient)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleEncounter formation = assets.LoadEncounter(812);
        BattleEnemyPlacement enemy = Assert.Single(formation.Enemies, slot => slot.Enabled);
        Assert.Equal(52, enemy.ModelId);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 2 && row.MagicId == 22);
        BattleCombatantDefinition[] actors =
        [
            Party("party-4", "Rinoa", 4, 0),
            Party("party-0", "Squall", 0, 0),
            Party(Caster, "Irvine", 2, 879) with { Abilities = [BattleAbility.Attack, BattleAbility.Magic], MagicStocks = [new(action.ActionId, 2)], Magic = 30, InitialAtb = BattleSession.AtbMaximum },
            new() { Id = "enemy-0", Name = assets.ReadEnemyName(enemy.ModelId), ModelId = enemy.ModelId, SceneSlot = enemy.Slot, MaximumHp = 10000, CurrentHp = 7862, Spirit = 5, Speed = 1, NativeStatusResistances = new byte[40], NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(enemy.ModelId) }
        ];

        BattleStage stage = assets.LoadStage(formation.StageId);
        NativeBattleGraphicsDefinition graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation);
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
        BattleSession battle = NativeBattleTypedFixture.CreateBattle(definition, BattleAtbMode.TurnBased, graphicsResources);
        BattleSnapshot fresh = battle.CaptureForContinuation();
        battle = NativeBattleTypedFixture.RestoreBattle(fresh.WithFixturePresentation(facts => facts with { NativeGraphics = fresh.ReadFixturePresentation().NativeGraphics! with { NativeOutcome = new NativeBattleOutcomeState() } }));
        for (int tick = 0; tick < 400 && !battle.NativeEntryReady; tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);

        string target = partyRecipient ? Caster : "enemy-0";
        BattleModelOwner owner = battle.ReadPresentationModelOwner(target);
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
            NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
            NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
            selected |= effect is { Binding.Effect: 22, ReturnedDescriptor: > 0 };
            contacts = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == Caster && item.TargetId == target && item.Message.StartsWith($"{action.Name}:", StringComparison.Ordinal));
            if (effect is { Binding.Effect: 22, ReturnedDescriptor: > 0 } && (graphics.ActorModels.Get(owner).Flags0 & 4) != 0)
            {
                NativeBattleLogicalResolvedCommand[] body = graphics.PrimitiveFrame!.LastCommands.Where(command => command.Command.PilotGeometry is { Identity.Effect: 22, Sprite: null } && command.Command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E && command.Command.Packet.Bank == "off_B6D070").ToArray();
                Assert.All(body, command => Assert.Equal(target, command.Command.ModelTextureOwner?.RequireActorId()));
                passedMaterialDraw |= body.Length > 0;
                Assert.All(graphics.PrimitiveFrame.LastCommands.Where(command => command.Command.PilotGeometry?.Sprite is not null), command => Assert.Null(command.Command.ModelTextureOwner));
                if (!frameCompared && body.Length > 0)
                {
                    BattleSession independent = NativeBattleTypedFixture.RestoreBattle(battle.CaptureForContinuation());
                    independent.Tick();
                    battle.Tick();
                    Assert.Equal(battle.Presentation!.Graphics.Callback, independent.Presentation!.Graphics.Callback);
                    Assert.Equal(battle.Presentation!.Graphics.PrimitiveFrame!.LastCommands.Select(command => command.Command.Packet), independent.Presentation!.Graphics.PrimitiveFrame!.LastCommands.Select(command => command.Command.Packet));
                    Assert.Equal(battle.Presentation!.Graphics.PrimitiveFrame.LastCommands.Select(command => command.Command.Words.ToArray()), independent.Presentation!.Graphics.PrimitiveFrame.LastCommands.Select(command => command.Command.Words.ToArray()));
                    Assert.Equal(battle.Presentation!.Graphics.PrimitiveFrame.LastCommands.Select(command => command.Command.ModelTextureOwner?.RequireActorId()), independent.Presentation!.Graphics.PrimitiveFrame.LastCommands.Select(command => command.Command.ModelTextureOwner?.RequireActorId()));
                    frameCompared = true;
                }
            }
            if (effect is { Binding.Effect: 22, ReturnedDescriptor: 0 } && !caster.IsBusy)
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
        Assert.Equal(0u, battle.Presentation!.Graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(0, battle.Presentation!.Graphics.ActorModels.Get(owner).Flags0 & 4);
        Assert.False(caster.IsBusy);
        Assert.Contains(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == Caster);

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
