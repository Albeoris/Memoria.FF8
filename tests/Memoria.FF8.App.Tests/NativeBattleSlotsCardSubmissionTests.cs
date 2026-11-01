using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSlotsCardSubmissionTests
{
    [Fact]
    public void Installed_card_submission_preserves_live_owners_and_round_trips_its_accepted_command()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_SLOTS_SNAPSHOT");
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(input) && Directory.Exists(directory), "Select a standalone Slots save and installed resources.");
        BattleSnapshot? initial = GameSession.Load(input!).Battle;
        Assert.SkipUnless(initial is not null, "The selected standalone must contain an active battle.");
        using GameAssets game = new(directory);
        AssertSubmission(initial!, game);
    }

    [Fact]
    public void Installed_resource_card_submission_preserves_live_owners_without_a_standalone()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition definition = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 5, 1) with { SelphieCardSlots = true };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.True(battle.CanChoosePlayerCommand("party"));
        Assert.NotNull(battle.NativeGraphics);
        Assert.NotNull(battle.NativeEffectMemory);
        AssertSubmission(battle.Capture(), game);
    }

    private static void AssertSubmission(BattleSnapshot initial, GameAssets game)
    {
        BattleCombatantDefinition owner = initial.Definition.Combatants.Single(combatant => combatant.IsPlayer && combatant.ModelId == 5);
        // Controlled admission component: retain the installed battle owners and supply a ready Limit selection.
        initial = initial with
        {
            Definition = initial.Definition with { SelphieCardSlots = true, NativeLimitsDisabled = false },
            NativeLimits = initial.NativeLimits.Where(limit => limit.ActorId != owner.Id).Append(new(owner.Id, 4)).ToArray(),
            Combatants = initial.Combatants.Select(combatant => combatant.Id == owner.Id ? combatant with { Atb = BattleSession.AtbMaximum, NativeAtb = combatant.NativeAtb is { } atb ? atb with { Current = atb.Maximum } : null } : combatant).ToArray()
        };
        if (initial.NativeGraphics?.UiServices is { } ui)
        {
            byte index = checked((byte)initial.NativeGraphics.ActorIds.IndexOf(owner.Id));
            (NativeBattleUiCommands commands, uint mask, _) = NativeBattleUiCommands.Clear().Enqueue(index, 0x11).Dispatch(0);
            initial = initial with { NativeGraphics = initial.NativeGraphics with { UiServices = ui with { Commands = commands, ReadyMask = mask } } };
        }
        BattleSession battle = Restore(initial);
        BattleCombatant actor = battle.Combatants.Single(combatant => combatant.IsPlayer && combatant.ModelId == 5);
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal(actor.Id);
        BattleNativeSlotsRoll selected = deal.Candidates[0];
        Assert.True(battle.SelectNativeSlotsCard(deal.Id, 0));
        NativeBattleGraphicsState? graphics = battle.NativeGraphics;
        NativeBattleEffectMemory? memory = battle.NativeEffectMemory;
        NativeGlobalRandomChange? randomReceipt = battle.NativeGlobalRandomChange;
        long resets = battle.DiagnosticResetCount;
        long restores = battle.DiagnosticRestoreCount;
        int queuedBefore = battle.QueuedActions.Count;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();

        bool submitted = battle.SubmitNativeSlotsCard(deal.Id);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Console.WriteLine($"Slots submit: {elapsed.TotalMilliseconds:F3} ms, {allocated} allocated bytes.");
        Assert.True(submitted);
        Assert.Equal(resets, battle.DiagnosticResetCount);
        Assert.Equal(restores, battle.DiagnosticRestoreCount);
        Assert.Same(memory, battle.NativeEffectMemory);
        if (graphics is not null)
        {
            NativeBattleGraphicsState current = Assert.IsType<NativeBattleGraphicsState>(battle.NativeGraphics);
            Assert.Same(graphics.Effects, current.Effects);
            Assert.Same(graphics.Auxiliary, current.Auxiliary);
            Assert.Same(graphics.ModuleEffects, current.ModuleEffects);
            Assert.Same(graphics.Descriptors, current.Descriptors);
            Assert.Equal(graphics.Models, current.Models);
            Assert.Equal(graphics, current with { UiServices = graphics.UiServices });
        }
        Assert.Equal(randomReceipt, battle.NativeGlobalRandomChange);
        Assert.Null(battle.PendingSlotsDeal);
        Assert.Equal(queuedBefore + 1, battle.QueuedActions.Count);
        BattleAttackCommand accepted = Assert.Single(battle.QueuedActions, command => command.ActorId == actor.Id && command.NativeQueuedCommand == 16);
        Assert.Equal(selected, accepted.NativeSlots);
        Assert.Equal(selected.MagicId, accepted.ActionId);
        Assert.Null(battle.GetNativeSlotsRoll(actor.Id));
        Assert.False(battle.SelectNativeSlotsCard(deal.Id, 0));

        BattleSnapshot queued = battle.Capture();
        string serialized = JsonSerializer.Serialize(queued);
        Assert.False(battle.SubmitNativeSlotsCard(deal.Id));
        Assert.Equal(serialized, JsonSerializer.Serialize(battle.Capture()));
        BattleSession cold = Restore(JsonSerializer.Deserialize<BattleSnapshot>(serialized)!);
        Assert.Equal(serialized, JsonSerializer.Serialize(cold.Capture()));
        Assert.Equal(selected, Assert.Single(cold.QueuedActions, command => command.ActorId == actor.Id && command.NativeQueuedCommand == 16).NativeSlots);
        Assert.Equal(battle.NativeGlobalRandomChange, cold.NativeGlobalRandomChange);

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }
    }
}
