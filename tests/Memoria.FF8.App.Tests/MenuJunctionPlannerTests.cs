using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MenuJunctionPlannerTests
{
    [Theory]
    [InlineData("Elem-Atk", 9)]
    [InlineData("ST-Atk", 10)]
    [InlineData("Elem-Def 1", 11)]
    [InlineData("ST-Def 1", 15)]
    public void Effect_candidates_and_direct_assignment_share_original_mask_eligibility(string slot, int index)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = Setup(assets, false);
        MenuJunctionPlanner planner = Planner(source, catalog);
        BattleNativePlayerStatRules rules = catalog.ReadStatRules(source, 0);
        string original = JsonSerializer.Serialize(source);
        int eligible = 0;
        int rejected = 0;
        foreach (int spell in Enumerable.Range(1, 32))
        {
            bool expected = NativeJunctionMagicEligibility.HasContribution(rules.MagicRecords[spell].AsSpan(), index);
            Assert.Equal(expected, planner.IsMagicCandidate("character-0", spell, slot));
            foreach (string action in new[] { "junction-magic", "junction-magic-reassign" })
            {
                MenuJunctionPlan plan = planner.Plan(new(action, $"magic-0-{spell}", "character-0", slot));
                Assert.Equal(expected, plan.UnavailableReason is null);
                if (!expected)
                {
                    Assert.Empty(plan.Changes);
                    Assert.False(plan.HasChanges);
                }
            }
            if (expected)
            {
                eligible++;
            }
            else
            {
                rejected++;
            }
        }
        Assert.True(eligible > 0);
        Assert.True(rejected > 0);
        Assert.True(planner.IsMagicCandidate("character-0", 0, slot));
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Optimizer_preserves_pins_effects_other_owners_and_reserved_byte(bool shared)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = Setup(assets, shared);
        source = source.SetJunctionMagic(0, 0, 1);
        for (int slot = 9; slot < 19; slot++)
        {
            source = source.SetJunctionMagic(0, slot, slot + 11);
        }
        source = source.AcquireGuardianForce(1).TeachGuardianForceAbility(1, 1).ToggleGuardianForceJunction(1, 1).SetJunctionMagic(1, 0, 31);
        source.Records[0].Junction.MagicJunctions[19] = 30;
        Assert.Equal(30, source.Records[0].Junction.MagicJunctions[19]);
        string original = JsonSerializer.Serialize(source);
        MenuJunctionPlan plan = Planner(source, catalog).Optimize("character-0", "Attack", new HashSet<string> { "HP", "Luck" });
        Assert.Null(plan.UnavailableReason);
        Assert.True(plan.HasChanges);
        NativeCharacterVitals result = Apply(source, plan);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(source.Records[0].Junction.MagicJunctions[0], result.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(0, result.Records[0].Junction.MagicJunctions[8]);
        Assert.Equal(source.Records[0].Junction.MagicJunctions.Skip(9), result.Records[0].Junction.MagicJunctions.Skip(9));
        Assert.Equal(source.Records[0].Junction.Commands, result.Records[0].Junction.Commands);
        Assert.Equal(source.Records[0].Junction.Abilities, result.Records[0].Junction.Abilities);
        Assert.Equal(source.Records[0].Junction.GuardianForces, result.Records[0].Junction.GuardianForces);
        Assert.Equal(source.Records.Skip(1), result.Records.Skip(1));
        Assert.Equal(JsonSerializer.Serialize(source.MagicSystem), JsonSerializer.Serialize(result.MagicSystem));
        Assert.All(plan.Changes, change => Assert.Equal("character-0", change.CharacterId));
        Assert.DoesNotContain(plan.Changes, change => change.Category is "GF" or "Command" or "Passive" or "Element" or "Status");
        Assert.DoesNotContain((byte)30, result.Records[0].Junction.MagicJunctions.Take(9));
        if (shared)
        {
            Assert.DoesNotContain((byte)31, result.Records[0].Junction.MagicJunctions.Take(9));
        }
    }

    [Theory]
    [InlineData("Attack")]
    [InlineData("Magic")]
    [InlineData("Defense")]
    public void Optimizer_is_deterministic_does_not_allocate_intrinsic_hit_and_stabilizes(string priority)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = Setup(assets, false);
        MenuJunctionPlan first = Planner(source, catalog).Optimize("character-0", priority, new HashSet<string>());
        Assert.Null(first.UnavailableReason);
        Assert.True(first.HasChanges);
        NativeCharacterVitals result = Apply(source, first);
        Assert.Equal(0, result.Records[0].Junction.MagicJunctions[7]);
        NativeCharacterVitals shuffled = source.DeepCopy();
        Array.Reverse(shuffled.Records[0].MagicInventory);
        MenuJunctionPlan reordered = Planner(shuffled, catalog).Optimize("character-0", priority, new HashSet<string>());
        Assert.Null(reordered.UnavailableReason);
        Assert.Equal(result.Records[0].Junction, Apply(shuffled, reordered).Records[0].Junction);
        MenuJunctionPlan stable = Planner(result, catalog).Optimize("character-0", priority, new HashSet<string>());
        Assert.Null(stable.UnavailableReason);
        Assert.False(stable.HasChanges);
        Assert.Empty(stable.Changes);
    }

    [Fact]
    public void Manual_move_reports_the_hidden_direction_loss_and_keeps_source_immutable()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = Setup(assets, false).SetJunctionMagic(0, 11, 1);
        string original = JsonSerializer.Serialize(source);
        MenuJunctionPlan plan = Planner(source, catalog).Plan(new("junction-magic", "magic-0-1", "character-0", "Elem-Atk"));
        Assert.Null(plan.UnavailableReason);
        Assert.True(plan.HasChanges);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Contains(plan.Changes, change => change.Category == "Magic" && change.Slot == "Elem-Def 1" && change.After == "None");
        Assert.Contains(plan.Changes, change => change.Category == "Magic" && change.Slot == "Elem-Atk" && change.Before == "None");
        Assert.Contains(plan.Changes, change => change.Category == "Element" && change.Slot == "Fire Defense" && change.After == "0%");
        Assert.Contains(plan.Changes, change => change.Category == "Element" && change.Slot == "Fire Attack" && change.Before == "0%");
    }

    [Fact]
    public void Gf_transfer_reports_the_unavailable_reserve_record_and_all_lost_assignments()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = Setup(assets, false).ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(7, 0);
        source = source.SetJunctionMagic(7, 0, 1).SetJunctionAbility(7, 0, 21, true);
        MenuJunctionPlan plan = Planner(source, catalog).Plan(new("junction-gf-reassign", "gf-0", "character-0", "character-7"));
        Assert.Null(plan.UnavailableReason);
        Assert.True(plan.RequiresReview);
        Assert.Contains(plan.Changes, change => change.CharacterId == "character-7" && change.Category == "GF");
        Assert.Contains(plan.Changes, change => change.CharacterId == "character-7" && change.Category == "Magic" && change.Slot == "HP");
        Assert.Contains(plan.Changes, change => change.CharacterId == "character-7" && change.Category == "Command");
        Assert.Contains(plan.Changes, change => change.CharacterId == "character-0" && change.Category == "GF");
        Assert.Equal(8, plan.Before.Characters.Count);
    }

    [Fact]
    public void Gf_transfer_does_not_report_preferred_shared_caster_changes_as_character_access_changes()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = FullBank(assets, true) with { CombatParty = [0, 4, 255] };
        source.Records[4] = source.Records[4] with { StatBonuses = Enumerable.Repeat((byte)10, source.Records[4].StatBonuses.Length).ToArray() };
        MenuJunctionPlan optimized = Planner(source, catalog).Optimize("character-0", "Magic", new HashSet<string>());
        Assert.Null(optimized.UnavailableReason);
        Assert.True(optimized.HasChanges);
        source = Apply(source, optimized);
        MenuJunctionPlan plan = Planner(source, catalog).Plan(new("junction-gf-reassign", "gf-0", "character-4", "character-0"));
        Assert.Null(plan.UnavailableReason);
        Assert.True(plan.HasChanges);
        MenuMagic[] unbound = plan.Before.Magic.Where(magic => magic.CharacterId == "character-1" && magic.OwnerCharacterId is null).ToArray();
        Assert.NotEmpty(unbound);
        foreach (MenuMagic magic in unbound)
        {
            MenuMagic projected = plan.After.Magic.Single(value => value.Id == magic.Id);
            Assert.NotEmpty(magic.Caster);
            Assert.NotEmpty(projected.Caster);
            Assert.NotEqual(magic.Caster, projected.Caster);
            Assert.Equal(magic.Availability, projected.Availability);
        }
        Assert.DoesNotContain(plan.Changes, change => change.Category == "Access");
        Assert.All(plan.Changes, change => Assert.Contains(change.CharacterId, new[] { "character-0", "character-4" }));
        NativeCharacterVitals result = Apply(source, plan);
        foreach (int row in new[] { 1, 2, 3, 5, 6, 7 })
        {
            Assert.Equal(source.Records[row], result.Records[row]);
        }
    }

    [Fact]
    public void Shared_magic_preview_still_reports_actual_casting_availability_changes()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = FullBank(assets, true).ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(7, 0).SetJunctionMagic(7, 0, 1);
        source.Records[7] = source.Records[7] with { MemberFlags = 0 };
        MenuJunctionPlan plan = Planner(source, catalog).Plan(new("junction-gf-reassign", "gf-0", "character-0", "character-7"));
        Assert.Null(plan.UnavailableReason);
        MenuMagic before = plan.Before.Magic.Single(magic => magic.Id == "magic-0-1");
        MenuMagic after = plan.After.Magic.Single(magic => magic.Id == before.Id);
        Assert.NotEqual(before.Availability, after.Availability);
        Assert.Contains(plan.Changes, change => change.CharacterId == "character-0" && change.Category == "Access" && change.Slot == before.Name);
    }

    [Fact]
    public void Fill_preserves_equipped_commands_and_preview_is_one_reversible_transaction()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = Setup(assets, false).SetJunctionAbility(0, 1, 21, true);
        MenuJunctionPlanner planner = Planner(source, catalog);
        MenuJunctionPlan plan = planner.FillEmptyCommands("character-0");
        Assert.Null(plan.UnavailableReason);
        Assert.Equal(new[] { "20", "21", "22" }, plan.After.Characters.Single(character => character.Id == "character-0").CommandIds);
        NativeCharacterVitals result = Apply(source, plan);
        MenuJunctionPlan reverse = Planner(result, catalog).Reverse(plan);
        Assert.Null(reverse.UnavailableReason);
        Assert.Equal(source.Records[0].Junction, Apply(result, reverse).Records[0].Junction);
        NativeCharacterVitals changed = result.WithSavedHitPoints(0, 1);
        Assert.False(Planner(changed, catalog).IsCurrent(plan));
        Assert.NotNull(Planner(changed, catalog).Reverse(plan).UnavailableReason);
        Assert.Throws<InvalidOperationException>(() => Apply(changed, plan));
    }

    [Fact]
    public void Missing_rules_disable_preview_instead_of_inventing_values()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals source = Setup(assets, false);
        MenuJunctionPlan plan = Planner(source, null).Optimize("character-0", "Attack", new HashSet<string>());
        Assert.False(plan.HasChanges);
        Assert.NotNull(plan.UnavailableReason);
    }

    [Fact]
    public void Explicit_removal_reviews_and_reverses_saved_bindings_outside_the_editable_rows()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = Setup(assets, false);
        source.Records[0].Junction.MagicJunctions[19] = 1;
        source.Records[0] = source.Records[0] with { Junction = source.Records[0].Junction with { Commands = 24u << 24 } };
        MenuJunctionPlan plan = Planner(source, catalog).Plan(new("junction-off", TargetId: "character-0", Value: "All"));
        Assert.Null(plan.UnavailableReason);
        Assert.True(plan.HasChanges);
        Assert.Equal(NativeJunctionEditOperation.RemoveCharacterJunctions, plan.Operation);
        Assert.Contains(plan.Changes, change => change.Category == "Magic" && change.Slot == "Additional saved magic binding");
        Assert.Contains(plan.Changes, change => change.Category == "Command" && change.Slot == "Additional saved command binding");
        NativeCharacterVitals result = Apply(source, plan);
        Assert.Equal(0, result.Records[0].Junction.MagicJunctions[19]);
        Assert.Equal(0u, result.Records[0].Junction.Commands);
        MenuJunctionPlan undo = Planner(result, catalog).Reverse(plan);
        Assert.Null(undo.UnavailableReason);
        Assert.True(undo.IsUndo);
        Assert.Equal(source.Records[0].Junction, Apply(result, undo).Records[0].Junction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Junction_projection_reuses_unchanged_rows_and_reads_each_needed_rule_once(bool shared)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = FullBank(assets, shared);
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = source };
        Dictionary<int, int> reads = [];
        BattleNativePlayerStatRules Read(NativeCharacterVitals vitals, int row)
        {
            reads[row] = reads.GetValueOrDefault(row) + 1;
            return catalog.ReadStatRules(vitals, row);
        }
        MenuData before = LiveMenuData.BuildJunctionSnapshot(state, null, catalog, null, readRules: row => Read(source, row));
        Assert.Equal(8, reads.Count);
        Assert.All(reads.Values, count => Assert.Equal(1, count));
        NativeCharacterVitals intended = source.SetJunctionMagic(0, 1, 1);
        NativeCharacterVitals result = NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended));
        reads.Clear();
        MenuData after = LiveMenuData.BuildJunctionSnapshot(state with { CharacterVitals = result }, null, catalog, null, before, new HashSet<int> { 0 }, row => Read(result, row));
        Assert.All(reads.Values, count => Assert.Equal(1, count));
        Assert.Equal(shared ? 3 : 1, reads.Count);
        Assert.All(reads.Keys, row => Assert.Contains(row, shared ? new[] { 0, 1, 4 } : new[] { 0 }));
        foreach (MenuCharacter character in before.Characters.Where(character => character.Id != "character-0"))
        {
            Assert.Same(character, after.Characters.Single(projected => projected.Id == character.Id));
        }
        Assert.Same(Assert.Single(before.GuardianForces), Assert.Single(after.GuardianForces));
        if (!shared)
        {
            Assert.Same(before.Magic.First(magic => magic.CharacterId == "character-1"), after.Magic.First(magic => magic.CharacterId == "character-1"));
        }
        Assert.Empty(after.Items);
        Assert.All(after.Magic, magic => Assert.Empty(magic.RecoveryTargetScores));
    }

    [Fact]
    public void Distinct_full_bank_previews_keep_authoritative_effects_and_shared_casting_impacts()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = FullBank(assets, true);
        MenuJunctionPlanner planner = Planner(source, catalog);
        string original = JsonSerializer.Serialize(source);
        List<double> distinct = [];
        List<double> repeated = [];
        MenuJunctionPlan[] plans = new MenuJunctionPlan[56];
        for (int spell = 1; spell <= 56; spell++)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            MenuJunctionPlan plan = planner.Plan(new("junction-magic", $"magic-0-{spell}", "character-0", "Mag"));
            distinct.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Null(plan.UnavailableReason);
            plans[spell - 1] = plan;
            MenuCharacter projected = plan.After.Characters.Single(character => character.Id == "character-0");
            BattleNativePlayerStatRules rules = catalog.ReadStatRules(plan.Result, 0);
            Assert.Equal(LiveMenuData.Statistics(rules, plan.Result.Records[0], plan.Result.GetMagicInventory(0)), projected.Stats);
            MenuJunctionEffects effects = LiveMenuData.JunctionEffects(rules, plan.Result.Records[0], plan.Result.GetMagicInventory(0));
            Assert.Equal(effects.Elements, projected.JunctionEffects!.Elements);
            Assert.Equal(effects.Statuses, projected.JunctionEffects.Statuses);
        }
        for (int spell = 1; spell <= 56; spell++)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            MenuJunctionPlan repeatedPlan = planner.Plan(new("junction-magic", $"magic-0-{spell}", "character-0", "Mag"));
            repeated.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Same(plans[spell - 1], repeatedPlan);
        }
        MenuJunctionPlan last = plans[^1];
        for (int spell = 1; spell <= 56; spell++)
        {
            int? caster = last.Result.ResolveFieldMagicCaster(0, spell, row => catalog.ReadStatRules(last.Result, row));
            int? owner = MagicJunctionPolicy.GetOwner(last.Result, spell);
            foreach (MenuMagic magic in last.After.Magic.Where(magic => magic.SpellId == spell))
            {
                Assert.Equal(owner is int ownerRow ? $"character-{ownerRow}" : null, magic.OwnerCharacterId);
                if (caster is int casterRow)
                {
                    Assert.Contains(last.After.Characters.Single(character => character.Id == $"character-{casterRow}").Name, magic.Caster);
                }
                else
                {
                    Assert.Empty(magic.Caster);
                }
            }
        }
        Assert.Equal(original, JsonSerializer.Serialize(source));
        distinct.Sort();
        repeated.Sort();
        Console.WriteLine($"56 distinct spell previews: median {distinct[28]:F2} ms, p95 {distinct[53]:F2} ms, maximum {distinct[55]:F2} ms; repeated median {repeated[28]:F3} ms.");
    }

    private static NativeCharacterVitals FullBank(GameAssets assets, bool shared)
    {
        NativeCharacterVitals source = Setup(assets, shared);
        source = source with { CombatParty = [0, 1, 4], MagicSystem = source.MagicSystem with { RecordZeroExtension = Enumerable.Range(33, 24).Select(spell => new NativeMagicStock((byte)spell, 100)).ToArray() } };
        foreach (int row in new[] { 0, 1, 4 })
        {
            source.Records[row] = source.Records[row] with { MemberFlags = (ushort)(source.Records[row].MemberFlags | 1) };
        }
        return source;
    }

    private static MenuJunctionPlanner Planner(NativeCharacterVitals source, LiveMenuCatalog? catalog) => new(new GameState { Scene = SceneKind.Field, CharacterVitals = source }, catalog: catalog);

    private static NativeCharacterVitals Apply(NativeCharacterVitals source, MenuJunctionPlan plan) => NativeJunctionMenuInput.Apply(source, NativeJunctionMenuProjection.PlayerAction(plan.Action));

    private static NativeCharacterVitals Setup(GameAssets assets, bool shared)
    {
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        foreach (int ability in Enumerable.Range(1, 24))
        {
            source = source.TeachGuardianForceAbility(0, ability);
        }
        source = source.ToggleGuardianForceJunction(0, 0);
        for (int row = 0; row < 8; row++)
        {
            source.Records[row] = source.Records[row] with { MagicInventory = Enumerable.Range(1, 32).Select(spell => new NativeMagicStock((byte)spell, 100)).ToArray() };
        }
        return MagicModeCoordinator.Preview(source, new GameplayPreferences { SharedMagicStock = shared }).Vitals;
    }
}
