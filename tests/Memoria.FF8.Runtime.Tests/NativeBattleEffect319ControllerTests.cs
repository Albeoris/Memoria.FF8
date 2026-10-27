using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect319Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect319Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect319ControllerTests
{
    private const uint Arena = 0x20DFAB8;

    [Fact]
    public void First_root_callback_selects_second_bank_without_child_or_io_consumption()
    {
        Harness harness = new();
        NativeBattleEffect319Step first = AdvanceRoot65DBE0(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, first.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1u, Read(first.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(first.Frame, RootBank).ReadWord(12));
        Assert.Single(first.Frame.Effect.Pools);
        Assert.Empty(harness.Operations);
        Assert.Equal(0, harness.Polls);
    }

    [Theory]
    [InlineData(false, 0, 65)]
    [InlineData(true, 2, 17)]
    public void Root_drains_all_pools_and_waits_actual_complete_result(bool complete, int expectedResult, int expectedAge)
    {
        Harness harness = new() { Complete = complete };
        NativeBattleEffect319Step result = AdvanceRoot65DBE0(Frame(17), RootBank, null!, harness.Inputs());
        Assert.Equal(expectedResult, result.Result);
        Assert.Equal(expectedAge, Read(result.Frame, RootBank).ReadWord(12));
        Assert.IsType<NativeBattleEffect319Advance534210>(Assert.Single(harness.Operations));
        Assert.Equal(1, harness.Polls);
        Assert.Equal(0, harness.LastPollIndex);
        Assert.Equal(123u, result.Frame.State.ReadDword(InitializerGlobal));
    }

    [Fact]
    public void Live_delayed_bone_prevents_io_select_and_applies_current_zero_scale()
    {
        NativeBattleEffect319Frame frame = Frame(65);
        NativeBattleEffectTaskPool bones = frame.Effect.Pools[2];
        (bones, int slot) = bones.Append(0x65F2B0);
        bones = bones.WriteSlot(slot, bones.Slots[slot].WriteBytes(12, new byte[16]).WriteDword(12, 0x12345678).WriteWord(16, 8).WriteWord(20, 1024).WriteWord(24, 512));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, bones) } };
        Harness harness = new() { Complete = true };
        NativeBattleEffect319Step result = AdvanceRoot65DBE0(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(0, harness.Polls);
        Assert.Equal(new NativeBattleEffect319ScaleBone56BEF0(0x12345678, 0, 0), harness.Operations[0]);
        Assert.Equal(7, result.Frame.Effect.Pools[2].Slots[slot].ReadWord(16));
    }

    [Fact]
    public void Retirement_returns_current_common_unknownness_and_exclusive_root_bank()
    {
        Harness harness = new() { Complete = true };
        NativeBattleEffect319Frame frame = Frame(65);
        NativeBattleEffectArena payload = NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1);
        frame = WriteBytes(frame, Arena + 16, payload, 4);
        NativeBattleEffect319Frame result = Advance(frame, null!, harness.Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        (byte[] data, bool[] known) = result.Memory.Arena!.ReadSnapshot(16, 4);
        Assert.Equal(new[] { true, true, false, true }, known);
        Assert.Equal(new byte[] { 1, 2, 4 }, data.Where((_, index) => known[index]).ToArray());
        NativeBattleEffect319Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect319Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(known, restored.Memory.Arena!.ReadSnapshot(16, 4).Known);
    }

    [Fact]
    public void Sound_is_emitted_once_at_age_one_without_current_model_access()
    {
        Harness harness = new();
        NativeBattleEffect319Step result = AdvanceMain65DDA0(Frame(1), Arena, null!, harness.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(2, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(new NativeBattleEffect319Sound501330(SoundAddress, 0, 128), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Contact_reads_the_current_headers_absolute_first_index()
    {
        Harness harness = new();
        NativeBattleEffect319Frame frame = Frame(12);
        harness.Packets = harness.Packets with { Headers = harness.Packets.Headers.SetItem(0, harness.Packets.Headers[0] with { FirstContactIndex = 7 }) };
        EmitMainEvents(frame, 12, harness.Inputs());
        Assert.Equal(new NativeBattleEffect319Contact506690(7), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Bone_births_store_real_record_pointers_with_original_delay_and_step_order()
    {
        Harness harness = new();
        NativeBattleEffect319Frame frame = EmitMainEvents(Frame(27), 27, harness.Inputs());
        Assert.Equal(new NativeBattleEffect319MeshMask(ModelAddress(3), uint.MaxValue, 2), Assert.Single(harness.Operations));
        Assert.Equal(new[] { 10, 13, 23, 24, 25 }, harness.Bones);
        NativeBattleEffectTaskPool bones = frame.Effect.Pools[2];
        Assert.Equal(5, bones.Slots.Count(task => (task.Flags & 1) != 0));
        int[] delays = [0, 8, 16, 16, 16];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleModulePoolTask task = bones.Slots[index];
            Assert.Equal((uint)(0x100010 + 48 * harness.Bones[index]), task.ReadDword(12));
            Assert.Equal(delays[index], task.ReadWord(16));
            Assert.Equal(1024, task.ReadWord(20));
            Assert.Equal(512, task.ReadWord(24));
            Assert.Equal(0, task.ReadWord(18));
            Assert.Equal(0, task.ReadWord(22));
        }
    }

    [Fact]
    public void Mandatory_bone_allocation_exhaustion_fails_instead_of_silently_skipping()
    {
        Harness harness = new();
        NativeBattleEffect319Frame frame = EmitMainEvents(Frame(27), 27, harness.Inputs());
        Assert.Throws<InvalidDataException>(() => EmitMainEvents(frame, 27, harness.Inputs()));
    }

    [Fact]
    public void Optional_sprite_births_consume_three_crt_values_per_success()
    {
        NativeBattleEffect319Frame frame = Frame(15);
        frame = Write(frame, Arena, CopyTask(Read(frame, Arena), 0x64, PointView(new(100, -200, 300)), 8));
        NativeCrtRandomState expected = frame.Random;
        ushort[] values = new ushort[12];
        for (int index = 0; index < values.Length; index++)
        {
            expected = expected.Draw(out values[index]);
        }
        NativeBattleEffect319Frame result = BirthSprites(frame, Arena);
        Assert.Equal(expected, result.Random);
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModulePoolTask task = result.Effect.Pools[3].Slots[index];
            Assert.Equal(unchecked((ushort)(100 + (values[index * 3] & 1023) - 512)), task.ReadWord(16));
            Assert.Equal(unchecked((ushort)(-200 + (values[index * 3 + 1] & 1023) - 512)), task.ReadWord(18));
            Assert.Equal(unchecked((ushort)(300 + (values[index * 3 + 2] & 1023) - 512)), task.ReadWord(20));
            Assert.Equal(0, task.ReadWord(12));
        }
    }

    [Fact]
    public void Full_sprite_pool_consumes_no_crt_values()
    {
        NativeBattleEffect319Frame frame = Frame(15);
        NativeBattleEffectTaskPool sprites = frame.Effect.Pools[3];
        while (sprites.Slots.Any(task => (task.Flags & 1) == 0))
        {
            (sprites, _) = sprites.Append(0x65E430);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(3, sprites) } };
        NativeBattleEffect319Frame result = BirthSprites(frame, Arena);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.Effect.Pools, result.Effect.Pools);
    }

    [Fact]
    public void Signed_terminal_check_queues_actual_file_destination_after_increment()
    {
        Harness harness = new();
        NativeBattleEffect319Step result = AdvanceMain65DDA0(Frame(39), Arena, null!, harness.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(40, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(new NativeBattleEffect319Queue5341D0(360, Arena + ThirdPacketOffset, 0), Assert.Single(harness.Operations));
    }

    private static NativeBattleEffect319Frame Frame(int age, bool initialized = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)age).WriteByte(14, initialized ? (byte)1 : (byte)0));
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity);
                pools = pools.Add(pool);
            }
        }
        NativeBattleEffect319State state = NativeBattleEffect319State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(3));
        NativeBattleOriginalEffectState effect = new(319, 318, new(319, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools);
        NativeBattleEffect319Frame frame = new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        if (initialized)
        {
            NativeBattleEffectTaskPool main = frame.Effect.Pools[1];
            (main, _) = main.Append(MainCallback);
            main = main.WriteSlot(0, main.Slots[0].WriteBytes(12, new byte[main.Stride - 12]).WriteWord(12, (ushort)age));
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, main) } };
            if (age == 17 || age == 65)
            {
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, main.UpdateLinks(null, null)) } };
            }
        }
        return frame;
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect319Operation> Operations { get; } = [];
        public List<int> Bones { get; } = [];
        public bool Complete { get; init; }
        public int Polls { get; private set; }
        public int LastPollIndex { get; private set; }
        public NativeBattleActionPacketsState Packets { get; set; } = NativeBattleActionPacketsState.Fresh();
        public NativeBattleEffect319Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets, (_, _) => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, _, _, retained) => new(frame, retained), (frame, _, retained) => new(frame, retained), (_, _, bone) =>
        {
            Bones.Add(bone);
            return (uint)(0x100010 + 48 * bone);
        }, (frame, index) =>
        {
            Polls++;
            LastPollIndex = index;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 123) }, Complete);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
