using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        NativeBattleGraphicsState originalGraphics = source.ReadFixturePresentation().NativeGraphics!;
        Assert.Equal(210, originalGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, originalGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal(0, source.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Empty(originalGraphics.OriginalEffect.Pools);
        Assert.Null(source.ReadFixturePresentation().NativeCamera!.ControlCursor);
        Assert.Empty(source.ReadFixturePresentation().NativeCamera!.Order);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.All(source.ReadFixturePresentation().NativeEffectMemory!.Effect210!.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        string casterId = ReadOriginalModelActorId(originalGraphics, 3)!;
        string companionId = ReadOriginalModelActorId(originalGraphics, 4)!;
        string targetId = ReadOriginalModelActorId(originalGraphics, 0)!;
        BattleCombatantDefinition caster = source.Definition.Combatants.Single(actor => actor.Id == casterId);
        BattleCombatantDefinition companion = source.Definition.Combatants.Single(actor => actor.Id == companionId);
        Assert.Equal(128, caster.ModelId);
        Assert.Equal(82, companion.ModelId);
        Assert.True(companion.InitialSuspended);
        Assert.True(companion.InitialUntargetable);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 315 && row.MagicId == 210 && row.KernelSection == 3 && row.KernelIndex == 315);
        Assert.Equal((byte)54, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.Id == targetId).NativeStatus!;
        NativeBattleActionPacketsState packets = source.ReadFixturePresentation().NativeActionPackets!;
        sbyte cameraChoice = unchecked((sbyte)action.RawRecord[4]);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(3), 8, 54, cameraChoice, 315, 210, null));
        (packets, _) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(0), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = originalGraphics.Descriptors.EnqueueAction(128, header);
        BattleSnapshot prepared = (source.WithFixturePresentation(facts => facts with { NativeActionPackets = packets })).WithFixturePresentation(facts => facts with { NativeGraphics = originalGraphics with { Descriptors = descriptors } });
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeEffectMemory), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(originalGraphics)), JsonSerializer.Serialize(ReadOriginalModelCells(prepared.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(source.Combatants, prepared.Combatants);
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-before-entry", prepared, null);
        BattleSession battle = Restore(prepared);
        bool registered = false;
        bool completed = false;
        bool cameraViewChanged = false;
        NativeBattleEffect210Resources resources = game.ReadNativeEffect210Resources();
        HashSet<string> views = [];
        HashSet<int> bankSides = [];
        int completions = 0;
        BattleSnapshot prior = prepared;
        int callbacks = 0;
        for (; callbacks < 2400; callbacks++)
        {
            battle.Tick();
            BattleSnapshot current = battle.CaptureForContinuation();
            NativeBattleGraphicsState graphics = current.ReadFixturePresentation().NativeGraphics!;
            views.Add(JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera!.CapturePackedView1D97778()));
            if (graphics.OriginalEffect is { Binding.Effect: 210, ReturnedDescriptor: > 0 } active)
            {
                NativeBattleEffect210State state = current.ReadFixturePresentation().NativeEffectMemory!.Effect210!;
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
                bool runningCamera = current.ReadFixturePresentation().NativeCamera!.ControlResource?.Source.Identity == resources.Camera.Identity && current.ReadFixturePresentation().NativeCamera!.Variables[0] == 1 && current.ReadFixturePresentation().NativeCamera!.Order.Any(slot => current.ReadFixturePresentation().NativeCamera!.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
                if (runningCamera)
                {
                    cameraViewChanged |= JsonSerializer.Serialize(prior.ReadFixturePresentation().NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera!.CapturePackedView1D97778());
                }
                if (!registered && runningCamera)
                {
                    NativeBattleEffectAnimationRegisters control = current.ReadFixturePresentation().NativeCamera!.ControlResource!;
                    Assert.Equal(8, control.StreamOffset);
                    Assert.Equal(24, control.ModelTableOffset);
                    Assert.Equal(game.ReadNativeEffect210Resources().Camera.Data.ToArray(), control.Source.Data.ToArray());
                    Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.ReadFixturePresentation().NativeEffectMemory!.Textures!.Animation));
                    Assert.NotEmpty(current.ReadFixturePresentation().NativeCamera!.Order);
                    ColdNext("following-action-camera-entry", current);
                    registered = true;
                }
            }
            if (registered && prior.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 210, ReturnedDescriptor: > 0 } && graphics.OriginalEffect is { Binding.Effect: 210, ReturnedDescriptor: 0 })
            {
                completions++;
            }
            if (completions > 0 && graphics.OriginalEffect is { Binding.Effect: 210, ReturnedDescriptor: 0 } && current.ReadFixturePresentation().NativeEffectMemory!.Active == 0)
            {
                Assert.Null(current.ReadFixturePresentation().NativeCamera!.ControlCursor);
                Assert.Empty(current.ReadFixturePresentation().NativeCamera!.Order);
                Assert.Null(current.ActiveAction);
                Assert.Empty(current.QueuedActions);
                Assert.Empty(graphics.OriginalEffect.Pools);
                Assert.All(current.ReadFixturePresentation().NativeEffectMemory!.Effect210!.FixedBanks.Values, bank => Assert.NotNull(bank));
                Assert.All(current.ReadFixturePresentation().NativeEffectMemory!.Effect210!.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                Assert.Equal(source.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
                BattleCombatantSnapshot currentCompanion = current.Combatants.Single(actor => actor.Id == companionId);
                Assert.True(currentCompanion.NativeParticipant!.Suspended);
                Assert.True(currentCompanion.NativeParticipant.Untargetable);
                ColdNext("following-action-completed", current);
                NativeBattleRestorationPresentationTests.Publish(output!, "following-action-completed-visible", current, new BattleScenePresentation(assets, current.Definition).Render(battle, 320, 224).ToArray());
                completed = true;
                break;
            }
            prior = current;
        }
        BattleSnapshot result = battle.CaptureForContinuation();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-action-last", result, null);
        NativeBattleGraphicsState lastGraphics = result.ReadFixturePresentation().NativeGraphics!;
        var lastEffect = lastGraphics.OriginalEffect;
        File.WriteAllText(Path.Combine(output!, "following-action-last-state.json"), JsonSerializer.Serialize(new
        {
            result.Frame,
            Callbacks = callbacks,
            lastGraphics.Callback,
            lastGraphics.Flags,
            Registered = registered,
            Completed = completed,
            Completions = completions,
            HeaderIndex = header.Ordinal,
            InitialActionCount = source.ReadFixturePresentation().NativeActionPackets!.ActionCount,
            InitialContactCount = source.ReadFixturePresentation().NativeActionPackets!.ContactCount,
            Packets = new { result.ReadFixturePresentation().NativeActionPackets!.ActionCount, result.ReadFixturePresentation().NativeActionPackets!.ContactCount, result.ReadFixturePresentation().NativeActionPackets!.ActiveHeader, PreparedHeader = result.ReadFixturePresentation().NativeActionPackets!.ReadHeader(header) },
            lastGraphics.ActionGraphics,
            DescriptorQueue = lastGraphics.Descriptors.Queue,
            Effect = lastEffect is null ? null : new
            {
                lastEffect.Binding.Effect,
                lastEffect.ReturnedDescriptor,
                Pools = lastEffect.Pools.Select(pool => new
                {
                    pool.DescriptorAddress,
                    pool.BankAddress,
                    pool.Stride,
                    pool.Head,
                    pool.Tail,
                    CallbackCounts = pool.Slots.Where(task => task.Callback != 0).GroupBy(task => task.Callback).Select(group => new { Callback = group.Key, Count = group.Count() }),
                    Tasks = pool.Slots.Select((task, index) => new { Task = task, Index = index }).Where(item => item.Task.Callback != 0).Take(12).Select(item => new { item.Index, item.Task.Flags, item.Task.State, item.Task.Next, item.Task.Callback, PayloadPrefix = Convert.ToHexString(item.Task.Payload.AsSpan()[..Math.Min(48, item.Task.Payload.Length)]), item.Task.UnknownPayloadRanges })
                })
            },
            ActiveEffect = result.ReadFixturePresentation().NativeEffectMemory!.Active,
            result.ReadFixturePresentation().NativeEffectMemory!.EffectIo,
            Camera = new { result.ReadFixturePresentation().NativeCamera!.ControlCursor, result.ReadFixturePresentation().NativeCamera!.Variables, result.ReadFixturePresentation().NativeCamera!.Order, Tasks = result.ReadFixturePresentation().NativeCamera!.Tasks.Select(task => task is null ? null : new { task.Channel, task.ProgramOffset, task.Elapsed, task.Duration }) },
            CameraViewChanged = cameraViewChanged,
            DistinctViewMatrices = views.Count,
            BankSides = bankSides.Order()
        }));
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Scenario = "Following declared original128 Amount0 action after effect210 cleanup; original82 companion, warm owners and resources preserved", Effect = 210, CameraChoice = cameraChoice, CameraSelector = action.RawRecord[4] & 127, CameraViewChanged = cameraViewChanged, Registered = registered, Completed = completed, Completions = completions, DistinctViewMatrices = views.Count, BankSides = bankSides.Order(), Callbacks = result.ReadFixturePresentation().NativeGraphics!.Callback - originalGraphics.Callback, result.Frame, NativeCallback = result.ReadFixturePresentation().NativeGraphics!.Callback }));
        Assert.True(registered);
        Assert.True(completed);
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
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
        }
    }
}
