using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect76ComponentSourceTests
{
    [Fact]
    public void Installed_startup_retains_its_process_owned_DAT_text_through_the_first_actual_91_callback()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(76));
        Assert.Equal(981, source.ReadDefinition().EncounterId);
        Assert.Equal(142, source.ReadDefinition().StageId);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 76);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)355, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleEnemyActorModel owner = Assert.IsType<BattleEnemyActorModel>(admission.Actor);
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.Id == owner.Actor.ActorId);
        Assert.Equal(126, caster.ModelId);
        NativeBattleGraphicsResource resource = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(caster.Id));
        Assert.Equal(71, resource.PoseSource!.BoneCount);
        string expected = resource.ReadText91(1);
        Assert.NotNull(resource.ReadText91(3));
        Assert.NotNull(resource.ReadText91(4));
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)14, header.Sequence);
        Assert.Equal(owner, Assert.Single(process.ActionPackets.ReadHeaderContacts(reference)).Primary.Recipient);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        var combat = (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus);
        int eventCount = source.Events.Count;
        BattleSnapshot snapshot = source.CaptureForContinuation();
        BattleCombatant pending = Assert.Single(source.Combatants, actor => actor.NativeAllocationPendingStatistics);
        BattleEnemyActorModel pendingOwner = Assert.IsType<BattleEnemyActorModel>(Assert.Single(snapshot.PresentationContinuation!.ModelRegistry!.Loads.Keys, candidate => candidate is BattleEnemyActorModel enemy && enemy.Actor.ActorId == pending.Id));
        Assert.False(pending.IsActive);
        Assert.Equal(new NativeBattleTargetRequestAdapter.OriginalPreviousOperand(pending.RequireNativeActorIndex()), snapshot.NativeLastAllocatedTarget);
        BattleSession actorReferenceRestore = BattleSession.Restore(snapshot with { NativeLastAllocatedTarget = new BattlePreviousTarget.Actor(pendingOwner.Actor) });
        Assert.True(Assert.Single(actorReferenceRestore.Combatants, actor => actor.Id == pending.Id).NativeAllocationPendingStatistics);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeLastAllocatedTarget = new NativeBattleTargetRequestAdapter.OriginalPreviousOperand(caster.RequireNativeActorIndex()) }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeLastAllocatedTarget = new BattlePreviousTarget.Actor(owner.Actor) }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeLastAllocatedTarget = null }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeScriptState = snapshot.NativeScriptState! with { AllocationControl = snapshot.NativeScriptState!.AllocationControl! with { ActionGate = false } } }));
        BattlePresentationSnapshot presentation = snapshot.PresentationContinuation!;
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { PresentationContinuation = presentation with { ModelRegistry = presentation.ModelRegistry! with { Loads = presentation.ModelRegistry!.Loads.Remove(pendingOwner) } } }));
        BattleSession fork;
        try
        {
            fork = source.ForkForContinuation();
        }
        catch (InvalidDataException error)
        {
            string actors = string.Join("; ", source.Combatants.Where(actor => !actor.IsPlayer).Select(NativeBattleComponentSourceTests.OriginalSourceActorFacts));
            throw new InvalidDataException($"Original source private-fork ownership failed: {actors}.", error);
        }
        int forkEventCount = fork.Events.Count;
        bool observed = false;
        for (int frame = 0; frame < 1200 && !observed; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            observed = fork.Events.Skip(forkEventCount).Any(item => item.Kind == "NativeText" && item.ActorId == caster.Id && item.Message == expected && item.Amount == 60 && item.NativeMessageMode == 3);
        }
        NativeBattleGraphicsState finalGraphics = fork.Presentation!.Graphics;
        string textEvents = string.Join("; ", fork.Events.Where(item => item.Kind == "NativeText").TakeLast(8).Select(item => $"frame={item.Frame}, actor={item.ActorId}, duration={item.Amount}, mode={item.NativeMessageMode}, text={item.Message}"));
        string queuedTexts = string.Join("; ", finalGraphics.Descriptors.Packets.Where(packet => packet.Message is not null).Take(8).Select(packet => $"actor={packet.Message!.ActorId}, duration={packet.Message.Amount}, mode={packet.Message.NativeMessageMode}, text={packet.Message.Message}"));
        string activeTasks = string.Join("; ", finalGraphics.Descriptors.Tasks.Where(task => (task.Flags & 1) != 0).Take(8).Select(task => $"kind={task.Kind}, phase={task.Phase}"));
        string moduleTasks = finalGraphics.OriginalEffect is { } activeEffect ? string.Join("; ", activeEffect.Pools.SelectMany(pool => pool.Slots).Where(task => task.Callback != 0).Take(8).Select(task => $"callback={task.Callback:X}, age={task.ReadWord(12)}")) : "";
        Assert.True(observed, $"The actual effect callback must read the bound caster DAT text bank. Source events={eventCount}, initial fork events={forkEventCount}, final fork events={fork.Events.Count}, host callback={fork.NativeHostCallbacks}, graphics callback={finalGraphics.Callback}, flags={finalGraphics.Flags:X}, effect={finalGraphics.OriginalEffect?.RequestedEffect}, returned={finalGraphics.OriginalEffect?.ReturnedDescriptor}, expected={expected}. NativeText=[{textEvents}]. Queued text=[{queuedTexts}]. Active tasks=[{activeTasks}]. Module tasks=[{moduleTasks}].");
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(eventCount, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }
}
