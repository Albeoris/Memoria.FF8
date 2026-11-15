using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private bool TryAdvanceAutoPlayBattle(GameTime gameTime, Ff7BattleSession battle)
    {
        if (autoPlay is not { Running: true, Paused: false })
        {
            return false;
        }
        if (battle.GuestAbilityFailure is { } failure)
        {
            throw new NotSupportedException($"Automatic combat stopped at an unavailable guest action: {failure.Reason}");
        }
        if (battle.Outcome != Ff7BattleOutcome.Fighting)
        {
            if (!battle.ResultReady)
            {
                AdvanceBattleTime(gameTime, battle, escapeHeld: false);
                return true;
            }
            if (battle.Outcome == Ff7BattleOutcome.Defeat && !battle.ReturnsAfterDefeat)
            {
                throw new NotSupportedException("Automatic combat stopped at its actual Defeat outcome.");
            }
            Ff7Session session = Session!;
            if (battle.Outcome == Ff7BattleOutcome.Victory)
            {
                PrepareBattleResult(session, battle);
                if (battleResultPager!.MoveNext(battleResultRows.Length))
                {
                    UpdateAudio();
                    return true;
                }
            }
            if (!TrySettleMixedBattle(battle))
            {
                if (battle.Outcome == Ff7BattleOutcome.Victory)
                {
                    session.ConfirmBattleVictory();
                }
                else if (battle.Outcome == Ff7BattleOutcome.Escaped)
                {
                    session.ConfirmBattleEscape();
                }
                else
                {
                    session.ConfirmBattleDefeat();
                }
            }
            tickRemainder = 0;
            restoredNativeBattle = null;
            ResetBattlePresentation();
            sharedInput.SuppressHeldUiInput();
            if (Session is not null && autoPlay is null)
            {
                ApplyAutoPlayCommand("start");
            }
            autoPlay?.ResetIdleDeadline();
            return true;
        }
        if (CurrentBattleAtbMode == BattleAtbMode.TurnBased && battle.ReadyPlayer is null)
        {
            battle.AdvanceTurn();
        }
        if (battle.ReadyPlayer is int owner)
        {
            var choice = PlanAutoPlayBattleCommand(battle.Commands(owner), battle.Actors, (command, target) => battle.GuestAbilityTargetUnsupportedReason(owner, command.Command, command.Argument, target) is null);
            if (choice is { } action)
            {
                selectedBattleCommand = null;
                battleCommandPage = null;
                BattleSmartAttackPreview? preview = action.Command.Command == Ff7BattleCommand.Attack ? mixedCombat?.GuestAt(owner)?.PreviewSmartAttack(action.TargetSlot, forcePhysical: false) : null;
                ushort mask = AutoPlayBattleTargetMask(action.Command, action.TargetSlot);
                bool submitted = preview is not null ? mixedCombat!.GuestAt(owner)!.SubmitSmartAttack(preview) : battle.Submit(owner, action.Command.Command, action.Command.Argument, mask);
                if (!submitted)
                {
                    throw new InvalidOperationException("The native battle rejected the selected automatic command.");
                }
                autoPlay.ResetIdleDeadline();
            }
        }
        pendingLimitConfirm = battle.BeatRushSelecting;
        AdvanceBattleTime(gameTime, battle, escapeHeld: false);
        return true;
    }

    internal static (Ff7BattleCommandOption Command, int TargetSlot)? PlanAutoPlayBattleCommand(IReadOnlyList<Ff7BattleCommandOption> commands, IReadOnlyList<Ff7BattleActorState> actors, Func<Ff7BattleCommandOption, int, bool>? targetSupported = null)
    {
        Ff7BattleCommandOption? command = commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Attack && option.UnsupportedReason is null)
            ?? commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Limit && option.UnsupportedReason is null);
        if (command is null)
        {
            throw new NotSupportedException("Automatic combat requires an available native Attack or Limit command.");
        }
        AutoPlayBattleDecision decision = AutoPlayBattlePolicy.Choose(false, true, "Fighting", true, false,
            [new(true, true, true, false, true)],
            actors.Select(actor => new AutoPlayBattleTarget(actor.Actor.Slot >= 4, (command.ValidTargets & 1 << actor.Actor.Slot) != 0, actor.Actor.HitPoints > 0, targetSupported is not null && !targetSupported(command, actor.Actor.Slot))).ToArray());
        if (decision.Kind != AutoPlayBattleDecisionKind.Attack)
        {
            return null;
        }
        int targetSlot = actors[decision.TargetIndex].Actor.Slot;
        ushort mask = AutoPlayBattleTargetMask(command, targetSlot);
        if (targetSupported is not null && actors.Any(actor => (mask & 1 << actor.Actor.Slot) != 0 && !targetSupported(command, actor.Actor.Slot)))
        {
            throw new NotSupportedException("The native automatic command requires an unavailable group target bridge.");
        }
        return (command, targetSlot);
    }

    internal static ushort AutoPlayBattleTargetMask(Ff7BattleCommandOption command, int targetSlot)
    {
        return command.SelectSingle ? checked((ushort)(1 << targetSlot)) : (ushort)(command.ValidTargets & (command.AllTargets ? ushort.MaxValue : targetSlot < 3 ? 15 : 0x3F0));
    }
}
