using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221GridTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_grids_use_low_nine_position_bits_512_spacing_32_UV_steps_and_actual_alternate_rows(bool alternate)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, basis + 0x4000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, basis + 0x5000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797800 + 0xCC, 0x00665544);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, basis + 0x3000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x02030201);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 0x5678);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 0x103);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 0x10);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 0x107);
        byte[] payload = new byte[12];
        short[] parameters = [-3, -2, 2, 2, -100, 1000];
        for (int index = 0; index < parameters.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(index * 2), parameters[index]);
        }
        frame = frame with { Arena = frame.Arena.Write(0x3000, payload).MarkUnknown(0x4006, 2) };
        int projections = 0;
        int packets = 0;
        ImmutableArray<byte> rowV = [3, 9, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 244];
        NativeBattleCinematicGrid28Services services = new((owner, texture) =>
        {
            Assert.Equal(-2, texture);
            return owner.WriteTemporaryDword(0xFC, 0x1234);
        }, (owner, texture) => new(owner, 0xABCD), (owner, selector) =>
        {
            Assert.Equal(-3, selector);
            return 0x2797800;
        }, owner =>
        {
            Assert.Equal(0x1000, WaterSchedulerFixture.ReadSlotWord(owner, 0, 0x8E));
            return owner;
        }, (owner, color) => owner, (owner, near, far, third) => owner, (owner, color) => owner, (owner, vertex, normal, material, screen, color, flags) =>
        {
            Assert.Equal(unchecked((ushort)(-253 + projections / 3 * 512)), owner.ReadTemporaryWord(0xC0));
            Assert.Equal(unchecked((ushort)(-249 + projections % 3 * 512)), owner.ReadTemporaryWord(0xC4));
            Assert.Equal((ushort)256, owner.ReadTemporaryWord(0xC8));
            owner = Write(owner, screen, (uint)(0x10000 + projections));
            owner = Write(owner, color, 0x00030201);
            projections++;
            return new(owner, -projections);
        }, (owner, output, ordering, batch) =>
        {
            Assert.Equal(9, projections);
            Assert.Equal(basis + 0x5000 + (uint)(52 * packets), output);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(52, batch.AllocationBytes);
            Assert.Equal(packets % 2 == 0 ? 0xE0 : 0, attempt.Data[0xC]);
            Assert.Equal(packets % 2 == 0 ? 0xFF : 0x20, attempt.Data[0x18]);
            Assert.Equal(alternate ? rowV[packets / 2] : packets < 2 ? (byte)0xE0 : (byte)0, attempt.Data[0xD]);
            Assert.Equal(alternate ? rowV[packets / 2 + 1] : packets < 2 ? (byte)0xFF : (byte)0x20, attempt.Data[0x25]);
            Assert.Equal(0x3E030201u, attempt.Primitive!.Words[1]);
            Assert.Equal(packets < 2 ? 0xFFCu : 0xFF8u, ordering);
            foreach (int untouched in new[] { 0, 1, 2, 0x26, 0x27, 0x32, 0x33 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[untouched]);
            }
            packets++;
            return owner;
        });
        NativeBattleWaterExecutionFrame result = alternate ? NativeBattleApocalypse221Grid.Draw68(frame, services, rowV) : NativeBattleApocalypse221Grid.Draw51(frame, services);
        Assert.Equal(4, packets);
        Assert.Equal(basis + 0x50D0, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0x5678, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8E));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4006, 2));
    }

    [Fact]
    public void Alternate_grid_never_substitutes_an_absent_row_directory()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleApocalypse221Grid.Draw68(WaterSchedulerFixture.Create(), null!, []));
    }

    private static NativeBattleWaterExecutionFrame Write(NativeBattleWaterExecutionFrame frame, uint address, uint value)
        => frame with { Arena = frame.Arena.Write(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), BitConverter.GetBytes(value)) };
}
