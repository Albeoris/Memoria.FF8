using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedBattlePresentationProcessActionTests
{
    private const int EntryBudget = 1200;
    private const int ActionBudget = 1500;
    private const int StandingReturnBudget = 240;

    [Fact]
    public void Installed_zero_action_completion_retires_and_preserves_the_process_for_attack()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (definition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, 5, 4, expanded: true);
        selected = selected with { NativeActionCameraRules = game.ReadNativeActionCameraRules() };
        BattleSession logical = new(definition, BattleAtbMode.TurnBased);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        for (int frame = 0; frame < EntryBudget && !process.EntryReady; frame++)
        {
            process.Advance(logical);
        }
        Assert.True(process.EntryReady);
        const string actorId = "party-1";
        BattleModelOwner owner = logical.ReadPresentationModelOwner(actorId);
        BattleModelOwner target = logical.ReadPresentationModelOwner("enemy-8");
        var models = Models(process);
        NativeBattleModelCpuResources cpu = process.ModelCpuResources;
        string before = ExpandedBattlePresentationProcessEntryTests.LogicalState(logical);
        BattleSession.PreparedHostedDispatch prepared = process.PrepareCompletion(0x47DDA0, owner, actorId);
        process.ValidatePreparedCompletion(prepared);
        long token = process.PublishCompletion(prepared);
        BattleCombatant actor = Assert.Single(logical.Combatants, candidate => candidate.Id == actorId);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        int record = Array.FindIndex(vitals.Records, value => value.CharacterId == actor.ModelId);
        BattleNativeAction attack = assets.ReadPlayerNativeAttackData(vitals, record);
        NativeBattleActionHeaderPreparation header = Prepare(owner, attack, selected.NativeCommandSequences);
        NativeBattleContactPacket packet = Contact(target, attack, 137);
        long? nextAction = null;
        Descriptor? nextDescriptor = null;
        uint? prePostUi = null;
        for (int frame = 0; frame < ActionBudget && nextAction is null; frame++)
        {
            process.Advance(logical, afterGraphics: (services, graphicsToken) =>
            {
                NativeBattlePresentationCompletionRequested? request = services.ReadOrderedReceipts().OfType<NativeBattlePresentationCompletionRequested>().SingleOrDefault();
                if (request is null)
                {
                    return;
                }
                Assert.Null(graphicsToken);
                Assert.Equal(new NativeBattlePresentationCompletionRequested(token, 0x47DDA0, owner, actorId), request);
                Assert.Same(request, process.PendingCompletion);
                Assert.False(process.IsCompletionAcknowledged(token));
                prePostUi = process.Graphics.UiServices!.Counter;
                Assert.Throws<InvalidOperationException>(() => process.Advance(logical));
                Assert.Throws<InvalidOperationException>(() => process.AccumulateElapsed(TimeSpan.FromSeconds(1)));
                Assert.Throws<InvalidOperationException>(() => process.SetActionMenu(true));
                process.PublishCompletionAck(process.PrepareCompletionAck(token, true));
                nextAction = process.PublishAction(process.PrepareAction(header, [packet]));
                nextDescriptor = ReadDescriptor(process);
            });
            Assert.Null(process.LastStepActionToken);
            Assert.Empty(process.LastStep.Contacts);
        }
        Assert.Equal(before, ExpandedBattlePresentationProcessEntryTests.LogicalState(logical));
        Assert.Single(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationCompletionRequested>());
        Assert.Equal(Assert.IsType<uint>(prePostUi) + 1, process.Graphics.UiServices!.Counter);
        Assert.True(process.IsCompletionAcknowledged(token));
        Assert.Null(process.PendingCompletion);
        long action = Assert.IsType<long>(nextAction);
        Assert.Equal(action, process.ActiveActionToken);

        // Reject the handoff of a reached native AA contact and observe the following frame.
        // Its graphics have already committed. Only the failed frame's output must not replay.
        InvalidOperationException injected = new("Canonical handoff failed after observing the contact.");
        bool sawContact = false;
        for (int frame = 0; frame < ActionBudget && !sawContact; frame++)
        {
            NativeBattleEffectPresentationStep previous = process.LastStep;
            try
            {
                process.Advance(logical, afterGraphics: (services, graphicsToken) =>
                {
                    NativeBattlePresentationContactReceived? contact = services.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().SingleOrDefault();
                    if (contact is not null)
                    {
                        Assert.Equal(action, graphicsToken);
                        Assert.Equal(packet, contact.Packet);
                        throw injected;
                    }
                });
            }
            catch (InvalidOperationException failure) when (ReferenceEquals(failure, injected))
            {
                Assert.Same(previous, process.LastStep);
                Assert.Empty(process.Graphics.PendingContacts);
                sawContact = true;
            }
        }
        Assert.True(sawContact);
        Assert.NotNull(nextDescriptor);
        for (int frame = 0; frame < ActionBudget && !process.IsActionComplete(action); frame++)
        {
            process.Advance(logical);
            Assert.Empty(process.LastStep.Contacts);
        }
        Assert.True(process.IsActionComplete(action));
        Assert.Equal(models, Models(process));
        Assert.Same(cpu, process.ModelCpuResources);
    }

    [Theory]
    [InlineData(BattleAbility.Draw, false)]
    [InlineData(BattleAbility.Magic, true)]
    public void Main_failed_draw_and_silenced_fire_finish_native_control_and_allow_ordinary_attack(BattleAbility ability, bool nativeClock)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, 5, 4, expanded: true, nativeClock: nativeClock);
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Fire");
        const string caster = "party-1";
        const string target = "enemy-8";
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        logicalDefinition = logicalDefinition with
        {
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = [fire],
            NativeCommandSequences = selected.NativeCommandSequences,
            NativeKnownMagic = new(NativeInputConfiguration.OriginalInitHash, [0, 0]),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeCommandNames = assets.ReadNativeCommandNames(),
            NativeMessageSpeed = 1,
            Combatants = logicalDefinition.Combatants.Select(actor => actor.Id == caster
                ? actor with { Attack = 35, Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.Draw], MagicStocks = [new(fire.Id, 5)], NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 1), NativeStatusResistances = new byte[40] }
                : actor.Id == "party-3" ? actor with { Attack = 35, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 3), NativeStatusResistances = new byte[40] }
                : actor.Id == target ? actor with { MaximumHp = 6000, CurrentHp = 6000, NativeDrawSlots = [new((byte)fire.Id, 0), new(0, 0), new(0, 0), new(0, 0)], NativeStatusResistances = new byte[40] }
                : actor.NativeTemplate is { } template ? actor with { MaximumHp = template.Levels[actor.Level].MaximumHp, CurrentHp = template.Levels[actor.Level].MaximumHp }
                : actor).ToArray()
        };
        BattleSession logical = new(logicalDefinition, nativeClock ? BattleAtbMode.Normal : BattleAtbMode.TurnBased);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        logical.AttachExpandedPresentationProcess(process);
        for (int callback = 0; callback < EntryBudget && !logical.NativeEntryReady; callback++)
        {
            logical.Tick();
        }
        Assert.True(logical.NativeEntryReady);
        var models = Models(process);
        BattleCombatant actor = logical.Combatants.Single(item => item.Id == caster);
        BattleCombatant enemy = logical.Combatants.Single(item => item.Id == target);
        BattleCombatant idle = logical.Combatants.Single(item => item.Id == "party-4");
        if (nativeClock)
        {
            idle.Atb = 0;
            idle.NativeStatusTimers[1] = 100;
        }
        int mark = logical.Events.Count;
        logical.QueueAction(caster, target, ability, fire.Id);
        logical.Tick();
        Assert.Equal(BattleActionPhase.Windup, logical.ActiveAction?.Phase);
        NativeBattleAtbState? heldGauge = idle.NativeAtb;
        short heldTimer = idle.NativeStatusTimers[1];
        actor.NativeStatus0 |= 0x10;
        long? token = null;
        bool reachedControl = false;
        bool sequenceTen = false;
        BattleModelOwner casterOwner = logical.ReadPresentationModelOwner(caster);
        for (int callback = 0; callback < ActionBudget && logical.ActiveAction is not null; callback++)
        {
            logical.Tick();
            token ??= process.LastStepActionToken;
            reachedControl |= process.Graphics.ActionGraphics is { Callback: 0x50BD00, Actor: var owner } && owner == casterOwner;
            sequenceTen |= process.Graphics.ActorModels.Get(casterOwner).Sequence == 10;
            Assert.Empty(process.LastStep.Contacts);
            if (nativeClock && logical.ActiveAction is not null)
            {
                Assert.Equal(1u, logical.Capture().NativeClock!.ActionPause);
                Assert.Equal(heldGauge, idle.NativeAtb);
                Assert.Equal(heldTimer, idle.NativeStatusTimers[1]);
            }
        }
        Assert.Null(logical.ActiveAction);
        if (nativeClock)
        {
            Assert.Equal(0u, logical.Capture().NativeClock!.ActionPause);
            int postPoll = logicalDefinition.NativeClockRules!.AtbSpeedMultiplier * (actor.NativeAtbSpeed + 30) / 10;
            Assert.Equal(postPoll, actor.NativeAtb!.Current);
        }
        Assert.True(process.IsActionComplete(Assert.IsType<long>(token)));
        Assert.True(reachedControl && sequenceTen);
        Assert.Equal(6000, enemy.CurrentHp);
        Assert.Equal(5, logical.GetAvailableMagicCasts(caster, fire.Id));
        Assert.Single(logical.Events.Skip(mark), item => item.Kind == "ActionComplete" && item.ActorId == caster);
        Assert.Contains(logical.Events.Skip(mark), item => item.ActorId == caster && item.Kind == (ability == BattleAbility.Draw ? "Draw" : "NativeText"));
        Assert.DoesNotContain(logical.Events.Skip(mark), item => item.Kind == "EffectContact" && item.ActorId == caster);

        string next = "party-3";
        logical.QueueAction(next, target, BattleAbility.Attack, 0);
        for (int callback = 0; callback < ActionBudget && (logical.ActiveAction is not null || logical.QueuedActions.Count != 0); callback++)
        {
            logical.Tick();
        }
        Assert.Null(logical.ActiveAction);
        Assert.True(enemy.CurrentHp < 6000);
        Assert.Same(process, logical.ExpandedPresentationProcess);
        Assert.Equal(models, Models(process));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void Main_queued_repeated_and_reflected_fire_admit_each_invocation_on_the_same_process(int casts, bool reflect)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, 5, 4, expanded: true);
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Fire");
        const string caster = "party-1";
        const string target = "enemy-8";
        logicalDefinition = logicalDefinition with
        {
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = [fire],
            NativeCommandSequences = selected.NativeCommandSequences,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            Combatants = logicalDefinition.Combatants.Select(actor => actor.Id == caster
                ? actor with { Magic = 15, MaximumHp = 6000, CurrentHp = 6000, Abilities = [BattleAbility.Attack, BattleAbility.Magic], MagicStocks = [new(fire.Id, 5)], NativeStatus0 = reflect ? (ushort)0 : (ushort)2, NativeStatus1 = casts == 3 ? 0x40000u : casts == 2 ? 0x20000u : 0u }
                : actor.Id == target ? actor with { MaximumHp = 6000, CurrentHp = 6000, NativeStatus1 = reflect ? 0x80u : 0u }
                : actor.NativeTemplate is { } template ? actor with { MaximumHp = template.Levels[actor.Level].MaximumHp, CurrentHp = template.Levels[actor.Level].MaximumHp }
                : actor).ToArray()
        };
        selected = selected with
        {
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([fire.NativeAction!.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        };
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        logical.AttachExpandedPresentationProcess(process);
        for (int callback = 0; callback < EntryBudget && !logical.NativeEntryReady; callback++)
        {
            logical.Tick();
        }
        Assert.True(logical.NativeEntryReady);
        var models = Models(process);

        logical.QueueAction(caster, target, BattleAbility.Magic, fire.Id, magicTargets: reflect ? null : Enumerable.Repeat(target, casts).ToArray());
        List<long> tokens = [];
        List<NativeBattleContactPacket> contacts = [];
        for (int callback = 0; callback < ActionBudget && (logical.ActiveAction is not null || logical.QueuedActions.Count != 0); callback++)
        {
            logical.Tick();
            if (process.LastStepActionToken is long token && (tokens.Count == 0 || tokens[^1] != token))
            {
                Assert.All(tokens, earlier => Assert.True(process.IsActionComplete(earlier)));
                tokens.Add(token);
            }
            contacts.AddRange(process.LastStep.Contacts);
        }

        Assert.Null(logical.ActiveAction);
        Assert.Equal(reflect ? 2 : casts + 1, tokens.Count);
        Assert.All(tokens, token => Assert.True(process.IsActionComplete(token)));
        Assert.Equal(reflect ? 2 : casts + 1, contacts.Count);
        Assert.Equal(logical.ReadPresentationModelOwner(target), contacts[0].Primary.Recipient);
        if (reflect)
        {
            Assert.IsAssignableFrom<BattlePartyModelOwner>(contacts[1].Primary.Recipient);
            Assert.Equal(6000, logical.Combatants.Single(actor => actor.Id == target).CurrentHp);
        }
        else
        {
            Assert.All(contacts.Take(casts), packet => Assert.Equal(logical.ReadPresentationModelOwner(target), packet.Primary.Recipient));
            Assert.Equal(logical.ReadPresentationModelOwner(caster), contacts[^1].Primary.Recipient);
            Assert.InRange(contacts[^1].Primary.Amount6, 300, 540);
            Assert.True(logical.Combatants.Single(actor => actor.Id == target).CurrentHp < 6000);
        }
        Assert.Equal(5 - casts, logical.GetAvailableMagicCasts(caster, fire.Id));
        Assert.Equal(contacts.Count, logical.Events.Count(item => item.Kind == "EffectContact" && item.ActorId == caster));
        Assert.Single(logical.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
        Assert.Equal(models, Models(process));
    }

    [Theory]
    [InlineData(4, 5, "enemy-8", "party-0", false)]
    [InlineData(11, 14, "enemy-17", "party-10", false)]
    [InlineData(4, 5, "enemy-8", "party-0", true)]
    public void Main_queued_attack_then_fire_use_the_attached_process_once(int allyCount, int enemyCount, string target, string fireActor, bool poison)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, allyCount, enemyCount, 4, expanded: true);
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Fire");
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        BattleCombatantDefinition[] actors = logicalDefinition.Combatants.Select(actor => actor.IsPlayer ? actor with
        {
            NativeAttack = assets.ReadPlayerNativeAttackData(vitals, Array.FindIndex(vitals.Records, row => row.CharacterId == actor.ModelId)),
            Abilities = [BattleAbility.Attack, BattleAbility.Magic],
            MagicStocks = [new(fire.Id, 5)],
            Attack = 35,
            Magic = 30,
            NativeStatus0 = actor.Id == "party-1" && poison ? (ushort)2 : (ushort)0,
            NativeStatusResistances = new byte[40]
        } : actor.NativeTemplate is { } template
            ? actor with { MaximumHp = template.Levels[actor.Level].MaximumHp, CurrentHp = template.Levels[actor.Level].MaximumHp }
            : actor with { MaximumHp = 6000, CurrentHp = 6000, NativeStatusResistances = new byte[40] }).ToArray();
        logicalDefinition = logicalDefinition with { Combatants = actors, NativeActions = assets.NativeActions.ToArray(), ActionDefinitions = [fire], NativeCommandSequences = selected.NativeCommandSequences, NativeFieldBattleRules = 0 };
        selected = selected with
        {
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([fire.NativeAction!.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        };
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        BattleSession control = new(logicalDefinition, BattleAtbMode.TurnBased);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        logical.AttachExpandedPresentationProcess(process);
        for (int callback = 0; callback < EntryBudget && !logical.NativeEntryReady; callback++)
        {
            logical.Tick();
        }
        Assert.True(logical.NativeEntryReady);

        const string attackActor = "party-1";
        BattleModelOwner attackOwner = logical.ReadPresentationModelOwner(attackActor);
        BattleModelOwner fireOwner = logical.ReadPresentationModelOwner(fireActor);
        BattleModelOwner targetOwner = logical.ReadPresentationModelOwner(target);
        var originalModels = Models(process);
        NativeBattleModelCpuResources cpu = process.ModelCpuResources;
        NativeBattleVector standing = HeaderDisplacement(process.Graphics.ActorModels.Get(attackOwner));
        NativeBattleVector targetPosition = process.Graphics.ActorModels.Get(targetOwner).Transform!.Position28;
        double standingDistance = WorldDistance(process.Graphics.ActorModels.Get(attackOwner), targetPosition);
        RunMain(BattleAbility.Attack, 0, attackActor, attackOwner, 0x50BB00);
        RejectPreparedFireWithoutPublication();
        RunMain(BattleAbility.Magic, fire.Id, fireActor, fireOwner, 0x50A9A0, alreadyQueued: true);
        Assert.True(ReturnedToStanding(process.Graphics.ActorModels.Get(attackOwner), standing));
        Assert.Equal(4, logical.GetAvailableMagicCasts(fireActor, fire.Id));
        Assert.Equal(originalModels, Models(process));
        Assert.Same(cpu, process.ModelCpuResources);
        Assert.Same(process, logical.ExpandedPresentationProcess);
        Assert.Null(logical.NativeGraphics);

        void RunMain(BattleAbility ability, int id, string actor, BattleModelOwner sourceOwner, uint service, bool alreadyQueued = false)
        {
            int startHp = logical.Combatants.Single(item => item.Id == target).CurrentHp;
            BattleCombatant caster = logical.Combatants.Single(item => item.Id == actor);
            int sourceHp = caster.CurrentHp;
            int firstEvent = logical.Events.Count;
            if (!alreadyQueued)
            {
                logical.QueueAction(actor, target, ability, id);
            }
            control.QueueAction(actor, target, ability, id);
            for (int callback = 0; callback < 150 && (control.ActiveAction is not null || control.QueuedActions.Count != 0); callback++)
            {
                control.Tick();
            }
            Assert.Null(control.ActiveAction);
            List<NativeBattleContactPacket> contacts = [];
            long? token = null;
            bool reachedService = false;
            bool delayedCompletion = false;
            for (int callback = 0; callback < ActionBudget && (logical.ActiveAction is not null || logical.QueuedActions.Count != 0); callback++)
            {
                bool delayedThisPass = !delayedCompletion && poison && ability == BattleAbility.Attack && RotateCompletedActionBehindCompletion(process);
                logical.Tick();
                token ??= process.LastStepActionToken;
                if (delayedThisPass)
                {
                    delayedCompletion = true;
                    Assert.True(process.IsActionComplete(Assert.IsType<long>(token)));
                    Assert.NotNull(logical.ActiveAction);
                    Assert.Null(process.PendingCompletion);
                    Assert.Equal(sourceHp, caster.CurrentHp);
                    Assert.DoesNotContain(logical.Events.Skip(firstEvent), item => item.Kind == "ActionComplete");
                }
                if (process.Graphics.ActionGraphics is { } action && process.LastStepActionToken == token && token is not null)
                {
                    Assert.Equal(sourceOwner, action.Actor);
                    reachedService |= action.Callback == service;
                }
                contacts.AddRange(process.LastStep.Contacts);
                if (ability == BattleAbility.Attack && process.LastStepActionToken == token && !process.LastStep.Contacts.IsEmpty)
                {
                    Assert.True(WorldDistance(process.Graphics.ActorModels.Get(sourceOwner), targetPosition) < standingDistance);
                }
                if (poison && ability == BattleAbility.Attack && token is long current && !process.IsActionComplete(current))
                {
                    Assert.Equal(sourceHp, caster.CurrentHp);
                }
            }
            Assert.Null(logical.ActiveAction);
            Assert.True(reachedService);
            Assert.True(process.IsActionComplete(Assert.IsType<long>(token)));
            bool expectsPoison = poison && ability == BattleAbility.Attack;
            Assert.Equal(expectsPoison ? 2 : 1, contacts.Count);
            NativeBattleContactPacket contact = contacts[0];
            Assert.Equal(targetOwner, contact.Primary.Recipient);
            if (expectsPoison)
            {
                Assert.True(delayedCompletion);
                NativeBattleContactHalf self = contacts[1].Primary;
                Assert.Equal(sourceOwner, self.Recipient);
                Assert.InRange(self.Amount6, (ushort)25, (ushort)45);
                Assert.Equal(sourceHp - self.Amount6, caster.CurrentHp);
                Assert.Equal(caster.CurrentHp, logical.ReadPresentedVital(caster.Id).HitPoints);
            }
            int hp = logical.Combatants.Single(item => item.Id == target).CurrentHp;
            Assert.True(startHp > hp);
            Assert.Equal(control.Combatants.Single(item => item.Id == target).CurrentHp, hp);
            Assert.Equal(JsonSerializer.Serialize(control.Capture().Random), JsonSerializer.Serialize(logical.Capture().Random));
            BattleEvent[] events = logical.Events.Skip(firstEvent).ToArray();
            Assert.Single(events, item => item.Kind == "EffectContact" && item.ActorId == actor && item.TargetId == target);
            Assert.Equal(expectsPoison ? 1 : 0, events.Count(item => item.Kind == "EffectContact" && item.ActorId == actor && item.TargetId == actor));
            Assert.Single(events, item => item.Kind == "ActionComplete" && item.ActorId == actor);
        }

        void RejectPreparedFireWithoutPublication()
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            BattleSession child = (BattleSession)typeof(ExpandedBattlePresentationProcess).GetField("presentation", privateInstance)!.GetValue(process)!;
            PropertyInfo graphicsProperty = typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!;
            MethodInfo commit = typeof(BattleSession).GetMethod("PrepareAndPublishExpandedAction", privateInstance)!;
            NativeBattleDescriptorState full = NativeBattleDescriptorState.Clear();
            for (int index = 0; index < 29; index++)
            {
                (full, _) = full.Enqueue(112, 128, 0);
            }
            logical.QueueAction(fireActor, target, BattleAbility.Magic, fire.Id);
            logical.Tick();
            BattleActionPresentation action = Assert.IsType<BattleActionPresentation>(logical.ActiveAction);
            Assert.Equal(BattleActionPhase.Windup, action.Phase);
            NativeBattleGraphicsState original = process.Graphics;
            NativeBattleGraphicsState blocked = original with { Descriptors = full };
            graphicsProperty.SetValue(child, blocked);
            string before = JsonSerializer.Serialize(logical.Capture());
            try
            {
                TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => commit.Invoke(logical, [action]));
                Assert.Contains("exhausted its physical nodes", Assert.IsType<InvalidOperationException>(failure.InnerException).Message);
                Assert.Same(blocked, process.Graphics);
                Assert.Null(process.ActiveActionToken);
                Assert.Equal(before, JsonSerializer.Serialize(logical.Capture()));
            }
            finally
            {
                graphicsProperty.SetValue(child, original);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Main_darkside_applies_aftercare_in_order_and_lethal_cost_skips_poison(bool lethal)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, 5, 4, expanded: true);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        BattleNativeCommandBinding darkside = assets.ReadNativeCommand(28);
        const string caster = "party-1";
        const string target = "enemy-8";
        const int maximumHp = 500;
        int hp = lethal ? 50 : maximumHp;
        logicalDefinition = logicalDefinition with
        {
            NativeActions = assets.NativeActions.ToArray(),
            NativeCommandSequences = selected.NativeCommandSequences,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeRetainedMemory = selected.NativeRetainedMemory,
            NativeFieldBattleRules = 0,
            Combatants = logicalDefinition.Combatants.Select(actor => actor.Id == caster
                ? actor with
                {
                    NativeAttack = assets.ReadPlayerNativeAttackData(vitals, Array.FindIndex(vitals.Records, row => row.CharacterId == actor.ModelId)),
                    NativeCommands = [darkside],
                    MaximumHp = maximumHp,
                    CurrentHp = hp,
                    NativeStatus0 = 2,
                    NativeStatusResistances = new byte[40]
                }
                : actor.Id == target ? actor with { MaximumHp = 6000, CurrentHp = 6000, NativeStatusResistances = new byte[40] }
                : actor.NativeTemplate is { } template ? actor with { MaximumHp = template.Levels[actor.Level].MaximumHp, CurrentHp = template.Levels[actor.Level].MaximumHp }
                : actor).ToArray()
        };
        selected = selected with { NativeActionCameraRules = game.ReadNativeActionCameraRules() };
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        Assert.Empty(logical.GetNativePlayerActionChoices(caster, limits: false, command: 28));
        ExpandedBattlePresentationProcess process = new(logical, selected);
        logical.AttachExpandedPresentationProcess(process);
        for (int callback = 0; callback < EntryBudget && !logical.NativeEntryReady; callback++)
        {
            logical.Tick();
        }
        Assert.True(logical.NativeEntryReady);
        BattleModelOwner casterOwner = logical.ReadPresentationModelOwner(caster);
        BattleModelOwner targetOwner = logical.ReadPresentationModelOwner(target);
        NativeBattleModelAliases aliases = logical.ReadBattlePresentationPacketBindings().Aliases;
        Assert.False(aliases.TryEncodeExact(casterOwner, out _));
        Assert.False(aliases.TryEncodeExact(targetOwner, out _));
        BattleCombatant actorState = logical.Combatants.Single(actor => actor.Id == caster);
        var models = Models(process);
        BattleActionDefinition choice = Assert.Single(logical.GetNativePlayerActionChoices(caster, limits: false, command: 28));
        Assert.Equal(28 << 16, choice.Id);
        BattleNativeAction native = Assert.IsType<BattleNativeAction>(choice.NativeAction);
        Assert.Equal((byte)0, native.AttackFlags);
        logical.QueueNativePlayerCommand(caster, target, 28, 0);
        BattleSnapshot queued = logical.Capture();
        BattleAttackCommand queuedCommand = Assert.Single(queued.QueuedActions);
        BattleNativeAction forged = native with { Power = (byte)(native.Power ^ 1) };
        InvalidDataException wrongWeapon = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(queued with { QueuedActions = [queuedCommand with { NativeAction = forged }] }));
        Assert.Contains("differs from its installed kernel contract", wrongWeapon.Message);

        long? attack = null;
        List<long> tokens = [];
        List<NativeBattleContactPacket> contacts = [];
        for (int callback = 0; callback < ActionBudget && (logical.ActiveAction is not null || logical.QueuedActions.Count != 0); callback++)
        {
            logical.Tick();
            if (process.LastStepActionToken is long token && (tokens.Count == 0 || tokens[^1] != token))
            {
                attack ??= token;
                tokens.Add(token);
            }
            if (attack is not long primary || !process.IsActionComplete(primary))
            {
                Assert.Equal(hp, actorState.CurrentHp);
            }
            contacts.AddRange(process.LastStep.Contacts);
        }

        Assert.Null(logical.ActiveAction);
        Assert.Equal(lethal ? 2 : 3, tokens.Count);
        Assert.All(tokens, token => Assert.True(process.IsActionComplete(token)));
        Assert.Equal(lethal ? new BattleModelOwner[] { targetOwner, casterOwner } : [targetOwner, casterOwner, casterOwner], contacts.Select(packet => packet.Primary.Recipient));
        BattleEvent cost = Assert.Single(logical.Events, item => item.Kind == "Damage" && item.Message.StartsWith("Darkside cost:", StringComparison.Ordinal));
        Assert.Equal(50, cost.Amount);
        if (lethal)
        {
            Assert.Equal(0, actorState.CurrentHp);
            Assert.DoesNotContain(logical.Events, item => item.Message.StartsWith("Poison:", StringComparison.Ordinal));
        }
        else
        {
            BattleEvent poison = Assert.Single(logical.Events, item => item.Kind == "Damage" && item.Message.StartsWith("Poison:", StringComparison.Ordinal));
            Assert.InRange(poison.Amount, 25, 45);
            Assert.True(logical.Events.ToList().IndexOf(cost) < logical.Events.ToList().IndexOf(poison));
            Assert.Equal(hp - cost.Amount - poison.Amount, actorState.CurrentHp);
        }
        Assert.Single(logical.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster);
        Assert.Equal(models, Models(process));
    }

    [Fact]
    public void Main_gunblade_holds_Oilboyles_installed_stage_two_until_descriptor_completion()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        const int formation = 79;
        const string source = "party-0";
        int authoredCount = assets.LoadEncounter(formation).Enemies.Count(enemy => enemy.Enabled);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, authoredCount, authoredCount, expanded: true, encounterId: formation);
        BattleCombatantDefinition authoredEnemy = logicalDefinition.Combatants.First(actor => !actor.IsPlayer && actor.ModelId == 99);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(authoredEnemy.ModelId, authoredEnemy.Level);
        Assert.Equal(new byte[] { 4, 203, 12, 2 }, ai.Scripts[2][16..20]);
        NativeBattleAiAbility expectedRetaliation = ai.Abilities[16 * ai.LevelBand + 2];
        Assert.Equal((byte)8, expectedRetaliation.Command);
        Assert.Equal((ushort)181, expectedRetaliation.Id);
        int maximumHp = authoredEnemy.NativeTemplate!.Levels[authoredEnemy.Level].MaximumHp;
        string targetId = authoredEnemy.Id;
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        BattleCombatantDefinition[] actors = logicalDefinition.Combatants.OrderBy(actor => actor.IsPlayer ? actor.Id == source ? 1 : 0 : 2).Select(actor =>
            actor.Id == source ? actor with
            {
                NativeAttack = assets.ReadPlayerNativeAttackData(vitals, Array.FindIndex(vitals.Records, row => row.CharacterId == actor.ModelId)),
                Attack = 35
            } : actor.Id == targetId ? actor with
            {
                NativeAi = ai, NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(actor.ModelId), CurrentHp = maximumHp / 3, MaximumHp = maximumHp
            } : actor.NativeTemplate is { } template ? actor with
            {
                MaximumHp = template.Levels[actor.Level].MaximumHp, CurrentHp = template.Levels[actor.Level].MaximumHp
            } : actor).ToArray();
        NativeLimitProgress options = selected.LimitProgress! with { Options978 = (ushort)(selected.LimitProgress!.Options978!.Value & ~1) };
        logicalDefinition = logicalDefinition with
        {
            Combatants = actors, NativeActions = assets.NativeActions.ToArray(), NativeCommandSequences = selected.NativeCommandSequences,
            NativeFieldBattleRules = 0, NativeRetainedMemory = selected.NativeRetainedMemory, LimitProgress = options,
            InitialRandom = NativeBattleRandomState.ForecastPhase(5)
        };
        selected = selected with { LimitProgress = options, NativeActionCameraRules = game.ReadNativeActionCameraRules() };
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        logical.AttachExpandedPresentationProcess(process);
        for (int callback = 0; callback < EntryBudget && !logical.NativeEntryReady; callback++)
        {
            logical.Tick();
        }
        Assert.True(logical.NativeEntryReady);
        Assert.Empty(logical.QueuedActions);
        BattleModelOwner sourceOwner = logical.ReadPresentationModelOwner(source);
        Assert.False(logical.ReadBattlePresentationPacketBindings().Aliases.TryEncodeExact(sourceOwner, out _));
        BattleCombatant enemy = Assert.Single(logical.Combatants, actor => actor.Id == targetId);
        Assert.Equal(maximumHp, enemy.MaximumHp);
        BattleModelOwner targetOwner = logical.ReadPresentationModelOwner(targetId);
        logical.QueueAttack(source, targetId);
        long? token = null;
        bool sawPlaceholder = false;
        bool sawRecomputedHit = false;
        int contactCount = 0;
        int hp = enemy.CurrentHp;
        NativeBattleRandomState? beforeRetaliation = null;
        for (int callback = 0; callback < ActionBudget && (token is null || !process.IsActionComplete(token.Value)); callback++)
        {
            beforeRetaliation = logical.Capture().Random!;
            logical.Tick();
            token ??= process.LastStepActionToken;
            if (token is null || !process.IsActionComplete(token.Value))
            {
                Assert.DoesNotContain(logical.QueuedActions, command => command.NativeAiCallbackStage == 2);
            }
            if (token is not null && !process.IsActionComplete(token.Value))
            {
                Assert.Equal((byte)2, enemy.NativePendingReaction.Stage);
                Assert.NotNull(logical.Capture().NativeReactionOrder);
                sawPlaceholder |= enemy.CurrentHp == hp;
                sawRecomputedHit |= enemy.CurrentHp < hp;
            }
            if (!process.LastStep.Contacts.IsEmpty)
            {
                Assert.Equal(token, process.LastStepActionToken);
                NativeBattleContactPacket contact = Assert.Single(process.LastStep.Contacts);
                Assert.Equal(targetOwner, contact.Primary.Recipient);
                Assert.True(enemy.CurrentHp < hp);
                contactCount++;
            }
        }
        Assert.True(sawPlaceholder);
        Assert.True(sawRecomputedHit);
        Assert.Equal(1, contactCount);
        long completed = Assert.IsType<long>(token);
        Assert.True(process.IsActionComplete(completed));
        Assert.Null(logical.ActiveAction);
        Assert.Equal((byte)0, enemy.NativePendingReaction.Stage);
        Assert.Null(logical.Capture().NativeReactionOrder);
        NativeBattleRandomState expectedRandom = beforeRetaliation!.Draw(out byte retaliationRoll);
        Assert.Equal(0, retaliationRoll % 3);
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(logical.Capture().Random));
        BattleAttackCommand retaliation = Assert.Single(logical.QueuedActions);
        Assert.Equal((byte)2, retaliation.NativeAiCallbackStage);
        Assert.Equal(targetId, retaliation.ActorId);
        Assert.Equal(source, retaliation.TargetId);
        var selection = Assert.IsType<BattleTargetSelection.Explicit>(retaliation.TargetRequest.Selection);
        Assert.Equal(new BattlePartyActorReference(source), Assert.Single(selection.Actors));
        Assert.Equal(expectedRetaliation.Command, retaliation.NativeQueuedCommand);
        Assert.Equal(expectedRetaliation.Id, retaliation.ActionId);
        Assert.Equal(expectedRetaliation.Sequence, retaliation.NativeSequence);
        Assert.Null(process.ActiveActionToken);
        Assert.Single(logical.Events, item => item.Kind == "ActionComplete" && item.ActorId == source);
    }

    [Theory]
    [InlineData(4, 5, "enemy-8", 0, true, false)]
    [InlineData(4, 5, "enemy-8", 0, false, false)]
    [InlineData(11, 14, "enemy-17", 6, true, false)]
    [InlineData(11, 14, "enemy-17", 6, false, false)]
    [InlineData(4, 5, "enemy-8", 0, false, true)]
    public void Main_gunblade_uses_the_native_widget_then_fire_on_the_same_owner(int allyCount, int enemyCount, string targetId, int model, bool press, bool automaticOption)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, allyCount, enemyCount, 4, expanded: true);
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Fire");
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        string source = $"party-{model}";
        const string fireActor = "party-1";
        NativeLimitProgress options = selected.LimitProgress! with { Options978 = (ushort)(selected.LimitProgress!.Options978!.Value & ~1 | (automaticOption ? 1 : 0)) };
        BattleCombatantDefinition[] actors = logicalDefinition.Combatants.OrderBy(actor => actor.IsPlayer ? actor.Id == source ? 1 : 0 : 2).Select(actor => actor.IsPlayer ? actor with
        {
            NativeAttack = assets.ReadPlayerNativeAttackData(vitals, Array.FindIndex(vitals.Records, row => row.CharacterId == actor.ModelId)),
            Abilities = [BattleAbility.Attack, BattleAbility.Magic],
            MagicStocks = [new(fire.Id, 5)],
            Attack = 35,
            Magic = 30,
            NativeStatusResistances = new byte[40]
        } : actor.NativeTemplate is { } template
            ? actor with { MaximumHp = template.Levels[actor.Level].MaximumHp, CurrentHp = template.Levels[actor.Level].MaximumHp }
            : actor with { MaximumHp = 6000, CurrentHp = 6000, NativeStatusResistances = new byte[40] }).ToArray();
        NativeBattleUiRandomState ui = new(789);
        logicalDefinition = logicalDefinition with
        {
            Combatants = actors, NativeActions = assets.NativeActions.ToArray(), ActionDefinitions = [fire], NativeCommandSequences = selected.NativeCommandSequences,
            NativeFieldBattleRules = 0, NativeRetainedMemory = selected.NativeRetainedMemory, LimitProgress = options, NativeBattleUiRandom = ui
        };
        selected = selected with
        {
            LimitProgress = options, NativeBattleUiRandom = ui,
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([fire.NativeAction!.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        };
        NativeBattleUiRandomState expectedUi = automaticOption ? ui.Draw(out ushort roll) : ui;
        bool expectedTrigger = press || automaticOption && expectedUi.State > 0x2000;
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        BattleSession control = new(logicalDefinition, BattleAtbMode.TurnBased);
        control.SetGunbladeMode(expectedTrigger ? BattleGunbladeMode.Automatic : BattleGunbladeMode.Manual);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        logical.AttachExpandedPresentationProcess(process);
        for (int callback = 0; callback < EntryBudget && !logical.NativeEntryReady; callback++)
        {
            logical.Tick();
        }
        Assert.True(logical.NativeEntryReady);
        BattleModelOwner sourceOwner = logical.ReadPresentationModelOwner(source);
        Assert.False(logical.ReadBattlePresentationPacketBindings().Aliases.TryEncodeExact(sourceOwner, out _));
        BattleModelOwner targetOwner = logical.ReadPresentationModelOwner(targetId);
        BattleCombatant target = Assert.Single(logical.Combatants, actor => actor.Id == targetId);
        int hp = target.CurrentHp;
        NativeBattleRandomState? random = logical.Capture().Random;
        var originalModels = Models(process);
        NativeBattleModelCpuResources cpu = process.ModelCpuResources;
        logical.QueueAttack(source, targetId);
        control.QueueAttack(source, targetId);
        Assert.False(logical.CanTriggerGunblade);
        List<NativeBattleContactPacket> contacts = [];
        long? token = null;
        bool sawInput = false;
        bool pressed = false;
        for (int callback = 0; callback < ActionBudget && (logical.ActiveAction is not null || logical.QueuedActions.Count != 0); callback++)
        {
            logical.Tick();
            token ??= process.LastStepActionToken;
            if (process.TriggerWidget is { } widget && process.PendingTrigger is null && contacts.Count == 0 && target.CurrentHp == hp)
            {
                sawInput = true;
                Assert.Equal(sourceOwner, widget.Actor);
                Assert.Equal(hp, target.CurrentHp);
                Assert.Equal(random, logical.Capture().Random);
            }
            if (automaticOption)
            {
                Assert.False(logical.CanTriggerGunblade);
            }
            if (press && !pressed && logical.GunbladeTriggerWindowOpen)
            {
                Assert.True(logical.TriggerGunblade());
                Assert.True(process.TriggerWidget!.PendingTrigger);
                Assert.False(logical.CanTriggerGunblade);
                pressed = true;
            }
            if (!process.LastStep.Contacts.IsEmpty)
            {
                Assert.Equal(token, process.LastStepActionToken);
                contacts.AddRange(process.LastStep.Contacts);
            }
        }
        Assert.Null(logical.ActiveAction);
        Assert.True(sawInput);
        Assert.Equal(press, pressed);
        long gunbladeToken = Assert.IsType<long>(token);
        Assert.True(process.IsActionComplete(gunbladeToken));
        Assert.Null(process.PendingTrigger);
        Assert.Equal(targetOwner, Assert.Single(contacts).Primary.Recipient);
        Assert.True(hp > target.CurrentHp);
        Assert.Single(logical.Events, item => item.Kind == "EffectContact" && item.ActorId == source && item.TargetId == targetId);
        Assert.Equal(expectedTrigger ? 1 : 0, logical.Events.Count(item => item.Kind == "GunbladeTrigger" && item.ActorId == source));
        RunControl();
        Assert.Equal(control.Combatants.Single(item => item.Id == targetId).CurrentHp, target.CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(control.Capture().Random), JsonSerializer.Serialize(logical.Capture().Random));
        Assert.Equal(expectedUi, logical.Capture().NativeBattleUiRandom);

        logical.QueueAction(fireActor, targetId, BattleAbility.Magic, fire.Id);
        control.QueueAction(fireActor, targetId, BattleAbility.Magic, fire.Id);
        contacts.Clear();
        long? fireToken = null;
        for (int callback = 0; callback < ActionBudget && (logical.ActiveAction is not null || logical.QueuedActions.Count != 0); callback++)
        {
            logical.Tick();
            if (process.LastStepActionToken is long current && current != gunbladeToken)
            {
                fireToken ??= current;
                Assert.Equal(fireToken, current);
                contacts.AddRange(process.LastStep.Contacts);
            }
        }
        Assert.Null(logical.ActiveAction);
        Assert.True(process.IsActionComplete(Assert.IsType<long>(fireToken)));
        Assert.True(process.IsActionComplete(gunbladeToken));
        Assert.Equal(targetOwner, Assert.Single(contacts).Primary.Recipient);
        Assert.Equal(4, logical.GetAvailableMagicCasts(fireActor, fire.Id));
        RunControl();
        Assert.Equal(control.Combatants.Single(item => item.Id == targetId).CurrentHp, target.CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(control.Capture().Random), JsonSerializer.Serialize(logical.Capture().Random));
        Assert.Equal(originalModels, Models(process));
        Assert.Same(cpu, process.ModelCpuResources);
        Assert.Same(process, logical.ExpandedPresentationProcess);

        void RunControl()
        {
            for (int callback = 0; callback < 150 && (control.ActiveAction is not null || control.QueuedActions.Count != 0); callback++)
            {
                control.Tick();
            }
            Assert.Null(control.ActiveAction);
        }
    }

    [Theory]
    [InlineData(4, 5, "party-1", "enemy-8", "party-0")]
    [InlineData(11, 14, "party-1", "enemy-17", "party-10")]
    public void Installed_attack_then_fire_complete_their_own_descriptors_on_one_process(int allyCount, int enemyCount, string attackId, string targetId, string fireId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, allyCount, enemyCount, 4, expanded: true);
        BattleNativeAction fire = Assert.Single(assets.NativeActions, candidate => candidate.Command == 2 && candidate.Name == "Fire");
        selected = selected with
        {
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([fire.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        };
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        string logicalBefore = ExpandedBattlePresentationProcessEntryTests.LogicalState(logical);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        BattleModelRoster roster = logical.ReadBattlePresentationRoster();
        BattleModelOwner attackOwner = logical.ReadPresentationModelOwner(attackId);
        BattleModelOwner attackTarget = logical.ReadPresentationModelOwner(targetId);
        BattleModelOwner fireOwner = logical.ReadPresentationModelOwner(fireId);

        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        BattleCombatant attackActor = logical.Combatants.Single(actor => actor.Id == attackId);
        int attackRecord = Enumerable.Range(0, vitals.Records.Length).First(row => vitals.Records[row].CharacterId == attackActor.ModelId);
        BattleNativeAction attack = assets.ReadPlayerNativeAttackData(vitals, attackRecord);
        NativeBattleActionHeaderPreparation attackHeader = Prepare(attackOwner, attack, selected.NativeCommandSequences);
        NativeBattleContactPacket attackPacket = Contact(attackTarget, attack, 137);
        Assert.NotEqual((byte)0, attackHeader.Sequence);
        Assert.Throws<InvalidOperationException>(() => process.BeginAction(attackHeader, [attackPacket]));
        for (int callback = 0; callback < EntryBudget && !process.EntryReady; callback++)
        {
            process.Advance(logical);
        }
        Assert.True(process.EntryReady, $"Installed entry stalled at callback {process.Graphics.Callback}.");
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);
        ExpandedBattlePresentationProcessEntryTests.AssertRandomExchange(logical, process);

        BattleSession.PreparedHostedDispatch stale = process.PrepareAction(attackHeader, [attackPacket]);
        process.ValidatePreparedAction(stale);
        long previousCallback = process.Graphics.Callback;
        process.Advance(logical);
        Assert.Equal(previousCallback + 1, process.Graphics.Callback);
        NativeBattleGraphicsState afterIdle = process.Graphics;
        NativeBattleEffectMemory idleMemory = process.Memory;
        Assert.Throws<InvalidOperationException>(() => process.PublishAction(stale));
        Assert.Same(afterIdle, process.Graphics);
        Assert.Same(idleMemory, process.Memory);
        Assert.Null(process.ActiveActionToken);

        var originalModelOwners = Models(process);
        NativeBattleModelCpuResources originalCpu = process.ModelCpuResources;
        NativeBattleModelTextureCache originalCache = process.ModelTextureCache;
        NativeBattleStageTask originalStage = Assert.IsType<NativeBattleStageTask>(process.Stage.Task);
        ulong initialRandomCalls = process.GlobalRandomCalls;
        NativeBattleVector attackOrigin = process.Graphics.ActorModels.Get(attackOwner).Transform!.Position28;
        NativeBattleVector initialDisplacement = HeaderDisplacement(process.Graphics.ActorModels.Get(attackOwner));
        MotionObservation initialMotion = ObserveMotion(process, attackOwner);
        NativeBattleContactPacket invalidLate = attackPacket with { Primary = attackPacket.Primary with { Recipient = new BattleEnemyActorModel(new("unbound-enemy")) } };
        NativeBattleGraphicsState beforeInvalid = process.Graphics;
        Assert.Throws<InvalidDataException>(() => process.BeginAction(attackHeader, [attackPacket, invalidLate]));
        Assert.Same(beforeInvalid, process.Graphics);
        Assert.Null(process.ActiveActionToken);

        long attackToken = process.PublishAction(process.PrepareAction(attackHeader, [attackPacket]));
        Descriptor attackDescriptor = ReadDescriptor(process);
        Assert.Throws<InvalidOperationException>(() => process.BeginAction(attackHeader, [attackPacket]));
        ActionObservation attackResult = AdvanceAction(process, logical, attackToken, attackDescriptor, attackHeader, 0x50BB00, attackTarget);

        Assert.Equal(attackPacket, Assert.Single(attackResult.Contacts));
        Assert.True(attackResult.SawInlineContact, "Attack did not publish its AA callback.");
        Assert.True(attackResult.SourceSequenceProgressed, "The installed Attack sequence did not advance.");
        Assert.True(attackResult.SourceDisplaced && attackResult.MinimumDistance < attackResult.InitialDistance && attackResult.ApproachedAtContact,
            $"Attack did not approach its target by the first AA contact. Initial {attackResult.InitialDistance:F1}, minimum {attackResult.MinimumDistance:F1}, contact {attackResult.FirstContactDistance:F1}. Initial pose {initialMotion}, contact pose {attackResult.FirstContactMotion}.");
        MotionObservation completedMotion = ObserveMotion(process, attackOwner);
        // Descriptor 104 can finish on the action callback bit before the standing sequence clears displacement.
        for (int callback = 0; callback < StandingReturnBudget && !ReturnedToStanding(process.Graphics.ActorModels.Get(attackOwner), initialDisplacement); callback++)
        {
            process.Advance(logical);
            Assert.True(process.IsActionComplete(attackToken));
            Assert.Null(process.LastStepActionToken);
            Assert.Empty(process.LastStep.Contacts);
            ObserveDamage(process.Graphics, attackResult);
        }
        Assert.True(ReturnedToStanding(process.Graphics.ActorModels.Get(attackOwner), initialDisplacement),
            $"Attack never returned to standing. Initial {initialMotion}, first contact {attackResult.FirstContactMotion}, descriptor completion {completedMotion}, final {ObserveMotion(process, attackOwner)}.");
        Assert.Equal(initialDisplacement, HeaderDisplacement(process.Graphics.ActorModels.Get(attackOwner)));
        Assert.Equal(attackOrigin, process.Graphics.ActorModels.Get(attackOwner).Transform!.Position28);
        Assert.Contains(attackTarget, attackResult.ReactingOwners);
        Assert.Contains(137, attackResult.DisplayedDamage);
        Assert.Equal(originalModelOwners, Models(process));

        NativeBattleActionHeaderPreparation fireHeader = Prepare(fireOwner, fire, selected.NativeCommandSequences);
        Assert.NotEqual(attackHeader.Source, fireHeader.Source);
        ImmutableArray<NativeBattleContactPacket> firePackets = roster.Enemies.Select((owner, index) => Contact(owner, fire, checked((ushort)(201 + index)))).ToImmutableArray();
        Assert.Equal(enemyCount, firePackets.Length);
        long beforeFire = process.Graphics.Callback;
        NativeCrtRandomState crtBeforeFire = Assert.IsType<NativeCrtRandomState>(logical.CurrentCrtRandom);
        long fireToken = process.BeginAction(fireHeader, firePackets);
        Descriptor fireDescriptor = ReadDescriptor(process);
        Assert.NotEqual(attackDescriptor.Serial, fireDescriptor.Serial);
        Assert.True(fireToken > attackToken);
        Assert.True(process.IsActionComplete(attackToken));
        ActionObservation fireResult = AdvanceAction(process, logical, fireToken, fireDescriptor, fireHeader, 0x50A9A0);

        Assert.Equal(firePackets.AsEnumerable(), fireResult.Contacts);
        Assert.Equal(enemyCount, fireResult.ReceivedDuringFire);
        Assert.True(fireResult.SourceSequenceProgressed, "The installed Fire casting sequence did not advance.");
        Assert.Equal(firePackets.Select(packet => packet.Primary.PresentedAmount).Order(), fireResult.DisplayedDamage.Order());
        Assert.True(process.IsActionComplete(attackToken));
        Assert.True(process.IsActionComplete(fireToken));
        Assert.Equal(beforeFire + fireResult.Frames, process.Graphics.Callback);
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);
        Assert.Equal(selected.StageId, process.Stage.ResourceStageId);
        Assert.Equal(originalStage.Callback, process.Stage.Task!.Callback);
        Assert.NotNull(process.Camera.Projection);
        Assert.True(process.GlobalRandomCalls >= initialRandomCalls);
        ExpandedBattlePresentationProcessEntryTests.AssertRandomExchange(logical, process);
        Assert.NotEqual(crtBeforeFire, logical.CurrentCrtRandom);
        Assert.Same(originalCpu, process.ModelCpuResources);
        Assert.Same(originalCache, process.ModelTextureCache);
        Assert.Equal(originalModelOwners, Models(process));
        Assert.Equal(logicalBefore, ExpandedBattlePresentationProcessEntryTests.LogicalState(logical));

        process.Advance(logical);
        Assert.Null(process.LastStepActionToken);
        Assert.Empty(process.LastStep.Contacts);
    }

    private static NativeBattleActionHeaderPreparation Prepare(BattleModelOwner actor, BattleNativeAction action, ImmutableArray<byte> commandSequences)
    {
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(action.Command, 0, action.ActionId, commandSequences);
        NativeBattleActionDataReference? data = action.Command == 2 ? new(0x47E970, action.ActionId, action.Name) : null;
        return new(actor, action.Command, sequence, -1, action.ActionId, action.Command == 1 ? (ushort)0 : action.MagicId, data);
    }

    private static NativeBattleContactPacket Contact(BattleModelOwner target, BattleNativeAction action, ushort amount)
    {
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(action.DamagePresentation, 0, 0, (byte)(action.AttackFlags & 3), covered: false);
        return new(new(target, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, 0, amount, 0), null);
    }

    private static bool RotateCompletedActionBehindCompletion(ExpandedBattlePresentationProcess process)
    {
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        if (descriptors.Queue is not [int action, int completion] ||
            descriptors.Packets[action] is not { Command: 104, PriorityState: 255 } ||
            descriptors.Packets[completion] is not { Command: 10, PriorityState: 128 })
        {
            return false;
        }
        Assert.Equal((byte)128, descriptors.Priorities[descriptors.Depth]);
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        BattleSession child = (BattleSession)typeof(ExpandedBattlePresentationProcess).GetField("presentation", privateInstance)!.GetValue(process)!;
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(child, graphics with { Descriptors = descriptors.Rotate() });
        return true;
    }

    private static Descriptor ReadDescriptor(ExpandedBattlePresentationProcess process)
    {
        NativeBattleDescriptorState descriptors = process.Graphics.Descriptors;
        int slot = Assert.Single(descriptors.Queue, index => descriptors.Packets[index].Command == 104);
        NativeBattleDescriptorPacket packet = descriptors.Packets[slot];
        Assert.Equal((byte)128, packet.PriorityState);
        Assert.Equal(0x1D280C4u, packet.Argument);
        Assert.DoesNotContain(descriptors.Queue, index => descriptors.Packets[index].Command == 10);
        return new(slot, packet.Serial, packet.Argument);
    }

    private static ActionObservation AdvanceAction(ExpandedBattlePresentationProcess process, BattleSession logical, long token, Descriptor descriptor, NativeBattleActionHeaderPreparation header, uint service, BattleModelOwner? movementTarget = null)
    {
        ActionObservation observed = new();
        NativeBattleVector? destination = movementTarget is not null ? process.Graphics.ActorModels.Get(movementTarget).Transform!.Position28 : null;
        NativeBattleVector initialDisplacement = HeaderDisplacement(process.Graphics.ActorModels.Get(header.Source));
        if (destination is { } targetPosition)
        {
            observed.InitialDistance = WorldDistance(process.Graphics.ActorModels.Get(header.Source), targetPosition);
            observed.MinimumDistance = observed.InitialDistance;
        }
        Assert.Equal(token, process.ActiveActionToken);
        Assert.False(process.IsActionComplete(token));
        for (int callback = 0; callback < ActionBudget && !process.IsActionComplete(token); callback++)
        {
            process.Advance(logical);
            observed.Frames++;
            Assert.Equal(token, process.LastStepActionToken);
            NativeBattleGraphicsState graphics = process.Graphics;
            if (graphics.ActionGraphics is { } action)
            {
                Assert.Equal(service, action.Callback);
                Assert.Equal(header.Source, action.Actor);
                Assert.Equal(header.Data, action.PendingData);
            }
            NativeBattleGraphicsModel source = graphics.ActorModels.Get(header.Source);
            observed.SourceSequenceProgressed |= source.Sequence == header.Sequence && (source.SequenceFrame != 0 || source.SequenceState?.Offset46 > 0);
            if (destination is { } movementDestination)
            {
                double distance = WorldDistance(source, movementDestination);
                NativeBattleVector displacement = HeaderDisplacement(source);
                observed.SourceDisplaced |= displacement != default && displacement != initialDisplacement;
                observed.MinimumDistance = Math.Min(observed.MinimumDistance, distance);
                if (observed.FirstContactDistance is null && !process.LastStep.Contacts.IsEmpty)
                {
                    observed.FirstContactDistance = distance;
                    observed.FirstContactMotion = ObserveMotion(process, header.Source);
                    observed.ApproachedAtContact = observed.SourceDisplaced && observed.MinimumDistance < observed.InitialDistance;
                }
            }
            observed.Contacts.AddRange(process.LastStep.Contacts);
            if (!process.LastStep.Contacts.IsEmpty)
            {
                Assert.False(process.IsActionComplete(token));
                observed.SawInlineContact |= graphics.ActionCallbackReady;
                if (graphics.OriginalEffect?.Binding.Effect == header.Effect && header.Effect != 0)
                {
                    observed.ReceivedDuringFire += process.LastStep.Contacts.Length;
                }
                foreach (NativeBattleContactPacket contact in process.LastStep.Contacts)
                {
                    NativeBattleGraphicsModel target = graphics.ActorModels.Get(contact.Primary.Recipient);
                    if (target.Sequence == contact.Primary.Sequence || target.SequenceState?.Pending3 == contact.Primary.Sequence)
                    {
                        observed.ReactingOwners.Add(contact.Primary.Recipient);
                    }
                }
            }
            ObserveDamage(graphics, observed);
            if (!process.IsActionComplete(token))
            {
                Assert.Equal(token, process.ActiveActionToken);
                Assert.Contains(descriptor.Slot, graphics.Descriptors.Queue);
                NativeBattleDescriptorPacket pending = graphics.Descriptors.Packets[descriptor.Slot];
                Assert.Equal(descriptor.Serial, pending.Serial);
                Assert.Equal((ushort)104, pending.Command);
                Assert.Equal(descriptor.Argument, pending.Argument);
                observed.SawFinishedDescriptor |= pending.PriorityState == 255;
            }
        }

        Assert.True(process.IsActionComplete(token), $"Action 0x{service:X} stalled at callback {process.Graphics.Callback} after {observed.Contacts.Count} contacts.");
        Assert.Null(process.ActiveActionToken);
        NativeBattleDescriptorPacket completed = process.Graphics.Descriptors.Packets[descriptor.Slot];
        Assert.Equal(descriptor.Serial, completed.Serial);
        Assert.Equal((ushort)104, completed.Command);
        Assert.Equal(descriptor.Argument, completed.Argument);
        Assert.True(observed.SawFinishedDescriptor, "The exact descriptor never reached native completion state 255.");
        Assert.Equal((byte)0, completed.PriorityState);
        Assert.DoesNotContain(descriptor.Slot, process.Graphics.Descriptors.Queue);
        Assert.Equal(0, process.Camera.Mask & 0xFF);
        Assert.False(process.Camera.ForceCamera);
        return observed;
    }

    private static (BattleModelOwner Owner, byte? Model, int? Body, int? Linked)[] Models(ExpandedBattlePresentationProcess process)
    {
        return process.Graphics.ActorModels.Entries.Select(entry =>
        {
            NativeBattleGraphicsModel model = entry.State;
            Assert.NotEqual(0, model.Flags0 & 2);
            int body = Assert.IsType<int>(model.ActiveResourceRow100);
            Assert.Equal(body, model.OriginalResourceRow104);
            Assert.Equal(entry.Owner, process.ModelTextureCache.PresentationActors![body]);
            return (entry.Owner, model.ModelId4, model.ActiveResourceRow100, model.LinkedTextureResourceIndex);
        }).ToArray();
    }

    private static NativeBattleVector HeaderDisplacement(NativeBattleGraphicsModel model)
    {
        NativeBattleSequenceState state = Assert.IsType<NativeBattleSequenceState>(model.SequenceState);
        return new(state.Header36, state.Header38, state.Header40);
    }

    private static bool ReturnedToStanding(NativeBattleGraphicsModel model, NativeBattleVector initialDisplacement)
    {
        return HeaderDisplacement(model) == initialDisplacement && (model.ResourceFlags44 & 0x40) != 0;
    }

    private static MotionObservation ObserveMotion(ExpandedBattlePresentationProcess process, BattleModelOwner owner)
    {
        NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(owner);
        NativeBattleActionMatrix matrix = Assert.IsType<NativeBattleActionMatrix>(model.GeometryWorldMatrix);
        return new(process.Graphics.Callback, model.ModelId4, model.Sequence, model.ResourceFlags44, HeaderDisplacement(model), (matrix.X, matrix.Y, matrix.Z));
    }

    private static void ObserveDamage(NativeBattleGraphicsState graphics, ActionObservation observed)
    {
        foreach (NativeBattleDamageTextDraw draw in graphics.Effects.TextDraws)
        {
            int amount = 0;
            foreach (byte glyph in draw.Glyphs.TakeWhile(glyph => glyph != 0))
            {
                Assert.InRange(glyph, (byte)56, (byte)65);
                amount = amount * 10 + glyph - 56;
            }
            observed.DisplayedDamage.Add(amount);
        }
    }

    private static double WorldDistance(NativeBattleGraphicsModel model, NativeBattleVector target)
    {
        NativeBattleActionMatrix matrix = Assert.IsType<NativeBattleActionMatrix>(model.GeometryWorldMatrix);
        double x = matrix.X - target.X;
        double z = matrix.Z - target.Z;
        return Math.Sqrt(x * x + z * z);
    }

    private sealed record Descriptor(int Slot, byte Serial, uint Argument);
    private sealed record MotionObservation(long Callback, byte? ModelId, byte? Sequence, ushort? ResourceFlags, NativeBattleVector Displacement, (int X, int Y, int Z) World);

    private sealed class ActionObservation
    {
        internal int Frames { get; set; }
        internal bool SourceSequenceProgressed { get; set; }
        internal bool SourceDisplaced { get; set; }
        internal bool ApproachedAtContact { get; set; }
        internal double InitialDistance { get; set; }
        internal double MinimumDistance { get; set; }
        internal double? FirstContactDistance { get; set; }
        internal MotionObservation? FirstContactMotion { get; set; }
        internal bool SawInlineContact { get; set; }
        internal bool SawFinishedDescriptor { get; set; }
        internal int ReceivedDuringFire { get; set; }
        internal List<NativeBattleContactPacket> Contacts { get; } = [];
        internal HashSet<BattleModelOwner> ReactingOwners { get; } = [];
        internal HashSet<int> DisplayedDamage { get; } = [];
    }
}
