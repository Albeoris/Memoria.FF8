using System.Text.Json;
using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect63FollowingActionTests
{
    [Fact]
    public void Completed_component_reuses_current_bones_and_restarts_camera_before_separate_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT63_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT63_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed effect63 standalone and a new evidence directory.");
        bool requirePlayback = !string.Equals(Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT63_FOLLOWING_REQUIRE_PLAYBACK"), "false", StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        NativeBattleGraphicsState originalGraphics = source.NativeGraphics!;
        Assert.Equal(63, originalGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, originalGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        Assert.Empty(originalGraphics.OriginalEffect.Pools);
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.All(source.NativeEffectMemory.Effect63!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        string casterId = originalGraphics.ActorIds[4]!;
        string targetId = originalGraphics.ActorIds[0]!;
        BattleCombatantDefinition caster = BattleSession.Restore(source).GetCombatantDefinition(casterId);
        Assert.Equal(125, caster.ModelId);
        Assert.Equal((byte)141, originalGraphics.Models[4].ModelId4);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 361 && row.MagicId == 63 && row.KernelSection == 3 && row.KernelIndex == 361);
        Assert.Equal((byte)11, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        string warmVertices = originalGraphics.Models[4].CurrentBodyBoneMatrices!.SkeletonSha256;
        string warmState = JsonSerializer.Serialize(source.NativeEffectMemory.Effect63);
        string[] warmSkeletons = originalGraphics.Models.Skip(3).Take(3).Select(model => model.CurrentBodyBoneMatrices!.SkeletonSha256).ToArray();
        Assert.Equal(new byte?[] { 140, 141, 141 }, originalGraphics.Models.Skip(3).Take(3).Select(model => model.ModelId4).ToArray());
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(4, 8, 11, cameraChoice, 361, 63, null));
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = originalGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot prepared = source with { Combatants = source.Combatants.Select(actor => actor.Id == targetId ? actor with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } } : actor).ToArray(), NativeActionPackets = packets, NativeGraphics = originalGraphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(source.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(originalGraphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(source.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), prepared.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-before-entry", prepared, null);
        BattleSession battle = Restore(prepared);
        NativeBattleEffect63Resources resources = game.ReadNativeEffect63Resources();
        NativeBattleEffect63PresentationObserver observer = new(assets);
        bool registered = false;
        bool warmRetained = false;
        bool cameraTrack = false;
        bool completed = false;
        bool cameraViewChanged = false;
        bool returnedEdge = false;
        int completions = 0;
        int cleanupCallbacks = 0;
        HashSet<string> views = [];
        HashSet<int> bankSides = [];
        BattleSnapshot prior = prepared;
        int callbacks = 0;
        for (; callbacks < 1200; callbacks++)
        {
            battle.Tick();
            BattleSnapshot current = battle.Capture();
            NativeBattleGraphicsState graphics = current.NativeGraphics!;
            observer.Observe(prior, current, ColdNext);
            Assert.Equal(new byte?[] { 140, 141, 141 }, graphics.Models.Skip(3).Take(3).Select(model => model.ModelId4).ToArray());
            Assert.Equal(warmSkeletons, graphics.Models.Skip(3).Take(3).Select(model => model.CurrentBodyBoneMatrices!.SkeletonSha256).ToArray());
            views.Add(JsonSerializer.Serialize(current.NativeCamera!.CapturePackedView1D97778()));
            if (graphics.OriginalEffect is { Binding.Effect: 63, ReturnedDescriptor: > 0 } active)
            {
                NativeBattleEffect63State state = current.NativeEffectMemory!.Effect63!;
                uint arena = state.ReadDword(WorkGlobal);
                uint cursor = state.ReadDword(CursorGlobal);
                if (cursor >= arena)
                {
                    bankSides.Add(cursor >= arena + SecondPacketOffset ? 1 : 0);
                }
                Assert.Contains(active.Pools, pool => pool.DescriptorAddress == RootDescriptor && pool.Head is not null);
                Assert.Null(state.FixedBanks[RootBank]);
                bool ownsCamera = current.NativeCamera.ControlResource?.Source.Identity == resources.Camera.Identity && current.NativeEffectMemory.Textures?.Animation?.Source.Identity == resources.Camera.Identity;
                if (ownsCamera)
                {
                    cameraViewChanged |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
                    cameraTrack |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(4))..]));
                    if (!registered)
                    {
                        NativeBattleEffectAnimationRegisters control = current.NativeCamera.ControlResource!;
                        Assert.Equal(8, control.StreamOffset);
                        Assert.Equal(52, control.ModelTableOffset);
                        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(4)), control.ModelTableOffset);
                        Assert.Equal(resources.Camera.Data.ToArray(), control.Source.Data.ToArray());
                        Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
                        Assert.Equal(warmVertices, current.NativeGraphics!.Models[4].CurrentBodyBoneMatrices!.SkeletonSha256);
                        warmRetained = true;
                        ColdNext("following-action-camera-entry-warm-source", current);
                        registered = true;
                    }
                }
            }
            if (registered && prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 63, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: 63, ReturnedDescriptor: 0 })
            {
                completions++;
                returnedEdge = true;
                ColdNext("following-action-returned-descriptor-edge", current);
            }
            if (returnedEdge)
            {
                cleanupCallbacks++;
            }
            if (completions > 0 && graphics.OriginalEffect is { Binding.Effect: 63, ReturnedDescriptor: 0 } effect && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104))
            {
                Assert.Null(current.NativeCamera.ControlCursor);
                Assert.Empty(current.NativeCamera.Order);
                Assert.Empty(effect.Pools);
                Assert.All(current.NativeEffectMemory.Effect63!.FixedBanks.Values, bank => Assert.NotNull(bank));
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                Assert.Equal(source.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                ColdNext("following-action-completed-after-cleanup", current);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-completed-visible", current, new BattleScenePresentation(assets, current.Definition, true).Render(battle, 320, 224).ToArray());
                completed = true;
                break;
            }
            prior = current;
        }
        BattleSnapshot result = battle.Capture();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-last", result, null);
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Scenario = "Following declared original125 Amount0 action after effect63 cleanup; genuine current three-model ownership, bones and retained State on entry", Effect = 63, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, RequirePlayback = requirePlayback, Registered = registered, CameraTrack = cameraTrack, CameraViewChanged = cameraViewChanged, WarmSourceRetained = warmRetained, WarmModels = warmSkeletons.Length, WarmStateBeforeEntry = warmState, ReturnedDescriptorEdge = returnedEdge, CleanupCallbacks = cleanupCallbacks, Completed = completed, Completions = completions, DistinctViewMatrices = views.Count, BankSides = bankSides.Order(), Callbacks = result.NativeGraphics!.Callback - originalGraphics.Callback, result.Frame, NativeCallback = result.NativeGraphics.Callback }));
        observer.Publish(output!);
        if (completed)
        {
            observer.Accept(result, output!);
        }
        Assert.True(registered);
        if (requirePlayback)
        {
            Assert.True(cameraTrack);
            Assert.True(views.Count > 1);
            Assert.True(cameraViewChanged);
        }
        Assert.True(warmRetained);
        Assert.True(returnedEdge);
        Assert.True(completed);
        Assert.Equal(1, completions);
        Assert.Equal(2, bankSides.Count);
        Assert.True(cleanupCallbacks >= 4);

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
