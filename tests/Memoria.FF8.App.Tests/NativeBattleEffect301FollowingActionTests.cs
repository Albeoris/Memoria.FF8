using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect301FollowingActionTests
{
    [Fact]
    public void Completed_component_reuses_current_bones_and_restarts_camera_before_separate_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT301_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT301_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed effect301 standalone and a new evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        NativeBattleGraphicsState originalGraphics = source.NativeGraphics!;
        Assert.Equal(301, originalGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, originalGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        Assert.Empty(originalGraphics.OriginalEffect.Pools);
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.All(source.NativeEffectMemory.Effect301!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        int casterIndex = originalGraphics.OriginalEffect.Parameters!.Actor78;
        string casterId = originalGraphics.ActorIds[casterIndex]!;
        string targetId = originalGraphics.ActorIds[0]!;
        BattleCombatantDefinition caster = BattleSession.Restore(source).GetCombatantDefinition(casterId);
        Assert.Equal(94, caster.ModelId);
        Assert.Equal((byte)110, originalGraphics.Models[casterIndex].ModelId4);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 152 && row.MagicId == 282 && row.KernelSection == 3 && row.KernelIndex == 152);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        string warmVertices = originalGraphics.Models[casterIndex].CurrentBodyBoneMatrices!.SkeletonSha256;
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(checked((byte)casterIndex), 8, 12, cameraChoice, 152, 282, null));
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = originalGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleCombatantSnapshot target = source.Combatants.Single(actor => actor.Id == targetId);
        target = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([282])[281];
        BattleSnapshot prepared = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(281, binding) }, Combatants = source.Combatants.Select(actor => actor.Id == targetId ? target : actor).ToArray(), NativeActionPackets = packets, NativeGraphics = originalGraphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(source.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(originalGraphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(source.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), prepared.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-before-entry", prepared, null);
        BattleSession battle = Restore(prepared);
        NativeBattleEvilEye282Resources resources = game.ReadNativeEvilEye282Resources();
        string retained301 = JsonSerializer.Serialize(source.NativeEffectMemory.Effect301);
        Assert.False(source.NativeEffectMemory.Effect301!.ScriptSourcePending);
        Assert.Equal(0x103CFB8u, source.NativeEffectMemory.Effect301.ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
        bool requirePlayback = !string.Equals(Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT301_FOLLOWING_REQUIRE_PLAYBACK"), "false", StringComparison.OrdinalIgnoreCase);
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
            Assert.Equal(retained301, JsonSerializer.Serialize(current.NativeEffectMemory!.Effect301));
            views.Add(JsonSerializer.Serialize(current.NativeCamera!.CapturePackedView1D97778()));
            if (graphics.OriginalEffect is { Binding.Effect: 282, ReturnedDescriptor: > 0 } active)
            {
                uint arena = current.NativeEffectMemory!.EvilEye282!.ReadDword(NativeBattleEvilEye282Profile.ArenaGlobal);
                uint cursor = current.NativeEffectMemory.EvilEye282.ReadDword(NativeBattleEvilEye282Profile.CursorGlobal);
                if (cursor >= arena + 0x162C)
                {
                    bankSides.Add(cursor >= arena + 0x1162C ? 1 : 0);
                }
                bool ownsCamera = current.NativeCamera.ControlResource?.Source.Identity == resources.Camera.Identity && current.NativeEffectMemory.Textures?.Animation?.Source.Identity == resources.Camera.Identity;
                if (ownsCamera)
                {
                    cameraViewChanged |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
                    cameraTrack |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
                    if (!registered)
                    {
                        NativeBattleEffectAnimationRegisters control = current.NativeCamera.ControlResource!;
                        Assert.Equal(8, control.StreamOffset);
                        Assert.Equal(24, control.ModelTableOffset);
                        Assert.Equal(resources.Camera.Data.ToArray(), control.Source.Data.ToArray());
                        Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
                        Assert.Equal(warmVertices, current.NativeGraphics!.Models[casterIndex].CurrentBodyBoneMatrices!.SkeletonSha256);
                        warmRetained = true;
                        ColdNext("following-action-camera-entry-warm-source", current);
                        registered = true;
                    }
                }
            }
            if (registered && prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 282, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: 282, ReturnedDescriptor: 0 })
            {
                completions++;
                returnedEdge = true;
                ColdNext("following-action-returned-descriptor-edge", current);
            }
            if (returnedEdge)
            {
                cleanupCallbacks++;
            }
            if (completions > 0 && graphics.OriginalEffect is { Binding.Effect: 282, ReturnedDescriptor: 0 } effect && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104))
            {
                Assert.Null(current.NativeCamera.ControlCursor);
                Assert.Empty(current.NativeCamera.Order);
                Assert.Empty(effect.Pools);
                Assert.All(current.NativeEffectMemory.Effect301!.FixedBanks.Values, bank => Assert.NotNull(bank));
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                Assert.Equal(source.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                ColdNext("following-action-completed-after-cleanup", current);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-completed-visible", current, new BattleScenePresentation(assets, current.Definition, true).Render(battle, 320, 224).ToArray());
                Assert.Equal(retained301, JsonSerializer.Serialize(current.NativeEffectMemory.Effect301));
                Assert.Equal(0x103CFB8u, current.NativeEffectMemory.Effect301!.ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
                completed = true;
                break;
            }
            prior = current;
        }
        BattleSnapshot result = battle.Capture();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-last", result, null);
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Scenario = "Following DIFFERENT original94 action152/effect282 after301 cleanup; exhausted301cursor/currentsource retained without reset", Effect = 282, PriorEffect = 301, Retained301ScriptCursor = source.NativeEffectMemory.Effect301!.ReadDword(NativeBattleEffect301State.ScriptPointerAddress), RequirePlayback = requirePlayback, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, Registered = registered, CameraTrack = cameraTrack, CameraViewChanged = cameraViewChanged, WarmSourceRetained = warmRetained, ReturnedDescriptorEdge = returnedEdge, CleanupCallbacks = cleanupCallbacks, Completed = completed, Completions = completions, DistinctViewMatrices = views.Count, BankSides = bankSides.Order(), Callbacks = result.NativeGraphics!.Callback - originalGraphics.Callback, result.Frame, NativeCallback = result.NativeGraphics.Callback }));
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
