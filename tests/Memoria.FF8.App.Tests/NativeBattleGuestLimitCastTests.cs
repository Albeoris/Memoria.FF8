using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuestLimitCastTests
{
    [Fact]
    public void Installed_Ice_Strike228_completes_original_graphics_and_independent_standalones()
    {
        Installed_guest_limit_completes_original_graphics_and_independent_standalones(7, 228);
    }

    [Theory]
    [InlineData(7, 228)]
    [InlineData(8, 261)]
    [InlineData(9, 262)]
    [InlineData(10, 263)]
    public void Installed_guest_limit_completes_original_graphics_and_independent_standalones(int characterId, ushort effectId)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, characterId, effectId);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "limit-" + effectId + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Limit effect evidence: " + output);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        BattleSnapshot before = battle.Capture();
        bool selected = false;
        bool drawn = false;
        bool midpoint = false;
        int captured228Frames = 0;
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
            battle.QueueNativePlayerCommand("party", "enemy", limit.Command, NativeBattleLimitRules.GuestActionId(characterId));
            CompareNext("queued", battle);
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                NativeBattleGraphicsState graphics = battle.NativeGraphics!;
                if (effectId == 228 && NativeBattleCinematicCaptureAssertions.AssertFrame(battle.Capture(), effectId) > 0)
                {
                    captured228Frames++;
                }
                bool active = graphics.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == effectId;
                if (active)
                {
                    selected = true;
                    if (!drawn && graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                    {
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        NativeBattlePrimitiveFrame withoutEffect = graphics.PrimitiveFrame with { LastCommands = graphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(battle.Capture() with { NativeGraphics = graphics with { PrimitiveFrame = withoutEffect } });
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
                    if (effectId == 228)
                    {
                        Assert.True(captured228Frames > 1);
                    }
                    Assert.True(midpoint);
                    Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
                    CompareNext("completed", battle);
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", characterId, effectId, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback, selected, drawn, midpoint }));
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
            if (effectId == 228)
            {
                NativeBattleCinematicCaptureAssertions.AssertFrame(current.Capture(), effectId);
                NativeBattleCinematicCaptureAssertions.AssertFrame(clone.Capture(), effectId);
            }
            Assert.Equal(JsonSerializer.Serialize(current.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(current, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next", current);
        }
    }

    [Fact]
    public void Continue_the_named_limit_effect_standalone_to_its_next_boundary()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone Limit effect snapshot and a new output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = BattleSession.Restore(snapshot);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        BattleSnapshot before = snapshot;
        try
        {
            if (battle.NativeGraphics!.OriginalEffect is null)
            {
                BattleCombatant actor = battle.Combatants.Single(candidate => candidate.Id == "party");
                Assert.Equal(7, actor.ModelId);
                for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand(actor.Id); callback++)
                {
                    before = battle.Capture();
                    battle.Tick();
                }
                Assert.True(battle.CanChoosePlayerCommand(actor.Id));
                //The standalone component explicitly selects its crisis input after native ready registration.
                battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new(actor.Id, 4)] });
                battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                before = battle.Capture();
                NativeBattleRestorationPresentationTests.Publish(output!, "before-command", before, scene.Render(battle, 320, 224).ToArray());
                BattleNativeLimitDefinition limit = actor.NativeLimit ?? throw new InvalidDataException("The selected pre-entry component has no original guest Limit definition.");
                battle.QueueNativePlayerCommand(actor.Id, "enemy", limit.Command, NativeBattleLimitRules.GuestActionId(actor.ModelId));
                before = battle.Capture();
                NativeBattleRestorationPresentationTests.Publish(output!, "queued", before, scene.Render(battle, 320, 224).ToArray());
            }
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                if (Released(battle))
                {
                    NativeBattleRestorationPresentationTests.Publish(output!, "completed-before-host-mode", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
                    battle.SetMode(BattleAtbMode.Normal);
                    if (battle.Outcome == BattleOutcome.Active && battle.Combatants.Where(actor => !actor.IsPlayer).All(actor => !actor.IsAlive))
                    {
                        before = battle.Capture();
                        battle.Tick();
                    }
                    BattleSnapshot completed = battle.Capture();
                    NativeBattleRestorationPresentationTests.Publish(output!, "completed", completed, scene.Render(battle, 320, 224).ToArray());
                    BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(completed))!);
                    battle.Tick();
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(clone, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output!, "completed-next", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
                    File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "passed", Source = source, CompletionHostMode = battle.Mode, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback }));
                    return;
                }
            }
            Assert.Fail("The selected Limit exceeded its diagnostic continuation bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Error = error.ToString(), before.Frame, NativeCallback = before.NativeGraphics?.Callback }));
            throw;
        }
    }

    private static bool Released(BattleSession battle)
    {
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

    internal static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets, int characterId, ushort effectId)
    {
        BattleDefinition original = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 0, 116);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        int row = characterId < 8 ? characterId : 0;
        if (characterId >= 8)
        {
            NativeCharacterVital[] records = vitals.Records.ToArray();
            records[row] = records[row] with { CharacterId = checked((byte)characterId) };
            vitals = vitals with { DreamEnabled = true, Records = records };
        }
        NativeCharacterVital character = vitals.Records[row];
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, row);
        BattleNativeCharacterGrowth growth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics stats = rules.Project(character.MagicInventory, growth);
        int weapon = characterId >= 8 ? vitals.DreamWeapons[characterId - 8] : character.Weapon;
        BattleCombatantDefinition actor = original.Combatants[0] with { Name = "Guest", ModelId = characterId, WeaponId = weapon, EquipmentIds = [], Abilities = [BattleAbility.Attack, BattleAbility.LimitBreak], Level = rules.Level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = stats.Speed, Luck = stats.Luck, Hit = stats.Hit, Evasion = stats.Evasion, ElementDefenses = stats.ElementDefenses.ToArray(), ElementAttack = stats.ElementAttack, ElementAttackPercent = stats.ElementAttackPercent, NativeMagicInventory = character.MagicInventory.ToArray(), NativeLimit = assets.ReadNativeLimitDefinition(characterId), NativeAttack = assets.ReadPlayerNativeAttackData(vitals, row), NativePlayerStatRules = rules, NativeStatusResistances = rules.ProjectStatusResistances(character.MagicInventory).ToArray(), NativeDeathCount = 0, NativeCharacterFlag100 = (character.MemberFlags & 0x100) != 0, NativeGrowth = growth };
        BattleCombatantDefinition[] actors = [actor, original.Combatants[1] with { Information246 = assets.ReadEnemyNativeInformation246(61) }];
        return original with { Combatants = actors, GuardianForces = [], ActionDefinitions = [], NativeLimitsDisabled = false, LimitProgress = game.ReadNewGameLimitProgress(), NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94)), NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([effectId]) };
    }
}
