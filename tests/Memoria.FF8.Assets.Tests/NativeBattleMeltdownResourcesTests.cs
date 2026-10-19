using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMeltdownResourcesTests
{
    [Fact]
    public void Original_Meltdown_runs_both_acid_pools_uv_scrolling_glows_melt_and_action_result()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleMeltdownResources resources = game.ReadNativeMeltdownResources();
        Assert.Same(resources, game.ReadNativeMeltdownResources());
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 148, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(148, 147, new(148, 0x6120D0, 0x6120B0), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(147);
        Assert.Throws<NotSupportedException>(() => NativeBattleMeltdownController.Initialize(memory with { Meltdown148 = null }, effect, packets, resources, new(1)));
        NativeBattleMeltdownFrame frame = NativeBattleMeltdownController.Initialize(memory, effect, packets, resources, new(1));
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 8192, 4096), 216, 160, 108);
        NativeBattlePoseSource pose = new([], []);
        ushort flags = 0;
        NativeBattleGraphicsModel Model(int index) => new(index, index == 3 ? flags : (ushort)0) { Transform = new(new(0, 0, 0), index == 3 ? new(0, -100, 0) : new(0, -500, -1000), 0, 0, 4096) };
        List<int> contacts = [];
        List<int> sounds = [];
        List<int> fades = [];
        List<int> drawn = [];
        int allocations = 0;
        int now = 0;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleMeltdownInputs inputs = new(packets, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, false, Model, _ => pose, (current, operation) =>
        {
            switch (operation)
            {
                case NativeBattleMeltdownContact:
                    contacts.Add(now);
                    return current;
                case NativeBattleMeltdownSound:
                    sounds.Add(now);
                    return current;
                case NativeBattleMeltdownFade:
                    fades.Add(now);
                    return current;
                case NativeBattleMeltdownActor actor:
                    flags |= 0xC;
                    if (actor.Draw)
                    {
                        drawn.Add(now);
                    }
                    return current;
                case NativeBattleMeltdownFlags restored:
                    flags = restored.Flags;
                    return current;
            }
            NativeBattleGfGeometryBatch batch = operation is NativeBattleMeltdownSprite sprite ? NativeBattleMeltdownPacketConversion.FromSprite(sprite.Batch) : Assert.IsType<NativeBattleMeltdownPackets>(operation).Batch;
            NativeBattleMeltdownState state = current.Memory.Meltdown148!;
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "2466638", state.Cursor!.Value, 0x10000, ordering, batch);
            allocations += batch.AllocationBytes;
            ordering = written.Ordering;
            return current with { Memory = current.Memory with { Meltdown148 = state with { Packets = written.Arena, Cursor = written.Cursor } } };
        });
        for (now = 1; now <= 200 && frame.Effect.ReturnedDescriptor != 0; now++)
        {
            ordering = NativeBattleLogicalOrdering.Clear();
            frame = NativeBattleMeltdownController.Advance(frame, resources, inputs);
            if (now == 56)
            {
                Assert.Equal((ushort)12, flags);
                string json = JsonSerializer.Serialize(frame);
                frame = JsonSerializer.Deserialize<NativeBattleMeltdownFrame>(json)!;
                Assert.Equal(json, JsonSerializer.Serialize(frame));
            }
        }
        Assert.Equal(92, now);
        Assert.Equal(new[] { 81 }, contacts);
        Assert.Equal(new[] { 2 }, sounds);
        Assert.Equal(new[] { 36 }, fades);
        Assert.Equal(Enumerable.Range(41, 34), drawn);
        Assert.Equal((ushort)0, flags);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 798; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.True(allocations > 0);
    }
}
