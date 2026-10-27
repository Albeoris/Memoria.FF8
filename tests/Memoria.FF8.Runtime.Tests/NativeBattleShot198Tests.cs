using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot198Tests
{
    [Fact]
    public void Original_phase10_contact_phase22_readiness_and_physical_beam_producer_survive_restore()
    {
        NativeBattleActionPacketsState packets = Packets();
        List<NativeBattleShot198Operation> operations = [];
        NativeBattleShot198Inputs inputs = Inputs(packets, operations);
        NativeBattleShot198Frame frame = NativeBattleShot198.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot198.Advance(frame, inputs, Resources());
        }
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        frame = NativeBattleShot198.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot198.Advance(frame, inputs, Resources());
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 77; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(new uint[] { 0xCF9834, 0xCF6394, 0xCF8D2C, 0xCFA52C }, operations.OfType<NativeBattleShot198Mesh>().Select(mesh => mesh.Address));
        Assert.All(operations.OfType<NativeBattleShot198Sprite>(), sprite => Assert.Equal((ushort)0, sprite.Flags));
        NativeBattleShot198Anchor anchor = Assert.IsType<NativeBattleShot198Anchor>(frame.Memory.Shot198!.Anchors[0]);
        Assert.Equal(1950, anchor.Rotation.X);
        Assert.Equal(142, anchor.Rotation.Z);
        Assert.Equal(8, operations.OfType<NativeBattleShot198Sprite>().Count(sprite => sprite.Address == 0xCF6200));
        for (int phase = 1; phase < 10; phase++)
        {
            frame = NativeBattleShot198.Advance(frame, inputs, Resources());
        }
        Assert.Empty(operations.OfType<NativeBattleShot198Contact>());
        frame = NativeBattleShot198.Advance(frame, inputs, Resources());
        Assert.Single(operations.OfType<NativeBattleShot198Contact>());
        expected = new(1);
        for (int draw = 0; draw < 954; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(3, operations.OfType<NativeBattleShot198Mesh>().Count(mesh => mesh.Address == 0xCFAE64 && mesh.Phase == 0));
        for (int phase = 11; phase < 22; phase++)
        {
            frame = NativeBattleShot198.Advance(frame, inputs, Resources());
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot198.Advance(frame, inputs, Resources());
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        NativeBattleShot198Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShot198Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleShot198.Advance(frame, inputs, Resources())), NativeBattleTestSnapshots.Json(NativeBattleShot198.Advance(restored, Inputs(packets, []), Resources())));
        frame = NativeBattleShot198.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot198.Advance(frame, inputs, Resources());
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, operations.OfType<NativeBattleShot198Contact>().Count());
    }

    [Fact]
    public void Paused_beam_writes_retained_matrix_radius_length_and_draws_both_meshes_without_random_or_motion()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot198Frame frame = NativeBattleShot198.Enter(Fixture(), packets);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x225DDB0, 0x225DDC0, 36, 100);
        (children, int beam) = children.Append(0x5AD7C0);
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(beam, children.Slots[beam].WriteWord(12, 0).WriteWord(14, 0).WriteWord(28, 13312).WriteWord(30, 332).WriteWord(32, 1000).WriteWord(34, 1000)) });
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 777, 0, 4096);
        frame = frame with { Memory = frame.Memory with { Shot198 = frame.Memory.Shot198! with { Anchors = frame.Memory.Shot198!.Anchors.SetItem(0, new(new(10, 20, 30), new(0, 0, -4096), identity)) } }, Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], NativeBattleEffectTaskPool.Initialize(0x225B778, 0x225A958, 36, 100), children] } };
        List<NativeBattleShot198Operation> operations = [];
        frame = NativeBattleShot198.Advance(frame, Inputs(packets, operations) with { GraphicsFlags = 0x201 }, Resources());
        Assert.Equal(new uint[] { 0xCF6394, 0xCF8D2C }, operations.OfType<NativeBattleShot198Mesh>().Select(mesh => mesh.Address));
        NativeBattleShot198Anchor anchor = Assert.IsType<NativeBattleShot198Anchor>(frame.Memory.Shot198!.Anchors[0]);
        Assert.Equal((1950, 777, 1000), (anchor.Rotation.X, anchor.Rotation.Y, anchor.Rotation.Z));
        Assert.Equal((ushort)0, frame.Effect.Pools[2].Slots[beam].ReadWord(12));
        Assert.Equal((ushort)13312, frame.Effect.Pools[2].Slots[beam].ReadWord(28));
        Assert.Equal(new NativeCrtRandomState(1), frame.Random);
    }

    [Fact]
    public void Entry_clears_only_both_marker_banks_and_preserves_particle_tails_binding_scratch_and_anchor_translation()
    {
        NativeBattleShot198State state = NativeBattleShot198State.FromColdProcess();
        byte[] bytes = Enumerable.Repeat((byte)0x5A, 9760).ToArray();
        NativeBattleShot198Anchor anchor = new(new(1, 2, 3), new(4, 5, 6), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 42, 43, 44, 4096));
        state = (state with { ParticleStorage = bytes.ToImmutableArray(), Anchors = state.Anchors.SetItem(0, anchor) }).Enter(2);
        foreach (int bank in new[] { 0, 4832 })
        {
            for (int slot = 0; slot < 200; slot++)
            {
                Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(state.ParticleStorage.AsSpan().Slice(bank + slot * 24)));
                Assert.All(state.ParticleStorage.Skip(bank + slot * 24 + 4).Take(20), value => Assert.Equal((byte)0x5A, value));
            }
        }
        Assert.All(state.ParticleStorage.Skip(4800).Take(32), value => Assert.Equal((byte)0x5A, value));
        Assert.All(state.ParticleStorage.Skip(9632), value => Assert.Equal((byte)0x5A, value));
        Assert.Equal(anchor, state.Anchors[0]);
    }

    [Theory]
    [InlineData(0, 0, 4800)]
    [InlineData(1, 4832, 9632)]
    public void Paired_bone_particles_keep_the_unchecked_second_physical_slot_store(int serial, int bank, int overflow)
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot198Frame frame = NativeBattleShot198.Enter(Fixture(), packets);
        byte[] bytes = frame.Memory.Shot198!.ParticleStorage.ToArray();
        for (int slot = 0; slot < 199; slot++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bank + slot * 24), 999);
        }
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(0x225DDB0, 0x225DDC0, 36, 100);
        (particles, int task) = particles.Append(0x5AD480);
        particles = NativeBattleTaskFixture.EditPool(particles, data => data with { Slots = particles.Slots.SetItem(task, particles.Slots[task].WriteWord(12, 0).WriteWord(14, (ushort)(6 + serial))) });
        frame = frame with { Memory = frame.Memory with { Shot198 = frame.Memory.Shot198! with { ParticleStorage = bytes.ToImmutableArray() } }, Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], NativeBattleEffectTaskPool.Initialize(0x225B778, 0x225A958, 36, 100), particles] } };
        List<int> bones = [];
        NativeBattleShot198Inputs inputs = Inputs(packets, []) with { ReadBone = (_, bone, _) => { bones.Add(bone); return new(100, 200, 300); } };
        frame = NativeBattleShot198.Advance(frame, inputs, Resources());
        Assert.Equal(new[] { 29, 30 }, bones);
        Assert.Equal(7 + serial, BinaryPrimitives.ReadInt32LittleEndian(frame.Memory.Shot198!.ParticleStorage.AsSpan().Slice(bank + 199 * 24)));
        Assert.Equal((short)893, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot198!.ParticleStorage.AsSpan().Slice(bank + 199 * 24 + 6)));
        Assert.Equal(7 + serial, BinaryPrimitives.ReadInt32LittleEndian(frame.Memory.Shot198!.ParticleStorage.AsSpan().Slice(overflow)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot198!.ParticleStorage.AsSpan().Slice(overflow + 4)));
        Assert.Equal((short)1409, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot198!.ParticleStorage.AsSpan().Slice(overflow + 6)));
    }


    private static NativeBattleShot198Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot198 = NativeBattleShot198State.FromColdProcess() }, new(198, 197, new(198, 0x5AA3E0, 0x5AA3C0), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 2, -1, 0, 198, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot198Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot198Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), new(0, 0, -4096), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => -400, _ => new(100, 200, 300), _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleShot198Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xCF5E44, 0xCF6054, 0xCF6200 }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096), new(0, 0, 4096), new(0, -4096, 0)], [], 123);
    }
}
