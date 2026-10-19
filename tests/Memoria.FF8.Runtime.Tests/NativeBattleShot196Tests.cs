using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot196Tests
{
    [Fact]
    public void Adjacent_Quick_shots_keep_independent_particle_banks_and_original_channel_and_animation_rotation()
    {
        List<NativeBattleShot196Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot196Resources resources = Resources();
        NativeBattleShot196Frame frame = NativeBattleShot196.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot196.Advance(frame, Inputs(packets, operations), resources);
        }
        for (int shot = 0; shot < 2; shot++)
        {
            frame = NativeBattleShot196.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
            frame = NativeBattleShot196.Advance(frame, Inputs(packets, operations), resources);
            Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        }
        Assert.Equal(2, operations.OfType<NativeBattleShot196Contact>().Count());
        Assert.Equal(new[] { 2, 1 }, operations.OfType<NativeBattleShot196Sound>().Select(sound => sound.Channel));
        Assert.Equal(new ushort[] { 1, 3, 4 }, operations.OfType<NativeBattleShot196Sequence>().Select(sequence => sequence.Sequence));
        Assert.Equal(2, frame.Memory.Shot196!.ImpactSerial);
        for (int bank = 0; bank < 2; bank++)
        {
            Assert.Equal(12, Enumerable.Range(0, 150).Count(index => BinaryPrimitives.ReadInt32LittleEndian(frame.Memory.Shot196.ImpactParticles.AsSpan().Slice((bank * 150 + index) * 24, 4)) == bank + 1));
        }
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 206; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
    }

    [Fact]
    public void Original_opening_repeated_contact_and_ending_drain_all_three_native_pools()
    {
        List<NativeBattleShot196Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot196Frame frame = NativeBattleShot196.Enter(Fixture(), packets);
        NativeBattleShot196Inputs inputs = Inputs(packets, operations);
        NativeBattleShot196Resources resources = Resources();
        for (int callback = 0; callback < 6; callback++)
        {
            frame = NativeBattleShot196.Advance(frame, inputs, resources);
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot196.Advance(frame, inputs, resources);
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.Contains(operations, operation => operation is NativeBattleShot196Bind { Actor: 0 });
        Assert.Contains(operations, operation => operation is NativeBattleShot196Sequence { Sequence: 1 });
        frame = NativeBattleShot196.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot196.Advance(frame, inputs, resources);
        Assert.Single(operations.OfType<NativeBattleShot196Contact>());
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.Equal(0, frame.Memory.Shot196!.Control & 4);
        Assert.Single(operations.OfType<NativeBattleShot196Sound>());
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 103; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        frame = NativeBattleShot196.Advance(frame, inputs, resources);
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        frame = NativeBattleShot196.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot196.Advance(frame, inputs, resources);
        Assert.Equal(2, operations.OfType<NativeBattleShot196Contact>().Count());
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot196.Advance(frame, inputs, resources);
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, operations.OfType<NativeBattleShot196Contact>().Count());
        Assert.Contains(operations, operation => operation is NativeBattleShot196Sequence { Sequence: 2 });
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(3, frame.Memory.Shot196!.Control);
    }

    [Fact]
    public void Mid_shot_standalone_restoration_preserves_particle_rng_and_the_entire_remaining_callback_stream()
    {
        List<NativeBattleShot196Operation> initial = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot196Resources resources = Resources();
        NativeBattleShot196Frame frame = NativeBattleShot196.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot196.Advance(frame, Inputs(packets, initial), resources);
        }
        frame = NativeBattleShot196.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot196.Advance(frame, Inputs(packets, initial), resources);
        frame = NativeBattleShot196.Advance(frame, Inputs(packets, initial), resources);
        NativeBattleShot196Frame restored = JsonSerializer.Deserialize<NativeBattleShot196Frame>(JsonSerializer.Serialize(frame))!;
        for (int callback = 0; callback < 20; callback++)
        {
            List<NativeBattleShot196Operation> first = [];
            List<NativeBattleShot196Operation> second = [];
            frame = NativeBattleShot196.Advance(frame, Inputs(packets, first), resources);
            restored = NativeBattleShot196.Advance(restored, Inputs(packets, second), resources);
            Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
            Assert.Equal(first.Select(operation => JsonSerializer.Serialize(operation, operation.GetType())), second.Select(operation => JsonSerializer.Serialize(operation, operation.GetType())));
        }
    }

    [Fact]
    public void Native_graphics_pause_keeps_contact_rng_and_all_task_clocks_while_still_publishing_held_draws()
    {
        List<NativeBattleShot196Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot196Resources resources = Resources();
        NativeBattleShot196Frame frame = NativeBattleShot196.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot196.Advance(frame, Inputs(packets, operations), resources);
        }
        frame = NativeBattleShot196.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot196.Advance(frame, Inputs(packets, operations), resources);
        operations.Clear();
        NativeBattleShot196Frame paused = NativeBattleShot196.Advance(frame, Inputs(packets, operations) with { GraphicsFlags = 0x200 }, resources);
        Assert.Equal(frame.Random, paused.Random);
        Assert.Equal(frame.Memory.Shot196!.Clock, paused.Memory.Shot196!.Clock);
        Assert.Equal(frame.Effect.Pools[1].Slots[0].ReadWord(12), paused.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Empty(operations.OfType<NativeBattleShot196Contact>());
        Assert.NotEmpty(operations.OfType<NativeBattleShot196Mesh>());
    }

    [Fact]
    public void Reopening_requires_retained_globals_and_clears_only_original_particle_markers()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot196Frame cold = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleShot196.Enter(cold with { Memory = cold.Memory with { Shot196 = null } }, packets));
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 7200).ToArray();
        NativeBattleShot196State before = cold.Memory.Shot196! with { ImpactParticles = bytes.ToImmutableArray(), Clock = 89, StopClock = 48, ShotSerial = 99, ImpactSerial = 98 };
        NativeBattleShot196Frame entered = NativeBattleShot196.Enter(cold with { Memory = cold.Memory with { Shot196 = before } }, packets);
        NativeBattleShot196State after = entered.Memory.Shot196!;
        Assert.Equal(48, after.StopClock);
        Assert.Equal(0, after.Clock);
        Assert.Equal(0, after.ShotSerial);
        Assert.Equal(0, after.ImpactSerial);
        Assert.Equal(before.Anchors, after.Anchors);
        Assert.Equal(before.Echoes, after.Echoes);
        for (int offset = 0; offset < bytes.Length; offset++)
        {
            Assert.Equal(offset % 24 < 4 ? (byte)0 : (byte)0xCC, after.ImpactParticles[offset]);
        }
    }
    private static NativeBattleShot196Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot196 = NativeBattleShot196State.FromColdProcess() }, new(196, 195, new(196, 0x5B1900, 0x5B18E0), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 5, -1, 0, 196, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot196Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot196Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleShot196Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xD05DB0, 0xD05FC0 }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096)], [], 123);
    }
}
