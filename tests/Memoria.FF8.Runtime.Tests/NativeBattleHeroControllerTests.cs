using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHeroControllerTests
{
    [Theory]
    [InlineData(309, 0x25074A0, 0x2507490, 0x3CC, 1, 0x3D4)]
    [InlineData(310, 0x2507450, 0x2507440, 0x3D0, 6, 0x16E8)]
    public void Root_yields_once_then_uses_its_actual_arena_bank_and_retained_suffix(int effect, uint main, uint children, int stride, int capacity, int firstDraw)
    {
        var fixture = Fixture((ushort)effect, [3]);
        NativeBattleHeroFrame frame = fixture.Frame;
        Assert.Equal(main, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)20, Assert.Single(frame.Effect.Pools).Stride);
        frame = NativeBattleHeroController.Advance(NativeBattleTestSnapshots.Independent(frame), fixture.Resources, fixture.Inputs);
        Assert.Single(frame.Effect.Pools);
        Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(0x1000 + firstDraw + 0x10000, frame.State.PacketCursor);
        frame = NativeBattleHeroController.Advance(NativeBattleTestSnapshots.Independent(frame), fixture.Resources, fixture.Inputs);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        Assert.Equal(children, pool.DescriptorAddress);
        Assert.Equal(0x20E0AB8u, pool.BankAddress);
        Assert.Equal(stride, pool.Stride);
        Assert.Equal(capacity, pool.Slots.Length);
        Assert.Equal(0x1000 + firstDraw, frame.State.PacketCursor);
        NativeBattleModulePoolTask task = pool.Slots[0];
        Assert.Equal((ushort)3072, task.ReadWord(14));
        Assert.Equal(unchecked((ushort)-92), task.ReadWord(frame.Profile.AnchorOffset + 2));
        Assert.Equal((ushort)0x1234, task.ReadWord(frame.Profile.AnchorOffset + 6));
        for (int index = 0; index < 16; index++)
        {
            int row = frame.Profile.SpokeOffset + index * 16;
            Assert.Equal((ushort)0xA5A5, task.ReadWord(row + 6));
            Assert.Equal((ushort)0xA5A5, task.ReadWord(row + 14));
        }
        Assert.Equal(task.ReadBytes(12, 2), frame.Memory.Arena!.Read(0x1000 + 12, 2));
        Assert.All(fixture.Operations.OfType<NativeBattleHeroAct>(), operation => Assert.Equal(true, operation.Context.ScaleEnabled));
        Assert.All(fixture.Operations.OfType<NativeBattleHeroAct>().Where(operation => operation.Source.Address == frame.Profile.OrbitSource), operation => Assert.Null(operation.Context.AnchorWord6));
        Assert.All(fixture.Operations.OfType<NativeBattleHeroAct>().Where(operation => operation.Source.Address == frame.Profile.MainSource), operation => Assert.Equal((short)0x1234, operation.Context.AnchorWord6));
    }

    [Theory]
    [InlineData(-1, 1023)]
    [InlineData(-32768, -15360)]
    [InlineData(32767, 17407)]
    public void Radius_uses_signed_WORD_arithmetic_shift_before_wrapping(int scale, int expected)
    {
        Assert.Equal((short)expected, NativeBattleHeroController.Radius((short)scale));
    }

    [Theory]
    [InlineData(-30, -101, -92)]
    [InlineData(30000, 32767, -15058)]
    public void Height_uses_two_distinct_signed_truncations_and_low_WORD_store(int angle, int height, int expected)
    {
        Assert.Equal((short)expected, NativeBattleHeroController.AnchorY((short)angle, (short)height));
    }

    [Fact]
    public void Spokes_consume_exactly48_ordered_raw_draws_and_preserve_unwritten_WORDs()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x3CC, 1);
        NativeBattleModulePoolTask input = NativeBattleTaskFixture.EditTask(pool.Slots[0], taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 0x3CC - 12).ToImmutableArray() });
        NativeCrtRandomState expected = new(123);
        var result = NativeBattleHeroController.InitializeSpokes(input, expected, 16);
        for (int index = 0; index < 16; index++)
        {
            expected = expected.Draw(out ushort angle);
            expected = expected.Draw(out ushort first);
            expected = expected.Draw(out ushort second);
            int row = 16 + index * 16;
            Assert.Equal((ushort)0, result.Task.ReadWord(row));
            Assert.Equal(unchecked((ushort)((angle & 0x7FF) - 0x400)), result.Task.ReadWord(row + 2));
            Assert.Equal((ushort)(index << 8), result.Task.ReadWord(row + 4));
            Assert.Equal((ushort)0, result.Task.ReadWord(row + 8));
            Assert.Equal(unchecked((ushort)((first & 63) - 32)), result.Task.ReadWord(row + 10));
            Assert.Equal(unchecked((ushort)((second & 63) - 32)), result.Task.ReadWord(row + 12));
            Assert.Equal((ushort)0xA5A5, result.Task.ReadWord(row + 6));
            Assert.Equal((ushort)0xA5A5, result.Task.ReadWord(row + 14));
        }
        Assert.Equal(expected, result.Random);
    }

    [Fact]
    public void Hero_sound_contact_fan_and_release_follow_age_not_animation_remaining()
    {
        var fixture = Fixture(309, [3]);
        NativeBattleHeroFrame frame = fixture.Frame;
        List<(int Tick, NativeBattleHeroOperation Operation)> observed = [];
        int tick = 0;
        NativeBattleHeroInputs inputs = fixture.Inputs with { Execute = (current, operation) =>
        {
            observed.Add((tick, operation));
            return current;
        } };
        for (tick = 1; tick <= 41; tick++)
        {
            frame = NativeBattleHeroController.Advance(NativeBattleTestSnapshots.Independent(frame), fixture.Resources, inputs);
            Assert.Equal(tick == 41 ? 0u : frame.Profile.MainDescriptor, frame.Effect.ReturnedDescriptor);
            if (tick == 20)
            {
                NativeBattleHeroFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleHeroFrame>(NativeBattleTestSnapshots.Json(frame))!;
                Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleHeroController.Advance(NativeBattleTestSnapshots.Independent(frame), fixture.Resources, fixture.Inputs)), NativeBattleTestSnapshots.Json(NativeBattleHeroController.Advance(restored, fixture.Resources, fixture.Inputs)));
            }
        }
        var sound = Assert.Single(observed, item => item.Operation is NativeBattleHeroSound);
        Assert.Equal(3, sound.Tick);
        Assert.Equal(new NativeBattleHeroSound(300188, 0, 128), sound.Operation);
        var contact = Assert.Single(observed, item => item.Operation is NativeBattleHeroContact);
        Assert.Equal(34, contact.Tick);
        Assert.Equal(0, ((NativeBattleHeroContact)contact.Operation).Index);
        Assert.Equal(320, observed.Count(item => item.Operation is NativeBattleHeroFan));
        Assert.Equal(20, observed.Count(item => item.Operation is NativeBattleHeroFanBegin));
        Assert.Equal(20, observed.Count(item => item.Operation is NativeBattleHeroFanEnd));
        Assert.DoesNotContain(observed.Where(item => item.Tick is < 18 or > 37), item => item.Operation is NativeBattleHeroFan);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void Hero_started_bank_does_not_spawn_again_when_retained_root_counter_equals_one()
    {
        var fixture = Fixture(309, [3]);
        NativeBattleHeroFrame frame = NativeBattleHeroController.Advance(fixture.Frame, fixture.Resources, fixture.Inputs);
        frame = NativeBattleHeroController.Advance(NativeBattleTestSnapshots.Independent(frame), fixture.Resources, fixture.Inputs);
        NativeCrtRandomState before = frame.Random;
        NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
        roots = NativeBattleTaskFixture.EditPool(roots, data => data with { Slots = roots.Slots.SetItem(0, roots.Slots[0].WriteWord(12, 1)) });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, roots) } };
        frame = NativeBattleHeroController.Advance(NativeBattleTestSnapshots.Independent(frame), fixture.Resources, fixture.Inputs);
        Assert.Equal(before, frame.Random);
        Assert.Equal((ushort)2, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal((ushort)2, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Single(frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void HolyWar_staggers_each_target_and_contacts_every_matching_physical_row()
    {
        var fixture = Fixture(310, [3, 3, 4]);
        NativeBattleHeroFrame frame = fixture.Frame;
        List<(int Tick, int Index)> contacts = [];
        int tick = 0;
        NativeBattleHeroInputs inputs = fixture.Inputs with { Execute = (current, operation) =>
        {
            if (operation is NativeBattleHeroContact contact)
            {
                contacts.Add((tick, contact.Index));
            }
            return current;
        } };
        for (tick = 1; tick <= 51; tick++)
        {
            frame = NativeBattleHeroController.Advance(NativeBattleTestSnapshots.Independent(frame), fixture.Resources, inputs);
            if (tick == 6)
            {
                Assert.Single(frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
            }
            if (tick == 7)
            {
                Assert.Equal(2, frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
            }
        }
        Assert.Equal(new[] { (34, 0), (34, 1), (39, 0), (39, 1), (44, 2) }, contacts);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void Current_stack_unknown_or_another_frame_fails_and_explicit_CRT_contract_only_covers_real_spawn()
    {
        var fixture = Fixture(309, [3]);
        NativeBattleHeroFrame first = NativeBattleHeroController.Advance(fixture.Frame, fixture.Resources, fixture.Inputs);
        NativeBattleHeroStackInput missing = fixture.Inputs.Stack with { CurrentWord = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleHeroController.Advance(first, fixture.Resources, fixture.Inputs with { Stack = missing }));
        NativeBattleExecutionStack execution = missing.Execution!;
        NativeBattleHeroCrtStackContract contract = new(NativeBattleHeroCrtStackContract.Win32X86Profile, execution.CaptureSha256, execution.CapturedThreadId);
        NativeBattleHeroInputs admitted = fixture.Inputs with { Stack = missing with { Crt = contract } };
        NativeBattleHeroFrame spawned = NativeBattleHeroController.Advance(first, fixture.Resources, admitted);
        Assert.Throws<NotSupportedException>(() => NativeBattleHeroController.Advance(spawned, fixture.Resources, admitted));
        Assert.Throws<InvalidDataException>(() => (fixture.Inputs.Stack with { Callback = 1 }).RequireScale(false));
        Assert.Throws<NotSupportedException>(() => (missing with { Crt = contract with { Profile = "unverified" } }).RequireScale(true));
        Assert.Throws<NotSupportedException>(() => (fixture.Inputs.Stack with { CurrentWord = NativeBattleStackBytes.Unknown("common47CF60:0", -0x88, 4) }).RequireScale(false));
    }

    [Fact]
    public void Entry_and_startup_yield_do_not_require_a_phantom_execution_frame()
    {
        var fixture = Fixture(309, [3]);
        NativeBattleHeroInputs unknown = fixture.Inputs with { Stack = new(null, 0, null, null) };
        NativeBattleHeroFrame first = NativeBattleHeroController.Advance(fixture.Frame, fixture.Resources, unknown);
        Assert.Equal((ushort)1, first.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Throws<NotSupportedException>(() => NativeBattleHeroController.Advance(first, fixture.Resources, unknown));
    }

    [Fact]
    public void Lazy_stack_resolver_cannot_fall_back_to_a_precreation_word_after48CRT()
    {
        var fixture = Fixture(309, [3]);
        List<bool> phases = [];
        NativeBattleHeroStackInput selected = fixture.Inputs.Stack with { ResolveCurrentWord = initialized =>
        {
            phases.Add(initialized);
            return initialized ? null : fixture.Inputs.Stack.CurrentWord;
        } };
        Assert.True(selected.RequireScale(false));
        Assert.Throws<NotSupportedException>(() => selected.RequireScale(true));
        Assert.Equal(new[] { false, true }, phases);
        NativeBattleExecutionStack execution = selected.Execution!;
        selected = selected with { Crt = new(NativeBattleHeroCrtStackContract.Win32X86Profile, execution.CaptureSha256, execution.CapturedThreadId) };
        Assert.True(selected.RequireScale(true));
        Assert.Equal(2, phases.Count);
    }

    [Theory]
    [InlineData(309)]
    [InlineData(310)]
    public void Anchor_observer_runs_after_child_allocation_and_preserves_its_published_frame_memory(ushort effect)
    {
        var fixture = Fixture(effect, [3]);
        List<string> order = [];
        NativeBattleHeroInputs inputs = fixture.Inputs with
        {
            ObserveAnchor571400 = (current, actor) =>
            {
                Assert.Equal((byte)3, actor);
                NativeBattleModulePoolTask child = current.Effect.Pools[1].Slots[0];
                Assert.Equal(current.Profile.Child, child.Callback);
                Assert.Equal((ushort)0, child.ReadWord(12));
                Assert.Equal((ushort)3072, child.ReadWord(14));
                order.Add("observe");
                return current with { Memory = current.Memory with { SoundPendingFlags = 0x100 } };
            },
            ReadAnchor571400 = actor =>
            {
                order.Add("read");
                return fixture.Inputs.ReadAnchor571400(actor);
            },
            Execute = (current, _) =>
            {
                Assert.Equal(0x100u, current.Memory.SoundPendingFlags);
                return current;
            }
        };
        NativeBattleHeroFrame first = NativeBattleHeroController.Advance(fixture.Frame, fixture.Resources, inputs);
        Assert.Empty(order);
        NativeBattleHeroFrame frame = NativeBattleHeroController.Advance(first, fixture.Resources, inputs);
        Assert.Equal(new[] { "observe", "read" }, order);
        Assert.Equal(0x100u, frame.Memory.SoundPendingFlags);
    }

    private static (NativeBattleHeroFrame Frame, NativeBattleHeroResources Resources, NativeBattleHeroInputs Inputs, List<NativeBattleHeroOperation> Operations) Fixture(ushort effect, byte[] actors)
    {
        NativeBattleHeroProfile profile = NativeBattleHeroProfile.ForEffect(effect);
        NativeBattleHeroResources resources = new(effect, Source(profile.MainSource, 0x888, 4, 40), Source(profile.OrbitSource, 0x7BC, 3, 18), Fan(), 300188);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(4, 0, 0, 0, 18, effect, null));
        foreach (byte actor in actors)
        {
            (packets, _) = packets.AppendContact(new(new(actor, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(checked((byte)actors.Length));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(effect - 1);
        byte[] tim = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(20), 12);
        memory = memory with { Arena = memory.Arena!.Write(256, tim).Write(0x1000, Enumerable.Repeat((byte)0xA5, profile.ChildStride * profile.ChildCapacity).ToArray()) };
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 0, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.InitializerGlobal, 256)] };
        NativeBattleHeroFrame frame = NativeBattleHeroController.Initialize(memory, selected, new(1), resources, 0x1000);
        frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, frame.Effect.Pools) };
        NativeBattleExecutionStack execution = new(0x200000, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('A', 64), 1, 2);
        NativeBattleStackBytes word = NativeBattleStackBytes.Unknown("common47CF60:0", -0x88, 4).StoreUInt32(-0x88, 0x1A7904C, 0x4B04C6, "vitals");
        List<NativeBattleHeroOperation> operations = [];
        NativeBattleHeroInputs inputs = new(packets, actor => new(actor, 2) { MinimumY146 = -101, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, -30, 4096) }, _ => new(100, 200, 300, 0x1234), new(execution, 0, word, null), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        return (frame, resources, inputs, operations);
    }

    private static NativeBattleGfAnimationSource Source(uint address, int length, int count, int duration)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 0x20);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x24), duration);
        for (int index = 0; index < count; index++)
        {
            int node = 0x60 + index * 8;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x28 + index * 4), node - 0x20);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(node), 0x200);
        }
        return new(address, bytes.ToImmutableArray());
    }

    private static ImmutableArray<byte> Fan()
    {
        byte[] bytes = new byte[0x2B8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 28);
        return bytes.ToImmutableArray();
    }
}
