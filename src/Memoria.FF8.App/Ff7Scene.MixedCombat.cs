using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private CrossoverMixedBattle? mixedCombat;
    private CrossoverGuestFieldVitals? mixedFieldVitals;
    public event Action<CombinedSessionSave>? MixedCombatSettled;
    public event Action<CombinedSessionSave>? MixedOriginResumeRequested;
    internal bool MixedCombatEnabled => envelope.MixedCombat is not null;
    internal bool MixedTriggerWindowOpen => mixedCombat is { } mixed && (mixed.Actions.RenzokukenTrigger?.TriggerWindowOpen ?? mixed.Attack.TriggerWindowOpen);
    internal bool CanResumeMixedOrigin => Session is null && envelope.Ff8Payload is { Length: > 0 } && envelope.GuestBattleSettlements?.LastOrDefault()?.Outcome == Ff7BattleOutcome.Defeat;

    private void ResumeMixedOrigin()
    {
        if (CanResumeMixedOrigin)
        {
            notice = null;
            MixedOriginResumeRequested?.Invoke(envelope);
        }
    }

    internal string? MixedCombatUnavailableReason() => MixedCombatUnavailableReason(!MixedCombatEnabled);

    private string? MixedCombatUnavailableReason(bool enabled)
    {
        if (Session?.Battle is not null || mixedCombat is not null)
        {
            return "Finish the current battle before changing its combat owner.";
        }
        if (!enabled)
        {
            return null;
        }
        if (Session is not { } session)
        {
            return "Start or continue FFVII before selecting a combat guest.";
        }
        if (identityFf8Directory is null || envelope.Ff8Payload is not { Length: > 0 })
        {
            return "Switch from an FFVIII save to supply Squall's original character and equipment.";
        }
        if (!session.Progress.Party.Contains(0))
        {
            return "The current mixed profile binds Squall to Cloud's occupied story slot.";
        }
        return null;
    }

    internal void ToggleMixedCombat()
    {
        bool enabled = !MixedCombatEnabled;
        notice = SetMixedCombat(enabled) ?? (enabled
            ? "Squall owns Cloud's battle slot. His Revolver and Gunblade trigger use his FFVIII character. His available commands use his FFVIII stock and equipment."
            : "Native FFVII characters own the next battle.");
    }

    internal string? SetMixedCombat(bool enabled)
    {
        if (!enabled && !MixedCombatEnabled)
        {
            return null;
        }
        if (MixedCombatUnavailableReason(enabled) is { } unavailable)
        {
            return unavailable;
        }
        if (enabled)
        {
            using GameAssets origin = new(identityFf8Directory!);
            using BattleAssets battle = new(identityFf8Directory!);
            GameplaySaveComponentDocument source = CrossoverSaveAdapter.ReadFf8(envelope);
            if (source.Progression?.Clocks.GlobalRandom is null)
            {
                return "Save FFVIII with its current gameplay codec before selecting a combat guest.";
            }
            CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(source.Characters.Restore(origin.ReadNewGameCharacterVitals()), battle);
            if (kit.OpeningCombatUnavailableReason() is { } unsupported)
            {
                return unsupported;
            }
            envelope = envelope with { MixedCombat = new(0), MixedBattlePayload = null };
            Session!.SetFullCombatEnabled(true);
        }
        else
        {
            envelope = envelope with { MixedCombat = null, MixedBattlePayload = null };
        }
        AttachMixedFieldVitals();
        return null;
    }

    private void EnsureMixedBattle(Ff7BattleSession battle)
    {
        if (envelope.MixedCombat is null || ReferenceEquals(mixedCombat?.Host, battle))
        {
            return;
        }
        ResetMixedCombat();
        Ff7Session session = Session ?? throw new InvalidOperationException("A mixed encounter requires its native FFVII session.");
        CombinedSessionSave source = envelope with { Ff7 = session.Capture(), ActiveGame = SessionGame.Ff7 };
        mixedCombat = new(identityFf8Directory ?? throw new InvalidOperationException("Select the FFVIII installation before entering mixed combat."), source, battle);
        mixedCombat.Actions.BindForeignGeometry(slot =>
        {
            Ff7BattleActorState actor = battle.Actors.Single(actor => actor.Actor.Slot == slot);
            return BattleRenderer(battle).CaptureActorGeometry(BattleActorVisual(battle, actor));
        });
    }

    private CombinedSessionSave CaptureMixedCombat(CombinedSessionSave saved)
    {
        return saved with { MixedBattlePayload = mixedCombat?.Capture() ?? (saved.Ff7?.Battle?.GuestVitals is null ? null : saved.MixedBattlePayload) };
    }

    private void ReadMixedTrigger()
    {
        if (sharedInput.GunbladeTriggerRequested)
        {
            mixedCombat?.Actions.PressTrigger();
        }
    }

    private bool IsMixedBattleSlot(int slot) => mixedCombat?.Attack.ActorSlot == slot;

    private CrossoverFf8BattleActorVisual? MixedBattleVisual(Ff7BattleSession battle)
    {
        EnsureMixedBattle(battle);
        return mixedCombat is { } mixed && battle.IsActorPresentationReady(mixed.Attack.ActorSlot) ? mixed.Actions.Visual : null;
    }

    private bool TrySettleMixedBattle(Ff7BattleSession battle)
    {
        if (mixedCombat is not { } mixed)
        {
            return false;
        }
        DeliverGuestBattleSounds();
        bool gameOver = battle.Outcome == Ff7BattleOutcome.Defeat && !battle.ReturnsAfterDefeat;
        CrossoverBattleVitalsSettlementResult staged = mixed.Settle(Capture(), installation);
        envelope = staged.Save;
        MixedCombatSettled?.Invoke(staged.Save);
        if (gameOver)
        {
            ResetMixedCombat();
            StopAudio();
            autoPlay = null;
            Session?.Dispose();
            Session = null;
            titleSelection = 1;
            notice = null;
        }
        else
        {
            Continue(staged.Save);
        }
        return true;
    }

    private void ResetMixedCombat()
    {
        DeliverGuestBattleSounds();
        mixedCombat?.Dispose();
        mixedCombat = null;
        mixedFieldVitals?.Dispose();
        mixedFieldVitals = null;
        if (Session is { } session)
        {
            session.GuestFieldVitals = null;
        }
    }

    private void AttachMixedFieldVitals()
    {
        mixedFieldVitals?.Dispose();
        mixedFieldVitals = null;
        if (Session is not { } session)
        {
            return;
        }
        session.GuestFieldVitals = null;
        if (session.Battle?.GuestVitals is null)
        {
            envelope = envelope with { MixedBattlePayload = null };
        }
        if (envelope.MixedCombat is not null)
        {
            mixedFieldVitals = new(identityFf8Directory ?? throw new InvalidOperationException("Guest field vitals require the selected FFVIII installation."), () => envelope, saved =>
            {
                envelope = saved;
                MixedCombatSettled?.Invoke(saved);
            });
            session.GuestFieldVitals = mixedFieldVitals;
        }
    }
}
