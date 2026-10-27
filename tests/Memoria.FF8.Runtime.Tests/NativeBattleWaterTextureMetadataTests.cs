using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterTextureMetadataTests
{
    private const uint Manager = 0x2797690;
    private const uint ArenaBase = 0x20DFAB8;
    private const int Slot = 0x2000;
    private const int Instruction = 0x4000;

    [Theory]
    [InlineData(704, 256, 0x80, 0x60, 0xFB)]
    [InlineData(-1, 0, 0x80, 0x20, 0xBF)]
    [InlineData(0, 0, 1, 0x40, 0x40)]
    public void Page_getter_uses_signed_X_depth_flag_and_current_blend_without_source_index_masking(short x, short y, byte sourceFlags, byte blend, uint expected)
    {
        NativeBattleWater222Profile profile = Profile(x, y, sourceFlags);
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(Slot + 0x92, [blend, 0xCC]) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterTextureMetadata.SelectTPageB665C0(frame, profile, 1);
        Assert.Equal(expected, result.ReadTemporaryDword(0xFC));
        Assert.Equal(0u, result.ReadTemporaryDword(0xF8));
        Assert.Equal(frame.Arena, result.Arena);
        Assert.Equal(frame.Core, result.Core);
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(-1, -1, 0x7FFF)]
    [InlineData(16, 512, 1)]
    [InlineData(-32, 257, 0x407E)]
    public void Clut_getter_uses_unsigned_Y_mask_and_signed_X_shift(short x, short y, ushort expected)
    {
        NativeBattleWater222Profile profile = Profile(0, 0, 0);
        byte[] tables = profile.Tables.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(48), x);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(50), y);
        profile = profile with { Tables = profile.Tables with { Data = tables.ToImmutableArray() } };
        NativeBattleWaterExecutionFrame frame = Fixture();
        Assert.Equal(expected, NativeBattleWaterTextureMetadata.ReadClutB66640(frame, profile, 1));
        Assert.Equal(0xABCD1234u, frame.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void Outer52_captures_old_blend_before_replacing_page_and_updates_only_selected_slot_words_and_PC()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(Slot + 0x92, [0x60, 0xDD]) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterTextureMetadata.Execute52AA3A60(frame, Profile(704, 256, 0x80));
        Assert.Equal((ushort)0xFB, SlotWord(result, 0x92));
        Assert.Equal((ushort)0, SlotWord(result, 0x9E));
        Assert.Equal((ushort)0x401, SlotWord(result, 0x9A));
        Assert.Equal(ArenaBase + Instruction + 4, CoreDword(result, 0x2797450));
        byte[] before = frame.Arena.Read(Slot, 0x100);
        byte[] after = result.Arena.Read(Slot, 0x100);
        foreach (int offset in Enumerable.Range(0, 0x100).Where(offset => offset is not (0x92 or 0x93 or 0x9A or 0x9B or 0x9E or 0x9F)))
        {
            Assert.Equal(before[offset], after[offset]);
        }
    }

    [Fact]
    public void Minus_one_clears_words_without_resolving_profile_metadata_or_consuming_frame_F8_FC()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(-1);
        frame = frame with { Core = frame.Core.MarkUnknown(Manager + 0x98, 16) };
        bool[] known = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame).Known.ToArray();
        Array.Fill(known, false, 0xF8, 8);
        frame = frame with { TemporaryFrame = frame.TemporaryFrame.SetKnownness(0, known) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterTextureMetadata.Execute52AA3A60(frame, null!);
        Assert.Equal((ushort)0, SlotWord(result, 0x92));
        Assert.Equal((ushort)0, SlotWord(result, 0x9A));
        Assert.Equal((ushort)0, SlotWord(result, 0x9E));
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(ArenaBase + Instruction + 4, CoreDword(result, 0x2797450));
    }

    [Fact]
    public void UnavailableAddressedMetadataDoesNotWrapIntoTheUploadRange()
    {
        const int index = 65537;

        Assert.Throws<NotSupportedException>(() => NativeBattleWaterTextureMetadata.SelectTPageB665C0(Fixture(), Profile(0, 0, 0), index));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterTextureMetadata.ReadClutB66640(Fixture(), Profile(0, 0, 0), index));
    }

    [Fact]
    public void NegativeNonSentinelSelectorUsesSignedPointerArithmeticWithinTheRetainedOwner()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(-2);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterTextureMetadata.Execute52AA3A60(frame, Profile(0, 0, 0));
        Assert.Equal((ushort)0x20, SlotWord(result, 0x92));
        Assert.Equal((ushort)0, SlotWord(result, 0x9A));
        Assert.Equal(ArenaBase + Instruction + 4, CoreDword(result, 0x2797450));
    }

    [Fact]
    public void MetadataPointerArithmeticCanReachAdjacentBytesBeyondTheUploadCount()
    {
        NativeBattleWater222Profile profile = Profile(0, 0, 0);
        byte[] tables = profile.Tables.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(40), 704);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(42), 256);
        tables[85] = 0x80;
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(80), 16);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(82), 16);
        profile = profile with { Tables = profile.Tables with { Data = tables.ToImmutableArray() } };
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(Slot + 0x92, [0x60]) };
        Assert.Equal(0xFBu, NativeBattleWaterTextureMetadata.SelectTPageB665C0(frame, profile, 5).ReadTemporaryDword(0xFC));
        Assert.Equal((ushort)0x401, NativeBattleWaterTextureMetadata.ReadClutB66640(frame, profile, 5));
    }

    [Theory]
    [InlineData(0x98)]
    [InlineData(0xA4)]
    [InlineData(0x9C)]
    public void Changed_current_pointer_cannot_be_hydrated_from_baseline_tables(int offset)
    {
        NativeBattleWaterExecutionFrame frame = CoreDword(Fixture(), Manager + (uint)offset, 0x803FF000);
        NativeBattleWater222Profile profile = Profile(704, 256, 0x80);
        if (offset == 0x9C)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleWaterTextureMetadata.ReadClutB66640(frame, profile, 1));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleWaterTextureMetadata.SelectTPageB665C0(frame, profile, 1));
        }
    }

    [Fact]
    public void Independent_JSON_next_preserves_current_page_blend_and_getter_frame_owners()
    {
        NativeBattleWater222Profile profile = Profile(704, 256, 0x80);
        NativeBattleWaterExecutionFrame first = NativeBattleWaterTextureMetadata.Execute52AA3A60(Fixture(), profile);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(first))!;
        first = CoreDword(first, 0x2797450, ArenaBase + Instruction);
        restored = CoreDword(restored, 0x2797450, ArenaBase + Instruction);
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleWaterTextureMetadata.Execute52AA3A60(first, profile)), NativeBattleTestSnapshots.Json(NativeBattleWaterTextureMetadata.Execute52AA3A60(restored, profile)));
    }

    private static NativeBattleWater222Profile Profile(short x, short y, byte sourceFlags)
    {
        byte[] tables = new byte[0x70];
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(8), x);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(10), y);
        tables[81] = sourceFlags;
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(48), 16);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(50), 16);
        byte[] directory = new byte[0x1C];
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(0), 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(4), 0x48);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(8), 0x70);
        return new(new("tables", tables.ToImmutableArray()), new("directory", directory.ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray()));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short selector = 1)
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = CoreDword(frame, 0x27973EC, Manager);
        frame = CoreDword(frame, 0x27973E8, ArenaBase + Slot);
        frame = CoreDword(frame, Manager + 0x98, 0x186C86C);
        frame = CoreDword(frame, Manager + 0x9C, 0x186C894);
        frame = CoreDword(frame, Manager + 0xA4, 0x186C8BC);
        frame = CoreDword(frame, 0x2797450, ArenaBase + Instruction);
        byte[] instruction = [0x52, 0, 0, 0];
        BinaryPrimitives.WriteInt16LittleEndian(instruction.AsSpan(2), selector);
        frame = frame with { Arena = frame.Arena.Write(Instruction, instruction).Write(Slot, Enumerable.Repeat((byte)0xA5, 0x100).ToArray()) };
        return frame.WriteTemporaryDword(0xF8, 0x12345678).WriteTemporaryDword(0xFC, 0xABCD1234);
    }

    private static NativeBattleWaterExecutionFrame CoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }

    private static uint CoreDword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(address, 4));
    private static ushort SlotWord(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(Slot + offset, 2));
}
