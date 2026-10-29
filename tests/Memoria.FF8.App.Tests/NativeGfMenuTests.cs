using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGfMenuTests
{
    [Fact]
    public void AuthenticLearningTablesOfferOnlyUnlockedTargetsAndKeepProgressAcrossSelection()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        Assert.Equal("Abilityx3", names.AbilityName(18));
        Assert.Equal("Abilityx4", names.AbilityName(19));
        NativeGuardianForceAbility[] available = original.GuardianForceAbilities(0).Where(ability => !ability.Learned).ToArray();
        Assert.True(available.Length >= 2);
        NativeGuardianForceAbility first = available.First(ability => ability.Ability != 0 && ability.RequiredPoints > 1);
        NativeGuardianForceAbility other = available.First(ability => ability.Ability != first.Ability && ability.Ability != 0);
        string preserved = JsonSerializer.Serialize(original);
        NativeCharacterVitals selected = original.SelectGuardianForceLearning(0, first.Ability).AwardGuardianForceAbilityPoints(0, 1);
        Assert.Equal(1, selected.GuardianForceAbilities(0).Single(ability => ability.Ability == first.Ability).Progress);
        selected = selected.SelectGuardianForceLearning(0, other.Ability).SelectGuardianForceLearning(0, first.Ability);
        Assert.Equal(1, selected.GuardianForceAbilities(0).Single(ability => ability.Ability == first.Ability).Progress);
        NativeCharacterVitals learned = selected.AwardGuardianForceAbilityPoints(0, ushort.MaxValue);
        Assert.True(learned.GuardianForces[0].LearnedBank.Has(first.Ability));
        Assert.Equal(255, learned.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == first.Ability).EarnedPoints);
        Assert.NotEqual(first.Ability, learned.GuardianForces[0].Learning);
        Assert.Throws<InvalidOperationException>(() => learned.SelectGuardianForceLearning(0, first.Ability));
        Assert.Equal(preserved, JsonSerializer.Serialize(original));
        JsonElement snapshot = JsonSerializer.SerializeToElement(learned);
        NativeCharacterVitals restored = snapshot.Deserialize<NativeCharacterVitals>()!;
        restored.Validate();
        Assert.True(JsonElement.DeepEquals(snapshot, JsonSerializer.SerializeToElement(restored)));
        System.Text.Json.Nodes.JsonObject missing = System.Text.Json.Nodes.JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        missing.Remove("MenuDefinitions");
        Assert.Throws<JsonException>(() => missing.Deserialize<NativeCharacterVitals>());
        missing = System.Text.Json.Nodes.JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        missing["GuardianForces"]![0]!.AsObject().Remove("AbilityProgress");
        Assert.Throws<JsonException>(() => missing.Deserialize<NativeCharacterVitals>());
    }

    [Fact]
    public void LearningPrerequisiteExclusionForgottenMaskAndExpandedLearnedListPreserveTheNativePasses()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        // Isolated rule component; no fixture state or journal is saved as a research checkpoint.
        NativeGuardianForceLearningRule[] rules = Enumerable.Repeat(new NativeGuardianForceLearningRule(255, 255, 0), 21).ToArray();
        rules[0] = new NativeGuardianForceLearningRule(1, 255, 20);
        rules[1] = new NativeGuardianForceLearningRule(101, 2, 39);
        rules[2] = new NativeGuardianForceLearningRule(1, 255, 40);
        rules[3] = new NativeGuardianForceLearningRule(100, 255, 41);
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Learning = vitals.MenuDefinitions.Learning.SetItem(0, rules.ToImmutableArray()), Templates = default, SourceTemplates = default } };
        NativeGuardianForceVital[] gfs = vitals.GuardianForces.ToArray();
        // Exercise an imported native record before a managed progression ledger is created.
        gfs[0] = gfs[0] with { Experience = 0, LearnedBank = new NativeGuardianForceLearnedBank(1UL << 20, 0), ForgottenAbilities = 0, Progression = null, LastProjection = null };
        vitals = vitals with { GuardianForces = gfs };
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = vitals.ResolveGuardianForceTemplates();
        vitals = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Templates = templates, SourceTemplates = templates } };
        Assert.Contains(vitals.GuardianForceAbilities(0), entry => entry.Ability == 39 && !entry.Learned);
        Assert.DoesNotContain(vitals.GuardianForceAbilities(0), entry => entry.Ability == 41);
        gfs[0] = gfs[0] with { ForgottenAbilities = 1u << 1 };
        Assert.DoesNotContain(vitals.GuardianForceAbilities(0), entry => entry.Ability == 39);
        gfs[0] = gfs[0] with { ForgottenAbilities = 0, LearnedBank = new NativeGuardianForceLearnedBank((1UL << 20) | (1UL << 40), 0) };
        Assert.DoesNotContain(vitals.GuardianForceAbilities(0), entry => entry.Ability == 39);
        gfs[0] = gfs[0] with { LearnedBank = new NativeGuardianForceLearnedBank((1UL << 24) - 1, 0) };
        IReadOnlyList<NativeGuardianForceAbility> expanded = vitals.GuardianForceAbilities(0);
        Assert.Equal(23, expanded.Count(entry => entry.Learned));
        Assert.Contains(expanded, entry => entry.Ability == 39 && !entry.Learned);
        Assert.Contains(expanded, entry => entry.Ability == 40 && !entry.Learned);
        Assert.Equal(25, expanded.Count);
    }

    [Fact]
    public void EncounterPassivesUnionOnlyTheCombatPartyAndIncludeKnockedOutMembers()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        Assert.Equal("Enc-Half", names.AbilityName(80));
        Assert.Equal("Enc-None", names.AbilityName(81));
        NativeCharacterVital[] rows = vitals.Records.ToArray();
        rows[0] = rows[0] with { Junction = rows[0].Junction with { Abilities = 80 } };
        rows[1] = rows[1] with { HitPoints = new NativeHpWord(0, 0), Status = 1, Junction = rows[1].Junction with { Abilities = 81 } };
        rows[2] = rows[2] with { Junction = rows[2].Junction with { Abilities = 82 } };
        vitals = vitals with { Records = rows, CombatParty = [0, 1, 255] };
        Assert.Equal(12, vitals.GetEncounterAbilityFlags());
        Assert.Equal(4, (vitals with { CombatParty = [0, 255, 255] }).GetEncounterAbilityFlags());
    }

    [Fact]
    public void LiveMenuJunctionAndCommandSelectionMutateTheExplicitNativeCharacterAndSlot()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        List<PlayerAction> inputs = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }, catalog: names), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            inputs.Add(input);
            vitals = NativeJunctionMenuInput.Apply(vitals, input);
        });
        controller.Open("junction-gf");
        controller.Navigate(MenuInput.Confirm);
        Assert.Contains(0, vitals.Records[0].Junction.GuardianForceAssignments);
        Assert.Equal(PlayerActionKind.JunctionGuardianForce, Assert.Single(inputs).Kind);
        controller.Open("junction-command");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-command-choice", controller.ScreenId);
        int choice = controller.Entries.ToList().FindIndex(entry => entry.Label == "Magic");
        Assert.True(choice >= 0);
        for (int index = 0; index < choice; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.Navigate(MenuInput.Confirm);
        PlayerAction command = inputs.Last();
        Assert.Equal(PlayerActionKind.SetJunctionCommand, command.Kind);
        Assert.Equal(0, command.Value);
        Assert.Equal(0, command.Quantity);
        Assert.Equal(byte.Parse(command.Text!), (byte)vitals.Records[0].Junction.Commands);
        Assert.Equal("Magic", controller.Data.Characters.First(character => character.Id == "character-0").Commands[1]);
        Assert.DoesNotContain(controller.Entries, entry => entry.Label == "Revive");
        Assert.Throws<InvalidOperationException>(() => vitals.ToggleGuardianForceJunction(1, 0));
        Assert.Throws<InvalidOperationException>(() => vitals.SetJunctionAbility(0, 0, 38, true));
        vitals = vitals.ToggleGuardianForceJunction(0, 0);
        Assert.Equal(0u, vitals.Records[0].Junction.Commands);
    }

    [Fact]
    public void LearningAndPassiveScreensUseCanonicalIdsAndRefreshAfterTheGfLearnsItsTarget()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        NativeGuardianForceAbility target = vitals.GuardianForceAbilities(0).First(entry => !entry.Learned && entry.Ability is >= 39 and < 83);
        List<PlayerAction> inputs = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }, catalog: names), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            inputs.Add(input);
            vitals = NativeJunctionMenuInput.Apply(vitals, input);
        });
        controller.Open("gf-learn");
        Assert.Contains(controller.Entries, entry => !entry.Enabled && vitals.GuardianForceAbilities(0).Any(ability => ability.Learned && names.AbilityName(ability.Ability) == entry.Label));
        int selected = controller.Entries.ToList().FindIndex(entry => entry.Id == target.Ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
        for (int index = 0; index < selected; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(PlayerActionKind.SelectGuardianForceLearning, Assert.Single(inputs).Kind);
        Assert.Equal(target.Ability, vitals.GuardianForces[0].Learning);
        vitals = vitals.AwardGuardianForceAbilityPoints(0, ushort.MaxValue);
        controller.Open("junction-gf");
        controller.Navigate(MenuInput.Confirm);
        controller.Open("junction-ability");
        Assert.Equal(2, controller.Entries.Count);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-ability-choice", controller.ScreenId);
        selected = controller.Entries.ToList().FindIndex(entry => entry.Id == target.Ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(selected > 0);
        for (int index = 0; index < selected; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(PlayerActionKind.SetJunctionAbility, inputs.Last().Kind);
        Assert.Equal(target.Ability, (byte)vitals.Records[0].Junction.Abilities);
        Assert.Equal(names.AbilityName(target.Ability), controller.Data.Characters.First(character => character.Id == "character-0").Abilities[0]);
    }

    [Fact]
    public void MagicJunctionMenuRequiresStockAndLearnedSlotsMovesOneSpellAndClampsHpAfterRemoval()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).TeachGuardianForceAbility(0, 1).TeachGuardianForceAbility(0, 2);
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        // Explicit isolated component inputs; no session journal or research snapshot is fabricated.
        NativeCharacterVital[] records = vitals.Records.ToArray();
        NativeMagicStock[] stock = records[0].MagicInventory.ToArray();
        stock[0] = new NativeMagicStock(1, 40);
        records[0] = records[0] with { MagicInventory = stock };
        vitals = vitals with { Records = records };
        ushort unjunctionedMaximum = vitals.Definitions.CharacterMaximum(vitals.Records[0]);
        Assert.Throws<InvalidOperationException>(() => vitals.SetJunctionMagic(0, 2, 1));
        Assert.Throws<InvalidOperationException>(() => vitals.SetJunctionMagic(0, 0, 2));
        List<PlayerAction> inputs = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }, catalog: names), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            inputs.Add(input);
            vitals = NativeJunctionMenuInput.Apply(vitals, input);
        });
        controller.Open("junction-magic");
        Assert.True(controller.Entries[0].Enabled);
        Assert.True(controller.Entries[1].Enabled);
        Assert.False(controller.Entries[2].Enabled);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-stock", controller.ScreenId);
        controller.Navigate(MenuInput.Down);
        Assert.Equal("Fire", controller.SelectedEntry!.Label);
        Assert.Equal(40, Assert.Single(controller.Data.Magic, magic => magic.Id == controller.SelectedEntry.Action!.SubjectId).Quantity);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(PlayerActionKind.SetJunctionMagic, Assert.Single(inputs).Kind);
        Assert.Equal(0, inputs[0].Quantity);
        Assert.Equal(1, vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(40, vitals.Records[0].MagicInventory[0].Count);
        Assert.True(vitals.Definitions.CharacterMaximum(vitals.Records[0]) > unjunctionedMaximum);
        records = vitals.Records.ToArray();
        records[0] = records[0] with { HitPoints = new NativeHpWord((ushort)(unjunctionedMaximum + 1), (ushort)(unjunctionedMaximum + 1)) };
        vitals = vitals with { Records = records };
        controller.Open("junction-magic");
        controller.SelectEntry("junction-magic", "Str");
        Assert.Equal("Str", controller.SelectedEntry!.Id);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-stock", controller.ScreenId);
        controller.SelectEntry("junction-stock", "magic-0-1");
        Assert.Equal("Fire", controller.SelectedEntry!.Label);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(PlayerActionKind.SetJunctionMagic, inputs.Last().Kind);
        Assert.Equal(1, inputs.Last().Quantity);
        Assert.Equal(0, vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(1, vitals.Records[0].Junction.MagicJunctions[1]);
        Assert.Equal(new NativeHpWord(unjunctionedMaximum, unjunctionedMaximum), vitals.Records[0].HitPoints);
        Assert.Equal(40, vitals.Records[0].MagicInventory[0].Count);
        vitals = vitals.SetJunctionMagic(0, 1, 0);
        Assert.Equal(0, vitals.Records[0].Junction.MagicJunctions[1]);
        Assert.Equal(40, vitals.Records[0].MagicInventory[0].Count);
    }

    [Theory]
    [InlineData(PlayerActionKind.JunctionGuardianForce, 8, "0", null)]
    [InlineData(PlayerActionKind.SetJunctionCommand, 0, "20", 3)]
    [InlineData(PlayerActionKind.SetJunctionAbility, 0, "39", null)]
    [InlineData(PlayerActionKind.SelectGuardianForceLearning, 16, "20", null)]
    [InlineData(PlayerActionKind.SetJunctionMagic, 0, "1", 19)]
    [InlineData(PlayerActionKind.SetJunctionMagic, 0, "57", 0)]
    public void MalformedMenuInputsAreRejectedBeforeApplyingNativeChanges(PlayerActionKind kind, int row, string choice, int? slot)
    {
        PlayerAction action = new(kind, row, choice, Quantity: slot);
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(action));
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original GF/kernel initialization resources are required.");
        return new GameAssets(directory);
    }
}
