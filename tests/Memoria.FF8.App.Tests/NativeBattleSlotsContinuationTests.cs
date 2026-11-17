using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSlotsContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Fire_Slots_preserves_each_preflight_and_one_repeated_action(bool addedCaster)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Select the installed original game resources.");
        using BattleAssets assets = new(directory);
        BattleNativeAction fire = Assert.Single(assets.NativeActions, row => row is { KernelSection: 1, Command: 16, ActionId: 1 });
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        using GameAssets? game = addedCaster ? null : new(directory);
        NativeBattleExperienceDefinition? experience = game?.ReadNativeBattleExperienceDefinition();
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, field, addedCaster ? (ushort)22 : (ushort)513, requireMaximumParty: addedCaster,
            limit: fire with { CharacterId = 5 }, nativeSeed: 7,
            prepareVitals: experience is { } levelRules ? vitals => GameWorld.PrepareDiagnosticCharacterLevel(vitals.WithBattleParty([5, 0, 1], 11), 5, 8, levelRules) : null,
            additionalEnemies: addedCaster ? 10 : 0);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == 5);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.Equal(addedCaster, caster.NativeActorIndex is null);

        if (addedCaster)
        {
            Assert.True(TrySelectRoll(world, battle, caster, roll => roll is { MagicId: 1, Repetitions: 3 }), "The installed seed must yield Fire x3.");
        }
        else
        {
            // Keep the authored early action from defeating the one-HP caster before the native Limit can be selected.
            battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: caster.Id);
            for (int callback = 0; callback < 2000 && !battle.CanChoosePlayerCommand(caster.Id); callback++)
            {
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand(caster.Id));
            Assert.Equal(8, caster.Level);
            byte crisis = 0;
            for (int selection = 0; selection < 256 && crisis == 0; selection++)
            {
                crisis = battle.RefreshNativeLimit(caster.Id);
            }
            Assert.InRange(crisis, (byte)1, (byte)4);
            Assert.True(SelectRollAtAuthenticPhase(battle, caster, roll => roll is { MagicId: 1, Repetitions: 3 }), $"The original Level {caster.Level}, Crisis {crisis} Slots data must yield Fire x3.");
        }
        Assert.True(Assert.Single(battle.GetNativePlayerActionChoices(caster.Id, limits: true)).Available);

        NativeBattleRandomState expectedRandom = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
        string[] stock = caster.MagicStocks.Select(item => $"{item.Id}/{item.Count}").ToArray();
        BattleCombatant[] allocated = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.Slot is not null).ToArray();
        foreach (BattleCombatant enemy in allocated)
        {
            enemy.NativeStatus1 |= 0x40;
        }
        BattleCombatant[] additional = allocated.Where(actor => actor.NativeActorIndex is null).OrderBy(actor => actor.Slot!.Value.Position).ToArray();
        BattleCombatant?[] sampledSlots = new BattleCombatant?[4 + additional.Length];
        foreach (BattleCombatant enemy in allocated.Where(enemy => (enemy.NativeStatus0 & 1) == 0))
        {
            int position = enemy.NativeActorIndex is int original ? original - 3 : 4 + Array.IndexOf(additional, enemy);
            sampledSlots[position] = enemy;
        }
        string[] selectedTargets = new string[3];
        for (int cast = 0; cast < selectedTargets.Length; cast++)
        {
            byte sample;
            do
            {
                expectedRandom = expectedRandom.Draw(out sample);
            }
            while (sampledSlots[sample % sampledSlots.Length] is null);
            selectedTargets[cast] = sampledSlots[sample % sampledSlots.Length]!.Id;
        }

        int start = battle.Events.Count;
        battle.QueueNativePlayerCommand(caster.Id, allocated[0].Id, 16, fire.ActionId);
        BattleAttackCommand command = Assert.Single(battle.QueuedActions, item => item.ActorId == caster.Id);
        Assert.Equal(selectedTargets, command.MagicCasts.Select(cast => cast.FallbackAnchor.ActorId));
        Assert.Equal(selectedTargets[0], Assert.Single(Assert.IsType<BattleTargetSelection.Explicit>(command.NativeSlots!.PreflightSelection).Actors).ActorId);
        Assert.Equal(expectedRandom.Indexes.ToArray(), Assert.IsType<NativeBattleRandomState>(battle.Capture().Random).Indexes.ToArray());

        BattleSession queuedFork = battle.ForkForContinuation();
        BattleSession? publishedFork = null;
        BattleModelOwner casterOwner = battle.ReadPresentationModelOwner(caster.Id);
        int headerCount = 0;
        HashSet<long> primaryInvocations = [];
        for (int callback = 0; callback < 1600 && caster.IsBusy; callback++)
        {
            battle.Tick();
            queuedFork.Tick();
            publishedFork?.Tick();
            if (process.ActiveActionToken is long token && process.Graphics?.ActionGraphics is { } graphics &&
                process.ActionPackets.ReadHeader(graphics.Header) is { Command: 16 } active && active.Source == casterOwner)
            {
                primaryInvocations.Add(token);
            }
            if (publishedFork is null)
            {
                NativeBattleActionHeaderSlot[] headers = process.ActionPackets.PublishedHeaders().Select(entry => entry.Slot)
                    .Where(slot => slot.Source == casterOwner && slot.Command == 16).ToArray();
                if (headers.Length > 0)
                {
                    headerCount = headers.Length;
                    Assert.Equal(3, headerCount);
                    Assert.Equal((byte)2, headers[0].ReadRepeatModeMinusOne());
                    Assert.All(headers, header => Assert.Equal(fire.MagicId, header.Effect));
                    NativeBattleDescriptorState descriptors = process.Graphics!.Descriptors;
                    Assert.InRange(descriptors.Queue.Count(index => descriptors.Packets[index].Command == 104), 0, 1);
                    publishedFork = battle.ForkForContinuation();
                }
            }
        }

        Assert.False(caster.IsBusy);
        Assert.Equal(3, headerCount);
        Assert.NotNull(publishedFork);
        Assert.Equal(stock, caster.MagicStocks.Select(item => $"{item.Id}/{item.Count}"));
        Assert.True(process.IsActionComplete(Assert.Single(primaryInvocations)));
        Assert.Equal(3, battle.Events.Skip(start).Count(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.LimitBreak));
        Assert.Equal(battle.Events.ToArray(), queuedFork.Events.ToArray());
        Assert.Equal(battle.Events.ToArray(), publishedFork.Events.ToArray());
        Assert.Equal(battle.Capture().Random!.Indexes.ToArray(), publishedFork.Capture().Random!.Indexes.ToArray());
        foreach (BattleCombatant target in allocated.Where(actor => actor.LastAttackerId == caster.Id))
        {
            Assert.Equal(checked((byte?)caster.NativeActorIndex), target.LastNativeAction?.AttackerIndex);
            if (target.NativeActorIndex is int original)
            {
                Assert.Equal(caster.NativeActorIndex, battle.NativeActionMemory.Action(original)?.AttackerIndex);
                Assert.Equal(target.NativePresentationStatus, battle.NativePresentationStatusMemory.Rows[original]);
            }
        }
    }

    [Theory]
    [InlineData(51, 280)]
    [InlineData(52, 189)]
    public void Installed_support_slots_retains_the_group_without_preflight_rng(ushort actionId, ushort effect)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Select the installed original game resources.");
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row is { KernelSection: 1, Command: 16 } && row.ActionId == actionId);
        Assert.Equal(effect, action.MagicId);
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        foreach (byte seed in new byte[] { 7, 11, 12, 13 })
        {
            using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, field, 22, limit: action with { CharacterId = 5 }, nativeSeed: seed);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == 5);
            if (!TrySelectRoll(world, battle, caster, roll => roll.MagicId == actionId))
            {
                continue;
            }

            NativeBattleRandomState before = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
            BattleCombatant[] targets = battle.Combatants.Where(actor => actor.IsActive && actor.Slot is not null && actor.IsPlayer &&
                (actor.IsAlive || (action.TargetFlags!.Value & 1) != 0)).ToArray();
            Assert.Contains(targets, actor => actor.NativeActorIndex is null);
            int start = battle.Events.Count;
            battle.QueueNativePlayerCommand(caster.Id, caster.Id, 16, actionId);
            BattleAttackCommand command = Assert.Single(battle.QueuedActions, item => item.ActorId == caster.Id);
            Assert.Equal(new BattleTargetSelection.SideGroup(BattleTargetSide.Allies), command.TargetRequest.Selection);
            Assert.Null(command.NativeSlots!.PreflightSelection);
            Assert.Equal(command.NativeSlots.Repetitions, command.MagicCasts.Length);
            Assert.Equal(before.Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());

            for (int callback = 0; callback < 1600 && caster.IsBusy; callback++)
            {
                battle.Tick();
            }
            Assert.False(caster.IsBusy);
            Assert.Contains(battle.Events.Skip(start), entry => entry.Kind == "ActionComplete" && entry.ActorId == caster.Id);
            string[] contacts = battle.Events.Skip(start).Where(entry => entry.ActorId == caster.Id && entry.Kind == "EffectContact" &&
                entry.Ability == BattleAbility.LimitBreak).Select(entry => Assert.IsType<string>(entry.TargetId)).Order().ToArray();
            Assert.Equal(targets.Select(target => target.Id).Order(), contacts);
            Assert.Equal(1 << (actionId - 51), battle.NativeScriptState.LimitSpecialSetMask);
            return;
        }
        Assert.Fail($"The installed seed set did not yield Slots action {actionId}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_revival_group_uses_its_admitted_position_after_target_retirement(bool addedTarget)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Select the installed original game resources.");
        using BattleAssets assets = new(directory);
        BattleActionDefinition life = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.AttackType == 5);
        int outgoingRow = addedTarget ? 8 : 1;
        int nextRow = addedTarget ? 9 : 2;
        int secondRow = addedTarget ? 1 : 4;
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, field, 22, life, additionalEnemies: 0, prepareParty: records =>
        {
            foreach (int row in new[] { outgoingRow, nextRow, secondRow })
            {
                records[row] = records[row] with { HitPoints = new(0, 0), Status = 1 };
            }
        });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == 10);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: caster.Id);
        for (int callback = 0; callback < 1600 && !battle.CanChoosePlayerCommand(caster.Id); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand(caster.Id));
        BattleCombatant outgoing = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == outgoingRow);
        BattleCombatant next = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == nextRow);
        BattleCombatant second = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == secondRow);
        Assert.Equal(addedTarget, outgoing.NativeActorIndex is null);
        Assert.False(outgoing.IsAlive);
        Assert.False(next.IsAlive);
        Assert.False(second.IsAlive);
        caster.NativeStatus1 |= 0x20000;
        battle.QueueAction(caster.Id, outgoing.Id, BattleAbility.Magic, life.Id, magicTargets: [outgoing.Id, second.Id]);
        BattleAttackCommand command = Assert.Single(battle.QueuedActions, item => item.ActorId == caster.Id);
        Assert.Equal(outgoing.Slot, command.MagicCasts[0].PreflightSlot);
        Assert.Equal(outgoing.Id, command.MagicCasts[0].FallbackAnchor.ActorId);

        BattleSnapshot accepted = battle.Capture();
        BattleAttackCommand invalid = command with { MagicCasts = command.MagicCasts.SetItem(0, command.MagicCasts[0] with { PreflightSlot = null }) };
        InvalidDataException rejection = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(accepted with { QueuedActions = [invalid] }));
        Assert.Contains("bound target requests", rejection.Message);

        // Exercise the actual ejection lifecycle at the boundary between admission and action publication.
        MethodInfo eject = typeof(BattleSession).GetMethod("EjectPartyOccupant", BindingFlags.Instance | BindingFlags.NonPublic)!;
        eject.Invoke(battle, [outgoing]);
        Assert.Null(outgoing.Slot);
        Assert.Equal(command.MagicCasts[0].PreflightSlot, Assert.Single(battle.PartyOccupancyChanges).Slot);
        BattleSession fork = battle.ForkForContinuation();

        int firstEvent = battle.Events.Count;
        for (int callback = 0; callback < 1600 && caster.IsBusy; callback++)
        {
            battle.Tick();
            fork.Tick();
        }
        Assert.False(caster.IsBusy);
        Assert.False(outgoing.IsAlive);
        Assert.True(next.IsAlive);
        Assert.True(second.IsAlive);
        Assert.Equal(new[] { next.Id, second.Id }, battle.Events.Skip(firstEvent).Where(entry => entry.Kind == "EffectContact" &&
            entry.Ability == BattleAbility.Magic && entry.ActorId == caster.Id).Select(entry => entry.TargetId));
        Assert.Equal(battle.Events.ToArray(), fork.Events.ToArray());
    }

    private static bool TrySelectRoll(GameWorld world, BattleSession battle, BattleCombatant caster, Func<BattleNativeSlotsRoll, bool> select)
    {
        for (int turn = 0; turn < 24; turn++)
        {
            if (battle.NativeEntryReady && battle.CanChoosePlayerCommand(caster.Id) && battle.ActiveAction is null &&
                battle.RefreshNativeLimit(caster.Id) != 0)
            {
                return SelectRollAtAuthenticPhase(battle, caster, select);
            }
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        return false;
    }
    internal static bool SelectRollAtAuthenticPhase(BattleSession battle, BattleCombatant caster, Func<BattleNativeSlotsRoll, bool> select)
    {
        BattleNativeLimitDefinition rules = Assert.IsType<BattleNativeLimitDefinition>(caster.NativeLimit);
        NativeBattleRandomState original = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
        byte crisis = battle.GetNativeLimitLevel(caster.Id);
        for (int phase = 0; phase < 256; phase++)
        {
            NativeBattleRandomState start = original with { Indexes = original.Indexes.SetItem(original.Lane, unchecked((byte)(original.Indexes[original.Lane] + phase))) };
            _ = start.Draw(out byte a).Draw(out byte b).Draw(out byte c).Draw(out byte d);
            BattleNativeSlotsRoll expected = NativeBattleSlotsRules.Roll(rules, caster.Level, crisis, a, b, c, d);
            if (select(expected))
            {
                // Install the exercised original cursor before Do over. The live roll still reads and consumes its four operands.
                FieldInfo random = typeof(BattleSession).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic)!;
                random.SetValue(battle, start);
                Assert.Equal(expected, battle.RollNativeSlots(caster.Id));
                return true;
            }
        }
        return false;
    }

}
