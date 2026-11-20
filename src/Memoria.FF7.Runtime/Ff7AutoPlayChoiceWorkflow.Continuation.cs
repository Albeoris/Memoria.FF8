using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    private string? PreferActualContinuation(Ff7AutoPlayChoiceState choice, AutoPlayChoiceCoverage coverage, Ff7SessionSave current)
    {
        if (nativeSession is null || choice.BeforeActive is not { Accepted: true } before || state.Active is not { Accepted: true, Returned: true } actual || actual.Entity != before.Entity || actual.Script != before.Script || actual.EventId != before.EventId || actual.Generation != before.Generation || coverage.BeforeCheckpoint is not string sourcePath || coverage.ContinuationCheckpoint is not string primaryPath)
        {
            return null;
        }
        try
        {
            using FileStream sourceInput = File.OpenRead(sourcePath);
            CombinedSessionSave source = CombinedSessionCodec.Read(sourceInput);
            using FileStream primaryInput = File.OpenRead(primaryPath);
            CombinedSessionSave primary = CombinedSessionCodec.Read(primaryInput);
            FieldHostRequest? owner = source.Ff7?.Vm.PendingRequests?.SingleOrDefault(request => request.Command is FieldDialogueCommand { FirstChoice: not null } && request.Entity == before.Entity && request.Priority == 1);
            if (owner is null || primary.Ff7 is null || source.Ff7 is null || source.Ff7.FieldGeneration != current.FieldGeneration || source.Ff7.FieldName != current.FieldName)
            {
                return null;
            }
            FieldChoiceServiceProof proof = nativeSession.ReadChoiceServiceProof(owner.Entity, owner.ProgramCounter);
            if (!ActualContinuationDominates(primary.Ff7, current, proof))
            {
                return null;
            }
            return PublishActualContinuation(state, choice, coverage, current, primary, Write, path =>
            {
                using FileStream candidateInput = File.OpenRead(path);
                return CombinedSessionCodec.Read(candidateInput);
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    internal static string? PublishActualContinuation(Ff7AutoPlayState state, Ff7AutoPlayChoiceState choice, AutoPlayChoiceCoverage coverage, Ff7SessionSave current, CombinedSessionSave primary, Func<string, Ff7SessionSave, string> write, Func<string, CombinedSessionSave> read)
    {
        if (coverage.ActiveOption is not int option || coverage.ContinuationCheckpoint is not string primaryPath)
        {
            return null;
        }
        string candidatePath = write(choice.Key + "-candidate-" + option, current);
        state.Choice = choice with { RetiredCandidatePaths = [.. choice.RetiredCandidatePaths ?? [], candidatePath] };
        CombinedSessionSave candidate = read(candidatePath);
        if (!SameCombinedGuestState(primary, candidate) || candidate.Ff7 is null || !Same(current with { AutoPlay = null, AudioPresentation = null }, candidate.Ff7 with { AutoPlay = null, AudioPresentation = null }))
        {
            return null;
        }
        state.Choice = choice with { ContinuationActive = state.Active, RetiredCandidatePaths = [.. choice.RetiredCandidatePaths ?? [], primaryPath] };
        coverage.ContinuationCheckpoint = candidatePath;
        return candidatePath;
    }

    internal static bool SameCombinedGuestState(CombinedSessionSave first, CombinedSessionSave second) => Same(first with { Ff7 = null }, second with { Ff7 = null });

    private static bool Same<T>(T first, T second) => JsonSerializer.SerializeToUtf8Bytes(first).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(second));

    internal static bool ActualContinuationDominates(Ff7SessionSave primary, Ff7SessionSave candidate, FieldChoiceServiceProof proof)
    {
        if (!proof.Complete || primary.FieldName != candidate.FieldName || primary.FieldGeneration != candidate.FieldGeneration || primary.ResourceProfile != candidate.ResourceProfile || primary.Vm.ProgramIdentity != candidate.Vm.ProgramIdentity || primary.World is not null || candidate.World is not null || primary.Battle is not null || candidate.Battle is not null || primary.Process.GlobalCommand != Ff7GlobalCommand.None || candidate.Process.GlobalCommand != Ff7GlobalCommand.None || primary.Process.PlayerLocked || candidate.Process.PlayerLocked || primary.Process.Join is not null || candidate.Process.Join is not null || primary.Process.FadeState is { Completed: false } || candidate.Process.FadeState is { Completed: false } || primary.Process.MusicTransition is not null || candidate.Process.MusicTransition is not null || (primary.Progress.Clock?.CountdownSeconds ?? 0) != 0 || (candidate.Progress.Clock?.CountdownSeconds ?? 0) != 0 || candidate.Progress.Gil <= primary.Progress.Gil)
        {
            return false;
        }
        if (primary.Progress.Characters.Length != candidate.Progress.Characters.Length)
        {
            return false;
        }
        List<Ff7CharacterState> normalized = [];
        foreach (Ff7CharacterState character in primary.Progress.Characters)
        {
            Ff7CharacterState? other = candidate.Progress.Characters.SingleOrDefault(item => item.Id == character.Id);
            if (other is null || other.HitPoints < character.HitPoints || other.MagicPoints < character.MagicPoints || other.NativeRecord.Length != character.NativeRecord.Length || other.NativeRecord.Length < 60)
            {
                return false;
            }
            byte[] record = other.NativeRecord.ToArray();
            character.NativeRecord.AsSpan(44, 2).CopyTo(record.AsSpan(44, 2));
            character.NativeRecord.AsSpan(48, 2).CopyTo(record.AsSpan(48, 2));
            if (!record.AsSpan().SequenceEqual(character.NativeRecord))
            {
                return false;
            }
            normalized.Add(other with { HitPoints = character.HitPoints, MagicPoints = character.MagicPoints, NativeRecord = record });
        }
        Ff7Progress projected = candidate.Progress with { Characters = normalized.ToArray(), Gil = primary.Progress.Gil, Clock = primary.Progress.Clock, FieldRandomIndex = primary.Progress.FieldRandomIndex };
        if (!Same(primary.Progress, projected))
        {
            return false;
        }
        byte[] persistent = candidate.Vm.PersistentVariables.ToArray();
        primary.Vm.PersistentVariables.AsSpan(16, 4).CopyTo(persistent.AsSpan(16, 4));
        if (!persistent.AsSpan().SequenceEqual(primary.Vm.PersistentVariables))
        {
            return false;
        }
        HashSet<int> scratch = proof.ScratchMapBytes.Concat(proof.PresentationMapBytes).ToHashSet();
        if (primary.Vm.MapVariables.Length != candidate.Vm.MapVariables.Length)
        {
            return false;
        }
        for (int index = 0; index < primary.Vm.MapVariables.Length; index++)
        {
            if (primary.Vm.MapVariables[index] != candidate.Vm.MapVariables[index] && !scratch.Contains(index))
            {
                return false;
            }
        }
        if (primary.Vm.Entities.Length != candidate.Vm.Entities.Length || primary.Process.Actors.Length != candidate.Process.Actors.Length)
        {
            return false;
        }
        if (candidate.Process.Actors.Any(actor => primary.Process.Actors.Count(item => item.Slot == actor.Slot) != 1))
        {
            return false;
        }
        for (int entity = 0; entity < primary.Vm.Entities.Length; entity++)
        {
            var first = primary.Vm.Entities[entity];
            var second = candidate.Vm.Entities[entity];
            if (first.ModelBound != second.ModelBound || first.PartyCharacter != second.PartyCharacter || first.Priority != second.Priority || !first.SavedProgramCounters.AsSpan().SequenceEqual(second.SavedProgramCounters) || !Same(first.SyncStates, second.SyncStates) || !Same(first.SyncOwners, second.SyncOwners) || !proof.PresentationEntities.Contains(entity) && !Same(first, second))
            {
                return false;
            }
        }
        Ff7Actor[] actors = candidate.Process.Actors.Select(actor => actor with { BlinkCountdown = primary.Process.Actors.Single(item => item.Slot == actor.Slot).BlinkCountdown }).ToArray();
        Ff7FieldProcess process = candidate.Process with { Tick = primary.Process.Tick, BlinkRandomIndex = primary.Process.BlinkRandomIndex, Actors = actors, Windows = primary.Process.Windows, BackgroundStates = primary.Process.BackgroundStates, Fade = primary.Process.Fade, FadeState = primary.Process.FadeState, MusicTicks = primary.Process.MusicTicks, Audio = primary.Process.Audio, SoundVolumeClockSubmillisecondTicks = primary.Process.SoundVolumeClockSubmillisecondTicks };
        if (!Same(primary.Process, process) || !Same(primary.Vm.Input, candidate.Vm.Input) || !Same(primary.Vm.PendingRequests, candidate.Vm.PendingRequests))
        {
            return false;
        }
        Ff7SessionSave native = candidate with { Progress = primary.Progress, Process = primary.Process, Vm = primary.Vm, AutoPlay = null, EventCoverage = null, AudioPresentation = primary.AudioPresentation };
        if (!Same(primary with { AutoPlay = null, EventCoverage = null }, native))
        {
            return false;
        }
        return true;
    }
}
