using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect227FollowingActionTests
{
    [Fact]
    public void Completed_component_restarts_no_contact_camera_texts_and_retained_banks_before_separate_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT227_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT227_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed effect227 standalone and a new evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        NativeBattleGraphicsState originalGraphics = source.NativeGraphics!;
        Assert.Equal(227, originalGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, originalGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        Assert.Empty(originalGraphics.OriginalEffect.Pools);
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.All(source.NativeEffectMemory.Effect227!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        string casterId = originalGraphics.ActorIds[3]!;
        string companionId = originalGraphics.ActorIds[4]!;
        BattleCombatantDefinition companion = source.Definition.Combatants.Single(actor => actor.Id == companionId);
        Assert.Equal(123, companion.ModelId);
        Assert.True(companion.InitialHidden);
        Assert.True(companion.InitialSuspended);
        Assert.True(companion.InitialUntargetable);
        BattleCombatantDefinition caster = source.Definition.Combatants.Single(actor => actor.Id == casterId);
        Assert.Equal(121, caster.ModelId);
        Assert.Equal((byte)137, originalGraphics.Models[3].ModelId4);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 339 && row.MagicId == 227 && row.KernelSection == 3 && row.KernelIndex == 339);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, int header) = packets.Prepare(new(3, 8, 13, cameraChoice, 339, 227, null));
        (packets, _) = packets.Publish(0);
        Assert.Equal(source.NativeActionPackets!.ContactCount, packets.ContactCount);
        (NativeBattleDescriptorState descriptors, _) = originalGraphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot prepared = source with { NativeActionPackets = packets, NativeGraphics = originalGraphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(source.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(originalGraphics.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(source.Combatants, prepared.Combatants);
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-before-entry", prepared, null);
        BattleSession battle = Restore(prepared);
        NativeBattleEffect227Resources resources = game.ReadNativeEffect227Resources();
        bool registered = false;
        bool cameraTrack = false;
        HashSet<int> textIndices = [];
        NativeBattleGraphicsResource originalText = source.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == casterId).Resource;
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
            views.Add(JsonSerializer.Serialize(current.NativeCamera!.CapturePackedView1D97778()));
            if (graphics.OriginalEffect is { Binding.Effect: 227, ReturnedDescriptor: > 0 } active)
            {
                NativeBattleEffect227State state = current.NativeEffectMemory!.Effect227!;
                uint arena = state.ReadDword(ArenaGlobal);
                uint cursor = state.ReadDword(CursorGlobal);
                if (cursor >= arena + FirstPacketOffset)
                {
                    bankSides.Add(state.ReadDword(SecondCursorGlobal) == arena + ThirdPacketOffset ? 1 : 0);
                }
                Assert.Contains(active.Pools, pool => pool.DescriptorAddress == RootDescriptor && pool.Head is not null);
                Assert.Null(state.FixedBanks[RootBank]);
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
                        ColdNext("following-action-camera-entry-current-banks", current);
                        registered = true;
                    }
                }
            }
            if (registered && prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 227, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: 227, ReturnedDescriptor: 0 })
            {
                completions++;
                returnedEdge = true;
                ColdNext("following-action-returned-descriptor-edge", current);
            }
            foreach ((int index, int duration) in new[] { (0, 42), (1, 30), (2, 30), (3, 15) })
            {
                if (current.Events.Any(message => message.Frame > source.Frame && message.Kind == "NativeText" && message.ActorId == casterId && message.Message == originalText.ReadText91(index) && message.Amount == duration && message.NativeMessageMode == 3))
                {
                    textIndices.Add(index);
                }
            }
            if (returnedEdge)
            {
                cleanupCallbacks++;
            }
            if (completions > 0 && graphics.OriginalEffect is { Binding.Effect: 227, ReturnedDescriptor: 0 } effect && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104))
            {
                Assert.Null(current.NativeCamera.ControlCursor);
                Assert.Empty(current.NativeCamera.Order);
                Assert.Empty(effect.Pools);
                Assert.All(current.NativeEffectMemory.Effect227!.FixedBanks.Values, bank => Assert.NotNull(bank));
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                Assert.Equal(source.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                BattleCombatantSnapshot currentCompanion = current.Combatants.Single(actor => actor.Id == companionId);
                Assert.True(currentCompanion.NativeParticipant!.Hidden);
                Assert.True(currentCompanion.NativeParticipant.Suspended);
                Assert.True(currentCompanion.NativeParticipant.Untargetable);
                Assert.Equal(4, graphics.Models[3].Flags0 & 4);
                ColdNext("following-action-completed-after-cleanup", current);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-completed-visible", current, new BattleScenePresentation(assets, current.Definition, true).Render(battle, 320, 224).ToArray());
                completed = true;
                break;
            }
            prior = current;
        }
        BattleSnapshot result = battle.Capture();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-last", result, null);
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Scenario = "Following declared original121 zero-contact action after effect227 cleanup; original123 companion and actual retained banks preserved", Effect = 227, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, Registered = registered, CameraTrack = cameraTrack, CameraViewChanged = cameraViewChanged, ReturnedDescriptorEdge = returnedEdge, CleanupCallbacks = cleanupCallbacks, Texts = textIndices.Order(), Completed = completed, Completions = completions, DistinctViewMatrices = views.Count, BankSides = bankSides.Order(), Callbacks = result.NativeGraphics!.Callback - originalGraphics.Callback, result.Frame, NativeCallback = result.NativeGraphics.Callback }));
        Assert.True(registered);
        Assert.True(cameraTrack);
        Assert.True(returnedEdge);
        Assert.True(completed);
        Assert.Equal(4, textIndices.Count);
        Assert.Equal(1, completions);
        Assert.True(views.Count > 1);
        Assert.True(cameraViewChanged);
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
