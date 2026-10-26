using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class TerminalHoldPathValidationTests
{
    private static readonly string[] Paths = ["released", "second-held", "first-held", "both-held"];

    [Theory]
    [InlineData("released")]
    [InlineData("second-held")]
    [InlineData("first-held")]
    [InlineData("both-held")]
    public void Exact_observed_hold_paths_preserve_two_executions_and_the_real_repair(string path)
    {
        JsonObject report = Report(path);
        LiveEvidenceVerdict result = Validate(report);
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
        Assert.Contains("CONTROLLED_DEFEAT_HOLD_REQUIRES_SEPARATE_NATURAL_TERMINAL_CHECK", result.DiagnosticIssues);
        Assert.Equal(path == "released", result.DiagnosticIssues.Contains("DEFEAT_HOLD_NOT_TRIGGERED"));
        Assert.Equal(1, report["executions"]![1]!["after"]![0]!["statuses0"]!.GetValue<int>());
    }

    [Fact]
    public void An_unused_armed_policy_has_no_held_callback_budget_to_expire()
    {
        JsonObject report = Report("released");
        report["declared_terminal_hold"]!["maximum_callbacks"] = 1;
        Assert.Equal("PASS", Validate(report).Verdict);
    }

    public static IEnumerable<object[]> OwnerForgeries()
    {
        foreach (string path in Paths)
        {
            foreach (string corruption in new[] { "sequence", "lifecycle_id", "restore_count", "reset_count", "native_result", "physical", "release_state", "release_early", "release_not_drained", "declaration", "expired", "unknown" })
            {
                yield return [path, corruption];
            }
        }
    }

    [Theory]
    [MemberData(nameof(OwnerForgeries))]
    public void Every_path_rejects_owner_terminal_and_release_forgeries(string path, string corruption)
    {
        JsonObject report = Report(path);
        JsonObject release = report["terminal_holds"]!.AsArray().Last()!.AsObject();
        switch (corruption)
        {
            case "sequence":
                release["sequence"] = 3;
                break;
            case "lifecycle_id":
                release["lifecycle_id"] = Guid.NewGuid().ToString();
                break;
            case "restore_count":
            case "reset_count":
            case "native_result":
                release[corruption] = 1;
                break;
            case "physical":
                release["party"]![0]!["native_index"] = 3;
                break;
            case "release_state":
                release["party"]![0]!["current_hp"] = 1;
                break;
            case "release_early":
                release["callback"] = 21;
                break;
            case "release_not_drained":
                release["drained"] = false;
                break;
            case "declaration":
                report["declared_terminal_hold"]!["party_actor_ids"]![0] = "unrepaired-party";
                break;
            case "expired":
                release["kind"] = "expired";
                break;
            case "unknown":
                release["kind"] = "invented";
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData("second-held")]
    [InlineData("first-held")]
    [InlineData("both-held")]
    public void Each_triggered_path_enforces_the_hold_budget_and_the_mapped_execution(string path)
    {
        JsonObject expired = Report(path);
        expired["declared_terminal_hold"]!["maximum_callbacks"] = 1;
        if (path == "second-held")
        {
            expired["terminal_holds"]!.AsArray().Last()!["callback"] = 23;
        }
        Assert.NotEqual("PASS", Validate(expired).Verdict);
        JsonObject wrongExecution = Report(path);
        wrongExecution["terminal_holds"]![0]!["callback"] = path == "second-held" ? 11 : 21;
        Assert.Contains("TERMINAL_HOLD_TIMING_OR_REPAIR_INVALID", Validate(wrongExecution).Errors);
        JsonObject missingDefeat = Report(path);
        int execution = path == "second-held" ? 1 : 0;
        missingDefeat["executions"]![execution]!["after"]![0]!["statuses0"] = 0;
        Assert.Contains("TERMINAL_HOLD_DEFEAT_NOT_OBSERVED", Validate(missingDefeat).Errors);
    }

    [Theory]
    [InlineData("first-held")]
    [InlineData("both-held")]
    public void Actual_rearm_is_bound_to_the_exact_repair_rng_state_and_callback(string path)
    {
        foreach (string corruption in new[] { "random", "party", "callback", "drained" })
        {
            JsonObject report = Report(path);
            JsonObject rearm = report["terminal_holds"]![1]!.AsObject();
            switch (corruption)
            {
                case "random":
                    rearm["random"]!["battle"]!["lane"] = 1;
                    break;
                case "party":
                    rearm["party"]![0]!["statuses0"] = 1;
                    break;
                case "callback":
                    rearm["callback"] = 17;
                    break;
                case "drained":
                    rearm["drained"] = false;
                    break;
            }
            Assert.NotEqual("PASS", Validate(report).Verdict);
        }
    }

    [Fact]
    public void Held_first_and_held_second_without_a_real_rearm_is_not_a_supported_path()
    {
        JsonObject report = Report("both-held");
        report["terminal_holds"]!.AsArray().RemoveAt(1);
        Assert.Contains("TERMINAL_HOLD_EVIDENCE_INVALID", Validate(report).Errors);
    }

    private static JsonObject Report(string path)
    {
        JsonObject report = (JsonObject)typeof(LiveEvidenceValidationTests).GetMethod("TerminalHoldReport", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        JsonArray source = report["terminal_holds"]!.AsArray();
        int[] selected = path switch
        {
            "released" => [3],
            "second-held" => [2, 3],
            "first-held" => [0, 1, 3],
            "both-held" => [0, 1, 2, 3],
            _ => throw new ArgumentException("Unknown test path.", nameof(path))
        };
        JsonArray receipts = new(selected.Select(index => source[index]!.DeepClone()).ToArray());
        if (path == "released")
        {
            receipts[0]!["sequence"] = 0;
        }
        else if (path is "second-held" or "first-held")
        {
            foreach (JsonNode? receipt in receipts)
            {
                receipt!["sequence"] = 1;
            }
        }
        report["terminal_holds"] = receipts;
        return report;
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(document.RootElement);
    }
}
