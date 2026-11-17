using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledPartyReplacementTests
{
    [Fact]
    public void Native_party_replacement_rebinds_the_original_model_slot_and_can_fork_before_binding()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        ExpandedBattleInstalledEntry.Prepare(world, session, PrepareParty, formation: 511, allies: 4, additionalEnemies: 0);
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer && actor.IsActive));
        BattleCombatant outgoing = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 0);
        NativeBattleModelSlot originalSlot = NativeBattleModelSlot.Decode(0);
        BattleModelOwner oldOwner = new BattlePartyActorModel(new(outgoing.Id));
        Assert.Equal(oldOwner, process.FindOriginalActor(originalSlot));
        Assert.False(outgoing.IsAlive);

        // Enter at the installed status consumer's boundary, with the actual registered scene models.
        MethodInfo eject = typeof(BattleSession).GetMethod("EjectPartyOccupant", BindingFlags.Instance | BindingFlags.NonPublic)!;
        eject.Invoke(battle, [outgoing]);
        Assert.Equal(new(BattleRosterSlot.Ally(0), 0, 4), Assert.Single(battle.PartyOccupancyChanges));
        Assert.False(outgoing.IsActive);
        BattleSession fork = battle.ForkForContinuation();
        BattlePresentationProcess forkedProcess = Assert.IsType<BattlePresentationProcess>(fork.Presentation);
        List<uint> callbacks = [];
        List<uint> restoredCallbacks = [];
        for (int callback = 0; callback < 1200 && !callbacks.Contains(0x497270); callback++)
        {
            battle.Tick();
            fork.Tick();
            Observe(process, callbacks);
            Observe(forkedProcess, restoredCallbacks);
        }
        Assert.Equal(new uint[] { 0x4971F0, 0x497270 }, callbacks);
        Assert.Equal(callbacks, restoredCallbacks);
        BattleCombatant incoming = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 4);
        BattleModelOwner newOwner = new BattlePartyActorModel(new(incoming.Id));
        Assert.Equal(BattleRosterSlot.Ally(0), incoming.Slot);
        Assert.Equal(newOwner, process.FindOriginalActor(originalSlot));
        Assert.Equal(newOwner, forkedProcess.FindOriginalActor(originalSlot));
        Assert.Equal(incoming.Atb, Assert.Single(fork.Combatants, actor => actor.Id == incoming.Id).Atb);
        Assert.Equal((byte)4, battle.MagicVitals!.CombatParty[0]);
        Assert.DoesNotContain(process.Graphics.ActorModels.Owners, owner => owner == oldOwner);

        static void Observe(BattlePresentationProcess process, List<uint> reached)
        {
            if (process.LastFrameServices is { } frame)
            {
                reached.AddRange(frame.ReadOrderedReceipts().OfType<NativeBattlePartyReplacementReached>().Select(receipt => receipt.Descriptor.Callback));
            }
        }
    }

    private static void PrepareParty(GameSession session)
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals!.WithGuestBattleRecords().Rest();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        for (int row = 0; row < NativeCharacterVitals.NativeRecordCount; row++)
        {
            records[row] = records[row] with { MemberFlags = (ushort)((records[row].MemberFlags & ~1) | (row <= 4 ? 1 : 0)) };
        }
        records[0] = records[0] with { Status = 1, HitPoints = new(0, 0) };
        session.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([0, 1, 2, 3], 4));
    }
}
