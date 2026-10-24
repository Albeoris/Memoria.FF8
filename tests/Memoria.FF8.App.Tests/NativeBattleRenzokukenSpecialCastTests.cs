using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRenzokukenSpecialCastTests
{
    [Fact]
    public void Installed_spider_Renzokuken_completes_target_animation_camera_and_standalones()
    {
        Installed_special_target_Renzokuken_completes_target_animation_camera_and_standalones(159, 92);
    }

    [Theory]
    [InlineData((ushort)161, 91)]
    [InlineData((ushort)161, 131)]
    [InlineData((ushort)334, 67)]
    [InlineData((ushort)335, 77)]
    [InlineData((ushort)336, 90)]
    [InlineData((ushort)337, 117)]
    [InlineData((ushort)339, 112)]
    [InlineData((ushort)340, 126)]
    [InlineData((ushort)341, 84)]
    [InlineData((ushort)342, 124)]
    [InlineData((ushort)343, 122)]
    public void Installed_special_target_Renzokuken_completes_target_animation_camera_and_standalones(ushort effectId, int model)
    {
        RunSpecial(effectId, model, false);
    }

    [Fact]
    public void Installed_physical_then_spider_Renzokuken_preserves_approach_and_return()
    {
        RunSpecial(159, 92, true);
    }

    private static void RunSpecial(ushort effectId, int model, bool physicalFirst)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, effectId, model);
        const byte crisis = 4;
        int contacts = definition.NativeRenzokukenResources.Single(resource => resource.MagicId == effectId).Timeline.InputThresholds.Length;
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Bind(battle);
        BattleScenePresentation scene = new(assets, definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "renzokuken-special-" + effectId + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Renzokuken evidence: " + output);
        BattleSnapshot before = battle.Capture();
        try
        {
            for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand("party"));
            if (physicalFirst)
            {
                battle.QueueAction("party", "enemy", BattleAbility.Attack, 0);
                bool approach = false;
                for (int callback = 0; callback < 4096; callback++)
                {
                    battle.Tick();
                    NativeBattleSequenceState? state = battle.NativeGraphics!.Models[0].SequenceState;
                    if (!approach && state is { } sequence && (sequence.Header36 != 0 || sequence.Header38 != 0 || sequence.Header40 != 0))
                    {
                        CompareNext("physical-approach");
                        approach = true;
                    }
                    if (battle.ActiveAction is null && battle.QueuedActions.Count == 0)
                    {
                        break;
                    }
                }
                Assert.True(approach);
                Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
                for (int callback = 0; callback < 4096 && !battle.CanChoosePlayerCommand("party"); callback++)
                {
                    battle.AdvanceTurn();
                    battle.Tick();
                }
                Assert.True(battle.CanChoosePlayerCommand("party"));
            }
            int priorDamage = battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
            double initialDistance = Distance();
            //A declared component seed selects the selected original contact resource and no optional finisher.
            NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
            {
                candidate = candidate.Draw(out byte resource).Draw(out byte finisher);
                return definition.Combatants.Single(actor => actor.Id == "enemy").NativeRenzokukenMagicIds[crisis + 1 + resource % 2] == effectId && !NativeBattleRenzokukenRules.HasFinisher(crisis, finisher);
            });
            BattleSnapshot ready = battle.Capture();
            const uint transitionColor = 0x00312617;
            battle = BattleSession.Restore(ready with { NativeLimits = [new("party", crisis)], Random = random, NativeStage = ready.NativeStage! with { ModelColors = ready.NativeStage.ModelColors! with { Color40 = transitionColor } } });
            Bind(battle);
            const int actorIndex = 0;
            uint? shadowColor = battle.NativeGraphics!.Models[actorIndex].Color44;
            Assert.NotNull(shadowColor);
            uint interpolatedColor = NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(transitionColor, 1 << 9, 0x4060F0);
            bool interpolatedCurrentStageColor = false;
            Publish("before-command");
            battle.QueueNativePlayerCommand("party", "enemy", 5, 0);
            CompareNext("queued");
            for (int callback = 0; callback < 4096 && battle.GetNativeRenzokuken("party") is null; callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.NotNull(battle.GetNativeRenzokuken("party"));
            Assert.Equal(effectId, battle.GetNativeRenzokuken("party")!.MagicId);
            bool selected = false;
            bool registeredCamera = false;
            bool capturedMidpoint = false;
            bool targetAnimated = false;
            bool checkedApproach = false;
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                Assert.Equal(shadowColor, battle.NativeGraphics!.Models[actorIndex].Color44);
                interpolatedCurrentStageColor |= battle.NativeGraphics.Models[actorIndex].Color40 == interpolatedColor;
                selected |= battle.NativeGraphics!.OriginalEffect?.Binding.Effect == effectId;
                targetAnimated |= battle.NativeGraphics.Models.Any(target => target.ResourceMagicId == effectId && target.ResourceRenzokukenTargetBank == 0 && target.SequenceState is not null);
                if (battle.NativeGraphics.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == effectId)
                {
                    Assert.NotNull(battle.NativeEffectMemory!.Textures!.Animation);
                    Assert.Equal(JsonSerializer.Serialize(battle.NativeEffectMemory.Textures.Animation), JsonSerializer.Serialize(battle.NativeCamera!.ControlResource));
                    Assert.Null(battle.NativeCamera.ControlStagingSource);
                    registeredCamera = true;
                }
                if (physicalFirst && !checkedApproach && battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy") > priorDamage)
                {
                    Assert.True(Distance() < initialDistance, "Renzokuken must approach its physical target before the first damage contact.");
                    checkedApproach = true;
                }
                if (!capturedMidpoint && battle.NativeGraphics.TriggerWidget?.Input.ContactIndex > 0)
                {
                    CompareNext("midpoint");
                    capturedMidpoint = true;
                }
                if (selected && battle.GetNativeRenzokuken("party") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics.OriginalEffect is { ReturnedDescriptor: 0 })
                {
                    Assert.True(capturedMidpoint);
                    Assert.True(targetAnimated, "The special native target must execute its own embedded animation sequence.");
                    Assert.All(battle.NativeGraphics.Models, target => Assert.Null(target.ResourceRenzokukenTargetBank));
                    Assert.True(registeredCamera);
                    Assert.True(interpolatedCurrentStageColor, "Renzokuken must interpolate the current stage transition color into model DWORD40 without changing the shadow command in DWORD44.");
                    Assert.Equal(priorDamage + contacts, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy"));
                    Assert.Null(battle.Capture().NativeTimedAction);
                    Assert.Equal((byte)0, battle.Capture().NativeClock!.ActionPause);
                    CompareNext("completed");
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", battle.Frame, NativeCallback = battle.NativeGraphics.Callback }));
                    return;
                }
            }
            Assert.Fail("Renzokuken did not release its original task and timed command owners within the diagnostic bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Bind(BattleSession session)
        {
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        }

        double Distance()
        {
            NativeBattleActionMatrix matrix = battle.NativeGraphics!.Models[0].GeometryWorldMatrix!;
            int targetIndex = Array.IndexOf(battle.NativeGraphics.ActorIds.ToArray(), "enemy");
            NativeBattleVector destination = battle.NativeGraphics.Models[targetIndex].Transform!.Position28;
            return Math.Pow(matrix.X - destination.X, 2) + Math.Pow(matrix.Z - destination.Z, 2);
        }

        void Publish(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224).ToArray());
        }

        void CompareNext(string name)
        {
            Publish(name);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            Bind(clone);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = battle.Capture();
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next");
        }
    }
    private static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets, ushort effectId, int model)
    {
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 0, effectId);
        BattleEncounter formation = Enumerable.Range(0, 1024).Select(assets.LoadEncounter).FirstOrDefault(candidate => candidate.Enemies.Any(enemy => enemy.ModelId == model)) ?? assets.LoadEncounter(94);
        if (!formation.Enemies.Any(enemy => enemy.ModelId == model))
        {
            formation = formation with { Enemies = formation.Enemies.Select(enemy => enemy with { ModelId = model }).ToArray() };
        }
        int selectedSlot = formation.Enemies.First(enemy => enemy.ModelId == model).Slot;
        //Passive native scripts keep this independent resource component valid across gauge refills.
        BattleCombatantDefinition[] enemies = formation.Enemies.Select(enemy => source.Combatants[1] with
        {
            Id = enemy.Slot == selectedSlot ? "enemy" : "enemy-part-" + enemy.Slot,
            ModelId = enemy.ModelId,
            Name = "Target",
            NativeCombatantFlags = 0x11,
            NativeAi = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []),
            SceneSlot = enemy.Slot,
            NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(enemy.ModelId),
            Information246 = assets.ReadEnemyNativeInformation246(enemy.ModelId),
            NativeStatusResistances = assets.ReadEnemyNativeStatusData(enemy.ModelId).Resistances.ToArray()
        }).ToArray();
        BattleCombatantDefinition[] actors = [source.Combatants[0], .. enemies];
        return source with
        {
            EncounterId = formation.Id,
            Combatants = actors,
            NativeRenzokukenResources = game.ReadNativeRenzokukenResources(),
            //The component retains its supported original stage while using the selected encounter
            //for native target identities, adjacent parts and formation positions.
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation)
        };
    }

}
