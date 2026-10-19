using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot192Tests
{
    [Fact]
    public void Every_physical_contact_gets_its_own_original_random_branch_and_delayed_impact_pair()
    {
        List<NativeBattleShot192Operation> operations = [];
        List<int> bones = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot192Frame frame = NativeBattleShot192.Enter(Fixture(), packets);
        NativeBattleShot192Inputs inputs = Inputs(packets, operations) with { ReadBone = (actor, bone, angle) =>
        {
            bones.Add(actor);
            return new(100, 200, 300);
        } };
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot192.Advance(frame, inputs, Resources());
        }
        frame = NativeBattleShot192.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot192.Advance(frame, inputs, Resources());
        frame = NativeBattleShot192.Advance(frame, inputs, Resources());
        Assert.Equal(new[] { 3, 4, 4 }, bones);
        Assert.Equal(4, frame.Memory.Shot192!.ImpactSerial);
        Assert.Equal(14400, frame.Memory.Shot192.ImpactParticles.Length);
        Assert.Single(operations.OfType<NativeBattleShot192Contact>());
        Assert.Equal(3, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x5BA4B0));
        Assert.Single(frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x5BA800));
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 99; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        int clock = frame.Memory.Shot192.Clock;
        operations.Clear();
        frame = NativeBattleShot192.Advance(frame, inputs with { GraphicsFlags = 0x201 }, Resources());
        expected = expected.Draw(out _).Draw(out _);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(clock, frame.Memory.Shot192!.Clock);
        Assert.Contains(operations, operation => operation is NativeBattleShot192Mesh { Address: 0xD17B58 });
        Assert.Empty(frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x5BA800));
    }

    [Fact]
    public void Cleanup_retains_its_physical_completion_BYTE_across_an_independent_standalone()
    {
        List<NativeBattleShot192Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot192Frame frame = NativeBattleShot192.Enter(Fixture(), packets);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(NativeBattleShot192.Root, 0x227B348, 16, 4);
        (root, int slot) = root.Append(0x5BB260);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteWord(12, 0).WriteWord(14, 0)) };
        frame = frame with { Effect = frame.Effect with { Pools = [root, NativeBattleEffectTaskPool.Initialize(0x227C1A8, 0x227B388, 36, 100), NativeBattleEffectTaskPool.Initialize(0x227FAA0, 0x227FAB0, 36, 100)] } };
        frame = NativeBattleShot192.Advance(frame, Inputs(packets, operations), Resources());
        Assert.Empty(operations.OfType<NativeBattleShot192ReloadCommon>());
        frame = NativeBattleShot192.Advance(frame, Inputs(packets, operations), Resources());
        NativeBattleShot192ReloadCommon reload = Assert.Single(operations.OfType<NativeBattleShot192ReloadCommon>());
        Assert.Equal(0x227B356u, reload.CompletionAddress);
        NativeBattleShot192Frame restored = JsonSerializer.Deserialize<NativeBattleShot192Frame>(JsonSerializer.Serialize(frame))!;
        frame = NativeBattleShot192.Advance(frame, Inputs(packets, operations), Resources());
        restored = NativeBattleShot192.Advance(restored, Inputs(packets, []), Resources());
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.Single(operations.OfType<NativeBattleShot192ReloadCommon>());
        Assert.Throws<NotSupportedException>(() => NativeBattleShot192.WriteCompletionByte(frame.Effect, reload.CompletionAddress + 1, 1));
        frame = frame with { Effect = NativeBattleShot192.WriteCompletionByte(frame.Effect, reload.CompletionAddress, 1) };
        frame = NativeBattleShot192.Advance(frame, Inputs(packets, operations), Resources());
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Original_opening_repeated_contact_and_ending_drain_all_three_native_pools()
    {
        List<NativeBattleShot192Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot192Frame frame = NativeBattleShot192.Enter(Fixture(), packets);
        NativeBattleShot192Inputs inputs = Inputs(packets, operations);
        NativeBattleShot192Resources resources = Resources();
        for (int callback = 0; callback < 6; callback++)
        {
            frame = NativeBattleShot192.Advance(frame, inputs, resources);
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot192.Advance(frame, inputs, resources);
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.Contains(operations, operation => operation is NativeBattleShot192Bind { Actor: 0 });
        Assert.Contains(operations, operation => operation is NativeBattleShot192Sequence { Sequence: 1 });
        frame = NativeBattleShot192.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot192.Advance(frame, inputs, resources);
        Assert.Empty(operations.OfType<NativeBattleShot192Contact>());
        frame = NativeBattleShot192.Advance(frame, inputs, resources);
        Assert.Single(operations.OfType<NativeBattleShot192Contact>());
        Assert.Single(operations.OfType<NativeBattleShot192Sound>());
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 99; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot192.Advance(frame, inputs, resources);
        }
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        frame = NativeBattleShot192.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot192.Advance(frame, inputs, resources);
            NativeBattleShot192ReloadCommon? reload = operations.OfType<NativeBattleShot192ReloadCommon>().LastOrDefault();
            if (reload is not null)
            {
                frame = frame with { Effect = NativeBattleShot192.WriteCompletionByte(frame.Effect, reload.CompletionAddress, 1) };
            }
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, operations.OfType<NativeBattleShot192Contact>().Count());
        Assert.Contains(operations, operation => operation is NativeBattleShot192Sequence { Sequence: 2 });
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(3, frame.Memory.Shot192!.Control);
        Assert.Single(operations.OfType<NativeBattleShot192ReloadCommon>());
    }

    [Fact]
    public void Mid_shot_standalone_restoration_preserves_particle_rng_and_the_entire_remaining_callback_stream()
    {
        List<NativeBattleShot192Operation> initial = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot192Resources resources = Resources();
        NativeBattleShot192Frame frame = NativeBattleShot192.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot192.Advance(frame, Inputs(packets, initial), resources);
        }
        frame = NativeBattleShot192.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot192.Advance(frame, Inputs(packets, initial), resources);
        frame = NativeBattleShot192.Advance(frame, Inputs(packets, initial), resources);
        NativeBattleShot192Frame restored = JsonSerializer.Deserialize<NativeBattleShot192Frame>(JsonSerializer.Serialize(frame))!;
        for (int callback = 0; callback < 20; callback++)
        {
            List<NativeBattleShot192Operation> first = [];
            List<NativeBattleShot192Operation> second = [];
            frame = NativeBattleShot192.Advance(frame, Inputs(packets, first), resources);
            restored = NativeBattleShot192.Advance(restored, Inputs(packets, second), resources);
            Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
            Assert.Equal(first.Select(operation => JsonSerializer.Serialize(operation, operation.GetType())), second.Select(operation => JsonSerializer.Serialize(operation, operation.GetType())));
        }
    }

    [Fact]
    public void Native_graphics_pause_keeps_contact_rng_and_all_task_clocks_while_still_publishing_held_draws()
    {
        List<NativeBattleShot192Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot192Resources resources = Resources();
        NativeBattleShot192Frame frame = NativeBattleShot192.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot192.Advance(frame, Inputs(packets, operations), resources);
        }
        frame = NativeBattleShot192.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot192.Advance(frame, Inputs(packets, operations), resources);
        operations.Clear();
        NativeBattleShot192Frame paused = NativeBattleShot192.Advance(frame, Inputs(packets, operations) with { GraphicsFlags = 0x200 }, resources);
        Assert.Equal(frame.Random, paused.Random);
        Assert.Equal(frame.Memory.Shot192!.Clock, paused.Memory.Shot192!.Clock);
        Assert.Equal(frame.Effect.Pools[1].Slots[0].ReadWord(12), paused.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Empty(operations.OfType<NativeBattleShot192Contact>());
        Assert.NotEmpty(operations.OfType<NativeBattleShot192Mesh>());
    }

    [Fact]
    public void Reopening_requires_retained_globals_and_clears_only_original_particle_markers()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot192Frame cold = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleShot192.Enter(cold with { Memory = cold.Memory with { Shot192 = null } }, packets));
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 14400).ToArray();
        NativeBattleShot192State before = cold.Memory.Shot192! with { ImpactParticles = bytes.ToImmutableArray(), Clock = 89, StopClock = 48, ShotSerial = 99, ImpactSerial = 98 };
        NativeBattleShot192Frame entered = NativeBattleShot192.Enter(cold with { Memory = cold.Memory with { Shot192 = before } }, packets);
        NativeBattleShot192State after = entered.Memory.Shot192!;
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
    private static NativeBattleShot192Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot192 = NativeBattleShot192State.FromColdProcess() }, new(192, 191, new(192, 0x5B96F0, 0x5B96D0), new(0, 0, 24, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 1, -1, 0, 192, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(4, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot192Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot192Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => new(100, 200, 300), _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleShot192Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xD16B58, 0xD16CA4, 0xD16C30, 0xD16EC8, 0xD17074 }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096)], [], 123);
    }
}
