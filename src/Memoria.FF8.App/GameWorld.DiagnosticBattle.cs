using System.Diagnostics;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattleRequest([property: JsonRequired] int Encounter, [property: JsonRequired] byte Seed, [property: JsonRequired] ushort Flags, [property: JsonRequired] ushort FieldBattleMode, [property: JsonRequired] uint ElapsedCounter)
{
    public DiagnosticBattleDriverRecipe? Driver { get; init; }
    public byte? BattleMusic { get; init; }
    public int? PrimaryMusicChannel { get; init; }
    public ushort? MusicFieldWord { get; init; }
    public DiagnosticBattlePreparation? Fixture { get; init; }
    public DiagnosticTargetExpectation? Target { get; init; }
    public bool PrepareEntryBeforePresentation { get; init; }
    public int EntryCallbackBudget { get; init; } = 1024;
    public bool InstantGuardianForceCharge { get; init; } = true;
    public BattleAtbMode AtbMode { get; init; } = BattleAtbMode.TurnBased;
    public DiagnosticBattlePrelude? Prelude { get; init; }

    internal void Validate()
    {
        Driver?.Validate();
        Fixture?.Validate();
        Prelude?.Validate(this);
        if (!Enum.IsDefined(AtbMode))
        {
            throw new InvalidDataException("Diagnostic ATB mode must select an existing battle mode.");
        }
        if (EntryCallbackBudget is < 1 or > 18000)
        {
            throw new InvalidDataException("Diagnostic entry preparation requires a finite native callback budget of 1..18000.");
        }
        Target?.Validate();
        if (Encounter is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Diagnostic encounter must be an installed formation WORD.");
        }
        if ((Flags & 0x10) == 0 && (BattleMusic is null || PrimaryMusicChannel is null))
        {
            throw new NotSupportedException("Diagnostic music preparation requires explicit battle music BYTE203 and primary channel DWORD108, or the declared native keep-music flag.");
        }
    }
}

internal sealed record DiagnosticBattleReceipt(Guid BattleInstanceId, string InitializerVersion, string ExecutableSha256, DiagnosticBattleRequest Preparation, int Stage, ImmutableArray<string> OrderedStages, ImmutableArray<string> PresentOwners, double PreparationMilliseconds)
{
    public ImmutableArray<DiagnosticPreparationChange> PreparationChanges { get; init; } = [];
    public DiagnosticTargetReceipt? Target { get; init; }
    public DiagnosticEntryPreparationReceipt? EntryPreparation { get; init; }
    public bool InstantGuardianForceCharge { get; init; }
    public DiagnosticBattlePreludeReceipt? Prelude { get; init; }
}

internal sealed record DiagnosticEntryPreparationReceipt(long NativeCallbacks, double Milliseconds, string Scope);

internal sealed partial class GameWorld
{
    private sealed record DiagnosticBattleOwner(DiagnosticBattleRequest Request, Guid InstanceId);
    private DiagnosticBattleOwner? diagnosticBattle;
    internal DiagnosticBattleReceipt? DiagnosticReceipt { get; private set; }
    internal bool IsDiagnosticBattle => diagnosticBattle is not null;

