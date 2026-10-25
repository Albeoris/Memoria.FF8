using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class CoordinationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "battle-coordination-" + Guid.NewGuid().ToString("N"));
    private readonly ManualTime time = new();
    private readonly ProcessProbe processes = new();
    private readonly CoordinationScope baseline = new("campaign", "plan-hash", "baseline");
    private readonly ProcessIdentity renderer = new(123, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task IndependentConnectionsRaceForOneEffectWithOneWinner()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<EffectClaim?>[] workers = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            await start.Task;
            return Create().ClaimSpecific(baseline, "effect-1", $"worker-{index}", renderer);
        })).ToArray();
        start.SetResult();
        EffectClaim?[] results = await Task.WhenAll(workers);
        Assert.Single(results, result => result is not null);
    }

    [Fact]
    public void ClaimTokenResumesFromSharedStateWithoutLeaseFiles()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        EffectClaim original = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        LocalCoordinator resumed = Create();
        Assert.Equal(original, resumed.GetClaim(original.Token));
        Assert.Null(resumed.GetClaim("unknown-token"));
        time.Advance(TimeSpan.FromSeconds(15));
        EffectClaim refreshed = resumed.Heartbeat(original);
        Assert.Equal(refreshed, coordinator.GetClaim(original.Token));
        resumed.Release(refreshed);
        Assert.Null(coordinator.GetClaim(original.Token));
        Assert.Equal(Path.Combine(directory, "coordination.json"), Assert.Single(Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)));
    }

    [Fact]
    public void HeartbeatsExtendOnlyTheirOwnLiveLeasesAndStatusPreservesExpiredIdentity()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1", "effect-2"]);
        EffectClaim original = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        PerformanceSlotLease slot = Assert.IsType<PerformanceSlotLease>(coordinator.TryAcquirePerformanceSlot(original, Environment.MachineName, renderer));
        time.Advance(TimeSpan.FromSeconds(30));
        EffectClaim refreshed = coordinator.Heartbeat(original);
        PerformanceSlotLease refreshedSlot = coordinator.HeartbeatPerformanceSlot(slot);
        Assert.True(refreshed.ExpiresUtc > original.ExpiresUtc);
        Assert.True(refreshedSlot.ExpiresUtc > slot.ExpiresUtc);
        Assert.Equal(original.Token, refreshed.Token);
        Assert.Equal(slot.Generation, refreshedSlot.Generation);
        time.Advance(TimeSpan.FromSeconds(31));
        CoordinationStatus active = coordinator.GetStatus(baseline);
        Assert.Equal(1, active.Pending);
        Assert.Equal(1, active.Claimed);
        Assert.Equal(0, active.Expired);
        Assert.Equal(original.Token, Assert.Single(active.Effects, effect => effect.State == "claimed").Token);
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, coordinator.GetStatus(baseline).Expired);
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.HeartbeatPerformanceSlot(refreshedSlot));
    }

    [Fact]
    public void ExpiryAndRecoveryFenceOldHeartbeatReleaseAndPublication()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        EffectClaim original = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        AllocatedAttempt attempt = coordinator.AllocateAttempt(original, "case-1");
        PublicationInput publication = Publish(attempt);
        time.Advance(TimeSpan.FromSeconds(61));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.Heartbeat(original));
        Assert.Equal(1, coordinator.RecoverStale(baseline));
        EffectClaim replacement = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "replacement", renderer));
        Assert.True(replacement.Generation > original.Generation);
        Assert.NotEqual(original.Token, replacement.Token);
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.Release(original));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.Complete(original, [publication]));
        Assert.Null(coordinator.GetReceipt(attempt.AttemptId));
        Assert.True(new EvidenceStore(directory).Exists(attempt.RelativeDirectory + "/result.json"));
    }

    [Fact]
    public void StoreCampaignAndPassPlanCannotBeRebound()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        Assert.Throws<InvalidOperationException>(() => coordinator.RegisterEffects(baseline with { CampaignId = "other" }, ["effect-1"]));
        Assert.Throws<InvalidOperationException>(() => coordinator.RegisterEffects(baseline with { PlanHash = "other" }, ["effect-1"]));
        Assert.NotNull(coordinator.ClaimNext(baseline, "worker", renderer));
    }

    [Fact]
    public void CompletedBaselineDoesNotClaimOrCompleteFinalPass()
    {
        LocalCoordinator coordinator = Create();
        CoordinationScope final = baseline with { RunPassId = "final" };
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        coordinator.RegisterEffects(final, ["effect-1"]);
        EffectClaim claim = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        coordinator.Complete(claim, [Publish(coordinator.AllocateAttempt(claim, "case-1"))]);
        Assert.Null(coordinator.ClaimNext(baseline, "worker", renderer));
        Assert.NotNull(coordinator.ClaimNext(final, "worker", renderer));
    }

    [Fact]
    public void SlotExpiryNeverProvesTheRendererExitedAndOldOwnerCannotReleaseReplacement()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1", "effect-2"]);
        EffectClaim first = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "first", renderer));
        PerformanceSlotLease firstSlot = Assert.IsType<PerformanceSlotLease>(coordinator.TryAcquirePerformanceSlot(first, Environment.MachineName, renderer));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.ReleasePerformanceSlot(firstSlot));
        time.Advance(TimeSpan.FromSeconds(61));
        Assert.False(coordinator.RecoverStalePerformanceSlot(Environment.MachineName));
        Assert.Equal(0, coordinator.RecoverStale(baseline));
        EffectClaim second = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "second", renderer));
        Assert.Null(coordinator.TryAcquirePerformanceSlot(second, Environment.MachineName, renderer));
        processes.Exited = true;
        Assert.True(coordinator.RecoverStalePerformanceSlot(Environment.MachineName));
        PerformanceSlotLease replacement = Assert.IsType<PerformanceSlotLease>(coordinator.TryAcquirePerformanceSlot(second, Environment.MachineName, renderer));
        Assert.True(replacement.Generation > firstSlot.Generation);
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.ReleasePerformanceSlot(firstSlot));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.HeartbeatPerformanceSlot(firstSlot));
        coordinator.ReleasePerformanceSlot(replacement);
    }

    [Fact]
    public void SlotTokensAndProcessIdentityCannotBeForgedToReleaseRunningRenderer()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        EffectClaim claim = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        PerformanceSlotLease slot = Assert.IsType<PerformanceSlotLease>(coordinator.TryAcquirePerformanceSlot(claim, Environment.MachineName, renderer));
        PerformanceSlotLease forged = slot with { Process = renderer with { ProcessId = 999 }, Token = "wrong" };
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.ReleasePerformanceSlot(forged));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.ReleasePerformanceSlot(slot with { Process = renderer with { ProcessId = 999 } }));
        Assert.Equal(renderer, processes.LastIdentity);
        Assert.Throws<NotSupportedException>(() => coordinator.TryAcquirePerformanceSlot(claim, "a-different-host", renderer));
    }

    [Fact]
    public void ActiveSlotBlocksClaimReleaseAndCompletion()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        EffectClaim claim = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        PublicationInput publication = Publish(coordinator.AllocateAttempt(claim, "case-1"));
        PerformanceSlotLease slot = Assert.IsType<PerformanceSlotLease>(coordinator.TryAcquirePerformanceSlot(claim, Environment.MachineName, renderer));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.Release(claim));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.Complete(claim, [publication]));
        processes.Exited = true;
        coordinator.ReleasePerformanceSlot(slot);
        Assert.Single(coordinator.Complete(claim, [publication]));
    }

    [Fact]
    public void ReceiptTransactionSurvivesMissingExportAndExportNeverOverwrites()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        EffectClaim claim = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        AllocatedAttempt attempt = coordinator.AllocateAttempt(claim, "case-1");
        PublicationInput publication = Publish(attempt);
        PublicationReceipt committed = Assert.Single(coordinator.Complete(claim, [publication]));
        string receiptPath = "receipts/" + attempt.AttemptId + ".json";
        EvidenceStore evidence = new(directory);
        Assert.False(evidence.Exists(receiptPath));
        LocalCoordinator resumed = Create();
        Assert.Equal(committed, resumed.GetReceipt(attempt.AttemptId));
        resumed.ExportReceipt(attempt.AttemptId);
        byte[] bytes = evidence.ReadBytes(receiptPath);
        resumed.ExportReceipt(attempt.AttemptId);
        Assert.Equal(bytes, evidence.ReadBytes(receiptPath));
        Assert.True(File.Exists(Path.Combine(directory, "coordination.json")));
    }

    [Fact]
    public void ConflictingReceiptRecordCannotReplaceCommittedPublication()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        EffectClaim claim = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        PublicationInput publication = Publish(coordinator.AllocateAttempt(claim, "case-1"));
        PublicationReceipt committed = Assert.Single(coordinator.Complete(claim, [publication]));
        EvidenceStore evidence = new(directory);
        string receiptPath = "receipts/" + publication.AttemptId + ".json";
        evidence.WriteBytesNew(receiptPath, Encoding.UTF8.GetBytes("conflicting-content"));
        Assert.Throws<InvalidDataException>(() => coordinator.ExportReceipt(publication.AttemptId));
        Assert.Equal("conflicting-content", Encoding.UTF8.GetString(evidence.ReadBytes(receiptPath)));
        Assert.Equal(committed, coordinator.GetReceipt(publication.AttemptId));
    }

    [Fact]
    public void FailedSecondPublicationRollsBackFirstReceiptAndCompletion()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        EffectClaim claim = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker", renderer));
        PublicationInput first = Publish(coordinator.AllocateAttempt(claim, "case-1"));
        PublicationInput second = Publish(coordinator.AllocateAttempt(claim, "case-2"));
        byte[] before = File.ReadAllBytes(Path.Combine(directory, "coordination.json"));
        Assert.Throws<InvalidDataException>(() => coordinator.Complete(claim, [first, second with { ResultSha256 = new string('0', 64) }]));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(directory, "coordination.json")));
        Assert.Null(coordinator.GetReceipt(first.AttemptId));
        Assert.Null(coordinator.GetReceipt(second.AttemptId));
        Assert.Equal(2, coordinator.Complete(claim, [first, second]).Count);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task ExclusiveOsHandleBlocksReadersUntilReleased()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        using FileStream held = new(Path.Combine(directory, "coordination.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<CoordinationStatus> reader = Task.Run(() =>
        {
            started.SetResult();
            return Create().GetStatus(baseline);
        });
        await started.Task;
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(reader.IsCompleted);
        held.Dispose();
        CoordinationStatus status = await reader.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(1, status.Pending);
    }

    [Fact]
    public void MalformedSnapshotIsNeverSilentlyResetOrReplaced()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1"]);
        string path = Path.Combine(directory, "coordination.json");
        File.WriteAllText(path, "{}");
        Assert.Throws<JsonException>(() => coordinator.ClaimNext(baseline, "worker", renderer));
        Assert.Equal("{}", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void WrongAllocationCannotBePublishedAndUnacceptedEvidenceIsPreserved()
    {
        LocalCoordinator coordinator = Create();
        coordinator.RegisterEffects(baseline, ["effect-1", "effect-2"]);
        EffectClaim first = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker-1", renderer));
        EffectClaim second = Assert.IsType<EffectClaim>(coordinator.ClaimNext(baseline, "worker-2", renderer));
        PublicationInput publication = Publish(coordinator.AllocateAttempt(first, "case-1"));
        Assert.Throws<CoordinationOwnershipException>(() => coordinator.Complete(second, [publication]));
        Assert.Null(coordinator.GetReceipt(publication.AttemptId));
        Assert.Throws<InvalidOperationException>(() => coordinator.ExportReceipt(publication.AttemptId));
        Assert.Single(coordinator.Complete(first, [publication]));
    }

    [Fact]
    public void ProcessProbeChecksStartTimeAndDoesNotTreatPidReuseAsOriginalRenderer()
    {
        OwnedProcessProbe probe = new();
        ProcessIdentity current = ProcessIdentity.Current;
        Assert.False(probe.HasExited(current));
        Assert.True(probe.HasExited(current with { StartedUtc = current.StartedUtc.AddSeconds(-1) }));
    }

    private LocalCoordinator Create()
    {
        return new LocalCoordinator(directory, time, processes);
    }

    private PublicationInput Publish(AllocatedAttempt attempt)
    {
        byte[] content = Encoding.UTF8.GetBytes("{\"verdict\":\"ERROR\",\"synthetic\":true}");
        new EvidenceStore(directory).WriteBytesNew(attempt.RelativeDirectory + "/result.json", content);
        return new PublicationInput(attempt.AttemptId, attempt.CaseId, Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }

        public void Advance(TimeSpan duration)
        {
            now += duration;
        }
    }

    private sealed class ProcessProbe : IOwnedProcessProbe
    {
        public bool Exited { get; set; }
        public ProcessIdentity? LastIdentity { get; private set; }

        public bool HasExited(ProcessIdentity identity)
        {
            LastIdentity = identity;
            return Exited;
        }
    }
}
