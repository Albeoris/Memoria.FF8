using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyRemovalRepeatTests
{
    [Fact]
    public void RandomPartyEjectionUsesThreeDistinctOriginalLivingOwners()
    {
        NativeCharacterVital[] records = Records();
        Assert.True(ScenarioDiscovery.TrySelectEnemyRemovalRepeatParty(Eject(), 201, records, new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker), blocker);
        Assert.Equal(new byte[] { 0, 1, 3 }, party);
        Assert.All(records, record => Assert.Equal(new NativeHpWord(100, 100), record.HitPoints));
        Assert.Equal(3, party!.Select(row => records[row].CharacterId).Distinct().Count());
    }

    [Fact]
    public void MissingOrKoCompanionsAreNotInventedOrRevived()
    {
        NativeCharacterVital[] records = Records();
        for (int row = 1; row < records.Length; row++)
        {
            records[row] = records[row] with { HitPoints = new(0, 0), Status = 1 };
        }
        Assert.False(ScenarioDiscovery.TrySelectEnemyRemovalRepeatParty(Eject(), 201, records, new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.StartsWith("REMOVAL_REPEAT_PARTY_PRECONDITION:", blocker);
    }

    [Fact]
    public void UnconditionalFullPartyEjectionIsAnExplicitNonrepeatableNativeOutcome()
    {
        BattleNativeAction action = Eject() with { AttackType = 1, Statuses1 = 0x4010000, Hit = 255, StatusAttack = 255 };
        Assert.False(ScenarioDiscovery.TrySelectEnemyRemovalRepeatParty(action, 204, Records(), new byte[] { 0, 1, 3 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.StartsWith("NON_REPEATABLE_NATIVE_OUTCOME:", blocker);
        Assert.False(ScenarioDiscovery.TrySelectEnemyRemovalRepeatParty(action with { MagicId = 7, ActionId = 9, KernelIndex = 9 }, 204, Records(), new byte[] { 0, 1, 3 }, out _, out string? equivalent));
        Assert.Equal(blocker, equivalent);
    }

    [Theory]
    [InlineData(0x10000u, 255, 255)]
    [InlineData(0x4010000u, 70, 255)]
    [InlineData(0x4010000u, 255, 100)]
    public void OtherFullPartyRemovalProfilesRemainUnprovedInsteadOfBeingLabeledGuaranteed(uint statuses, byte hit, byte chance)
    {
        BattleNativeAction action = Eject() with { Statuses1 = statuses, Hit = hit, StatusAttack = chance };
        Assert.False(ScenarioDiscovery.TrySelectEnemyRemovalRepeatParty(action, 204, Records(), new byte[] { 0, 1, 3 }, out _, out string? blocker));
        Assert.StartsWith("REMOVAL_REPEAT_PRECONDITION:", blocker);
    }

    [Fact]
    public void OrdinaryDamageDoesNotAcquireRemovalCompanionsAndEjectionNeverAcquiresHpRepair()
    {
        Assert.True(ScenarioDiscovery.TrySelectEnemyRemovalRepeatParty(Eject() with { Statuses1 = 0, Power = 40 }, 201, Records(), new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.Null(blocker);
        JsonObject recipe = new() { ["driver"] = new JsonObject() };
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Eject() with { Power = 40, Statuses0 = 1 }, 201, new byte[] { 0, 1, 3 }, _ => throw new InvalidOperationException("Removed owners cannot use HP repair."));
        Assert.Empty(recipe["driver"]!.AsObject());
    }

    [Fact]
    public void InstalledRemovalPreparationUsesSameLevelActualPartyMaximaAndRemovesOutcomeRepairDeclarations()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for genuine removal companions.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        byte[] originalParty = initial.CombatParty.ToArray();
        NativeBattleExperienceDefinition experience = NativeBattleExperienceDefinitionReader.Read(kernel);
        Assert.True(ScenarioDiscovery.TrySelectEnemyRemovalRepeatParty(Eject(), 201, initial.Records, initial.CombatParty, out byte[]? party, out string? blocker), blocker);
        JsonObject recipe = new()
        {
            ["fixture"] = new JsonObject { ["characters"] = new JsonArray(new JsonObject { ["row"] = 0, ["character_id"] = 0, ["stocks"] = new JsonArray(), ["guardian_forces"] = new JsonArray(0) }) },
            ["driver"] = new JsonObject { ["repairs"] = new JsonArray(), ["terminal_hold"] = new JsonObject() }
        };
        NativeCharacterVitals prepared = ScenarioDiscovery.PrepareEnemyRemovalRepeatParty(recipe, initial, experience, party!, 24);
        JsonObject declared = JsonNode.Parse(recipe.ToJsonString())!.AsObject();
        Assert.Equal(party, prepared.CombatParty);
        Assert.Equal(3, declared["fixture"]!["characters"]!.AsArray().Count);
        foreach (JsonNode? node in declared["fixture"]!["characters"]!.AsArray())
        {
            int row = node!["row"]!.GetValue<int>();
            Assert.Equal(prepared.Records[row].CharacterId, node["character_id"]!.GetValue<byte>());
            Assert.Equal(24, NativeStatDefinitions.Level(prepared.Records[row].Experience, prepared.Definitions.Characters[prepared.Records[row].CharacterId]));
            Assert.Equal(prepared.GetMagicMaximumHitPoints(row), node["hit_points"]!.GetValue<ushort>());
        }
        Assert.False(recipe["driver"]!.AsObject().ContainsKey("repairs"));
        Assert.False(recipe["driver"]!.AsObject().ContainsKey("terminal_hold"));
        Assert.Equal(originalParty, initial.CombatParty);
    }

    private static NativeCharacterVital[] Records() => Enumerable.Range(0, 8).Select(row => new NativeCharacterVital((byte)row, new(100, 100), 0, null!, 0, 0, 0, [], [], 0, 0)).ToArray();

    private static BattleNativeAction Eject() => new(8, 72, 3, 72)
    {
        MagicId = 174,
        AttackType = 2,
        Power = 0,
        HitCount = 1,
        TargetFlags = null,
        AttackFlags = 1,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 255,
        Statuses0 = 0,
        Statuses1 = 0x10000,
        Hit = 70,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
