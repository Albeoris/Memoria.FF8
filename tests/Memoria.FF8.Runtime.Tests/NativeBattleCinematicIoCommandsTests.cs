using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.Tests.NativeBattleCinematicTextureMetadataTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicIoCommandsTests
{
    private const uint Arena = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Manager = 0x2797690;
    private const uint Pc = Arena + 0x3000;

    [Theory]
    [InlineData(0x8406)]
    [InlineData(0x0006)]
    [InlineData(0x0008)]
    public void Busy_wait_does_not_consume_PC_descriptor_or_address_operands(ushort raw)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(raw, 0);
        frame = frame with { Core = frame.Core.Write(0x2798219, [1]).MarkUnknown(0x2797450, 4).MarkUnknown(0x27973EC, 4) };
        NativeBattleCinematicIoStep result = NativeBattleCinematicIoCommands.Execute(frame, new(0, 0, 0), null!);
        Assert.Null(result.Request);
        Assert.Equal((ushort)7, result.Frame.ReadTemporaryWord(0x13E));
        Assert.Equal(frame.Core, result.Frame.Core);
    }

    [Fact]
    public void Actual_Ifrit_8406_publishes_pointer_two_before_scheduling_its_own_completion()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8406, 0);
        NativeBattleCinematicIoStep result = NativeBattleCinematicIoCommands.Execute(frame, Program(), null!);
        Assert.Equal(Arena + 0x50000, Dword(result.Frame, 0x2798A68 + 2 * 4));
        Assert.Equal((short)0x235, result.Request!.FileId);
        Assert.Equal(0xB2BB40u, result.Request.CompletionCallback);
        Assert.Equal(Pc + 2, Dword(result.Frame, 0x2797450));
        Assert.Equal((byte)0xFF, result.Frame.Core.Read(0x2798219, 1)[0]);
        Assert.Equal(Arena + 0x50000, Dword(result.Frame, Manager + 0xB4));
        NativeBattleCinematicResources resources = Resources() with { Files = [new(0x235, "part", [4, 5, 6, 7])] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = result.Frame.Arena, ScriptedEffects = result.Frame.Core, Cursor = 0 };
        NativeBattleWaterIoQueued queued = NativeBattleCinematicIoCommands.CopyAndQueue(memory, NativeBattleFileIoState.FromColdProcess(), result.Request, resources);
        Assert.Equal(new byte[] { 4, 5, 6, 7 }, queued.Memory.Arena!.Read(0x50000, 4));
        Assert.Equal(0, queued.Memory.Cursor);
        Assert.Equal((byte)0xFF, queued.Memory.ScriptedEffects!.Read(0x2798219, 1)[0]);
        Assert.Equal(0xB2BB40u, queued.FileIo.Rows[0].CompletionCallback);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCinematicIoCommands.Execute(restored, Program(), null!)));
    }

    [Fact]
    public void Special_file_branch_skips_resource_publication_and_unused_context_fields()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xC206, 0);
        frame = frame with { Core = frame.Core.MarkUnknown(Manager + 0xA0, 3).MarkUnknown(0x2798A68, 0x100) };
        NativeBattleCinematicIoStep result = NativeBattleCinematicIoCommands.Execute(frame, new(0, 0, 0), null!);
        Assert.Equal((short)0x16C, result.Request!.FileId);
        Assert.Equal(Arena + 0x50000, result.Request.DestinationAddress);
        Assert.Equal(Pc + 2, Dword(result.Frame, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Frame.Core.Read(Manager + 0xA0, 1));
    }

    [Theory]
    [InlineData(0x00FF, 0x50000u)]
    [InlineData(0x0280, 0x6000u)]
    [InlineData(0xFF00, 0x4000u)]
    public void Positive_read_modes_select_only_their_consumed_address_owners_and_advance_four(ushort operand, uint destination)
    {
        NativeBattleWaterExecutionFrame frame = WriteCore(Fixture(6, operand), 0x2798A68, Arena + 0x4000);
        NativeBattleCinematicIoStep result = NativeBattleCinematicIoCommands.Execute(frame, Program(), null!);
        Assert.Equal(Arena + destination, result.Request!.DestinationAddress);
        Assert.Equal((short)0x233, result.Request.FileId);
        Assert.Equal(Pc + 4, Dword(result.Frame, 0x2797450));
        Assert.Equal(Arena + destination, Dword(result.Frame, 0x2798A68));
    }

    [Fact]
    public void Descriptor_destination_uses_the_selected_relative_tables_and_unsigned_A2_offset()
    {
        NativeBattleCinematicResources resources = Resources();
        byte[] data = resources.StaticData.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x1E4), 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x1F8), 0x38);
        resources = resources with { StaticData = new("static", data.ToImmutableArray()) };
        NativeBattleWaterExecutionFrame frame = WriteCore(Fixture(6, 0x0100), 0x2798A68, Arena + 0x4000);
        frame = frame with { Core = frame.Core.Write(Manager + 0xA2, [3]) };
        NativeBattleCinematicIoStep result = NativeBattleCinematicIoCommands.Execute(frame, Program(), resources);
        Assert.Equal(Arena + 0x4038, result.Request!.DestinationAddress);
        Assert.Equal((short)0x236, result.Request.FileId);
        Assert.Equal(Arena + 0x4038, Dword(result.Frame, 0x2798A68 + 3 * 4));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort raw, ushort operand)
    {
        NativeBattleWaterExecutionFrame frame = WriteCore(Frame(), 0x2797450, Pc).WriteTemporaryWord(0x14A, raw);
        frame = WriteCore(frame, Manager + 0xB8, Arena + 0x50000);
        byte[] instruction = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(instruction, raw);
        BinaryPrimitives.WriteUInt16LittleEndian(instruction.AsSpan(2), operand);
        frame = frame with { Core = frame.Core.Write(Manager + 0xA0, [0x33, 2, 0]), Arena = frame.Arena.Write(0x3000, instruction).Write(0x20C8, [7, 0]) };
        return frame;
    }

    private static NativeBattleWaterScriptProgram Program() => new(0x3000, 4, Pc);
    private static uint Dword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(address, 4));
}
