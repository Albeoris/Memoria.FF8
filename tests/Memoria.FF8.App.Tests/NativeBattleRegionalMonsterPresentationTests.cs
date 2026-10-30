using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRegionalMonsterPresentationTests
{
    [Fact]
    public void Resume_the_selected_monster_standalone_for_one_callback()
    {
        ResumeSelectedStandalone(1);
    }

    [Fact]
    public void Resume_the_selected_monster_standalone_for_a_bounded_callback_range()
    {
        string? value = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_CALLBACKS");
        Assert.SkipUnless(value is not null, "Select a bounded diagnostic callback count.");
        int callbacks = int.Parse(value!);
        Assert.InRange(callbacks, 1, 4096);
        ResumeSelectedStandalone(callbacks);
    }

    private static void ResumeSelectedStandalone(int callbacks)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_EVIDENCE");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone monster checkpoint and a distinct evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        for (int callback = 0; callback < callbacks; callback++)
        {
            first.Tick();
            second.Tick();
        }
        Assert.Equal(snapshot.NativeGraphics!.Callback + callbacks, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
        NativeBattleRestorationPresentationTests.Publish(output!, "next", first.Capture(), pixels);
    }

    [Fact]
    public void Execute_the_selected_original_monster_bank_action_and_restore_its_standalone_boundaries()
    {
        string? selection = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_ACTION");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(selection is not null && output is not null && directory is not null, "Select installed resources, a model/level/slot case and a distinct evidence directory.");
        int[] values = selection!.Split('/').Select(int.Parse).ToArray();
        Assert.Equal(3, values.Length);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        int model = values[0];
        int level = values[1];
        int slot = values[2];
        Assert.InRange(level, 1, 100);
        Assert.InRange(slot, 0, 15);
        NativeBattleAiDefinition installedAi = assets.ReadEnemyAi(model, level);
        NativeBattleAiAbility ability = installedAi.Abilities[16 * installedAi.LevelBand + slot];
        Assert.NotEqual((ushort)0, ability.Id);
        BattleNativeAction native = Assert.Single(assets.NativeActions, action => action.Command == ability.Command && action.ActionId == ability.Id);
        string? control = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_CONTROL");
        if (control is not null)
        {
            ushort effect = ushort.Parse(control);
            Assert.Contains(effect, new ushort[] { 17, 18, 110, 248 });
            native = native with { Command = 252, ActionId = 1, MagicId = effect, AttackType = 0 };
            ability = new(252, 1, 0);
        }
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_SNAPSHOT");
        BattleSnapshot? source = sourcePath is null ? null : JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(sourcePath));
        BattleDefinition definition = source?.Definition ?? CreateDefinition(game, assets, model, level, slot, installedAi, native);
        Assert.Equal(model, definition.Combatants.Single(actor => actor.Id == "monster").ModelId);
        BattleSession battle = source is null ? new(definition, BattleAtbMode.Normal) : BattleSession.Restore(source);
        BattleScenePresentation scene = new(assets, definition, true);
        BattleSnapshot prior = battle.Capture();
        string startName = source is null ? "before-entry" : "resume-source";
        Publish(startName, prior);
        battle = Load(startName);
        bool selected = false;
        bool produced = false;
        bool visible = false;
        bool completed = false;
        bool restoredLive = false;
        int callbacks = 0;
        int peakChangedRgb = 0;
        int capturedControlFrames = 0;
        HashSet<uint> capturedControlBanks = [];
        try
        {
            // A diagnostic liveness bound only; reaching it never establishes native completion.
            for (; callbacks < 4096; callbacks++)
            {
                prior = battle.Capture();
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                produced |= current.Events.Any(item => item.Kind == "Windup" && item.ActorId == "monster");
                if (control is not null && native.MagicId is 17 or 18 && graphics.OriginalEffect is { } controlEffect && controlEffect.Binding.Effect == native.MagicId && (controlEffect.ReturnedDescriptor is > 0 || prior.NativeGraphics?.OriginalEffect?.ReturnedDescriptor is > 0) && graphics.PrimitiveFrame is { } controlPrimitives)
                {
                    NativeBattleLogicalCommand[] owned;
                    if (native.MagicId == 17)
                    {
                        ImmutableHashSet<NativeBattlePrimitiveReference> references = controlEffect.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToImmutableHashSet();
                        owned = controlPrimitives.LastCommands.Select(item => item.Command).Where(command => references.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet))).ToArray();
                    }
                    else
                    {
                        owned = NativeBattleSelectedCinematicObserver.CurrentCommands(current, prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor!.Value, includeCompleted: true);
                    }
                    NativeBattleLogicalCommand[] geometry = owned.Where(command => command.Opcode is >= 0x20 and < 0x40).ToArray();
                    foreach (NativeBattleLogicalCommand command in geometry)
                    {
                        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
                        Assert.Equal(native.MagicId, visual.Identity.Effect);
                        Assert.True(visual.Identity.Generation > 0);
                        capturedControlBanks.Add(visual.Identity.Bank);
                    }
                    if (geometry.Length > 0)
                    {
                        capturedControlFrames++;
                    }
                }
                if (graphics.OriginalEffect is { } effect && effect.RequestedEffect == native.MagicId)
                {
                    selected = true;
                    if (!restoredLive && effect.ReturnedDescriptor is > 0 && graphics.PrimitiveFrame is { LastCommands.IsEmpty: false })
                    {
                        VerifyContinuation("live", current);
                        restoredLive = true;
                    }
                }
                if (produced && graphics.OriginalEffect is { ReturnedDescriptor: > 0 } selectedEffect && selectedEffect.RequestedEffect == native.MagicId && !visible && graphics.PrimitiveFrame is { } primitive && !primitive.LastCommands.IsEmpty)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> references = NativeBattleTargetGuardianCastTests.GuardianPacketReferences(prior, current, native.MagicId, game);
                    byte[] rendered = scene.Render(battle, 320, 224).ToArray();
                    BattleSession background = BattleSession.Restore(current with { NativeGraphics = graphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !references.Contains(command.Command.Packet)).ToImmutableArray() } } });
                    byte[] plain = new BattleScenePresentation(assets, definition, true).Render(background, 320, 224).ToArray();
                    int changed = 0;
                    for (int offset = 0; offset < rendered.Length; offset += 4)
                    {
                        if (!rendered.AsSpan(offset, 3).SequenceEqual(plain.AsSpan(offset, 3)))
                        {
                            changed++;
                        }
                    }
                    if (changed > peakChangedRgb)
                    {
                        peakChangedRgb = changed;
                        visible = true;
                        Publish("visible", current);
                    }
                }
                if (produced && current.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == "monster") && battle.ActiveAction is null && graphics.OriginalEffect is not { ReturnedDescriptor: > 0 } && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104))
                {
                    completed = true;
                    VerifyContinuation("completed", current);
                    break;
                }
            }
            Assert.True(produced, "The selected original bank action never began its ordinary command.");
            Assert.True(native.MagicId == 0 || selected, "The ordinary command never selected its original effect module.");
            Assert.True(visible, "The selected action submitted no visible packet contribution.");
            Assert.True(completed, "The selected ordinary monster action did not release its command and effect task.");
            if (control is not null && native.MagicId is 17 or 18)
            {
                Assert.True(capturedControlFrames > 1, "The original control component must reach captured owned geometry on multiple callbacks.");
                if (native.MagicId == 17)
                {
                    Assert.Contains(0x2556FC8u, capturedControlBanks);
                    Assert.Contains(0x2556FD8u, capturedControlBanks);
                }
            }
            Assert.Single(battle.Events, item => item.Kind == "Windup" && item.ActorId == "monster");
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "monster");
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", model, level, slot, Driver = control is null ? "bank" : "opcode30-control", ability.Command, ability.Id, ability.Sequence, native.MagicId, callbacks, peakChangedRgb, restoredLive, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output!, "before-boundary.battle.json"), JsonSerializer.Serialize(prior));
            BattleSnapshot failed = battle.Capture();
            File.WriteAllText(Path.Combine(output!, "failed-live.battle.json"), JsonSerializer.Serialize(failed));
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", model, level, slot, Driver = control is null ? "bank" : "opcode30-control", ability.Command, ability.Id, ability.Sequence, native.MagicId, callbacks, peakChangedRgb, restoredLive, selected, produced, error = error.ToString() }));
            try
            {
                Publish("before-boundary", prior);
                Publish("failed-live", failed);
            }
            catch (Exception presentationError)
            {
                File.WriteAllText(Path.Combine(output!, "presentation-error.txt"), presentationError.ToString());
            }
            throw;
        }

        void Publish(string name, BattleSnapshot snapshot)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
        }

        BattleSession Load(string name)
        {
            BattleSnapshot loaded = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, name + ".battle.json")))!;
            BattleSession restored = BattleSession.Restore(loaded);
            Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(restored.Capture()));
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }

        void VerifyContinuation(string name, BattleSnapshot snapshot)
        {
            Publish(name, snapshot);
            BattleSession first = Load(name);
            BattleSession second = BattleSession.Restore(snapshot);
            second.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            first.Tick();
            second.Tick();
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(new BattleScenePresentation(assets, definition, true).Render(first, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(second, 320, 224).ToArray());
            Publish(name + "-next", first.Capture());
        }
    }

    internal static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets, int model, int level, int slot, NativeBattleAiDefinition installedAi, BattleNativeAction action)
    {
        BattleEncounter formation = Enumerable.Range(0, 1024).Select(assets.LoadEncounter).First(item => item.Slots.Any(enemy => enemy.ModelId == model));
        if (action.Command == 252 && action.MagicId is 17 or 18)
        {
            formation = Enumerable.Range(0, 1024).Select(assets.LoadEncounter).First(item => item.Slots.Any(enemy => enemy.ModelId == 91 && enemy.Slot == 2) && item.Slots.Any(enemy => enemy.ModelId == 74) && item.Slots.Any(enemy => enemy.ModelId == 73));
        }
        if (action.Command == 252 && action.MagicId == 248)
        {
            formation = Enumerable.Range(0, 1024).Select(assets.LoadEncounter).First(item => item.Enemies.Count == 3 && item.Enemies.All(enemy => enemy.ModelId == 71));
        }
        BattleEnemyPlacement placement = formation.Slots.First(enemy => enemy.ModelId == model);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(model).Project(level);
        bool allies = action.AttackType is 3 or 4 or 5 or 6 or 13 or 14;
        byte selector = allies ? (byte)200 : (byte)201;
        // Explicit one-action component. The selected original bank, sequence and kernel record are unchanged.
        // The short driver selects this bank slot once in INIT; it does not stand in for original story AI coverage.
        NativeBattleAiDefinition driver = installedAi with { Scripts = [[4, selector, 12, checked((byte)slot), 0], [0], [0], [0], [0]] };
        if (action.Command == 252)
        {
            byte controlSelector = action.MagicId == 110 ? (byte)205 : (byte)200;
            driver = installedAi with { Scripts = [[4, controlSelector, 30, checked((byte)action.MagicId), 0], [0], [0], [0], [0]] };
        }
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 30000, CurrentHp = 30000, NativeStatusResistances = assets.ReadPlayerNativeStatusData(game.ReadNewGameCharacterVitals(), 0).Resistances.ToArray(), NativeDeathCount = 0 },
            new() { Id = "monster", Name = assets.ReadEnemyName(model), ModelId = model, SceneSlot = placement.Slot, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = Math.Max(1, stats.MaximumHp / 2), Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = 1, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeStatusResistances = assets.ReadEnemyNativeStatusData(model).Resistances.ToArray(), Information246 = assets.ReadEnemyNativeInformation246(model), NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(model), NativeAi = driver }
        ];
        if (action.Command == 252 && action.MagicId == 110)
        {
            BattleEnemyPlacement wedge = formation.Slots.Single(enemy => enemy.ModelId == 73);
            BattleEnemyNativeProjection wedgeStats = assets.ReadEnemyNativeStats(73).Project(level);
            NativeBattleAiDefinition wedgeAi = assets.ReadEnemyAi(73, level) with { Scripts = [[0], [0], [0], [0], [0]] };
            // The original departure caller selects both active tower opponents with selector205.
            BattleCombatantDefinition companion = new() { Id = "companion", Name = assets.ReadEnemyName(73), ModelId = 73, SceneSlot = wedge.Slot, Level = level, MaximumHp = wedgeStats.MaximumHp, CurrentHp = wedgeStats.MaximumHp, Attack = wedgeStats.Strength, Defense = wedgeStats.Vitality, Magic = wedgeStats.Magic, Spirit = wedgeStats.Spirit, Speed = 1, ElementDefenses = wedgeStats.ElementDefenses.Select(value => (int)value).ToArray(), NativeStatusResistances = assets.ReadEnemyNativeStatusData(73).Resistances.ToArray(), Information246 = assets.ReadEnemyNativeInformation246(73), NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(73), NativeAi = wedgeAi };
            actors = [actors[0], actors[1], companion];
        }
        if (action.Command == 252 && action.MagicId == 248)
        {
            foreach (BattleEnemyPlacement companionPlacement in formation.Enemies.Where(enemy => enemy.Slot != placement.Slot))
            {
                NativeBattleAiDefinition companionAi = assets.ReadEnemyAi(model, level) with { Scripts = [[0], [0], [0], [0], [0]] };
                BattleCombatantDefinition companion = actors[1] with { Id = $"companion-{companionPlacement.Slot}", SceneSlot = companionPlacement.Slot, NativeAi = companionAi };
                actors = [.. actors, companion];
            }
        }
        if (action.Command == 252 && action.MagicId is 17 or 18)
        {
            List<BattleCombatantDefinition> tower = [];
            foreach (BattleEnemyPlacement towerPlacement in formation.Slots.Where(enemy => enemy.Slot < 2).OrderBy(enemy => enemy.Slot))
            {
                int towerModel = towerPlacement.ModelId;
                BattleEnemyNativeProjection towerStats = assets.ReadEnemyNativeStats(towerModel).Project(level);
                NativeBattleAiDefinition towerAi = assets.ReadEnemyAi(towerModel, level) with { Scripts = [[0], [0], [0], [0], [0]] };
                tower.Add(new() { Id = $"tower-{towerPlacement.Slot}", Name = assets.ReadEnemyName(towerModel), ModelId = towerModel, SceneSlot = towerPlacement.Slot, Level = level, MaximumHp = towerStats.MaximumHp, CurrentHp = towerStats.MaximumHp, Attack = towerStats.Strength, Defense = towerStats.Vitality, Magic = towerStats.Magic, Spirit = towerStats.Spirit, Speed = 1, ElementDefenses = towerStats.ElementDefenses.Select(value => (int)value).ToArray(), NativeStatusResistances = assets.ReadEnemyNativeStatusData(towerModel).Resistances.ToArray(), Information246 = assets.ReadEnemyNativeInformation246(towerModel), NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(towerModel), NativeAi = towerAi });
            }
            // Keep the original tower physical allocation order for the fixed camera reference50.
            actors = [actors[0], .. tower, actors[1]];
        }
        if (action.MagicId is 17 or 18 or 42)
        {
            NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
            BattleCombatantDefinition zell = new() { Id = "party-1", Name = "Zell", IsPlayer = true, ModelId = 1, WeaponId = vitals.Records[1].Weapon, MaximumHp = 30000, CurrentHp = 30000, NativeCharacterFlag100 = assets.ReadCharacterNativeConditionFlag100(1), NativeStatusResistances = assets.ReadPlayerNativeStatusData(vitals, 1).Resistances.ToArray(), NativeDeathCount = 0 };
            int thirdCharacter = action.MagicId is 17 or 18 ? 5 : 2;
            BattleCombatantDefinition third = new() { Id = "party-2", Name = thirdCharacter == 5 ? "Selphie" : "Irvine", IsPlayer = true, ModelId = thirdCharacter, WeaponId = vitals.Records[thirdCharacter].Weapon, MaximumHp = 30000, CurrentHp = 30000, NativeCharacterFlag100 = assets.ReadCharacterNativeConditionFlag100(thirdCharacter), NativeStatusResistances = assets.ReadPlayerNativeStatusData(vitals, thirdCharacter).Resistances.ToArray(), NativeDeathCount = 0 };
            actors = [actors[0], zell, third, .. actors.Skip(1)];
        }
        BattleDefinition baseline = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 0, 116);
        string? stackProfile = Environment.GetEnvironmentVariable("MEMORIA_FF8_MONSTER_STACK_PROFILE");
        if (stackProfile is not null)
        {
            NativeBattleExecutionStack selected = JsonSerializer.Deserialize<NativeBattleExecutionStack>(File.ReadAllBytes(stackProfile))!;
            selected.Validate();
            NativeBattleRetainedMemory retained = baseline.NativeRetainedMemory!;
            baseline = baseline with { NativeRetainedMemory = retained with { EffectMemory = retained.EffectMemory! with { ExecutionStack = selected } } };
        }
        return baseline with
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            GuardianForces = [],
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeFieldBattleMode = 0,
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects(action.MagicId == 0 ? [] : [action.MagicId]),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
    }
}
