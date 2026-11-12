using Memoria.FF7.Runtime;
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
    internal bool MixedCombatEnabled => envelope.GetMixedCombatBindings().Count > 0;
    internal bool MixedTriggerWindowOpen => mixedCombat?.ActiveGuest is { } guest && (guest.RenzokukenTrigger?.TriggerWindowOpen ?? guest.Attack.TriggerWindowOpen);
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
        if (session.Battle?.GuestVitalsBindings is not { Count: > 0 })
        {
            if (envelope.Ff8Payload is { Length: > 0 } && envelope.Mappings.Length > 0 && !MixedCombatEnabled)
            {
                session.SetFullCombatEnabled(true);
            }
            SetMappedCombat(ResolveMappedCombat(session));
        }
        AttachMixedFieldVitals();
    }

    private Ff7MixedCombatBinding[] ResolveMappedCombat(Ff7Session session, bool requireUnique = false)
    {
        if (envelope.Ff8Payload is not { Length: > 0 })
        {
            return [];
        }
        if (envelope.Mappings.Length == 0)
        {
            return [];
        }
        GameplaySaveCharacters characters = CrossoverSaveAdapter.ReadFf8(envelope).Characters;
        Ff7MixedCombatBinding[] active = envelope.Mappings.Where(mapping => session.Progress.Party.Contains(mapping.StoryCharacter) && CrossoverGuestRecord.FindOriginalRow(characters.Records, characters.ExtendedRosterEnabled, mapping.AppearanceCharacter) >= 0).OrderBy(mapping => mapping.StoryCharacter).Select(mapping => new Ff7MixedCombatBinding(mapping.StoryCharacter, mapping.AppearanceCharacter)).ToArray();
        if (active.Select(binding => binding.OriginCharacter).Distinct().Count() != active.Length)
        {
            if (requireUnique)
            {
                throw new NotSupportedException("The same FFVIII character is mapped to more than one occupied FFVII party slot.");
            }
            return [];
        }
        return active;
    }

    private void SetMappedCombat(Ff7MixedCombatBinding[] bindings)
    {
        envelope = envelope with
        {
            MixedCombat = bindings.Length == 1 ? bindings[0] : null,
            MixedCombatBindings = bindings.Length > 1 ? bindings : null
        };
    }

    private void RefreshMappedFieldCombat(Ff7Session session)
    {
        Ff7MixedCombatBinding[] mapped = ResolveMappedCombat(session);
        if (!mapped.SequenceEqual(envelope.GetMixedCombatBindings()))
        {
            SetMappedCombat(mapped);
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
        ResolveMappedCombat(session, requireUnique: true);
        session.SetFullCombatEnabled(true);
    }

    private void EnsureMixedBattle(Ff7BattleSession battle)
    {
        if (ReferenceEquals(mixedCombat?.Host, battle) || ReferenceEquals(restoredNativeBattle, battle))
        {
            return;
        }
        Ff7Session session = Session ?? throw new InvalidOperationException("A mixed encounter requires its native FFVII session.");
        if (battle.GuestVitalsBindings.Count == 0)
        {
            SetMappedCombat(ResolveMappedCombat(session, requireUnique: true));
        }
        if (!MixedCombatEnabled)
        {
            return;
        }
        restoredNativeBattle = null;
        ResetMixedCombat();
        CombinedSessionSave source = envelope with { Ff7 = session.Capture(), ActiveGame = SessionGame.Ff7 };
        CrossoverMixedBattle mixed = new(identityFf8Directory ?? throw new InvalidOperationException("Select the FFVIII installation before entering mixed combat."), source, battle);
        mixed.BindForeignGeometry(slot =>
        {
            if (mixed.GuestAt(slot) is { } guest)
            {
                return CrossoverFf8BattleActorRenderer.CaptureActorGeometry(guest.Visual ?? throw new InvalidDataException("The mapped ally has no loaded battle pose."));
            }
            Ff7BattleActorState actor = battle.Actors.Single(actor => actor.Actor.Slot == slot);
            return BattleRenderer(battle).CaptureActorGeometry(BattleActorVisual(battle, actor));
        });
        mixedCombat = mixed;
    }

    private CombinedSessionSave CaptureMixedCombat(CombinedSessionSave saved)
    {
        return saved with { MixedBattlePayload = mixedCombat?.Capture() ?? (Session?.Battle?.GuestVitalsBindings is { Count: > 0 } ? saved.MixedBattlePayload : null) };
    }

    private void ReadMixedTrigger()
    {
        if (sharedInput.GunbladeTriggerRequested)
        {
            mixedCombat?.ActiveGuest?.PressTrigger();
        }
    }

    private bool IsMixedBattleSlot(int slot) => mixedCombat?.GuestAt(slot) is not null;

    private IReadOnlyList<CrossoverFf8BattleActorVisual> MixedBattleVisuals(Ff7BattleSession battle)
    {
        EnsureMixedBattle(battle);
        return mixedCombat is { } mixed
            ? mixed.Guests.Where(guest => battle.IsActorPresentationReady(guest.ActorSlot) && mixed.ActiveGuest?.SuppressForeignActor(guest.ActorSlot) != true).Select(guest =>
            {
                CrossoverFf8BattleActorVisual? visual = guest.Visual;
                if (visual is not null && mixed.ActiveGuest?.ReadForeignPlacement(guest.ActorSlot) is { } placement)
                {
                    visual = visual with { OriginToHost = visual.OriginToHost * placement, ShadowSuppressed = mixed.ActiveGuest.SuppressForeignShadow(guest.ActorSlot) };
                }
                return visual;
            }).OfType<CrossoverFf8BattleActorVisual>().ToArray()
            : [];
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
        if (session.Battle?.GuestVitalsBindings is not { Count: > 0 })
        {
            envelope = envelope with { MixedBattlePayload = null };
        }
        if (MixedCombatEnabled)
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
