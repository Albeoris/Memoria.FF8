using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record BattleNativeControlInput(string ActorId, string Operation, BattleLimitFamily? Family = null, ushort Buttons = 0, int Selection = 0);

internal sealed partial class RecreateGame
{
    private object DispatchNativeBattleControl(BattleNativeControlInput input)
    {
        BattleSession active = ActiveBattle ?? throw new InvalidOperationException("Native controller input requires a live battle.");
        BattleCombatant actor = active.Combatants.Single(actor => actor.Id == input.ActorId && actor.IsPlayer);
        switch (input.Operation)
        {
            case "HoldEnemyCommands":
                active.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: actor.Id);
                break;
            case "Inspect":
                break;
            case "SelectActor":
                active.SelectPlayerCommandActor(actor.Id);
                break;
            case "RefreshLimit":
                active.RefreshNativeLimit(actor.Id);
                break;
            case "SelectFamily":
                active.SelectNativeLimitFamily(actor.Id, input.Family ?? throw new ArgumentException("SelectFamily requires Family."));
                break;
            case "DuelButtons":
                if (active.GetNativeDuel(actor.Id) is null)
                {
                    throw new InvalidOperationException("Duel input requires this actor's native controller.");
                }
                active.SubmitNativeDuelInput(input.Buttons);
                break;
            case "EndShot":
                active.EndNativeShot(actor.Id);
                break;
            case "RollSlots":
                active.RollNativeSlots(actor.Id);
                break;
            case "BeginSlotsCards":
                active.BeginNativeSlotsDeal(actor.Id);
                break;
            case "SelectSlotsCard":
                BattleNativeSlotsDeal selected = active.PendingSlotsDeal ?? throw new InvalidOperationException("No native Slots deal is active.");
                if (selected.ActorId != actor.Id || !active.SelectNativeSlotsCard(selected.Id, input.Selection))
                {
                    throw new InvalidOperationException("The native Slots card could not be selected.");
                }
                break;
            case "SubmitSlotsCard":
                BattleNativeSlotsDeal submitted = active.PendingSlotsDeal ?? throw new InvalidOperationException("No native Slots deal is active.");
                if (submitted.ActorId != actor.Id || !active.SubmitNativeSlotsCard(submitted.Id))
                {
                    throw new InvalidOperationException("The native Slots card could not be submitted.");
                }
                break;
            default:
                throw new ArgumentException("Unsupported native controller operation.");
        }
        return new
        {
            Battle = BattleStatus(),
            Crisis = active.GetNativeLimitLevel(actor.Id),
            Choices = active.GetNativePlayerActionChoices(actor.Id, limits: true),
            DuelChoices = active.GetNativeDuelChoices(actor.Id),
            Cards = active.PendingSlotsDeal?.Candidates.Select((card, index) => new { Index = index, Available = active.CanCastNativeSlotsCard(card) }).ToArray()
        };
    }
}
