using System.Diagnostics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNaturalRenz335337ComponentSourceTests
{
    [Fact]
    public void Natural_Norg_activation_supplies_the_original_335_logical_Renzokuken_owner()
    {
        Check(335, 77, 63);
    }

    [Fact]
    public void Natural_ten_Sorceress_deaths_supply_the_original_337_logical_Renzokuken_owner()
    {
        Check(337, 117, 813);
    }

    private static void Check(ushort effect, int model, int formation)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using Process cpu = Process.GetCurrentProcess();
        TimeSpan start = cpu.TotalProcessorTime;
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        Assert.Equal(formation, source.ReadDefinition().EncounterId);
        Assert.True(source.ReadDefinition().UsesOriginalEnemySlots);
        BattleCombatant target = Assert.Single(source.Combatants, actor => !actor.IsPlayer && actor.ModelId == model && actor.IsActive && actor.IsAlive && !actor.Hidden && !actor.Suspended && !actor.Untargetable);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)5, admission.Command);
        Assert.Equal((ushort)65533, admission.Action);
        BattleNativeRenzokukenState process = Assert.IsType<BattleNativeRenzokukenState>(source.GetNativeRenzokuken(source.Combatants.Single(actor => actor.IsPlayer && actor.NativeActorIndex == 0).Id));
        Assert.Equal(effect, process.MagicId);
        Assert.NotNull(source.CaptureForContinuation().LogicalContinuation?.Event?.Renzokuken);
        Assert.Contains(effect, target.NativeRenzokukenMagicIds);
        BattlePresentationProcess presentation = source.Presentation!;
        if (effect == 337)
        {
            Assert.Equal(1, presentation.NativeStage!.Extended!.Stage137.ShakeAmplitude);
        }
        NativeBattleGraphicsState graphics = presentation.Graphics;
        NativeBattleStageState stage = presentation.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.IsActive, actor.Hidden, actor.Suspended, actor.Untargetable)).ToArray();
        BattleActionHeaderReference headerReference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = presentation.ActionPackets.ReadHeader(headerReference);
        BattleSession fork = source.ForkForContinuation();
        bool registered = false;
        for (int frame = 0; frame < 1200 && !registered; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            registered = fork.Presentation!.TriggerWidget?.SourceMagicId == effect && fork.Presentation.LastFrameServices?.ReadOrderedReceipts().OfType<NativeBattleModelTriggerRegistered>().Any(receipt => receipt.Owner == admission.Actor && receipt.ActorId == source.Combatants.Single(actor => actor.IsPlayer && actor.NativeActorIndex == 0).Id) == true;
        }
        Assert.True(registered, "The actual widget must register under the ordinary command5 logical owner.");
        Assert.Equal(effect, fork.GetNativeRenzokuken(source.Combatants.Single(actor => actor.IsPlayer && actor.NativeActorIndex == 0).Id)!.MagicId);
        Assert.Same(graphics, presentation.Graphics);
        Assert.Same(stage, presentation.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, presentation.ActionPackets.ReadHeader(headerReference));
        Assert.Equal(combat, source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.IsActive, actor.Hidden, actor.Suspended, actor.Untargetable)).ToArray());
        Assert.True(cpu.TotalProcessorTime - start < TimeSpan.FromSeconds(effect == 337 ? 300 : 120), "The bounded natural source probe exceeded its process CPU budget.");
    }
}
