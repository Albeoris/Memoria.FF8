using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8;

internal sealed record AutoPlayExhaustedSurveyRequest(string NavigationKey, string Signature);

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
        Dictionary<string, string> candidates = new(StringComparer.Ordinal);
        bool NeedsExactInspection(AutoPlayVisit visit)
        {
            if (!state.VisitGuards.TryGetValue(visit.Key, out AutoPlayVisitGuard? guard) || guard.Version != AutoPlayVisitGuard.CurrentVersion || readFingerprint(guard) is not string current || current == guard.Fingerprint)
            {
                return false;
            }
            string signature = ExhaustedSurveySignature(visit.Key, guard, current);
            if (state.ExhaustedSurveyInspections.Contains(signature))
            {
                return false;
            }
            candidates[visit.Key] = signature;
            return true;
        }
        next = planner.PlanNavigation(navigation, NeedsExactInspection, isCurrent: isCurrent);
        if (next?.RefreshVisitKey is string target && candidates.TryGetValue(target, out string? requested))
        {
            state.PendingExhaustedSurvey = new(target, requested);
        }
        UpdateExhaustedSurveyTarget(state, next?.RefreshVisitKey);
        return next;
    }
}
