using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanReticleTests
{
    [Fact]
    public void First_phase_appends_actual_child_order_corners_and_parent_links_before_drawing()
    {
        var fixture = Fixture();
        List<short> radii = [];
        int spokes = 0;
        NativeBattleScanReticleInputs inputs = Inputs(fixture.Packets, (frame, task, radius, angle, count) =>
        {
            Assert.Equal((short)0, angle);
            Assert.Equal((short)32, count);
            Assert.Equal((byte)10, task.ReadByte(0x28));
            radii.Add(radius);
            return frame;
        }, (frame, _) =>
        {
            spokes++;
            return frame;
        });
        NativeBattleScanReticleStep step = NativeBattleScanReticle.Advance84D5B0(fixture.Frame, fixture.Address, inputs);
        NativeBattleEffectHierarchy hierarchy = new(step.Frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask parent = hierarchy.Read(fixture.Address);
        Assert.Equal(0, step.Result);
        Assert.Equal((byte)1, parent.ReadByte(0x29));
        Assert.Equal((ushort)1, parent.ReadWord(0x24));
        Assert.Equal((short)-20, unchecked((short)parent.ReadWord(0x1C)));
        NativeBattleModulePoolTask[] corners = hierarchy.Pools[2].Slots.Where(task => task.Flags != 0).Skip(1).ToArray();
        Assert.Equal(new uint[] { 0x84E010, 0x84E010, 0x84E010, 0x84E010, 0x84DDD0, 0x84DDD0, 0x84DDD0, 0x84DDD0 }, corners.Select(task => task.Callback));
        Assert.Equal(new ushort[] { 0, 1, 2, 3, 0, 1, 2, 3 }, corners.Select(task => task.ReadWord(0x74)));
        NativeBattleModulePoolTask[] pair = hierarchy.Pools[4].Slots.Where(task => task.Flags != 0).ToArray();
        Assert.Equal(new uint[] { 0x84DAA0, 0x84D810 }, pair.Select(task => task.Callback));
        Assert.All(corners.Concat(pair), task =>
        {
            Assert.Equal(fixture.Address, task.ReadDword(0x18));
            Assert.Equal((byte)4, task.ReadByte(0x2D));
            Assert.Equal(parent.ReadDword(0x0C), task.ReadDword(0x0C));
        });
        Assert.Equal(new short[] { 8 }, radii);
        Assert.Equal(1, spokes);
    }

    [Fact]
    public void Sentinel_retains_anchor_sets_radius_eight_then_draws_six_four_two_and_waits_actual_highlight()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteByte(0x29, 1).WriteWord(0x24, 20).WriteWord(0x1C, 123).WriteWord(0x1E, 456));
        NativeBattleScanReticleFrame frame = fixture.Frame with { Scan = fixture.Frame.Scan with { Effect = fixture.Frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        List<short> radii = [];
        NativeBattleScanReticleInputs inputs = Inputs(fixture.Packets, (current, task, radius, _, _) =>
        {
            Assert.Equal((ushort)123, task.ReadWord(0x1C));
            Assert.Equal((ushort)456, task.ReadWord(0x1E));
            radii.Add(radius);
            return current;
        });
        for (int index = 0; index < 6; index++)
        {
            frame = NativeBattleScanReticle.Advance84D5B0(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal(new short[] { 8, 6, 4, 2 }, radii);
        hierarchy = new(frame.Scan.Effect.Pools);
        Assert.Equal((byte)4, hierarchy.Read(fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)1, hierarchy.Read(fixture.Address).ReadByte(0x28));
        NativeBattleScanReticleStep waiting = NativeBattleScanReticle.Advance84D5B0(frame, fixture.Address, inputs);
        Assert.Equal((byte)4, new NativeBattleEffectHierarchy(waiting.Frame.Scan.Effect.Pools).Read(fixture.Address).ReadByte(0x29));
        NativeBattleScanReticleStep signaled = NativeBattleScanReticle.Advance84D5B0(waiting.Frame with { Highlight269AA90 = 0 }, fixture.Address, inputs);
        Assert.Equal(0, signaled.Result);
        hierarchy = new(signaled.Frame.Scan.Effect.Pools);
        Assert.Equal((byte)5, hierarchy.Read(fixture.Address).ReadByte(0x29));
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteByte(0x28, 0));
        NativeBattleScanReticleStep completed = NativeBattleScanReticle.Advance84D5B0(signaled.Frame with { Scan = signaled.Frame.Scan with { Effect = signaled.Frame.Scan.Effect with { Pools = hierarchy.Pools } } }, fixture.Address, inputs);
        Assert.Equal(2, completed.Result);
        Assert.Equal((ushort)1, new NativeBattleEffectHierarchy(completed.Frame.Scan.Effect.Pools).Read(fixture.Address).Flags);
    }

    [Fact]
    public void Ring_and_spokes_keep_physical_sixteen_byte_packets_order_and_word_wrap()
    {
        NativeBattleScanReticleRing ring = NativeBattleScanReticleLines.Ring84D6D0(0, 0, 8, 0, 32);
        Assert.Equal(512, ring.Packets.AllocationBytes);
        Assert.Equal(new NativeBattleScanReticlePoint(168, 108), ring.FinalPoint);
        Assert.Equal(Enumerable.Range(0, 32).Select(index => index * 16), ring.Packets.Primitives.Select(primitive => primitive.ByteOffset));
        Assert.All(ring.Packets.Primitives, primitive =>
        {
            Assert.Equal((ushort)1, primitive.Flags20);
            Assert.Equal(5, primitive.Primitive.Bucket);
            Assert.Equal(0x03000000u, primitive.Primitive.Words[0]);
            Assert.Equal(0x4000FF00u, primitive.Primitive.Words[1]);
            Assert.Equal(4, primitive.Primitive.Words.Length);
        });
        Assert.Equal(ring.Packets.Primitives[0].Primitive.Words[2], ring.Packets.Primitives[^1].Primitive.Words[3]);
        NativeBattleModulePacketBatch spokes = NativeBattleScanReticleLines.FourLines84E1F0(short.MaxValue, short.MaxValue);
        Assert.All(spokes.Primitives, primitive => Assert.Equal((ushort)1, primitive.Flags20));
        Assert.Equal(new uint[] { 0x000C0010, 0x000C0130, 0x00CC0010, 0x00CC0130 }, spokes.Primitives.Select(primitive => primitive.Primitive.Words[3]));
        Assert.Equal(unchecked((ushort)(short.MaxValue + 160)), (ushort)spokes.Primitives[0].Primitive.Words[2]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_signed_count_preserves_output_and_allocates_nothing(short count)
    {
        NativeBattleScanReticleRing ring = NativeBattleScanReticleLines.Ring84D6D0(0, 0, 8, 0, count);
        Assert.Null(ring.FinalPoint);
        Assert.Equal(0, ring.Packets.AllocationBytes);
        Assert.Empty(ring.Packets.Primitives);
    }

    [Fact]
    public void Negative_component_uses_truncation_toward_zero()
    {
        NativeBattleScanReticleRing ring = NativeBattleScanReticleLines.Ring84D6D0(0, 0, -1, 0, 1);
        Assert.Equal(new NativeBattleScanReticlePoint(160, 108), ring.FinalPoint);
        Assert.Equal(159u, ring.Packets.Primitives[0].Primitive.Words[2] & 0xFFFF);
    }

    private static NativeBattleScanReticleInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleScanReticleFrame, NativeBattleModulePoolTask, short, short, short, NativeBattleScanReticleFrame> ring, Func<NativeBattleScanReticleFrame, NativeBattleModulePoolTask, NativeBattleScanReticleFrame>? lines = null)
    {
        return new(packets, Enumerable.Range(0, 21).Select(index => new NativeBattleScanReticlePoint(index == 20 ? short.MaxValue : (short)-20, 10)).ToImmutableArray(), ring, lines ?? ((frame, _) => frame));
    }

    private static (NativeBattleScanReticleFrame Frame, uint Address, NativeBattleActionPacketsState Packets) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(scan.Effect.Pools).Append8DC540(2, 0x84D5B0, 0x78, 0x269AAE0, (_, _, _) => new(2, 4));
        scan = scan with { Effect = scan.Effect with { Pools = child.Hierarchy.Pools } };
        return (new(scan, 1), child.Address, packets);
    }
}
