using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect207ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena + 0x800;

    [Fact]
    public void First_root_callback_selects_second_packet_bank_and_does_not_initialize_children_early()
    {
        NativeBattleEffect207Frame frame = Frame(0, false);
        Harness harness = new();
        NativeBattleEffect207Step step = AdvanceRoot6A3B70(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + 0x11A2Cu, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1u, Read(step.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Single(step.Frame.Effect.Pools);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Root_does_not_inherit_graphics_pause_and_stops_before_increment_when_main_is_empty()
    {
        NativeBattleEffect207Frame frame = Frame(7);
        NativeBattleEffectTaskPool main = frame.Effect.Pools[1] with { Head = null, Tail = null };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, main) } };
        NativeBattleEffect207Step step = AdvanceRoot6A3B70(frame, RootBank, null!, new Harness { Flags = 0x201 }.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(7, Read(step.Frame, RootBank).ReadWord(12));
    }

    [Fact]
    public void Wrapped_age_one_repeats_clear_then_origin_before_scanning_existing_children()
    {
        NativeBattleEffect207Frame frame = Frame(81);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        Harness harness = new();
        NativeBattleEffect207Step step = AdvanceRoot6A3B70(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect207Clear45BEE0(576, 256, 320, 256, 0, 0, 0), harness.Operations[0]);
        Assert.Equal(new NativeBattleEffect207SetOrigin45B5E0(576, 256), harness.Operations[1]);
        Assert.Equal(0, step.Result);
        Assert.Equal(2, Read(step.Frame, RootBank).ReadWord(12));
    }

    [Fact]
    public void Age80_reads_current_contact_header_and_queues_actual_first_bank_completion_once()
    {
        Harness harness = new();
        NativeBattleEffect207Step step = AdvanceMain6A3D90(Frame(80), Main, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect207AllContacts506BA0(0, 1), harness.Operations[0]);
        Assert.Equal(new NativeBattleEffect207AppendCommonTextureLoad508630(Arena + 0x1A2C, Main + 16), harness.Operations[1]);
        Assert.Equal(81, Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(0, step.Result);
        NativeBattleEffect207Step next = AdvanceMain6A3D90(step.Frame, Main, null!, harness.Inputs());
        Assert.Equal(2, harness.Operations.Count);
        Assert.Equal(81, Read(next.Frame, Main).ReadWord(12));
        Assert.Equal(0, next.Result);
    }

    [Fact]
    public void Completion_consumes_byte_not_word_and_survives_json_boundary()
    {
        NativeBattleEffect207Frame frame = WriteCompletionByte(Frame(81), Main + 17, 1);
        Assert.Equal(0, AdvanceMain6A3D90(frame, Main, null!, new Harness().Inputs()).Result);
        frame = WriteCompletionByte(frame, Main + 16, 0x80);
        frame = JsonSerializer.Deserialize<NativeBattleEffect207Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleEffect207Step step = AdvanceMain6A3D90(frame, Main, null!, new Harness().Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(81, Read(step.Frame, Main).ReadWord(12));
    }

    [Fact]
    public void Completion_can_arrive_synchronously_without_a_fixed_clock_delay()
    {
        Harness harness = new() { CompleteTexture = true };
        NativeBattleEffect207Step step = AdvanceMain6A3D90(Frame(80), Main, null!, harness.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(81, Read(step.Frame, Main).ReadWord(12));
    }

    [Fact]
    public void Sixteen_birth_batches_fill_exact_capacity_and_exhaustion_does_not_draw_random_values()
    {
        NativeBattleEffect207Frame frame = Frame(48);
        frame = Write(frame, Main, Read(frame, Main).WriteWord(40, 100).WriteWord(42, 200).WriteWord(44, 300));
        NativeCrtRandomState expected = frame.Random;
        for (int batch = 0; batch < 16; batch++)
        {
            frame = BirthParticles(frame, Main);
        }
        for (int draw = 0; draw < 64 * 3; draw++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffectTaskPool particles = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ParticleDescriptor);
        Assert.Equal(64, particles.Slots.Count(task => task.Callback == 0x6A5650 && (task.Flags & 1) != 0));
        Assert.Equal(expected, frame.Random);
        Assert.All(particles.Slots, task => Assert.Equal(0, task.ReadWord(18)));
        NativeBattleEffect207Frame full = BirthParticles(frame, Main);
        Assert.Equal(frame.Random, full.Random);
    }

    [Fact]
    public void Root_retirement_publishes_live_orphan_payload_and_queue_without_waiting_for_it()
    {
        NativeBattleEffect207Frame frame = Frame(81);
        frame = WriteCompletionByte(frame, Main + 16, 1);
        NativeBattleEffectTaskPool particles = frame.Effect.Pools[2];
        (particles, int slot) = particles.Append(0x6A5650);
        particles = particles with { Slots = particles.Slots.SetItem(slot, particles.Slots[slot].WriteWord(30, 0xBEEF)), Head = null, Tail = null };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, particles) } };
        NativeBattleEffect207Frame retired = Advance(frame, null!, new Harness().Inputs());
        Assert.Empty(retired.Effect.Pools);
        Assert.Equal(0u, retired.Effect.ReturnedDescriptor);
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(retired.Memory.Arena!.Read(30, 2)));
        Assert.Equal(0x6A5650u, BinaryPrimitives.ReadUInt32LittleEndian(retired.Memory.Arena.Read(8, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(retired.Memory.Arena.Read(0, 2)) & 1);
        Assert.Equal(new byte[8], retired.State.ReadCommonQueue(ParticleDescriptor).Read(0, 8));
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(11, 2)]
    [InlineData(12, 0)]
    [InlineData(23, 14)]
    [InlineData(24, 16)]
    [InlineData(36, 31)]
    [InlineData(47, 18)]
    public void Palette_windows_follow_actual_piecewise_integer_arithmetic(int elapsed, int expected)
    {
        Assert.Equal(expected, PaletteIndex(elapsed));
    }

    internal static NativeBattleEffect207Frame Frame(short age, bool children = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, unchecked((ushort)age)).WriteByte(15, children ? (byte)1 : (byte)0).WriteDword(16, 0)) };
        NativeBattleEffect207State state = NativeBattleEffect207State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena);
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (children)
        {
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, Main, 0x4E8, 1);
            (main, _) = main.Append(MainCallback);
            main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteBytes(12, new byte[0x4DC]).WriteWord(12, unchecked((ushort)age))) };
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, Arena, 32, 64);
            pools = [root, main, particles];
            state = state.DetachCommonQueue(MainDescriptor).DetachCommonQueue(ParticleDescriptor);
        }
        NativeBattleOriginalEffectState effect = new(207, 206, new(207, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, pools);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    internal sealed class Harness
    {
        public uint Flags { get; init; }
        public bool CompleteTexture { get; init; }
        public List<NativeBattleEffect207Operation> Operations { get; } = [];
        public NativeBattleEffect207Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets(), (_, actor) => new(actor, 2), (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => new(null, 512, 160, 108), _ => 0, (frame, actor, retained) => new(frame, retained), (frame, operation) =>
        {
            Operations.Add(operation);
            return operation is NativeBattleEffect207AppendCommonTextureLoad508630 load && CompleteTexture ? WriteCompletionByte(frame, load.CompletionAddress, 1) : frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 11, -1, 365, 207, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
