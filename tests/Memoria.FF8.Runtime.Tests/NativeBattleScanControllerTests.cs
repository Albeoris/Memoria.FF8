using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanControllerTests
{
    [Fact]
    public void Entry_keeps_the_actual_contact_actor_unsigned_header_counts_and_five_physical_banks()
    {
        var fixture = Fixture();
        NativeBattleScanFrame frame = fixture.Frame;
        Assert.Equal(0x269ABA8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new uint[] { 0x269ABA8, 0x269CE50, 0x269CFC8, 0x269CE18, 0x269AA68 }, frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(new uint[] { 0x269AAE0, 0x269CE60, 0x269CFD8, 0x269ABB8, 0x269A1F8 }, frame.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new ushort[] { 0x64, 0x58, 0x78, 0x58, 0x6C }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 20, 100, 20 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleModulePoolTask root = Root(frame);
        Assert.Equal(0x84D110u, root.Callback);
        Assert.Equal(0x1D99A78u, root.ReadDword(0x0C));
        Assert.Equal(0x269AAE0u, root.ReadDword(0x10));
        Assert.Equal(0u, root.ReadDword(0x18));
        Assert.Equal((byte)4, root.ReadByte(0x2D));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal((ushort)0, root.ReadWord(0x58));
        Assert.Equal((byte)0, root.ReadByte(0x29));
        Assert.Null(frame.Memory.Textures);
        Assert.Null(frame.State.CurrentDrawOffset);
        Assert.Equal(37, frame.Memory.Cursor);
        NativeBattleModulePoolTask unsigned = Root(Fixture(200, 0).Frame);
        Assert.Equal((ushort)200, unsigned.ReadWord(0x5A));
        Assert.Equal((ushort)255, unsigned.ReadWord(0x58));
    }

    [Fact]
    public void Root_spawns_before_services_and_visits_all_child_banks_in_the_same_pass_in_original_order()
    {
        var fixture = Fixture();
        List<string> calls = [];
        NativeBattleScanControllerInputs inputs = fixture.Inputs with
        {
            Begin50A730 = frame =>
            {
                calls.Add("50A730");
                Assert.Equal((byte)1, Root(frame).ReadByte(0x63));
                Assert.Equal(0x84D230u, frame.Effect.Pools[1].Slots[0].Callback);
                return frame with { Memory = frame.Memory with { SoundPendingFlags = 0x20 } };
            },
            SetMode4A8480 = (frame, mode) =>
            {
                calls.Add($"4A8480:{mode}");
                Assert.Equal(0x20u, frame.Memory.SoundPendingFlags);
                return frame;
            }
        };
        NativeBattleScanFrame frame = NativeBattleScanController.Advance84D110(fixture.Frame, inputs, NoChild);
        Assert.Equal((byte)1, Root(frame).ReadByte(0x29));
        frame = NativeBattleScanController.Advance84D110(frame, inputs, (current, address) =>
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            int pool = Enumerable.Range(1, 4).Single(index => hierarchy.Address(index, 0) == address);
            calls.Add($"child:{pool}");
            Assert.Equal((byte)2, Root(current).ReadByte(0x29));
            if (pool == 1)
            {
                Assert.Equal((byte)4, hierarchy.Read(address).ReadByte(0x2D));
                for (int next = 2; next < 5; next++)
                {
                    hierarchy = hierarchy.Append8DC540(next, (uint)(0x1000 + next), hierarchy.Pools[next].Stride, 0x269AAE0, (_, _, _) => new(2, 4)).Hierarchy;
                }
            }
            return new(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, 0);
        });
        Assert.Equal(new[] { "50A730", "4A8480:0", "child:1", "child:2", "child:3", "child:4" }, calls);
        Assert.Equal((ushort)4, Root(frame).ReadWord(0x5E));
        Assert.Equal((byte)4, Root(frame).ReadByte(0x28));
        Assert.Equal((ushort)2, Root(frame).ReadWord(0x5C));
        Assert.Equal((ushort)2, Root(frame).ReadWord(0x24));
        Assert.Equal(0, frame.State.CurrentDrawOffset);
    }

    [Theory]
    [InlineData((byte)0, (ushort)0xFFFF, (byte)0, (byte)3)]
    [InlineData((byte)0xFF, (ushort)0, (byte)0, (byte)1)]
    [InlineData((byte)0x7F, (ushort)0x80, (byte)0x80, (byte)1)]
    public void Group_comparison_sign_extends_the_byte_and_word_and_wraps_the_increment(byte group, ushort maximum, byte nextGroup, byte nextPhase)
    {
        var fixture = Fixture();
        NativeBattleScanFrame frame = WriteRoot(fixture.Frame, root => root.WriteByte(0x29, 2).WriteByte(0x2A, group).WriteWord(0x58, maximum));
        frame = NativeBattleScanController.Advance84D110(frame, fixture.Inputs, NoChild);
        Assert.Equal(nextGroup, Root(frame).ReadByte(0x2A));
        Assert.Equal(nextPhase, Root(frame).ReadByte(0x29));
    }

    [Fact]
    public void Group_wait_uses_the_retained_latch_and_does_not_invent_child_completion()
    {
        var fixture = Fixture();
        NativeBattleScanFrame frame = WriteRoot(fixture.Frame, root => root.WriteByte(0x29, 2).WriteByte(0x63, 1));
        frame = NativeBattleScanController.Advance84D110(frame, fixture.Inputs, NoChild);
        Assert.Equal((byte)2, Root(frame).ReadByte(0x29));
        Assert.Equal((byte)1, Root(frame).ReadByte(0x63));
        frame = WriteRoot(frame, root => root.WriteByte(0x63, 0));
        frame = NativeBattleScanController.Advance84D110(frame, fixture.Inputs, NoChild);
        Assert.Equal((byte)3, Root(frame).ReadByte(0x29));
    }

    [Fact]
    public void Exit_wait_reads_the_previous_count_before_children_and_releases_the_actual_root_descriptor()
    {
        var fixture = Fixture();
        List<int> modes = [];
        NativeBattleScanControllerInputs inputs = fixture.Inputs with
        {
            SetMode4A8480 = (frame, mode) =>
            {
                modes.Add(mode);
                return frame;
            }
        };
        NativeBattleScanFrame frame = WriteRoot(fixture.Frame, root => root.WriteByte(0x29, 3).WriteByte(0x26, 0x80).WriteWord(0x5E, 1));
        frame = NativeBattleScanController.Advance84D110(frame, inputs, NoChild);
        Assert.Empty(modes);
        Assert.Equal((byte)3, Root(frame).ReadByte(0x29));
        Assert.Equal((ushort)0, Root(frame).ReadWord(0x5E));
        frame = NativeBattleScanController.Advance84D110(frame, inputs, NoChild);
        Assert.Equal(new[] { 1 }, modes);
        Assert.Equal((byte)4, Root(frame).ReadByte(0x29));
        Assert.Equal((byte)0x81, Root(frame).ReadByte(0x26));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Null(frame.Effect.Pools[0].Head);
        Assert.Equal((ushort)0, Root(frame).Flags);
        Assert.Same(frame, NativeBattleScanController.Advance84D110(frame, inputs, NoChild));
    }

    [Fact]
    public void A_live_child_owns_its_parent_count_and_completion_before_the_root_can_finish()
    {
        var fixture = Fixture();
        NativeBattleScanFrame frame = WriteRoot(fixture.Frame, root => root.WriteByte(0x29, 4).WriteByte(0x26, 1));
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Append8DC540(1, 0x84D230, 0x58, 0x269AAE0, (_, _, _) => new(2, 4)).Hierarchy;
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleScanController.Advance84D110(frame, fixture.Inputs, (current, _) => new(current, 0));
        Assert.Equal(0x269ABA8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((byte)1, Root(frame).ReadByte(0x28));
        frame = NativeBattleScanController.Advance84D110(frame, fixture.Inputs, (current, address) =>
        {
            NativeBattleEffectHierarchy completed = new NativeBattleEffectHierarchy(current.Effect.Pools).Complete8DC530(address);
            return new(current with { Effect = current.Effect with { Pools = completed.Pools } }, 2);
        });
        Assert.Equal((byte)0, Root(frame).ReadByte(0x28));
        Assert.Equal((ushort)0, Root(frame).ReadWord(0x5E));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Null(frame.Effect.Pools[1].Head);
    }

    [Fact]
    public void Snapshot_preserves_root_latches_physical_child_links_and_fixed_arena_buffer_parity()
    {
        var fixture = Fixture();
        NativeBattleScanFrame frame = NativeBattleScanController.Advance84D110(fixture.Frame, fixture.Inputs, NoChild);
        frame = NativeBattleScanController.Advance84D110(frame, fixture.Inputs, (current, _) => new(current, 0));
        NativeBattleScanFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleScanFrame>(NativeBattleTestSnapshots.Json(frame))!;
        for (int step = 0; step < 3; step++)
        {
            frame = NativeBattleScanController.Advance84D110(frame, fixture.Inputs, (current, _) => new(current, 0));
            restored = NativeBattleScanController.Advance84D110(restored, fixture.Inputs, (current, _) => new(current, 0));
            Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(restored));
            Assert.Equal(step % 2 == 0 ? 0x4000 : 0, frame.State.CurrentDrawOffset);
        }
        Assert.Equal((byte)1, Root(restored).ReadByte(0x63));
        Assert.Equal((byte)1, Root(restored).ReadByte(0x28));
    }

    [Theory]
    [InlineData((byte)0xFF)]
    [InlineData((byte)5)]
    public void Unsupported_signed_root_phase_fails_before_advancing_children(byte phase)
    {
        var fixture = Fixture();
        NativeBattleScanFrame frame = WriteRoot(fixture.Frame, root => root.WriteByte(0x29, phase));
        Assert.Throws<InvalidDataException>(() => NativeBattleScanController.Advance84D110(frame, fixture.Inputs, NoChild));
    }

    private static NativeBattleModulePoolTask Root(NativeBattleScanFrame frame)
    {
        return frame.Effect.Pools[0].Slots[0];
    }

    private static NativeBattleScanFrame WriteRoot(NativeBattleScanFrame frame, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(0x269AAE0, update(hierarchy.Read(0x269AAE0)));
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }

    private static NativeBattleScanChildStep NoChild(NativeBattleScanFrame frame, uint address)
    {
        throw new InvalidOperationException("No child callback should be reached.");
    }

    private static (NativeBattleScanFrame Frame, NativeBattleScanControllerInputs Inputs) Fixture(byte targets = 1, byte repeatMode = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, repeatMode);
        (packets, _) = packets.Publish(targets);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(39).LoadIntoArena(new byte[37]) with { Textures = null };
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame frame = NativeBattleScanController.Initialize84D030(memory, effect, packets);
        NativeBattleScanControllerInputs inputs = new(packets, index => new(index, 0), _ => throw new InvalidOperationException("Inactive models have no posed anchors."), current => current, (current, _) => current);
        return (frame, inputs);
    }
}
