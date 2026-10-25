using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect308Fragments;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect308FragmentsTests
{
    [Fact]
    public void Zero_normal_reads_index_zero_alias_and_publishes_GTE_before_skipping_allocation()
    {
        Fixture fixture = new();
        fixture.ContextPoints(false);
        NativeBattleEffect308Frame result = EmitSmoke668230(fixture.Frame, fixture.ContextAddress, fixture.Resources);
        Assert.Equal(32u, result.Memory.GeometryRegisters!.Lzcr);
        Assert.Equal(new int?[] { 0, 0, 0 }, result.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(Fixture.Smoke, Dword308(ReadBytes(result, Fixture.Main + 0xE90, 4), 0));
        Assert.Equal(0u, result.State.ReadDword(InternalRandomGlobal));
        Assert.Equal((short)0, Word308(ReadBytes(result, Fixture.Smoke, 12), 6));
        NativeBattleEffect308Resources missingAlias = fixture.Resources with { StaticFronts = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty };
        Assert.Throws<NotSupportedException>(() => EmitSmoke668230(fixture.Frame, fixture.ContextAddress, missingAlias));
    }

    [Theory]
    [InlineData(0u, 11, 1, 244)]
    [InlineData(100u, 12, 0, 253)]
    public void Positive_normal_consumes_one_local_draw_and_selects_life_variant_and_velocity_width(uint seed, int life, int variant, int velocityZ)
    {
        Fixture fixture = new();
        fixture.ContextPoints(true);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(InternalRandomGlobal, seed) };
        NativeBattleEffect308Frame result = EmitSmoke668230(fixture.Frame, fixture.ContextAddress, fixture.Resources);
        NativeBattleEffectArena row = ReadBytes(result, Fixture.Smoke, 12);
        Assert.Equal(life, Word308(row, 6));
        Assert.Equal(variant, row.Read(11, 1)[0]);
        Assert.Equal(new byte[] { 0, 0, (byte)velocityZ }, row.Read(8, 3));
        Assert.Equal((seed * 125 + 14) & 0x7FFF, result.State.ReadDword(InternalRandomGlobal));
        Assert.Equal(Fixture.Smoke + 12, Dword308(ReadBytes(result, Fixture.Main + 0xE90, 4), 0));
        Assert.Equal(fixture.Frame.Random, result.Random);
    }

    [Fact]
    public void Full_smoke_bank_preserves_hint_and_local_random_even_after_positive_normal_work()
    {
        Fixture fixture = new();
        fixture.ContextPoints(true);
        for (int index = 0; index < 128; index++)
        {
            fixture.Frame = WriteBytes(fixture.Frame, Fixture.Smoke + (uint)(index * 12) + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)1)), 2);
        }
        uint hint = Fixture.Smoke + 37 * 12;
        fixture.Frame = WriteBytes(fixture.Frame, Fixture.Main + 0xE90, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(hint)), 4);
        (NativeBattleEffect308Frame scan, uint address) = AllocateSmoke668470(fixture.Frame, Fixture.Main);
        Assert.Equal(0u, address);
        Assert.Same(fixture.Frame, scan);
        NativeBattleEffect308Frame result = EmitSmoke668230(fixture.Frame, fixture.ContextAddress, fixture.Resources);
        Assert.Equal(hint, Dword308(ReadBytes(result, Fixture.Main + 0xE90, 4), 0));
        Assert.Equal(0u, result.State.ReadDword(InternalRandomGlobal));
    }

    [Fact]
    public void Last_free_hint_wraps_and_busy_hint_search_restarts_at_bank_origin()
    {
        Fixture fixture = new();
        uint last = Fixture.Main + 0xE84;
        fixture.Frame = WriteBytes(fixture.Frame, Fixture.Main + 0xE90, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(last)), 4);
        (NativeBattleEffect308Frame frame, uint selected) = AllocateSmoke668470(fixture.Frame, Fixture.Main);
        Assert.Equal(last, selected);
        Assert.Equal(Fixture.Smoke, Dword308(ReadBytes(frame, Fixture.Main + 0xE90, 4), 0));
        frame = WriteBytes(frame, Fixture.Smoke + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)1)), 2);
        (frame, selected) = AllocateSmoke668470(frame, Fixture.Main);
        Assert.Equal(Fixture.Smoke + 12, selected);
        Assert.Equal(Fixture.Smoke + 24, Dword308(ReadBytes(frame, Fixture.Main + 0xE90, 4), 0));
    }

    [Fact]
    public void Fragment_opcode_passes_selected_bones_and_all_four_suffix_words_then_returns_exact_PC()
    {
        Fixture fixture = new();
        uint pc = ScriptRanges[0].Address + 2;
        short[] words = [1, 22, -1, 3, -400, 1234, 32767];
        fixture.Frame = WriteBytes(fixture.Frame, pc, NativeBattleEffectArena.Clear().Write(0, words.SelectMany(word => BitConverter.GetBytes(word)).ToArray()), words.Length * 2);
        (NativeBattleEffect308Frame result, uint next) = Birth667DB0(fixture.Frame, pc, 0x1D97494, Fixture.Main + 0x20, null!, fixture.Inputs());
        Assert.Equal(pc + 14, next);
        NativeBattleEffect308SelectFragments667DB0 operation = Assert.IsType<NativeBattleEffect308SelectFragments667DB0>(Assert.Single(fixture.Operations));
        Assert.Equal(new short[] { 1, 22 }, operation.BoneSelectors);
        Assert.Equal((short)3, operation.SmokeLimit);
        Assert.Equal((short)-400, operation.NormalSpeed);
        Assert.Equal((short)1234, operation.UnusedParameter);
        Assert.Equal((short)32767, operation.RandomThreshold);
        Assert.Equal(0, fixture.ProcessDraws);
        Assert.Equal(fixture.Frame.State, result.State);
    }

    [Fact]
    public void Collector_zero_old_budget_decrements_before_exit_after_one_process_draw()
    {
        Fixture fixture = new();
        fixture.SetContext(0, 0);
        byte[] geometry = Geometry();
        NativeBattleEffect308Frame result = fixture.Collect(geometry, 1);
        NativeBattleEffectArena context = Context(result, fixture.ContextAddress, 140);
        Assert.Equal(1, fixture.ProcessDraws);
        Assert.Equal(-1, unchecked((int)Dword308(context, 0x14)));
        Assert.Equal(0u, Dword308(context, 0x18));
        Assert.Equal(new byte[2], ReadBytes(result, Fixture.Scratch + 6, 2).Read(0, 2));
        Assert.Equal(100, result.Memory.ScriptedColorRegisters!.Mac[2]);
        Assert.Equal(0u, result.State.ReadDword(InternalRandomGlobal));
        Assert.Equal((short)0, Word308(ReadBytes(result, Fixture.Smoke, 12), 6));
    }

    [Fact]
    public void Collector_repeats_current_object_with_eight_face_cooldown_and_WORD_count_underflow()
    {
        Fixture fixture = new();
        fixture.SetContext(2, 0);
        byte[] geometry = Geometry();
        int visits = 0;
        NativeBattleEffect308Frame result = Collect667F40(fixture.Frame, (offset, count) =>
        {
            if (offset == 14)
            {
                visits++;
            }
            return geometry.AsSpan(offset, count).ToArray();
        }, 0x1900000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 1)], Fixture.Scratch, 24, fixture.ContextAddress, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena context = Context(result, fixture.ContextAddress, 140);
        Assert.Equal(9, visits);
        Assert.Equal(2, fixture.ProcessDraws);
        Assert.Equal(0u, Dword308(context, 0x14));
        Assert.Equal(7u, Dword308(context, 0x18));
        Assert.Equal((short)-1, Word308(context, 8));
        Assert.Equal((short)-1, Word308(context, 10));
        Assert.Equal(0u, result.State.ReadDword(InternalRandomGlobal));
        Assert.Equal((short)100, Word308(ReadBytes(result, Fixture.Scratch, 8), 4));
        Assert.False(ReadBytes(result, Fixture.Scratch + 24, 1).ReadSnapshot(0, 1).Known[0]);
    }

    [Fact]
    public void Unselected_group_keeps_unknown_XYZ_and_invalid_selected_scratch_fails_before_random()
    {
        Fixture fixture = new();
        fixture.SetContext(1, 0);
        byte[] geometry = Geometry();
        NativeBattleEffect308Frame hidden = fixture.Collect(geometry, 0);
        Assert.All(ReadBytes(hidden, Fixture.Scratch, 6).ReadSnapshot(0, 6).Known, known => Assert.False(known));
        Assert.Equal((short)1, Word308(ReadBytes(hidden, Fixture.Scratch, 8), 6));
        Assert.Equal(0, fixture.ProcessDraws);
        Assert.Throws<InvalidDataException>(() => fixture.Collect(geometry, 1, 0));
        Assert.Equal(0, fixture.ProcessDraws);
        Assert.Equal(0u, fixture.Frame.State.ReadDword(InternalRandomGlobal));
    }

    private static byte[] Geometry()
    {
        byte[] data = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 3);
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 6 + 4), 100);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(46), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(48), 2);
        return data;
    }

    private sealed class Fixture
    {
        public const uint Main = NativeBattleEffect308TestFrame.Main;
        public const uint Smoke = Main + 0x890;
        public const uint Scratch = 0x1900000;
        public NativeBattleEffect308Frame Frame;
        public NativeBattleEffect308Resources Resources;
        public uint ContextAddress;
        public int ProcessDraws;
        public List<NativeBattleEffect308Operation> Operations = [];

        public Fixture()
        {
            Frame = NativeBattleEffect308TestFrame.Create(20);
            Frame = Frame with { Staging = Frame.Staging!.Write(NativeBattleTemporaryMemory.Address, new byte[140]) };
            (Frame, ContextAddress) = Allocate(Frame, 140);
            Frame = WriteBytes(Frame, Main + 0xE90, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(Smoke)), 4);
            SetContext(1, 0);
            ImmutableDictionary<uint, ImmutableArray<byte>> fronts = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(NormalizationAddress, BitConverter.GetBytes((ushort)0xFF38).ToImmutableArray()).Add(NormalizationAddress + 128, BitConverter.GetBytes((ushort)4096).ToImmutableArray());
            Resources = new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, 0, null!, fronts);
        }

        public void SetContext(uint budget, uint cooldown)
        {
            NativeBattleEffectArena context = Context(Frame, ContextAddress, 140);
            foreach ((int offset, uint value) in new (int, uint)[] { (0x10, 1), (0x14, budget), (0x18, cooldown), (0x1C, 12), (0x24, 32767), (0x28, Main) })
            {
                context = context.Write(offset, BitConverter.GetBytes(value));
            }
            Frame = PublishContext(Frame, ContextAddress, context, 140);
        }

        public void ContextPoints(bool nonzero)
        {
            NativeBattleEffectArena context = Context(Frame, ContextAddress, 140);
            context = PutPoint(context, 0x2C, new(0, 0, 0));
            context = PutPoint(context, 0x34, new((short)(nonzero ? 1 : 0), 0, 0));
            context = PutPoint(context, 0x3C, new(0, (short)(nonzero ? 1 : 0), 0));
            Frame = PublishContext(Frame, ContextAddress, context, 140);
        }

        public NativeBattleEffect308Frame Collect(byte[] geometry, short selected, int scratchLength = 24)
        {
            return Collect667F40(Frame, (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1900000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), unchecked((ushort)selected))], Scratch, scratchLength, ContextAddress, Resources, Inputs());
        }

        public NativeBattleEffect308Inputs Inputs()
        {
            NativeBattleEffect308Inputs inputs = NativeBattleEffect308TestFrame.Inputs(Operations);
            return inputs with { ProcessRandom7059E0 = frame =>
            {
                ProcessDraws++;
                return new(frame, 0);
            } };
        }
    }
}
