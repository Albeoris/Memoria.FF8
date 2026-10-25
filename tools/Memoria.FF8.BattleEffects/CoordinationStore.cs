using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.BattleEffects;

/// <summary>One-machine coordinator with one atomically replaced JSON snapshot guarded by an exclusive OS file handle.</summary>
public sealed class LocalCoordinator
{
    private readonly EvidenceStore evidenceStore;
    private readonly TimeProvider time;
    private readonly IOwnedProcessProbe processes;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true, RespectRequiredConstructorParameters = true };

    public LocalCoordinator(string storePath, TimeProvider? timeProvider = null, IOwnedProcessProbe? processProbe = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        string canonicalPath = Path.GetFullPath(storePath);
        string? root = Path.GetPathRoot(canonicalPath);
        if (canonicalPath.StartsWith(@"\\", StringComparison.Ordinal) || (root is not null && new DriveInfo(root).DriveType == DriveType.Network))
        {
            throw new NotSupportedException("The coordinator requires a local filesystem.");
        }
        evidenceStore = new EvidenceStore(storePath);
        Directory.CreateDirectory(evidenceStore.Root);
        time = timeProvider ?? TimeProvider.System;
        processes = processProbe ?? new OwnedProcessProbe();
    }

    public void RegisterEffects(CoordinationScope scope, IEnumerable<string> effectKeys)
    {
        ValidateScope(scope);
        Transact(state =>
        {
            if (state.CampaignId is not null && state.CampaignId != scope.CampaignId)
            {
                throw new InvalidOperationException("The store belongs to another campaign.");
            }
            state.CampaignId = scope.CampaignId;
            CoordinationScope? existing = state.Passes.SingleOrDefault(pass => pass.RunPassId == scope.RunPassId);
            if (existing is not null && existing != scope)
            {
                throw new InvalidOperationException("An execution pass cannot change its campaign or pinned plan.");
            }
            if (existing is null)
            {
                state.Passes.Add(scope);
            }
            foreach (string effect in effectKeys)
            {
                EvidenceStore.Id(effect);
                if (!state.Effects.Any(entry => entry.Scope == scope && entry.EffectKey == effect))
                {
                    state.Effects.Add(new EffectEntry { Scope = scope, EffectKey = effect });
                }
            }
            return true;
        });
    }

    public EffectClaim? ClaimNext(CoordinationScope scope, string ownerId, ProcessIdentity process, TimeSpan? lease = null)
    {
        return Claim(scope, null, ownerId, process, lease);
    }

    public EffectClaim? ClaimSpecific(CoordinationScope scope, string effectKey, string ownerId, ProcessIdentity process, TimeSpan? lease = null)
    {
        EvidenceStore.Id(effectKey);
        return Claim(scope, effectKey, ownerId, process, lease);
    }

    public EffectClaim? GetClaim(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Read(state => state.Effects.SingleOrDefault(entry => entry.State == "claimed" && entry.Claim?.Token == token)?.Claim);
    }

    private EffectClaim? Claim(CoordinationScope scope, string? effectKey, string ownerId, ProcessIdentity process, TimeSpan? lease)
    {
        ValidateScope(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ValidateProcess(process);
        TimeSpan duration = Duration(lease);
        return Transact<EffectClaim?>(state =>
        {
            EffectEntry? entry = state.Effects.Where(entry => entry.Scope == scope && entry.State == "pending" && (effectKey is null || entry.EffectKey == effectKey)).OrderBy(entry => entry.EffectKey, StringComparer.Ordinal).FirstOrDefault();
            if (entry is null)
            {
                return null;
            }
            DateTimeOffset now = time.GetUtcNow();
            entry.Generation = checked(entry.Generation + 1);
            entry.Claim = new EffectClaim(scope, entry.EffectKey, ownerId, Guid.NewGuid().ToString("N"), entry.Generation, now, now, now + duration, process);
            entry.State = "claimed";
            return entry.Claim;
        });
    }

    public EffectClaim Heartbeat(EffectClaim claim, TimeSpan? lease = null)
    {
        TimeSpan duration = Duration(lease);
        return Transact(state =>
        {
            DateTimeOffset now = time.GetUtcNow();
            EffectEntry entry = RequireClaim(state, claim, now);
            entry.Claim = entry.Claim! with { HeartbeatUtc = now, ExpiresUtc = now + duration };
            return entry.Claim!;
        });
    }

    public void Release(EffectClaim claim)
    {
        Transact(state =>
        {
            EffectEntry entry = RequireClaim(state, claim, time.GetUtcNow());
            RequireNoSlot(state, claim.Token);
            entry.State = "pending";
            return true;
        });
    }

    public int RecoverStale(CoordinationScope scope)
    {
        ValidateScope(scope);
        return Transact(state =>
        {
            DateTimeOffset now = time.GetUtcNow();
            int recovered = 0;
            foreach (EffectEntry entry in state.Effects.Where(entry => entry.Scope == scope && entry.State == "claimed" && entry.Claim!.ExpiresUtc <= now))
            {
                if (!state.Slots.Any(slot => slot.Lease?.ClaimToken == entry.Claim!.Token))
                {
                    entry.State = "pending";
                    recovered++;
                }
            }
            return recovered;
        });
    }

    public AllocatedAttempt AllocateAttempt(EffectClaim claim, string caseId)
    {
        EvidenceStore.Id(caseId);
        return Transact(state =>
        {
            RequireClaim(state, claim, time.GetUtcNow());
            string id = Guid.NewGuid().ToString("N");
            AllocatedAttempt attempt = new(id, caseId, $"attempts/{claim.Scope.RunPassId}/{claim.EffectKey}/{caseId}/{id}");
            state.Attempts.Add(new AttemptEntry { ClaimToken = claim.Token, Attempt = attempt });
            return attempt;
        });
    }

    public PerformanceSlotLease? TryAcquirePerformanceSlot(EffectClaim claim, string hostId, ProcessIdentity renderer, TimeSpan? lease = null)
    {
        string host = Host(hostId);
        ValidateProcess(renderer);
        TimeSpan duration = Duration(lease);
        return Transact<PerformanceSlotLease?>(state =>
        {
            DateTimeOffset now = time.GetUtcNow();
            RequireClaim(state, claim, now);
            SlotEntry? slot = state.Slots.SingleOrDefault(slot => slot.HostId == host);
            if (slot is null)
            {
                slot = new SlotEntry { HostId = host };
                state.Slots.Add(slot);
            }
            if (slot.Lease is not null)
            {
                return null;
            }
            slot.Generation = checked(slot.Generation + 1);
            slot.Lease = new PerformanceSlotLease(host, claim.Token, Guid.NewGuid().ToString("N"), slot.Generation, now + duration, renderer);
            return slot.Lease;
        });
    }

    public PerformanceSlotLease HeartbeatPerformanceSlot(PerformanceSlotLease slot, TimeSpan? lease = null)
    {
        TimeSpan duration = Duration(lease);
        return Transact(state =>
        {
            DateTimeOffset now = time.GetUtcNow();
            SlotEntry entry = RequireSlot(state, slot, now);
            if (!state.Effects.Any(effect => effect.State == "claimed" && effect.Claim is { } current && current.Token == entry.Lease!.ClaimToken && current.ExpiresUtc > now))
            {
                throw new CoordinationOwnershipException("The effect claim is expired or fenced.");
            }
            entry.Lease = entry.Lease! with { ExpiresUtc = now + duration };
            return entry.Lease!;
        });
    }

    public void ReleasePerformanceSlot(PerformanceSlotLease slot)
    {
        Transact(state =>
        {
            SlotEntry entry = RequireSlot(state, slot, time.GetUtcNow());
            if (!processes.HasExited(entry.Lease!.Process))
            {
                throw new CoordinationOwnershipException("The owned renderer has not been confirmed exited.");
            }
            entry.Lease = null;
            return true;
        });
    }

    public bool RecoverStalePerformanceSlot(string hostId)
    {
        string host = Host(hostId);
        return Transact(state =>
        {
            SlotEntry? entry = state.Slots.SingleOrDefault(slot => slot.HostId == host);
            if (entry?.Lease is null || entry.Lease.ExpiresUtc > time.GetUtcNow() || !processes.HasExited(entry.Lease.Process))
            {
                return false;
            }
            entry.Lease = null;
            return true;
        });
    }

    public IReadOnlyList<PublicationReceipt> Complete(EffectClaim claim, IEnumerable<PublicationInput> publications)
    {
        PublicationInput[] inputs = publications.ToArray();
        if (inputs.Length == 0 || inputs.Select(input => input.AttemptId).Distinct(StringComparer.Ordinal).Count() != inputs.Length)
        {
            throw new ArgumentException("Completion requires distinct published attempts.", nameof(publications));
        }
        return Transact<IReadOnlyList<PublicationReceipt>>(state =>
        {
            DateTimeOffset now = time.GetUtcNow();
            EffectEntry entry = RequireClaim(state, claim, now);
            RequireNoSlot(state, claim.Token);
            List<PublicationReceipt> receipts = [];
            foreach (PublicationInput input in inputs)
            {
                AllocatedAttempt? attempt = state.Attempts.SingleOrDefault(entry => entry.ClaimToken == claim.Token && entry.Attempt.AttemptId == input.AttemptId && entry.Attempt.CaseId == input.CaseId)?.Attempt;
                if (attempt is null)
                {
                    throw new CoordinationOwnershipException("Attempt was not allocated to this claim.");
                }
                string relative = attempt.RelativeDirectory + "/result.json";
                string hash = Convert.ToHexStringLower(SHA256.HashData(evidenceStore.ReadBytes(relative)));
                if (!string.Equals(hash, input.ResultSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Published result hash does not match durable evidence.");
                }
                if (state.Receipts.Any(receipt => receipt.AttemptId == input.AttemptId))
                {
                    throw new InvalidOperationException("A committed receipt already exists for this attempt.");
                }
                PublicationReceipt receipt = new(1, claim.Scope, claim.EffectKey, input.AttemptId, input.CaseId, claim.Token, claim.Generation, relative, hash, now);
                state.Receipts.Add(receipt);
                receipts.Add(receipt);
            }
            RequireClaim(state, claim, time.GetUtcNow());
            entry.State = "completed";
            return receipts;
        });
    }

    public PublicationReceipt? GetReceipt(string attemptId)
    {
        return Read(state => state.Receipts.SingleOrDefault(receipt => receipt.AttemptId == attemptId));
    }

    public IReadOnlyList<PublicationReceipt> GetReceipts(CoordinationScope scope)
    {
        ValidateScope(scope);
        return Read<IReadOnlyList<PublicationReceipt>>(state => state.Receipts.Where(receipt => receipt.Scope == scope).OrderBy(receipt => receipt.AttemptId, StringComparer.Ordinal).ToArray());
    }

    public CoordinationStatus GetStatus(CoordinationScope scope)
    {
        ValidateScope(scope);
        return Read(state =>
        {
            DateTimeOffset now = time.GetUtcNow();
            EffectCoordinationStatus[] effects = state.Effects.Where(entry => entry.Scope == scope).OrderBy(entry => entry.EffectKey, StringComparer.Ordinal).Select(entry => new EffectCoordinationStatus(entry.EffectKey, entry.State, entry.Claim?.OwnerId, entry.State == "pending" ? null : entry.Claim?.Token, entry.Generation, entry.Claim?.ExpiresUtc, entry.State == "claimed" && entry.Claim!.ExpiresUtc <= now)).ToArray();
            return new CoordinationStatus(scope, effects.Count(effect => effect.State == "pending"), effects.Count(effect => effect.State == "claimed"), effects.Count(effect => effect.State == "completed"), effects.Count(effect => effect.IsExpired), effects);
        });
    }

    public void ExportReceipt(string attemptId)
    {
        EvidenceStore.Id(attemptId);
        PublicationReceipt receipt = GetReceipt(attemptId) ?? throw new InvalidOperationException("No committed receipt exists.");
        string key = $"receipts/{attemptId}.json";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions);
        try
        {
            evidenceStore.WriteBytesNew(key, bytes);
        }
        catch (IOException) when (evidenceStore.Exists(key))
        {
            if (!evidenceStore.ReadBytes(key).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException("An immutable receipt record already exists with different content.");
            }
        }
    }

    private T Read<T>(Func<CoordinatorState, T> read)
    {
        using FileStream exclusive = AcquireLock();
        return read(Load());
    }

    private T Transact<T>(Func<CoordinatorState, T> mutation)
    {
        using FileStream exclusive = AcquireLock();
        CoordinatorState state = Load();
        T result = mutation(state);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        string path = evidenceStore.Resolve("coordination.json");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
        return result;
    }

    private FileStream AcquireLock()
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(evidenceStore.Resolve("coordination.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)
            {
                if (elapsed.Elapsed >= TimeSpan.FromSeconds(10))
                {
                    throw new IOException("The coordinator lock could not be acquired within ten seconds.", exception);
                }
                Thread.Sleep(25);
            }
        }
    }

    private CoordinatorState Load()
    {
        string path = evidenceStore.Resolve("coordination.json");
        if (!File.Exists(path))
        {
            return new CoordinatorState();
        }
        CoordinatorState state = JsonSerializer.Deserialize<CoordinatorState>(File.ReadAllBytes(path), JsonOptions) ?? throw new InvalidDataException("The coordinator snapshot is null.");
        if (state.SchemaVersion != 1 || state.Effects is null || state.Passes is null || state.Slots is null || state.Attempts is null || state.Receipts is null)
        {
            throw new InvalidDataException("The coordinator snapshot has an unsupported schema.");
        }
        return state;
    }

    private static EffectEntry RequireClaim(CoordinatorState state, EffectClaim claim, DateTimeOffset now)
    {
        EffectEntry? entry = state.Effects.SingleOrDefault(entry => entry.Scope == claim.Scope && entry.EffectKey == claim.EffectKey && entry.State == "claimed" && entry.Claim is { } current && current.Token == claim.Token && current.Generation == claim.Generation && current.OwnerId == claim.OwnerId && current.ExpiresUtc > now);
        return entry ?? throw new CoordinationOwnershipException("The effect claim is expired or fenced.");
    }

    private static SlotEntry RequireSlot(CoordinatorState state, PerformanceSlotLease slot, DateTimeOffset now)
    {
        SlotEntry? entry = state.Slots.SingleOrDefault(entry => entry.HostId == slot.HostId && entry.Lease is { } current && current.Token == slot.Token && current.Generation == slot.Generation && current.ClaimToken == slot.ClaimToken && current.ExpiresUtc > now);
        return entry ?? throw new CoordinationOwnershipException("The performance slot lease is expired or fenced.");
    }

    private static void RequireNoSlot(CoordinatorState state, string token)
    {
        if (state.Slots.Any(slot => slot.Lease?.ClaimToken == token))
        {
            throw new CoordinationOwnershipException("Release the owned renderer and performance slot before releasing or completing the claim.");
        }
    }

    private static string Host(string hostId)
    {
        if (!string.Equals(hostId, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only the current machine's authoritative slot is supported.");
        }
        return Environment.MachineName.ToUpperInvariant();
    }

    private static TimeSpan Duration(TimeSpan? lease)
    {
        TimeSpan duration = lease ?? TimeSpan.FromSeconds(60);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(lease));
        }
        return duration;
    }

    private static void ValidateScope(CoordinationScope scope)
    {
        EvidenceStore.Id(scope.CampaignId);
        EvidenceStore.Id(scope.PlanHash);
        EvidenceStore.Id(scope.RunPassId);
    }

    private static void ValidateProcess(ProcessIdentity identity)
    {
        if (identity.ProcessId <= 0 || identity.StartedUtc.Offset != TimeSpan.Zero || identity.StartedUtc == DateTimeOffset.MinValue)
        {
            throw new ArgumentException("Process identity requires a positive PID and UTC start time.", nameof(identity));
        }
    }

    private sealed class CoordinatorState
    {
        public CoordinatorState()
        {
        }

        [JsonRequired]
        public int SchemaVersion { get; set; } = 1;
        [JsonRequired]
        public string? CampaignId { get; set; }
        [JsonRequired]
        public List<CoordinationScope> Passes { get; set; } = [];
        [JsonRequired]
        public List<EffectEntry> Effects { get; set; } = [];
        [JsonRequired]
        public List<SlotEntry> Slots { get; set; } = [];
        [JsonRequired]
        public List<AttemptEntry> Attempts { get; set; } = [];
        [JsonRequired]
        public List<PublicationReceipt> Receipts { get; set; } = [];
    }

    private sealed class EffectEntry
    {
        public EffectEntry()
        {
        }

        [JsonRequired]
        public CoordinationScope Scope { get; set; } = null!;
        [JsonRequired]
        public string EffectKey { get; set; } = "";
        [JsonRequired]
        public string State { get; set; } = "pending";
        [JsonRequired]
        public long Generation { get; set; }
        [JsonRequired]
        public EffectClaim? Claim { get; set; }
    }

    private sealed class SlotEntry
    {
        public SlotEntry()
        {
        }

        [JsonRequired]
        public string HostId { get; set; } = "";
        [JsonRequired]
        public long Generation { get; set; }
        [JsonRequired]
        public PerformanceSlotLease? Lease { get; set; }
    }

    private sealed class AttemptEntry
    {
        public AttemptEntry()
        {
        }

        [JsonRequired]
        public string ClaimToken { get; set; } = "";
        [JsonRequired]
        public AllocatedAttempt Attempt { get; set; } = null!;
    }
}
