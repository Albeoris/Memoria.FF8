using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7FrogModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalFrogImpactSelectsItsNativeSkeletonAndRestores(bool supplyCure)
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FROG_MODEL_SAVE");
        Assert.SkipUnless(ff7 is not null && ff8 is not null && path is not null, "Select original resources and an external native Frog continuation.");
        byte[] bytes = File.ReadAllBytes(path!);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave source = CombinedSessionCodec.Read(input);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        BattleModelData frog = BattleModelData.Read(archive.ReadEntry(archive.Find("rsaa").Single()), archive.ReadEntry(archive.Find("rsda").Single()));
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        if (supplyCure)
        {
            int cureId = Enumerable.Range(0, 128).First(id => Ff7NativeBattleActionResolver.TryReadItem((ushort)id, kernel) is { } attack && Ff7NativeBattleStatuses.ReadRecordMask(attack) == 0x800 && Ff7NativeBattleStatuses.ReadRecordMode(attack) == 1);
            // This separate authored supply case exercises native cure execution; the unmodified source has no cure item.
            Ff7Progress supplied = source.Ff7!.Progress with { Inventory = [.. source.Ff7!.Progress.Inventory, new(cureId, 1)] };
            source = source with { Ff7 = source.Ff7! with { Progress = supplied } };
        }
        using Ff7Session initial = Ff7Session.Restore(installation, source.Ff7!);
        for (int step = 0; step < 4 && initial.Battle is null; step++)
        {
            initial.AdvanceTick();
        }
        Ff7SessionSave entered = initial.Capture();
        if (supplyCure)
        {
            // Isolate cure in an authored initial status fixture; the other case retains the actual enemy execution.
            Ff7BattleActorState[] actors = entered.Battle!.Actors.Select(actor =>
            {
                uint statuses = actor.Actor.Statuses;
                if (actor.Actor.Slot >= 4)
                {
                    statuses |= 0x400U;
                }
                else if (actor.Actor.Slot == 1)
                {
                    statuses |= 0x800U;
                }
                return actor with { Actor = actor.Actor with { Statuses = statuses } };
            }).ToArray();
            entered = entered with { Battle = entered.Battle! with { Actors = actors, ActorModels = [] } };
        }
        using Ff7Session session = Ff7Session.Restore(installation, entered);
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        using CrossoverMixedBattle mixed = new(ff8!, source with { Ff7 = session.Capture() }, battle);
        int transformed = -1;
        for (int tick = 0; tick < 5000 && battle.Outcome == Ff7BattleOutcome.Fighting; tick++)
        {
            battle.Advance();
            transformed = battle.Actors.Where(actor => actor.Actor.Slot is 1 or 2 && battle.ActorModelPrefix(actor.Actor.Slot) == "rs" && battle.Capture().ActorModels.Single(model => model.Slot == actor.Actor.Slot).Joints is not null).Select(actor => actor.Actor.Slot).DefaultIfEmpty(-1).First();
            if (transformed >= 0)
            {
                break;
            }
        }
        Assert.True(transformed >= 0, "The actual native enemy attack did not publish a Frog actor model.");
        var model = battle.Capture().ActorModels.Single(model => model.Slot == transformed);
        Assert.True(model.PresentedFrog);
        Assert.Equal(frog.Bones.Count + 1, model.Joints!.Length);
        var pose = battle.RenderActorPose(transformed);
        Assert.InRange(pose.Clip, 0, frog.BodyClips.Count - 1);
        Assert.InRange(pose.Frame, 0, checked((int)frog.BodyClips[pose.Clip].FrameCount) - 1);
        CombinedSessionSave saved = source with { Ff7 = session.Capture(), MixedBattlePayload = mixed.Capture() };
        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, saved);
        serialized.Position = 0;
        CombinedSessionSave restored = CombinedSessionCodec.Read(serialized);
        using Ff7Session cold = Ff7Session.Restore(installation, restored.Ff7!);
        using CrossoverMixedBattle coldMixed = new(ff8!, restored, cold.Battle!);
        Assert.Equal("rs", cold.Battle!.ActorModelPrefix(transformed));
        for (int tick = 0; tick < 32; tick++)
        {
            battle.Advance();
            cold.Battle.Advance();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Battle.Capture()));
        }
        if (!supplyCure)
        {
            int frogAttacks = 0;
            for (int tick = 0; tick < 2000 && battle.Outcome == Ff7BattleOutcome.Fighting; tick++)
            {
                if (battle.ReadyPlayer is int owner)
                {
                    var choice = Ff7Scene.PlanAutoPlayBattleCommand(battle.AutoPlayCommands(owner), battle.Actors, (command, target) => battle.GuestAbilityTargetUnsupportedReason(owner, command.Command, command.Argument, target) is null, (command, target) => battle.DescribeRecovery(owner, command, target));
                    if (choice is { } action)
                    {
                        if (battle.ActorModelPrefix(owner) == "rs" && action.Command.Command == Ff7BattleCommand.Attack)
                        {
                            frogAttacks++;
                        }
                        var preview = action.Command.Command == Ff7BattleCommand.Attack ? mixed.GuestAt(owner)?.PreviewSmartAttack(action.TargetSlot) : null;
                        Assert.True(preview is not null ? mixed.GuestAt(owner)!.SubmitSmartAttack(preview) : battle.Submit(owner, action.Command.Command, action.Command.Argument, Ff7Scene.AutoPlayBattleTargetMask(action.Command, action.TargetSlot)));
                    }
                }
                battle.Advance();
            }
            Assert.True(frogAttacks > 0, "The original continuation did not execute an ordinary attack through the transformed resource bank.");
            Assert.Equal(bytes, File.ReadAllBytes(path!));
            return;
        }
        Ff7InventorySlot cure = Assert.Single(source.Ff7!.Progress.Inventory, item => item.Quantity > 0 && Ff7NativeBattleActionResolver.TryReadItem((ushort)item.ItemId, kernel) is { } attack && (Ff7NativeBattleStatuses.ReadRecordMask(attack) & 0x800) != 0 && Ff7NativeBattleStatuses.ReadRecordMode(attack) is 1 or 2);
        bool submitted = false;
        for (int tick = 0; tick < 5000 && battle.Outcome == Ff7BattleOutcome.Fighting && (battle.ActorModelPrefix(transformed) == "rs" || battle.Capture().Inventory.Any(item => item.ItemId == cure.ItemId && item.Quantity > 0)); tick++)
        {
            if (!submitted)
            {
                foreach (var actor in battle.Actors.Where(actor => actor.Actor.Slot is 1 or 2 && actor.AwaitingTurn))
                {
                    if (battle.Submit(actor.Actor.Slot, Ff7BattleCommand.Item, (ushort)cure.ItemId, (ushort)(1 << transformed)))
                    {
                        Assert.True(cold.Battle.Submit(actor.Actor.Slot, Ff7BattleCommand.Item, (ushort)cure.ItemId, (ushort)(1 << transformed)));
                        submitted = true;
                        break;
                    }
                }
            }
            battle.Advance();
            cold.Battle.Advance();
        }
        Assert.True(submitted);
        Assert.Equal(0U, battle.Actors.Single(actor => actor.Actor.Slot == transformed).Actor.Statuses & 0x800);
        Assert.Equal(Ff7BattleAnimationResources.ModelPrefix(battle.Actors.Single(actor => actor.Actor.Slot == transformed).Identity, false), battle.ActorModelPrefix(transformed));
        Assert.Equal(cure.Quantity - 1, battle.Capture().Inventory.Where(item => item.ItemId == cure.ItemId).Sum(item => item.Quantity));
        for (int tick = 0; tick < 32; tick++)
        {
            battle.Advance();
            cold.Battle.Advance();
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Battle.Capture()));
        Assert.Equal(bytes, File.ReadAllBytes(path!));
    }
}
