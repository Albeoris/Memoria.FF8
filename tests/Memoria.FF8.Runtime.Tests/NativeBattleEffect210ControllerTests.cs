using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena + 0xA750;

    [Fact]
    public void Root_toggles_both_addressed_cursors_before_start_and_advances_its_word_clock()
    {
        Harness harness = new();
        NativeBattleEffect210Step first = AdvanceRoot6F6890(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + 0x1B1B8u, first.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(Arena + 0x2B1B8u, first.Frame.State.ReadDword(SecondCursorGlobal));
        Assert.Equal(1, Read(first.Frame, RootBank).ReadWord(12));
        Assert.Equal(1, Read(first.Frame, RootBank).ReadByte(15));
        first = new(first.Frame with { State = first.Frame.State.WriteDword(FrozenGlobal, 1) }, 0);
        NativeBattleEffect210Step next = AdvanceRoot6F6890(first.Frame, RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + 0xB1B8u, next.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(Arena + 0x1B1B8u, next.Frame.State.ReadDword(SecondCursorGlobal));
        Assert.Equal(1, Read(next.Frame, RootBank).ReadWord(12));
        Assert.Single(next.Frame.Effect.Pools);
    }

    [Fact]
    public void Actual_age_one_initializes_main_once_and_saves_current_contact_point_with_W()
    {
        Harness harness = new();
        NativeBattleEffect210Step first = AdvanceRoot6F6890(Frame(1, false), RootBank, null!, harness.Inputs());
        Assert.Equal(1, Read(first.Frame, RootBank).ReadByte(14));
        Assert.Equal(0x1D972C0u, Read(first.Frame, Main).ReadDword(16));
        Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0, 0xEF, 0xBE }, ReadBytes(first.Frame, Main + 20, 8).Read(0, 8));
        Assert.Equal(new NativeBattleVector(100, 200, 300), Vector(ReadBytes(first.Frame, Main + 0x58, 6)));
        Assert.Equal(new NativeBattleEffect210IoInitialize534110(Arena + 0xA708), harness.Operations[0]);
        NativeBattleEffect210Frame frozen = first.Frame with { State = first.Frame.State.WriteDword(FrozenGlobal, 1) };
        NativeBattleEffect210Step next = AdvanceRoot6F6890(frozen, RootBank, null!, harness.Inputs());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect210IoInitialize534110>());
        Assert.Equal(2, Read(next.Frame, Main).ReadWord(12));
    }

    [Fact]
    public void Early_queue_clear_preserves_orphans_then_the_ACT_pool_reuses_the_same_physical_bytes()
    {
        NativeBattleEffect210Frame frame = InitializeCommonPool(Frame(33), EarlyDescriptor);
        (frame, uint child) = Append(frame, EarlyDescriptor, 0x6F6C00);
        frame = Write(frame, child, Read(frame, child).WriteWord(30, 0xBEEF));
        NativeBattleEffect210Step step = AdvanceMain6F7310(frame, Main, null!, new Harness().Inputs());
        Assert.DoesNotContain(step.Frame.Effect.Pools, pool => pool.DescriptorAddress == EarlyDescriptor);
        Assert.Equal(new byte[8], step.Frame.State.ReadCommonQueue(EarlyDescriptor).Read(0, 8));
        Assert.Equal(0x6F6C00u, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Arena!.Read(8, 4)));
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(step.Frame.Memory.Arena.Read(30, 2)));
        NativeBattleEffect210Frame replacement = InitializeCommonPool(step.Frame, ActDescriptor);
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(replacement, Arena + 30, 2).Read(0, 2)));
        Assert.Equal(0, AdvancePool(replacement, EarlyDescriptor, null!, new Harness().Inputs()).Survivors);
    }

    [Fact]
    public void Overlapping_pools_cannot_be_owned_independently()
    {
        NativeBattleEffect210Frame frame = InitializeCommonPool(Frame(33), EarlyDescriptor);
        Assert.Throws<InvalidDataException>(() => InitializeCommonPool(frame, ActDescriptor));
    }

    [Fact]
    public void Age35_queues_file704_before_advancing_the_current_IO_controller()
    {
        Harness harness = new();
        NativeBattleEffect210Step step = AdvanceMain6F7310(Frame(35), Main, null!, harness.Inputs());
        Assert.Collection(harness.Operations, operation => Assert.Equal(new NativeBattleEffect210IoQueue5341D0(704, Arena + 0xB1B8, 0), operation), operation => Assert.IsType<NativeBattleEffect210IoAdvance534210>(operation));
        Assert.Equal(36, Read(step.Frame, Main).ReadWord(12));
    }

    [Theory]
    [InlineData(true, 54, 0)]
    [InlineData(false, 55, 1)]
    public void Age54_wait_uses_actual_IO_busy_without_a_fixed_delay(bool busy, int expectedAge, int advances)
    {
        Harness harness = new() { Busy = busy };
        NativeBattleEffect210Step step = AdvanceMain6F7310(Frame(54), Main, null!, harness.Inputs());
        Assert.Equal(expectedAge, Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(advances, harness.Operations.OfType<NativeBattleEffect210IoAdvance534210>().Count());
        Assert.Equal(1, harness.Polls);
    }

    [Theory]
    [InlineData(false, 0, 307)]
    [InlineData(true, 2, 243)]
    public void Actual_completion_controls_retirement_and_wait_age307_survives_JSON(bool complete, int result, int age)
    {
        Harness harness = new() { Complete = complete, Contact = false };
        NativeBattleEffect210Frame frame = Frame(243);
        NativeBattleEffect210Step step = AdvanceMain6F7310(frame, Main, null!, harness.Inputs());
        Assert.Equal(result, step.Result);
        Assert.Equal(age, Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(2, harness.Selected);
        if (!complete)
        {
            NativeBattleEffect210Frame restored = JsonSerializer.Deserialize<NativeBattleEffect210Frame>(JsonSerializer.Serialize(step.Frame))!;
            NativeBattleEffect210Step next = AdvanceMain6F7310(restored, Main, null!, harness.Inputs());
            Assert.Equal(307, Read(next.Frame, Main).ReadWord(12));
            Assert.Single(harness.Operations.OfType<NativeBattleEffect210AllContacts506BA0>());
        }
    }

    [Fact]
    public void Age235_queues_three_actual_files_and_retains_original_point_mutation_widths()
    {
        Harness harness = new() { Contact = false };
        NativeBattleEffect210Step step = AdvanceMain6F7310(Frame(235), Main, null!, harness.Inputs());
        Assert.Equal(new short[] { 360, 361, 362 }, harness.Operations.OfType<NativeBattleEffect210IoQueue5341D0>().Select(operation => operation.FileId));
        Assert.Equal(new byte[] { 1, 1, 0 }, harness.Operations.OfType<NativeBattleEffect210IoQueue5341D0>().Select(operation => operation.Type));
        Assert.All(harness.Operations.OfType<NativeBattleEffect210IoQueue5341D0>(), operation => Assert.Equal(Arena + 0x1F08u, operation.DestinationAddress));
        Assert.All(harness.Operations.OfType<NativeBattleEffect210WriteModelBytes>(), operation => Assert.Equal(2, operation.Length));
        Assert.Equal(236, Read(step.Frame, Main).ReadWord(12));
    }

    [Fact]
    public void Four_birth_batches_fill_both_native_pools_and_failure_consumes_no_more_CRT()
    {
        NativeBattleEffect210Frame frame = InitializeCommonPool(InitializeCommonPool(Frame(115), SpriteDescriptor), MeshDescriptor);
        NativeCrtRandomState expected = frame.Random;
        for (int batch = 0; batch < 4; batch++)
        {
            frame = BirthRingParticles(frame, Main, null!, new Harness().Inputs());
        }
        for (int draw = 0; draw < 4 * (8 * 3 + 8 * 8); draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(32, frame.Effect.Pools.Single(pool => pool.DescriptorAddress == SpriteDescriptor).Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(32, frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MeshDescriptor).Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleEffect210Frame full = BirthRingParticles(frame, Main, null!, new Harness().Inputs());
        Assert.Equal(frame.Random, full.Random);
    }

    [Fact]
    public void Frozen_main_clock_still_advances_while_root_clock_remains_retained()
    {
        NativeBattleEffect210Frame frame = Frame(34);
        frame = frame with { State = frame.State.WriteDword(FrozenGlobal, 1) };
        NativeBattleEffect210Step step = AdvanceMain6F7310(frame, Main, null!, new Harness().Inputs());
        Assert.Equal(35, Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(2300u, step.Frame.State.ReadDword(BrightnessGlobal));
    }

    [Fact]
    public void Root_retirement_detaches_residual_unscheduled_flags_payload_and_partial_knownness()
    {
        NativeBattleEffect210Frame frame = Frame(7, false);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(14, 1));
        frame = InitializeCommonPool(frame, MeshDescriptor);
        (frame, uint orphan) = Append(frame, MeshDescriptor, 0x6F9B60);
        frame = Write(frame, orphan, Read(frame, orphan).WriteWord(20, 0xBEEF).MarkPayloadUnknown(22, 1));
        NativeBattleEffectTaskPool mesh = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MeshDescriptor);
        int index = frame.Effect.Pools.IndexOf(mesh);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, mesh with { Head = null, Tail = null }) } };
        NativeBattleEffect210Frame next = Advance(frame, null!, new Harness().Inputs());
        Assert.Empty(next.Effect.Pools);
        Assert.Equal(0u, next.Effect.ReturnedDescriptor);
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(next.Memory.Arena!.Read(0x183C + 20, 2)));
        Assert.False(next.Memory.Arena.ReadSnapshot(0x183C + 22, 1).Known[0]);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(next.Memory.Arena.Read(0x183C, 2)) & 1);
    }

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(11, 2108u)]
    [InlineData(12, 2300u)]
    [InlineData(-1, 0xFFFFFF41u)]
    public void Stage_brightness_follows_actual_signed_task_age(short age, uint expected)
    {
        NativeBattleEffect210Step step = AdvanceMain6F7310(Frame(age), Main, null!, new Harness().Inputs());
        Assert.Equal(expected, step.Frame.State.ReadDword(BrightnessGlobal));
    }

    internal static NativeBattleEffect210Frame Frame(short age, bool main = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(12, 0).WriteWord(12, unchecked((ushort)age))) };
        NativeBattleEffect210State state = NativeBattleEffect210State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, 0x1D97494);
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (main)
        {
            NativeBattleEffectTaskPool taskPool = NativeBattleEffectTaskPool.Initialize(MainDescriptor, Main, 0x3C0, 1);
            (taskPool, _) = taskPool.Append(MainCallback);
            taskPool = taskPool with { Slots = taskPool.Slots.SetItem(0, taskPool.Slots[0].WriteBytes(12, new byte[948]).WriteWord(12, unchecked((ushort)age))) };
            pools = pools.Add(taskPool);
            state = state.DetachCommonQueue(MainDescriptor);
        }
        NativeBattleOriginalEffectState effect = new(210, 209, new(210, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    internal sealed class Harness
    {
        public bool Busy { get; init; }
        public bool Complete { get; init; }
        public bool Contact { get; init; } = true;
        public int Polls { get; private set; }
        public int Selected { get; private set; }
        public List<NativeBattleEffect210Operation> Operations { get; } = [];
        public NativeBattleEffect210Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0x201, _ => Packets(Contact), (_, actor) => new(actor, 2), (_, _) => NativeBattleEffectArena.Clear().Write(28, new byte[] { 10, 0, 20, 0, 30, 0, 0xEF, 0xBE }).Write(38, BitConverter.GetBytes((short)700)), (_, _) => [], _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, _, _, retained) => new(frame, PutPoint(retained, 0, new(100, 200, 300))), (frame, _, retained) => new(frame, PutPoint(retained, 0, new(100, 200, 300))), frame =>
        {
            Polls++;
            return new(frame, Busy);
        }, (frame, mode) =>
        {
            Selected = mode;
            return new(frame, Complete);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });

        private static NativeBattleActionPacketsState Packets(bool contact)
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 54, -1, 315, 210, null));
            if (contact)
            {
                (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
