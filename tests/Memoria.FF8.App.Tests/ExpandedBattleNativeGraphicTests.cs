using System.Reflection;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleNativeGraphicTests
{
    [Theory]
    [InlineData(29, 110)]
    [InlineData(164, 248)]
    public void Installed_original_ai_graphic_completes_on_the_attached_process(ushort formation, ushort effectId)
    {
        RunInstalledAiGraphic(TestResources.GameDirectory, formation, effectId);
    }

    internal static void RunInstalledAiGraphic(string directory, ushort formation, ushort effectId)
    {
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 4)).Accepted);
        ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session);
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));

        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleEntryAdmissionTests.ReachReady(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        BattleCombatant attacker = battle.Combatants.Single(actor => actor.Id == "party-3");
        BattleCombatant trigger = battle.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == (effectId == 110 ? 74 : 101));
        BattleCombatant? wedge = null;
        if (effectId == 110)
        {
            wedge = battle.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 73);
            Assert.True(wedge.Suspended);
            Strike(trigger, 1);
            bool wedgeInit = false;
            for (int callback = 0; callback < 1200 && (wedge.Suspended || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
            {
                Advance();
                wedgeInit |= HasSeparateInit(wedge.Id);
            }
            Assert.True(wedgeInit);
            Assert.True(trigger.IsAlive);
            Assert.False(wedge.Suspended);
        }

        Strike(trigger, effectId == 110 ? 1 : trigger.MaximumHp / 10 + 1);
        long? actionToken = null;
        string? casterId = null;
        int contextCount = 0;
        bool drewEffect = false;
        bool reserveContext = false;
        HashSet<string> reserveInitOwners = [];
        Dictionary<BattleModelOwner, byte> reserveStatistics = [];
        for (int callback = 0; callback < 1600 && (actionToken is null || !process.IsActionComplete(actionToken.Value)); callback++)
        {
            Advance();
            if (effectId == 248)
            {
                if (battle.ActiveAction?.ExpandedAiCallback is { Stage: 0 } active)
                {
                    reserveInitOwners.Add(active.Actor.ActorId);
                }
                foreach (BattleAttackCommand command in battle.QueuedActions)
                {
                    if (command.ExpandedAiCallback is { Stage: 0 } queued)
                    {
                        reserveInitOwners.Add(queued.Actor.ActorId);
                    }
                }
                foreach (NativeBattleAllocationStatisticsRequested statistics in (process.LastFrameServices?.ReadOrderedReceipts() ?? []).OfType<NativeBattleAllocationStatisticsRequested>())
                {
                    reserveStatistics[statistics.Owner] = statistics.SceneSlot;
                }
                if (process.Graphics.OriginalEffect is { Binding.Effect: 243, Parameters: { } context, ReturnedDescriptor: > 0 } && !reserveContext)
                {
                    NativeBattleActionHeaderSlot contextHeader = process.ActionPackets.ReadHeader(context.Header);
                    Assert.Equal((byte)8, contextHeader.Command);
                    Assert.Equal((ushort)147, contextHeader.Action);
                    NativeBattleContactPacket packet = Assert.Single(process.ActionPackets.ReadHeaderContacts(context.Header));
                    Assert.Equal(contextHeader.Source, packet.Primary.Recipient);
                    Assert.Equal(0, packet.Primary.PresentedAmount);
                    Assert.Equal((byte)1, packet.Primary.Flags2);
                    Assert.Equal((byte)0, packet.Primary.HitFlags);
                    Assert.Equal((byte)0, packet.Primary.Sequence);
                    Assert.Null(packet.Primary.CoverSource);
                    Assert.Null(packet.Secondary);
                    Assert.Empty(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>());
                    reserveContext = true;
                }
            }
            if (process.Graphics.OriginalEffect is { Parameters: { } parameters } effect && effect.Binding.Effect == effectId && effect.ReturnedDescriptor is > 0)
            {
                actionToken ??= Assert.IsType<long>(process.LastStepActionToken);
                Assert.Equal(actionToken, process.LastStepActionToken);
                NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(parameters.Header);
                Assert.Equal((byte)252, header.Command);
                Assert.Equal((ushort)1, header.Action);
                Assert.Equal(effectId, header.Effect);
                casterId = Assert.IsType<BattleEnemyActorModel>(header.Source).Actor.ActorId;
                contextCount = header.ReadContactCount();
                Assert.True(contextCount > 0);
                if (effectId == 248)
                {
                    Assert.Equal(71, battle.Combatants.Single(actor => actor.Id == casterId).ModelId);
                    NativeBattleContactPacket packet = Assert.Single(process.ActionPackets.ReadHeaderContacts(parameters.Header));
                    Assert.Equal(header.Source, packet.Primary.Recipient);
                }
                Assert.Equal(contextCount, battle.Events.Count(entry => entry.Kind == "EffectContact" && entry.NativeGraphicMagicId == effectId));
                Assert.Empty(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>());
                drewEffect |= effectId == 110
                    ? process.Graphics.PrimitiveFrame?.LastCommands.Any(packet => packet.Command.PilotGeometry?.Identity.Effect == effectId) == true
                    : !effect.PresentationPackets.IsEmpty;
            }
        }
        Assert.True(process.IsActionComplete(Assert.IsType<long>(actionToken)));
        Assert.NotNull(casterId);
        Assert.True(drewEffect);
        Assert.Equal(contextCount, battle.Events.Count(entry => entry.Kind == "EffectContact" && entry.NativeGraphicMagicId == effectId));
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage" && entry.NativeGraphicMagicId == effectId);
        if (effectId == 248)
        {
            Assert.True(reserveContext);
            Assert.Contains(casterId, reserveInitOwners);
            BattleCombatant reserve = battle.Combatants.Single(actor => actor.Id == casterId);
            BattleModelOwner reserveOwner = battle.ReadPresentationModelOwner(reserve.Id);
            BattleEvent spawn = Assert.Single(battle.Events, entry => entry.Kind == "NativeSpawn" && entry.TargetId == reserve.Id);
            Assert.Equal(reserve.SceneSlot, spawn.Amount);
            Assert.True(reserveStatistics.TryGetValue(reserveOwner, out byte statisticsScene));
            Assert.Equal(reserve.SceneSlot, statisticsScene);
            Assert.False(reserve.NativeAllocationPendingStatistics);
            Assert.False(reserve.Suspended);
            Assert.True(reserve.IsActive);
            int physicalSlot = Assert.IsType<int>(reserve.NativeActorIndex);
            Assert.Contains($"into physical slot {physicalSlot}.", spawn.Message);
            Assert.Equal(reserveOwner, process.ActionPackets.ModelAliases.DecodeLiteral(checked((byte)physicalSlot)));
            Assert.Equal(reserveOwner, process.Graphics.NativeBindings.DecodeLiteral(checked((byte)physicalSlot), process.Graphics.ActorModels));
            AssertAllocationMaterial(reserve);
        }

        if (effectId == 110)
        {
            BattleCombatant? elvoret = null;
            bool arrived = false;
            bool elvoretInit = false;
            for (int callback = 0; callback < 1600; callback++)
            {
                Advance();
                arrived |= process.Graphics.OriginalEffect is { Binding.Effect: 17, ReturnedDescriptor: > 0 };
                elvoret ??= battle.Combatants.SingleOrDefault(actor => !actor.IsPlayer && actor.ModelId == 91 && actor.Id.StartsWith("native-allocation-", StringComparison.Ordinal));
                if (elvoret is not null)
                {
                    elvoretInit |= HasSeparateInit(elvoret.Id);
                    BattleModelOwner owner = battle.ReadPresentationModelOwner(elvoret.Id);
                    if (elvoretInit && elvoret.IsAlive && !elvoret.Suspended && !elvoret.NativeAllocationPendingStatistics &&
                        process.Graphics.ActorModels.TryGet(owner, out NativeBattleGraphicsModel? model) && (model.Flags0 & 2) != 0 &&
                        process.IsControlPrefixRetired && process.ActiveActionToken is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
                    {
                        break;
                    }
                }
            }
            Assert.True(arrived, "The original Elvoret arrival effect did not run after Control110.");
            Assert.NotNull(elvoret);
            Assert.True(elvoretInit, "Elvoret did not receive a separate INIT callback after its model load.");
            Assert.False(trigger.IsActive);
            Assert.False(wedge!.IsActive);
            Assert.True(elvoret.IsAlive);
            Assert.False(elvoret.Suspended);
            Assert.False(elvoret.NativeAllocationPendingStatistics);
            Assert.Equal(2, process.Graphics.ActorModels.Get(battle.ReadPresentationModelOwner(elvoret.Id)).Flags0 & 2);
            Assert.True(process.IsControlPrefixRetired);
            Assert.Null(process.ActiveActionToken);
            Assert.Null(battle.ActiveAction);
            Assert.Empty(battle.QueuedActions);
            AssertAllocationMaterial(elvoret);
        }

        BattleCombatant nextTarget = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive && actor.SceneSlot >= 8);
        int nextEvent = battle.Events.Count;
        Strike(nextTarget, nextTarget.CurrentHp);
        for (int callback = 0; callback < 1600 && !CompletedNextAttack(); callback++)
        {
            Advance();
        }
        Assert.True(CompletedNextAttack());

        void AssertAllocationMaterial(BattleCombatant actor)
        {
            BattleModelOwner owner = battle.ReadPresentationModelOwner(actor.Id);
            NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(owner);
            Assert.Equal(owner, process.ModelTextureCache.PresentationActors![model.TextureResourceIndex!.Value]);
            NativeBattleEffectTextureState textures = process.Memory.Textures!;
            Assert.Equal(process.PlannedModelRoster.Count, textures.PresentationTextures!.Count);
            NativeBattlePresentationTextureBank bank = textures.RequirePresentationTexture(actor.Id);
            Assert.NotSame(textures.VramCaptureIdentity, bank.Textures.VramCaptureIdentity);
        }

        bool HasSeparateInit(string actorId) =>
            battle.ActiveAction?.ExpandedAiCallback is { Stage: 0 } active && active.Actor.ActorId == actorId ||
            battle.QueuedActions.Any(command => command.ExpandedAiCallback is { Stage: 0 } request && request.Actor.ActorId == actorId);

        bool CompletedNextAttack() => battle.Events.Skip(nextEvent).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == attacker.Id && entry.TargetId == nextTarget.Id);

        void Strike(BattleCombatant target, int hp)
        {
            target.CurrentHp = hp;
            attacker.Atb = BattleSession.AtbMaximum;
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: attacker.Id, TargetId: target.Id));
        }

        void Advance()
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
        }
    }
}
