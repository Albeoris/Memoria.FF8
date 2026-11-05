using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record AutoPlayNavigationExcursion(string Checkpoint, string SourceNavigation, string SourceVisit, int SourceField, string EventId, string TargetId, int ObjectId, string ConditionSignature, int Story, NativePersistentPredicateRead[] Reads, string PersistentFingerprint, string ProgressFingerprint, bool Departed = false, bool Issued = false);
internal sealed record AutoPlayNavigationExcursionBlock(string SourceNavigation, string SourceVisit, string EventId, string TargetId, string ConditionSignature);

internal sealed partial class AutoPlayController
{
    internal static bool HasEmptyNavigationExcursionChoicePath(AutoPlayRunState state)
    {
        return state.SceneChoices.Count == 0 && state.PrimaryContinuation is null && state.InputSearch is not { Closed: false } && state.Coverage.Choices.All(choice => choice.ActiveOption is null) && (state.ActiveEvent is null || state.ActiveSelections is { Count: 0 });
    }

    internal static string? NavigationExcursionProgressFingerprint(GameState state)
    {
        SortedDictionary<int, byte> persistent = [];
        foreach ((int address, NativeMemoryValue cell) in state.EventMemory.Cells)
        {
            if (address >= 1024 || address + cell.Width <= 256)
            {
                continue;
            }
            if (!cell.Value.IsExact)
            {
                return null;
            }
            uint value = unchecked((uint)cell.Value.RequireExact());
            for (int offset = 0; offset < cell.Width; offset++)
            {
                int target = address + offset;
                byte lane = unchecked((byte)(value >> (offset * 8)));
                if (target >= 256 && target < 1024 && lane != 0)
                {
                    persistent[target] = lane;
                }
            }
        }
        JsonElement? partyIdentity = null;
        if (state.EventState is { } events && events.TryGetProperty("Scene", out JsonElement scene) && scene.TryGetProperty("Party", out JsonElement party))
        {
            partyIdentity = party;
        }
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(new { Persistent = persistent, state.WorldEntryMemory, state.CharacterVitals, state.Economy, state.CardInventory, state.LimitProgress, state.WeaponRecipes, state.ShotAvailability, state.SavedBattleFlags, state.AppliedBattleResultIds, state.KnownMagic, state.ScannedModels, state.ChocoboRewards, state.PendingEntityNames, Party = partyIdentity }, BeforeSnapshotJsonOptions);
        return Convert.ToHexString(SHA256.HashData(identity));
    }

    private void BeginNavigationExcursion(GameWorld world, AutoPlayContactSource source, string checkpoint)
    {
        if (run.NavigationExcursion is { Departed: true } || !HasEmptyNavigationExcursionChoicePath(run) || !world.HasCurrentFieldAllocation || !world.CanCacheAutoPlayNavigationSource(source.Target.CoverageId, source.Target.ObjectId))
        {
            return;
        }
        EventPredicateState predicates = world.ReadAutoPlayConditions(source.Target.CoverageId, source.Target.ObjectId);
        if (predicates.DeferredOperations.Count != 0 || predicates.Fingerprint != source.ConditionSignature)
        {
            return;
        }
        NativePersistentPredicateRead[] reads = predicates.Values.Select(value => new NativePersistentPredicateRead(value.Opcode, value.Address, value.Width)).Distinct().ToArray();
        string? persistent = world.Events.ReadPersistentPredicateFingerprint(reads);
        string? progress = NavigationExcursionProgressFingerprint(session.State);
        if (persistent is null || progress is null)
        {
            return;
        }
        run.NavigationExcursion = new(checkpoint, source.NavigationKey, source.VisitKey, source.FieldId, source.Target.CoverageId, source.Target.Id, source.Target.ObjectId, source.ConditionSignature, source.Story, reads, persistent, progress);
    }

    private void ConfirmNavigationExcursionSource(AutoPlayContactSource source, string checkpoint)
    {
        if (run.NavigationExcursion is { } excursion && excursion.Checkpoint == checkpoint && excursion.SourceNavigation == source.NavigationKey && excursion.TargetId == source.Target.Id)
        {
            run.NavigationExcursion = excursion with { Issued = true };
        }
    }

    private void ObserveNavigationExcursion(GameWorld world, string navigation)
    {
        if (run.NavigationExcursion is not { } excursion)
        {
            return;
        }
        planner.SetNavigationExcursionBlocks(run.NavigationExcursionBlocks);
        bool canReturn = navigation != excursion.SourceNavigation && planner.CanReachNavigation(navigation, excursion.SourceNavigation, vertex => IsNavigationResourceCurrent(world, vertex));
        ObserveNavigationExcursionBoundary(run, session.State.Variables.GetValueOrDefault(256), navigation, canReturn);
    }

