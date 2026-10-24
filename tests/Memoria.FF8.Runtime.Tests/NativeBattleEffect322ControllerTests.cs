using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect322Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect322ControllerTests
{
    private const uint Arena = 0x20DFAB8;

    [Fact]
    public void Root_selects_second_packet_bank_before_initialization_without_io()
    {
        Harness harness = new();
        NativeBattleEffect322Step result = AdvanceRoot65AC20(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1u, Read(result.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Empty(harness.Operations);
        Assert.Equal(0, harness.Polls);
    }

    [Fact]
    public void File_queue_occurs_after_all_pool_scans_at_old_age_59()
    {
        Harness harness = new();
        NativeBattleEffect322Frame frame = Frame(59);
        (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, main + 12, 0x1C);
        harness.OnOperation = (current, operation) =>
        {
            if (operation is NativeBattleEffect322IoQueue5341D0)
            {
                Assert.Equal(1, Read(current, main).ReadWord(12));
            }
        };
        NativeBattleEffect322Step result = AdvanceRoot65AC20(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(60, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(new NativeBattleEffect322IoQueue5341D0(360, Arena + ThirdPacketOffset, 0), harness.Operations[0]);
        Assert.IsType<NativeBattleEffect322IoAdvance534210>(harness.Operations[1]);
        Assert.Equal(0, harness.Polls);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public void Root_waits_for_actual_io_completion_and_holds_age_60(bool complete, int expected)
    {
        Harness harness = new() { Complete = complete };
        NativeBattleEffect322Step result = AdvanceRoot65AC20(Frame(60), RootBank, null!, harness.Inputs());
        Assert.Equal(expected, result.Result);
        Assert.Equal(60, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(1, harness.Polls);
        Assert.IsType<NativeBattleEffect322IoAdvance534210>(Assert.Single(harness.Operations));
        Assert.Equal(123u, result.Frame.State.ReadDword(InitializerGlobal));
    }

    [Fact]
    public void Retirement_returns_current_unknown_common_bytes_and_root_exclusive_bank()
    {
        NativeBattleEffect322Frame frame = Frame(60);
        frame = WriteBytes(frame, Arena + 0x88, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1), 4);
        NativeBattleEffect322Frame result = Advance(frame, null!, new Harness { Complete = true }.Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        (byte[] bytes, bool[] known) = result.Memory.Arena!.ReadSnapshot(0x88, 4);
        Assert.Equal(new[] { true, true, false, true }, known);
        Assert.Equal(new byte[] { 1, 2, 4 }, bytes.Where((_, index) => known[index]).ToArray());
        NativeBattleEffect322Frame restored = JsonSerializer.Deserialize<NativeBattleEffect322Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(known, restored.Memory.Arena!.ReadSnapshot(0x88, 4).Known);
    }

    [Theory]
    [InlineData(14, 0, 2)]
    [InlineData(33, 0, 3892)]
    [InlineData(34, 1, 512)]
    [InlineData(35, 1, 2560)]
    [InlineData(36, 2, 512)]
    [InlineData(37, 2, 2560)]
    [InlineData(38, 3, 102)]
    [InlineData(56, 3, 3788)]
    public void Path_parameters_preserve_three_native_ranges(int age, int segment, int parameter)
    {
        Assert.Equal((segment, parameter), PathParameter(age));
    }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(29, 3840)]
    [InlineData(30, 4096)]
    [InlineData(50, 4096)]
    [InlineData(51, 3584)]
    [InlineData(56, 1024)]
    public void Mesh_strength_keeps_zero_birth_and_late_half_step_fade(int age, int strength)
    {
        Assert.Equal(strength, MeshStrength(age));
    }

    [Fact]
    public void Bone_refresh_at_30_occurs_after_the_current_path_sample()
    {
        Harness harness = new();
        NativeBattleEffect322Frame frame = AddMain(Frame(10), 30);
        harness.OnSample = (_, _, _, _) =>
        {
            Assert.Equal(0, harness.PointQueries);
            throw new SampleObservedException();
        };
        Assert.Throws<SampleObservedException>(() => AdvanceMain65AE00(frame, Arena, null!, harness.Inputs()));
    }

    [Fact]
    public void Age_13_copies_full_current_point_and_preserves_stateful_query_publication()
    {
        Harness harness = new();
        NativeBattleEffect322Step result = AdvanceMain65AE00(AddMain(Frame(10), 13), Arena, null!, harness.Inputs());
        Assert.Equal(1, harness.PointQueries);
        Assert.Equal(14, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(new short[] { 100, -200, 300 }, new[] { Vector(TaskView(Read(result.Frame, Arena), 0x64, 8)).X, Vector(TaskView(Read(result.Frame, Arena), 0x64, 8)).Y, Vector(TaskView(Read(result.Frame, Arena), 0x64, 8)).Z });
        Assert.Equal(TaskView(Read(result.Frame, Arena), 0x64, 8).Read(0, 8), TaskView(Read(result.Frame, Arena), 0x6C, 8).Read(0, 8));
        Assert.Equal(777u, result.Frame.State.ReadDword(InitializerGlobal));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Burst_full_pool_consumes_no_random_values()
    {
        NativeBattleEffect322Frame frame = AddMain(Frame(10), 36);
        for (int index = 0; index < 32; index++)
        {
            (frame, _) = Append(frame, BurstDescriptor, 0x65B680);
        }
        NativeBattleEffect322Frame result = BirthBurst(frame, Arena);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Burst_success_consumes_two_draws_and_preserves_velocity_padding()
    {
        NativeBattleEffect322Frame frame = AddMain(Frame(10), 36);
        for (int index = 0; index < 31; index++)
        {
            (frame, _) = Append(frame, BurstDescriptor, 0x65B680);
        }
        uint final = Arena + 0x424 + 31 * 0x2C;
        frame = WriteBytes(frame, final + 0x1A, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x34, 0x12 }).MarkUnknown(1, 1), 2);
        NativeCrtRandomState expected = frame.Random.Draw(out _).Draw(out _);
        NativeBattleEffect322Frame result = BirthBurst(frame, Arena);
        Assert.Equal(expected, result.Random);
        Assert.Equal(new[] { true, false }, TaskView(Read(result, final), 0x1A, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(new[] { true, false }, TaskView(Read(result, final), 0x2A, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(0, Read(result, final).ReadWord(18));
        Assert.Equal(0x34, TaskView(Read(result, final), 0x1A, 2).ReadSnapshot(0, 2).Data[0]);
    }

    private static NativeBattleEffect322Frame AddMain(NativeBattleEffect322Frame frame, int age)
    {
        (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, main + 12, 0x1C);
        return Write(frame, main, Read(frame, main).WriteWord(12, (ushort)age));
    }

    private static NativeBattleEffect322Frame Frame(int age, bool initialized = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)age).WriteByte(15, initialized ? (byte)1 : (byte)0)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                pools = pools.Add(NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity));
            }
        }
        NativeBattleEffect322State state = NativeBattleEffect322State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(4));
        NativeBattleOriginalEffectState effect = new(322, 321, new(322, Entry, Initializer), new(4, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, pools), effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class SampleObservedException : Exception
    {
    }

    private sealed class Harness
    {
        public bool Complete { get; init; }
        public int Polls { get; private set; }
        public int PointQueries { get; private set; }
        public List<NativeBattleEffect322Operation> Operations { get; } = [];
        public Action<NativeBattleEffect322Frame, NativeBattleEffect322Operation>? OnOperation { get; set; }
        public Action<NativeBattleEffect322Frame, NativeBattleEffectArena, int, int>? OnSample { get; set; }

        public NativeBattleEffect322Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattleActionPacketsState.Fresh(), (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, selector, fraction, retained) =>
        {
            Assert.Equal(24, selector);
            Assert.Equal(2048, fraction);
            PointQueries++;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 777) }, PutPoint(retained, 0, new(100, -200, 300)));
        }, (frame, _, retained) => new(frame, retained), (frame, controls, segment, parameter, retained) =>
        {
            OnSample?.Invoke(frame, controls, segment, parameter);
            return new(frame, retained);
        }, (frame, index) =>
        {
            Assert.Equal(0, index);
            Polls++;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 123) }, Complete);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            OnOperation?.Invoke(frame, operation);
            return frame;
        });
    }
}
