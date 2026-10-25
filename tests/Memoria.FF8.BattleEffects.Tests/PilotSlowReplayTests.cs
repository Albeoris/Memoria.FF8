using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotSlowReplayTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slow-replay-" + Guid.NewGuid().ToString("N"));
    private readonly PilotManifest original;
    private readonly PilotSlowEffectsReport query;
    private readonly string executable;

    public PilotSlowReplayTests()
    {
        Directory.CreateDirectory(root);
        string build = Path.Combine(root, "fixed-build");
        string game = Path.Combine(root, "game");
        Directory.CreateDirectory(build);
        Directory.CreateDirectory(game);
        executable = Path.Combine(build, "host.exe");
        File.WriteAllText(executable, "synthetic fixed host; never launched");
        string recipe = Path.Combine(root, "recipe.json");
        File.WriteAllText(recipe, "{\"seed\":17}");
        PilotCase selected = new("chosen", recipe, EffectId: 116, RecipeSha256: Hash(File.ReadAllBytes(recipe)));
        original = new(1, executable, build, game, Path.Combine(root, "original-output"), new(1000, 1000, 1000, 1000, 1000, 10000, 1000), [selected, selected with { Id = "unrelated" }], "original-build");
        string manifest = Path.Combine(root, "original-manifest.json");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(original, RecordJson.Options);
        File.WriteAllBytes(manifest, bytes);
        PilotSlowCase slow = new(selected, manifest, Hash(bytes), "unused-telemetry", "original-build", [20, 60], 20, "unused-result", JsonSerializer.SerializeToElement(new { ability = "GuardianForce" }));
        query = new(30, 2, 1, 0, 0, 1, [new(116, 20, [slow])], []);
    }

    [Fact]
    public void Replay_preserves_the_exact_invocation_and_budgets_and_selects_only_one_case()
    {
        string output = Path.Combine(root, "fixed-output");
        PilotManifest replay = PilotSlowReplay.Prepare(query, "chosen", executable, output);

        Assert.Equal(original.Cases[0], Assert.Single(replay.Cases));
        Assert.Equal(original.Budgets, replay.Budgets);
        Assert.Equal(original.GameDir, replay.GameDir);
        Assert.Equal(output, replay.OutputRoot);
        Assert.NotEqual(original.BuildFingerprint, replay.BuildFingerprint);
        Assert.Equal(EvidenceStore.HashRecord(EvidenceStore.DescribeFiles(Path.GetDirectoryName(executable)!)), replay.BuildFingerprint);
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Changed_original_inputs_are_rejected(bool manifest)
    {
        PilotSlowCase selected = query.Effects[0].Cases[0];
        File.AppendAllText(manifest ? selected.SourceManifestPath : selected.Case.Recipe!, " ");

        Assert.Throws<InvalidDataException>(() => PilotSlowReplay.Prepare(query, "chosen", executable, Path.Combine(root, "new-output")));
    }

    [Fact]
    public void Unknown_scenario_and_existing_evidence_are_not_replayed()
    {
        Assert.Throws<ArgumentException>(() => PilotSlowReplay.Prepare(query, "unknown", executable, Path.Combine(root, "new-output")));
        string output = Path.Combine(root, "preserved-output");
        Directory.CreateDirectory(output);
        string evidence = Path.Combine(output, "result.json");
        File.WriteAllText(evidence, "preserved");

        Assert.Throws<InvalidDataException>(() => PilotSlowReplay.Prepare(query, "chosen", executable, output));
        Assert.Equal("preserved", File.ReadAllText(evidence));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public void Dispose()
    {
        Directory.Delete(root, true);
    }
}
