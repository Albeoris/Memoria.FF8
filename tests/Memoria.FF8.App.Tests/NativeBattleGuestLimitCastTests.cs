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

    [Fact]
    public void Installed_guest261_completes_original_graphics_and_independent_standalones()
    {
        Installed_guest_limit_completes_original_graphics_and_independent_standalones(8, 261);
    }

    [Fact]
    public void Installed_guest262_completes_original_graphics_and_independent_standalones()
    {
        Installed_guest_limit_completes_original_graphics_and_independent_standalones(9, 262);
    }

    [Fact]
    public void Installed_guest263_completes_original_graphics_and_independent_standalones()
    {
        Installed_guest_limit_completes_original_graphics_and_independent_standalones(10, 263);
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
        BattleDefinition definition = CreateDefinition(game, assets, characterId, effectId, out NativeBattleGraphicsDefinition graphicsResources);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "limit-" + effectId + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Limit effect evidence: " + output);
        BattleSession battle = NativeBattleTypedFixture.CreateBattle(definition, BattleAtbMode.TurnBased, graphicsResources);
        BattleScenePresentation scene = new(assets, definition);
        BattleSnapshot before = battle.CaptureForContinuation();
        bool selected = false;
        bool drawn = false;
        bool midpoint = false;
        int captured228Frames = 0;
        int capturedGuestFrames = 0;
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
                NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
                if (effectId is 261 or 262 or 263 && NativeBattleCinematicCaptureAssertions.AssertFrame(battle.Capture(), effectId) > 0)
                {
                    capturedGuestFrames++;
                }
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
                        BattleSnapshot renderSource = battle.CaptureForContinuation();
                        NativeBattleGraphicsState renderGraphics = renderSource.ReadFixturePresentation().NativeGraphics!;
                        NativeBattlePrimitiveFrame withoutEffect = renderGraphics.PrimitiveFrame! with { LastCommands = renderGraphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(renderSource.WithFixturePresentation(facts => facts with { NativeGraphics = renderGraphics with { PrimitiveFrame = withoutEffect } }));
                        byte[] backgroundPixels = new BattleScenePresentation(assets, definition).Render(background, 320, 224).ToArray();
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
                    if (effectId is 261 or 262 or 263)
                    {
                        Assert.True(capturedGuestFrames > 1);
                    }
                    Assert.True(drawn);
                    if (effectId == 228)
                    {
                        Assert.True(captured228Frames > 1);
                    }
                    Assert.True(midpoint);
                    Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
                    CompareNext("completed", battle);
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", characterId, effectId, battle.Frame, NativeCallback = battle.Presentation!.Graphics.Callback, selected, drawn, midpoint }));
                    return;
                }
            }
            Assert.Fail("The selected Limit did not release its original action and task owners within the diagnostic bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", characterId, effectId, before.Frame, NativeCallback = before.ReadFixturePresentation().NativeGraphics?.Callback, Error = error.ToString() }));
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
            if (effectId is 261 or 262 or 263)
            {
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(current.Capture(), effectId);
                _ = NativeBattleCinematicCaptureAssertions.AssertFrame(clone.Capture(), effectId);
            }
            if (effectId == 228)
            {
                NativeBattleCinematicCaptureAssertions.AssertFrame(current.Capture(), effectId);
                NativeBattleCinematicCaptureAssertions.AssertFrame(clone.Capture(), effectId);
            }
            Assert.Equal(JsonSerializer.Serialize(current.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(current, 320, 224).ToArray(), new BattleScenePresentation(assets, definition).Render(clone, 320, 224).ToArray());
            Publish(name + "-next", current);
        }
    }

    private static bool Released(BattleSession battle)
    {
        NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
        return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

    internal static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets, int characterId, ushort effectId, out NativeBattleGraphicsDefinition graphicsResources)
    {
        BattleDefinition original = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 0, 116, out _);
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
        graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94));
        return original with { Combatants = actors, GuardianForces = [], ActionDefinitions = [], NativeLimitsDisabled = false, LimitProgress = game.ReadNewGameLimitProgress(), NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([effectId]) };
    }
}
