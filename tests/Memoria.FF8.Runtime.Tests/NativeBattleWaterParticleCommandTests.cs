using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterParticleCommandTests
{
    private const uint ArenaBase = 0x20DFAB8;
    private const uint Manager = 0x2797690;
    private const uint Pc = 0xFFFFFFFC;
    private const int Bank = 0x4000;
    private const int Slot = 0x2000;

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void Allocation_initializes_only_native_bank_and_record_fields_without_clearing_unknown_payload(short count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x58, count);
        int length = 0x10 + count * 0x50;
        frame = frame with { Arena = frame.Arena.MarkUnknown(Bank, length).Write(Bank + length, [0xAC, 0xDC]) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(ArenaBase + Bank, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xB8));
        Assert.Equal(ArenaBase + Bank + (uint)length, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        Assert.Equal((ushort)count, Word(result, Bank));
        Assert.Equal(new byte[6], result.Arena.Read(Bank + 2, 6));
        for (int offset = 8; offset < length; offset++)
        {
            int recordOffset = (offset - 0x10) % 0x50;
            bool written = offset >= 0x10 && (recordOffset is 0 or 1 || recordOffset is >= 4 and < 12 || recordOffset is >= 0x30 and < 0x34);
            Assert.Equal(written, Known(result.Arena, Bank + offset));
            if (written)
            {
                Assert.Equal((byte)0, result.Arena.Read(Bank + offset, 1)[0]);
            }
        }
        Assert.Equal(new byte[] { 0xAC, 0xDC }, result.Arena.Read(Bank + length, 2));
        Assert.Equal((byte)6, result.Arena.Read(Slot + 0x1C, 1)[0]);
        Assert.Equal((byte)3, result.Core.Read(0x2797554, 1)[0]);
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1u, 4u)]
    [InlineData(6u, 8u)]
    [InlineData(0xFFFFFFFFu, 0u)]
    public void Allocator_aligns_raw_size_with_unsigned_wrap_and_never_dereferences_or_clears_memory(uint size, uint advance)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(Fixture(), Manager + 0x74, 0xFFFFFFFC);
        var result = NativeBattleWaterParticleCommands.AllocateB656E0(frame, size);
        Assert.Equal(0xFFFFFFFCu, result.Address);
        Assert.Equal(unchecked(0xFFFFFFFCu + advance), WaterSchedulerFixture.ReadCoreDword(result.Frame, Manager + 0x74));
        Assert.Same(frame.Arena, result.Frame.Arena);
    }

    [Fact]
    public void Registration_matches_masked_existing_entry_but_compares_full_current_header_and_only_literal_FF_terminates()
    {
        NativeBattleWaterExecutionFrame existing = Fixture().WriteTemporaryByte(0x142, 1);
        existing = existing with { Core = existing.Core.Write(0x2797554, [0x81]).MarkUnknown(0x2797555, 127) };
        NativeBattleWaterExecutionFrame same = NativeBattleWaterParticleCommands.RegisterDrawB65320(existing, 6);
        Assert.Same(existing.Core, same.Core);
        NativeBattleWaterExecutionFrame high = Fixture().WriteTemporaryByte(0x142, 0x80);
        high = high with { Core = high.Core.Write(0x2797554, [0, 0x7F, 0xFF]).MarkUnknown(0x2797557, 125) };
        NativeBattleWaterExecutionFrame appended = NativeBattleWaterParticleCommands.RegisterDrawB65320(high, 6);
        Assert.Equal(new byte[] { 0, 0x7F, 0x80 }, appended.Core.Read(0x2797554, 3));
        Assert.Throws<NotSupportedException>(() => appended.Core.Read(0x2797557, 1));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void Spawn_wraps_WORD_cursor_overwrites_active_modulo_record_and_preserves_unwritten_angle_and_tail(ushort count)
    {
        NativeBattleWaterExecutionFrame frame = SpawnFixture(count, 0xFFFF);
        int target = Bank + 0x10 + 0x50 * (0xFFFF % count);
        frame = frame with { Arena = frame.Arena.Write(target, Enumerable.Repeat((byte)0xA5, 0x50).ToArray()).MarkUnknown(target + 0x4A, 6) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal((ushort)0, Word(result, Bank + 2));
        Assert.Equal((ushort)7, Word(result, Bank + 8));
        Assert.Equal((ushort)1, Word(result, target));
        Assert.Equal((ushort)0, Word(result, target + 2));
        Assert.Equal(0xFFFFFEFCu, Dword(result, target + 4));
        Assert.Equal(0xA5A5A5A5u, Dword(result, target + 0x10));
        Assert.Equal(unchecked((uint)(-3 << 8)), Dword(result, target + 0x14));
        Assert.Equal(0x100u, Dword(result, target + 0x18));
        Assert.Equal(0x200u, Dword(result, target + 0x1C));
        Assert.Equal(new byte[16], result.Arena.Read(target + 0x20, 16));
        Assert.Equal(0x02345678u, Dword(result, target + 0x38));
        Assert.Equal(0u, Dword(result, target + 0x3C));
        Assert.Equal((ushort)0x1234, Word(result, target + 0x40));
        Assert.Equal((ushort)0x5678, Word(result, target + 0x48));
        Assert.Equal((byte)1, result.Arena.Read(target + 0x37, 1)[0]);
        Assert.False(Known(result.Arena, target + 0x4A));
        Assert.Equal(0u, result.ReadTemporaryDword(0x38));
        Assert.Equal(0u, result.ReadTemporaryDword(0x3C));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(WaterSchedulerFixture.ReadCoreDword(frame, Manager + 0x74), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
    }

    [Fact]
    public void Aliased_parent_positions_are_read_after_each_preceding_target_write()
    {
        int aliasBank = Slot + 0x70;
        int target = Slot + 0x80;
        NativeBattleWaterExecutionFrame frame = SpawnFixture(1, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, ArenaBase + (uint)aliasBank);
        frame = PutWord(frame, aliasBank, 1);
        frame = PutWord(frame, aliasBank + 2, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 2);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 3);
        NativeBattleWaterExecutionFrame result = Execute(frame);
        // X writes over owner94/96; Y then writes over owner98 before its next read.
        Assert.Equal(256u, Dword(result, target + 0x14));
        Assert.Equal(0u, Dword(result, target + 0x18));
        Assert.Equal(0u, Dword(result, target + 0x1C));
    }

    [Fact]
    public void Allocation_and_repeated_ring_overwrite_have_independent_JSON_next_equality()
    {
        NativeBattleWaterExecutionFrame allocated = Execute(Fixture(0x58, 16));
        NativeBattleWaterExecutionFrame spawn = SetInstruction(allocated, 0x59, -0x100);
        spawn = WaterSchedulerFixture.SlotWord(spawn, 0, 0x94, 14);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(spawn))!;
        Assert.Equal(JsonSerializer.Serialize(Execute(spawn)), JsonSerializer.Serialize(Execute(restored)));
    }

    [Theory]
    [InlineData(0x58, -1)]
    [InlineData(0x1059, 1)]
    public void Unsupported_negative_allocation_and_lookup_branch_fail_explicitly(ushort instruction, short operand)
    {
        Assert.Throws<NotSupportedException>(() => Execute(Fixture(instruction, operand)));
    }

    [Fact]
    public void Zero_capacity_ring_is_not_replaced_by_a_fallback_record()
    {
        NativeBattleWaterExecutionFrame frame = SpawnFixture(0, 0);
        Assert.Throws<NotSupportedException>(() => Execute(frame));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction = 0x58, short operand = 16)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x142, 3);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, ArenaBase + Bank);
        return SetInstruction(frame, instruction, operand);
    }

    private static NativeBattleWaterExecutionFrame SpawnFixture(ushort count, ushort cursor)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x59, -0x100);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, ArenaBase + Bank);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xFF345678);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0x1234);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9A, 0x5678);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)-3));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 2);
        frame = frame with { Arena = frame.Arena.Write(Slot + 0x18, [7]) };
        return PutWord(PutWord(frame, Bank, count), Bank + 2, cursor);
    }

    private static NativeBattleWaterExecutionFrame SetInstruction(NativeBattleWaterExecutionFrame frame, ushort instruction, short operand)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, instruction);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), operand);
        frame = frame with { Arena = frame.Arena.Write(0x1000, bytes) };
        return WaterSchedulerFixture.CoreDword(frame.WriteTemporaryWord(0x14A, instruction), 0x2797450, Pc);
    }

    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame) => NativeBattleWaterParticleCommands.Execute(frame, new(0x1000, 4, Pc));

    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }

    private static bool Known(NativeBattleEffectArena arena, int offset) => !arena.UnknownRegions.Any(region => region.Offset <= offset && offset < region.Offset + region.Length);
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
}
