using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterScalarReferenceCommandTests
{
    private const uint ArenaBase = 0x20DFAB8;
    private const uint Manager = 0x2797690;
    private const uint Pc = 0xFFFFFFFC;
    private const int Slot = 0x2000;
    private const int Reference = 0x2100;
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Theory]
    [InlineData(0, 0, 0, 0, false)]
    [InlineData(0, 255, 0, 0, true)]
    [InlineData(1, 9, 9, 0, false)]
    [InlineData(1, 9, 8, 0, true)]
    [InlineData(2, 0, 0, 5, true)]
    [InlineData(2, 0, 0, 3, false)]
    [InlineData(3, 0, 0, 3, true)]
    [InlineData(3, 0, 0, 1, false)]
    public void Conditional_four_actual_predicates_consume_parameter_payload_and_branch_operand_only_when_needed(int condition, byte count, byte prior, byte parameterFlags, bool branch)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)(0x55 | condition << 9), -6);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, [count, prior]) };
        if (!branch)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        int reads = 0;
        NativeBattleWaterScalarReferenceServices services = Services() with
        {
            ReadParameterByte = (current, address) =>
            {
                reads++;
                Assert.Equal(0x76543211u, address);
                return parameterFlags;
            }
        };
        NativeBattleWaterExecutionFrame result = Run(frame, services);
        Assert.Equal(branch ? 0xFFFFFFF6u : 0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(condition < 2 ? 0 : 1, reads);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
    }

    [Fact]
    public void Conditional_reads_current_parameter_pointer_even_when_its_payload_is_not_used()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x55);
        frame = frame with { Core = frame.Core.MarkUnknown(Manager + 0xC0, 4) };
        Assert.Throws<NotSupportedException>(() => Run(frame, Services()));
    }

    [Fact]
    public void Reference_copy_uses_low_six_mask_and_caches_before_selected_update()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x61, unchecked((short)0xFFE1), -2);
        frame = SlotDword(frame, Reference, 0x50, 0x11223344);
        frame = SlotDword(frame, Reference, 0x64, 0xAABBCCDD);
        List<string> order = [];
        NativeBattleWaterScalarReferenceServices services = Services() with
        {
            ResolveSlot = (current, selector) =>
            {
                Assert.Equal((short)-2, selector);
                return ArenaBase + Reference;
            },
            CachePosition = current =>
            {
                order.Add("cache");
                Assert.Equal(0x11223344u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x50));
                return current with { Arena = current.Arena.Write(Slot + 0x8C, [0x34, 0x12]) };
            },
            UpdateSelected = current =>
            {
                order.Add("update");
                Assert.Equal((ushort)0x1234, WaterSchedulerFixture.ReadSlotWord(current, 0, 0x8C));
                return current;
            }
        };
        NativeBattleWaterExecutionFrame result = Run(frame, services);
        Assert.Equal(new[] { "cache", "update" }, order);
        Assert.Equal(0xAABBCCDDu, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x54));
        Assert.Equal(2u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0xE275, 100, 2, 307200)]
    [InlineData(0x1C75, -300, -2, 153600)]
    public void Height_selects_actual_bounds_anchor_or_Scale38_and_updates_before_cache(ushort instruction, short scale, short operand, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, operand);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797068 + 0x60, 0x1D972C0);
        List<string> order = [];
        NativeBattleGraphicsModel model = new(0, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, scale) };
        if ((instruction & 0x200) != 0)
        {
            model = model with { GeometryBounds52 = new(new(0, -100, 0), new(0, 1000, 0)) };
        }
        NativeBattleWaterScalarReferenceServices services = Services() with
        {
            ReadActor = (current, address) =>
            {
                Assert.Equal(0x1D972C0u, address);
                return model;
            },
            ReadAnchor = (current, address) =>
            {
                Assert.True((instruction & 0x200) != 0);
                byte[] skeleton = new byte[0x40];
                skeleton[0] = 1;
                BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x10), -1);
                return new(skeleton.ToImmutableArray(), [Identity], Identity with { Y = 500 });
            },
            UpdateSelected = current =>
            {
                order.Add("update");
                return current;
            },
            CachePosition = current =>
            {
                order.Add("cache");
                return current;
            }
        };
        NativeBattleWaterExecutionFrame result = Run(frame, services);
        Assert.Equal(new[] { "update", "cache" }, order);
        for (int lane = 0; lane < 6; lane++)
        {
            bool selected = (instruction & (0x8000 >> lane)) != 0;
            Assert.Equal(selected ? expected : 0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x50 + lane * 4));
        }
        if ((instruction & 0x200) == 0)
        {
            Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        }
        else
        {
            Assert.Equal(500u, result.ReadTemporaryDword(0xF4));
        }
    }

    [Fact]
    public void Six_random_lanes_use_six_current_reference_words_and_consume_CRT_even_for_zero()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFC76, 4);
        short[] magnitudes = [0, 7, -9, 123, -456, 0];
        NativeCrtRandomState expectedRandom = frame.Random;
        uint[] expected = new uint[6];
        for (int lane = 0; lane < 6; lane++)
        {
            int offset = 0x8C + 2 * lane + (lane >= 3 ? 2 : 0);
            frame = SlotWord(frame, Reference, offset, unchecked((ushort)magnitudes[lane]));
            frame = SlotDword(frame, Slot, 0x50 + 4 * lane, 0xFFFF1234);
            NativeBattleWaterVectorRandom draw = NativeBattleWaterVectorCommands.RandomB65740(magnitudes[lane], expectedRandom);
            expectedRandom = draw.Random;
            expected[lane] = unchecked(0xFFFF1234u + (uint)(draw.Value << 16));
        }
        List<string> order = [];
        NativeBattleWaterScalarReferenceServices services = Services() with { UpdateSelected = current => Record(current, "update"), CachePosition = current => Record(current, "cache") };
        NativeBattleWaterExecutionFrame result = Run(frame, services);
        Assert.Equal(expectedRandom, result.Random);
        Assert.Equal(new[] { "update", "cache" }, order);
        Assert.Equal(expected, Enumerable.Range(0, 6).Select(lane => WaterSchedulerFixture.ReadSlotDword(result, 0, 0x50 + lane * 4)));

        NativeBattleWaterExecutionFrame Record(NativeBattleWaterExecutionFrame current, string text)
        {
            order.Add(text);
            return current;
        }
    }

    [Theory]
    [InlineData(128, -3, 7, 14043)]
    [InlineData(300, -3, 7, -4827)]
    [InlineData(32767, 32767, 7, 304396032)]
    public void Division_uses_signed_high_motion_and_unchecked_DWORD_numerator_before_truncation(short factor, short motion, short divisor, int expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8087, factor, divisor);
        frame = SlotDword(frame, Slot, 0x50, unchecked((uint)(motion << 16)) | 0xFFFF);
        NativeBattleWaterExecutionFrame result = Run(frame, Services());
        Assert.Equal(unchecked((uint)expected), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x68));
        Assert.Equal(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-32512, -1)]
    public void Actual_selected_IDIV_faults_are_explicit(short factor, short divisor)
    {
        NativeBattleWaterExecutionFrame frame = SlotDword(Fixture(0x8087, factor, divisor), Slot, 0x50, 0x01000000);
        Assert.Throws<NotSupportedException>(() => Run(frame, Services()));
    }

    [Fact]
    public void No_selected_division_lane_reads_operands_but_does_not_divide()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x87, 1, 0);
        Assert.Equal(2u, WaterSchedulerFixture.ReadCoreDword(Run(frame, Services()), 0x2797450));
        NativeBattleWaterExecutionFrame missing = Fixture(0x87, 1, 0);
        missing = missing with { Arena = missing.Arena.MarkUnknown(0x1004, 2) };
        Assert.Throws<NotSupportedException>(() => Run(missing, Services()));
    }

    [Fact]
    public void Color_factor_clamps_signed_channels_and_retains_current_command_without_scratch_or_random_writes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xD3, -1);
        frame = SlotWord(frame, Reference, 0x8C, unchecked((ushort)-128));
        frame = SlotWord(frame, Reference, 0x94, unchecked((ushort)-5));
        frame = SlotWord(frame, Reference, 0x96, 600);
        frame = SlotWord(frame, Reference, 0x98, unchecked((ushort)-1000));
        frame = SlotDword(frame, Slot, 0xCC, 0xAB123456);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = Run(frame, Services());
        Assert.Equal(0xABFF0002u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xCC));
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(restored, Services())));
    }

    private static NativeBattleWaterScalarReferenceServices Services() => new((frame, selector) => ArenaBase + Reference, UnexpectedParameter, UnexpectedActor, UnexpectedAnchor, UnexpectedPost, UnexpectedPost, NativeBattleActorAnchorRegisterFixture.Services());
    private static byte UnexpectedParameter(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException("Unused parameter payload was consumed.");
    private static NativeBattleGraphicsModel UnexpectedActor(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException("Unused actor was consumed.");
    private static NativeBattleWaterActorAnchorSource UnexpectedAnchor(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException("Unused current actor pose was consumed.");
    private static NativeBattleWaterExecutionFrame UnexpectedPost(NativeBattleWaterExecutionFrame frame) => throw new InvalidOperationException("Unexpected update/cache call.");

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, short first = 0, short second = 0)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xC0, 0x76543210);
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), first);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), second);
        return frame with { Arena = frame.Arena.Write(0x1000, bytes) };
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScalarReferenceServices services) => NativeBattleWaterScalarReferenceCommands.Execute(frame, new(0x1000, 6, Pc), services);

    private static NativeBattleWaterExecutionFrame SlotWord(NativeBattleWaterExecutionFrame frame, int slot, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(slot + offset, bytes) };
    }

    private static NativeBattleWaterExecutionFrame SlotDword(NativeBattleWaterExecutionFrame frame, int slot, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(slot + offset, bytes) };
    }
}
