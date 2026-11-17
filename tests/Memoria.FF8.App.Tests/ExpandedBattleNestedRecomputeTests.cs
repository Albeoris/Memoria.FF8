using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleNestedRecomputeTests
{
    private const byte Demi = 13;
    private const string FourthAlly = "party-3";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Deferred_summon_and_gunblade_append_their_stage4_Demi_after_the_original_completion(bool guardian)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession field = new(ExpandedBattleEffectMatrixTests.PrepareField(directory) with { GlobalRandom = new(917) });
        field.SetExamTimer(field.ExamTimer!.SetRemaining(600));
        GameWorld world = new(field, directory, sharedAssets: game);
        try
        {
            VerifyNestedRecompute(world, field, game, guardian);
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

    private static void VerifyNestedRecompute(GameWorld world, GameSession field, GameAssets game, bool guardian)
    {
        const int shiva = 1;
        ExpandedBattleInstalledEntry.Prepare(world, field, session => ExpandedBattleGuardianTests.PrepareParty(session, learnedBoost: true, guardianId: shiva));
        ushort flags = world.CaptureBattleEntryFlags();
        GameWorld.PreparedBattleEntry entry = world.PrepareBattleEntry(ExpandedBattleInstalledEntry.Formation, flags, ExpandedBattleInstalledEntry.Seed, BattleAtbMode.TurnBased);
        BattleDefinition definition = entry.Battle.ReadDefinition();
        BattleCombatantDefinition selected = definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive && actor.NativeAi is not null && (!guardian || actor.Id == "enemy-8"))
            .OrderByDescending(actor => actor.CurrentHp).ThenByDescending(actor => actor.Spirit).First();
        BattleCombatantDefinition[] actors = definition.Combatants.Select(actor => actor.Id == selected.Id ? WithDemiCounter(actor) : actor).ToArray();
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition with { Combatants = actors }, field.State.GameplayPreferences with { AtbMode = BattleAtbMode.TurnBased }, game, world.BattleResources);
        world.PublishPreparedBattleEntry(entry with { Battle = battle });

        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(process.EntryReady);
        BattleCombatant counter = battle.Combatants.Single(actor => actor.Id == selected.Id);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == (guardian ? FourthAlly : "party-0"));
        BattleCombatant demiTarget = battle.Combatants.Single(actor => actor.Id == FourthAlly);
        BattleModelOwner source = battle.ReadPresentationModelOwner(caster.Id);
        BattleModelOwner nestedSource = battle.ReadPresentationModelOwner(counter.Id);
        BattleModelOwner nestedTarget = battle.ReadPresentationModelOwner(demiTarget.Id);
        counter.NativeCombatantFlags |= 0x10;
        int originalDemiHp = demiTarget.CurrentHp;
        int originalCounterHp = counter.CurrentHp;

        BattleNativeAction demi = Assert.Single(definition.NativeActions, action => action.Command == 2 && action.ActionId == Demi && action.CrisisLevel is null);
        Assert.Equal((ushort)107, demi.MagicId);
        ushort[] expectedAffinity = caster.GuardianForceCompatibility!.ToArray();
        if (guardian)
        {
            BattleNativeAction summon = Assert.Single(definition.NativeActions, action => action.Command == 254 && action.ActionId == 64 + shiva);
            foreach (BattleGuardianForceVital vital in battle.GuardianForces.Where(vital => vital.Acquired))
            {
                expectedAffinity[vital.Id] = Math.Clamp(unchecked((ushort)(expectedAffinity[vital.Id] + summon.RawRecord[112 + vital.Id] - 100)), (ushort)1000, (ushort)6000);
            }
        }
        else
        {
            Assert.Equal(0, caster.ModelId);
            battle.SetGunbladeMode(BattleGunbladeMode.Manual);
            caster.NativeStatus0 |= 2;
        }

        caster.Atb = BattleSession.AtbMaximum;
        int start = battle.Events.Count;
        world.ApplyBattleInput(guardian
            ? new BattleInput(BattleInputKind.Action, ActorId: caster.Id, TargetId: counter.Id, Ability: BattleAbility.GuardianForce, ActionId: shiva)
            : new BattleInput(BattleInputKind.Attack, ActorId: caster.Id, TargetId: counter.Id));

        List<(ushort Command, uint Value)> timeline = [];
        List<NativeBattleContactPacket> demiContacts = [];
        HashSet<long> admitted = [];
        long? outerToken = null;
        long? nestedToken = null;
        bool pressed = false;
        string? recompute = null;
        for (int callback = 0; callback < 3200; callback++)
        {
            if (guardian && battle.GuardianForceCasts.Any(cast => cast.ActorId == caster.Id) && battle.ActiveAction is null && process.ActiveActionToken is null && battle.QueuedActions.Count == 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            if (guardian)
            {
                battle.PublishNativeGuardianBoostMappedButtons(0x100);
                if (!pressed && battle.NativeGuardianBoost is { Enabled: true, Phase: 4, Window: 1 })
                {
                    Assert.True(battle.PressNativeGuardianBoost());
                    pressed = true;
                }
            }
            else if (!pressed && battle.GunbladeTriggerWindowOpen)
            {
                Assert.True(battle.TriggerGunblade());
                pressed = true;
            }

            int beforeTickHp = counter.CurrentHp;
            uint beforeTickFlags = counter.NativeCombatantFlags;
            int? beforeTickHits = counter.NativeRemainingHits;
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            if (guardian && outerToken.HasValue && recompute is null && ReadField<NativeBattleActionScratch>(battle, "nativeActionScratch") is { GuardianFormulaE1E: true } scratch)
            {
                recompute = $"boost={scratch.GuardianBoostPercent} lane={scratch.EventLane} E00={scratch.DeferredEffectsE00} hp={beforeTickHp}->{counter.CurrentHp} flags={beforeTickFlags:X}->{counter.NativeCombatantFlags:X} BD9={beforeTickHits}->{counter.NativeRemainingHits}";
            }
            if (outerToken.HasValue && process.LastFrameServices is { } services)
            {
                foreach (NativeBattlePresentationCompletionRequested completed in services.ReadOrderedReceipts().OfType<NativeBattlePresentationCompletionRequested>().Where(receipt => receipt.ActorId == caster.Id))
                {
                    Assert.True(process.IsCompletionAcknowledged(completed.Token));
                    timeline.Add((10, completed.Callback));
                }
                if (nestedToken.HasValue && process.LastStepActionToken == nestedToken)
                {
                    demiContacts.AddRange(services.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet));
                }
            }

            if (process.ActiveActionToken is long token && admitted.Add(token))
            {
                BattlePresentationProcess.HostedPresentationInvocation invocation = ReadField<BattlePresentationProcess.HostedPresentationInvocation>(process, "hostedPresentationInvocation");
                NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(invocation.Header);
                Assert.Equal((ushort)104, process.Graphics.Descriptors.Packets[invocation.DescriptorSlot].Command);
                if (outerToken is null && header.Source == source && (guardian ? header.Command == 254 : header.Command is 1 or 12))
                {
                    outerToken = token;
                    timeline.Add((104, header.Command));
                }
                else if (outerToken.HasValue && header.Source == nestedSource && header is { Action: Demi, Effect: 107 })
                {
                    Assert.Null(nestedToken);
                    Assert.True(process.IsActionComplete(outerToken.Value));
                    nestedToken = token;
                    timeline.Add((104, Demi));
                }
                else if (outerToken.HasValue && header.Source == source && header is { Command: 0, Action: 2 })
                {
                    timeline.Add((104, 2));
                }
            }
            if (nestedToken is long counterToken && process.IsActionComplete(counterToken) && battle.ActiveAction is null)
            {
                break;
            }
        }

        Assert.True(pressed);
        Assert.NotNull(outerToken);
        if (!nestedToken.HasValue)
        {
            string recent = string.Join(", ", battle.Events.Skip(start).Where(item => item.ActorId == caster.Id || item.ActorId == counter.Id || item.TargetId == counter.Id).TakeLast(12).Select(item => $"{item.Kind}/{item.ActorId}/{item.TargetId}/{item.Amount}"));
            Assert.Fail($"No nested Demi for {counter.Id}. HP {originalCounterHp}->{counter.CurrentHp}, flags {counter.NativeCombatantFlags:X}, BD9 {counter.NativeRemainingHits}, recompute [{recompute}], operations [{string.Join(", ", timeline)}], events [{recent}].");
        }
        Assert.True(process.IsActionComplete(nestedToken.Value));
        Assert.Equal(guardian
            ? new (ushort, uint)[] { (104, 254), (10, 0x47DE70), (104, Demi) }
            : new (ushort, uint)[] { (104, 1), (10, 0x47DE70), (104, Demi), (104, 2), (10, 0x47E120) }, timeline);
        Assert.Equal(nestedTarget, Assert.Single(demiContacts).Primary.Recipient);
        Assert.Equal(originalDemiHp - demi.Power * originalDemiHp / 16, demiTarget.CurrentHp);
        Assert.InRange(counter.CurrentHp, 1, originalCounterHp - 1);
        Assert.Equal(expectedAffinity, caster.GuardianForceCompatibility);
        Assert.Single(battle.Events.Skip(start), item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);

        BattleCombatant nextTarget = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive).OrderBy(actor => actor.Id == counter.Id).First();
        demiTarget.Atb = BattleSession.AtbMaximum;
        int next = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: demiTarget.Id, TargetId: nextTarget.Id));
        for (int callback = 0; callback < 1200 && !CommandDrained(battle, process, next, demiTarget.Id); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
        }
        Assert.True(CommandDrained(battle, process, next, demiTarget.Id), "The follow-up Attack and any native counter did not drain.");
        Assert.Equal(nextTarget.Id, Assert.Single(battle.Events.Skip(next), item => item.Kind == "EffectContact" && item.ActorId == demiTarget.Id).TargetId);
        Assert.Single(battle.Events.Skip(next), item => item.Kind == "ActionComplete" && item.ActorId == demiTarget.Id);
    }

    private static BattleCombatantDefinition WithDemiCounter(BattleCombatantDefinition actor)
    {
        NativeBattleAiDefinition ai = actor.NativeAi!;
        byte[][] scripts = ai.Scripts.ToArray();
        // Use the same installed Demi and fourth-party selector as the synthetic turn fixture.
        scripts[4] = [4, 3, 3, Demi, 6, 0];
        return actor with { NativeAi = ai with { Scripts = scripts } };
    }

    private static bool CommandDrained(BattleSession battle, BattlePresentationProcess process, int start, string actorId)
    {
        return battle.ActiveAction is null && process.ActiveActionToken is null && process.PendingCompletion is null &&
            battle.Events.Skip(start).Any(item => item.Kind == "ActionComplete" && item.ActorId == actorId);
    }

    private static T ReadField<T>(object owner, string name)
    {
        return Assert.IsType<T>(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
    }
}
