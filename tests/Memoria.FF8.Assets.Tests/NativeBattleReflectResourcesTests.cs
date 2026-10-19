using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleReflectResourcesTests
{
    [Theory]
    [InlineData(1, 41, 240, 277)]
    [InlineData(3, 121, 256, 367)]
    public void Original_Reflect_completes_walls_hexagons_rings_and_preserves_unlinked_beam_RNG(ushort actions, int expectedTicks, int orphanCount, int randomDraws)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleReflectResources resources = game.ReadNativeReflectResources();
        Assert.Same(resources, game.ReadNativeReflectResources());
        Assert.Equal(0x12F9B80u, resources.Wall.Address);
        Assert.Equal(0x12FBAA0u, resources.Hexagon.Address);
        Assert.Equal(0x12FBB78u, resources.Ring.Address);
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int action = 0; action < actions; action++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 106, null));
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, checked((byte)actions));
        NativeBattleOriginalEffectState effect = new(106, 105, new(106, 0x6D2AE0, 0x6D2AC0), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleReflectFrame frame = NativeBattleReflectController.Initialize(NativeBattleEffectMemory.FromColdProcess(), effect, resources, 1);
        NativeBattleGraphicsModel model = new(3, 1) { Transform = new(new(0, 0, 0), new(0, -100, 0), 0, 0, 4096), Rotation14 = 0 };
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        int allocations = 0;
        List<int> contactTicks = [];
        List<int> soundTicks = [];
        NativeBattleReflectInputs inputs = new(packets, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, _ => model, _ => new([], []), (current, batch) =>
        {
            NativeBattleReflectState state = current.Memory.Reflect106!;
            NativeBattleRegionalPacketWrite write = NativeBattleRegionalPacketArena.Append(state.Packets, "2523C98", state.Cursor!.Value, 0x20000, ordering, batch);
            ordering = write.Ordering;
            allocations += batch.AllocationBytes;
            return current with { Memory = current.Memory with { Reflect106 = state with { Cursor = write.Cursor, Packets = write.Arena } } };
        });
        for (int tick = 1; tick <= expectedTicks; tick++)
        {
            ordering = NativeBattleLogicalOrdering.Clear();
            NativeBattleReflectStep step = NativeBattleReflectController.Advance(frame, resources, inputs);
            frame = step.Frame;
            contactTicks.AddRange(step.Contacts.Select(_ => tick));
            soundTicks.AddRange(step.Sounds.Select(_ => tick));
            Assert.Null(frame.Effect.Pools[2].Head);
            Assert.Null(frame.Effect.Pools[2].Tail);
            Assert.All(ordering.Proxies, proxy => Assert.Equal("2523C98", proxy.Packet.Bank));
            if (tick == 21)
            {
                string json = JsonSerializer.Serialize(frame);
                frame = JsonSerializer.Deserialize<NativeBattleReflectFrame>(json)!;
                Assert.Equal(json, JsonSerializer.Serialize(frame));
            }
            if (tick < expectedTicks)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
        }
        Assert.Equal(Enumerable.Range(0, actions).Select(index => 36 + index * 40), contactTicks);
        Assert.Equal(Enumerable.Range(0, actions).Select(index => 1 + index * 40), soundTicks);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(orphanCount, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.True(allocations > 0);
        NativeBattleSharedEffectRandomState random = new(1);
        for (int index = 0; index < randomDraws; index++)
        {
            random = random.Draw7059E0(out _);
        }
        Assert.Equal(random, frame.Memory.SharedEffectRandom);
        Assert.Same(frame, NativeBattleReflectController.Advance(frame, resources, inputs).Frame);
    }
}
