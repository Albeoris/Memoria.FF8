using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    public PendingEntityName? PendingEntityName => session.State.PendingEntityNames?.FirstOrDefault();

    public void CompleteEntityName(string text)
    {
        PendingEntityName pending = PendingEntityName ?? throw new InvalidOperationException("There is no required entity name request.");
        Rename(pending.Subject, text);
        session.CompleteEntityName(pending);
    }

    private int AcquiredGuardianForceMask()
    {
        int mask = 0;
        if (session.State.CharacterVitals is { } vitals)
        {
            for (int id = 0; id < vitals.GuardianForces.Length; id++)
            {
                if ((vitals.GuardianForces[id].Flags & 1) != 0)
                {
                    mask |= 1 << id;
                }
            }
        }
        return mask;
    }

    internal void QueueItemGuardianForceNames(int itemId, int acquiredBefore)
    {
        int acquired = AcquiredGuardianForceMask() & ~acquiredBefore;
        if (acquired == 0)
        {
            return;
        }
        List<PendingEntityName> names = [];
        for (int id = 0; id < 16; id++)
        {
            if ((acquired & (1 << id)) != 0)
            {
                int subject = GameNames.GuardianForceSubject(id);
                string name = GameNames.DefaultName(subject);
                // Solomon Ring4FA5B9 uses selector14 without80, so menu15 initializes the default.
                Events.Scene.Names[subject] = name;
                names.Add(new PendingEntityName(subject, name, $"item:{itemId}"));
            }
        }
        session.SetEventState(Events.Capture());
        session.EnqueueEntityNames(names);
    }

    private PlayerActionResult ApplyItemNaming(PlayerAction action, NativeFieldItem item)
    {
        if (action.Quantity is null || action.Text is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
        {
            throw new ArgumentException("Item naming requires the original item and selected target only.", nameof(action));
        }
        bool world = session.State.Scene == SceneKind.WorldMap;
        bool activeMenu = Events.PartyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (battle is not null || session.HasActiveBattle || Events.HasPendingBattle || Events.CurrentDialogue is not null || Events.PendingTripleTriad is not null || !world && (!activeMenu && !Events.CanAcceptMovementInput || Events.PartyMenu is not null && !activeMenu || !Events.Scene.MenuEnabled) || world && worldScene!.CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, Events.NextTrigger, 0, "The current interaction suspends naming item use.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Naming items require canonical saved characters and Guardian Forces.");
        NativeEconomy economy = session.State.Economy ?? throw new NotSupportedException("Naming items require canonical saved inventory.");
        (NativeEconomy consumed, int subject) = item.BeginNaming(vitals, economy, action.Value, action.Quantity.Value);
        string current = Events.Scene.Names.GetValueOrDefault(subject) ?? (subject == GameNames.BokoSubject ? NativePocketStationText.ChocoboName(session.State.PocketStation) ?? throw new NotSupportedException("Chocobo Tag requires its original saved Chocobo name buffer.") : GameNames.DefaultName(subject));
        PendingEntityName pending = new(subject, current, $"item:{action.Value}");
        session.BeginItemEntityName(consumed, pending);
        session.SetEventState(Events.Capture());
        return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.Dialogue, session.State.FieldId, Events.Frame, null, pending.DefaultName), 0);
    }
}
