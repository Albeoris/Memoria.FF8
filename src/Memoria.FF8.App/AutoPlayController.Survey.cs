using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record AutoPlaySurveyBaseline(int Version, int FieldId, string ProgramHash, Dictionary<string, AutoPlayVisitGuard> Guards, string? CanonicalProgressFingerprint = null);

internal sealed partial class AutoPlayController
{
    private bool surveyAwaitingInitialization;

    private void ObserveSurveyOperation()
    {
        ObserveSurveyInitialization(getWorld());
    }

    private void ResetSurveySegment()
    {
        run.SurveyBaseline = null;
        run.SurveyProgressBeforeInitialization = null;
        surveyAwaitingInitialization = false;
    }

    private void BeginSurveySegment(GameWorld world)
    {
        using AutoPlayPerformanceProfile.Scope measurement = performance.Measure("Survey.Begin");
        if (world.Events.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } initialization)
        {
            run.SurveyBaseline = null;
            surveyAwaitingInitialization = world.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Allocated or NativeFieldInitializationStage.Initializing };
            return;
        }
        Dictionary<string, AutoPlayVisitGuard> guards = new(StringComparer.Ordinal);
        foreach ((string key, AutoPlayVisitGuard guard) in run.VisitGuards)
        {
            if (guard.Version != AutoPlayVisitGuard.CurrentVersion || ReadSurveyProgramHash(world, guard.FieldId) != guard.ProgramHash || ReadSurveyFingerprint(world, guard.Reads) is not string fingerprint)
            {
                run.InspectedVisitsSinceProgress.Remove(key);
                continue;
            }
            guards.Add(key, guard with { Fingerprint = fingerprint });
        }
        run.SurveyBaseline = new(1, initialization.FieldId, initialization.ProgramHash, guards, ReadSurveyCanonicalProgress(world));
        surveyAwaitingInitialization = false;
    }

    private void ObserveSurveyInitialization(GameWorld world)
    {
        if (world.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Allocated } && world.Events.Frame == 0)
        {
            run.SurveyProgressBeforeInitialization ??= run.SurveyBaseline?.CanonicalProgressFingerprint ?? ReadSurveyCanonicalProgress(world);
            run.SurveyBaseline = null;
            surveyAwaitingInitialization = true;
            return;
        }
        if ((run.SurveyBaseline is null || surveyAwaitingInitialization && world.Events.Frame == 1) && world.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
        {
            string? canonical = ReadSurveyCanonicalProgress(world);
            if (run.SurveyProgressBeforeInitialization is string previous && canonical is string current && previous != current)
            {
                run.InspectedVisitsSinceProgress.Clear();
                run.RefreshTargetVisit = null;
            }
            run.SurveyProgressBeforeInitialization = null;
            BeginSurveySegment(world);
        }
    }

    private void ObserveSurveySegmentChanges(GameWorld world)
    {
        using AutoPlayPerformanceProfile.Scope measurement = performance.Measure("Survey.ObserveChanges");
        if (run.SurveyBaseline is not { } baseline)
        {
            return;
        }
        if (baseline.Version != 1)
        {
            throw new InvalidDataException("The field survey baseline has an unsupported version.");
        }
        string? canonical = ReadSurveyCanonicalProgress(world);
        if (world.Events.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } initialization || baseline.FieldId != initialization.FieldId || baseline.ProgramHash != initialization.ProgramHash)
        {
            foreach (string key in baseline.Guards.Keys)
            {
                run.InspectedVisitsSinceProgress.Remove(key);
            }
            ResetSurveySegment();
            return;
        }
        foreach (string key in FindChangedSurveyVisits(baseline, run.VisitGuards, field => ReadSurveyProgramHash(world, field), reads => ReadSurveyFingerprint(world, reads), recordedTravel: run.ActiveTravelRoute is not null, currentCanonicalProgressFingerprint: canonical))
        {
            run.InspectedVisitsSinceProgress.Remove(key);
        }
        BeginSurveySegment(world);
    }

    private string ReadSurveyProgramHash(GameWorld world, int field)
    {
        using AutoPlayPerformanceProfile.Scope measurement = performance.Measure("Survey.ProgramHash");
        return world.ReadAutoPlayFieldProgramHash(field);
    }

    private string? ReadSurveyFingerprint(GameWorld world, IReadOnlyList<NativePersistentPredicateRead> reads)
    {
        using AutoPlayPerformanceProfile.Scope measurement = performance.Measure("Survey.Fingerprint");
        return world.Events.ReadPersistentPredicateFingerprint(reads);
    }

    private static string? ReadSurveyCanonicalProgress(GameWorld world)
    {
        return world.Events.ReadPersistentPredicateFingerprint([new NativePersistentPredicateRead("PSHM_W", 256, 2)]);
    }

    internal static IReadOnlyList<string> FindChangedSurveyVisits(AutoPlaySurveyBaseline baseline, IReadOnlyDictionary<string, AutoPlayVisitGuard> currentGuards, Func<int, string> readProgramHash, Func<IReadOnlyList<NativePersistentPredicateRead>, string?> readFingerprint, bool recordedTravel = false, string? currentCanonicalProgressFingerprint = null)
    {
        if (baseline.Version != 1)
        {
            throw new InvalidDataException("The field survey baseline has an unsupported version.");
        }
        if (recordedTravel && baseline.CanonicalProgressFingerprint is string canonical && canonical == currentCanonicalProgressFingerprint)
        {
            // A recorded route has already activated this path. Its replay does not restart a survey of global state combinations.
            return [];
        }
        List<string> changed = [];
        foreach ((string key, AutoPlayVisitGuard previous) in baseline.Guards.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!currentGuards.TryGetValue(key, out AutoPlayVisitGuard? current) || previous.Version != AutoPlayVisitGuard.CurrentVersion || current.Version != AutoPlayVisitGuard.CurrentVersion || previous.FieldId != current.FieldId || previous.ProgramHash != current.ProgramHash || !previous.Reads.SequenceEqual(current.Reads) || readProgramHash(previous.FieldId) != previous.ProgramHash || readFingerprint(previous.Reads) is not string fingerprint || fingerprint != previous.Fingerprint)
            {
                changed.Add(key);
            }
        }
        return changed;
    }
}
