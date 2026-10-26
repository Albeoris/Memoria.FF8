using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class LocalizationSourceFieldContextsTests
{
    [Fact]
    public void RepeatedIdenticalStaticCallsShareOneTaggedBaseRecord()
    {
        var result = LocalizationSourceFieldContexts.Expand(Row(), Inventory(Call(8, "AASK", [0, 0, 2, 4, 3, -1, 50, 80]), Call(20, "AASK", [0, 0, 2, 4, 3, -1, 50, 80])));
        Assert.Equal("{X50Y80F2L4B3C-1}Body", Assert.Single(result).Text);
        var bindings = JsonSerializer.Deserialize<LocalizationSourceContextBinding[]>(result[0].Metadata["Context Bindings"]!)!;
        Assert.Equal([8, 20], Assert.Single(bindings).OperationIndices);
        Assert.Null(bindings[0].VariantKey);
    }

    [Fact]
    public void DistinctPacketsCreateVariantsGroupedByParametersAndStableAcrossCallOrdering()
    {
        FieldCompatibilityContext first = Call(8, "AMES", [0, 0, 10, 20]);
        FieldCompatibilityContext repeated = Call(10, "AMESW", [0, 0, 10, 20]);
        FieldCompatibilityContext second = Call(20, "AMES", [0, 0, 30, 40]);
        var result = LocalizationSourceFieldContexts.Expand(Row(), Inventory(first, repeated, second));
        var reordered = LocalizationSourceFieldContexts.Expand(Row(), Inventory(second, repeated, first));
        Assert.Equal(3, result.Count);
        Assert.Equal("Body", result[0].Text);
        Assert.Equal(result.Select(row => row.Key), reordered.Select(row => row.Key));
        Assert.Equal(1, result.Count(row => row.Text == "{X10Y20}Body"));
        Assert.Equal(1, result.Count(row => row.Text == "{X30Y40}Body"));
        Assert.All(result.Skip(1), row => Assert.StartsWith("$en_test_0000_ctx_", row.Key));
        var bindings = JsonSerializer.Deserialize<LocalizationSourceContextBinding[]>(result[0].Metadata["Context Bindings"]!)!;
        Assert.Equal(2, bindings.Length);
        Assert.Contains(bindings, binding => binding.OperationIndices.SequenceEqual([8, 10]));
    }

    [Fact]
    public void DynamicChoicesRemainUnmaterializedAndStatefulWindowsStayNative()
    {
        FieldCompatibilityContext dynamic = Call(8, "ASK", [0, 0, null, 3, 0, -1]) with { Dynamic = true };
        FieldCompatibilityContext size = Call(2, "WINSIZE", [0, 10, 20, 300, 80]) with { IsMessage = false, MessageId = null };
        var result = LocalizationSourceFieldContexts.Expand(Row(), Inventory(size, dynamic));
        Assert.Equal("Body", Assert.Single(result).Text);
        var bindings = JsonSerializer.Deserialize<LocalizationSourceContextBinding[]>(result[0].Metadata["Context Bindings"]!)!;
        Assert.True(Assert.Single(bindings).Dynamic);
        Assert.Equal("", bindings[0].Prefix);
        var message = LocalizationSourceFieldContexts.Expand(Row(), Inventory(size, Call(8, "MES", [0, 0])));
        Assert.Equal("Body", Assert.Single(message).Text);
    }

    private static LocalizationSourceEntry Row() => new("Fields", "test", "$en_test_0000", "Body", "en", new Dictionary<string, string?> { ["Source Ordinal"] = "0", ["JSM SHA256"] = "abc" });
    private static FieldCompatibilityContext Call(int operation, string opcode, int?[] operands) => new(new(0, 0, 0, operation, operation, operation * 4), opcode, operands.ToImmutableArray(), 0, true, operands.Any(value => value is null), [], null);
    private static FieldCompatibilityInventory Inventory(params FieldCompatibilityContext[] calls) => FieldCompatibilityAudit.Prepare(new("test", "en", "test", null, [new(0, "Body")])).Inventory with { JsmSha256 = "abc", Contexts = calls.ToImmutableArray() };
}
