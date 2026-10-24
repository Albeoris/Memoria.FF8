using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect209FollowingActionTests
{
    [Fact]
    public void Completed_original_component_runs_the_next_declared_action_with_restarted_camera()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT209_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT209_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed effect209 standalone and a new evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(209, source.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, source.NativeGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 319 && row.MagicId == 209 && row.KernelSection == 3 && row.KernelIndex == 319);
        string targetId = source.NativeGraphics.ActorIds[0]!;
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        (packets, int header) = packets.Prepare(new(3, 8, 13, -1, 319, 209, null));
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = source.NativeGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot prepared = source with { NativeActionPackets = packets, NativeGraphics = source.NativeGraphics with { Descriptors = descriptors } };
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-before-entry", prepared, null);
        BattleSession battle = Restore(prepared);
        bool registered = false;
        bool completed = false;
        HashSet<string> poses = [];
        int callbacks = 0;
        for (; callbacks < 400; callbacks++)
        {
            battle.Tick();
            BattleSnapshot current = battle.Capture();
            poses.Add(JsonSerializer.Serialize(current.NativeCamera!.CapturePackedView1D97778()));
            if (!registered && current.NativeGraphics!.OriginalEffect is { Binding.Effect: 209, ReturnedDescriptor: > 0 } && current.NativeCamera.ControlCursor is not null)
            {
                NativeBattleEffectAnimationRegisters control = current.NativeCamera.ControlResource!;
                Assert.Equal(8, control.StreamOffset);
                Assert.Equal(32, control.ModelTableOffset);
                Assert.Equal(game.ReadNativeEffect209Resources().Camera.Data.ToArray(), control.Source.Data.ToArray());
                Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory!.Textures!.Animation));
                Assert.NotEmpty(current.NativeCamera.Order);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-camera-entry", current, null);
                BattleSession cold = Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(current))!);
                battle.Tick();
                cold.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                registered = true;
            }
            if (registered && current.NativeGraphics!.OriginalEffect is { Binding.Effect: 209, ReturnedDescriptor: 0 } && current.NativeEffectMemory!.Active == 0)
            {
                Assert.Null(current.NativeCamera.ControlCursor);
                Assert.Empty(current.NativeCamera.Order);
                Assert.Null(current.ActiveAction);
                Assert.Empty(current.QueuedActions);
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                Assert.Equal(source.NativeStage!.Extended!.Stage137.Variants!.Index + 1, current.NativeStage!.Extended!.Stage137.Variants!.Index);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-completed", current, new BattleScenePresentation(assets, current.Definition, true).Render(battle, 320, 224).ToArray());
                completed = true;
                break;
            }
        }
        Assert.True(registered);
        Assert.True(completed);
        Assert.True(poses.Count > 1);
        BattleSnapshot result = battle.Capture();
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Scenario = "Following declared original115 Amount0 action after effect209 cleanup; warm state and resources preserved", Registered = registered, Completed = completed, DistinctViewMatrices = poses.Count, Callbacks = result.NativeGraphics!.Callback - source.NativeGraphics.Callback, result.Frame, NativeCallback = result.NativeGraphics.Callback }));

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession session = BattleSession.Restore(snapshot);
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return session;
        }
    }
}
