using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAuraResourcesTests
{
    [Fact]
    public void Original_Aura_runs_target_flames_glow_contact_and_restores_all_retained_bytes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleAuraResources resources = game.ReadNativeAuraResources();
        Assert.Same(resources, game.ReadNativeAuraResources());
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 111, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(111, 110, new(111, 0x6CD920, 0x6CD930), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleAuraFrame frame = NativeBattleAuraController.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(110), effect, resources, new(1));
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        NativeBattlePoseSource pose = new([], []);
        NativeBattleGraphicsModel Model(int index) => new(index, 0) { Transform = new(new(0, 0, 0), new(0, -100, 0), 0, 0, 4096) };
        List<int> contacts = [];
        List<int> sounds = [];
        List<int> fades = [];
        List<int> glows = [];
        int allocations = 0;
        int now = 0;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleAuraInputs inputs = new(packets, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, Model, _ => pose, (current, operation) =>
        {
            switch (operation)
            {
                case NativeBattleAuraContact:
                    contacts.Add(now);
                    return current;
                case NativeBattleAuraSound:
                    sounds.Add(now);
                    return current;
                case NativeBattleAuraFade:
                    fades.Add(now);
                    return current;
            }
            NativeBattleEffectPacketWrite written;
            if (operation is NativeBattleAuraSprite sprite)
            {
                glows.Add(now);
                NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, 0, 17, 2);
                written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, ordering, batch, NativeBattleEffectGouraudWrites.RgbBytes);
                allocations += batch.AllocationBytes;
            }
            else
            {
                NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleAuraPackets>(operation).Batch;
                written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, ordering, batch);
                allocations += batch.AllocationBytes;
            }
            ordering = written.Ordering;
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        });
        for (now = 1; now <= 100 && frame.Effect.ReturnedDescriptor != 0; now++)
        {
            ordering = NativeBattleLogicalOrdering.Clear();
            frame = NativeBattleAuraController.Advance(frame, resources, inputs);
            if (now == 20)
            {
                Assert.Equal(16, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
                string json = NativeBattleSnapshotJson.Serialize(frame);
                frame = NativeBattleSnapshotJson.Deserialize<NativeBattleAuraFrame>(json)!;
                Assert.Equal(json, NativeBattleSnapshotJson.Serialize(frame));
            }
        }
        Assert.Equal(41, now);
        Assert.Equal(new[] { 31 }, contacts);
        Assert.Equal(new[] { 2 }, sounds);
        Assert.Equal(new[] { 30 }, fades);
        Assert.Equal(new[] { 30, 31, 32, 33, 34, 35 }, glows);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 44; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.True(allocations > 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleAuraResourcesReader.Read(new byte[128]));
    }
}
