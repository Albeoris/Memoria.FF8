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
    internal bool MixedTriggerWindowOpen => mixedCombat?.Attack.TriggerWindowOpen == true;
    internal bool CanResumeMixedOrigin => Session is null && envelope.Ff8Payload is { Length: > 0 } && envelope.GuestBattleSettlements?.LastOrDefault()?.Outcome == Ff7BattleOutcome.Defeat;

    private void ResumeMixedOrigin()
    {
        if (CanResumeMixedOrigin)
        {
            notice = null;
            MixedOriginResumeRequested?.Invoke(envelope);
        }
    }

    internal string? MixedCombatUnavailableReason()
    {
        if (Session is null && envelope.MixedCombat is null)
        {
            return "Start or continue FFVII before selecting a combat guest.";
        }
        if (Session?.Battle is not null || mixedCombat is not null)
        {
            return "Finish the current battle before changing its combat owner.";
        }
        if (identityFf8Directory is null || envelope.Ff8Payload is not { Length: > 0 })
        {
            return "Switch from an FFVIII save to supply Squall's original character and equipment.";
        }
        if (Session is not null && !Session.Progress.Party.Contains(0))
        {
            return "The current mixed profile binds Squall to Cloud's occupied story slot.";
        }
        return null;
    }

    internal void ToggleMixedCombat()
    {
        if (MixedCombatUnavailableReason() is { } unavailable)
        {
            notice = unavailable;
            return;
        }
        if (envelope.MixedCombat is null)
        {
            using GameAssets origin = new(identityFf8Directory!);
            using BattleAssets battle = new(identityFf8Directory!);
            GameplaySaveComponentDocument source = CrossoverSaveAdapter.ReadFf8(envelope);
            if (source.Progression?.Clocks.GlobalRandom is null)
            {
                notice = "Save FFVIII with its current gameplay codec before selecting a combat guest.";
                return;
            }
            _ = CrossoverFf8BattleKit.ReadSquall(source.Characters.Restore(origin.ReadNewGameCharacterVitals()), battle);
            envelope = envelope with { MixedCombat = new(0), MixedBattlePayload = null };
            AttachMixedFieldVitals();
            Session!.SetFullCombatEnabled(true);
            notice = "Squall owns Cloud's battle slot. His Revolver and Gunblade trigger use his FFVIII character. His available commands use his FFVIII stock and equipment.";
        }
        else
        {
            envelope = envelope with { MixedCombat = null, MixedBattlePayload = null };
            AttachMixedFieldVitals();
            notice = "Native FFVII characters own the next battle.";
        }
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
            mixedCombat?.Attack.PressTrigger();
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
