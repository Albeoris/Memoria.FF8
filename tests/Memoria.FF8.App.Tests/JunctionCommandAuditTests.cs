using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionCommandAuditTests
{
    [Fact]
    public void EmptyCommandsOnlyNeedAttentionWhenDistinctAdditionsAreAvailable()
    {
        JunctionCommandConfiguration configuration = new("character-0", ["21", "0", "0"], ["21"]);
        Assert.False(JunctionCommandAudit.Inspect(configuration).NeedsAttention);
        JunctionCommandAttention attention = JunctionCommandAudit.Inspect(configuration with { AvailableCommands = ["21", "23"] });
        Assert.Equal(1, attention.EquippedCount);
        Assert.Equal(new[] { 1, 2 }, attention.ActionableEmptySlots);
    }

    [Fact]
    public void FillPreservesAllExistingCommandsAndUsesDeterministicDistinctLearnedChoices()
    {
        JunctionCommandConfiguration configuration = new("character-0", ["0", "24", "0"], ["22", "24", "23", "21", "21"]);
        Assert.Equal(new[] { "21", "24", "23" }, JunctionCommandAudit.ProposeFill(configuration));
        Assert.Equal(new[] { "0", "24", "0" }, configuration.Commands);
        Assert.Equal(new[] { "22", "24", "0" }, JunctionCommandAudit.ProposeFill(configuration with { AvailableCommands = ["24", "22"] }));
    }

    [Fact]
    public void AcknowledgmentUsesOnlyCommandCapabilitiesAndRelevantAttackMode()
    {
        JunctionCommandAudit audit = new();
        JunctionCommandConfiguration configuration = new("character-0", ["21", "0", "0"], ["21", "23"]);
        Assert.True(audit.RequiresReview(configuration));
        audit.Acknowledge(configuration);
        audit.BeginVisit();
        Assert.False(audit.RequiresReview(configuration with { AvailableCommands = ["23", "21", "21"] }));
        Assert.True(audit.RequiresReview(configuration with { Commands = ["23", "0", "0"] }));
        Assert.True(audit.RequiresReview(configuration with { AvailableCommands = ["21", "23", "24"] }));
        Assert.True(audit.RequiresReview(configuration with { SmartMagic = true }));
        audit.ResetSession();
        Assert.True(audit.RequiresReview(configuration));
    }

    [Fact]
    public void ExitReviewIncludesSelectedAndAffectedRecordsOnly()
    {
        JunctionCommandAudit audit = new();
        JunctionCommandConfiguration selected = new("character-0", ["0", "0", "0"], ["21"]);
        JunctionCommandConfiguration donor = selected with { RecordId = "character-1" };
        JunctionCommandConfiguration untouched = selected with { RecordId = "character-2" };
        audit.MarkAffected(donor.RecordId);
        Assert.Equal(new[] { "character-0", "character-1" }, audit.Review(selected.RecordId, [selected, donor, untouched]).Select(attention => attention.RecordId));
        audit.Acknowledge(donor);
        Assert.Single(audit.Review(selected.RecordId, [selected, donor, untouched]));
        audit.BeginVisit();
        Assert.Single(audit.Review(selected.RecordId, [selected, donor, untouched]));
    }

    [Fact]
    public void SmartMagicChecksEquippedDependenciesWithoutRequiringSpecificBuild()
    {
        JunctionCommandConfiguration configuration = new("character-0", ["24", "22", "25"], ["21", "22", "23", "24", "25"], true);
        Assert.True(JunctionCommandAudit.Inspect(configuration).MissingSmartMagicDependency);
        Assert.False(JunctionCommandAudit.Inspect(configuration with { SmartMagic = false }).NeedsAttention);
        Assert.False(JunctionCommandAudit.Inspect(configuration with { Commands = ["23", "22", "25"] }).NeedsAttention);
        Assert.Throws<ArgumentException>(() => JunctionCommandAudit.Inspect(configuration with { Commands = ["21"] }));
    }

    [Fact]
    public void SessionIdentityKeepsAcknowledgmentsAcrossReopeningAndClearsOnLoad()
    {
        JunctionCommandAudit audit = new();
        JunctionCommandConfiguration configuration = new("character-0", ["0", "0", "0"], ["21"]);
        audit.SetSession("session-one");
        audit.Acknowledge(configuration);
        audit.SetSession("session-one");
        Assert.False(audit.RequiresReview(configuration));
        audit.SetSession("session-two");
        Assert.True(audit.RequiresReview(configuration));
    }
}
