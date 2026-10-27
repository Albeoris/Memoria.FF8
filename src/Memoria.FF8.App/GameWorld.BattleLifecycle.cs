using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private bool observingBattle;

    internal static BattleSession CreateLiveBattleSession(BattleDefinition definition, BattleAtbMode mode)
    {
        BattleSession active = new(definition, mode);
        if (definition.NativeGraphics is null && definition.NativeSupportRules is not null)
        {
            active.CompleteComponentBattleEntry();
        }
        return active;
    }

    private void ObservePendingBattle()
    {
        if (observingBattle)
        {
            return;
        }
        observingBattle = true;
        try
        {
            ObserveBattleOccurrence();
            if (Events.PendingHostedBattle is { Backend: NativeBattleBackend.AutomaticVictory } && session.State.PendingBattleEncounter is { } occurrence)
            {
                BattleResult result = CaptureAutomaticBattleOutcome(occurrence);
                Events.ReturnFromHostedBattle(0, () => ApplyBattleResult(occurrence, result));
            }
        }
        finally
        {
            observingBattle = false;
        }
    }

    private void ObserveBattleOccurrence()
    {
        if (session.State.PendingBattleEncounter is not null || battle?.Outcome == BattleOutcome.Defeat)
        {
            return;
        }
        NativeBattleCompletion? scripted = Events.PendingHostedBattle;
        RandomEncounterRequest? random = session.State.RandomEncounters?.Pending;
        if (scripted is null && random is null)
        {
            return;
        }
        int formation = scripted?.Encounter ?? random!.Formation;
        SceneKind scene = scripted is null ? random!.Scene : SceneKind.Field;
        int field = scripted?.FieldId ?? random!.FieldId;
        long frame = scripted?.Frame ?? random!.Frame;
        string source = scripted?.ProgramHash ?? random!.SourceHash;
        ScriptedBattleOwner? owner = scripted is null ? null : new(scripted.FieldId, scripted.ObjectId, scripted.ScriptId, scripted.Operation, scripted.Flags, scripted.ProgramHash);
        BattleEncounterOccurrence occurrence = new(Guid.NewGuid().ToString("N"), formation, BattleResources.FormationSourceHash(formation), scene, field, frame, source, owner);
        if (scene == SceneKind.WorldMap)
        {
            RecordNativeWorldBattleDeparture(random!);
        }
        session.SetPendingBattleEncounter(occurrence);
    }

    private void CompleteBattle(BattleSession active, IReadOnlyDictionary<int, int>? guardianForceLearning = null)
    {
        BattleEncounterOccurrence occurrence = session.State.PendingBattleEncounter ?? throw new InvalidOperationException("A live battle has no owned encounter occurrence.");
        RequireNativeBattleReturnBranch(occurrence.SourceScene, occurrence.ScriptedOwner is not null);
        PublishBattleSnapshot(active);
        BattleSnapshot completed = session.CompleteLiveBattle();
        BattleResult result = CaptureBattleOutcome(active, occurrence, completed);
        if (active.NativeResult == 1 && !Events.ReadNativeDefeatReturnBranch4706B0().ResumePreviousScene)
        {
            int callbacks = checked((int)(active.NativeGraphics is null ? active.Frame : active.NativeHostCallbacks!.Value));
            NativeBattleAbandonment abandoned = occurrence.SourceScene == SceneKind.Field
                ? Events.CaptureNativeBattleAbandonment(occurrence, callbacks)
                : new(occurrence, Events.ReadNativeDefeatReturnBranch4706B0(), active.EntryClockCalls ?? throw new NotSupportedException("World defeat requires its actual entry clock."), session.ExamTimer!.ClockCalls, callbacks);
            abandoned.Validate();
            Events.RequireNativeBattleAbandonment(abandoned);
            ApplyBattleResult(occurrence, result, guardianForceLearning);
            Events.CommitNativeBattleAbandonment(abandoned);
            session.EnterNativeDefeatField75(abandoned);
            battle = null;
            worldScene?.Dispose();
            worldScene = null;
            BeforeFieldAllocation?.Invoke(this);
            LoadField(75, null, null);
            return;
        }
        if (occurrence.SourceScene == SceneKind.WorldMap)
        {
            bool menuBattle = Events.PendingMenuBattle is not null;
            NativeWorldBattleOutcome physical = new(occurrence, active.NativeResult!.Value, active.EntryClockCalls ?? throw new NotSupportedException("World outcome requires its actual entry clock."), session.ExamTimer!.ClockCalls) { DefeatBranch = active.NativeResult == 1 ? Events.ReadNativeDefeatReturnBranch4706B0() : null };
            Events.RequireNativeWorldBattleOutcome(physical);
            Action publishWorld = PrepareNativeWorldBattleReturn(active, occurrence);
            ApplyBattleResult(occurrence, result, guardianForceLearning);
            publishWorld();
            Events.PublishNativeWorldBattleOutcome(physical);
            session.SetEventState(Events.Capture());
            if (session.State.DepartingFieldState is not null)
            {
                session.SetDepartingFieldState(Events.Capture());
            }
            if (menuBattle)
            {
                Events.ReturnFromNativeMenuBattle(checked((int)(active.NativeGraphics is null ? active.Frame : active.NativeHostCallbacks!.Value)), active.NativeResult!.Value, () => { });
                session.SetEventState(Events.Capture());
                if (session.State.DepartingFieldState is not null)
                {
                    session.SetDepartingFieldState(Events.Capture());
                }
            }
            else
            {
                RandomEncounterSessionState encounters = session.State.RandomEncounters ?? throw new InvalidOperationException("World outcome lost its playthrough owner.");
                session.SetRandomEncounters(encounters with { Pending = null });
            }
            return;
        }
        NativeBattleRetainedMemory? reloadedMemory = null;
        NativeBattleFieldResourceReloadResult? registration = null;
        FieldAssets? reloadedField = null;
        if (occurrence.SourceScene == SceneKind.Field)
        {
            FieldAssets currentField = Field ?? throw new NotSupportedException("Native kind3 requires its current original field resources.");
            NativeBattleRetainedMemory retained = session.State.NativeBattleMemory ?? throw new NotSupportedException("Native kind3 requires its actual retained battle process memory.");
            registration = Events.ExecuteNativeBattleFieldResourceReload(currentField.NativeRegistration ?? throw new NotSupportedException("Native kind3 requires the current original field ONE/MCH registration inputs."), active.NativeResult ?? throw new InvalidOperationException("Active battle has no native return result."), retained, active.CurrentCrtRandom);
            reloadedMemory = registration.Memory;
            Events.ValidateNativeBattleFieldRegistrationMemory(reloadedMemory);
            reloadedField = assets.BindNativeRegisteredTextures(currentField, Events.Scene.NativeFieldRegistration ?? throw new InvalidDataException("Native kind3 did not publish its actual registered field resource addresses."), reloadedMemory.EffectMemory?.Textures ?? throw new NotSupportedException("Native kind3 requires its retained texture memory."), reloadedMemory.FieldTextureAnimations);
            _ = Events.RequireNativeBattleFieldReload(active.NativeResult ?? throw new InvalidOperationException("Active battle has no native return result."));
        }
        if (Events.PendingMenuBattle is not null)
        {
            Events.ReturnFromNativeMenuBattle(checked((int)(active.NativeGraphics is null ? active.Frame : active.NativeHostCallbacks!.Value)), active.NativeResult!.Value, () =>
            {
                ApplyBattleResult(occurrence, result, guardianForceLearning);
                EventEngine.PreparedNativeBattleFieldReturn prepared = Events.PrepareNativeBattleFieldReturn(active.NativeMusicProcess ?? throw new NotSupportedException("Native menu battle return requires its live physical music owner."), active.EntryFlags, active.NativeResult!.Value, BattleMusicEntryResources, reloadedMemory, registration?.CrtRandomBefore, registration?.CrtRandomAfter);
                Events.CommitNativeBattleFieldReturn(prepared);
            });
            PublishReloadedBattleField(reloadedField!);
            //471F70 follows the kind3 resume tail with a complete4767B0 field callback.
            Events.Step();
        }
        else if (occurrence.ScriptedOwner is not null)
        {
            Events.ReturnFromInteractiveBattle(checked((int)(active.NativeGraphics is null ? active.Frame : active.NativeHostCallbacks!.Value)), () =>
            {
                ApplyBattleResult(occurrence, result, guardianForceLearning);
                EventEngine.PreparedNativeBattleFieldReturn prepared = Events.PrepareNativeBattleFieldReturn(active.NativeMusicProcess ?? throw new NotSupportedException("Native battle return requires its live physical music owner."), active.EntryFlags, active.NativeResult!.Value, BattleMusicEntryResources, reloadedMemory, registration?.CrtRandomBefore, registration?.CrtRandomAfter);
                Events.CommitNativeBattleFieldReturn(prepared);
            }, active.NativeResult!.Value);
            PublishReloadedBattleField(reloadedField!);
        }
        else
        {
            Events.ReturnFromNativeRandomFieldBattle(checked((int)(active.NativeGraphics is null ? active.Frame : active.NativeHostCallbacks!.Value)), active.NativeResult!.Value, () =>
            {
                ApplyBattleResult(occurrence, result, guardianForceLearning);
                EventEngine.PreparedNativeBattleFieldReturn prepared = Events.PrepareNativeBattleFieldReturn(active.NativeMusicProcess ?? throw new NotSupportedException("Native battle return requires its live physical music owner."), active.EntryFlags, active.NativeResult.Value, BattleMusicEntryResources, reloadedMemory, registration?.CrtRandomBefore, registration?.CrtRandomAfter);
                Events.CommitNativeBattleFieldReturn(prepared);
            });
            PublishReloadedBattleField(reloadedField!);
            RandomEncounterSessionState encounters = session.State.RandomEncounters ?? throw new InvalidOperationException("Random outcome lost its playthrough owner.");
            session.SetRandomEncounters(encounters with { Pending = null });
            //Release the encounter owner before the resumed callback checks for a pending battle.
            Events.Step();
        }
    }

    internal static void RequireNativeBattleReturnBranch(SceneKind source, bool scripted)
    {
        if (source is not (SceneKind.Field or SceneKind.WorldMap) || source == SceneKind.WorldMap && scripted)
        {
            throw new NotSupportedException("Native world battle return requires its actual world reload/music producer before outcomes can be published.");
        }
    }

    private void CompleteNativeDefeatIfReady()
    {
        if (diagnosticBattle is not null)
        {
            return;
        }
        if (battle is { Outcome: BattleOutcome.Defeat, NativeResult: 1 } active && active.NativeGraphics?.NativeOutcome?.FadeDurationB0C == 0)
        {
            CompleteBattle(active);
            battle = null;
            PublishBattleSnapshot(null);
        }
    }

    private void PublishReloadedBattleField(FieldAssets field)
    {
        Field = field;
        renderer = new FieldRenderer(field, renderWidth, renderHeight);
    }
}
