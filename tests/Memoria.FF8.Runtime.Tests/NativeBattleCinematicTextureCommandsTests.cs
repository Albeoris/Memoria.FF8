using System.Buffers.Binary;
using static Memoria.FF8.Runtime.Tests.NativeBattleCinematicTextureMetadataTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicTextureCommandsTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Manager = 0x2797690;
    private const int PcOffset = 0x3000;

    [Theory]
    [InlineData(0x27)]
    [InlineData(0x28)]
    [InlineData(0xE029)]
    public void Busy_guard_reads_no_instruction_pointer_operand_profile_or_source(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, []);
        frame = frame with { Core = frame.Core.Write(0x2798219, [1]).MarkUnknown(0x2797450, 4).MarkUnknown(0x27973EC, 4) };
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleCinematicTextureCommandStep result = NativeBattleCinematicTextureCommands.Execute(frame, new(0, 0, 0), null!, textures);
        Assert.Equal(frame.Core, result.Frame.Core);
        Assert.Equal(frame.Arena, result.Frame.Arena);
        Assert.Same(textures, result.Textures);
        Assert.Equal((ushort)7, result.Frame.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Pair_budget_eleven_rewinds_PC_retains_pixel_and_retries_pixel_before_CLUT()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x27, [0x27, 0, 22, 0]);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD2, [11]) };
        NativeBattleCinematicTextureCommandStep partial = NativeBattleCinematicTextureCommands.Execute(frame, Program(4), Resources(), NativeBattleEffectTextureState.FromColdProcess());
        Assert.Equal(Arena + PcOffset, Dword(partial.Frame, 0x2797450));
        Assert.Equal((byte)12, partial.Frame.Core.Read(Manager + 0xD2, 1)[0]);
        Assert.Equal((ushort)7, partial.Frame.ReadTemporaryWord(0x13E));
        Assert.Equal(Arena + 0x5180, partial.Frame.ReadTemporaryDword(0xFC));
        NativeBattleWaterExecutionFrame retry = partial.Frame with { Core = partial.Frame.Core.Write(Manager + 0xD2, [0]) };
        NativeBattleCinematicTextureCommandStep completed = NativeBattleCinematicTextureCommands.Execute(retry, Program(4), Resources(), partial.Textures);
        Assert.Equal(Arena + PcOffset + 4, Dword(completed.Frame, 0x2797450));
        Assert.Equal((byte)2, completed.Frame.Core.Read(Manager + 0xD2, 1)[0]);
        NativeBattleEffectTextureDrain drain = completed.Textures.Drain505D20(completed.Frame.Arena);
        Assert.Equal(3, drain.Uploads.Length);
        Assert.Equal(drain.Uploads[0].Bytes, drain.Uploads[1].Bytes);
        Assert.Equal(new byte[] { 0x56, 0x78 }, drain.Uploads[2].Bytes);
    }

    [Theory]
    [InlineData(0xE029, 4, 0x7000)]
    [InlineData(0xC029, 4, 0x7000)]
    [InlineData(0x6029, 8, 0x6000)]
    public void Direct_pixel_flags_follow_native_priority_without_charging_the_script_budget(ushort instruction, uint advance, int expectedSource)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, [(byte)instruction, (byte)(instruction >> 8), 22, 0, 0x89, 0xCC, 1, 0]);
        frame = WriteCore(frame, Manager + 0xB4, Arena + 0x7000);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD2, [255]), Arena = frame.Arena.Write(0x6000, [0xAB, 0xCD]).Write(0x7000, [0x12, 0x34]) };
        NativeBattleCinematicTextureCommandStep result = NativeBattleCinematicTextureCommands.Execute(frame, Program(8), Resources(), NativeBattleEffectTextureState.FromColdProcess());
        Assert.Equal(Arena + PcOffset + advance, Dword(result.Frame, 0x2797450));
        Assert.Equal((byte)255, result.Frame.Core.Read(Manager + 0xD2, 1)[0]);
        NativeBattleEffectTextureDrain drain = result.Textures.Drain505D20(result.Frame.Arena);
        Assert.Equal(frame.Arena.Read(expectedSource, 2), Assert.Single(drain.Uploads).Bytes);
    }

    [Fact]
    public void Literal_direct_pixel_branch_needs_no_profile_or_texture_index()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x2029, [0x29, 0x20, 1, 0, 2, 0, 1, 0, 1, 0]);
        frame = WriteCore(frame, Manager + 0xB4, Arena + 0x7000);
        frame = frame with { Arena = frame.Arena.Write(0x7000, [0xAB, 0xCD]) };
        NativeBattleCinematicTextureCommandStep result = NativeBattleCinematicTextureCommands.Execute(frame, Program(10), null!, NativeBattleEffectTextureState.FromColdProcess());
        Assert.Equal(Arena + PcOffset + 10, Dword(result.Frame, 0x2797450));
        Assert.Equal((byte)0, result.Frame.Core.Read(Manager + 0xD2, 1)[0]);
        NativeBattleEffectTextureDrain drain = result.Textures.Drain505D20(result.Frame.Arena);
        Assert.Equal((short)1, Assert.Single(drain.Uploads).X);
        Assert.Equal(new byte[] { 0xAB, 0xCD }, drain.Uploads[0].Bytes);
    }

    [Fact]
    public void CLUT_only_resolves_the_latest_streamed_alias_without_reading_pixel_metadata()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x28, [0x28, 0, 22, 0]);
        frame = frame with { Core = frame.Core.MarkUnknown(Manager + 0x98, 4).MarkUnknown(Manager + 0xA4, 4), Arena = frame.Arena.Write(0x6000, frame.Arena.Read(0x5000, 0x200)).Write(0x6180, [0xAB, 0xCD]) };
        frame = WriteCore(frame, 0x2798A68 + 9 * 4, Arena + 0x6000);
        NativeBattleCinematicTextureCommandStep result = NativeBattleCinematicTextureCommands.Execute(frame, Program(4), Resources(), NativeBattleEffectTextureState.FromColdProcess());
        Assert.Equal(Arena + 0x6180, result.Frame.ReadTemporaryDword(0xFC));
        NativeBattleEffectTextureDrain drain = result.Textures.Drain505D20(result.Frame.Arena);
        Assert.Equal(new byte[] { 0xAB, 0xCD }, Assert.Single(drain.Uploads).Bytes);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, byte[] program)
    {
        NativeBattleWaterExecutionFrame frame = WriteCore(Frame(), 0x2797450, Arena + PcOffset);
        return (frame with { Arena = frame.Arena.Write(PcOffset, program).Write(0x20C8, [7, 0]) }).WriteTemporaryWord(0x14A, instruction);
    }

    private static NativeBattleWaterScriptProgram Program(int length) => new(PcOffset, length, Arena + PcOffset);
    private static uint Dword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(address, 4));
}
