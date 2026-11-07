using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.Tests;

public sealed class FreshEntryDiscoveredRecipeRoundtripTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Every_selected_runnable_discovered_recipe_matches_actual_host_typed_serialization()
    {
        string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_RECIPE_PREFLIGHT_MANIFEST");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(selected), "Select the external discovered cases manifest for headless recipe preflight.");
        string manifestPath = Path.GetFullPath(selected!);
        byte[] manifestBytes = ReadBounded(manifestPath, 16 * 1024 * 1024);
        using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
        JsonElement cases = manifest.RootElement.ValueKind == JsonValueKind.Array ? manifest.RootElement : manifest.RootElement.GetProperty("cases");
        int checkedRecipes = 0;
        int ignoredCases = 0;
        int failureCount = 0;
        List<object> failures = [];
        int mismatchRecipes = 0;
        bool mismatchPathsTruncated = false;
        Dictionary<MismatchPath, MismatchGroup> mismatchGroups = [];
        foreach (JsonElement entry in cases.EnumerateArray())
        {
            if (entry.TryGetProperty("status", out JsonElement status) && status.GetString() != "RUNNABLE")
            {
                ignoredCases++;
                continue;
            }
            string id = entry.GetProperty("id").GetString() ?? throw new InvalidDataException("A runnable case lacks its identity.");
            checkedRecipes++;
            try
            {
                string recipePath = entry.GetProperty("recipe").GetString() ?? throw new InvalidDataException("A runnable case lacks its recipe path.");
                if (!Path.IsPathFullyQualified(recipePath))
                {
                    recipePath = Path.Combine(Path.GetDirectoryName(manifestPath)!, recipePath);
                }
                byte[] recipeBytes = ReadBounded(recipePath, 4 * 1024 * 1024);
                string hash = Convert.ToHexStringLower(SHA256.HashData(recipeBytes));
                string expected = entry.GetProperty("recipe_sha256").GetString() ?? throw new InvalidDataException("A runnable recipe lacks its discovery hash.");
                if (!string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The recipe differs from its discovery hash.");
                }
                JsonObject submitted = JsonNode.Parse(recipeBytes)!.AsObject();
                string original = submitted.ToJsonString();
                JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(submitted);
                DiagnosticBattleRequest actual = canonical.Deserialize<DiagnosticBattleRequest>(Json) ?? throw new InvalidDataException("The typed host request is null.");
                actual.Validate();
                JsonNode observed = JsonSerializer.SerializeToNode(actual, Json)!;
                if (!JsonNode.DeepEquals(canonical, observed))
                {
                    mismatchRecipes++;
                    MismatchPath[] differences = DifferencePaths(canonical, observed, "").Distinct().Take(65).ToArray();
                    mismatchPathsTruncated |= differences.Length > 64;
                    foreach (MismatchPath difference in differences.Take(64))
                    {
                        if (!mismatchGroups.TryGetValue(difference, out MismatchGroup? group))
                        {
                            if (mismatchGroups.Count == 128)
                            {
                                mismatchPathsTruncated = true;
                                continue;
                            }
                            group = new(difference.Path, difference.Difference);
                            mismatchGroups.Add(difference, group);
                        }
                        group.RecipeCount++;
                        if (group.ExampleCaseIds.Count < 3)
                        {
                            group.ExampleCaseIds.Add(id);
                        }
                    }
                    throw new InvalidDataException("Typed host serialization differs at " + string.Join("; ", differences.Take(4).Select(value => value.Path + " (" + value.Difference + ")")));
                }
                if (submitted.ToJsonString() != original || !JsonNode.DeepEquals(canonical, FreshEntryRecipeCanonicalization.Canonicalize(canonical)))
                {
                    throw new InvalidDataException("Canonicalization mutated the submitted recipe or is not idempotent.");
                }
            }
            catch (Exception exception)
            {
                failureCount++;
                if (failures.Count < 32)
                {
                    failures.Add(new { id, error = exception.Message.Length > 512 ? exception.Message[..512] : exception.Message });
                }
            }
        }
        object summary = new { scope = "headless-discovered-recipe-typed-roundtrip", manifest_sha256 = Convert.ToHexStringLower(SHA256.HashData(manifestBytes)), checked_recipes = checkedRecipes, ignored_cases = ignoredCases, failure_count = failureCount, mismatch_recipe_count = mismatchRecipes, mismatch_path_groups = mismatchGroups.Values.OrderByDescending(group => group.RecipeCount).ThenBy(group => group.Path, StringComparer.Ordinal).ThenBy(group => group.Difference, StringComparer.Ordinal).ToArray(), mismatch_paths_truncated = mismatchPathsTruncated, failures, gameplay_or_gpu_execution = false };
        string serialized = JsonSerializer.Serialize(summary, Json);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RECIPE_PREFLIGHT_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            if (!Path.IsPathFullyQualified(output))
            {
                throw new InvalidDataException("Select an absolute external preflight summary output path.");
            }
            using FileStream stream = new(output, FileMode.CreateNew);
            JsonSerializer.Serialize(stream, summary, Json);
        }
        Console.WriteLine(serialized);
        Assert.True(checkedRecipes > 0, "The selected manifest has no runnable recipes.");
        Assert.True(failureCount == 0, serialized);
    }

    [Theory]
    [InlineData("enemy_ability", """{"model":1,"scene_slot":0,"level_band":0,"ability_slot":0,"command":8,"action_id":82,"sequence":11,"target_selector":0}""")]
    [InlineData("presentation", """{"signal_path":"signal.json","receipt_directory":"receipts","run_id":"roundtrip"}""")]
    [InlineData("slots_admission", """{"magic_action":51,"maximum_rolls":4,"maximum_seconds":1}""")]
    [InlineData("repairs", """[{"actor_id":"party-0","hit_points":1}]""")]
    [InlineData("prelude", """{"input":{"kind":"Action","actor_id":"party-0","target_id":"enemy-0"},"defeated_model":1,"defeated_scene_slot":0,"death_script_sha256":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","require_native_allocation":true}""")]
    public void Optional_nested_DTO_defaults_match_actual_host_serialization(string owner, string payload)
    {
        JsonObject recipe = JsonNode.Parse("""{"encounter":94,"seed":0,"flags":16,"field_battle_mode":0,"elapsed_counter":0,"driver":{"input":{"kind":"Action","actor_id":"party-0","target_id":"enemy-0"},"expected_effect":2}}""")!.AsObject();
        JsonObject container = owner == "prelude" ? recipe : recipe["driver"]!.AsObject();
        container[owner] = JsonNode.Parse(payload);
        JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(recipe);
        DiagnosticBattleRequest actual = canonical.Deserialize<DiagnosticBattleRequest>(Json)!;
        Assert.True(JsonNode.DeepEquals(canonical, JsonSerializer.SerializeToNode(actual, Json)));
    }

    [Fact]
    public void Mismatch_paths_group_repeated_array_positions_and_retain_other_distinct_paths()
    {
        JsonNode submitted = JsonNode.Parse("""{"items":[{"value":1},{"value":1}]}""")!;
        JsonNode observed = JsonNode.Parse("""{"items":[{"value":2,"extra":false},{"value":2}]}""")!;
        MismatchPath[] differences = DifferencePaths(submitted, observed, "").Distinct().OrderBy(value => value.Path, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "/items/[]/extra", "/items/[]/value" }, differences.Select(value => value.Path).ToArray());
        Assert.Equal(new[] { "property-absent", "value" }, differences.Select(value => value.Difference).ToArray());
    }

    private static byte[] ReadBounded(string path, int maximumBytes)
    {
        if (new FileInfo(path).Length > maximumBytes)
        {
            throw new InvalidDataException("The selected preflight input exceeds its bounded document size.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > maximumBytes)
        {
            throw new InvalidDataException("The selected preflight input grew beyond its bounded document size.");
        }
        return bytes;
    }

    private sealed record MismatchPath(string Path, string Difference);

    private sealed class MismatchGroup(string path, string difference)
    {
        public string Path { get; } = path;
        public string Difference { get; } = difference;
        public int RecipeCount { get; set; }
        public List<string> ExampleCaseIds { get; } = [];
    }

    private static IEnumerable<MismatchPath> DifferencePaths(JsonNode? expected, JsonNode? actual, string path)
    {
        if (JsonNode.DeepEquals(expected, actual))
        {
            yield break;
        }
        if (expected is JsonObject left && actual is JsonObject right)
        {
            foreach (string key in left.Select(pair => pair.Key).Union(right.Select(pair => pair.Key)).Order(StringComparer.Ordinal))
            {
                string childPath = path + "/" + key.Replace("~", "~0").Replace("/", "~1");
                if (!left.ContainsKey(key) || !right.ContainsKey(key))
                {
                    yield return new(childPath, "property-absent");
                    continue;
                }
                foreach (MismatchPath difference in DifferencePaths(left[key], right[key], childPath))
                {
                    yield return difference;
                }
            }
            yield break;
        }
        if (expected is JsonArray leftArray && actual is JsonArray rightArray)
        {
            if (leftArray.Count != rightArray.Count)
            {
                yield return new(path, "array-length");
            }
            for (int index = 0; index < Math.Min(leftArray.Count, rightArray.Count); index++)
            {
                foreach (MismatchPath difference in DifferencePaths(leftArray[index], rightArray[index], path + "/[]"))
                {
                    yield return difference;
                }
            }
            yield break;
        }
        yield return new(path, "value");
    }
}
