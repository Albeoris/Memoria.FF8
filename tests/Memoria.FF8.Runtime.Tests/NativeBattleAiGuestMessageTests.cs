using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiGuestMessageTests
{
    private const uint Section = NativeBattleFileStaging.Address;
    private const int TableOffset = 0x20;
    private const uint Table = Section + TableOffset;
    private static readonly NativeBattleAiHeapTextDirectory Directory = new(TableOffset, "en");

    [Fact]
    public void Reads_the_live_word_and_only_known_bytes_through_the_first_terminator()
    {
        NativeBattleFileStaging guest = CreateGuest().Write(Table + 4, [0x10, 0]).Write(Table + 0x10, [0x45, 0x46, 0]);
        (NativeBattleAiHeapMessage message, string text) = Directory.Read(guest.Read, Section, 2);

        Assert.Equal("AB", text);
        Assert.Equal(Table + 4, message.EntryAddress);
        Assert.Equal((ushort)0x10, message.EntryOffset);
        Assert.Equal(Table + 0x10, message.TextAddress);
        Assert.Equal(new byte[] { 0x45, 0x46, 0 }, message.Encoded.ToArray());

        guest = guest.Write(Table + 4, [0x20, 0]).Write(Table + 0x20, [0]);
        (message, text) = Directory.Read(guest.Read, Section, 2);
        Assert.Equal(string.Empty, text);
        Assert.Equal(Table + 0x20, message.TextAddress);
        Assert.Equal(new byte[] { 0 }, message.Encoded.ToArray());
    }

    [Fact]
    public void Distinguishes_native_empty_sentinel_from_unobserved_word_or_destination()
    {
        NativeBattleFileStaging guest = CreateGuest().Write(Table, [0xFF, 0xFF]).Write(Table + 4, [0x22, 0]);
        (NativeBattleAiHeapMessage message, string text) = Directory.Read(guest.Read, Section, 0);
        Assert.Equal(string.Empty, text);
        Assert.Equal(ushort.MaxValue, message.EntryOffset);
        Assert.Null(message.TextAddress);

        Assert.Throws<NotSupportedException>(() => Directory.Read(guest.Read, Section, 1));
        Assert.Throws<NotSupportedException>(() => Directory.Read(guest.Read, Section, 2));
        guest = guest.Write(Table + 0x22, [0x45]);
        Assert.Throws<NotSupportedException>(() => Directory.Read(guest.Read, Section, 2));
    }

    [Fact]
    public void Rejects_incoherent_directory_unsupported_encoding_and_ring_row_overflow()
    {
        NativeBattleFileStaging guest = CreateGuest().Write(Table, [0x20, 0]).Write(Table + 0x20, [0x10, 0]);
        Assert.Throws<InvalidDataException>(() => (Directory with { Offset = TableOffset + 2 }).Read(guest.Read, Section, 0));
        NotSupportedException encoding = Assert.Throws<NotSupportedException>(() => Directory.Read(guest.Read, Section, 0));
        Assert.Contains("encoded bytes 10", encoding.Message, StringComparison.Ordinal);

        guest = guest.Write(Table + 0x20, Enumerable.Repeat((byte)0x45, 48).ToArray());
        Assert.Throws<NotSupportedException>(() => Directory.Read(guest.Read, Section, 0));
    }

    [Fact]
    public void Original_admission_requires_installed_zero_entry_directory_metadata()
    {
        NativeBattleAiDefinition program = new([[0], [26, 2, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(program, [], 510, originalEnemySlots: true));
        NativeBattleAiDefinition heap = program with { HeapTextDirectory = Directory };
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(heap, [], 510, originalEnemySlots: true));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(heap with { Texts = ["A"] }, [], 510, originalEnemySlots: true));
    }

    private static NativeBattleFileStaging CreateGuest()
    {
        byte[] header = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 3);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), TableOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), TableOffset);
        return NativeBattleFileStaging.Unknown().Write(Section, header);
    }
}
