using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class FreshEntryPairValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Two_independent_source_bound_single_use_entries_pass_without_claiming_campaign_acceptance()
    {
        using Fixture fixture = new(ActualExecutable());
        foreach (JsonObject entry in fixture.Reports)
        {
            using JsonDocument entryDocument = JsonDocument.Parse(entry.ToJsonString());
            LiveEvidenceVerdict entryVerdict = LiveEvidenceValidation.ValidateFreshEntry(entryDocument.RootElement);
            Assert.True(entryVerdict.Verdict == "PASS", string.Join(";", entryVerdict.Errors));
        }
        var result = FreshEntryPairValidation.ValidateFiles(fixture.Pins());
        Assert.True(result.FunctionalPass, string.Join(";", result.Errors));
        Assert.Equal(2, result.Entries.Length);
        Assert.All(result.Entries, entry => Assert.Single(entry.Executions));
        Assert.False(result.CampaignAcceptance);
        using var report = JsonDocument.Parse(fixture.Reports[0].ToJsonString());
        Assert.Contains(LiveEvidenceValidation.Validate(report.RootElement).Errors, error => error.StartsWith("REPORT_INVALID:", StringComparison.Ordinal));
        Assert.Equal("PASS", LiveEvidenceValidation.ValidateFreshEntry(report.RootElement).Verdict);
    }

    [Fact]
    public void Two_independent_same_callback_post_frame_releases_pass_the_full_file_pair_contract()
    {
        using Fixture fixture = new(ActualExecutable());
        fixture.UsePostFrameRelease();
        foreach (JsonObject report in fixture.Reports)
        {
            JsonObject execution = report["executions"]![0]!.AsObject();
            BattleDiagnosticInvocationRelease release = execution["invocation_release"]!.Deserialize<BattleDiagnosticInvocationRelease>(Json)!;
            BattleDiagnosticInvocationRelease certificate = release.ExecutingCompletion!;
            Assert.Equal(BattleDiagnosticInvocationRelease.GraphicsFrameBoundary, release.Boundary);
            Assert.Equal(certificate.Callback, release.Callback);
            Assert.Equal(certificate.SimulationFrame, release.SimulationFrame);
            Assert.Equal(certificate.GraphicsCallback!.Value + 1, release.GraphicsCallback!.Value);
            Assert.True(release.PacketRemoved);
            Assert.Equal(0, release.Owners.DescriptorPackets);
            Assert.Equal(1, certificate.Owners.DescriptorPackets);
            Assert.True(certificate.Owners.AuxiliaryHead);
            Assert.NotEmpty(certificate.Blockers);
            Assert.Equal<byte>(certificate.CompletionPacket!.Bytes.SetItem(1, 0), release.RemovedCompletionPacket!.Bytes);
            using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
            LiveEvidenceVerdict verdict = LiveEvidenceValidation.ValidateFreshEntry(document.RootElement);
            Assert.True(verdict.Verdict == "PASS", string.Join(";", verdict.Errors));
        }
        FreshEntryPairVerdict pair = FreshEntryPairValidation.ValidateFiles(fixture.Pins());
        Assert.True(pair.FunctionalPass, string.Join(";", pair.Errors));
        Assert.Equal(2, pair.Entries.Length);
        Assert.All(pair.Entries, entry => Assert.Single(entry.Executions));
        Assert.False(pair.CampaignAcceptance);
    }

    [Theory]
    [InlineData("missing-certificate")]
    [InlineData("executing-mode")]
    [InlineData("unknown-mode")]
    [InlineData("descriptor-count")]
    [InlineData("not-removed")]
    [InlineData("certificate-no-blockers")]
    [InlineData("certificate-no-outstanding-owner")]
    [InlineData("certificate-wrong-mode")]
    [InlineData("certificate-already-removed")]
    [InlineData("nested-certificate")]
    [InlineData("retired-command-byte")]
    [InlineData("retired-priority-byte")]
    [InlineData("retired-slot-reused")]
    [InlineData("physical-slot-reused")]
    [InlineData("callback")]
    [InlineData("simulation-frame")]
    [InlineData("lifecycle")]
    [InlineData("graphics-callback-same")]
    [InlineData("graphics-callback-later")]
    [InlineData("certificate-future-time")]
    [InlineData("live-owner")]
    [InlineData("unknown-files")]
    [InlineData("foreign-certificate-dispatch")]
    [InlineData("foreign-header-generation")]
    [InlineData("header-payload")]
    [InlineData("header-data")]
    public void Typed_post_frame_release_rejects_changed_certificate_removed_packet_boundary_and_owner(string fault)
    {
        using Fixture fixture = new();
        fixture.UsePostFrameRelease();
        BattleDiagnosticInvocationRelease baseline = fixture.Reports[0]["executions"]![0]!["invocation_release"]!.Deserialize<BattleDiagnosticInvocationRelease>(Json)!;
        BattleDiagnosticInvocationRelease certificate = baseline.ExecutingCompletion!;
        DiagnosticInvocationReleaseValidation.Validate(baseline, baseline.Dispatch, baseline.LifecycleId);
        BattleDiagnosticInvocationRelease invalid = fault switch
        {
            "missing-certificate" => baseline with { ExecutingCompletion = null },
            "executing-mode" => baseline with { Boundary = BattleDiagnosticInvocationRelease.ExecutingBoundary },
            "unknown-mode" => baseline with { Boundary = "unproved-boundary" },
            "descriptor-count" => baseline with { Owners = baseline.Owners with { DescriptorPackets = 1 } },
            "not-removed" => baseline with { PacketRemoved = false },
            "certificate-no-blockers" => baseline with { ExecutingCompletion = certificate with { Blockers = [] } },
            "certificate-no-outstanding-owner" => baseline with { ExecutingCompletion = certificate with { Owners = certificate.Owners with { AuxiliaryHead = false } } },
            "certificate-wrong-mode" => baseline with { ExecutingCompletion = certificate with { Boundary = BattleDiagnosticInvocationRelease.GraphicsFrameBoundary } },
            "certificate-already-removed" => baseline with { ExecutingCompletion = certificate with { PacketRemoved = true } },
            "nested-certificate" => baseline with { ExecutingCompletion = certificate with { ExecutingCompletion = certificate } },
            "retired-command-byte" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(2, 104) } },
            "retired-priority-byte" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(1, 0x80) } },
            "retired-slot-reused" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(0, 8) } },
            "physical-slot-reused" => baseline with { CompletionPacketSlot = baseline.CompletionPacketSlot + 1 },
            "callback" => baseline with { Callback = baseline.Callback + 1 },
            "simulation-frame" => baseline with { SimulationFrame = baseline.SimulationFrame + 1 },
            "lifecycle" => baseline with { LifecycleId = Guid.NewGuid() },
            "graphics-callback-same" => baseline with { GraphicsCallback = certificate.GraphicsCallback },
            "graphics-callback-later" => baseline with { GraphicsCallback = certificate.GraphicsCallback + 2 },
            "certificate-future-time" => baseline with { ExecutingCompletion = certificate with { Timestamp = baseline.Timestamp + 1 } },
            "live-owner" => baseline with { Owners = baseline.Owners with { AuxiliaryHead = true } },
            "unknown-files" => baseline with { Owners = baseline.Owners with { Files = null } },
            "foreign-certificate-dispatch" => baseline with { ExecutingCompletion = certificate with { Dispatch = certificate.Dispatch with { Serial = certificate.Dispatch.Serial + 1 } } },
            "foreign-header-generation" => baseline with { Dispatch = baseline.Dispatch with { NativeHeaderGeneration = 2 } },
            "header-payload" => baseline with { Header = baseline.Header with { Payload = baseline.Header.Payload.SetItem(4, 22) } },
            "header-data" => baseline with { Header = baseline.Header with { Data = new NativeBattleActionDataReference(1, 2) } },
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(invalid, baseline.Dispatch, baseline.LifecycleId));
    }

    [Theory]
    [InlineData("missing-certificate")]
    [InlineData("descriptor-count")]
    [InlineData("retired-slot-reused")]
    public void Full_file_pair_rejects_post_frame_certificate_and_removed_packet_tampering(string fault)
    {
        using Fixture fixture = new(ActualExecutable());
        fixture.UsePostFrameRelease();
        JsonObject execution = fixture.Reports[1]["executions"]![0]!.AsObject();
        BattleDiagnosticInvocationRelease release = execution["invocation_release"]!.Deserialize<BattleDiagnosticInvocationRelease>(Json)!;
        BattleDiagnosticInvocationRelease invalid = fault switch
        {
            "missing-certificate" => release with { ExecutingCompletion = null },
            "descriptor-count" => release with { Owners = release.Owners with { DescriptorPackets = 1 } },
            "retired-slot-reused" => release with { RemovedCompletionPacket = release.RemovedCompletionPacket! with { Bytes = release.RemovedCompletionPacket!.Bytes.SetItem(0, 8) } },
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        execution["invocation_release"] = JsonSerializer.SerializeToNode(invalid, Json);
        FreshEntryPairVerdict pair = FreshEntryPairValidation.ValidateFiles(fixture.Pins());
        Assert.False(pair.FunctionalPass);
        Assert.NotEmpty(pair.Errors);
    }

    private static BattleDiagnosticInvocationRelease PostFrameRelease(BattleDiagnosticInvocationRelease baseline)
    {
        BattleDiagnosticInvocationRelease certificate = baseline with { Timestamp = baseline.Timestamp - 1, GraphicsCallback = 7, Owners = baseline.Owners with { AuxiliaryHead = true }, Blockers = ["effect-or-auxiliary-task"] };
        return certificate with { Boundary = BattleDiagnosticInvocationRelease.GraphicsFrameBoundary, PacketRemoved = true, GraphicsCallback = 8, RemovedCompletionPacket = certificate.CompletionPacket! with { Bytes = certificate.CompletionPacket!.Bytes.SetItem(1, 0) }, ExecutingCompletion = certificate, Timestamp = baseline.Timestamp, Owners = baseline.Owners with { DescriptorPackets = 0 }, Blockers = [] };
    }

    [Theory]
    [InlineData("same-report")]
    [InlineData("process")]
    [InlineData("lifecycle")]
    [InlineData("battle")]
    [InlineData("recipe")]
    [InlineData("source")]
    [InlineData("build")]
    [InlineData("initialization")]
    [InlineData("dispatch")]
    [InlineData("completion")]
    [InlineData("restore")]
    [InlineData("second-use")]
    [InlineData("repair")]
    [InlineData("release-missing")]
    [InlineData("release-generation")]
    [InlineData("release-task")]
    [InlineData("release-packet")]
    [InlineData("release-tail")]
    public void Pair_rejects_owner_source_recipe_native_and_independent_anchor_divergence(string fault)
    {
        using Fixture fixture = new(ActualExecutable());
        JsonObject second = fixture.Reports[1];
        switch (fault)
        {
            case "process":
                second["initialization"]!["fresh_entry_host"] = fixture.Reports[0]["initialization"]!["fresh_entry_host"]!.DeepClone();
                break;
            case "lifecycle":
                foreach (string name in new[] { "start_owners", "end_owners" })
                {
                    second["executions"]![0]![name]!["retained_memory_owner_id"] = fixture.Reports[0]["executions"]![0]![name]!["retained_memory_owner_id"]!.DeepClone();
                }
                break;
            case "battle":
                second["battle_instance_id"] = fixture.Reports[0]["battle_instance_id"]!.DeepClone();
                second["initialization"]!["battle_instance_id"] = second["battle_instance_id"]!.DeepClone();
                break;
            case "recipe":
                second["initialization"]!["preparation"]!["driver"]!["expected_effect"] = 2;
                second["expected_effect"] = 2;
                break;
            case "source":
                second["initialization"]!["fresh_entry_host"]!["source_files"]![0]!["sha256"] = new string('f', 64);
                break;
            case "build":
                File.AppendAllText(Path.Combine(fixture.Build, "Memoria.FF8.Runtime.dll"), "changed");
                break;
            case "dispatch":
                second["executions"]![0]!["dispatches"]![0]!["executed_effect"] = 2;
                break;
            case "completion":
                second["executions"]![0]!["cleanup_completed"] = false;
                break;
            case "restore":
                second["executions"]![0]!["start_owners"]!["restore_count"] = 1;
                break;
            case "second-use":
                second["executions"]!.AsArray().Add(second["executions"]![0]!.DeepClone());
                break;
            case "release-missing":
                second["executions"]![0]!["invocation_release"] = null;
                break;
            case "release-generation":
                second["executions"]![0]!["invocation_release"]!["dispatch"]!["native_header_generation"] = 2;
                break;
            case "release-task":
                second["executions"]![0]!["invocation_release"]!["owners"]!["returned_task_pointer"] = 1;
                break;
            case "release-packet":
                second["executions"]![0]!["invocation_release"]!["completion_packet_serial"] = 8;
                break;
            case "release-tail":
                second["executions"]![0]!["post_release_tail"]!["first_presented_frame"] = 0;
                break;
            case "repair":
                second["initialization"]!["preparation"]!["driver"]!["repairs"]!.AsArray().Add(new JsonObject { ["actor_id"] = "party0", ["hit_points"] = 500 });
                break;
        }
        var pins = fixture.Pins(preserveInitialization: fault == "initialization");
        if (fault == "initialization")
        {
            second["initialization"]!["stage"] = 999;
            pins = fixture.Pins(preserveInitialization: true);
        }
        if (fault == "same-report")
        {
            pins = pins with { Entries = [pins.Entries[0], pins.Entries[0]] };
        }
        Assert.False(FreshEntryPairValidation.ValidateFiles(pins).FunctionalPass);
    }

    [Fact]
    public void Existing_same_battle_one_window_is_not_implicitly_promoted()
    {
        JsonObject report = LiveEvidenceValidationTests.Report();
        report["executions"]!.AsArray().RemoveAt(1);
        using var document = JsonDocument.Parse(report.ToJsonString());
        Assert.Contains("EXACTLY_TWO_EXECUTIONS_REQUIRED", LiveEvidenceValidation.Validate(document.RootElement).Errors);
        Assert.NotEqual("PASS", LiveEvidenceValidation.ValidateFreshEntry(document.RootElement).Verdict);
    }

    [Fact]
    public void Coherently_replaced_EXE_and_initialization_cannot_bypass_actual_execution_stack_source()
    {
        using Fixture fixture = new();
        var result = FreshEntryPairValidation.ValidateFiles(fixture.Pins());
        Assert.False(result.FunctionalPass);
        Assert.Contains(result.Errors, error => error.Contains("Execution-stack source names a different original executable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("source")]
    [InlineData("recipe")]
    [InlineData("dispatch")]
    [InlineData("completion")]
    [InlineData("restore")]
    [InlineData("second-use")]
    [InlineData("repair")]
    [InlineData("release-missing")]
    [InlineData("release-generation")]
    [InlineData("release-task")]
    [InlineData("release-packet")]
    [InlineData("release-tail")]
    public void Pure_single_entry_structure_still_rejects_divergence_without_installed_inputs(string fault)
    {
        using Fixture fixture = new();
        JsonObject report = fixture.Reports[0];
        switch (fault)
        {
            case "owner":
                report["executions"]![0]!["start_owners"]!["retained_memory_owner_id"] = Guid.NewGuid().ToString();
                break;
            case "source":
                report["initialization"]!["fresh_entry_host"]!["source_files"]![0]!["sha256"] = new string('f', 64);
                break;
            case "recipe":
                report["initialization"]!["preparation"]!["driver"]!["expected_effect"] = 2;
                break;
            case "dispatch":
                report["executions"]![0]!["dispatches"]![0]!["executed_effect"] = 2;
                break;
            case "completion":
                report["executions"]![0]!["cleanup_completed"] = false;
                break;
            case "restore":
                report["executions"]![0]!["start_owners"]!["restore_count"] = 1;
                break;
            case "second-use":
                report["executions"]!.AsArray().Add(report["executions"]![0]!.DeepClone());
                break;
            case "release-missing":
                report["executions"]![0]!["invocation_release"] = null;
                break;
            case "release-generation":
                report["executions"]![0]!["invocation_release"]!["dispatch"]!["native_header_generation"] = 2;
                break;
            case "release-task":
                report["executions"]![0]!["invocation_release"]!["owners"]!["returned_task_pointer"] = 1;
                break;
            case "release-packet":
                report["executions"]![0]!["invocation_release"]!["completion_packet_serial"] = 8;
                break;
            case "release-tail":
                report["executions"]![0]!["post_release_tail"]!["first_presented_frame"] = 0;
                break;
            case "repair":
                report["initialization"]!["preparation"]!["driver"]!["repairs"]!.AsArray().Add(new JsonObject { ["actor_id"] = "party0", ["hit_points"] = 500 });
                break;
        }
        using var document = JsonDocument.Parse(report.ToJsonString());
        Assert.NotEqual("PASS", LiveEvidenceValidation.ValidateFreshEntry(document.RootElement).Verdict);
    }

    private static string ActualExecutable()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EXECUTABLE");
        Assert.SkipUnless(path is not null && File.Exists(path), "Select the existing authenticated original executable for full-file pair source validation.");
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash.ToLowerInvariant(), Hash(File.ReadAllBytes(path!)));
        return path!;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-entry-pair-" + Guid.NewGuid().ToString("N"));
        private readonly FreshEntryFilePin recipe;
        private readonly FreshEntryFilePin executable;
        private readonly FreshEntryFilePin stack;
        private readonly string fingerprint;
        public string Build { get; }
        public JsonObject[] Reports { get; } = new JsonObject[2];

        public Fixture(string? actualExecutable = null)
        {
            Directory.CreateDirectory(root);
            Build = Path.Combine(root, "build");
            Directory.CreateDirectory(Build);
            File.WriteAllText(Path.Combine(Build, "Memoria.FF8.dll"), "synthetic managed host fixture");
            File.WriteAllText(Path.Combine(Build, "Memoria.FF8.Runtime.dll"), "synthetic runtime fixture");
            fingerprint = Hash(JsonSerializer.SerializeToUtf8Bytes(EvidenceStore.DescribeFiles(Build), RecordJson.Options));
            executable = actualExecutable is null ? Write("original.bin", "synthetic mismatching source fixture") : Pin(actualExecutable);
            NativeBattleExecutionStack profile = new(0x200100, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('a', 64), 1, 2);
            stack = Write("stack.json", JsonSerializer.Serialize(profile));
            var source = Write("container.bin", "synthetic installed container fixture");
            var declaration = new DiagnosticFreshEntryDeclaration(DiagnosticFreshEntryDeclaration.SingleUse, [new("container.bin", source.Sha256)]);
            JsonObject template = LiveEvidenceValidationTests.Report();
            JsonObject driver = new()
            {
                ["input"] = template["input"]!.DeepClone(), ["expected_effect"] = 1,
                ["expected_native_action"] = null, ["expected_native_command"] = null, ["tail_frames"] = 2,
                ["fresh_entry"] = JsonSerializer.SerializeToNode(declaration, RecordJson.Options),
                ["repairs"] = new JsonArray(), ["supporting_effects"] = new JsonArray(), ["supporting_invocations"] = new JsonArray()
            };
            JsonObject preparation = new() { ["driver"] = driver };
            recipe = Write("recipe.json", preparation.ToJsonString());
            for (int index = 0; index < 2; index++)
            {
                JsonObject report = LiveEvidenceValidationTests.Report();
                report["executions"]!.AsArray().RemoveAt(1);
                string battle = Guid.NewGuid().ToString();
                string lifecycle = Guid.NewGuid().ToString();
                report["battle_instance_id"] = battle;
                report["fresh_entry"] = JsonSerializer.SerializeToNode(declaration, RecordJson.Options);
                report["supporting_effects"] = new JsonArray();
                report["supporting_invocations"] = new JsonArray();
                report["declared_repairs"] = new JsonArray();
                report["declared_terminal_hold"] = null;
                foreach (string name in new[] { "start_owners", "end_owners" })
                {
                    report["executions"]![0]![name]!["battle_instance_id"] = battle;
                    report["executions"]![0]![name]!["retained_memory_owner_id"] = lifecycle;
                }
                var host = new DiagnosticFreshEntryHostReceipt(Guid.Parse(battle), Guid.Parse(lifecycle), Guid.NewGuid(), 100 + index, 1000 + index, Hash(File.ReadAllBytes(Path.Combine(Build, "Memoria.FF8.dll"))), Hash(File.ReadAllBytes(Path.Combine(Build, "Memoria.FF8.Runtime.dll"))), Hash(JsonSerializer.SerializeToUtf8Bytes(profile)), declaration.SourceFiles);
                report["initialization"] = new JsonObject
                {
                    ["battle_instance_id"] = battle, ["initializer_version"] = "direct-new-game-v2", ["executable_sha256"] = executable.Sha256,
                    ["preparation"] = preparation.DeepClone(), ["fresh_entry_host"] = JsonSerializer.SerializeToNode(host, RecordJson.Options),
                    ["ordered_stages"] = new JsonArray("installed-cold-process", "canonical-encounter-construction", "resource-derived-new-game")
                };
                JsonObject execution = report["executions"]![0]!.AsObject();
                JsonObject dispatched = execution["dispatches"]![0]!.AsObject();
                dispatched["native_command"] = 2;
                dispatched["native_actor_index"] = 0;
                dispatched["native_header_index"] = 0;
                dispatched["native_header_generation"] = 1;
                BattleDiagnosticEffectDispatch focal = dispatched.Deserialize<BattleDiagnosticEffectDispatch>(Json)!;
                byte[] headerBytes = new byte[20];
                headerBytes[1] = 2;
                headerBytes[4] = 21;
                headerBytes[6] = 1;
                byte[] completionBytes = new byte[12];
                completionBytes[0] = 7;
                completionBytes[1] = 0x80;
                completionBytes[2] = 10;
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(completionBytes.AsSpan(4), 0x47DDA0);
                TelemetryFrameSummary inheritedFrames = execution["frames"]!.Deserialize<TelemetryFrameSummary>(Json)!;
                TelemetryFrameInterval lastFocalPresent = inheritedFrames.Intervals[0];
                TelemetryFrameSummary focalFrames = TelemetryFrameCollector.Recompute([lastFocalPresent], inheritedFrames.DroppedSamples);
                execution["frames"] = JsonSerializer.SerializeToNode(focalFrames, RecordJson.Options);
                execution["end_frame"] = lastFocalPresent.End.FrameIndex;
                execution["renderer_frames"] = lastFocalPresent.End.UniqueRenders;
                execution["end_owners"]!["frame_index"] = lastFocalPresent.End.FrameIndex;
                long endCallback = execution["end_callback"]!.GetValue<long>();
                long endFrame = execution["end_frame"]!.GetValue<long>();
                long releaseTicks = 112;
                BattleDiagnosticInvocationOwners releasedOwners = new(1, 0, 0, 0, false, false, 0, false, 0, 0, false, 0, false, false, false, false) { ObservedOriginalEffect = true, SelectedReturnedDescriptor = 0, OriginalPools = [] };
                BattleDiagnosticInvocationRelease release = new(Guid.Parse(lifecycle), endCallback, endFrame, releaseTicks, focal, new(headerBytes.ToImmutableArray(), 0, 0, null), new(completionBytes.ToImmutableArray()), 0x47DDA0, 0, 0, 7, 0x47DDA0, true, true, true, true, releasedOwners, []);
                execution["invocation_release"] = JsonSerializer.SerializeToNode(release, RecordJson.Options);
                execution["effect_completed_ticks"] = releaseTicks;
                execution["effect_duration_ms"] = 10;
                execution["post_release_tail"] = new JsonObject { ["first_presented_frame"] = endFrame + 1, ["last_presented_frame"] = endFrame + 2, ["first_timestamp"] = 115, ["last_timestamp"] = 120, ["presented_frames"] = 2 };
                foreach (JsonNode? observedEvent in execution["events"]!.AsArray())
                {
                    observedEvent!["frame"] = endFrame;
                }
                Reports[index] = report;
                Write($"initialization-{index}.json", report["initialization"]!.ToJsonString());
            }
        }

        public void UsePostFrameRelease()
        {
            foreach (JsonObject report in Reports)
            {
                JsonObject execution = report["executions"]![0]!.AsObject();
                BattleDiagnosticInvocationRelease baseline = execution["invocation_release"]!.Deserialize<BattleDiagnosticInvocationRelease>(Json)!;
                execution["invocation_release"] = JsonSerializer.SerializeToNode(PostFrameRelease(baseline), Json);
            }
        }

        public FreshEntryPairPins Pins(bool preserveInitialization = false)
        {
            ImmutableArray<FreshEntryRunPins>.Builder entries = ImmutableArray.CreateBuilder<FreshEntryRunPins>();
            for (int index = 0; index < 2; index++)
            {
                var report = Write($"report-{index}.json", Reports[index].ToJsonString());
                var initialization = preserveInitialization ? Pin(Path.Combine(root, $"initialization-{index}.json")) : Write($"initialization-{index}.json", Reports[index]["initialization"]!.ToJsonString());
                entries.Add(new(report, initialization));
            }
            return new(recipe, executable, stack, root, Build, fingerprint, entries.ToImmutable());
        }

        private FreshEntryFilePin Write(string name, string text)
        {
            string path = Path.Combine(root, name);
            File.WriteAllText(path, text);
            return Pin(path);
        }
        private static FreshEntryFilePin Pin(string path) => new(path, Hash(File.ReadAllBytes(path)));
        public void Dispose() => Directory.Delete(root, true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
