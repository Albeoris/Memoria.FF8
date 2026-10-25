using System.Diagnostics;

namespace Memoria.FF8.BattleEffects;

public sealed record CoordinationScope(string CampaignId, string PlanHash, string RunPassId);
public sealed record ProcessIdentity(int ProcessId, DateTimeOffset StartedUtc)
{
    public static ProcessIdentity Current
    {
        get
        {
            using Process process = Process.GetCurrentProcess();
            return new ProcessIdentity(process.Id, process.StartTime.ToUniversalTime());
        }
    }
}

public interface IOwnedProcessProbe
{
    bool HasExited(ProcessIdentity identity);
}

public sealed class OwnedProcessProbe : IOwnedProcessProbe
{
    public bool HasExited(ProcessIdentity identity)
    {
        try
        {
            using Process process = Process.GetProcessById(identity.ProcessId);
            return process.HasExited || process.StartTime.ToUniversalTime().Ticks != identity.StartedUtc.UtcTicks;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }
}

public sealed record EffectClaim(CoordinationScope Scope, string EffectKey, string OwnerId, string Token, long Generation, DateTimeOffset AcquiredUtc, DateTimeOffset HeartbeatUtc, DateTimeOffset ExpiresUtc, ProcessIdentity Process);
public sealed record PerformanceSlotLease(string HostId, string ClaimToken, string Token, long Generation, DateTimeOffset ExpiresUtc, ProcessIdentity Process);
public sealed record AllocatedAttempt(string AttemptId, string CaseId, string RelativeDirectory);
public sealed record PublicationInput(string AttemptId, string CaseId, string ResultSha256);
public sealed record PublicationReceipt(int SchemaVersion, CoordinationScope Scope, string EffectKey, string AttemptId, string CaseId, string ClaimToken, long ClaimGeneration, string ResultPath, string ResultSha256, DateTimeOffset AcceptedUtc);
public sealed record EffectCoordinationStatus(string EffectKey, string State, string? OwnerId, string? Token, long Generation, DateTimeOffset? ExpiresUtc, bool IsExpired);
public sealed record CoordinationStatus(CoordinationScope Scope, int Pending, int Claimed, int Completed, int Expired, IReadOnlyList<EffectCoordinationStatus> Effects);

public sealed class CoordinationOwnershipException : InvalidOperationException
{
    public CoordinationOwnershipException(string message) : base(message)
    {
    }
}
