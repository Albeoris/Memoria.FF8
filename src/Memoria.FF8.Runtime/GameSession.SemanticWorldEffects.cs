namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    /// <summary>Executes retained primitive world effect causes without claiming a historical motor/contact callback.</summary>
    public NativeSemanticSalaryEffects ApplySemanticWorldEffect(ReplayJournalEntry input, Func<NativeSalaryResult, NativeSalaryPresentation?, NativeGlobalSoundProcess?, NativeSemanticSalaryEffects> preparePayment)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(preparePayment);
        input.Validate();
        if (input.Kind != ReplayEntryKind.PersistentEffect || input.Code != (int)ReplayPersistentEffectKind.WorldCallbackEffects || input.EffectArguments is null || input.Scene != SceneKind.WorldMap)
        {
            throw new NotSupportedException("This semantic world effect has no implemented current native producer; old results cannot be applied.");
        }
        ReplayWorldEffectCause cause = ReplayWorldEffectCause.FromArguments(input.EffectArguments);
        GameState departure = State;
        if (departure.Scene != SceneKind.WorldMap || input.FieldId != departure.FieldId || departure.World is not { } world || departure.ReplayJournal is null)
        {
            throw new NotSupportedException("Semantic world effects require the actual current allocation and journal ledger.");
        }
        byte entity = world.NativeFootRuntime?.EntityType ?? world.NativeCarRuntime?.EntityType ?? unchecked((byte)(departure.WorldArrival?.Mode ?? throw new NotSupportedException("World effects require their current allocated packet.")));
        bool normal = world.NativeFootRuntime is not null || world.NativeCarRuntime is not null;
        if (entity != cause.EntityType || normal != cause.SampleSalaryAndDrawPoints || (world.NativeCarRuntime is not null) != cause.FuelGroundType.HasValue || !normal && cause.MovementUnits != 0)
        {
            throw new NotSupportedException("Retained primitive world effects differ from the current native producer family.");
        }
        NativeWorldSalaryState effects = NativeWorldSalaryState.Begin(departure);
        for (int index = 0; index < input.Frames; index++)
        {
            effects = effects.WithGraphicsSpeed(cause.RawMagnitude);
            if (cause.FuelGroundType is byte ground)
            {
                effects = effects.PrepareCarFuel(entity, ground, cause.RawMagnitude);
            }
            if (normal)
            {
                effects = effects.PrepareDrawPointAging(cause.MovementUnits);
            }
            NativeSalaryResult? sample = normal ? SampleWorldSalary(departure, effects.SeeD, effects.Economy, cause.MovementUnits) : null;
            NativeSemanticSalaryEffects presentation;
            if (sample?.Payment is not null)
            {
                presentation = preparePayment(sample, effects.Presentation, effects.SoundProcess);
            }
            else
            {
                NativeSalaryPresentation? display = sample?.CloseDisplay == true ? effects.Presentation?.Close() ?? throw new NotSupportedException("Semantic salary closure requires its current UI process.") : effects.Presentation;
                presentation = new NativeSemanticSalaryEffects(display, effects.SoundProcess);
            }
            effects = effects.Advance(sample, presentation.Presentation?.Advance(cause.UiTicks), presentation.SoundProcess, cause.MovementUnits, cause.UiTicks);
        }
        ReplayJournalEntry committed = input with { FieldId = departure.FieldId, Frame = world.Frame, Receipts = [], SemanticAdmissions = [], WorldEffects = [], ResultScene = SceneKind.WorldMap, ResultFieldId = departure.FieldId, ResultFrame = world.Frame };
        GameState candidate = effects.ApplySemanticEffectsTo(departure with { WorldProcess = (departure.WorldProcess ?? throw new NotSupportedException("Semantic world effect callbacks require their current outer process clock.")).AdvanceMainLoopCallbacks(input.Frames), ExamTimer = departure.ExamTimer?.AdvanceCallbacks(NativeTimerCallbackKind.World, input.Frames), ReplayJournal = departure.ReplayJournal.Append(committed) }, input.Frames);
        Validate(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The world session changed while preparing semantic effect owners.");
        }
        State = candidate;
        ReplayJournalChanged?.Invoke(candidate.ReplayJournal!);
        return new NativeSemanticSalaryEffects(effects.Presentation, effects.SoundProcess);
    }

    private static long CountSemanticWorldEffectCallbacks(GameState state)
    {
        return state.ReplayJournal?.ActiveEntries.Where(entry => entry.Kind == ReplayEntryKind.PersistentEffect && entry.Scene == SceneKind.WorldMap && entry.Code == (int)ReplayPersistentEffectKind.WorldCallbackEffects && entry.EffectArguments is not null).Sum(entry => (long)entry.Frames) ?? 0;
    }
}

public sealed record NativeSemanticSalaryEffects(NativeSalaryPresentation? Presentation, NativeGlobalSoundProcess? SoundProcess);
