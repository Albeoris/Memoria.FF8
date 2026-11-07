using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRinoa86CastTests
{
    [Fact]
    public void Lethal_Cannon_finishes_after_target_resource_release_and_json_restore()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 4, 86);
        BattleNativeAction action = source.NativeActions.Single(action => action.Command == 19 && (action.MagicId & 0x1FF) == 86);
        BattleEncounter formation = assets.LoadEncounter(202);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleCombatantDefinition[] actors = [source.Combatants[0], .. formation.Enemies.Select(placement => source.Combatants[1] with
        {
            Id = "enemy-" + placement.Slot,
            ModelId = placement.ModelId,
            SceneSlot = placement.Slot,
            CurrentHp = 1,
            Information246 = assets.ReadEnemyNativeInformation246(placement.ModelId),
            NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(placement.ModelId),
            NativeStatusResistances = assets.ReadEnemyNativeStatusData(placement.ModelId).Resistances.ToArray()
        })];
        BattleDefinition definition = source with
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl,
            LimitProgress = source.LimitProgress! with { AngeloFlags = checked((byte)(1 << (action.ActionId + 4))) },
            NativeSavedBattleFlags = (byte)(source.NativeSavedBattleFlags!.Value & ~0x30),
            NativeSavedBattleFlagsKnownMask = (byte)(source.NativeSavedBattleFlagsKnownMask | 0x30)
        };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("party"));
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", 4)] });
        battle.QueueNativePlayerCommand("party", actors[1].Id, definition.Combatants[0].NativeLimit!.Command, action.ActionId);
        bool contact = false;
        bool releasedDuringEffect = false;
        for (int callback = 0; callback < 4096; callback++)
        {
            battle.Tick();
            BattleSnapshot snapshot = battle.Capture();
            bool active = snapshot.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: > 0, Binding.Effect: 86 };
            bool killed = snapshot.Combatants.Where(actor => actor.Id != "party").All(actor => actor.CurrentHp == 0);
            bool released = killed && snapshot.NativeGraphics.Models[3].Flags0 == 0;
            if (active && ((!contact && killed) || (!releasedDuringEffect && released)))
            {
                if (released)
                {
                    int row = snapshot.NativeGraphics.Models[3].ActiveResourceRow100!.Value;
                    NativeBattleGraphicsResource original = definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == actors[1].Id).Resource;
                    NativeBattleGraphicsResource retained = NativeBattleEnemyResourceView.Resolve(snapshot.NativeModelCpuResources!, snapshot.NativeFileStaging!, row, original);
                    Assert.NotNull(retained.PoseSource);
                }
                BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
                battle.Tick();
                clone.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                Assert.Equal(new BattleScenePresentation(assets, definition, true).Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
                contact |= killed;
                releasedDuringEffect |= released;
            }
            if (contact && Released(battle))
            {
                Assert.True(releasedDuringEffect, "The target must release its resource row before Cannon completes.");
                Assert.All(battle.Capture().Combatants.Where(actor => actor.Id != "party"), actor => Assert.Equal(0, actor.CurrentHp));
                Assert.Equal(actors.Length - 1, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "party"));
                return;
            }
        }
        Assert.Fail("Lethal Cannon did not release its action and effect owners within the diagnostic bound.");
    }

    [Fact]
    public void Installed_Rinoa86_completes_its_original_body_programs_and_independent_standalones()
    {
        const int characterId = 4;
        const ushort effectId = 86;
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, characterId, effectId);
        BattleNativeAction originalAction = source.NativeActions.Single(action => action.Command == 19 && (action.MagicId & 0x1FF) == effectId);
        Assert.InRange(originalAction.ActionId, (ushort)0, (ushort)3);
        //The independent component declares its learned Combine input and enables the original saved menu branch.
        NativeLimitProgress progress = source.LimitProgress! with { AngeloFlags = checked((byte)(1 << (originalAction.ActionId + 4))) };
        BattleDefinition definition = source with { LimitProgress = progress, NativeSavedBattleFlags = (byte)(source.NativeSavedBattleFlags!.Value & ~0x30), NativeSavedBattleFlagsKnownMask = (byte)(source.NativeSavedBattleFlagsKnownMask | 0x30) };
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "rinoa86-" + effectId + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Limit effect evidence: " + output);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        BattleSnapshot before = battle.Capture();
        bool selected = false;
        bool drawn = false;
        bool midpoint = false;
        bool following = false;
        NativeBattleCameraPose? previousFollowPose = null;
        try
        {
            for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
            {
                before = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.NativeEntryReady);
            Assert.True(battle.CanChoosePlayerCommand("party"));
            //This independent component explicitly selects its crisis input before the first Limit command.
            battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", 4)] });
            Publish("before-command", battle);
            BattleNativeLimitDefinition limit = definition.Combatants[0].NativeLimit!;
            battle.QueueNativePlayerCommand("party", "enemy", limit.Command, originalAction.ActionId);
            CompareNext("queued", battle);
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                NativeBattleGraphicsState graphics = battle.NativeGraphics!;
                bool active = graphics.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == effectId;
                if (active)
                {
                    selected = true;
                    Assert.Equal("FF8_EN:1565C90", battle.NativeCamera!.ControlResource?.Source.Identity);
                    bool followsBody = battle.NativeCamera.Tasks.Any(task => task?.Pose?.Knots.Any(knot => knot.EyeReference == 0xF9 || knot.TargetReference == 0xF9) == true);
                    if (followsBody)
                    {
                        NativeBattleCameraPose pose = battle.NativeCamera.CurrentPose!;
                        if (!following && previousFollowPose is not null && pose != previousFollowPose)
                        {
                            CompareNext("following", battle);
                            following = true;
                        }
                        previousFollowPose = pose;
                    }
                    if (!drawn && graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                    {
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        BattleSnapshot captured = battle.Capture();
                        NativeBattleGraphicsState capturedGraphics = captured.NativeGraphics!;
                        NativeBattlePrimitiveFrame withoutEffect = capturedGraphics.PrimitiveFrame! with { LastCommands = capturedGraphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(captured with { NativeGraphics = capturedGraphics with { PrimitiveFrame = withoutEffect } });
                        byte[] backgroundPixels = new BattleScenePresentation(assets, definition, true).Render(background, 320, 224).ToArray();
                        drawn = Enumerable.Range(0, pixels.Length / 4).Any(index => !pixels.AsSpan(index * 4, 3).SequenceEqual(backgroundPixels.AsSpan(index * 4, 3)));
                    }
                    if (!midpoint && callback >= 60)
                    {
                        CompareNext("midpoint", battle);
                        midpoint = true;
                    }
                }
                if (selected && Released(battle))
                {
                    Assert.True(drawn);
                    Assert.True(midpoint);
                    Assert.True(following, "The original camera must follow the moving effect-owned body.");
                    Assert.Equal((ushort)86, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
                    Publish("completed-before-host-mode", battle);
                    battle.SetMode(BattleAtbMode.Normal);
                    CompareNext("completed", battle);
                    for (int wait = 0; wait < 400 && !battle.CanChoosePlayerCommand("party"); wait++)
                    {
                        battle.Tick();
                    }
                    Assert.True(battle.CanChoosePlayerCommand("party"));
                    battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", 4)] });
                    battle.QueueNativePlayerCommand("party", "enemy", limit.Command, originalAction.ActionId);
                    bool nextActionStarted = false;
                    bool nextActionCompleted = false;
                    for (int wait = 0; wait < 4096; wait++)
                    {
                        battle.Tick();
                        nextActionStarted |= battle.ActiveAction is not null;
                        if (nextActionStarted && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0)
                        {
                            CompareNext("following-cast-completed", battle);
                            nextActionCompleted = true;
                            break;
                        }
                    }
                    Assert.True(nextActionCompleted, "The next Combine action must finish after camera cleanup.");
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", characterId, effectId, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback, selected, drawn, midpoint, HostModeAfterRelease = "Normal", OriginalActionId = originalAction.ActionId }));
                    return;
                }
            }
            Assert.Fail("The selected Limit did not release its original action and task owners within the diagnostic bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", characterId, effectId, before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        void Publish(string name, BattleSession current)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, current.Capture(), scene.Render(current, 320, 224).ToArray());
        }

        void CompareNext(string name, BattleSession current)
        {
            Publish(name, current);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            Assert.Equal(JsonSerializer.Serialize(current.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = current.Capture();
            current.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(current.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(current, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next", current);
        }
    }

    private static bool Released(BattleSession battle)
    {
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

}