    internal DiagnosticBattleRequest PrepareDiagnosticEntryBeforePresentation(DiagnosticBattleRequest request, Action<DiagnosticPreparationProgress>? progress = null)
    {
        BattleSession active = battle ?? throw new InvalidOperationException("Diagnostic entry preparation requires its live battle.");
        if (diagnosticBattle is null || (active.NativeHostCallbacks ?? 0) != 0 || Field is not null || worldScene is not null)
        {
            throw new InvalidOperationException("Entry preparation can run only before the diagnostic host's first presentation callback.");
        }
        long started = Stopwatch.GetTimestamp();
        DiagnosticPreparationProgressObserver observer = new(active, progress);
        observer.Report("entry", boundary: true);
        void RejectFocalWarmup(BattleDiagnosticEffectDispatch dispatch)
        {
            if (request.Driver is { } driver && dispatch.RequestedEffect == driver.ExpectedEffect)
            {
                throw new NotSupportedException("Entry preparation cannot execute or warm the focal effect before its measured first invocation.");
            }
        }
        active.DiagnosticEffectDispatched += RejectFocalWarmup;
        try
        {
            if (request.Prelude is not null)
            {
                request = PrepareDiagnosticPrelude(request, active, observer);
            }
            bool Ready()
            {
                if (!active.NativeEntryReady || active.ObserveDiagnosticDrainBlockers().Count != 0 || request.Target is not null && VerifyDiagnosticTarget() is null)
                {
                    return false;
                }
                return request.Driver is null || RecreateGame.DiagnosticCommandReady(active, request.Driver);
            }
            int callbacks = 0;
            while (!Ready() && callbacks < request.EntryCallbackBudget)
            {
                active.Tick();
                callbacks++;
                observer.Report("entry");
            }
            observer.Report("entry", boundary: true);
            if (!Ready())
            {
                object facts = new
                {
                    Callbacks = callbacks,
                    active.NativeEntryReady,
                    active.Outcome,
                    DrainBlockers = active.ObserveDiagnosticDrainBlockers(),
                    Target = request.Target is { } target ? active.ObserveDiagnosticTarget(target.ActorId) : null,
                    CommandReady = request.Driver is null || RecreateGame.DiagnosticCommandReady(active, request.Driver),
                    Actors = active.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, actor.IsReady, actor.IsBusy, actor.NativeCombatantFlags, actor.NativeAtb }).ToArray(),
                    Owners = active.ObserveDiagnosticInvocationOwners()
                };
                throw new NotSupportedException("Diagnostic entry did not initialize its original owners within the declared callback budget. " + JsonSerializer.Serialize(facts));
            }
            SynchronizeNativeBattleHostCallbacks(active);
            DiagnosticReceipt = DiagnosticReceipt! with { EntryPreparation = new(active.NativeHostCallbacks ?? throw new NotSupportedException("Entry preparation lost its native callback ledger."), Stopwatch.GetElapsedTime(started).TotalMilliseconds, "Original entry/declared target/command readiness callbacks before presentation; measured focal executions retain normal real-time pacing.") };
        }
        finally
        {
            active.DiagnosticEffectDispatched -= RejectFocalWarmup;
        }
        return request;
    }

    internal BattleSession StartDiagnosticBattle(DiagnosticBattleRequest request)
    {
        request.Validate();
        if (battle is not null || diagnosticBattle is not null || Field is not null || worldScene is not null || session.State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame || session.State.EventState is not null || HasPendingBattle)
        {
            throw new InvalidOperationException("Direct diagnostic entry requires a canonical New Game process before exploration allocation.");
        }
        if (session.ExamTimer is null || session.State.CharacterVitals is null || session.State.InputConfiguration is null || session.State.Economy is null)
        {
            throw new NotSupportedException("Diagnostic initialization requires canonical New Game timer, character, input and economy owners before battle allocation.");
        }
        long started = Stopwatch.GetTimestamp();
        NativeBattleRetainedMemory retained = session.CurrentNativeBattleMemory ?? throw new NotSupportedException("Diagnostic initialization requires installed cold-process retained memory.");
        string executable = retained.ColdExecutableSha256 ?? throw new NotSupportedException("Diagnostic initialization requires an authenticated executable identity.");
        DiagnosticPreparationResult? characters = request.Fixture is null ? null : PrepareDiagnosticCharacters(session.State.CharacterVitals, request.Fixture, request.Fixture.Characters.Any(character => character.Level.HasValue) ? assets.ReadNativeBattleExperienceDefinition() : null);
        DiagnosticInventoryPreparationResult? inventory = request.Fixture is null ? null : PrepareDiagnosticInventory(session.State.Economy, request.Fixture);
        DiagnosticLimitPreparationResult? limits = request.Fixture?.Limits is { } limitRecipe ? PrepareDiagnosticLimits(session.State.LimitProgress ?? throw new NotSupportedException("Diagnostic Limit preparation requires the actual initialized saved progress owner."), limitRecipe) : null;
        Events.InitializeNewGameMusicOwners(assets.NativeRuntimeProfile ?? throw new NotSupportedException("Diagnostic music initialization requires the authenticated native runtime profile."));
        PreparedNativeBattleMusicEntry preparedMusic = Events.PrepareNativeDiagnosticBattleMusicEntry(checked((ushort)request.Encounter), request.Flags, BattleMusicEntryResources, request.BattleMusic, request.PrimaryMusicChannel, request.MusicFieldWord, retained.MusicProcess?.ReturnChannel);
        diagnosticBattle = new(request, Guid.NewGuid());
        try
        {
            if (characters is not null)
            {
                session.SetCharacterVitals(characters.Vitals);
            }
            if (inventory is not null)
            {
                session.SetEconomy(inventory.Economy);
            }
            if (limits is not null)
            {
                session.SetLimitProgress(limits.Progress);
            }
            PreparedBattleEntry prepared = PrepareBattleEntry(request.Encounter, request.Flags, request.Seed, request.AtbMode, preparedMusic);
            prepared.Battle.ConfigureDiagnosticScheduling(request.Fixture?.HoldEnemyCommands ?? false, request.InstantGuardianForceCharge, request.Prelude?.Input.ActorId ?? request.Driver?.Input.ActorId);
            if (request.Driver?.TerminalHold is { } terminalHold)
            {
                prepared.Battle.ConfigureDiagnosticTerminalHold(terminalHold);
            }
            battle = CommitPreparedBattleEntry(prepared);
            battle.DiagnosticEffectDispatched += ObserveDiagnosticOpening;
            battle.DiagnosticEventPublished += ObserveDiagnosticOpeningCompletion;
            DiagnosticReceipt = new(diagnosticBattle.InstanceId, "direct-new-game-v2", executable, request, battle.EntryStageId ?? throw new NotSupportedException("Diagnostic entry requires the actual encounter stage owner."), ["installed-cold-process", "hardcoded-new-game", "resource-derived-new-game", "explicit-diagnostic-field-inputs", "declarative-character-preparation", "declarative-inventory-preparation", "declarative-limit-preparation", "native-music-entry", "canonical-encounter-construction", "lazy-effect-resolver", "diagnostic-publication"], ["retained-memory", "music", "global-sound", "exam-timer", "character-vitals", "input-configuration", "economy"], Stopwatch.GetElapsedTime(started).TotalMilliseconds) { PreparationChanges = [.. characters?.Changes ?? [], .. inventory?.Changes ?? [], .. limits?.Changes ?? []] };
            DiagnosticReceipt = DiagnosticReceipt with { InstantGuardianForceCharge = request.InstantGuardianForceCharge };
            return battle;
        }
        catch
        {
            diagnosticBattle = null;
            throw;
        }
    }

    private int ApplyDiagnosticBattleInput(BattleInput input)
    {
        if (input.Kind is BattleInputKind.Return or BattleInputKind.StartEncounter or BattleInputKind.ConfigureEncounters or BattleInputKind.ConfigurePlaythrough or BattleInputKind.InstantVictory or BattleInputKind.InstantDefeat or BattleInputKind.AdvanceTurn or BattleInputKind.SetMode)
        {
            throw new NotSupportedException("Diagnostic battles preserve the live encounter and normal action admission; this lifecycle or accelerated input is unavailable.");
        }
        BattleSession active = battle ?? throw new InvalidOperationException("Diagnostic battle has no live session.");
        int advanced = ExecuteBattleCommand(active, input);
        PublishBattleSnapshot(active);
        return advanced;
    }

    internal void AbandonFaultedDiagnosticBattle()
    {
        if (diagnosticBattle is null)
        {
            throw new InvalidOperationException("Fault abandonment requires an active diagnostic battle owner.");
        }
        diagnosticAllocationFaulted = true;
        session.AbandonSnapshotPreparation(snapshotPreparation);
    }

    internal void EndDiagnosticBattle()
    {
        if (diagnosticBattle is null)
        {
            throw new InvalidOperationException("No diagnostic battle lifecycle is active.");
        }
        // Isolated teardown releases the live battle without applying exploration rewards or progress.
        if (battle is not null)
        {
            battle.DiagnosticEffectDispatched -= ObserveDiagnosticOpening;
            battle.DiagnosticEventPublished -= ObserveDiagnosticOpeningCompletion;
        }
        battle = null;
        diagnosticBattle = null;
        if (diagnosticAllocationFaulted)
        {
            return;
        }
        session.SetLiveBattle(null);
        session.Restore(session.State with { Scene = SceneKind.Title });
    }
}
