using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    private const int Particle = 0x4010;
    private const uint Matrix = 0x27979E8;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Theory]
    [InlineData(0, 0x10, 4, 8)]
    [InlineData(0x100, 0x20, 2, 0)]
    [InlineData(0x200, 0x28, 2, 0)]
    public void AssignmentUsesFourSelectedLanesAndSentinelDoesNotConsumeOldField(int group, int offset, int stride, int shift)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)(0xF004 | group), 0x8000, 0x7654, 0x7FFF, 0xFFFF);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle, 0x50) };
        NativeBattleWaterExecutionFrame result = One(frame);
        int[] values = [-32768, 0, 32767, -1];
        for (int lane = 0; lane < 4; lane++)
        {
            if (lane == 1)
            {
                Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + offset + lane * stride, stride));
            }
            else if (stride == 4)
            {
                Assert.Equal(unchecked((uint)(values[lane] << shift)), Dword(result, offset + lane * stride));
            }
            else
            {
                Assert.Equal(unchecked((ushort)values[lane]), Word(result, offset + lane * stride));
            }
        }
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void AdditiveRandomSkipsZeroOperandAndConsumesOneDrawForNonzeroLane()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xC005, 0, 5);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle + 0x10, 4) };
        frame = PutDword(frame, 0x14, 100);
        NativeBattleWaterVectorRandom expected = NativeBattleWaterVectorCommands.RandomB65740(5, frame.Random);
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(expected.Random, result.Random);
        Assert.Equal(unchecked(100u + (uint)(expected.Value << 8)), Dword(result, 0x14));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x10, 4));
    }

    [Fact]
    public void EmptyArithmeticMaskDoesNotReadOperandsOrParticleFields()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(4);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle, 0x50).MarkUnknown(0x1002, 0xFE) };
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Random, result.Random);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle, 1));
    }

    [Theory]
    [InlineData(0x7F0B, 0x38)]
    [InlineData(0x7F0C, 0x3C)]
    public void ColorInstructionsWriteThreeBytesAndKeepFourthControlByte(ushort instruction, int offset)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0x80FE);
        frame = frame with { Arena = frame.Arena.Write(Particle + offset + 3, new byte[] { 0xA5 }) };
        Assert.Equal(new byte[] { 0x7F, 0xFE, 0x80, 0xA5 }, One(frame).Arena.Read(Particle + offset, 4));
        Assert.Equal((ushort)0x7F80, Word(One(Fixture(0x11)), 0x34));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 99)]
    [InlineData(7, 100)]
    [InlineData(255, 107)]
    public void ResourceSelectionReadsCurrentMapDirectorySequenceAndSlotTick(int index, byte resourceIndex)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)((index << 8) | 7));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279773C, 0x30000000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A68 + 4u * resourceIndex, Address(0x6000));
        frame = ArenaDword(frame, 0x600C, 0x20);
        frame = ArenaDword(frame, 0x6024 + 4 * index, 0x800);
        frame = ArenaDword(frame, 0x6820, 0x80);
        int reads = 0;
        NativeBattleWaterParticleServices services = Services() with { ReadMetadata = (_, address, length) =>
        {
            reads++;
            Assert.Equal(0x30000000u + (uint)index, address);
            Assert.Equal(1, length);
            return new byte[] { resourceIndex };
        } };
        NativeBattleWaterExecutionFrame result = One(frame, services);
        Assert.Equal(1, reads);
        Assert.Equal(Address(0x6820), Dword(result, 8));
        Assert.Equal(Address(0x6820), Dword(result, 0xC));
        Assert.Equal(Address(0x6020), Dword(result, 0x4C));
        Assert.Equal(Address(0x60A0), Dword(result, 0x30));
        Assert.Equal((ushort)5, Word(result, 0x34));
        Assert.Equal(Address(0x6020), result.ReadTemporaryDword(0xFC));
        Assert.Equal((byte)0, result.Arena.Read(Particle + 0x4A, 1)[0]);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(One(restored, services)));
    }

    [Theory]
    [InlineData(0xFF02, -32768)]
    [InlineData(0x0002, -2)]
    [InlineData(0x7F02, 0)]
    [InlineData(0x8002, 32767)]
    public void JumpUsesSignedDisplacementFromOpcodeAddressWithoutYieldOrParticleReads(ushort instruction, short displacement)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, unchecked((ushort)displacement));
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle, 0x50) };
        frame = frame.WriteTemporaryWord(0x13E, 0x5A5A);
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(unchecked(0x1000u + (uint)displacement), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x5A5A, result.ReadTemporaryWord(0x13E));
        Assert.Equal(JsonSerializer.Serialize(frame.Arena), JsonSerializer.Serialize(result.Arena));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void JumpExecutesTargetUntilRealYieldAndBackwardJumpResumesAfterRestore()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2, 6, 0xFFFF, 0x0101, 2, unchecked((ushort)-8));
        NativeBattleWaterExecutionFrame yielded = Advance(frame);
        Assert.Equal(0x1008u, Dword(yielded, 4));
        Assert.Equal((ushort)125, Word(yielded, 2));
        yielded = PutWord(yielded, 2, 0);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(yielded))!;
        NativeBattleWaterExecutionFrame next = Advance(restored);
        Assert.Equal(0x1008u, Dword(next, 4));
        Assert.Equal((ushort)125, Word(next, 2));
        Assert.Equal(JsonSerializer.Serialize(Advance(yielded)), JsonSerializer.Serialize(next));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterParticles.AdvanceType6(Fixture(2, 0), new(0x1000, 4, 0x1000), Bank, Services(), 3));
    }

    [Fact]
    public void ResourceSelectionDefersToCurrentCorePhysicalOwnerBounds()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFF07);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279773C, 0x30000000);
        NativeBattleWaterParticleServices services = Services() with { ReadMetadata = (_, _, _) => new byte[] { 255 } };
        Assert.Throws<InvalidDataException>(() => One(frame, services));
    }

    [Fact]
    public void HighSequenceAndTextureIndicesYieldThenResumeFromRetainedState()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFF07, 0xFF13, 0x0101);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279773C, 0x30000000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A68, Address(0x6000));
        frame = ArenaDword(frame, 0x600C, 0x20);
        frame = ArenaDword(frame, 0x6420, 0x800);
        frame = ArenaDword(frame, 0x6820, 0x80);
        frame = frame with { Arena = frame.Arena.Write(0x6824, new byte[] { 0x80, 0, 2 }) };
        int draws = 0;
        NativeBattleWaterParticleServices services = Services() with
        {
            ReadMetadata = (_, address, length) =>
            {
                Assert.Equal(0x300000FFu, address);
                Assert.Equal(1, length);
                return new byte[] { 0 };
            },
            PrepareTexturePage = (current, index) =>
            {
                Assert.Equal(255, index);
                return current.WriteTemporaryDword(0xFC, 0x12345678);
            },
            ReadClut = (_, index) =>
            {
                Assert.Equal(255, index);
                return 0xABCD;
            },
            DrawRectangles = (current, _) =>
            {
                draws++;
                Assert.Equal(Address(0x60A0), current.ReadTemporaryDword(0x90));
                return current;
            }
        };
        NativeBattleWaterExecutionFrame yielded = Advance(frame, services);
        Assert.Equal(1, draws);
        Assert.Equal(0x1006u, Dword(yielded, 4));
        Assert.Equal((ushort)125, Word(yielded, 2));
        Assert.Equal((ushort)0x5678, Word(yielded, 0x40));
        Assert.Equal((ushort)0xABCD, Word(yielded, 0x48));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(yielded))!;
        Assert.Equal(JsonSerializer.Serialize(Advance(yielded, services)), JsonSerializer.Serialize(Advance(restored, services)));
        Assert.Equal(3, draws);
    }

    [Fact]
    public void SequenceSelectionReadsCurrentArenaAfterAliasingPointerStores()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(7);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279773C, 0x30000000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A68, Address(0x6000));
        frame = ArenaDword(frame, 0x600C, unchecked((uint)-0x2000));
        frame = ArenaDword(frame, 0x4004, 0x18);
        NativeBattleWaterParticleServices services = Services() with { ReadMetadata = (_, _, _) => new byte[] { 0 } };
        NativeBattleWaterExecutionFrame result = One(frame, services);
        Assert.Equal(Address(Particle + 8), Dword(result, 8));
        Assert.Equal(unchecked(Address(0x4000) + Address(Particle + 8)), Dword(result, 0x30));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyOrInactiveRecordsDoNotConsumeUnknownParticlePayload(bool inactive)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFFFF);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle, 0x50).MarkUnknown(0x1000, 2) };
        if (inactive)
        {
            frame = PutDword(frame, 4, 0);
        }
        else
        {
            frame = frame with { Arena = WriteWord(frame.Arena, 0x4000, 0) };
        }
        int matrices = 0;
        NativeBattleWaterExecutionFrame result = Advance(frame, Services() with { ResolveMatrixRow = (_, _) =>
        {
            matrices++;
            return Matrix;
        } });
        Assert.Equal(1, matrices);
        Assert.Equal(0u, result.ReadTemporaryDword(0x20));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x30, 4));
    }

    [Fact]
    public void ReferenceRandomUsesShiftedMaskActualSlotLookupAndSkipsZeroCoordinates()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x7012, 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 99);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 99);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 5);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, unchecked((ushort)-7));
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 1, 0xFF }), Arena = frame.Arena.MarkUnknown(Particle + 0x18, 4) };
        NativeBattleWaterVectorRandom x = NativeBattleWaterVectorCommands.RandomB65740(5, frame.Random);
        NativeBattleWaterVectorRandom z = NativeBattleWaterVectorCommands.RandomB65740(-7, x.Random);
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(z.Random, result.Random);
        Assert.Equal(unchecked((uint)(x.Value << 8)), Dword(result, 0x14));
        Assert.Equal(unchecked((uint)(z.Value << 8)), Dword(result, 0x1C));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x18, 4));
        Assert.Equal(WaterSchedulerFixture.ReadCoreDword(frame, 0x27973E8), WaterSchedulerFixture.ReadCoreDword(result, 0x27973E8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(255)]
    public void TextureInstructionPreservesGetterAndStoreOrder(int selectedIndex)
    {
        List<string> order = [];
        NativeBattleWaterParticleServices services = Services() with
        {
            PrepareTexturePage = (frame, index) =>
            {
                Assert.Equal(selectedIndex, index);
                order.Add("page");
                return frame.WriteTemporaryDword(0xFC, 0x5678).WriteTemporaryDword(0xF8, 0);
            },
            ReadClut = (frame, index) =>
            {
                Assert.Equal(selectedIndex, index);
                Assert.Equal((ushort)0x5678, Word(frame, 0x40));
                order.Add("clut");
                return 0xABCD;
            }
        };
        NativeBattleWaterExecutionFrame result = One(Fixture((ushort)((selectedIndex << 8) | 0x13)), services);
        Assert.Equal(new[] { "page", "clut" }, order);
        Assert.Equal((ushort)0xABCD, Word(result, 0x48));
    }

    [Fact]
    public void PositiveUnsignedWaitDefersInstructionsEvenAfterWordUnderflowAndJsonNextMatches()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFFFF);
        frame = PutWord(frame, 2, 1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 2) };
        frame = Advance(frame);
        Assert.Equal((ushort)0xFFFE, Word(frame, 2));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(Advance(frame)), JsonSerializer.Serialize(Advance(restored)));
        Assert.Equal((ushort)0xFFFB, Word(Advance(restored), 2));
    }

    [Fact]
    public void ZeroYieldContinuesAndTerminalStillIntegratesFullVelocityBeforeNarrowing()
    {
        NativeBattleWaterExecutionFrame zero = Advance(Fixture(1, 0x0101));
        Assert.Equal((ushort)125, Word(zero, 2));
        Assert.Equal(0x1004u, Dword(zero, 4));
        NativeBattleWaterExecutionFrame frame = Fixture(0);
        short[] velocity = [32767, -32768, 32767, -32768];
        short[] acceleration = [1, -1, 32767, -32768];
        int[] sums = [32768, -32769, 65534, -65536];
        for (int i = 0; i < 4; i++)
        {
            frame = PutWord(frame, 0x20 + 2 * i, unchecked((ushort)velocity[i]));
            frame = PutWord(frame, 0x28 + 2 * i, unchecked((ushort)acceleration[i]));
        }
        frame = frame with { Arena = frame.Arena.Write(Particle + 0x38, new byte[] { 250, 5, 100, 0xA5 }).Write(Particle + 0x3C, new byte[] { 10, 246, 128 }) };
        frame = Advance(frame);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(unchecked((ushort)sums[i]), Word(frame, 0x20 + 2 * i));
            Assert.Equal(unchecked((uint)(sums[i] << 4)), Dword(frame, 0x10 + 4 * i));
        }
        Assert.Equal(new byte[] { 255, 0, 0, 0xA5 }, frame.Arena.Read(Particle + 0x38, 4));
        Assert.Equal(0u, Dword(frame, 4));
        Assert.Equal(0u, Dword(frame, 0x30));
    }

    [Fact]
    public void RetainedFilterSkipsScriptPhysicsColorSequenceButDrawsRawPlanarMatrix()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFFFF).WriteTemporaryByte(0x145, 0xFF);
        frame = PutDword(frame, 0x10, unchecked((uint)-4096));
        frame = PutDword(frame, 0x14, 10 << 8);
        frame = PutDword(frame, 0x18, unchecked((uint)(-20 << 8)));
        frame = PutDword(frame, 0x1C, 30 << 8);
        frame = PutDword(frame, 0x30, Address(0x5000));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 2).MarkUnknown(Particle + 8, 4).MarkUnknown(Particle + 0x20, 16).MarkUnknown(Particle + 0x3C, 3) };
        NativeBattleActionMatrix? drawn = null;
        NativeBattleWaterParticleServices services = Services() with { DrawRectangles = (current, matrix) =>
        {
            drawn = matrix;
            Assert.Equal(Address(0x5000), current.ReadTemporaryDword(0x90));
            return current;
        } };
        NativeBattleWaterExecutionFrame result = Advance(frame, services);
        Assert.NotNull(drawn);
        Assert.Equal(new short[] { -256, -1, 0, 0, -256, -1, 0, 0, 0 }, drawn.Rotation);
        Assert.Equal((110, -220, 330), (drawn.X, drawn.Y, drawn.Z));
        Assert.Equal((byte)0xFF, result.ReadTemporaryByte(0x145));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.Arena.Read(Particle + 0x10, 16), result.Arena.Read(Particle + 0x10, 16));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x20, 1));
    }

    [Theory]
    [InlineData(0, 0x6008, false)]
    [InlineData(1, 0x6000, true)]
    [InlineData(2, 0x6100, false)]
    public void SequenceAdvancesExactlyOnceAndTerminalRetainsRecordPointer(byte command, int next, bool terminal)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x0101);
        frame = PutDword(frame, 8, Address(0x6000));
        frame = PutDword(frame, 0xC, Address(0x6100));
        frame = PutDword(frame, 0x4C, Address(0x7000));
        frame = PutWord(frame, 0x34, 5);
        frame = ArenaDword(frame, 0x6000, 0x100);
        frame = frame with { Arena = frame.Arena.Write(0x6004, new byte[] { 0, 4, command }) };
        int draws = 0;
        NativeBattleWaterExecutionFrame result = Advance(frame, Services() with { DrawRectangles = (current, _) =>
        {
            draws++;
            return current;
        } });
        Assert.Equal(Address(next), Dword(result, 8));
        Assert.Equal((ushort)0x400, Word(result, 0x34));
        Assert.Equal(terminal ? 0u : Address(0x7100), Dword(result, 0x30));
        Assert.Equal(terminal ? 0 : 1, draws);
        Assert.Equal(terminal ? 0u : 0x1002u, Dword(result, 4));
    }

    [Fact]
    public void SequenceUsesFullSubtractionBeforeNarrowedTimerAndUnsupportedCasesFail()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xCA, 0x8000);
        frame = PutWord(frame, 2, 1);
        frame = PutDword(frame, 8, Address(0x6000));
        frame = PutWord(frame, 0x34, 0x7FFF);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x6000, 8) };
        Assert.Equal((ushort)0xFFFF, Word(Advance(frame), 0x34));
        Assert.Throws<NotSupportedException>(() => One(Fixture(0x0304)));
        NativeBattleWaterExecutionFrame unsupportedPlacement = Fixture(0x0101);
        unsupportedPlacement = unsupportedPlacement with { Arena = unsupportedPlacement.Arena.Write(Particle + 0x37, new byte[] { 3 }) };
        NotSupportedException placement = Assert.Throws<NotSupportedException>(() => Advance(unsupportedPlacement));
        Assert.Contains("placement mode 3", placement.Message);
        Assert.Contains("PC 0x00001002", placement.Message);
        Assert.Contains("raw instruction 0x0101", placement.Message);
        NotSupportedException instruction = Assert.Throws<NotSupportedException>(() => One(Fixture(0xAB03)));
        Assert.Contains("0xAB03", instruction.Message);
        Assert.Contains("index 171", instruction.Message);
        Assert.Contains("PC 0x00001000", instruction.Message);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterParticles.AdvanceType6(Fixture(1, 1, 0x0101), new(0x1000, 6, 0x1000), Bank, Services(), 2));
    }

    private static NativeBattleWaterExecutionFrame Fixture(params ushort[] words)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x44, Address(Particle)).WriteTemporaryDword(0x24, 3).WriteTemporaryDword(0x28, 5);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x4000));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xCA, 5);
        frame = frame with { Arena = frame.Arena.Write(0x4000, new byte[] { 1, 0 }).Write(Particle + 0x37, new byte[] { 1 }) };
        frame = PutDword(frame, 4, 0x1000);
        for (int i = 0; i < words.Length; i++)
        {
            frame = frame with { Arena = WriteWord(frame.Arena, 0x1000 + 2 * i, words[i]) };
        }
        frame = frame.WriteTemporaryWord(0x14A, words[0]);
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0));
        for (int i = 0; i < 9; i++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, Matrix + (uint)(2 * i), unchecked((ushort)basis.Rotation[i]));
        }
        frame = WaterSchedulerFixture.CoreDword(frame, Matrix + 20, 100);
        frame = WaterSchedulerFixture.CoreDword(frame, Matrix + 24, unchecked((uint)-200));
        return WaterSchedulerFixture.CoreDword(frame, Matrix + 28, 300);
    }

    private static NativeBattleWaterParticleServices Services()
    {
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        return new((_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected external metadata read."), (_, _) => Matrix, (_, _) => throw new Xunit.Sdk.XunitException("Unexpected texture page getter."), (_, _) => throw new Xunit.Sdk.XunitException("Unexpected CLUT getter."), (_, _) => throw new Xunit.Sdk.XunitException("Unexpected rectangle draw."))
        {
            ReadRegisters = _ => registers,
            PublishRegisters = (current, step) =>
            {
                registers = step;
                return current;
            }
        };
    }
    private static NativeBattleWaterExecutionFrame One(NativeBattleWaterExecutionFrame frame, NativeBattleWaterParticleServices? services = null) => NativeBattleWaterParticles.ExecuteOne(frame, new(0x1000, 0x100, 0x1000), Bank, services ?? Services());
    private static NativeBattleWaterExecutionFrame Advance(NativeBattleWaterExecutionFrame frame, NativeBattleWaterParticleServices? services = null) => NativeBattleWaterParticles.AdvanceType6(frame, new(0x1000, 0x100, 0x1000), Bank, services ?? Services());
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(Particle + offset, 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(Particle + offset, 2));
    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value) => frame with { Arena = WriteWord(frame.Arena, Particle + offset, value) };
    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleWaterExecutionFrame PutDword(NativeBattleWaterExecutionFrame frame, int offset, uint value) => ArenaDword(frame, Particle + offset, value);
    private static NativeBattleWaterExecutionFrame ArenaDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }
}
