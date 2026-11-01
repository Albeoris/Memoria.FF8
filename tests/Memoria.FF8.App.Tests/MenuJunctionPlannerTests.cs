using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MenuJunctionPlannerTests
{
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
