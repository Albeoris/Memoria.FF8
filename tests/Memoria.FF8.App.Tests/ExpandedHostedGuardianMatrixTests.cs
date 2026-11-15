using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedGuardianMatrixTests
{
    private const string CasterId = "party-3";
    private const int CallbackBudget = 3200;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void Installed_ordinary_guardian_finishes_its_attached_invocation(int guardianId)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: assets);
        string identity = $"GF {guardianId}";
        try
        {
            BattleActionDefinition installed = Assert.Single(world.BattleResources.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == guardianId);
            identity = $"{identity} ({installed.Name})";
            RunInstalledGuardian(world, session, guardianId, identity);
        }
        catch (Exception failure)
        {
            InvalidOperationException primary = new($"{identity} failed on the attached presentation process.", failure);
            try
            {
                world.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(primary, cleanup);
            }
            throw primary;
        }
        world.Dispose();
    }

    private static void RunInstalledGuardian(GameWorld world, GameSession session, int guardianId, string identity)
    {
        ExpandedBattleInstalledEntry.Prepare(world, session, current => ExpandedBattleGuardianTests.PrepareParty(current, learnedBoost: false, guardianId: guardianId));
        int initialResults = session.State.AppliedBattleResultIds.Length;
        uint initialVictories = session.State.BattleStatistics!.Victories;
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        BattleSession child = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        BattleActionDefinition action = Assert.Single(battle.ReadDefinition().ActionDefinitions, candidate => candidate.Ability == BattleAbility.GuardianForce && candidate.Id == guardianId);
        BattleNativeAction native = Assert.IsType<BattleNativeAction>(action.NativeAction);
        Assert.Equal((byte)254, native.Command);
        Assert.Equal(64 + guardianId, native.ActionId);
        Assert.False(action.NativeGuardianBoostAvailable);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(5, battle.Combatants.Count(actor => !actor.IsPlayer));
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == CasterId);
        BattleModelOwner casterOwner = battle.ReadPresentationModelOwner(CasterId);
        Assert.False(child.NativeActionPackets.ModelAliases.TryEncodeExact(casterOwner, out _));
        Assert.True(battle.CanSelectExpandedGuardianForce(CasterId, guardianId));
        ushort[] initialAffinity = caster.GuardianForceCompatibility!.ToArray();
        ushort[] expectedAffinity = initialAffinity.ToArray();
        foreach (BattleGuardianForceVital guardian in battle.GuardianForces.Where(guardian => guardian.Acquired))
        {
            expectedAffinity[guardian.Id] = Math.Clamp(unchecked((ushort)(initialAffinity[guardian.Id] + native.RawRecord[112 + guardian.Id] - 100)), (ushort)1000, (ushort)6000);
        }

        BattleCombatant selected = battle.Combatants.First(target => BattleSession.IsValidSelectedActionTarget(caster, action, target));
        caster.Atb = BattleSession.AtbMaximum;
        int chargeStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: CasterId, TargetId: selected.Id, Ability: BattleAbility.GuardianForce, ActionId: guardianId));
        Assert.Contains(battle.GuardianForceCasts, cast => cast.ActorId == CasterId && cast.InitialTicks > 0);
        int callback = 0;
        for (; callback < CallbackBudget && battle.GuardianForceCasts.Any(cast => cast.ActorId == CasterId); callback++)
        {
            if (battle.ActiveAction is null && process.ActiveActionToken is null && battle.QueuedActions.Count == 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.ExpandedPresentationProcess);
        }
        Assert.Single(battle.Events.Skip(chargeStart), entry => entry is { Kind: "GuardianForceReady", ActorId: CasterId });

        long? token = null;
        Dictionary<BattleContactOccurrence, BattleModelOwner>? frozenRecipients = null;
        List<NativeBattleContactPacket> delivered = [];
        for (; callback < CallbackBudget && !Completed(battle, chargeStart); callback++)
        {
            BattleModelTargets? eligible = token is null
                ? BattleModelTargets.From(battle.Combatants.Where(target => BattleSession.IsValidSelectedActionTarget(caster, action, target)).Select(target => battle.ReadPresentationModelOwner(target.Id)))
                : null;
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.ExpandedPresentationProcess);
            if (token is null && child.NativeActionPackets.PublishedHeaders().FirstOrDefault(header => header.Slot.Command == 254 && header.Slot.Source == casterOwner) is { Slot: not null } admitted)
            {
                token = Assert.IsType<long>(process.ActiveActionToken);
                Assert.Equal(native.ActionId, admitted.Slot.Action);
                Assert.Equal(native.MagicId, admitted.Slot.Effect);
                NativeBattleContactPacket[] packets = child.NativeActionPackets.ReadHeaderContacts(admitted.Reference).ToArray();
                Assert.NotEmpty(packets);
                Assert.All(packets, packet => Assert.Contains(packet.Primary.Recipient, eligible!.Enumerate()));
                frozenRecipients = packets.ToDictionary(packet => packet.Occurrence!.Value, packet => packet.Primary.Recipient);
                Assert.Equal(expectedAffinity, caster.GuardianForceCompatibility);
            }
            if (token is long current && process.LastStepActionToken == current)
            {
                delivered.AddRange(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet));
            }
        }

        Assert.True(Completed(battle, chargeStart),
            $"{identity} did not complete after {callback} callbacks. Frame={battle.Frame}, graphics={process.Graphics.Callback}, queued={battle.QueuedActions.Count}, active={battle.ActiveAction?.Ability}/{battle.ActiveAction?.Phase}, token={process.ActiveActionToken}, effect={process.Graphics.OriginalEffect?.RequestedEffect}, Boost={process.PendingGuardianBoost?.Percent}, contacts={delivered.Count}/{frozenRecipients?.Count}.");
        Assert.True(process.IsActionComplete(Assert.IsType<long>(token)));
        Assert.NotNull(frozenRecipients);
        BattleContactOccurrence[] observed = delivered.Select(packet => packet.Occurrence!.Value).ToArray();
        Assert.True(observed.Length == frozenRecipients.Count && observed.Distinct().Count() == observed.Length && frozenRecipients.Keys.ToHashSet().SetEquals(observed),
            $"{identity} frozen occurrences [{string.Join(", ", frozenRecipients.Keys)}]. Receipt order [{string.Join(", ", observed)}].");
        Assert.All(delivered, packet => Assert.Equal(frozenRecipients[packet.Occurrence!.Value], packet.Primary.Recipient));
        Assert.Contains(casterOwner, process.Graphics.ActorModels.Owners);
        Assert.All(frozenRecipients.Values, owner => Assert.Contains(owner, process.Graphics.ActorModels.Owners));
        Assert.Single(battle.Events.Skip(chargeStart), entry => entry is { Kind: "ActionComplete", ActorId: CasterId });
        Assert.Equal(expectedAffinity, caster.GuardianForceCompatibility);
        Assert.Null(process.ActiveActionToken);
        Assert.Null(process.PendingGuardianBoost);
        Assert.Null(process.PendingCompletion);
        Assert.Null(battle.ActiveAction);
        Assert.Null(battle.NativeGuardianBoost);
        Assert.Empty(battle.GuardianForceCasts);

        if (battle.Outcome != BattleOutcome.Active || !battle.Combatants.Any(actor => !actor.IsPlayer && actor.IsAlive && !actor.IsEjected))
        {
            VerifyTerminalReturn(world, session, battle, process, occurrence, initialResults, initialVictories);
        }
        else
        {
            VerifyNextAttack(world, battle, process, caster);
        }
    }

    private static bool Completed(BattleSession battle, int start)
    {
        return battle.Events.Skip(start).Any(entry => entry is { Kind: "ActionComplete", ActorId: CasterId });
    }

    private static void VerifyTerminalReturn(GameWorld world, GameSession session, BattleSession battle, ExpandedBattlePresentationProcess process, BattleEncounterOccurrence occurrence, int initialResults, uint initialVictories)
    {
        for (int callback = 0; callback < 500 && battle.Outcome == BattleOutcome.Active; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, world.Battle!.ExpandedPresentationProcess);
            Assert.Equal(initialResults, session.State.AppliedBattleResultIds.Length);
        }
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.True(process.TerminalFinished);
        Assert.True(battle.CanReturnToField);
        Assert.Null(battle.DebugOutcome);
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Null(world.Events.LastFault);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Equal(initialResults + 1, session.State.AppliedBattleResultIds.Length);
        Assert.Equal(occurrence.OccurrenceId, session.State.AppliedBattleResultIds[^1]);
        Assert.Equal(initialVictories + 1, session.State.BattleStatistics!.Victories);
        Assert.NotNull(world.LastBattleRewards);
        NativeBattleFieldReturnReceipt receipt = Assert.IsType<NativeBattleFieldReturnReceipt>(world.Events.Scene.NativeBattleFieldReturn);
        Assert.Equal((byte)4, receipt.Reload.NativeResult);
        Assert.Equal(133, receipt.Reload.FieldId);
    }

    private static void VerifyNextAttack(GameWorld world, BattleSession battle, ExpandedBattlePresentationProcess process, BattleCombatant caster)
    {
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable && !actor.IsEjected);
        caster.Atb = BattleSession.AtbMaximum;
        int start = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: target.Id));
        for (int callback = 0; callback < 1200 && !Completed(battle, start); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.ExpandedPresentationProcess);
        }
        Assert.True(Completed(battle, start), "The next Attack did not complete on the attached Guardian Force process.");
        Assert.Equal(target.Id, Assert.Single(battle.Events.Skip(start), entry => entry is { Kind: "EffectContact", ActorId: CasterId, Ability: BattleAbility.Attack }).TargetId);
    }
}
