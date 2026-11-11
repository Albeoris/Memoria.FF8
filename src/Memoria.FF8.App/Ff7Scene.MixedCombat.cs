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
        if (session.Battle?.GuestVitals is null && envelope.Ff8Payload is { Length: > 0 } && envelope.Mappings.Length > 0 && envelope.MixedCombat is null)
        {
            session.SetFullCombatEnabled(true);
        }
        if (session.Battle?.GuestVitals is null)
        {
            envelope = envelope with { MixedCombat = ResolveMappedCombat(session, requireSingle: false) };
        }
        AttachMixedFieldVitals();
    }

    private Ff7MixedCombatBinding? ResolveMappedCombat(Ff7Session session, bool requireSingle)
    {
        if (envelope.Ff8Payload is not { Length: > 0 })
        {
            return null;
        }
        CharacterAppearanceMapping[] active = envelope.Mappings.Where(mapping => session.Progress.Party.Contains(mapping.StoryCharacter)).Take(2).ToArray();
        if (active.Length > 1 && requireSingle)
        {
            throw new NotSupportedException("This FFVII encounter can bind one FFVIII guest. Multiple mapped characters occupy the current party.");
        }
        return active.Length == 1 ? new(active[0].StoryCharacter, active[0].AppearanceCharacter) : null;
    }

    private void RefreshMappedFieldCombat(Ff7Session session)
    {
        Ff7MixedCombatBinding? mapped = ResolveMappedCombat(session, requireSingle: false);
        if (mapped != envelope.MixedCombat)
        {
            envelope = envelope with { MixedCombat = mapped };
            AttachMixedFieldVitals();
        }
    }

    internal void PrepareMappedCombatForSwitch()
    {
        if (envelope.Mappings.Length == 0)
        {
            return;
        }
        Ff7Session session = Session ?? throw new InvalidOperationException("Mixed combat requires the destination FFVII session.");
        ResolveMappedCombat(session, requireSingle: true);
        string directory = identityFf8Directory ?? throw new InvalidOperationException("Select the FFVIII installation before entering mixed combat.");
        using GameAssets origin = new(directory);
        using BattleAssets battle = new(directory);
        GameplaySaveComponentDocument source = CrossoverSaveAdapter.ReadFf8(envelope);
        if (source.Progression?.Clocks.GlobalRandom is null)
        {
            throw new NotSupportedException("Save FFVIII with its current gameplay codec before switching with a combat mapping.");
        }
        NativeCharacterVitals vitals = source.Characters.Restore(origin.ReadNewGameCharacterVitals());
        foreach (int characterId in envelope.Mappings.Select(mapping => mapping.AppearanceCharacter).Distinct())
        {
            CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(vitals, battle, characterId);
            if (kit.OpeningCombatUnavailableReason() is { } unsupported)
            {
                throw new NotSupportedException(unsupported);
            }
        }
        session.SetFullCombatEnabled(true);
    }

    private void EnsureMixedBattle(Ff7BattleSession battle)
    {
        if (ReferenceEquals(mixedCombat?.Host, battle) || ReferenceEquals(restoredNativeBattle, battle))
        {
            return;
        }
        Ff7Session session = Session ?? throw new InvalidOperationException("A mixed encounter requires its native FFVII session.");
        Ff7MixedCombatBinding? binding = battle.GuestVitals is null ? ResolveMappedCombat(session, requireSingle: true) : envelope.MixedCombat;
        if (binding is null)
        {
            return;
        }
        envelope = envelope with { MixedCombat = binding };
        restoredNativeBattle = null;
        ResetMixedCombat();
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
