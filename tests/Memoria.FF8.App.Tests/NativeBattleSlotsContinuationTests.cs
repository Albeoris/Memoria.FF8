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
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, field, 22, requireMaximumParty: addedCaster,
            limit: fire with { CharacterId = 5 }, nativeSeed: 7,
            prepareVitals: addedCaster ? null : vitals => vitals.WithBattleParty([5, 4, 6], 3));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == 5);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.Equal(addedCaster, caster.NativeActorIndex is null);

        BattleNativeSlotsRoll? selected = null;
        for (int turn = 0; turn < 24 && selected is null; turn++)
        {
            if (battle.NativeEntryReady && battle.CanChoosePlayerCommand(caster.Id) && battle.ActiveAction is null &&
                battle.RefreshNativeLimit(caster.Id) != 0)
            {
                for (int reroll = 0; reroll < 256; reroll++)
                {
                    if (battle.RollNativeSlots(caster.Id) is { MagicId: 1, Repetitions: 3 } roll)
                    {
                        selected = roll;
                        break;
                    }
                }
                break;
            }
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.NotNull(selected);
        Assert.True(Assert.Single(battle.GetNativePlayerActionChoices(caster.Id, limits: true)).Available);

        NativeBattleRandomState expectedRandom = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
        string[] stock = caster.MagicStocks.Select(item => $"{item.Id}/{item.Count}").ToArray();
        BattleCombatant[] allocated = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.Slot is not null).ToArray();
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
        for (int callback = 0; callback < 1600 && caster.IsBusy; callback++)
        {
            battle.Tick();
            queuedFork.Tick();
            publishedFork?.Tick();
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
        Assert.Single(battle.Events.Skip(start), entry => entry.Kind == "ActionComplete" && entry.ActorId == caster.Id);
        Assert.Equal(3, battle.Events.Skip(start).Count(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.LimitBreak));
        Assert.Equal(battle.Events.ToArray(), queuedFork.Events.ToArray());
        Assert.Equal(battle.Events.ToArray(), publishedFork.Events.ToArray());
        Assert.Equal(battle.Capture().Random!.Indexes.ToArray(), publishedFork.Capture().Random!.Indexes.ToArray());
        foreach (BattleCombatant target in allocated.Where(actor => actor.LastAttackerId == caster.Id))
        {
            Assert.Null(target.LastNativeAction?.AttackerIndex);
            if (target.NativeActorIndex is int original)
            {
                Assert.Equal(caster.NativeActorIndex, battle.NativeActionMemory.Action(original)?.AttackerIndex);
                Assert.Equal(target.NativePresentationStatus, battle.NativePresentationStatusMemory.Rows[original]);
            }
        }
    }
}
