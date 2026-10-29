using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleProtectResourcesTests
{
    [Fact]
    public void Original_Protect_runs_complete_shield_ninety_particles_exact_contact_and_parent_drain()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleProtectResources resources = game.ReadNativeProtectResources();
        Assert.Same(resources, game.ReadNativeProtectResources());
        int table = BinaryPrimitives.ReadInt32LittleEndian(resources.Shield.Data.AsSpan().Slice(4));
        Assert.Equal(19, BinaryPrimitives.ReadInt32LittleEndian(resources.Shield.Data.AsSpan().Slice(table)));
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(resources.Shield.Data.AsSpan().Slice(table + 4)));
        Assert.Equal(new uint[] { 0x3F3F3F, 0x0F0F0F, 0xFF000000 }, resources.FlashScript);
        Assert.Equal(90, resources.ParticleCounts.AsSpan().Slice(21, 5).ToArray().Sum(value => value));
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 32, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(32, 31, new(32, 0x86B9A0, 0x86B980), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26C3954, 0)] };
        NativeBattleProtectFrame frame = NativeBattleProtectController.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(31), effect, packets, resources, new(1));
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleGraphicsModel Model(int index) => new(index, 0) { Transform = new(new(0, 0, 0), new(0, -100, 0), 0, 0, 4096), Rotation14 = 0, SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };
        List<int> contacts = [];
        List<int> sounds = [];
        List<int> flashes = [];
        int sprites = 0;
        int now = 0;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleProtectInputs inputs = new(packets, projection.View!, projection, 0, NativeBattleGteColorInterpolation.CodeConstants45F270, Model, _ => pose, (current, operation) =>
        {
            if (operation is NativeBattleProtectContact)
            {
                contacts.Add(now);
                return current;
            }
            if (operation is NativeBattleProtectSound)
            {
                sounds.Add(now);
                return current;
            }
            if (operation is NativeBattleProtectStage)
            {
                return current;
            }
            NativeBattleProtectState state = current.Memory.Protect32!;
            NativeBattleEffectPacketWrite written;
            if (operation is NativeBattleProtectSprite sprite)
            {
                sprites++;
                written = NativeBattleEffectPacketArena.Append(current.Memory, state.Cursor!.Value, ordering, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, 0, 17, 2), NativeBattleEffectGouraudWrites.RgbBytes);
            }
            else
            {
                NativeBattleProtectPackets draw = Assert.IsType<NativeBattleProtectPackets>(operation);
                if (draw.Flags20 == 1)
                {
                    flashes.Add(now);
                }
                written = NativeBattleEffectPacketArena.Append(current.Memory, state.Cursor!.Value, ordering, draw.Batch, draw.Flags20);
            }
            ordering = written.Ordering;
            return current with { Memory = written.Memory with { Protect32 = state with { Cursor = written.Cursor } } };
        });
        for (now = 1; now <= 49; now++)
        {
            ordering = NativeBattleLogicalOrdering.Clear();
            frame = NativeBattleProtectController.Advance(frame, resources, inputs);
            if (now == 30)
            {
                string json = NativeBattleSnapshotJson.Serialize(frame);
                frame = NativeBattleSnapshotJson.Deserialize<NativeBattleProtectFrame>(json)!;
                Assert.Equal(json, NativeBattleSnapshotJson.Serialize(frame));
            }
            if (now < 49)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
        }
        Assert.Equal(new[] { 23 }, contacts);
        Assert.Equal(new[] { 4 }, sounds);
        Assert.Equal(new[] { 26, 27 }, flashes);
        Assert.Equal(90 * 12, sprites);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        NativeCrtRandomState random = new(1);
        for (int draw = 0; draw < 180; draw++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, frame.Random);
    }
}
