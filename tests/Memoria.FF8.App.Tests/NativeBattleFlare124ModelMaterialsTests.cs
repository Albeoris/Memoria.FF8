using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFlare124ModelMaterialsTests
{
    private const string Caster = "party-5";

    [Fact]
    public void ExpandedFlareSilhouetteUsesItsOwnLoadedTextureBank()
    {
        const string irvineId = "party-2";
        const byte flare = 15;
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        using GameAssets game = new(directory);
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory());
        GameSession session = new(field);
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, session, PrepareParty, additionalEnemies: 0);
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleCombatant irvine = Assert.Single(battle.Combatants, actor => actor.Id == irvineId);
        BattleModelOwner owner = battle.ReadPresentationModelOwner(irvineId);
        Assert.False(process.Graphics.NativeBindings.TryEncodeExactActor(owner, process.Graphics.ActorModels, out _));
        irvine.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: irvineId, TargetId: irvineId, Ability: BattleAbility.Magic, ActionId: flare));

        bool drewSilhouette = false;
        for (int callback = 0; callback < 600 && (irvine.IsBusy || !drewSilhouette); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            foreach (NativeBattleLogicalResolvedCommand packet in process.Graphics.PrimitiveFrame!.LastCommands)
            {
                if (packet.Command.PilotGeometry?.Identity.Bank == 0x6BCAD0)
                {
                    Assert.Equal(irvineId, packet.Command.ModelTextureOwner?.RequireActorId());
                    drewSilhouette = true;
                }
            }
        }
        Assert.True(drewSilhouette);
        Assert.False(irvine.IsBusy);
        Assert.Equal(1, irvine.MagicStocks.Single(stock => stock.Id == flare).Count);

        static void PrepareParty(GameSession current)
        {
            NativeCharacterVitals vitals = (current.State.CharacterVitals! with { CombatParty = [0, 1, 3] }).WithGuestBattleRecords().Rest();
            NativeCharacterVital[] records = vitals.Records.ToArray();
            foreach (int row in new[] { 0, 1, 3 })
            {
                records[row] = records[row] with { MemberFlags = (ushort)(records[row].MemberFlags | 1), HitPoints = new(0, 0), Status = 1 };
            }
            uint magicCommand = checked((uint)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2)));
            NativeMagicStock[] stock = records[2].MagicInventory.Select(_ => new NativeMagicStock(0, 0)).ToArray();
            stock[0] = new(flare, 2);
            records[2] = records[2] with { MemberFlags = (ushort)(records[2].MemberFlags | 1), HitPoints = new(1000, 1000), Status = 0, MagicInventory = stock, Junction = records[2].Junction with { Commands = magicCommand } };
            current.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([0, 1, 3, 2], 4));
        }
    }

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
        BattleSnapshot fresh = battle.CaptureForContinuation();
        battle = BattleSession.Restore(fresh.WithFixturePresentation(facts => facts with { NativeGraphics = fresh.ReadFixturePresentation().NativeGraphics! with { NativeOutcome = new NativeBattleOutcomeState() } }));
        for (int tick = 0; tick < 400 && !battle.NativeEntryReady; tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);

        string target = partyRecipient ? Caster : "enemy-0";
        BattleModelOwner owner = battle.ReadPresentationModelOwner(target);
        if (partyRecipient)
        {
            NativeBattleGraphicsModel model = battle.Presentation!.Graphics.ActorModels.Get(owner);
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
            NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
            NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
            selected |= effect is { Binding.Effect: 124, ReturnedDescriptor: > 0 };
            contacts = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == Caster && item.TargetId == target);
            if (effect is { Binding.Effect: 124, ReturnedDescriptor: > 0 } && (graphics.ActorModels.Get(owner).Flags0 & 4) != 0)
            {
                passedMaterialDraw |= graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                if (!frameCompared)
                {
                    BattleSession independent = BattleSession.Restore(battle.Capture());
                    independent.Tick();
                    battle.Tick();
                    Assert.Equal(battle.Presentation!.Graphics.Callback, independent.Presentation!.Graphics.Callback);
                    Assert.Equal(battle.Presentation!.Graphics.PrimitiveFrame!.LastCommands.Select(command => command.Command.Packet), independent.Presentation!.Graphics.PrimitiveFrame!.LastCommands.Select(command => command.Command.Packet));
                    Assert.Equal(battle.Presentation!.Graphics.PrimitiveFrame.LastCommands.Select(command => command.Command.Words.ToArray()), independent.Presentation!.Graphics.PrimitiveFrame.LastCommands.Select(command => command.Command.Words.ToArray()));
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
        Assert.Equal(0u, battle.Presentation!.Graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(0, battle.Presentation!.Graphics.ActorModels.Get(owner).Flags0 & 4);
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
