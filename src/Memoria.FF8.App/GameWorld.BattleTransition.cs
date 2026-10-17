using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal ushort CaptureBattleEntryFlags()
    {
        if (battle is not null)
        {
            throw new InvalidOperationException("Battle entry requires a pending encounter without an active battle.");
        }
        if (Events.PendingMenuBattle is { } menu)
        {
            return menu.Flags;
        }
        if (session.State.RandomEncounters?.Pending is { } random)
        {
            return random.Scene == SceneKind.Field ? Events.BeginNativeRandomFieldBattle(random) : (ushort)0;
        }
        return Events.PendingInteractiveBattle?.Flags ?? throw new InvalidOperationException("Battle entry has no suspended request.");
    }

    internal void PublishPreparedBattleEntry(PreparedBattleEntry entry)
    {
        battle = CommitPreparedBattleEntry(entry);
        PublishBattleSnapshot(battle);
        session.SetEventState(Events.Capture());
    }

    internal NativeBattleEntryTransition CreateBattleEntryTransition()
    {
        BattleEncounterOccurrence occurrence = session.State.PendingBattleEncounter ?? throw new InvalidOperationException("A battle transition requires its observed encounter.");
        NativeCrtRandomState? random = session.State.CrtRandom;
        int draws = 0;
        NativeBattleEntryTransition transition = new(assets.ReadNativeBattleEntryTransitionResources(), checked((ushort)occurrence.Formation), 640, 448, () =>
        {
            random = (random ?? throw new NotSupportedException("Battle entry transition requires its retained CRT random owner.")).Draw(out ushort value);
            draws++;
            return value;
        });
        if (draws > 0)
        {
            session.AdvanceNativeBattleEntryRandom(session.State.CrtRandom!, draws);
        }
        return transition;
    }
}
