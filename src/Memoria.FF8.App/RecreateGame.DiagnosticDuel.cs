using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private void ObserveDiagnosticDuelDispatch(BattleDiagnosticEffectDispatch dispatch)
    {
        if (dispatch.ActorId != diagnosticDriverRecipe!.Input.ActorId || diagnosticDriver!.Finished)
        {
            return;
        }
        BattleSession active = diagnosticDriverBattle!;
        DiagnosticDuelCycleDriver cycle = diagnosticDriver.DuelCycle ?? throw new InvalidDataException("Duel dispatched without its real command11 admission.");
        cycle.Observe(ObserveDiagnosticDuel(active), false);
        NativeBattleRetainedActionPackets packets = dispatch.ActionId is 65530 or 65532 ? active.ObserveDiagnosticDuelClosingPackets(dispatch) : active.Presentation!.ObserveDiagnosticActionPackets(dispatch);
        BattleDiagnosticEffectInvocation invocation = active.Presentation!.ObserveDiagnosticEffectInvocation(dispatch);
        NativeBattleRetainedActionHeaderSlot header = packets.Headers[dispatch.NativeHeaderIndex!.Value];
        diagnosticDriver.ObserveDuelDispatch(dispatch, new(dispatch.Serial, dispatch.Callback, dispatch.NativeHeaderIndex.Value, dispatch.NativeHeaderGeneration!.Value, packets)
        {
            AttachedPresentation = active.Presentation is not null,
            Parameter80 = invocation.Parameter80,
            Parameter82 = invocation.Parameter82,
            HeaderEffect = (ushort)(header.ReadOriginalByte(6) | header.ReadOriginalByte(7) << 8),
            RetainedEffect = invocation.ExecutedEffect,
            Duel = active.ObserveDiagnosticDuelState(diagnosticDriverRecipe.Input.ActorId!),
            HostedTargets = dispatch is { NativeCommand: 239, ActionId: >= 6 and <= 9 } ? active.ObserveDiagnosticHostedDuelTargets(dispatch) : null
        });
    }

    private DiagnosticDuelObservation ObserveDiagnosticDuel(BattleSession active) => new(active.NativeHostCallbacks ?? throw new NotSupportedException("Duel diagnostics require the native callback ledger."), active.ObserveDiagnosticDuelState(diagnosticDriverRecipe!.Input.ActorId!));

    private void ObserveDiagnosticDuelClosing(string actorId)
    {
        if (diagnosticDriverRecipe?.DuelCycle is null || actorId != diagnosticDriverRecipe.Input.ActorId || diagnosticDriverBattle is not { } active || diagnosticDriver is not { Finished: false, DuelCycle: { Completed: false } })
        {
            return;
        }
        ObserveDiagnosticDuelUi(active);
    }

    private void ObserveDiagnosticDuelUi(BattleSession active)
    {
        if (diagnosticDriverRecipe?.DuelCycle is not null && diagnosticDriver?.DuelCycle is { Completed: false } cycle)
        {
            cycle.Observe(ObserveDiagnosticDuel(active), false);
        }
    }

    private void BeginDiagnosticDuel(BattleSession active, DiagnosticBattleDriver driver, BattleDiagnosticLimitSelectionReceipt selection)
    {
        string actorId = diagnosticDriverRecipe!.Input.ActorId!;
        string targetId = diagnosticDriverRecipe.Input.TargetId!;
        BattleCombatant actor = active.Combatants.Single(actor => actor.Id == actorId);
        BattleCombatant target = active.Combatants.Single(actor => actor.Id == targetId);
        if (!actor.IsAlive || !target.IsAlive || actor.ModelId != 1 || actor.NativeLimit is not { CharacterId: 1, Command: 11 } || active.GetNativeDuel(actorId) is not null || selection.CommandOwner != 1 || selection.Command != 11 || !selection.Accepted)
        {
            throw new InvalidDataException("Duel diagnostics require the original Zell owner and a genuine new command11 Limit admission.");
        }
        BattleDiagnosticCombatant[] observed = active.ObserveDiagnosticCombatants();
        byte Index(string id) => checked((byte)(observed.Single(combatant => combatant.Id == id).NativeIndex ?? throw new NotSupportedException("Duel diagnostics require the actual native physical actor index.")));
        driver.BeginDuelCycle(new(driver.Execution, actorId, targetId, Index(actorId), Index(targetId), diagnosticDriverRecipe.DuelCycle!, selection, ObserveDiagnosticDuel(active)));
    }

    private void AdvanceDiagnosticDuel(BattleSession active, DiagnosticBattleDriver driver, bool drained)
    {
        if (diagnosticDriverRecipe!.DuelCycle is null || driver.DuelCycle is not { } cycle || driver.Stage is not (DiagnosticDriverStage.AwaitDispatch or DiagnosticDriverStage.Drain))
        {
            return;
        }
        DiagnosticDuelObservation observation = ObserveDiagnosticDuel(active);
        cycle.Observe(observation, drained);
        if (cycle.NeedsInput && observation.Duel is { Phase: BattleNativeDuelPhase.Ready } state)
        {
            BattleCombatant actor = active.Combatants.Single(actor => actor.Id == diagnosticDriverRecipe.Input.ActorId);
            BattleNativeDuelChoice choice = FindDiagnosticDuelChoice(actor.NativeLimit!, state.MoveSetId, cycle.Receipt.Declaration.MoveAction, active.GetNativeDuelChoices(actor.Id));
            cycle.BeginInput(choice, observation);
        }
    }

    private ushort ReadDiagnosticDuelButtons(BattleSession active, ushort ordinaryButtons)
    {
        if (diagnosticDriverRecipe?.DuelCycle is null || diagnosticDriver?.DuelCycle is not { } cycle || active.GetNativeDuel(diagnosticDriverRecipe.Input.ActorId!) is not { } state)
        {
            return ordinaryButtons;
        }
        return cycle.Buttons(state);
    }

    internal static BattleNativeDuelChoice FindDiagnosticDuelChoice(BattleNativeLimitDefinition rules, byte start, byte desiredAction, IReadOnlyList<BattleNativeDuelChoice> choices)
    {
        Queue<(byte Set, byte First)> pending = new();
        HashSet<byte> visited = [start];
        pending.Enqueue((start, byte.MaxValue));
        while (pending.TryDequeue(out var current))
        {
            for (int index = 1; index <= 3; index++)
            {
                byte next = rules.DuelMoveSets[4 * current.Set + index];
                if (next == byte.MaxValue || !visited.Add(next))
                {
                    continue;
                }
                byte action = NativeBattleDuelRules.ActionId(rules, next);
                byte first = current.First == byte.MaxValue ? next : current.First;
                if (action == desiredAction)
                {
                    return choices.Single(choice => choice.MoveSetId == first && choice.Learned);
                }
                if (action < 6)
                {
                    pending.Enqueue((next, first));
                }
            }
        }
        throw new NotSupportedException("The installed learned Duel move graph cannot reach the declared move.");
    }
}
