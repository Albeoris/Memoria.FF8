using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect323Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect323FollowingActionTests
{
    [Fact]
    public void Completed_component_reuses_current_bones_and_restarts_camera_before_separate_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT323_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT323_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed effect323 standalone and a new evidence directory.");
        bool requirePlayback = !string.Equals(Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT323_FOLLOWING_REQUIRE_PLAYBACK"), "false", StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleFixtureInspection.ReadLogicalSnapshot(path!);
        NativeBattleGraphicsState originalGraphics = source.ReadFixturePresentation().NativeGraphics!;
        Assert.Equal(323, originalGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, originalGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal(0, source.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Empty(originalGraphics.OriginalEffect.Pools);
        Assert.Null(source.ReadFixturePresentation().NativeCamera!.ControlCursor);
        Assert.Empty(source.ReadFixturePresentation().NativeCamera!.Order);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.All(source.ReadFixturePresentation().NativeEffectMemory!.Effect323!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        string casterId = ReadOriginalModelActorId(originalGraphics, 4)!;
        string targetId = ReadOriginalModelActorId(originalGraphics, 0)!;
        BattleCombatantDefinition caster = source.Definition.Combatants.Single(actor => actor.Id == casterId);
        Assert.Equal(119, caster.ModelId);
        Assert.Equal((byte)135, ReadOriginalModelCell(originalGraphics, 4).ModelId4);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 215 && row.MagicId == 323 && row.KernelSection == 3 && row.KernelIndex == 215);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        string warmVertices = ReadOriginalModelCell(originalGraphics, 4).CurrentBodyBoneMatrices!.SkeletonSha256;
        NativeBattleActionPacketsState packets = source.ReadFixturePresentation().NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(4), 8, 13, cameraChoice, 215, 323, null));
        (packets, _) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(0), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = originalGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header.Ordinal * 20));
        BattleSnapshot prepared = (source.WithFixturePresentation(facts => facts with { NativeActionPackets = packets })).WithFixturePresentation(facts => facts with { NativeGraphics = originalGraphics with { Descriptors = descriptors } });
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeEffectMemory), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeFileStaging), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(originalGraphics)), JsonSerializer.Serialize(ReadOriginalModelCells(prepared.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(source.Combatants, prepared.Combatants);
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-before-entry", prepared, null);
        BattleSession battle = Restore(prepared);
        NativeBattleEffect323Resources resources = game.ReadNativeEffect323Resources();
        NativeBattleEffect323PresentationObserver observer = new();
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
            BattleSnapshot current = battle.CaptureForContinuation();
            NativeBattleGraphicsState graphics = current.ReadFixturePresentation().NativeGraphics!;
            observer.Observe(prior, current, ColdNext);
            views.Add(JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera!.CapturePackedView1D97778()));
            if (graphics.OriginalEffect is { Binding.Effect: 323, ReturnedDescriptor: > 0 } active)
            {
                NativeBattleEffect323State state = current.ReadFixturePresentation().NativeEffectMemory!.Effect323!;
                uint arena = state.ReadDword(ArenaGlobal);
                uint cursor = state.ReadDword(CursorGlobal);
                if (cursor >= arena + FirstPacketOffset)
                {
                    bankSides.Add(cursor >= arena + SecondPacketOffset ? 1 : 0);
                }
                Assert.Contains(active.Pools, pool => pool.DescriptorAddress == RootDescriptor && pool.Head is not null);
                Assert.Null(state.FixedBanks[RootBank]);
                bool ownsCamera = current.ReadFixturePresentation().NativeCamera!.ControlResource?.Source.Identity == resources.Camera.Identity && current.ReadFixturePresentation().NativeEffectMemory!.Textures?.Animation?.Source.Identity == resources.Camera.Identity;
                if (ownsCamera)
                {
                    cameraViewChanged |= JsonSerializer.Serialize(prior.ReadFixturePresentation().NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera!.CapturePackedView1D97778());
                    cameraTrack |= current.ReadFixturePresentation().NativeCamera!.Order.Any(slot => current.ReadFixturePresentation().NativeCamera!.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(4))..]));
                    if (!registered)
                    {
                        NativeBattleEffectAnimationRegisters control = current.ReadFixturePresentation().NativeCamera!.ControlResource!;
                        Assert.Equal(8, control.StreamOffset);
                        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(4)), control.ModelTableOffset);
                        Assert.Equal(resources.Camera.Data.ToArray(), control.Source.Data.ToArray());
                        Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.ReadFixturePresentation().NativeEffectMemory!.Textures!.Animation));
                        Assert.Equal(warmVertices, ReadOriginalModelCell(current.ReadFixturePresentation().NativeGraphics!, 4).CurrentBodyBoneMatrices!.SkeletonSha256);
                        warmRetained = true;
                        ColdNext("following-action-camera-entry-warm-source", current);
                        registered = true;
                    }
                }
            }
            if (registered && prior.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 323, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: 323, ReturnedDescriptor: 0 })
            {
                completions++;
                returnedEdge = true;
                ColdNext("following-action-returned-descriptor-edge", current);
            }
            if (returnedEdge)
            {
                cleanupCallbacks++;
            }
            if (completions > 0 && graphics.OriginalEffect is { Binding.Effect: 323, ReturnedDescriptor: 0 } effect && current.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104))
            {
                Assert.Null(current.ReadFixturePresentation().NativeCamera!.ControlCursor);
                Assert.Empty(current.ReadFixturePresentation().NativeCamera!.Order);
                Assert.Empty(effect.Pools);
                Assert.All(current.ReadFixturePresentation().NativeEffectMemory!.Effect323!.FixedBanks.Values, bank => Assert.NotNull(bank));
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                Assert.Equal(source.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                ColdNext("following-action-completed-after-cleanup", current);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-completed-visible", current, new BattleScenePresentation(assets, current.Definition).Render(battle, 320, 224).ToArray());
                observer.Accept(current, output!);
                completed = true;
                break;
            }
            prior = current;
        }
        BattleSnapshot result = battle.CaptureForContinuation();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-last", result, null);
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Scenario = "Following declared original119 Amount0 action after effect323 cleanup; genuine current bones retained on entry", Effect = 323, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, RequirePlayback = requirePlayback, Registered = registered, CameraTrack = cameraTrack, CameraViewChanged = cameraViewChanged, WarmSourceRetained = warmRetained, ReturnedDescriptorEdge = returnedEdge, CleanupCallbacks = cleanupCallbacks, Completed = completed, Completions = completions, DistinctViewMatrices = views.Count, BankSides = bankSides.Order(), Callbacks = result.ReadFixturePresentation().NativeGraphics!.Callback - originalGraphics.Callback, result.Frame, NativeCallback = result.ReadFixturePresentation().NativeGraphics!.Callback }));
        observer.Publish(output!);
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
            BattleSnapshot json = NativeBattleFixtureInspection.ReadLogicalSnapshot(Path.Combine(output!, name + ".battle.json"));
            BattleSession cold = Restore(json);
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
        }
    }
}
