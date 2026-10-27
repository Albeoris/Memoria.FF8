using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot188Tests
{
    [Fact]
    public void Original_opening_repeated_contact_and_ending_drain_all_three_native_pools()
    {
        List<NativeBattleShot188Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot188Frame frame = NativeBattleShot188.Enter(Fixture(), packets);
        NativeBattleShot188Inputs inputs = Inputs(packets, operations);
        NativeBattleShot188Resources resources = Resources();
        for (int callback = 0; callback < 6; callback++)
        {
            frame = NativeBattleShot188.Advance(frame, inputs, resources);
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot188.Advance(frame, inputs, resources);
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.Contains(operations, operation => operation is NativeBattleShot188Bind { Actor: 0 });
        Assert.Contains(operations, operation => operation is NativeBattleShot188Sequence { Sequence: 1 });
        frame = NativeBattleShot188.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot188.Advance(frame, inputs, resources);
        Assert.Empty(operations.OfType<NativeBattleShot188Contact>());
        frame = NativeBattleShot188.Advance(frame, inputs, resources);
        Assert.Single(operations.OfType<NativeBattleShot188Contact>());
        Assert.Single(operations.OfType<NativeBattleShot188Sound>());
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 136; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot188.Advance(frame, inputs, resources);
        }
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        frame = NativeBattleShot188.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot188.Advance(frame, inputs, resources);
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, operations.OfType<NativeBattleShot188Contact>().Count());
        Assert.Contains(operations, operation => operation is NativeBattleShot188Sequence { Sequence: 2 });
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(3, frame.Memory.Shot188!.Control);
    }

    [Fact]
    public void Mid_shot_standalone_restoration_preserves_particle_rng_and_the_entire_remaining_callback_stream()
    {
        List<NativeBattleShot188Operation> initial = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot188Resources resources = Resources();
        NativeBattleShot188Frame frame = NativeBattleShot188.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot188.Advance(frame, Inputs(packets, initial), resources);
        }
        frame = NativeBattleShot188.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot188.Advance(frame, Inputs(packets, initial), resources);
        frame = NativeBattleShot188.Advance(frame, Inputs(packets, initial), resources);
        NativeBattleShot188Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShot188Frame>(NativeBattleTestSnapshots.Json(frame))!;
        for (int callback = 0; callback < 20; callback++)
        {
            List<NativeBattleShot188Operation> first = [];
            List<NativeBattleShot188Operation> second = [];
            frame = NativeBattleShot188.Advance(frame, Inputs(packets, first), resources);
            restored = NativeBattleShot188.Advance(restored, Inputs(packets, second), resources);
            Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(restored));
            Assert.Equal(first.Select(operation => NativeBattleTestSnapshots.Json(operation, operation.GetType())), second.Select(operation => NativeBattleTestSnapshots.Json(operation, operation.GetType())));
        }
    }

    [Fact]
    public void Native_graphics_pause_keeps_contact_rng_and_all_task_clocks_while_still_publishing_held_draws()
    {
        List<NativeBattleShot188Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot188Resources resources = Resources();
        NativeBattleShot188Frame frame = NativeBattleShot188.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot188.Advance(frame, Inputs(packets, operations), resources);
        }
        frame = NativeBattleShot188.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot188.Advance(frame, Inputs(packets, operations), resources);
        operations.Clear();
        NativeBattleShot188Frame paused = NativeBattleShot188.Advance(frame, Inputs(packets, operations) with { GraphicsFlags = 0x200 }, resources);
        Assert.Equal(frame.Random, paused.Random);
        Assert.Equal(frame.Memory.Shot188!.Clock, paused.Memory.Shot188!.Clock);
        Assert.Equal(frame.Effect.Pools[1].Slots[0].ReadWord(12), paused.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Empty(operations.OfType<NativeBattleShot188Contact>());
        Assert.NotEmpty(operations.OfType<NativeBattleShot188Mesh>());
    }

    [Fact]
    public void Reopening_requires_retained_globals_and_clears_only_original_particle_markers()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot188Frame cold = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleShot188.Enter(cold with { Memory = cold.Memory with { Shot188 = null } }, packets));
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 7200).ToArray();
        NativeBattleShot188State before = cold.Memory.Shot188! with { ImpactParticles = bytes.ToImmutableArray(), Clock = 89, StopClock = 48, ShotSerial = 99, ImpactSerial = 98 };
        NativeBattleShot188Frame entered = NativeBattleShot188.Enter(cold with { Memory = cold.Memory with { Shot188 = before } }, packets);
        NativeBattleShot188State after = entered.Memory.Shot188!;
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
    private static NativeBattleShot188Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot188 = NativeBattleShot188State.FromColdProcess() }, new(188, 187, new(188, 0x5BE340, 0x5BE320), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 0, -1, 0, 188, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot188Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot188Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleShot188Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xD1CA28, 0xD1CB00, 0xD1CB74, 0xD1CF44 }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096)], [], 123);
    }
}
