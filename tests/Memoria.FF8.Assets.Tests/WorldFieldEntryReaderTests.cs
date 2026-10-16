using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldFieldEntryReaderTests
{
    [Theory]
    [InlineData(800, 48, true)]
    [InlineData(849, 48, true)]
    public void Installed_world_event_selects_Garden_battle_from_original_clause(ushort story, int entity, bool selected)
    {
        using WorldAssets assets = new(GameDirectory());
        WorldFieldSelection? result = assets.WorldEvents.SelectWorldEvent(new(-23410, 63204, story, entity), shape => shape == 79, NativeSavedWorldState.CreateNewGame().WorldEventFlags116!);
        if (!selected)
        {
            Assert.Null(result);
            return;
        }
        Assert.NotNull(result);
        Assert.Equal((7, 1016, 1040), (result.ClauseIndex, result.ClauseOffset, result.InstructionOffset));
        Assert.Equal(new WorldFieldEntry(64, 46, 1674, 71, 253, 128), result.Entry);
        Assert.Contains(result.Predicates, item => item.Opcode == 0xFF17 && item.Operand == 79 && item.Passed == true);
    }

    [Fact]
    public void Installed_world_event_precedes_terrain_packet_and_refuses_unimplemented_later_contracts()
    {
        using WorldAssets assets = new(GameDirectory());
        WorldFieldSelectionInput input = new(12560, -26800, 800, 48);
        Assert.Equal(253, assets.SelectWorldCallback(input, 8, shape => shape == 79, NativeWorldEventFlags.CreateNewGame())!.Entry.FieldId);
        Assert.Throws<NotSupportedException>(() => assets.WorldEvents.SelectWorldEvent(input, _ => false, NativeWorldEventFlags.CreateNewGame()));
        //Earlier flag61 invokes its original message script; it must not silently skip to the battle.
        Assert.Throws<NotSupportedException>(() => assets.WorldEvents.SelectWorldEvent(input, _ => true, new(1UL << 61, ulong.MaxValue)));
    }

    [Fact]
    public void Installed_Garden_callback_outside_battle_radius_reads_only_proven_flags_on_nonentrance_ground()
    {
        using WorldAssets assets = new(GameDirectory());
        NativeWorldEventFlags flags = new(4, 0xF000000000000084);
        WorldFieldSelectionInput input = new(-24233, 66878, 800, 48);
        Assert.Null(assets.WorldEvents.SelectWorldEvent(input, _ => false, flags, (opcode, operand) => opcode switch
        {
            0xFF1B when operand == 92 => false,
            0xFF25 => operand == 0,
            0xFF32 => operand != 1,
            0xFF29 => false,
            0xFF2D when (operand & 255) == 1 => operand >> 8 == 0,
            0xFF31 when (operand & 255) == 1 => false,
            _ => throw new NotSupportedException($"Unexpected installed ordinary callback predicate{opcode:X4}/{operand}.")
        }));
    }

    [Fact]
    public void Partial_world_flag_authority_refuses_only_when_the_reached_bit_is_unknown()
    {
        NativeWorldEventFlags partial = new(4, 0xF000000000000004);
        Assert.True(partial.Matches(258));
        Assert.False(partial.Matches(317));
        Assert.Throws<NotSupportedException>(() => partial.Matches(263));
        Assert.Throws<InvalidDataException>(() => new NativeWorldEventFlags(4, 0).Matches(258));
    }

    [Fact]
    public void Pure_later_predicates_reject_unknown_flag_alternatives_without_hiding_unknown_selected_commands()
    {
        byte[] rejected = Program([(0xFF01, 0), (0xFF27, 5), (0xFF03, 482), (0xFF04, 0), (0xFF08, 0), (0xFF16, 0)], [(0xFF01, 0), (0xFF04, 0), (0xFF08, 0), (0xFF16, 0)]);
        WorldFieldEntryReader events = WorldFieldEntryReader.Read(rejected, Packet()).ReadWorldEvents(rejected);
        WorldFieldSelectionInput input = new(0, 0, 800, 48);
        WorldFieldSelection selected = Assert.IsType<WorldFieldSelection>(events.SelectWorldEvent(input, _ => false, new(0, 0)));
        Assert.Equal(1, selected.ClauseIndex);
        Assert.Contains(selected.Predicates, item => item.Opcode == 0xFF27 && item.Passed is null);
        byte[] dependent = Program([(0xFF01, 0), (0xFF27, 5), (0xFF02, 800), (0xFF04, 0), (0xFF08, 0), (0xFF16, 0)]);
        Assert.Throws<NotSupportedException>(() => WorldFieldEntryReader.Read(dependent, Packet()).ReadWorldEvents(dependent).SelectWorldEvent(input, _ => false, new(0, 0)));
        byte[] random = Program([(0xFF01, 0), (0xFF27, 5), (0xFF2F, 2800), (0xFF03, 482), (0xFF04, 0), (0xFF08, 0), (0xFF16, 0)]);
        bool evaluatedRandom = false;
        Assert.Throws<NotSupportedException>(() => WorldFieldEntryReader.Read(random, Packet()).ReadWorldEvents(random).SelectWorldEvent(input, _ => false, new(0, 0), (_, _) =>
        {
            evaluatedRandom = true;
            return false;
        }));
        Assert.False(evaluatedRandom);
    }

    [Theory]
    [InlineData(799, 48, false)]
    [InlineData(800, 48, true)]
    [InlineData(849, 48, true)]
    [InlineData(850, 48, false)]
    [InlineData(800, 0, false)]
    public void Event_predicates_keep_native_entity_and_story_boundaries(ushort story, int entity, bool selected)
    {
        byte[] program = Program([(0xFF01, 0), (0xFF09, 48), (0xFF02, 800), (0xFF03, 850), (0xFF17, 79), (0xFF04, 0), (0xFF08, 0), (0xFF16, 0)]);
        WorldFieldEntryReader events = WorldFieldEntryReader.Read(program, Packet()).ReadWorldEvents(program);
        Assert.Equal(selected, events.SelectWorldEvent(new(0, 0, story, entity), _ => true, NativeWorldEventFlags.CreateNewGame()) is not null);
        Assert.Null(events.SelectWorldEvent(new(0, 0, story, entity), _ => false, NativeWorldEventFlags.CreateNewGame()));
    }

    [Fact]
    public void World_event_continues_after_clause_terminator_and_returns_first_packet_immediately()
    {
        byte[] table = Packet().Concat(Packet(1, 2, 3, 4, 5)).ToArray();
        byte[] program = Program([(0xFF01, 0), (0xFF04, 0), (0xFF16, 0)], [(0xFF01, 0), (0xFF04, 0), (0xFF08, 1), (0xFF20, 0)]);
        WorldFieldEntryReader terrain = WorldFieldEntryReader.Read(program, table);
        WorldFieldSelectionInput input = new(0, 0, 800, 48);
        Assert.Null(terrain.Select(input, 8));
        WorldFieldSelection result = Assert.IsType<WorldFieldSelection>(terrain.ReadWorldEvents(program).SelectWorldEvent(input, _ => false, NativeWorldEventFlags.CreateNewGame()));
        Assert.Equal(1, result.ClauseIndex);
        Assert.Equal(1, result.Entry.Index);
    }

    [Fact]
    public void Selected_world_event_executes_commands_in_order_and_stops_at_first_packet()
    {
        byte[] program = Program([(0xFF01, 0), (0xFF04, 0), (0xFF28, 0x013D), (0xFF2E, 0xFF01), (0xFF08, 0), (0xFF28, 0x003D), (0xFF05, 0)]);
        WorldFieldEntryReader events = WorldFieldEntryReader.Read(program, Packet()).ReadWorldEvents(program);
        var commands = new List<(ushort Opcode, ushort Operand)>();
        WorldFieldSelection result = Assert.IsType<WorldFieldSelection>(events.SelectWorldEvent(new(0, 0, 800, 48), _ => false, NativeWorldEventFlags.CreateNewGame(), executeRuntimeCommand: (opcode, operand) => commands.Add((opcode, operand))));
        Assert.Equal(new (ushort, ushort)[] { (0xFF28, 0x013D), (0xFF2E, 0xFF01) }, commands);
        Assert.Equal(16, result.InstructionOffset);
        Assert.Equal(0, result.Entry.Index);
    }

    [Fact]
    public void Command_only_world_event_ends_selected_stream_without_selecting_a_later_clause()
    {
        byte[] program = Program([(0xFF01, 0), (0xFF04, 0), (0xFF28, 0x013D), (0xFF0E, 32), (0xFF2E, 0xFF01), (0xFF05, 0)], [(0xFF01, 0), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0)]);
        WorldFieldEntryReader events = WorldFieldEntryReader.Read(program, Packet()).ReadWorldEvents(program);
        var commands = new List<(ushort Opcode, ushort Operand)>();
        Assert.Null(events.SelectWorldEvent(new(0, 0, 800, 48), _ => false, NativeWorldEventFlags.CreateNewGame(), executeRuntimeCommand: (opcode, operand) => commands.Add((opcode, operand))));
        Assert.Equal(new (ushort, ushort)[] { (0xFF28, 0x013D) }, commands);
    }

    [Fact]
    public void Reached_world_commands_require_runtime_support_and_failed_branches_do_not_execute_them()
    {
        byte[] program = Program([(0xFF01, 0), (0xFF04, 0), (0xFF0A, 0), (0xFF09, 49), (0xFF0B, 0), (0xFF28, 0x013D), (0xFF05, 0), (0xFF0D, 0), (0xFF2E, 0xFF01), (0xFF05, 0)]);
        WorldFieldEntryReader events = WorldFieldEntryReader.Read(program, Packet()).ReadWorldEvents(program);
        var input = new WorldFieldSelectionInput(0, 0, 800, 48);
        Assert.Throws<NotSupportedException>(() => events.SelectWorldEvent(input, _ => false, NativeWorldEventFlags.CreateNewGame()));
        var commands = new List<(ushort Opcode, ushort Operand)>();
        Assert.Null(events.SelectWorldEvent(input, _ => false, NativeWorldEventFlags.CreateNewGame(), executeRuntimeCommand: (opcode, operand) => commands.Add((opcode, operand))));
        Assert.Equal(new (ushort, ushort)[] { (0xFF2E, 0xFF01) }, commands);
    }

    [Theory]
    [InlineData(3000, true)]
    [InlineData(3001, false)]
    [InlineData(65536 + 3000, true)]
    public void Allocated_shape_predicate_uses_eligible_ordered_records_and_inclusive_narrowed_horizontal_distance(int distance, bool expected)
    {
        NativeGardenWorldObject[] objects =
        [
            new(79, 0, new(0, 0, 0), 0, 0, 0),
            new(79, 0, new(0, 0, 0), 0, 0, 0),
            new(79, 0, new(distance, short.MaxValue, 0), 0, 0, 0)
        ];
        WorldFieldSelectionInput input = new(0, 0, 800, 48);
        Assert.Equal(expected, WorldAllocatedShapeProximity.Evaluate(input, 79, objects, index => index != 1));
        Assert.False(WorldAllocatedShapeProximity.Evaluate(input, 79, objects[..2], index => index != 1));
    }

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

    [Theory]
    [InlineData(0xFF0F, 8191, false)]
    [InlineData(0xFF0F, 32768, true)]
    [InlineData(0xFF10, 8190, false)]
    [InlineData(0xFF10, 65535, true)]
    [InlineData(0xFF11, 8191, false)]
    [InlineData(0xFF11, 8190, true)]
    [InlineData(0xFF12, 8190, false)]
    [InlineData(0xFF12, 8189, true)]
    public void Local_coordinate_predicates_mask_negative_world_coordinates_and_compare_unsigned_operands_strictly(ushort opcode, ushort operand, bool expected)
    {
        byte[] program = Program([(0xFF01, 0), (opcode, operand), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]);
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(program, Packet());
        Assert.Equal(expected, reader.Select(new(-1, -2, 0, 0), 8) is not null);
    }

    [Theory]
    [InlineData(48, 48, true)]
    [InlineData(48, 49, false)]
    [InlineData(128, -1, false)]
    [InlineData(128, 9, true)]
    [InlineData(128, 10, false)]
    [InlineData(128, 128, true)]
    [InlineData(129, -1, false)]
    [InlineData(129, 1, true)]
    [InlineData(129, 2, false)]
    [InlineData(130, 7, false)]
    [InlineData(130, 8, true)]
    [InlineData(130, 9, true)]
    [InlineData(130, 10, false)]
    [InlineData(131, 15, false)]
    [InlineData(131, 16, true)]
    [InlineData(131, 22, true)]
    [InlineData(131, 23, false)]
    [InlineData(132, 31, false)]
    [InlineData(132, 32, true)]
    [InlineData(132, 40, true)]
    [InlineData(132, 41, false)]
    [InlineData(132, 132, true)]
    [InlineData(133, 33, false)]
    [InlineData(133, 34, true)]
    [InlineData(133, 40, true)]
    [InlineData(133, 41, false)]
    [InlineData(65535, -1, true)]
    public void Entity_predicate_keeps_native_exact_groups_unsigned_guards_and_default_match(ushort operand, int type, bool expected)
    {
        byte[] program = Program([(0xFF01, 0), (0xFF09, operand), (0xFF04, 0), (0xFF08, 0), (0xFF05, 0), (0xFF16, 0)]);
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(program, Packet());
        Assert.Equal(expected, reader.Select(new(0, 0, 0, type), 8) is not null);
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




    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(254, 0, true)]
    [InlineData(255, 0, false)]
    [InlineData(255, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(1, 257, false)]
    [InlineData(1, 65535, false)]
    [InlineData(0, 32768, false)]
    public void Saved_world_flag109_masks_only_the_byte_and_compares_the_full_signed_operand(byte flag, ushort operand, bool expected)
    {
        Assert.Equal(expected, WorldFieldEntryReader.MatchesSavedWorldFlag109(flag, operand));
        Assert.True(WorldFieldEntryReader.MatchesSavedWorldFlag109(flag, operand, true));
    }

    [Theory]
    [InlineData(899, 0, 0, false)]
    [InlineData(900, 0, 0, true)]
    [InlineData(912, 0, 254, true)]
    [InlineData(912, 0, 1, false)]
    [InlineData(912, 48, 0, false)]
    [InlineData(912, 49, 0, true)]
    public void Installed_orphanage_selector_uses_actual_flag109_callback(ushort story, int entity, byte flag, bool expected)
    {
        using WorldAssets assets = new(GameDirectory());
        WorldFieldSelectionInput input = new(-29440, 69760, story, entity);
        WorldFieldSelection? selected = assets.FieldEntries.Select(input, 8, (opcode, operand) => opcode == 0xFF21 ? WorldFieldEntryReader.MatchesSavedWorldFlag109(flag, operand) : throw new NotSupportedException());
        Assert.Equal(expected, selected is not null);
        if (selected is not null)
        {
            Assert.Equal(new WorldFieldEntry(18, -139, -789, 13, 505, 128), selected.Entry);
            Assert.Contains(selected.Predicates, item => item.Opcode == 0xFF21 && item.Operand == 0 && item.Passed == true);
        }
        if (story >= 900)
        {
            Assert.Throws<NotSupportedException>(() => assets.FieldEntries.Select(input, 8));
        }
        Assert.Null(assets.FieldEntries.Select(input, 0));
    }
    [Fact]
    public void UnknownTextReadinessAndReleasedInputRejectTheWholeBranchBeforeItsCommands()
    {
        byte[] program = Program([(0xFF01, 0), (0xFF04, 0), (0xFF0A, 0), (0xFF2C, 0x0109), (0xFF20, 0xFFFF), (0xFF0B, 0), (0xFF28, 0x0104), (0xFF05, 0), (0xFF0D, 0), (0xFF28, 0x0105), (0xFF05, 0), (0xFF16, 0)]);
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(program, Packet()).ReadWorldEvents(program);
        var commands = new List<ushort>();
        Assert.Null(reader.SelectWorldEvent(new(0, 0, 3500, 50), _ => false, NativeWorldEventFlags.CreateNewGame(), (opcode, _) => opcode == 0xFF2C ? null : opcode == 0xFF20 ? false : throw new InvalidOperationException(), (_, operand) => commands.Add(operand)));
        Assert.Equal(new ushort[] { 0x0105 }, commands);
    }

    [Theory]
    [InlineData((ushort)0xFF20)]
    [InlineData((ushort)0xFF2F)]
    public void UnknownTextReadinessCannotSelectCommandsOrAdvanceAnImpureQuery(ushort following)
    {
        byte[] program = Program([(0xFF01, 0), (0xFF04, 0), (0xFF0A, 0), (0xFF2C, 0x0109), (following, 0xFFFF), (0xFF0B, 0), (0xFF28, 0x0104), (0xFF05, 0), (0xFF16, 0)]);
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(program, Packet()).ReadWorldEvents(program);
        int impureReads = 0;
        int commands = 0;
        Assert.Throws<NotSupportedException>(() => reader.SelectWorldEvent(new(0, 0, 3500, 50), _ => false, NativeWorldEventFlags.CreateNewGame(), (opcode, _) =>
        {
            if (opcode == 0xFF2F)
            {
                impureReads++;
            }
            return opcode == 0xFF2C ? null : true;
        }, (_, _) => commands++));
        Assert.Equal(0, commands);
        Assert.Equal(0, impureReads);
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
        return Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
    }
}
