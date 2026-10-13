using System.Buffers.Binary;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldFieldEntryReaderTests
{
    [Theory]
    [InlineData(16400, -30000, 17, 0, 12, 0, 159, "bggate_1")]
    [InlineData(24576, -29406, 17, 0, 13, 0, 159, "bggate_1")]
    [InlineData(12560, -26800, 17, 0, 11, 1, 106, "bcgate_1")]
    [InlineData(12560, -26800, 17, 32, 11, 1, 106, "bcgate_1")]
    [InlineData(30200, -28000, 17, 0, 13, 2, 136, "bdview1")]
    [InlineData(26623, -28000, 569, 0, 13, 0, 159, "bggate_1")]
    public void Installed_section7_selects_original_Garden_Balamb_and_Fire_Cavern_packets(int x, int z, ushort story, int type, int clause, int row, int fieldId, string fieldName)
    {
        string directory = GameDirectory();
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world/main/field archives are required.");
        using var assets = new WorldAssets(directory);
        WorldFieldEntryReader reader = assets.FieldEntries;
        Assert.Equal(38, reader.ClauseCount);
        Assert.Equal(2652, reader.ProgramLength);
        Assert.Equal(72, reader.Entries.Count);
        WorldFieldSelection selection = Assert.IsType<WorldFieldSelection>(reader.Select(new WorldFieldSelectionInput(x, z, story, type), 8));
        Assert.Equal(clause, selection.ClauseIndex);
        Assert.Equal(row, selection.Entry.Index);
        Assert.Equal(fieldId, selection.Entry.FieldId);
        Assert.NotEmpty(selection.Predicates);
        using var fields = new GameAssets(directory);
        Assert.Equal(fieldName, fields.LoadField(selection.Entry.FieldId).Name);
        Assert.Equal(new WorldFieldEntry(0, -1865, -1694, 247, 159, 80), reader.Entries[0]);
        Assert.Equal(new WorldFieldEntry(1, 58, 4327, 197, 106, 0), reader.Entries[1]);
        Assert.Equal(new WorldFieldEntry(2, 175, -2269, 12, 136, 128), reader.Entries[2]);
        Assert.Equal(1, assets.GetNativeMapLocationId(x, z));
    }

    [Theory]
    [InlineData(30112, -28000, 17)]
    [InlineData(26624, -28000, 17)]
    [InlineData(24576, -29406, 570)]
    [InlineData(16400, -30000, 570)]
    public void Original_local_boundaries_are_strict_and_Garden_upper_story_bound_is_exclusive(int x, int z, ushort story)
    {
        string directory = GameDirectory();
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world/main archives are required.");
        using var assets = new WorldAssets(directory);
        Assert.Null(assets.FieldEntries.Select(new WorldFieldSelectionInput(x, z, story, 0), 8));
    }

    [Fact]
    public void Reader_preserves_signed_coordinates_unsigned_triangle_and_direction_byte_at_native_offsets()
    {
        byte[] table = Packet(short.MinValue, -123, 0x8001, 0xF123, 255);
        table[9] = 17;
        table[10] = 99;
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]), table);
        WorldFieldSelection result = Assert.IsType<WorldFieldSelection>(reader.Select(new WorldFieldSelectionInput(0, 0, 0, 0), 8));
        Assert.Equal(new WorldFieldEntry(0, short.MinValue, -123, 0x8001, 0xF123, 255), result.Entry);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    public void Native_story_range_is_lower_inclusive_and_upper_exclusive(ushort story, bool selected)
    {
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF02, 10), (0xFF03, 20), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]), Packet());
        Assert.Equal(selected, reader.Select(new WorldFieldSelectionInput(0, 0, story, 0), 8) is not null);
    }

    [Fact]
    public void Entrance_flag_and_failed_native_predicates_do_not_reach_unsupported_state()
    {
        WorldFieldEntryReader unknown = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF20, 64), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]), Packet());
        var input = new WorldFieldSelectionInput(0, 0, 17, 0);
        Assert.Null(unknown.Select(input, 0));
        Assert.Throws<NotSupportedException>(() => unknown.Select(input, 8));
        WorldFieldEntryReader shortCircuit = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF06, 273), (0xFF20, 64), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)], [(0xFF01, 0), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]), Packet());
        Assert.Equal(1, Assert.IsType<WorldFieldSelection>(shortCircuit.Select(input, 8)).ClauseIndex);
        WorldFieldEntryReader failedBranch = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF04, 0), (0xFF0A, 0), (0xFF09, 49), (0xFF20, 64), (0xFF0B, 0), (0xFF08, 70), (0xFF05, 0), (0xFF0C, 0), (0xFF09, 128), (0xFF0B, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]), Packet());
        Assert.Equal(0, Assert.IsType<WorldFieldSelection>(failedBranch.Select(input, 8)).Entry.Index);
    }

    [Fact]
    public void Selection_returns_first_native_clause_and_last_packet_command_before_end()
    {
        byte[] table = Packet().Concat(Packet(1, 2, 3, 4, 5)).ToArray();
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF04, 0), (0xFF08, 0), (0xFF08, 1), (0xFF05, 0), (0xFF16, 0)], [(0xFF01, 0), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]), table);
        WorldFieldSelection result = Assert.IsType<WorldFieldSelection>(reader.Select(new WorldFieldSelectionInput(0, 0, 0, 0), 8));
        Assert.Equal(0, result.ClauseIndex);
        Assert.Equal(new WorldFieldEntry(1, 1, 2, 3, 4, 5), result.Entry);
    }

    [Fact]
    public void Malformed_tables_missing_packets_and_reached_jump_cycles_refuse()
    {
        Assert.Throws<InvalidDataException>(() => WorldFieldEntryReader.Read(new byte[3], Packet()));
        Assert.Throws<InvalidDataException>(() => WorldFieldEntryReader.Read(new byte[4], new byte[23]));
        Assert.Throws<InvalidDataException>(() => WorldFieldEntryReader.Read(new byte[] { 4, 0, 0, 0 }, Packet()));
        WorldFieldEntryReader missing = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF04, 0), (0xFF08, 1), (0xFF05, 0), (0xFF16, 0)]), Packet());
        Assert.Throws<InvalidDataException>(() => missing.Select(new WorldFieldSelectionInput(0, 0, 0, 0), 8));
        WorldFieldEntryReader cycle = WorldFieldEntryReader.Read(Program([(0xFF01, 0), (0xFF0E, 12)]), Packet());
        Assert.Throws<InvalidDataException>(() => cycle.Select(new WorldFieldSelectionInput(0, 0, 0, 0), 8));
    }

    private static byte[] Program(params (ushort Opcode, ushort Operand)[][] clauses)
    {
        int position = (clauses.Length + 1) * 4;
        byte[] bytes = new byte[position + clauses.Sum(clause => clause.Length * 4)];
        for (int index = 0; index < clauses.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 4), position);
            foreach ((ushort opcode, ushort operand) in clauses[index])
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(position), opcode);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(position + 2), operand);
                position += 4;
            }
        }
        return bytes;
    }

    private static byte[] Packet(short x = 0, short y = 0, ushort triangle = 0, ushort fieldId = 74, byte direction = 0)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), triangle);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), fieldId);
        bytes[8] = direction;
        return bytes;
    }

    private static string GameDirectory()
    {
        return Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
    }
}
