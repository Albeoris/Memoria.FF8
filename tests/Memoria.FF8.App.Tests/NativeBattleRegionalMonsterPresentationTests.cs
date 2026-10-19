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
            Assert.Single(battle.Events, item => item.Kind == "Windup" && item.ActorId == "monster");
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "monster");
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", model, level, slot, ability.Command, ability.Id, ability.Sequence, native.MagicId, callbacks, peakChangedRgb, restoredLive, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output!, "before-boundary.battle.json"), JsonSerializer.Serialize(prior));
            BattleSnapshot failed = battle.Capture();
            File.WriteAllText(Path.Combine(output!, "failed-live.battle.json"), JsonSerializer.Serialize(failed));
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", model, level, slot, ability.Command, ability.Id, ability.Sequence, native.MagicId, callbacks, peakChangedRgb, restoredLive, selected, produced, error = error.ToString() }));
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
        BattleEnemyPlacement placement = formation.Slots.First(enemy => enemy.ModelId == model);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(model).Project(level);
        bool allies = action.AttackType is 3 or 4 or 5 or 6 or 13 or 14;
        byte selector = allies ? (byte)200 : (byte)201;
        // Explicit one-action component. The selected original bank, sequence and kernel record are unchanged.
        // The short driver selects this bank slot once in INIT; it does not stand in for original story AI coverage.
        NativeBattleAiDefinition driver = installedAi with { Scripts = [[4, selector, 12, checked((byte)slot), 0], [0], [0], [0], [0]] };
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 30000, CurrentHp = 30000, NativeStatusResistances = assets.ReadPlayerNativeStatusData(game.ReadNewGameCharacterVitals(), 0).Resistances.ToArray(), NativeDeathCount = 0 },
            new() { Id = "monster", Name = assets.ReadEnemyName(model), ModelId = model, SceneSlot = placement.Slot, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = Math.Max(1, stats.MaximumHp / 2), Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = 1, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeStatusResistances = assets.ReadEnemyNativeStatusData(model).Resistances.ToArray(), Information246 = assets.ReadEnemyNativeInformation246(model), NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(model), NativeAi = driver }
        ];
        BattleDefinition baseline = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 0, 116);
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
