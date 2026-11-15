using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleGuardianTests
{
    private const int GuardianId = 1;
    private const string CasterId = "party-3";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Shiva_computes_once_and_finishes_on_the_fourth_casters_attached_process(bool learnedBoost)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: game);
        try
        {
            RunInstalledShiva(world, session, learnedBoost);
        }
        catch (Exception failure)
        {
            try
            {
                world.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }
            throw;
        }
        world.Dispose();
    }

    private static void RunInstalledShiva(GameWorld world, GameSession session, bool learnedBoost)
    {
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, learnedBoost));
        int initialResults = session.State.AppliedBattleResultIds.Length;
        uint initialVictories = session.State.BattleStatistics!.Victories;
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        Assert.True(process.EntryReady);

        BattleSession child = ReadField<BattleSession>(process, "presentation");
        BattleActionDefinition action = Assert.Single(battle.ReadDefinition().ActionDefinitions, candidate => candidate.Ability == BattleAbility.GuardianForce && candidate.Id == GuardianId);
        BattleNativeAction native = Assert.IsType<BattleNativeAction>(action.NativeAction);
        Assert.Equal("Diamond Dust", action.Name);
        Assert.Equal(learnedBoost, action.NativeGuardianBoostAvailable);
        Assert.True(action.AllTargets);
        Assert.False(action.Allies);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        Assert.Equal(5, enemies.Length);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == CasterId);
        BattleModelOwner casterOwner = battle.ReadPresentationModelOwner(CasterId);
        Assert.False(child.NativeActionPackets.ModelAliases.TryEncodeExact(casterOwner, out _));
        Assert.True(battle.CanSelectExpandedGuardianForce(CasterId, GuardianId));
        ushort[] originalAffinity = caster.GuardianForceCompatibility!.ToArray();
        ushort[] affinityAfterOne = originalAffinity.ToArray();
        foreach (BattleGuardianForceVital guardian in battle.GuardianForces.Where(guardian => guardian.Acquired))
        {
            affinityAfterOne[guardian.Id] = Math.Clamp(unchecked((ushort)(originalAffinity[guardian.Id] + native.RawRecord[112 + guardian.Id] - 100)), (ushort)1000, (ushort)6000);
        }
        Assert.False(originalAffinity.SequenceEqual(affinityAfterOne));

        caster.Atb = BattleSession.AtbMaximum;
        int chargeStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: CasterId, TargetId: enemies[0].Id, Ability: BattleAbility.GuardianForce, ActionId: GuardianId));
        const int callbackBudget = 3200;
        int callback = 0;
        for (; callback < callbackBudget && battle.GuardianForceCasts.Any(cast => cast.ActorId == CasterId); callback++)
        {
            if (battle.ActiveAction is null && process.ActiveActionToken is null && battle.QueuedActions.Count == 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.ExpandedPresentationProcess);
        }

        int? admittedAt = null;
        BattleContactOccurrence[]? occurrences = null;
        List<NativeBattleContactPacket> delivered = [];
        long? token = null;
        bool sawModule = false;
        bool sawWidget = false;
        bool pressed = false;
        for (; callback < callbackBudget && (admittedAt is not int start || !Completed(battle, start)); callback++)
        {
            battle.PublishNativeGuardianBoostMappedButtons(learnedBoost ? (ushort)0x100 : (ushort)0);
            if (!pressed && battle.NativeGuardianBoost is { Enabled: true, Phase: 4, Window: 1 })
            {
                Assert.True(battle.PressNativeGuardianBoost());
                pressed = true;
            }
            int beforeTick = battle.Events.Count;
            int[]? hpBeforeAdmission = admittedAt is null ? enemies.Select(enemy => enemy.CurrentHp).ToArray() : null;
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.ExpandedPresentationProcess);
            sawModule |= process.Graphics.OriginalEffect?.RequestedEffect == native.MagicId;
            if (battle.NativeGuardianBoost is { Phase: < 6 } widget)
            {
                sawWidget = true;
                Assert.Equal(learnedBoost, widget.Enabled);
            }
            if (occurrences is null)
            {
                var headers = child.NativeActionPackets.PublishedHeaders().Where(header => header.Slot.Command == 254 && header.Slot.Source == casterOwner).ToArray();
                if (headers.Length != 0)
                {
                    var (reference, header) = Assert.Single(headers);
                    admittedAt = beforeTick;
                    token = Assert.IsType<long>(process.ActiveActionToken);
                    Assert.Equal(native.ActionId, header.Action);
                    Assert.Equal(enemies.Length, header.ReadContactCount());
                    occurrences = Enumerable.Range(0, enemies.Length).Select(index => child.NativeActionPackets.ResolveContact(reference, index)).ToArray();
                    Assert.All(occurrences, occurrence => Assert.Equal(0, child.NativeActionPackets.ReadContact(occurrence).Primary.PresentedAmount));
                    Assert.Equal(hpBeforeAdmission!, enemies.Select(enemy => enemy.CurrentHp));
                    Assert.Equal(affinityAfterOne, caster.GuardianForceCompatibility);
                    NativeBattleActionScratch preliminary = ReadField<NativeBattleActionScratch>(battle, "nativeActionScratch");
                    Assert.False(preliminary.GuardianFormulaE1E);
                    Assert.True(preliminary.DeferredEffectsE00);
                }
            }
            if (token.HasValue && process.LastStepActionToken == token)
            {
                delivered.AddRange(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet));
            }
        }

        if (admittedAt is not int invocationStart || !Completed(battle, invocationStart))
        {
            Assert.Fail(DescribeStalledGuardian(battle, child, process, sawModule, sawWidget, delivered.Count));
        }
        Assert.True(sawModule);
        Assert.True(sawWidget);
        Assert.Equal(learnedBoost, pressed);
        Assert.NotNull(occurrences);
        Assert.NotNull(token);
        Assert.True(process.IsActionComplete(token.Value));
        Assert.Equal(occurrences, delivered.Select(packet => packet.Occurrence!.Value).OrderBy(occurrence => occurrence.Ordinal));
        Assert.Equal(enemies.Select(enemy => enemy.Id).Order(StringComparer.Ordinal), battle.Events.Skip(admittedAt!.Value).Where(entry => entry is { Kind: "EffectContact", ActorId: CasterId, Ability: BattleAbility.GuardianForce }).Select(entry => entry.TargetId).Order(StringComparer.Ordinal));
        Assert.Single(battle.Events.Skip(chargeStart), entry => entry is { Kind: "GuardianForceReady", ActorId: CasterId });
        Assert.Single(battle.Events.Skip(admittedAt!.Value), entry => entry is { Kind: "ActionComplete", ActorId: CasterId });
        Assert.Contains(battle.Events.Skip(admittedAt!.Value), entry => entry.NativeStopAllSounds);
        Assert.Equal(affinityAfterOne, caster.GuardianForceCompatibility);
        Assert.Equal(learnedBoost ? (byte)76 : (byte)100, ReadField<NativeBattleActionScratch>(battle, "nativeActionScratch").GuardianBoostPercent);
        Assert.Null(battle.NativeGuardianBoost);
        Assert.Null(process.PendingGuardianBoost);
        Assert.Null(battle.ExpandedEffect);
        Assert.All(process.Graphics.ActorModels.Party.Entries, entry => Assert.Equal(2, entry.State.Flags0 & 6));

        if (!learnedBoost)
        {
            Assert.All(enemies, enemy =>
            {
                Assert.Equal(0, enemy.CurrentHp);
                Assert.False(enemy.IsAlive);
            });
            VerifyLethalSummonReturn(world, session, battle, process, occurrence, initialResults, initialVictories);
            return;
        }

        string nextTarget = enemies.First(enemy => enemy.IsAlive && enemy.IsActive).Id;
        caster.Atb = BattleSession.AtbMaximum;
        int next = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: CasterId, TargetId: nextTarget));
        for (int nextCallback = 0; nextCallback < 1200 && !Completed(battle, next); nextCallback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Same(process, battle.ExpandedPresentationProcess);
        Assert.True(Completed(battle, next), "The next Attack did not complete on the attached Guardian Force process.");
        Assert.Equal(nextTarget, Assert.Single(battle.Events.Skip(next), entry => entry is { Kind: "EffectContact", ActorId: CasterId }).TargetId);
    }

    private static void VerifyLethalSummonReturn(GameWorld world, GameSession session, BattleSession battle, ExpandedBattlePresentationProcess process, BattleEncounterOccurrence occurrence, int initialResults, uint initialVictories)
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
        Assert.Single(battle.Events, item => item.Kind == "Outcome");

        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Null(world.Events.LastFault);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Equal(initialResults + 1, session.State.AppliedBattleResultIds.Length);
        Assert.Equal(occurrence.OccurrenceId, session.State.AppliedBattleResultIds[^1]);
        Assert.Equal(initialVictories + 1, session.State.BattleStatistics!.Victories);
        BattleRewardSummary rewards = Assert.IsType<BattleRewardSummary>(world.LastBattleRewards);
        Assert.Contains(rewards.CharacterExperience, item => item.Value > 0);
        NativeBattleFieldReturnReceipt receipt = Assert.IsType<NativeBattleFieldReturnReceipt>(world.Events.Scene.NativeBattleFieldReturn);
        Assert.Equal((byte)4, receipt.Reload.NativeResult);
        Assert.Equal(133, receipt.Reload.FieldId);
    }

    private static string DescribeStalledGuardian(BattleSession battle, BattleSession child, ExpandedBattlePresentationProcess process, bool sawModule, bool sawWidget, int delivered)
    {
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleActionPacketsState action = child.NativeActionPackets;
        string descriptors = string.Join(",", graphics.Descriptors.Packets.Where(packet => packet.PriorityState != 0).Select(packet => $"{packet.Command}/{packet.PriorityState}"));
        string tasks = string.Join(",", graphics.Descriptors.Tasks.Where(task => task.Kind != NativeBattleDescriptorTaskKind.None).Select(task => $"{task.Kind}/{task.Phase}"));
        string models = string.Join(",", graphics.ActorModels.Entries.Select(entry => $"{entry.Owner.Side}/{entry.State.ModelId4}:{entry.State.Kind5}/{entry.State.EffectCounter97}/{entry.State.Flags0:X}"));
        string charge = string.Join(",", battle.GuardianForceCasts.Select(cast => $"{cast.ActorId}:{cast.RemainingTicks}/{cast.InitialTicks}"));
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        object? hostedEvent = typeof(BattleSession).GetField("expandedHostedEvent", privateInstance)!.GetValue(battle);
        object? completion = hostedEvent?.GetType().GetProperty("CompletionToken")?.GetValue(hostedEvent);
        object? operation = typeof(BattleSession).GetField("expandedHostedCurrentOperation", privateInstance)!.GetValue(battle);
        return $"Shiva did not complete. Frame={battle.Frame} graphics={graphics.Callback} outcome={battle.Outcome} charge=[{charge}] logical={battle.ActiveAction?.Ability}/{battle.ActiveAction?.Phase} queue={battle.QueuedActions.Count} operation={operation} completion={completion}/{process.PendingCompletion?.Token}. Token={process.ActiveActionToken} boost={battle.NativeGuardianBoost?.Phase}/{battle.NativeGuardianBoost?.Remaining} pendingBoost={process.PendingGuardianBoost?.Percent} sawWidget={sawWidget} header={action.ActiveHeader?.Ordinal} cursor={action.ActiveContact?.Ordinal} packets={action.ActionCount}/{action.ContactCount} delivered={delivered}. Descriptors=[{descriptors}] tasks=[{tasks}] effect={graphics.OriginalEffect?.RequestedEffect}/{graphics.OriginalEffect?.ReturnedDescriptor} sawModule={sawModule} models=[{models}] modelLoads={graphics.ModelLoadCount} effects={graphics.Effects.PublishedCount} modules={graphics.ModuleEffects.PublishedCount} camera={process.Camera.Mask}.";
    }

    private static bool Completed(BattleSession battle, int from)
    {
        return battle.Events.Skip(from).Any(entry => entry is { Kind: "ActionComplete", ActorId: CasterId });
    }

    internal static void PrepareParty(GameSession session, bool learnedBoost, int guardianId = GuardianId)
    {
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords();
        vitals = vitals.AcquireGuardianForce(guardianId).RefreshGuardianForces().Rest();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        int existing = Array.FindIndex(vitals.Records, record => record.Junction.GuardianForceAssignments.Contains(guardianId));
        if (existing == -1)
        {
            vitals = vitals.ToggleGuardianForceJunction(3, guardianId);
        }
        else if (existing != 3)
        {
            vitals = vitals.ReassignGuardianForceJunction(3, guardianId, existing);
        }

        int bit = Assert.Single(Enumerable.Range(19, 9), index => (vitals.Definitions.GuardianForceModifiers[index - 19].Flags & 1) != 0);
        NativeGuardianForceVital guardian = vitals.GuardianForces[guardianId];
        ulong boost = 1UL << bit;
        vitals.GuardianForces[guardianId] = guardian with { LearnedBank = guardian.LearnedBank with { High = learnedBoost ? guardian.LearnedBank.High | boost : guardian.LearnedBank.High & ~boost } };
        NativeCharacterVital caster = vitals.Records[3];
        int summonCommand = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)3);
        Assert.True(summonCommand >= 0);
        vitals.Records[3] = caster with { Junction = caster.Junction with { Commands = (uint)(20 + summonCommand) }, GuardianForceCompatibility = Enumerable.Repeat((ushort)3500, 16).ToArray() };
        session.SetCharacterVitals(vitals.WithBattleParty([0, 1, 2, 3], 4));
    }

    private static T ReadField<T>(object owner, string name)
    {
        return Assert.IsType<T>(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
    }
}
