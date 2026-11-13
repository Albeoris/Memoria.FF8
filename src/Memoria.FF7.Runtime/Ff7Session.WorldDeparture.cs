using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private List<Action>? fieldEntryEffects;
    internal Action<ushort>? ValidateWorldFieldDestination { get; set; }

    private void PublishFieldEntryEffect(Action effect)
    {
        if (fieldEntryEffects is { } pending)
        {
            pending.Add(effect);
        }
        else
        {
            effect();
        }
    }

    private void CompleteWorldDeparture(Ff7WorldRuntime source, Ff7WorldFieldDeparture request)
    {
        (FieldTransitionCommand entry, byte[] persistent) = source.PrepareFieldDeparture(request);
        if (entry.Map <= 64)
        {
            throw new NotSupportedException("FFVII world departure requires a field destination.");
        }
        long generation = checked(FieldGeneration + 1);
        var original = (Field, MapId, models, encounterTables, Host, Machine, Progress);
        List<Action> effects = [];
        fieldEntryEffects = effects;
        try
        {
            LoadField(entry.Map, new FieldVariables(persistent, new byte[256]), prepareHost: PrepareHost);
            ValidateWorldFieldDestination?.Invoke(entry.Map);
            World = null;
            fieldLoadIncomplete = true;
            Machine.Initialize();
            Host.InitializePlayerMovement(Field.Scripts.Scale);
            Host.InitializeBackgroundTriggers();
            if (entry.Triangle >= Field.Walkmesh.Count || !Host.Process.Actors.Any(actor => actor.PlayerControlled))
            {
                throw new InvalidDataException("FFVII world departure has no valid destination triangle or controlled actor.");
            }
            Host.PlacePlayerAtEntry(entry);
            Host.Process.Validate();
            Progress.Validate();
            _ = Machine.Capture();
        }
        catch
        {
            (Field, MapId, models, encounterTables, Host, Machine, Progress) = original;
            World = source;
            throw;
        }
        finally
        {
            fieldEntryEffects = null;
            fieldLoadIncomplete = false;
        }

        FieldGeneration = generation;
        dialogueTokens.Clear();
        Host.AudioRequested = original.Host.AudioRequested;
        Host.NativeSoundRequested = original.Host.NativeSoundRequested;
        Host.NativeSoundLoopAlreadyPlaying = original.Host.NativeSoundLoopAlreadyPlaying;
        Host.BeginBattleRequest = BeginFieldBattle;
        source.Dispose();
        foreach (Action effect in effects)
        {
            effect();
        }

        void PrepareHost(Ff7FieldHost host)
        {
            host.AudioRequested = command => effects.Add(() => original.Host.AudioRequested?.Invoke(command));
            host.NativeSoundRequested = request => effects.Add(() => original.Host.NativeSoundRequested?.Invoke(request));
            host.NativeSoundLoopAlreadyPlaying = null;
            host.BeginBattleRequest = _ => throw new NotSupportedException("FFVII world departure cannot prepare a field initializer that starts a battle.");
            host.SubmitNativeAudio(new(0xF1));
            host.ResetNativeSoundPitches();
        }
    }
}
