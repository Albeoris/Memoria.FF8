using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect210FollowingActionTests
{
    [Fact]
    public void Completed_original_component_restarts_camera_banks_and_cleanup_for_the_next_declared_action()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT210_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT210_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed effect210 standalone and a new evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        NativeBattleGraphicsState originalGraphics = source.NativeGraphics!;
        Assert.Equal(210, originalGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, originalGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        Assert.Empty(originalGraphics.OriginalEffect.Pools);
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.All(source.NativeEffectMemory.Effect210!.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        string casterId = originalGraphics.ActorIds[3]!;
        string companionId = originalGraphics.ActorIds[4]!;
        string targetId = originalGraphics.ActorIds[0]!;
        BattleCombatantDefinition caster = source.Definition.Combatants.Single(actor => actor.Id == casterId);
        BattleCombatantDefinition companion = source.Definition.Combatants.Single(actor => actor.Id == companionId);
        Assert.Equal(128, caster.ModelId);
        Assert.Equal(82, companion.ModelId);
        Assert.True(companion.InitialSuspended);
        Assert.True(companion.InitialUntargetable);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 315 && row.MagicId == 210 && row.KernelSection == 3 && row.KernelIndex == 315);
        Assert.Equal((byte)54, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        (packets, int header) = packets.Prepare(new(3, 8, 54, -1, 315, 210, null));
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = originalGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot prepared = source with { NativeActionPackets = packets, NativeGraphics = originalGraphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(source.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(originalGraphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(source.Combatants, prepared.Combatants);
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-before-entry", prepared, null);
        BattleSession battle = Restore(prepared);
        bool registered = false;
        bool completed = false;
        HashSet<string> views = [];
        HashSet<int> bankSides = [];
        int completions = 0;
        BattleSnapshot prior = prepared;
        int callbacks = 0;
        for (; callbacks < 2400; callbacks++)
        {
            battle.Tick();
            BattleSnapshot current = battle.Capture();
            NativeBattleGraphicsState graphics = current.NativeGraphics!;
            views.Add(JsonSerializer.Serialize(current.NativeCamera!.CapturePackedView1D97778()));
            if (graphics.OriginalEffect is { Binding.Effect: 210, ReturnedDescriptor: > 0 } active)
            {
                NativeBattleEffect210State state = current.NativeEffectMemory!.Effect210!;
                uint arena = state.ReadDword(ArenaGlobal);
                uint end = state.ReadDword(SecondCursorGlobal);
                if (end == arena + 0x1B1B8 || end == arena + 0x2B1B8)
                {
                    bankSides.Add(end == arena + 0x1B1B8 ? 0 : 1);
                }
                Assert.Contains(active.Pools, pool => pool.DescriptorAddress == RootDescriptor && pool.Head is not null);
                Assert.Null(state.FixedBanks[RootBank]);
                foreach (NativeBattleEffectTaskPool pool in active.Pools.Where(pool => pool.DescriptorAddress != RootDescriptor))
                {
                    Assert.Null(state.CommonQueueStorage[pool.DescriptorAddress]);
                }
                if (!registered && current.NativeCamera.ControlCursor is not null)
                {
                    NativeBattleEffectAnimationRegisters control = current.NativeCamera.ControlResource!;
                    Assert.Equal(8, control.StreamOffset);
                    Assert.Equal(24, control.ModelTableOffset);
                    Assert.Equal(game.ReadNativeEffect210Resources().Camera.Data.ToArray(), control.Source.Data.ToArray());
                    Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
                    Assert.NotEmpty(current.NativeCamera.Order);
                    ColdNext("following-action-camera-entry", current);
                    registered = true;
                }
            }
            if (registered && prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 210, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: 210, ReturnedDescriptor: 0 } && current.NativeEffectMemory!.Active == 0)
            {
                completions++;
                Assert.Null(current.NativeCamera.ControlCursor);
                Assert.Empty(current.NativeCamera.Order);
                Assert.Null(current.ActiveAction);
                Assert.Empty(current.QueuedActions);
                Assert.Empty(graphics.OriginalEffect.Pools);
                Assert.All(current.NativeEffectMemory.Effect210!.FixedBanks.Values, bank => Assert.NotNull(bank));
                Assert.All(current.NativeEffectMemory.Effect210.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                Assert.Equal(source.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                BattleCombatantSnapshot currentCompanion = current.Combatants.Single(actor => actor.Id == companionId);
                Assert.True(currentCompanion.NativeParticipant!.Suspended);
                Assert.True(currentCompanion.NativeParticipant.Untargetable);
                ColdNext("following-action-completed", current);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-completed-visible", current, new BattleScenePresentation(assets, current.Definition, true).Render(battle, 320, 224).ToArray());
                completed = true;
                break;
            }
            prior = current;
        }
        Assert.True(registered);
        Assert.True(completed);
        Assert.Equal(1, completions);
        Assert.True(views.Count > 1);
        Assert.Equal(2, bankSides.Count);
        BattleSnapshot result = battle.Capture();
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Scenario = "Following declared original128 Amount0 action after effect210 cleanup; original82 companion, warm owners and resources preserved", Effect = 210, Registered = registered, Completed = completed, Completions = completions, DistinctViewMatrices = views.Count, BankSides = bankSides.Order(), Callbacks = result.NativeGraphics!.Callback - originalGraphics.Callback, result.Frame, NativeCallback = result.NativeGraphics.Callback }));

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession session = BattleSession.Restore(snapshot);
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return session;
        }

        void ColdNext(string name, BattleSnapshot snapshot)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, null);
            BattleSnapshot json = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json"));
            BattleSession cold = Restore(json);
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
        }
    }
}
