using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterInstructionDispatcherTests
{
    private const uint ArenaBase = 0x20DFAB8;
    private const uint Manager = 0x2797690;
    private const uint Header = 0x2797068;
    private const uint Pc = 0xFFFFFFFC;
    private const int File0 = 0x10000;
    private const int File1 = 0x40000;
    private const int SlotBase = File1 + 0x58;

    [Fact]
    public void Fetch_reads_current_bounded_program_bytes_and_never_reuses_an_original_resource_copy()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x80D4, -2);
        Assert.Equal((ushort)0x80D4, NativeBattleWaterInstructionDispatcher.ReadInstruction(frame, Pc, Services()));
        frame = frame with { Arena = frame.Arena.Write(0x1000, [0x55, 0x06]) };
        Assert.Equal((ushort)0x0655, NativeBattleWaterInstructionDispatcher.ReadInstruction(frame, Pc, Services()));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterInstructionDispatcher.ReadInstruction(frame, unchecked(Pc + 16), Services()));
    }

    [Fact]
    public void Growth_then_reference_command_derives_new_bank_capacity_and_current_physical_slot()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x7D, 16);
        frame = frame with { Arena = frame.Arena.Write(SlotBase + 15 * 0x100 + 4, [0xD1, 0xD2]) };
        frame = Run(frame, 0x7D, Resources(), Services());
        Assert.Equal(16, NativeBattleWaterInstructionDispatcher.CurrentBank(frame, Resources()).Capacity);
        Assert.Equal(new byte[] { 0xD1, 0xD2 }, frame.Arena.Read(SlotBase + 15 * 0x100 + 4, 2));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973E8, ArenaBase + (uint)(SlotBase + 15 * 0x100));
        frame = frame with { Core = frame.Core.Write(0x2797454, [15, 0xFF]) };
        frame = PutWord(frame, SlotBase + 15 * 0x100 + 0x12, 123);
        frame = PutDword(frame, SlotBase + 15 * 0x100 + 0x50, 0x76543210);
        frame = Instruction(frame, 0x61, 1, 123);
        NativeBattleWaterExecutionFrame result = Run(frame, 0x61, Resources(), Services());
        Assert.Equal(15, NativeBattleWaterInstructionDispatcher.CurrentIndex(result, NativeBattleWaterInstructionDispatcher.CurrentBank(result, Resources())));
        Assert.Equal(0x76543210u, Dword(result, SlotBase + 15 * 0x100 + 0x50));
        Assert.Equal((ushort)0x7654, Word(result, SlotBase + 15 * 0x100 + 0x8C));
    }

    [Fact]
    public void Terminal_header_command_does_not_resolve_old_PC_program_resource_or_reset_inputs()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973C0, 0x2797628);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, [1, 1]).MarkUnknown(0x2797450, 4).MarkUnknown(0x2798A6C, 4) };
        NativeBattleWaterInstructionServices services = Services() with { ResolveProgram = UnexpectedProgram };
        NativeBattleWaterExecutionFrame result = Run(frame, 1, null!, services);
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0xFFFF, result.ReadTemporaryWord(0x13E));
        Assert.Equal(new byte[] { 4, 0 }, result.Core.Read(Header + 0x1A, 2));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x2798A6C, 4));
    }

    [Fact]
    public void Vector_dispatch_reuses_existing_math_random_and_selected_post_action()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFC0A, 1, 2, 3, 4, 5, 6);
        NativeBattleWaterExecutionFrame result = Run(frame, 0xA, Resources(), Services());
        for (int lane = 0; lane < 6; lane++)
        {
            Assert.Equal((uint)(lane + 1) << 16, Dword(result, SlotBase + 0x50 + 4 * lane));
        }
        Assert.Equal(new ushort[] { 1, 2, 3 }, Enumerable.Range(0, 3).Select(lane => Word(result, SlotBase + 0x8C + lane * 2)));
        Assert.Equal(new ushort[] { 4, 5, 6 }, Enumerable.Range(0, 3).Select(lane => Word(result, SlotBase + 0x94 + lane * 2)));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(10u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Busy_texture_pair_yields_without_consuming_operand_budget_resource_pointers_or_texture_service()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x27);
        frame = frame with { Core = frame.Core.Write(0x2798219, [0xFF]).MarkUnknown(Manager + 0xD2, 1).MarkUnknown(0x2798A68, 8), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        NativeBattleWaterExecutionFrame result = Run(frame, 0x27, null!, Services());
        Assert.Equal(Pc, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)3, result.ReadTemporaryWord(0x13E));
        Assert.Same(frame.Arena, result.Arena);
    }

    [Fact]
    public void Saturated_second_upload_preserves_first_queue_pointer_count_and_retry_PC_with_late_source_reads()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x27, 0);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD2, [11]) };
        frame = PutDword(frame, File0 + 0x14, 0x20);
        frame = PutDword(frame, File0 + 0x20, 0x100);
        frame = PutDword(frame, File0 + 8, 0x40);
        frame = PutDword(frame, File0 + 0x40, 0x200);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        int published = 0;
        NativeBattleWaterInstructionServices services = Services() with
        {
            ReadTextures = current => textures,
            PublishTextures = (current, next) =>
            {
                Assert.Equal((byte)12, current.Core.Read(Manager + 0xD2, 1)[0]);
                published++;
                textures = next;
                return current;
            }
        };
        NativeBattleWaterExecutionFrame result = Run(frame, 0x27, Resources(), services);
        Assert.Equal(1, published);
        Assert.Equal(1, textures.Count);
        Assert.Equal(File0 + 0x120, textures.Slots[0]!.ArenaOffset);
        Assert.Null(textures.Slots[0]!.Source);
        Assert.Equal(Pc, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)3, result.ReadTemporaryWord(0x13E));
        result = result with { Arena = result.Arena.Write(File0 + 0x120, [0x34, 0x12]) };
        NativeBattleEffectTextureDrain drained = textures.Drain505D20(result.Arena);
        Assert.Equal(new byte[] { 0x34, 0x12 }, Assert.Single(drained.Uploads).Bytes.ToArray());
    }

    [Fact]
    public void Async_file_service_observes_real_busy_destination_before_instruction_advance()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xC206);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xB8, ArenaBase + 0x90000);
        NativeBattleWaterFileReadRequest? requested = null;
        NativeBattleWaterInstructionServices services = Services() with
        {
            SubmitFileRead = (current, request) =>
            {
                requested = request;
                Assert.Equal(Pc, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
                Assert.Equal((byte)0xFF, current.Core.Read(0x2798219, 1)[0]);
                Assert.Equal(request.DestinationAddress, WaterSchedulerFixture.ReadCoreDword(current, Manager + 0xB4));
                return current;
            }
        };
        NativeBattleWaterExecutionFrame result = Run(frame, 6, Resources(), services);
        Assert.Equal(new NativeBattleWaterFileReadRequest(364, ArenaBase + 0x90000, 0, 0xAA0ED0), requested);
        Assert.Equal(0xFFFFFFFEu, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Unknown_or_mismatched_opcode_fails_without_calling_services_and_independent_JSON_next_is_exact()
    {
        NativeBattleWaterExecutionFrame unknown = Fixture(0x7F);
        Assert.Throws<NotSupportedException>(() => Run(unknown, 0x7F, null!, Services()));
        Assert.Throws<InvalidDataException>(() => Run(unknown, 0x52, null!, Services()));
        NativeBattleWaterExecutionFrame frame = Fixture(0x80D4, -2);
        frame = PutDword(frame, SlotBase + 0xCC, 0x12123456);
        NativeBattleWaterExecutionFrame clone = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(Run(frame, 0xD4, Resources(), Services())), JsonSerializer.Serialize(Run(clone, 0xD4, Resources(), Services())));
    }

    private static NativeBattleWaterInstructionServices Services() => new(ResolveProgram, new(UnexpectedActorFlags, UnexpectedWriteFlags, UnexpectedSound), new(UnexpectedPointer, UnexpectedPointer, UnexpectedBytes, UnexpectedContact, UnexpectedFrame, UnexpectedStage), UnexpectedByte, UnexpectedActor, UnexpectedAnchor, UnexpectedHeaderBase, UnexpectedCapture, UnexpectedTextures, UnexpectedPublishTextures, UnexpectedFile, UnexpectedEcx);

    private static NativeBattleWaterScriptProgram ResolveProgram(NativeBattleWaterExecutionFrame frame, uint pc) => new(0x1000, 16, Pc);
    private static NativeBattleWaterScriptProgram UnexpectedProgram(NativeBattleWaterExecutionFrame frame, uint pc) => throw new InvalidOperationException("Terminal header transition consumed an old program.");
    private static ushort UnexpectedActorFlags(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException();
    private static NativeBattleWaterExecutionFrame UnexpectedWriteFlags(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => throw new InvalidOperationException();
    private static NativeBattleWaterExecutionFrame UnexpectedSound(NativeBattleWaterExecutionFrame frame, uint address, short argument, byte flags) => throw new InvalidOperationException();
    private static uint UnexpectedPointer(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException();
    private static byte[] UnexpectedBytes(NativeBattleWaterExecutionFrame frame, uint address, int count) => throw new InvalidOperationException();
    private static NativeBattleWaterExecutionFrame UnexpectedContact(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException();
    private static NativeBattleWaterExecutionFrame UnexpectedFrame(NativeBattleWaterExecutionFrame frame) => throw new InvalidOperationException();
    private static NativeBattleWaterExecutionFrame UnexpectedStage(NativeBattleWaterExecutionFrame frame, int index, short depth, uint color) => throw new InvalidOperationException();
    private static byte UnexpectedByte(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException();
    private static NativeBattleGraphicsModel UnexpectedActor(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException();
    private static NativeBattleWaterActorAnchorSource UnexpectedAnchor(NativeBattleWaterExecutionFrame frame, uint address) => throw new InvalidOperationException();
    private static uint UnexpectedHeaderBase(NativeBattleScriptedEffectMemory core, NativeBattleEffectArena arena, uint address) => throw new InvalidOperationException();
    private static NativeBattleWaterResetCaptureInputs UnexpectedCapture(NativeBattleScriptedEffectMemory core, NativeBattleEffectArena arena, uint parameters, byte index) => throw new InvalidOperationException();
    private static NativeBattleEffectTextureState UnexpectedTextures(NativeBattleWaterExecutionFrame frame) => throw new InvalidOperationException();
    private static NativeBattleWaterExecutionFrame UnexpectedPublishTextures(NativeBattleWaterExecutionFrame frame, NativeBattleEffectTextureState textures) => throw new InvalidOperationException();
    private static NativeBattleWaterExecutionFrame UnexpectedFile(NativeBattleWaterExecutionFrame frame, NativeBattleWaterFileReadRequest request) => throw new InvalidOperationException();
    private static uint UnexpectedEcx(NativeBattleWaterExecutionFrame frame) => throw new InvalidOperationException();

    private static NativeBattleWater222Resources Resources()
    {
        byte[] tables = new byte[0x70];
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(6), 1);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(44), 1);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(46), 1);
        return new([new("file0", new byte[0x1799C].ToImmutableArray()), new("file1", new byte[0x26258].ToImmutableArray())], new(new("tables", tables.ToImmutableArray()), new("init", new byte[0x1C].ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray())));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, params short[] operands)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A68, ArenaBase + File0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, ArenaBase + File1);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x90, ArenaBase + SlotBase);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973E8, ArenaBase + SlotBase);
        frame = WaterSchedulerFixture.CoreWord(frame, Header + 0x38, 8);
        frame = PutWord(frame, SlotBase + 0xC8, 3);
        return Instruction(frame, instruction, operands);
    }

    private static NativeBattleWaterExecutionFrame Instruction(NativeBattleWaterExecutionFrame frame, ushort instruction, params short[] operands)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, instruction);
        for (int index = 0; index < operands.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 + index * 2), operands[index]);
        }
        frame = frame with { Arena = frame.Arena.Write(0x1000, bytes) };
        return WaterSchedulerFixture.CoreDword(frame.WriteTemporaryWord(0x14A, instruction), 0x2797450, Pc);
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, ushort opcode, NativeBattleWater222Resources resources, NativeBattleWaterInstructionServices services) => NativeBattleWaterInstructionDispatcher.Execute(frame, opcode, resources, services);
    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));

    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }

    private static NativeBattleWaterExecutionFrame PutDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }
}
