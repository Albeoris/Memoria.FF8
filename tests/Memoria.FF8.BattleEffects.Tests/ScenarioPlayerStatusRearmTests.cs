using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioPlayerStatusRearmTests
{
    [Fact]
    public void UnavoidableSelfBerserkUsesExplicitPinnedColdPairWithoutStatusOrClockOverrides()
    {
        string root = Path.Combine(Path.GetTempPath(), "native-status-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Data", "lang-en"));
        try
        {
            File.WriteAllText(Path.Combine(root, "FF8_EN.exe"), "test executable pin");
            foreach (string family in new[] { "main", "battle", "magic" })
            {
                foreach (string extension in new[] { "fs", "fi", "fl" })
                {
                    File.WriteAllText(Path.Combine(root, "Data", "lang-en", family + "." + extension), family + extension);
                }
            }
            JsonObject recipe = SelfRecipe();
            recipe["driver"]!["repairs"] = new JsonArray(new JsonObject { ["actor_id"] = "party-0" });
            Assert.True(ScenarioDiscovery.TryConfigurePlayerStatusExecutionProfile(recipe, Action(), root, out string? blocker), blocker);
            DiagnosticFreshEntryDeclaration declaration = recipe["driver"]!["fresh_entry"]!.Deserialize<DiagnosticFreshEntryDeclaration>(RecordJson.Options)!;
            declaration.Validate();
            Assert.Equal(DiagnosticFreshEntryDeclaration.SingleUse, declaration.Kind);
            Assert.Equal(10, declaration.SourceFiles.Length);
            Assert.Contains(declaration.SourceFiles, pin => pin.RelativePath == "Data/lang-en/magic.fs");
            Assert.All(declaration.SourceFiles, pin => Assert.Equal(64, pin.Sha256.Length));
            Assert.Empty(recipe["driver"]!["repairs"]!.AsArray());
            Assert.Null(recipe["driver"]!["terminal_hold"]);
            Assert.Equal("party-0", recipe["driver"]!["input"]!["target_id"]!.GetValue<string>());
            Assert.Null(recipe["fixture"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MissingActualArchivesBlocksFreshPairWithoutPartlyChangingRecipe()
    {
        JsonObject recipe = SelfRecipe();
        string before = recipe.ToJsonString();
        Assert.False(ScenarioDiscovery.TryConfigurePlayerStatusExecutionProfile(recipe, Action(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), out string? blocker));
        Assert.StartsWith("FRESH_ENTRY_SOURCE_PRECONDITION:", blocker);
        Assert.Equal(before, recipe.ToJsonString());
    }

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void ResistibleOrHostileProfilesKeepOrdinarySameBattleProtocol(byte chance, bool self)
    {
        JsonObject recipe = SelfRecipe();
        if (!self)
        {
            recipe["driver"]!["input"]!["target_id"] = "enemy-0";
        }
        string before = recipe.ToJsonString();
        Assert.True(ScenarioDiscovery.TryConfigurePlayerStatusExecutionProfile(recipe, Action() with { StatusAttack = chance }, Path.GetTempPath(), out string? blocker), blocker);
        Assert.Equal(before, recipe.ToJsonString());
    }

    private static JsonObject SelfRecipe() => JsonNode.Parse("""{"driver":{"input":{"actor_id":"party-0","target_id":"party-0"}}}""")!.AsObject();

    [Fact]
    public void MadRushDeclaresItsActualRecipientsStatusRemovalBetweenExecutions()
    {
        JsonObject recipe = new() { ["driver"] = new JsonObject() };
        ScenarioDiscovery.DeclareOrdinaryPlayerStatusRearm(recipe, Action(), true, "party-0", 486);
        BattleDiagnosticCombatRepair repair = recipe["driver"]!["repairs"]![0]!.Deserialize<BattleDiagnosticCombatRepair>(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
        repair.Validate();
        Assert.Equal("party-0", repair.ActorId);
        Assert.Equal(486, repair.HitPoints);
        Assert.Equal((ushort)32, repair.RemoveStatuses0);
        Assert.Equal(34u, repair.RemoveStatuses1);
        Assert.False(repair.Revive);
        Assert.Null(recipe["driver"]!["terminal_hold"]);
    }

    [Theory]
    [InlineData(false, 2, 32, 34u)]
    [InlineData(true, 3, 32, 34u)]
    [InlineData(true, 2, 0, 0u)]
    [InlineData(true, 2, 1, 0u)]
    [InlineData(true, 2, 4, 0u)]
    [InlineData(true, 2, 0, 0x10000u)]
    [InlineData(true, 2, 0, 0x80000000u)]
    public void HostileCurativeAndUnsupportedStatusOwnersRetainTheirExistingRecipe(bool party, byte type, ushort saved, uint battle)
    {
        JsonObject recipe = new() { ["driver"] = new JsonObject() };
        string before = recipe.ToJsonString();
        ScenarioDiscovery.DeclareOrdinaryPlayerStatusRearm(recipe, Action() with { AttackType = type, Statuses0 = saved, Statuses1 = battle }, party, "party-0", 486);
        Assert.Equal(before, recipe.ToJsonString());
    }

    private static BattleNativeAction Action() => new(24, 3, 10, 3)
    {
        MagicId = 273,
        AttackType = 2,
        Power = 0,
        HitCount = 1,
        TargetFlags = 8,
        AttackFlags = 35,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 255,
        Statuses0 = 32,
        Statuses1 = 34,
        Hit = null,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
