using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8;

internal sealed record AutoPlayExhaustedSurveyRequest(string NavigationKey, string Signature, bool RetryWalkingBlocks = false);

internal sealed partial class AutoPlayController
{
    internal static string ExhaustedSurveySignature(string navigation, AutoPlayVisitGuard guard, string fingerprint)
    {
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(new { Navigation = navigation, guard.Version, guard.FieldId, guard.ProgramHash, guard.Reads, Fingerprint = fingerprint }, BeforeSnapshotJsonOptions);
        return Convert.ToHexString(SHA256.HashData(identity));
    }

    internal static void SeedExhaustedSurveyInspection(AutoPlayRunState state, string navigation, AutoPlayVisitGuard guard)
    {
        if (guard.Version == AutoPlayVisitGuard.CurrentVersion)
        {
            state.ExhaustedSurveyInspections.Add(ExhaustedSurveySignature(navigation, guard, guard.Fingerprint));
        }
    }

    internal static void UpdateExhaustedSurveyTarget(AutoPlayRunState state, string? target)
    {
        if (state.PendingExhaustedSurvey is { } pending && pending.NavigationKey != target)
        {
            state.PendingExhaustedSurvey = null;
        }
        state.RefreshTargetVisit = target;
    }

    internal static bool CompleteExhaustedSurveyInspection(AutoPlayRunState state, string navigation, Func<AutoPlayVisitGuard, string?> readFingerprint)
    {
        UpdateExhaustedSurveyTarget(state, state.RefreshTargetVisit);
        if (state.PendingExhaustedSurvey is not { } pending || pending.NavigationKey != navigation)
        {
            return false;
        }
        string? current = state.VisitGuards.TryGetValue(navigation, out AutoPlayVisitGuard? guard) && guard.Version == AutoPlayVisitGuard.CurrentVersion ? readFingerprint(guard) : null;
        bool completed = state.ExhaustedSurveyInspections.Add(pending.Signature);
        if (current is not null)
        {
            state.ExhaustedSurveyInspections.Add(ExhaustedSurveySignature(navigation, guard!, current));
            if (pending.RetryWalkingBlocks)
            {
                AutoPlayEventCoverage[] blocks = SoftWalkingBlocks(state, navigation);
                if (blocks.Length != 0)
                {
                    state.ExhaustedSurveyInspections.Add(SoftWalkingSurveySignature(navigation, guard!, current, blocks));
                    foreach (AutoPlayEventCoverage item in blocks)
                    {
                        string? signature = item.Event.ConditionSignature;
                        bool completedEntry = item.Completed && (item.ObservedConditionSignature == signature || item.Routes.Any(route => route.ConditionSignature == signature) || item.ObservedConditionSignature is null && item.Routes.Count == 0);
                        if (signature is not null && !completedEntry)
                        {
                            item.Conditions?.ConsumedEntries.Remove(signature);
                        }
                        item.BlockedReason = null;
                        item.BlockKind = null;
                    }
                }
            }
        }
        UpdateExhaustedSurveyTarget(state, null);
        return completed;
    }

    internal static AutoPlayPlanStep? PlanWithExhaustedSurvey(AutoPlayRunState state, AutoPlayPlanner planner, string navigation, Func<AutoPlayVisit, bool>? needsRefresh, Func<AutoPlayNavigationVisit, bool> isCurrent, Func<AutoPlayVisitGuard, string?> readFingerprint)
    {
        UpdateExhaustedSurveyTarget(state, state.RefreshTargetVisit);
        AutoPlayPlanStep? next = planner.PlanNavigation(navigation, needsRefresh, state.RefreshTargetVisit, isCurrent);
        if (next is not null)
        {
            UpdateExhaustedSurveyTarget(state, next.RefreshVisitKey);
            return next;
        }

        UpdateExhaustedSurveyTarget(state, null);
        Dictionary<string, AutoPlayExhaustedSurveyRequest> candidates = new(StringComparer.Ordinal);
        bool NeedsExactInspection(AutoPlayVisit visit)
        {
            if (!state.VisitGuards.TryGetValue(visit.Key, out AutoPlayVisitGuard? guard) || guard.Version != AutoPlayVisitGuard.CurrentVersion || readFingerprint(guard) is not string current)
            {
                return false;
            }
            string signature = ExhaustedSurveySignature(visit.Key, guard, current);
            if (current != guard.Fingerprint && !state.ExhaustedSurveyInspections.Contains(signature))
            {
                candidates[visit.Key] = new(visit.Key, signature);
                return true;
            }
            AutoPlayEventCoverage[] blocks = SoftWalkingBlocks(state, visit.Key);
            if (blocks.Length != 0)
            {
                signature = SoftWalkingSurveySignature(visit.Key, guard, current, blocks);
                if (!state.ExhaustedSurveyInspections.Contains(signature))
                {
                    candidates[visit.Key] = new(visit.Key, signature, RetryWalkingBlocks: true);
                    return true;
                }
            }
            return false;
        }
        next = planner.PlanNavigation(navigation, NeedsExactInspection, isCurrent: isCurrent);
        if (next?.RefreshVisitKey is string target && candidates.TryGetValue(target, out AutoPlayExhaustedSurveyRequest? requested))
        {
            state.PendingExhaustedSurvey = requested;
        }
        UpdateExhaustedSurveyTarget(state, next?.RefreshVisitKey);
        return next;
    }

    private static AutoPlayEventCoverage[] SoftWalkingBlocks(AutoPlayRunState state, string navigation)
    {
        AutoPlayNavigationVisit? observed = state.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == navigation);
        AutoPlayVisitCoverage? shared = observed is null ? null : state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observed.VisitKey);
        return shared is null ? [] : shared.Events.Where(item => AutoPlayPlanner.IsSoftWalkingBlock(item) && observed!.Events.Any(known => known.Id == item.Event.Id && known.ConditionSignature == item.Event.ConditionSignature)).OrderBy(item => item.Event.Id, StringComparer.Ordinal).ToArray();
    }

    private static string SoftWalkingSurveySignature(string navigation, AutoPlayVisitGuard guard, string fingerprint, IReadOnlyList<AutoPlayEventCoverage> blocks)
    {
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(new { WalkingSearchRetry = true, Guard = ExhaustedSurveySignature(navigation, guard, fingerprint), Blocks = blocks.Select(item => new { item.Event.Id, item.Event.ConditionSignature }).ToArray() }, BeforeSnapshotJsonOptions);
        return Convert.ToHexString(SHA256.HashData(identity));
    }
}
