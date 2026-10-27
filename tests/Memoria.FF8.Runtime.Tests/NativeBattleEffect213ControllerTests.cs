using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect213Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect213ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena + 0x1614;

    [Fact]
    public void First_root_callback_selects_second_bank_before_child_initialization()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect213Step first = AdvanceRoot69EB70(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, first.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1u, Read(first.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(first.Frame, RootBank).ReadWord(12));
        Assert.Single(first.Frame.Effect.Pools);
        NativeBattleEffect213Frame later = Write(first.Frame, RootBank, Read(first.Frame, RootBank).WriteWord(12, 2));
        NativeBattleEffect213Step second = AdvanceRoot69EB70(later, RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + FirstPacketOffset, second.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(0u, Read(second.Frame, RootBank).ReadDword(16));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Initialized_root_retires_before_increment_only_after_all_four_pools_drain()
    {
        NativeBattleEffect213Frame frame = Frame(7, true, false);
        NativeBattleEffect213Step result = AdvanceRoot69EB70(frame, RootBank, null!, new Harness { Flags = 0x201 }.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(7, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(5, result.Frame.Effect.Pools.Length);
    }

    [Fact]
    public void Retirement_returns_current_common_unknown_bytes_and_root_exclusive_owner()
    {
        NativeBattleEffect213Frame frame = Frame(7, true, false);
        uint point = Arena + CommonPools[3].Bank + 16;
        frame = WriteBytes(frame, point, NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]).MarkUnknown(2, 1), 4);
        NativeBattleEffect213Frame result = Advance(frame, null!, new Harness().Inputs());
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Empty(result.Effect.Pools);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        var snapshot = result.Memory.Arena!.ReadSnapshot((int)(point - Arena), 4);
        Assert.Equal(new byte[] { 1, 2, 4 }, snapshot.Data.Where((_, index) => snapshot.Known[index]));
        Assert.Equal(new[] { true, true, false, true }, snapshot.Known);
    }

    [Fact]
    public void Normalization_copies_all_rows_and_divides_raw_z_without_subtracting_minimum()
    {
        byte[] bytes = new byte[MutableStreamLength];
        for (int index = 0; index < StreamVertexCount; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8), (short)(index * 2));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 4), (short)(index + 1));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 8 + 6), 0xBEEF);
        }
        NativeBattleEffect213Frame frame = Frame(0, false);
        frame = frame with { Staging = frame.Staging!.Write(MutableStreamAddress, bytes) };
        frame = InitializeStreamCopy(frame);
        NativeBattleEffectArena copy = ReadBytes(frame, Arena + StreamCopyOffset, MutableStreamLength);
        Assert.Equal(4096 / 307, BinaryPrimitives.ReadInt16LittleEndian(copy.Read(6, 2)));
        Assert.Equal((308 << 12) / 307, BinaryPrimitives.ReadInt16LittleEndian(copy.Read(MutableStreamLength - 2, 2)));
        Assert.Equal(bytes, frame.Staging!.Read(MutableStreamAddress, MutableStreamLength));
        Assert.Equal(bytes.AsSpan(0, 6).ToArray(), copy.Read(0, 6));
    }

    [Fact]
    public void Target_sort_preserves_original_contact_indices_and_publishes_count_frame()
    {
        Harness harness = new() { Targets = [0, 2, 1] };
        NativeBattleEffect213Frame frame = InitializeTargets(Frame(0), Main, harness.Inputs());
        NativeBattleModulePoolTask task = Read(frame, Main);
        Assert.Equal(ModelAddress(2), task.ReadDword(16));
        Assert.Equal(ModelAddress(1), task.ReadDword(20));
        Assert.Equal(ModelAddress(0), task.ReadDword(24));
        Assert.Equal(new byte[] { 1, 2, 0 }, new[] { task.ReadByte(0x1C), task.ReadByte(0x1D), task.ReadByte(0x1E) });
        Assert.Equal((3 << 12) / 40, task.ReadWord(0x26));
        Assert.Equal(777, task.ReadWord(0x2C));
        Assert.Equal(123u, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(ModelAddress(2), Assert.Single(harness.Counts));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void Initial_next_index_uses_actual_contact_count(int count, int next)
    {
        Harness harness = new() { Targets = Enumerable.Range(0, count).ToArray() };
        NativeBattleEffect213Frame frame = InitializeTargets(Frame(0), Main, harness.Inputs());
        Assert.Equal(next, Read(frame, Main).ReadWord(0x22));
        Assert.Equal(count, Read(frame, Main).ReadWord(14));
    }

    [Fact]
    public void Contact_crossing_uses_previous_fraction_then_original_index_and_current_next_count()
    {
        Harness harness = new() { Targets = [0, 1] };
        NativeBattleEffect213Frame frame = InitializeTargets(Frame(0), Main, harness.Inputs());
        NativeBattleModulePoolTask task = Read(frame, Main).WriteWord(0x24, 4000).WriteWord(0x26, 200);
        frame = Write(frame, Main, task);
        frame = JsonSerializer.Deserialize<NativeBattleEffect213Frame>(JsonSerializer.Serialize(frame))!;
        (frame, NativeBattleVector point) = InterpolateTarget(frame, Main, 5, harness.Inputs());
        Assert.Equal((short)(100 + ((0 - 100) * 4000 >> 12)), point.X);
        Assert.Equal(new NativeBattleEffect213Contact506690(1), Assert.Single(harness.Operations));
        Assert.Equal(new[] { ModelAddress(1), ModelAddress(0) }, harness.Counts);
        Assert.Equal(104, Read(frame, Main).ReadWord(0x24));
        Assert.Equal(1, Read(frame, Main).ReadWord(0x20));
        Assert.Equal(1, Read(frame, Main).ReadWord(0x22));
        Assert.Equal(2, harness.Points.Count);
        Assert.All(harness.Points, query => Assert.Equal(((short)0xF1, (short)0), (query.Selector, query.Fraction)));
    }

    [Fact]
    public void Early_interpolation_does_not_advance_fraction_or_acknowledge_contact()
    {
        Harness harness = new();
        NativeBattleEffect213Frame frame = InitializeTargets(Frame(0), Main, harness.Inputs());
        frame = Write(frame, Main, Read(frame, Main).WriteWord(0x24, 4000).WriteWord(0x26, 200));
        (frame, _) = InterpolateTarget(frame, Main, 4, harness.Inputs());
        Assert.Equal(4000, Read(frame, Main).ReadWord(0x24));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Full_particle_pool_skips_mouth_payload_and_all_conditional_random_draws()
    {
        NativeBattleEffect213Frame frame = FullParticles();
        NativeCrtRandomState random = frame.Random;
        frame = BirthMouth(frame, NativeBattleCuragaMatrices.Identity8DD770(), 100);
        Assert.Equal(random, frame.Random);
    }

    [Fact]
    public void Full_particle_pool_keeps_only_the_unconditional_vertex_selection_random_draw()
    {
        NativeBattleEffect213Frame frame = FullParticles();
        NativeCrtRandomState expected = frame.Random.Draw(out _);
        frame = Write(frame, Main, Read(frame, Main).WriteWord(0x2C, 200));
        Harness harness = new();
        frame = BirthAtVertices(frame, Main, 200, 300, 1000, harness.Inputs());
        Assert.Equal(expected, frame.Random);
        Assert.Empty(harness.Points);
    }

    [Fact]
    public void Full_particle_pool_keeps_radial_angle_draw_and_no_failed_birth_size_draws()
    {
        NativeBattleEffect213Frame frame = FullParticles();
        NativeCrtRandomState expected = frame.Random.Draw(out _);
        frame = BirthRadial(frame, 0, default, 256);
        Assert.Equal(expected, frame.Random);
    }

    [Fact]
    public void Rotation_only_load_accepts_unknown_translation_and_preserves_current_translation()
    {
        NativeBattleEffect213Frame frame = Frame(0);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = 22, Z = 33 }, 0xCAFE));
        NativeBattleEffectArena partial = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)).MarkUnknown(20, 12);
        frame = LoadMatrix(frame, Packed(partial), true, false);
        Assert.Equal(new int?[] { 11, 22, 33 }, frame.Memory.GeometryRegisters!.Translation);
        Assert.Null(frame.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(frame.Memory.GeometryRegisters.Rotation20[19]);
    }

    private static NativeBattleEffect213Frame FullParticles()
    {
        NativeBattleEffect213Frame frame = Frame(0);
        int index = 2;
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        while (pool.Slots.Any(task => (task.Flags & 1) == 0))
        {
            (pool, _) = pool.Append(0x6A0AF0);
        }
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    }

    private static NativeBattleEffect213Frame Frame(int age, bool children = true, bool main = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)age).WriteByte(15, children ? (byte)1 : (byte)0)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (children)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity);
                if (shape.Descriptor == MainDescriptor && main)
                {
                    (pool, _) = pool.Append(MainCallback);
                    pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteBytes(12, new byte[shape.Stride - 12])) };
                }
                pools = pools.Add(pool);
            }
        }
        NativeBattleEffect213State state = NativeBattleEffect213State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(3));
        NativeBattleOriginalEffectState effect = new(213, 212, new(213, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public int[] Targets { get; init; } = [0];
        public List<NativeBattleEffect213Operation> Operations { get; } = [];
        public List<uint> Counts { get; } = [];
        public List<(uint Model, short Selector, short Fraction)> Points { get; } = [];
        public NativeBattleEffect213Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets(), (_, model) => NativeBattleEffectArena.Clear().Write(0x1C, BitConverter.GetBytes((short)(((model - 0x1D972C0) / 156) * 100))), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, model, selector, fraction, retained) =>
        {
            Points.Add((model, selector, fraction));
            return new(frame, PutPoint(retained, 0, new(unchecked((short)(((model - 0x1D972C0) / 156) * 100)), 0, 0)));
        }, (frame, model) =>
        {
            Counts.Add(model);
            return new(frame with { State = frame.State.WriteDword(CursorGlobal, 123) }, 777);
        }, (frame, _, _, retained) => new(frame, retained), (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });

        private NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 14, -1, 233, 213, null));
            foreach (int actor in Targets)
            {
                (packets, _) = packets.AppendContact(new(new((byte)actor, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            (packets, _) = packets.Publish((byte)Targets.Length);
            return packets;
        }
    }
}
