using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect256ParticleStateTests
{
    private const uint ArenaAddress = 0x20DFAB8;
    private const uint MatrixAddress = 0x27979E8;

    [Theory]
    [InlineData(7, true)]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(9, false)]
    public void Animated_rectangle_modes_publish_actual_translation_and_preserve_their_padding_owner(int mode, bool knownPad)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x145, 1).WriteTemporaryWord(0xF2, 0xCAFE);
        frame = frame with { Arena = frame.Arena.Write(0x201E, [unchecked((byte)mode)]) };
        frame = SlotWord(frame, 0x8C, 256);
        frame = SlotWord(frame, 0x8E, 256);
        frame = SlotWord(frame, 0x94, 10);
        frame = SlotWord(frame, 0x96, 20);
        frame = SlotWord(frame, 0x98, 30);
        frame = SlotWord(frame, 0x9A, 0xBEEF);
        frame = Matrix(frame, MatrixAddress, 100, -200, 300, null);
        frame = Matrix(frame, 0x2797968, 0, 0, 0, knownPad ? (ushort)0xBADA : null);
        if (!knownPad && mode != 8)
        {
            frame = UnknownTemporary(frame, 0xF2, 2);
        }
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        int draws = 0;
        frame = NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, (_, _) => MatrixAddress, (current, matrix) =>
        {
            draws++;
            Assert.Equal((110, -180, 330), (matrix.X, matrix.Y, matrix.Z));
            return current;
        }, _ => registers, (current, updated) =>
        {
            registers = updated;
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Equal(new int?[] { 110, -180, 330 }, registers.Geometry.Translation);
        Assert.Equal(new int?[] { 110, -180, 330 }, registers.Colors.Mac);
        Assert.Equal(new byte[] { 0xEF, 0xBE }, registers.Colors.NormalInput!.Value.AsSpan().Slice(6, 2).ToArray());
        Assert.Equal(knownPad, frame.TemporaryFrame.Known[0xF2]);
        Assert.Equal(knownPad, registers.Geometry.Rotation20[18].HasValue);
        if (knownPad)
        {
            ushort expected = mode == 8 ? (ushort)0xBADA : (ushort)0xCAFE;
            Assert.Equal(expected, frame.ReadTemporaryWord(0xF2));
            Assert.Equal(unchecked((byte)expected), registers.Geometry.Rotation20[18]);
            Assert.Equal(unchecked((byte)(expected >> 8)), registers.Geometry.Rotation20[19]);
        }
        NativeBattleGeometryRegisterStep restored = JsonSerializer.Deserialize<NativeBattleGeometryRegisterStep>(JsonSerializer.Serialize(registers))!;
        Assert.Equal(JsonSerializer.Serialize(registers), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Rectangle_leaf_publishes_RTPT_AVSZ_and_fourth_RTPS_with_unknown_vector_tails()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973EC, 0x2797690);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x300000);
        frame = frame.WriteTemporaryDword(0x90, ArenaAddress + 0x5000).WriteTemporaryDword(0x98, 0x123456).WriteTemporaryDword(0x14C, 0x1D8E058);
        frame = frame with { Arena = frame.Arena.Write(0x5000, BitConverter.GetBytes(1)).Write(0x5014, [0x80]) };
        foreach ((int offset, ushort value) in new (int, ushort)[] { (4, 4096), (6, 4096), (12, 0x0101), (18, 0x20) })
        {
            frame = frame with { Arena = frame.Arena.Write(0x5004 + offset, BitConverter.GetBytes(value)) };
        }
        foreach (int offset in new[] { 0xE6, 0xEE, 0xF6, 0xFE })
        {
            frame = UnknownTemporary(frame, offset, 2);
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 3000 };
        NativeBattleGeometryRegisterStep initial = new(NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleWaterParticleRectangleResult result = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(frame, initial, new(null, 100, 160, 108), 341, 0, 4096, _ => 17);
        Assert.Equal(40, result.Packets.AllocationBytes);
        Assert.Equal(new uint?[] { 3000, 3000, 3000, 3000 }, result.Registers.Geometry.Sz);
        Assert.Equal((uint)749, result.Registers.Geometry.Otz);
        Assert.Equal((uint)4096, result.Registers.Geometry.Mac0);
        Assert.Equal((uint)1, result.Registers.Colors.Ir0);
        Assert.Equal(new int?[] { 16, 16, 3000 }, result.Registers.Colors.Mac);
        Assert.False(result.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(result.Registers.Colors.NormalInputKnown.Value[7]);
        Assert.Null(result.Registers.Geometry.V1[6]);
        Assert.Null(result.Registers.Geometry.V2[6]);
        Assert.Null(result.Registers.Geometry.Rotation20[18]);
        Assert.Equal((uint)749, result.Frame.ReadTemporaryDword(0xE0));
        Assert.Equal((uint)750, result.Frame.ReadTemporaryDword(0xE4));
        Assert.Null(initial.Geometry.Otz);
    }

    [Fact]
    public void Paused_particle_with_empty_frame_still_publishes_anchor_MAC_translation_and_planar_R20()
    {
        const int particle = 0x4010;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x145, 1);
        frame = SlotDword(frame, 0xB8, ArenaAddress + 0x4000);
        frame = frame with { Arena = frame.Arena.Write(0x4000, BitConverter.GetBytes((ushort)1)).Write(particle + 4, BitConverter.GetBytes(ArenaAddress + 0x1000)).Write(particle + 0x10, BitConverter.GetBytes(4096)).Write(particle + 0x14, BitConverter.GetBytes(10 << 8)).Write(particle + 0x18, BitConverter.GetBytes(-20 << 8)).Write(particle + 0x1C, BitConverter.GetBytes(30 << 8)).Write(particle + 0x30, BitConverter.GetBytes(ArenaAddress + 0x5000)).Write(particle + 0x37, [1]) };
        frame = Matrix(frame, MatrixAddress, 100, -200, 300, null);
        frame = UnknownTemporary(frame, 0xA6, 2);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        int draws = 0;
        NativeBattleWaterParticleServices services = new((_, _, _) => throw new InvalidOperationException(), (_, _) => MatrixAddress, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, _) =>
        {
            draws++;
            NativeBattleWaterParticleRectangleResult drawn = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(current, registers, new(null, 100, 160, 108), 341, 0, 0, _ => 17);
            registers = drawn.Registers;
            return drawn.Frame;
        })
        {
            ReadRegisters = _ => registers,
            PublishRegisters = (current, updated) =>
            {
                registers = updated;
                return current;
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterParticles.AdvanceType6(frame, new(0x1000, 2, ArenaAddress + 0x1000), new(0x2000, 0x400, 4, 0x2000), services);
        Assert.Equal(1, draws);
        Assert.Equal(new int?[] { 110, -220, 330 }, registers.Geometry.Translation);
        Assert.Equal(new int?[] { 110, -220, 330 }, registers.Colors.Mac);
        Assert.Equal(new short[] { 256, 0, 0, 0, 256, 0, 0, 0, 0 }, NativeBattleGeometry.CaptureMatrix(registers.Geometry).Rotation);
        Assert.Equal((byte)0, registers.Geometry.Rotation20[18]);
        Assert.Equal((byte)0, registers.Geometry.Rotation20[19]);
        Assert.False(registers.Colors.NormalInputKnown!.Value[6]);
        Assert.Equal(frame.Random, result.Random);
    }

    private static NativeBattleWaterExecutionFrame Matrix(NativeBattleWaterExecutionFrame frame, uint address, int x, int y, int z, ushort? padding)
    {
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Y = y, Z = z }, padding);
        NativeBattleScriptedEffectMemory core = frame.Core.Write(address, matrix.Bytes.AsSpan());
        if (padding is null)
        {
            core = core.MarkUnknown(address + 18, 2);
        }
        return frame with { Core = core };
    }
    private static NativeBattleWaterExecutionFrame UnknownTemporary(NativeBattleWaterExecutionFrame frame, int offset, int length)
    {
        bool[] known = frame.TemporaryFrame.Known.ToArray();
        Array.Fill(known, false, offset, length);
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Known = known.ToImmutableArray() } };
    }
    private static NativeBattleWaterExecutionFrame SlotWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value) => WaterSchedulerFixture.SlotWord(frame, 0, offset, value);
    private static NativeBattleWaterExecutionFrame SlotDword(NativeBattleWaterExecutionFrame frame, int offset, uint value) => WaterSchedulerFixture.SlotDword(frame, 0, offset, value);
}
