using System.Text;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class EvidenceStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "battle-evidence-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Canonical_records_use_json_envelopes_and_read_through_independent_instances()
    {
        EvidenceStore first = new(directory);
        first.WriteNew("campaign.json", new { schema_version = 1, campaign = "synthetic" });
        first.WriteBytesNew("effects/effect-a/frames.jsonl", Encoding.UTF8.GetBytes("{\"frame\":1}\n"));
        EvidenceStore second = new(directory);
        Assert.Equal("{\"frame\":1}\n", Encoding.UTF8.GetString(second.ReadBytes("effects/effect-a/frames.jsonl")));
        Assert.True(second.Exists("campaign.json"));
        Assert.False(second.Exists("absent.json"));
        Assert.Equal(new[] { "campaign.json", "effects/effect-a/frames.jsonl" }, second.ListPaths(""));
        Assert.Equal(new[] { "effects/effect-a/frames.jsonl" }, second.ListPaths("effects/"));
        Assert.Empty(second.ListPaths("missing/"));
        Assert.False(File.Exists(first.Resolve("campaign.json")));
        Assert.False(Directory.Exists(first.Resolve("effects")));
        Assert.True(File.Exists(first.Resolve("records/campaign.json.record.json")));
        Assert.False(File.Exists(first.Resolve("coordination.json")));
        JsonNode envelope = JsonNode.Parse(File.ReadAllBytes(first.Resolve("records/campaign.json.record.json")))!;
        Assert.Contains("synthetic", envelope["payload_utf8"]!.GetValue<string>());
        Assert.Null(envelope["payload_base64"]);
    }

    [Fact]
    public void Record_keys_normalize_separators_and_inserts_never_replace_even_identical_payloads()
    {
        EvidenceStore store = new(directory);
        byte[] original = [1, 2, 3];
        store.WriteBytesNew("effects\\effect-a\\plan.json", original);
        original[0] = 9;
        Assert.Equal(new byte[] { 1, 2, 3 }, store.ReadBytes("effects/effect-a/plan.json"));
        Assert.Throws<IOException>(() => store.WriteBytesNew("effects/effect-a/plan.json", [1, 2, 3]));
        Assert.Throws<IOException>(() => store.WriteBytesNew("effects/effect-a/plan.json", [4]));
        Assert.Equal(new byte[] { 1, 2, 3 }, store.ReadBytes("effects/effect-a/plan.json"));
        Assert.Throws<FileNotFoundException>(() => store.ReadBytes("missing.json"));
    }

    [Fact]
    public void Tampered_envelope_payload_or_hash_is_rejected_on_read()
    {
        EvidenceStore store = new(directory);
        store.WriteBytesNew("first.json", [1, 2, 3]);
        store.WriteBytesNew("second.json", [4, 5, 6]);
        string firstPath = store.Resolve("records/first.json.record.json");
        JsonNode first = JsonNode.Parse(File.ReadAllBytes(firstPath))!;
        first["payload_utf8"] = "changed";
        File.WriteAllText(firstPath, first.ToJsonString());
        string secondPath = store.Resolve("records/second.json.record.json");
        JsonNode second = JsonNode.Parse(File.ReadAllBytes(secondPath))!;
        second["sha256"] = "broken";
        File.WriteAllText(secondPath, second.ToJsonString());
        Assert.Throws<InvalidDataException>(() => store.ReadBytes("first.json"));
        Assert.Throws<InvalidDataException>(() => store.ReadBytes("second.json"));
    }

    [Fact]
    public void Binary_and_utf8_envelopes_preserve_exact_payload_bytes()
    {
        EvidenceStore store = new(directory);
        byte[] binary = [0xff, 0xfe, 0x80, 0, 1];
        byte[] text = Encoding.UTF8.GetBytes("\uFEFF{\r\n  \"text\": \"π\"\r\n}\n");
        store.WriteBytesNew("binary.bin", binary);
        store.WriteBytesNew("text.json", text);
        Assert.Equal(binary, store.ReadBytes("binary.bin"));
        Assert.Equal(text, store.ReadBytes("text.json"));
        JsonNode envelope = JsonNode.Parse(File.ReadAllBytes(store.Resolve("records/binary.bin.record.json")))!;
        Assert.Null(envelope["payload_utf8"]);
        Assert.Equal(Convert.ToBase64String(binary), envelope["payload_base64"]!.GetValue<string>());
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Concurrent_independent_writers_have_exactly_one_immutable_winner()
    {
        _ = new EvidenceStore(directory);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] writers = Enumerable.Range(0, 6).Select(index => Task.Run(async () =>
        {
            await start.Task;
            try
            {
                new EvidenceStore(directory).WriteBytesNew("same.json", [(byte)index]);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        })).ToArray();
        start.SetResult();
        Assert.Single(await Task.WhenAll(writers), won => won);
        Assert.Single(new EvidenceStore(directory).ReadBytes("same.json"));
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("/absolute.json")]
    [InlineData("effects//plan.json")]
    [InlineData("effects/./plan.json")]
    [InlineData("campaign.json:alternate")]
    [InlineData("NUL.json")]
    public void Invalid_record_keys_are_rejected(string key)
    {
        EvidenceStore store = new(directory);
        Assert.Throws<ArgumentException>(() => store.WriteBytesNew(key, [1]));
        Assert.Throws<ArgumentException>(() => store.ReadBytes(key));
        Assert.Throws<ArgumentException>(() => store.Exists(key));
    }

    [Fact]
    public void Evidence_envelopes_coexist_with_coordinator_state()
    {
        EvidenceStore store = new(directory);
        store.WriteNew("record.json", new BuildFile("synthetic.bin", 0, new string('a', 64)));
        LocalCoordinator coordinator = new(directory);
        CoordinationScope scope = new("campaign", "plan", "pass");
        coordinator.RegisterEffects(scope, ["effect"]);
        Assert.NotNull(coordinator.ClaimNext(scope, "synthetic-worker", ProcessIdentity.Current));
        Assert.Equal("synthetic.bin", new EvidenceStore(directory).Read<BuildFile>("record.json").Path);
        Assert.False(File.Exists(store.Resolve("record.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