    internal static void ObserveNavigationExcursionBoundary(AutoPlayRunState state, int story, string navigation, bool canReturn)
    {
        if (state.NavigationExcursion is not { } excursion)
        {
            return;
        }
        if (!excursion.Issued || story != excursion.Story || !HasEmptyNavigationExcursionChoicePath(state))
        {
            state.NavigationExcursion = null;
            return;
        }
        if (navigation == excursion.SourceNavigation)
        {
            if (excursion.Departed)
            {
                state.NavigationExcursion = null;
            }
            return;
        }
        state.NavigationExcursion = canReturn ? null : excursion with { Departed = true };
    }

    internal static bool HasUnchangedNavigationExcursionProgress(AutoPlayNavigationExcursion excursion, GameState current, string? persistentFingerprint)
    {
        return excursion.Issued && excursion.Departed && current.Variables.GetValueOrDefault(256) == excursion.Story && NavigationExcursionProgressFingerprint(current) == excursion.ProgressFingerprint && persistentFingerprint == excursion.PersistentFingerprint;
    }

    private bool TryReturnNavigationExcursion(GameWorld world, string navigation)
    {
        if (run.NavigationExcursion is not { Departed: true } excursion || !HasEmptyNavigationExcursionChoicePath(run))
        {
            return false;
        }
        if (session.State.Variables.GetValueOrDefault(256) != excursion.Story || !planner.CanReachNavigation(excursion.SourceNavigation, excursion.SourceNavigation, vertex => IsNavigationResourceCurrent(world, vertex)) || planner.CanReachNavigation(navigation, excursion.SourceNavigation, vertex => IsNavigationResourceCurrent(world, vertex)))
        {
            run.NavigationExcursion = null;
            return false;
        }
        GameState current = CaptureState();
        if (!HasUnchangedNavigationExcursionProgress(excursion, current, world.Events.ReadPersistentPredicateFingerprint(excursion.Reads)))
        {
            run.NavigationExcursion = null;
            return false;
        }
        GameState before = ReadCheckpoint(Path.Combine(runDirectory!, excursion.Checkpoint));
        if (before.FieldId != excursion.SourceField || before.Variables.GetValueOrDefault(256) != excursion.Story || NavigationExcursionProgressFingerprint(before) != excursion.ProgressFingerprint)
        {
            throw new InvalidDataException("The retained navigation excursion checkpoint no longer matches its exact source progress.");
        }
        try
        {
            restore(before);
            GameWorld source = getWorld();
            source.EnsureField();
            EventPredicateState predicates = source.ReadAutoPlayConditions(excursion.EventId, excursion.ObjectId);
            AutoPlayEventCoverage? item = run.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == excursion.SourceVisit)?.Events.SingleOrDefault(candidate => candidate.Event.Id == excursion.EventId);
            if (session.State.Scene != SceneKind.Field || session.State.FieldId != excursion.SourceField || VisitKey() != excursion.SourceVisit || CurrentNavigationKey(source, excursion.SourceVisit) != excursion.SourceNavigation || !source.Events.CanAcceptMovementInput || !source.CanCacheAutoPlayNavigationSource(excursion.EventId, excursion.ObjectId) || predicates.DeferredOperations.Count != 0 || predicates.Fingerprint != excursion.ConditionSignature || item?.Event.ConditionSignature != excursion.ConditionSignature || source.Events.ReadPersistentPredicateFingerprint(excursion.Reads) != excursion.PersistentFingerprint)
            {
                throw new NotSupportedException("Navigation excursion recovery requires its authentic available source component, event and unchanged native predicates.");
            }
        }
        catch
        {
            restore(current);
            throw;
        }
        AutoPlayNavigationExcursionBlock blocked = new(excursion.SourceNavigation, excursion.SourceVisit, excursion.EventId, excursion.TargetId, excursion.ConditionSignature);
        if (!run.NavigationExcursionBlocks.Contains(blocked))
        {
            run.NavigationExcursionBlocks.Add(blocked);
        }
        planner.SetNavigationExcursionBlocks(run.NavigationExcursionBlocks);
        ClearNativeUnavailableInteraction();
        UpdateExhaustedSurveyTarget(run, null);
        ResetIdleDeadline();
        ReproductionPath = SaveBundle($"excursion-return-{run.Steps:D10}", CaptureState());
        Status = "Restored the authenticated pre-transition checkpoint after exhausting a one-way navigation excursion without quest progress.";
        return true;
    }
}
