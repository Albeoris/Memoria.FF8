using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShellResourcesTests
{
    [Fact]
    public void Original_Shell_runs_all_actor_and_shield_queues_with_native_scale_context_and_restoration()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleShellResources resources = game.ReadNativeShellResources();
        Assert.Same(resources, game.ReadNativeShellResources());
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 33, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(33, 32, new(33, 0x866010, 0x865FF0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26C0380, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(32);
        Assert.Throws<NotSupportedException>(() => NativeBattleShellController.Initialize(memory with { Shell33 = null }, effect, packets, resources, new(1)));
        NativeBattleShellFrame frame = NativeBattleShellController.Initialize(memory, effect, packets, resources, new(1));
        Assert.False(frame.Memory.Shell33!.PendingColdActorBank);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources.ActorBank), NativeBattleSnapshotJson.Serialize(frame.Memory.Shell33.ActorBank));
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleGraphicsModel Model(int index) => new(index, 0) { Transform = new(new(0, 0, 0), new(0, -100, 0), 0, 0, 4096), Rotation14 = 0, GeometryBounds52 = new(new(0, -100, 0), new(0, 0, 0)), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };
        List<int> contacts = [];
        List<int> sounds = [];
        int allocations = 0;
        int now = 0;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleShellInputs inputs = new(packets, projection.View!, projection, 0, NativeBattleGteColorInterpolation.CodeConstants45F270, Model, _ => pose, (current, operation) =>
        {
            if (operation is NativeBattleShellContact)
            {
                contacts.Add(now);
                return current;
            }
            if (operation is NativeBattleShellSound)
            {
                sounds.Add(now);
                return current;
            }
            if (operation is NativeBattleShellStage)
            {
                return current;
            }
            NativeBattleShellPackets draw = Assert.IsType<NativeBattleShellPackets>(operation);
            NativeBattleShellState state = current.Memory.Shell33!;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, state.Cursor!.Value, ordering, draw.Batch);
            allocations += draw.Batch.AllocationBytes;
            ordering = written.Ordering;
            return current with { Memory = written.Memory with { Shell33 = state with { Cursor = written.Cursor } } };
        });
        for (now = 1; now <= 200 && frame.Effect.ReturnedDescriptor != 0; now++)
        {
            ordering = NativeBattleLogicalOrdering.Clear();
            frame = NativeBattleShellController.Advance(frame, resources, inputs);
            if (now == 4)
            {
                NativeBattleShellState state = frame.Memory.Shell33!;
                NativeBattleModulePoolTask actor = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(state.ContextTask);
                Assert.Equal(1024u, actor.ReadDword(0x34 + 0x214));
                Assert.Equal(1024u, actor.ReadDword(0x34 + 0x218));
                Assert.Equal(1024u, actor.ReadDword(0x34 + 0x21C));
                Assert.Equal((ushort)45, actor.ReadWord(0x34 + 0x1A));
            }
            if (now == 30)
            {
                string json = NativeBattleSnapshotJson.Serialize(frame);
                frame = NativeBattleSnapshotJson.Deserialize<NativeBattleShellFrame>(json)!;
                Assert.Equal(json, NativeBattleSnapshotJson.Serialize(frame));
            }
        }
        Assert.Equal(new[] { 35 }, contacts);
        Assert.Equal(new[] { 4 }, sounds);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.True(allocations > 0);
        Assert.All(frame.Memory.Shell33!.Channels, value => Assert.Equal((byte)0, value));
    }
}
