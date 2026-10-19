using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorStoreTests
{
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 6, 0x1000);
    private const uint ActorAddress = 0x1D972C0;

    [Theory]
    [InlineData((ushort)0x0048, false)]
    [InlineData((ushort)0x8048, true)]
    public void Position_store_preserves_independent_vertical_owner_and_sets_angle_only_for_high_bit(ushort instruction, bool copyAngle)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = Position(frame, 0x94, -32768, 32767, -1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x208C, 6).MarkUnknown(0x1000, 6) };
        NativeBattleGraphicsModel actor = Actor();
        NativeBattleCinematicActorStore48.Execute(frame, Read, Write);
        Assert.Equal(new NativeBattleVector(-32768, 32767, -1), actor.Transform!.Position28);
        Assert.Equal((short)4, actor.Transform.Vertical34);
        Assert.Equal(copyAngle ? (short)32767 : (short)5, actor.Transform.Angle36);
        Assert.Equal((ushort)0x1002, actor.Flags0);
        Assert.Equal((short)8, actor.Rotation12);

        NativeBattleGraphicsModel Read(uint address)
        {
            Assert.Equal(ActorAddress, address);
            return actor;
        }
        void Write(uint address, NativeBattleGraphicsModel value)
        {
            Assert.Equal(ActorAddress, address);
            actor = value;
        }
    }

    [Fact]
    public void Rotation_mode_reads_only_cached_angles_and_other_modes_need_no_transform()
    {
        NativeBattleWaterExecutionFrame frame = Position(Fixture(0x8248), 0x8C, -1, -32768, 32767);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2094, 6) };
        NativeBattleGraphicsModel actor = Actor();
        NativeBattleModelTransform original = actor.Transform!;
        NativeBattleCinematicActorStore48.Execute(frame, _ => actor, (_, value) => actor = value);
        Assert.Equal(((short)-1, (short)-32768, (short)32767), (actor.Rotation12, actor.Rotation14, actor.Rotation16));
        Assert.Same(original, actor.Transform);
        frame = Fixture(0x0448) with { Arena = frame.Arena.MarkUnknown(0x208C, 14) };
        actor = actor with { Flags0 = 2, Transform = null };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorStore48.Execute(frame, _ => actor, (_, value) => actor = value);
        Assert.Equal((ushort)0x1002, actor.Flags0);
        Assert.Null(actor.Transform);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Color_offsets_use_signed_WORDs_clamp_each_channel_and_replace_the_whole_color_DWORD()
    {
        NativeBattleWaterExecutionFrame frame = Position(Fixture(0x8038), 0x8C, -32768, 128, 32767);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2094, 6) };
        NativeBattleGraphicsModel actor = Actor() with { Color40 = 0xFF010203 };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorStore48.StoreColor38(frame, 0x00123456, _ => actor, (_, value) => actor = value);
        Assert.Equal(0x00FF3400u, actor.Color40);
        Assert.Equal((ushort)2, actor.Flags0);
        Assert.Equal(0u, result.ReadTemporaryDword(0xF0));
        Assert.Equal(52u, result.ReadTemporaryDword(0xF4));
        Assert.Equal(255u, result.ReadTemporaryDword(0xF8));
        Assert.Equal(0x00FF3400u, result.ReadTemporaryDword(0xFC));
    }

    [Theory]
    [InlineData((ushort)0x0049, (short)-10, (short)-10, true)]
    [InlineData((ushort)0x0249, (short)-10, (short)-10, true)]
    [InlineData((ushort)0xFE49, (short)-11, (short)-10, false)]
    [InlineData((ushort)0x0049, (short)32767, (short)-32768, false)]
    public void Scale_branch_uses_signed_inclusive_comparison_and_reads_displacement_only_when_required(ushort instruction, short scale, short boundary, bool reached)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(boundary)).Write(0x1004, BitConverter.GetBytes((short)-6)) };
        if (reached)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        }
        NativeBattleGraphicsModel actor = Actor();
        actor = actor with { Transform = actor.Transform! with { Scale38 = scale } };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorStore48.BranchScale49(frame, Program, _ => actor);
        Assert.Equal(reached ? 0x1006u : 0xFFAu, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
    }

    [Fact]
    public void Camera_matrix_ignores_unused_operand_and_overwrites_unknown_padding_with_manager_WORD()
    {
        NativeBattleWaterExecutionFrame frame = Position(Fixture(0x66), 0x94, -2, 3, -4);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797692, 0xABCD);
        byte[] matrix = new byte[32];
        for (int lane = 0; lane < 3; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(lane * 8), 4096);
            BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(20 + lane * 4), 10 + lane * 10);
        }
        frame = frame with { Core = frame.Core.Write(0x27979E8, matrix).MarkUnknown(0x27979FA, 2), Arena = frame.Arena.MarkUnknown(0x1000, 6) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterMatrixCommands.Execute(frame, Program, new(0, 0x3000, 2, 0x2000));
        Assert.Equal(8u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797A1C));
        Assert.Equal(23u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797A20));
        Assert.Equal(26u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797A24));
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797A1A, 2)));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x27979FA, 2));
    }

    private static NativeBattleGraphicsModel Actor() => new(0, 2) { Rotation12 = 8, Rotation14 = 9, Rotation16 = 10, Transform = new(new(7, 8, 9), new(1, 2, 3), 4, 5, -10) };

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, NativeBattleScriptedEffectMemory.Address + 0x60, ActorAddress);
        return WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
    }

    private static NativeBattleWaterExecutionFrame Position(NativeBattleWaterExecutionFrame frame, int offset, short x, short y, short z)
    {
        frame = WaterSchedulerFixture.SlotWord(frame, 0, offset, unchecked((ushort)x));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, offset + 2, unchecked((ushort)y));
        return WaterSchedulerFixture.SlotWord(frame, 0, offset + 4, unchecked((ushort)z));
    }
}
