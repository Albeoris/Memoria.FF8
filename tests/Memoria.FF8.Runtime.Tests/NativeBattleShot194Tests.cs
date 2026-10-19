using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot194Tests
{
    [Fact]
    public void Original_firing_owns_twenty_frames_and_physical_task_address_ring_operand()
    {
        List<NativeBattleShot194Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot194Frame frame = NativeBattleShot194.Enter(Fixture(), packets);
        NativeBattleShot194Inputs inputs = Inputs(packets, operations);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot194.Advance(frame, inputs, Resources());
        }
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        frame = NativeBattleShot194.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        for (int phase = 0; phase <= 9; phase++)
        {
            frame = NativeBattleShot194.Advance(frame, inputs, Resources());
        }
        NativeBattleEffectTaskPool actions = frame.Effect.Pools[1];
        int main = Enumerable.Range(0, actions.Slots.Length).FirstOrDefault(index => actions.Slots[index].Callback == 0x5B4DF0 && (actions.Slots[index].Flags & 1) != 0, -1);
        Assert.True(main >= 0);
        NativeBattleModulePoolTask[] rings = frame.Effect.Pools[2].Slots.Where(task => task.Callback == 0x5B6580 && (task.Flags & 1) != 0).ToArray();
        Assert.Equal(2, rings.Length);
        short firstVelocityAfterUpdate = unchecked((short)rings[0].ReadWord(26));
        short secondVelocityAfterUpdate = unchecked((short)rings[1].ReadWord(26));
        short[] initialCandidates = Enumerable.Range(-249, 499).Select(value => (short)value).Where(value => value - value / 12 == firstVelocityAfterUpdate).ToArray();
        Assert.Contains(initialCandidates, first =>
        {
            short second = NativeBattleShot194Trail.SecondRingVelocity(first, checked(actions.BankAddress + (uint)(main * actions.Stride)));
            return unchecked((short)(second - second / 12)) == secondVelocityAfterUpdate;
        });
        Assert.Empty(operations.OfType<NativeBattleShot194Contact>());
        Assert.Single(operations.OfType<NativeBattleShot194Sound>());
        frame = NativeBattleShot194.Advance(frame, inputs, Resources());
        Assert.Empty(operations.OfType<NativeBattleShot194Contact>());
        frame = NativeBattleShot194.Advance(frame, inputs, Resources());
        Assert.Single(operations.OfType<NativeBattleShot194Contact>());
        for (int phase = 12; phase < 15; phase++)
        {
            frame = NativeBattleShot194.Advance(frame, inputs, Resources());
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot194.Advance(frame, inputs, Resources());
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        Assert.Equal(0, frame.Memory.Shot194!.Control & 4);
        frame = NativeBattleShot194.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot194.Advance(frame, inputs, Resources());
            foreach (NativeBattleShot194ReloadCommon reload in operations.OfType<NativeBattleShot194ReloadCommon>())
            {
                frame = frame with { Effect = NativeBattleShot194.WriteCompletionByte(frame.Effect, reload.CompletionAddress, 1) };
            }
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Trail_lane_generation_and_module_reentry_preserve_unassigned_storage()
    {
        byte[] lanes = Enumerable.Repeat((byte)0xA5, 8 * 232).ToArray();
        byte[] particles = Enumerable.Repeat((byte)0xCC, 7200).ToArray();
        NativeBattleShot194State state = NativeBattleShot194State.FromColdProcess() with { TrailLanes = lanes.ToImmutableArray(), ImpactParticles = particles.ToImmutableArray(), StopClock = 321, LaneSerial = 17 };
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        state = NativeBattleShot194Trail.Initialize5B5AC0(state, 3, new(new(0, 0, 0), identity), new(230, -46, 0), new(4096, 0, 0));
        Assert.Equal(new NativeBattleVector(0, 0, 0), NativeBattleShot194Trail.ReadSample(state, 3, 0));
        Assert.Equal(new NativeBattleVector(230, -46, 0), NativeBattleShot194Trail.ReadSample(state, 3, 23));
        Assert.Equal(lanes.AsSpan(0, 3 * 232).ToArray(), state.TrailLanes.AsSpan().Slice(0, 3 * 232).ToArray());
        Assert.Equal((byte)0xA5, state.TrailLanes[3 * 232 + 46]);
        NativeBattleShot194State after = state.Enter(2);
        Assert.Equal(state.TrailLanes, after.TrailLanes);
        Assert.Equal(321, after.StopClock);
        Assert.Equal(0, after.LaneSerial);
        for (int offset = 0; offset < 7200; offset++)
        {
            Assert.Equal(offset % 24 < 4 ? (byte)0 : (byte)0xCC, after.ImpactParticles[offset]);
        }
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleShot194State>(JsonSerializer.Serialize(state))));
    }

    private static NativeBattleShot194Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot194 = NativeBattleShot194State.FromColdProcess() }, new(194, 193, new(194, 0x5B4C60, 0x5B4C40), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 2, -1, 0, 194, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot194Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot194Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => new(100, 200, 300), _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return operation is NativeBattleShot194Quad ? frame with { Effect = frame.Effect with { DrawArenaOffset = frame.Effect.DrawArenaOffset + 40 } } : frame;
        });
    }

    private static NativeBattleShot194Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xD0D938, 0xD0DB5C, 0xD0DC34, 0xD0DCA8 }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096)], [], 123, Enumerable.Repeat(0x2E808080u, 8).ToImmutableArray());
    }
}
