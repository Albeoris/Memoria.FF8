using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect315Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315ControllerTests
{
    [Fact]
    public void Main_birth_updates_actual_Default2_and_next_model_fields_with_retained_padding()
    {
        Harness harness = new();
        NativeBattleEffect315Frame frame = Active();
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x16, 0xBEEF));
        frame = AdvanceMain(frame, ChildBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal((ushort)7, Read(frame, ChildBank).ReadWord(0xE));
        Assert.Equal((byte)13, harness.Models[3].SequenceState!.Default2);
        Assert.Equal((byte)13, harness.Models[3].Sequence);
        Assert.Equal((ushort)0xBEEF, Read(frame, ChildBank).ReadWord(0x16));
        Assert.Equal(new NativeBattleVector(100, 2, 300), harness.Models[4].Transform!.Initial20);
        Assert.Equal(new NativeBattleVector(4, 5, 6), harness.Models[4].Transform!.Position28);
        Assert.Equal((ushort)6, harness.Models[4].Flags0);
        Assert.Equal((ushort)2, harness.Models[3].Flags0);
        Assert.Equal(0x8020u, harness.Models[3].Flags8);
        Assert.Equal(0x12345678u, frame.State.ReadDword(VibrationGlobal));
        Assert.Equal((ushort)1, Read(frame, ChildBank).ReadWord(0xC));
    }

    [Fact]
    public void Main_schedule_consumes_118_shared_CRT_draws_and_activates_only_after_old_age160()
    {
        Harness harness = new();
        NativeBattleEffect315Frame frame = Active();
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 118; draw++)
        {
            expected = expected.Draw(out _);
        }
        for (int age = 0; age <= 160; age++)
        {
            (frame, int result) = AdvanceMain(frame, ChildBank, Resources(), harness.Inputs());
            Assert.Equal(age == 160 ? 2 : 0, result);
            if (age < 160)
            {
                Assert.Empty(harness.Operations.OfType<NativeBattleEffect315Activate487670>());
            }
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal((ushort)161, Read(frame, ChildBank).ReadWord(0xC));
        Assert.Equal((byte)7, harness.Models[3].SequenceState!.Default2);
        Assert.Equal(0x20u, harness.Models[3].Flags8);
        Assert.Equal(new NativeBattleEffect315Activate487670(4), Assert.Single(harness.Operations.OfType<NativeBattleEffect315Activate487670>()));
        Assert.Single(harness.Operations.OfType<NativeBattleEffect315Contact506690>());
        Assert.Equal(new[] { Resources().SoundA, Resources().SoundB }, harness.Operations.OfType<NativeBattleEffect315Sound5013A0>().Select(item => item.Packed));
        Assert.Single(harness.Operations.OfType<NativeBattleEffect315StopVibration4A2940>());
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        Assert.Equal(13, pool.Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x59A500));
        Assert.Equal(17, pool.Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x59AAF0));
        Assert.Equal(2, pool.Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x59AC70));
        Assert.Equal(new ushort[] { 0, 1, 2 }, pool.Slots.Where(task => task.Callback == 0x599470).Select(task => task.ReadWord(0x18)));
        Assert.Equal(new ushort[] { 1, 2, 4 }, pool.Slots.Where(task => task.Callback == 0x599470).Select(task => task.ReadWord(0x1A)));
        NativeBattleModulePoolTask body = Assert.Single(pool.Slots, task => task.Callback == 0x599CC0);
        Assert.Equal(unchecked((ushort)-850), body.ReadWord(0x20));
        Assert.Equal(unchecked((ushort)-12), body.ReadWord(0x22));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x200u)]
    public void Main_pause_consumes_no_random_and_performs_no_births(uint flags)
    {
        Harness harness = new() { Flags = flags };
        NativeBattleEffect315Frame before = Active();
        NativeBattleEffect315Frame after = AdvanceMain(before, ChildBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(before, after);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Color_callback_publishes_current_transition_DPCS_before_pause_and_uses_full_palette_code()
    {
        Harness harness = new() { Flags = 1 };
        NativeBattleEffect315Frame frame = Task(0x59ADB0, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0xE, 3));
        frame = AdvanceTask(frame, ChildBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(0xA0102030u, harness.Models[3].Color40);
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(0xC));
        Assert.NotEqual(0, harness.Models[3].Flags0 & 0x800);
        Assert.Equal(0x300u, frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x200u, frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0x100u, frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Equal(4096u, frame.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void Model_color_callbacks_clear_only_bit800_at_their_distinct_completion_limits()
    {
        foreach ((uint callback, ushort age) in new[] { (0x59ADB0u, (ushort)116), (0x59AEF0u, (ushort)117) })
        {
            Harness harness = new();
            NativeBattleEffect315Frame frame = Task(callback, age);
            frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0xE, 3).WriteWord(0x1C, 4));
            (frame, int result) = AdvanceTask(frame, ChildBank, Resources(), harness.Inputs());
            Assert.Equal(2, result);
            Assert.Equal((ushort)(age + 1), Read(frame, ChildBank).ReadWord(0xC));
            Assert.Equal((ushort)6, harness.Models[3].Flags0);
        }
    }

    [Theory]
    [InlineData(0x599CC0u, 64, 700, 10)]
    [InlineData(0x59A3E0u, 109, 900, 11)]
    public void Body_callers_fill_actual_156_byte_fields_and_draw_even_while_paused(uint callback, ushort age, int radius, int multiplier)
    {
        Harness harness = new() { Flags = 1 };
        NativeBattleEffect315Frame frame = Task(callback, age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0xE, 4).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 999).WriteWord(0x20, unchecked((ushort)-2000)).WriteWord(0x22, 25));
        frame = AdvanceTask(frame, ChildBank, Resources(), harness.Inputs()).Frame;
        NativeBattleEffectArena context = Assert.Single(harness.Contexts);
        Assert.Equal(radius, BitConverter.ToInt32(context.Read(0x34, 4)));
        Assert.Equal(multiplier, BitConverter.ToInt32(context.Read(0x38, 4)));
        Assert.Equal(-2000, BitConverter.ToInt32(context.Read(0x30, 4)));
        Assert.Equal(25, BitConverter.ToInt32(context.Read(0x3C, 4)));
        Assert.Equal(new byte[] { 100, 0, 200, 0 }, context.Read(0x74, 4));
        Assert.Equal(new byte[] { 44, 1, 231, 3 }, context.Read(0x78, 4));
        Assert.Equal(callback == 0x599CC0 ? 64 * 4096 / 70 : 4096 + (30 - 109) * 4096 / 80, BitConverter.ToInt32(context.Read(0x2C, 4)));
        Assert.Equal(age, Read(frame, ChildBank).ReadWord(0xC));
        Assert.Equal((ushort)200, Read(frame, ChildBank).ReadWord(0x12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(0x599CC0u, false)]
    [InlineData(0x599CC0u, true)]
    [InlineData(0x59A3E0u, false)]
    [InlineData(0x59A3E0u, true)]
    public void Body_callers_copy_anchor_bytes_and_preserve_padding_knowledge(uint callback, bool unknownPadding)
    {
        Harness harness = new() { Flags = 1 };
        NativeBattleEffect315Frame frame = Task(callback, 1);
        NativeBattleModulePoolTask task = Read(frame, ChildBank).WriteWord(0xE, 4).WriteWord(0x10, 100).WriteWord(0x12, unchecked((ushort)-200)).WriteWord(0x14, 300).WriteWord(0x16, 0xBEEF).WriteWord(0x20, unchecked((ushort)-2000)).WriteWord(0x22, 25);
        if (unknownPadding)
        {
            task = task.MarkPayloadUnknown(0x16, 2);
        }
        frame = Write(frame, ChildBank, task);

        frame = AdvanceTask(frame, ChildBank, Resources(), harness.Inputs()).Frame;

        NativeBattleEffectArena context = Assert.Single(harness.Contexts);
        (byte[] data, bool[] known) = context.ReadSnapshot(0x74, 8);
        Assert.Equal(new byte[] { 100, 0, 56, 255, 44, 1 }, data.Take(6));
        Assert.Equal(new[] { true, true, true, true, true, true, !unknownPadding, !unknownPadding }, known);
        Assert.Equal(new byte[] { 100, 0, 56, 255, 44, 1 }, context.Read(0x74, 6));
        if (unknownPadding)
        {
            Assert.Throws<NotSupportedException>(() => context.Read(0x7A, 2));
            Assert.Throws<NotSupportedException>(() => Read(frame, ChildBank).ReadWord(0x16));
        }
        else
        {
            Assert.Equal(new byte[] { 100, 0, 56, 255, 44, 1, 239, 190 }, data);
            Assert.Equal(new byte[] { 239, 190 }, context.Read(0x7A, 2));
            Assert.Equal((ushort)0xBEEF, Read(frame, ChildBank).ReadWord(0x16));
        }
        Assert.Equal((ushort)1, Read(frame, ChildBank).ReadWord(0xC));
        Assert.Equal(unchecked((ushort)-200), Read(frame, ChildBank).ReadWord(0x12));
    }

    [Fact]
    public void Common_restore_preserves_high_completion_byte_and_waits_actual_WORD_nonzero()
    {
        Harness harness = new();
        NativeBattleEffect315Frame frame = Task(0x59B070, 1, true);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0xE, 0x3400));
        (frame, int result) = AdvanceTask(frame, RootBank, Resources(), harness.Inputs());
        Assert.Equal(2, result);
        Assert.Equal((ushort)0x3401, Read(frame, RootBank).ReadWord(0xE));
        Assert.Equal(new NativeBattleEffect315ReleaseCommon508630(ArenaAddress, RootBank + 0xE), Assert.Single(harness.Operations.OfType<NativeBattleEffect315ReleaseCommon508630>()));
    }

    [Theory]
    [InlineData(0, FirstPacketOffset)]
    [InlineData(1, SecondPacketOffset)]
    [InlineData(256, FirstPacketOffset)]
    [InlineData(257, SecondPacketOffset)]
    public void Root_parity_uses_low_age_bit_and_keeps_advancing_when_main_is_paused(ushort age, int offset)
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect315Frame frame = Active();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0xC, age));
        frame = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(ArenaAddress + (uint)offset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)(age + 1), Read(frame, RootBank).ReadWord(0xC));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(0xC));
    }

    [Fact]
    public void Child_drain_appends_waiter_in_same_root_scan_and_detaches_only_after_auxiliary_completion()
    {
        Harness harness = new();
        NativeBattleEffect315Frame frame = Active();
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        frame = WithPool(frame, 1, children.WriteSlot(0, children.Slots[0].UpdateHeader(0, children.Slots[0].State, children.Slots[0].Next, children.Slots[0].Callback)).UpdateLinks(null, null));
        NativeCrtRandomState random = frame.Random;
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(RootDescriptor, frame.Effect.ReturnedDescriptor);
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        Assert.Equal(1, root.Head);
        Assert.Equal((ushort)1, root.Slots[1].ReadWord(0xC));
        Assert.Equal(0x59B070u, root.Slots[1].Callback);
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect315ReleaseCommon508630>());
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Equal((ushort)2, BitConverter.ToUInt16(frame.State.RootBankStorage.Read(RootStride + 0xC, 2)));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(frame.State.RootBankStorage.Read(RootStride + 0xE, 2)));
        Assert.Equal(random, frame.Random);
    }

    private static NativeBattleEffect315Frame Task(uint callback, ushort age, bool root = false)
    {
        NativeBattleEffect315Frame frame = Active();
        uint address = root ? RootBank : ChildBank;
        return Write(frame, address, (Read(frame, address).UpdateHeader(Read(frame, address).Flags, Read(frame, address).State, Read(frame, address).Next, callback)).WriteWord(0xC, age));
    }

    private static NativeBattleEffect315Frame Active()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        (NativeBattleEffectTaskPool root, _) = pools[0].Append(0x59B000);
        (NativeBattleEffectTaskPool child, _) = pools[1].Append(0x598E70);
        NativeBattleOriginalEffectState effect = new(315, 314, new(315, Entry, Initializer), new(3, 0, 8, 0, 0, 0), null) { Pools = pools.SetItem(0, root).SetItem(1, child), ReturnedDescriptor = RootDescriptor };
        return new(memory, effect, new(12345), NativeBattleEffect315State.FromColdProcess().WriteDword(CasterGlobal, 3).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null, ChildBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect315Resources Resources() => new([], [], null!, 0x1234, 0x5678, null!, [], [], Enumerable.Repeat(0xA0112233u, 12).SelectMany(BitConverter.GetBytes).ToImmutableArray(), Enumerable.Repeat(0xB0445566u, 24).SelectMany(BitConverter.GetBytes).ToImmutableArray());

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public List<NativeBattleEffect315Operation> Operations { get; } = [];
        public List<NativeBattleEffectArena> Contexts { get; } = [];
        public Dictionary<int, NativeBattleGraphicsModel> Models { get; } = Enumerable.Range(0, 7).ToDictionary(index => index, index => new NativeBattleGraphicsModel(index, 6) { Flags8 = 0x20, Transform = new(new(1, 2, 3), new(4, 5, 6), 77, 88, 99), Sequence = 13, SequenceState = new() { Default2 = 7 }, GeometryBounds52 = new(new(-1, -550, -1), new(1, 850, 1)) });

        public NativeBattleEffect315Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, frame => Flags, frame => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), frame => new(null, 100, 160, 108), (frame, model) => Models[Index(model)], frame => NativeBattleActionPacketsState.Fresh(), (frame, model, selector, fraction, retained) => new(frame, retained.Write(0, BitConverter.GetBytes((short)100)).Write(2, BitConverter.GetBytes((short)-200)).Write(4, BitConverter.GetBytes((short)300))), frame => 0x00102030, Execute, frame => new(frame, 0x12345678), (frame, handle) => true);

        private NativeBattleEffect315Frame Execute(NativeBattleEffect315Frame frame, NativeBattleEffect315Operation operation)
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleEffect315ModelFlags flags:
                    Models[Index(flags.ModelAddress)] = Models[Index(flags.ModelAddress)] with { Flags0 = (ushort)((Models[Index(flags.ModelAddress)].Flags0 & ~flags.ClearFlags0) | flags.SetFlags0) };
                    break;
                case NativeBattleEffect315ModelFlags8 flags8:
                    Models[Index(flags8.ModelAddress)] = Models[Index(flags8.ModelAddress)] with { Flags8 = (Models[Index(flags8.ModelAddress)].Flags8!.Value & ~flags8.ClearFlags8) | flags8.SetFlags8 };
                    break;
                case NativeBattleEffect315ModelDefaultSequence2 sequence:
                    Models[Index(sequence.ModelAddress)] = Models[Index(sequence.ModelAddress)] with { SequenceState = Models[Index(sequence.ModelAddress)].SequenceState! with { Default2 = sequence.Value } };
                    break;
                case NativeBattleEffect315ModelInitial20XZ initial:
                    NativeBattleModelTransform transform = Models[Index(initial.ModelAddress)].Transform!;
                    Models[Index(initial.ModelAddress)] = Models[Index(initial.ModelAddress)] with { Transform = transform with { Initial20 = new(initial.X, transform.Initial20.Y, initial.Z) } };
                    break;
                case NativeBattleEffect315ModelPosition28XZ position:
                    NativeBattleModelTransform current = Models[Index(position.ModelAddress)].Transform!;
                    Models[Index(position.ModelAddress)] = Models[Index(position.ModelAddress)] with { Transform = current with { Position28 = new(position.X, current.Position28.Y, position.Z) } };
                    break;
                case NativeBattleEffect315ModelColor40 color:
                    Models[Index(color.ModelAddress)] = Models[Index(color.ModelAddress)] with { Color40 = color.Value };
                    break;
                case NativeBattleEffect315DrawBody156 body:
                    Contexts.Add(Context(frame, body.TemporaryAddress, 156));
                    break;
                case NativeBattleEffect315ReleaseCommon508630 release:
                    frame = WriteCompletionByte(frame, release.CompletionAddress, 1);
                    break;
            }
            return frame;
        }
        private static int Index(uint address) => checked((int)((address - 0x1D972C0) / 156));
    }
}
