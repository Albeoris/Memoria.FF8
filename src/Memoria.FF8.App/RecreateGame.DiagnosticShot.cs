using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private byte diagnosticShotInitialLearnedFlags;

    private void BeginDiagnosticShot(BattleSession active, DiagnosticBattleDriver driver, BattleDiagnosticLimitSelectionReceipt selection)
    {
        DiagnosticShotCycleDeclaration declaration = diagnosticDriverRecipe!.ShotCycle!;
        string actorId = diagnosticDriverRecipe.Input.ActorId!;
        string targetId = diagnosticDriverRecipe.Input.TargetId!;
        BattleCombatant actor = active.Combatants.Single(combatant => combatant.Id == actorId);
        BattleCombatant target = active.Combatants.Single(combatant => combatant.Id == targetId);
        if (!actor.IsAlive || actor.ModelId != 2 || actor.NativeLimit is not { CharacterId: 2, Command: 14 } || !target.IsAlive || active.GetNativeShot(actorId) is not null)
        {
            throw new NotSupportedException("Shot diagnostics require the original living Irvine owner, a living declared target and a new legal Limit admission.");
        }
        BattleDefinition definition = active.Capture().Definition;
        byte crisis = active.GetNativeLimitLevel(actorId);
        ushort InstalledEffect(byte command)
        {
            BattleNativeAction[] actions = definition.NativeActions.Where(action => action.Command == command && action.ActionId == declaration.AmmoAction && (action.CrisisLevel is null || action.CrisisLevel == (command == 14 ? crisis : null))).ToArray();
            return actions.Length == 1 ? actions[0].MagicId : throw new NotSupportedException("Shot diagnostic cycle does not identify one original installed command action.");
        }
        if (InstalledEffect(14) != declaration.OpeningEffect || InstalledEffect(237) != diagnosticDriverRecipe.ExpectedEffect || InstalledEffect(238) != declaration.EndingEffect)
        {
            throw new InvalidDataException("Shot declaration differs from its real installed opening, firing or ending effect.");
        }
        diagnosticShotInitialLearnedFlags = definition.NativeShotAvailability?.Flags770 ?? throw new NotSupportedException("Shot diagnostic cycle lacks its original saved learning owner.");
        DiagnosticShotObservation before = ObserveDiagnosticShot(active);
        if (before.Inventory < (driver.Execution == 1 ? 3 : 2))
        {
            throw new NotSupportedException("Two explicit Shot cancellation cycles require finite initial ammunition of at least three; no refill is permitted.");
        }
        BattleDiagnosticCombatant[] observed = active.ObserveDiagnosticCombatants();
        byte Index(string id) => checked((byte)(observed.Single(combatant => combatant.Id == id).NativeIndex ?? throw new NotSupportedException("Shot diagnostics require the actual native physical actor index.")));
        driver.BeginShotCycle(new(driver.Execution, actorId, targetId, Index(actorId), Index(targetId), declaration, diagnosticDriverRecipe.ExpectedEffect, selection, before));
    }

    private DiagnosticShotObservation ObserveDiagnosticShot(BattleSession active)
    {
        DiagnosticShotCycleDeclaration declaration = diagnosticDriverRecipe!.ShotCycle!;
        int inventory = active.Items.Where(stock => stock.Id == 101 + declaration.AmmoAction).Sum(stock => stock.Count);
        byte learned = active.NativeScriptState.ShotFlagsAfter ?? diagnosticShotInitialLearnedFlags;
        return new(active.NativeHostCallbacks ?? throw new NotSupportedException("Shot diagnostics require the live callback ledger."), inventory, learned, active.GetNativeShot(diagnosticDriverRecipe.Input.ActorId!));
    }

    private void AdvanceDiagnosticShot(BattleSession active, DiagnosticBattleDriver driver, bool drained)
    {
        if (diagnosticDriverRecipe!.ShotCycle is null || driver.Stage is not (DiagnosticDriverStage.AwaitDispatch or DiagnosticDriverStage.Drain) || driver.ShotCycle is not { } cycle)
        {
            return;
        }
        DiagnosticShotOperation operation = cycle.Observe(ObserveDiagnosticShot(active), drained);
        if (operation == DiagnosticShotOperation.Trigger)
        {
            ApplyBattleCommand(new(BattleInputKind.ShotTrigger, ActorId: diagnosticDriverRecipe.Input.ActorId));
            cycle.Triggered(ObserveDiagnosticShot(active));
        }
        else if (operation == DiagnosticShotOperation.End)
        {
            active.EndNativeShot(diagnosticDriverRecipe.Input.ActorId!);
            cycle.Ended(ObserveDiagnosticShot(active));
        }
    }
}
