using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot193Tests
{
    [Fact]
    public void Cleanup_retains_its_physical_completion_BYTE_across_an_independent_standalone()
    {
        List<NativeBattleShot193Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot193Frame frame = NativeBattleShot193.Enter(Fixture(), packets);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(NativeBattleShot193.Root, 0x2276788, 16, 4);
        (root, int slot) = root.Append(0x5B9690);
        root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteWord(12, 0).WriteWord(14, 0)) });
        frame = frame with { Effect = frame.Effect with { Pools = [root, NativeBattleEffectTaskPool.Initialize(0x22775E8, 0x22767C8, 36, 100), NativeBattleEffectTaskPool.Initialize(0x22792C0, 0x22792D0, 36, 100)] } };
        frame = NativeBattleShot193.Advance(frame, Inputs(packets, operations), Resources());
        Assert.Empty(operations.OfType<NativeBattleShot193ReloadCommon>());
        frame = NativeBattleShot193.Advance(frame, Inputs(packets, operations), Resources());
        NativeBattleShot193ReloadCommon reload = Assert.Single(operations.OfType<NativeBattleShot193ReloadCommon>());
        Assert.Equal(0x2276796u, reload.CompletionAddress);
        NativeBattleShot193Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShot193Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleShot193.Advance(frame, Inputs(packets, operations), Resources());
        restored = NativeBattleShot193.Advance(restored, Inputs(packets, []), Resources());
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(restored));
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.Single(operations.OfType<NativeBattleShot193ReloadCommon>());
        Assert.Throws<NotSupportedException>(() => NativeBattleShot193.WriteCompletionByte(frame.Effect, reload.CompletionAddress + 1, 1));
        frame = frame with { Effect = NativeBattleShot193.WriteCompletionByte(frame.Effect, reload.CompletionAddress, 1) };
        frame = NativeBattleShot193.Advance(frame, Inputs(packets, operations), Resources());
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Original_opening_repeated_contact_and_ending_drain_all_three_native_pools()
    {
        List<NativeBattleShot193Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot193Frame frame = NativeBattleShot193.Enter(Fixture(), packets);
        NativeBattleShot193Inputs inputs = Inputs(packets, operations);
        NativeBattleShot193Resources resources = Resources();
        for (int callback = 0; callback < 6; callback++)
        {
            frame = NativeBattleShot193.Advance(frame, inputs, resources);
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot193.Advance(frame, inputs, resources);
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.Contains(operations, operation => operation is NativeBattleShot193Bind { Actor: 0 });
        Assert.Contains(operations, operation => operation is NativeBattleShot193Sequence { Sequence: 1 });
        frame = NativeBattleShot193.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot193.Advance(frame, inputs, resources);
        Assert.Empty(operations.OfType<NativeBattleShot193Contact>());
        frame = NativeBattleShot193.Advance(frame, inputs, resources);
        Assert.Single(operations.OfType<NativeBattleShot193Contact>());
        Assert.Single(operations.OfType<NativeBattleShot193Sound>());
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 159; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot193.Advance(frame, inputs, resources);
        }
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        frame = NativeBattleShot193.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot193.Advance(frame, inputs, resources);
            NativeBattleShot193ReloadCommon? reload = operations.OfType<NativeBattleShot193ReloadCommon>().LastOrDefault();
            if (reload is not null)
            {
                frame = frame with { Effect = NativeBattleShot193.WriteCompletionByte(frame.Effect, reload.CompletionAddress, 1) };
            }
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, operations.OfType<NativeBattleShot193Contact>().Count());
        Assert.Contains(operations, operation => operation is NativeBattleShot193Sequence { Sequence: 2 });
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(3, frame.Memory.Shot193!.Control);
        Assert.Single(operations.OfType<NativeBattleShot193ReloadCommon>());
    }

    [Fact]
    public void Mid_shot_standalone_restoration_preserves_particle_rng_and_the_entire_remaining_callback_stream()
    {
        List<NativeBattleShot193Operation> initial = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot193Resources resources = Resources();
        NativeBattleShot193Frame frame = NativeBattleShot193.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot193.Advance(frame, Inputs(packets, initial), resources);
        }
        frame = NativeBattleShot193.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot193.Advance(frame, Inputs(packets, initial), resources);
        frame = NativeBattleShot193.Advance(frame, Inputs(packets, initial), resources);
        NativeBattleShot193Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShot193Frame>(NativeBattleTestSnapshots.Json(frame))!;
        for (int callback = 0; callback < 20; callback++)
        {
            List<NativeBattleShot193Operation> first = [];
            List<NativeBattleShot193Operation> second = [];
            frame = NativeBattleShot193.Advance(frame, Inputs(packets, first), resources);
            restored = NativeBattleShot193.Advance(restored, Inputs(packets, second), resources);
            Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(restored));
            Assert.Equal(first.Select(operation => NativeBattleTestSnapshots.Json(operation, operation.GetType())), second.Select(operation => NativeBattleTestSnapshots.Json(operation, operation.GetType())));
        }
    }

    [Fact]
    public void Native_graphics_pause_keeps_contact_rng_and_all_task_clocks_while_still_publishing_held_draws()
    {
        List<NativeBattleShot193Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot193Resources resources = Resources();
        NativeBattleShot193Frame frame = NativeBattleShot193.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot193.Advance(frame, Inputs(packets, operations), resources);
        }
        frame = NativeBattleShot193.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot193.Advance(frame, Inputs(packets, operations), resources);
        operations.Clear();
        NativeBattleShot193Frame paused = NativeBattleShot193.Advance(frame, Inputs(packets, operations) with { GraphicsFlags = 0x200 }, resources);
        Assert.Equal(frame.Random, paused.Random);
        Assert.Equal(frame.Memory.Shot193!.Clock, paused.Memory.Shot193!.Clock);
        Assert.Equal(frame.Effect.Pools[1].Slots[0].ReadWord(12), paused.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Empty(operations.OfType<NativeBattleShot193Contact>());
        Assert.NotEmpty(operations.OfType<NativeBattleShot193Mesh>());
    }

    [Fact]
    public void Reopening_requires_retained_globals_and_clears_only_original_particle_markers()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot193Frame cold = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleShot193.Enter(cold with { Memory = cold.Memory with { Shot193 = null } }, packets));
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 7200).ToArray();
        NativeBattleShot193State before = cold.Memory.Shot193! with { ImpactParticles = bytes.ToImmutableArray(), Clock = 89, StopClock = 48, ShotSerial = 99, ImpactSerial = 98 };
        NativeBattleShot193Frame entered = NativeBattleShot193.Enter(cold with { Memory = cold.Memory with { Shot193 = before } }, packets);
        NativeBattleShot193State after = entered.Memory.Shot193!;
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
    private static NativeBattleShot193Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot193 = NativeBattleShot193State.FromColdProcess() }, new(193, 192, new(193, 0x5B7190, 0x5B7170), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 2, -1, 0, 193, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot193Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot193Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => new(100, 200, 300), _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleShot193Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xD14430, 0xD1450C, 0xD145E4, 0xD14658, 0xD149B0 }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096)], [], 123);
    }
}
