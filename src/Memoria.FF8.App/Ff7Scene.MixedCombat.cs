using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private CrossoverMixedBattle? mixedCombat;
    private CrossoverGuestFieldVitals? mixedFieldVitals;
    private Ff7BattleSession? restoredNativeBattle;
    public event Action<CombinedSessionSave>? MixedCombatSettled;
    public event Action<CombinedSessionSave>? MixedOriginResumeRequested;
    internal bool MixedCombatEnabled => envelope.MixedCombat is not null;
    internal bool MixedTriggerWindowOpen => mixedCombat is { } mixed && (mixed.Actions.RenzokukenTrigger?.TriggerWindowOpen ?? mixed.Attack.TriggerWindowOpen);
    internal bool CanResumeMixedOrigin => Session is null && envelope.Ff8Payload is { Length: > 0 } && envelope.GuestBattleSettlements?.LastOrDefault()?.Outcome == Ff7BattleOutcome.Defeat;

    private void ResumeMixedOrigin()
    {
        if (CanResumeMixedOrigin)
        {
            noticeWindow.Clear();
            MixedOriginResumeRequested?.Invoke(envelope);
        }
    }

    private void ApplyMappedCombat(Ff7Session session)
    {
        if (session.Battle?.GuestVitals is null)
        {
            Ff7MixedCombatBinding? mapped = envelope.Ff8Payload is { Length: > 0 } && CharacterMappingAssociations.UsesSquallCombat(envelope.Mappings) ? new(0) : null;
            if (mapped is not null && mapped != envelope.MixedCombat)
            {
                session.SetFullCombatEnabled(true);
            }
            envelope = envelope with { MixedCombat = mapped };
        }
        AttachMixedFieldVitals();
    }

    internal void PrepareMappedCombatForSwitch()
    {
        if (!MixedCombatEnabled)
        {
            return;
        }
        string directory = identityFf8Directory ?? throw new InvalidOperationException("Select the FFVIII installation before entering mixed combat.");
        using GameAssets origin = new(directory);
        using BattleAssets battle = new(directory);
        GameplaySaveComponentDocument source = CrossoverSaveAdapter.ReadFf8(envelope);
        if (source.Progression?.Clocks.GlobalRandom is null)
        {
            throw new NotSupportedException("Save FFVIII with its current gameplay codec before switching with the Squall mapping.");
        }
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(source.Characters.Restore(origin.ReadNewGameCharacterVitals()), battle);
        if (kit.OpeningCombatUnavailableReason() is { } unsupported)
        {
            throw new NotSupportedException(unsupported);
        }
        Session!.SetFullCombatEnabled(true);
    }

    private void EnsureMixedBattle(Ff7BattleSession battle)
    {
        if (envelope.MixedCombat is not { } binding || ReferenceEquals(mixedCombat?.Host, battle) || ReferenceEquals(restoredNativeBattle, battle))
        {
            return;
        }
        restoredNativeBattle = null;
        if (battle.GuestVitals is null && !battle.Actors.Any(actor => actor.Actor.Slot < 3 && actor.Identity == binding.StoryCharacter))
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
            noticeWindow.Clear();
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
