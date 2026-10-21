using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEnemyGrid67Tests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Enemy_grid_uses_its_U_masks_and_endpoint_while_original_grid_keeps_its_existing_policy(bool enemy)
    {
        const uint arena = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, arena + 0x4000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, arena + 0x5000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797800 + 0xCC, 0x00665544);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, arena + 0x3000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x02030201);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 0x5678);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 0x1003);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 0x10);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 0x1807);
        byte[] payload = new byte[12];
        short[] values = [-3, -2, 4, 2, -100, 1000];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(index * 2), values[index]);
        }
        frame = frame with { Arena = frame.Arena.Write(0x3000, payload).MarkUnknown(0x4006, 2) };
        int projections = 0;
        int packets = 0;
        List<string> calls = [];
        byte[] left = enemy ? [0x40, 0x60, 0, 0x20] : [0x40, 0x60, 0x80, 0xA0];
        byte[] right = enemy ? [0x60, 0x7F, 0x20, 0x40] : [0x60, 0x80, 0xA0, 0xC0];
        NativeBattleCinematicGrid28Services services = new((owner, texture) =>
        {
            calls.Add("page");
            Assert.Equal(-2, texture);
            return owner.WriteTemporaryDword(0xFC, 0x1234);
        }, (owner, texture) =>
        {
            calls.Add("clut");
            Assert.Equal(-2, texture);
            return new(owner, 0xABCD);
        }, (owner, selector) =>
        {
            calls.Add("resolve");
            Assert.Equal(-3, selector);
            return 0x2797800;
        }, owner =>
        {
            calls.Add("basis");
            Assert.Equal(0x1000, WaterSchedulerFixture.ReadSlotWord(owner, 0, 0x8E));
            return owner;
        }, (owner, color) =>
        {
            calls.Add("lighting");
            Assert.Equal(0x02030201u, color);
            return owner;
        }, (owner, near, far, third) =>
        {
            calls.Add("cue");
            Assert.Equal(-100, near);
            Assert.Equal(1000, far);
            Assert.Equal(512, third);
            return owner;
        }, (owner, color) =>
        {
            calls.Add("far");
            Assert.Equal(0x00665544u, color);
            return owner;
        }, (owner, vertex, normal, material, screen, color, flags) =>
        {
            Assert.Equal(unchecked((ushort)(-509 + projections / 5 * 512)), owner.ReadTemporaryWord(0xC0));
            Assert.Equal(unchecked((ushort)(-1017 + projections % 5 * 512)), owner.ReadTemporaryWord(0xC4));
            Assert.Equal((ushort)256, owner.ReadTemporaryWord(0xC8));
            owner = Write(owner, screen, checked((uint)(0x10000 + projections)));
            owner = Write(owner, color, 0x00030201);
            projections++;
            return new(owner, -4);
        }, (owner, output, ordering, batch) =>
        {
            Assert.Equal(15, projections);
            Assert.Equal(arena + 0x5000 + checked((uint)(packets * 52)), output);
            Assert.Equal(0xFFCu, ordering);
            Assert.Equal(52, batch.AllocationBytes);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(left[packets % 4], attempt.Data[0xC]);
            Assert.Equal(left[packets % 4], attempt.Data[0x24]);
            Assert.Equal(right[packets % 4], attempt.Data[0x18]);
            Assert.Equal(right[packets % 4], attempt.Data[0x30]);
            Assert.Equal(packets < 4 ? (byte)0xE0 : (byte)0, attempt.Data[0xD]);
            Assert.Equal(packets < 4 ? (byte)0xFF : (byte)0x20, attempt.Data[0x25]);
            Assert.Equal(attempt.Data[0xD], attempt.Data[0x19]);
            Assert.Equal(attempt.Data[0x25], attempt.Data[0x31]);
            Assert.Equal(0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(0xE)));
            Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(0x1A)));
            Assert.Equal(0x3E030201u, attempt.Primitive!.Words[1]);
            foreach (int untouched in new[] { 0, 1, 2, 0x26, 0x27, 0x32, 0x33 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[untouched]);
            }
            packets++;
            return owner;
        });
        NativeBattleWaterExecutionFrame result = enemy ? NativeBattleCinematicGrid28.DrawEnemy67(frame, services) : NativeBattleCinematicGrid28.DrawApocalypse(frame, services, []);
        Assert.Equal(new[] { "page", "clut", "resolve", "basis", "lighting", "cue", "far" }, calls);
        Assert.Equal(8, packets);
        Assert.Equal(enemy ? -192 : -448, unchecked((int)result.ReadTemporaryDword(0x70)));
        Assert.Equal(-288, unchecked((int)result.ReadTemporaryDword(0x74)));
        Assert.Equal(arena + 0x51A0, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0x5678, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8E));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4006, 2));
    }

    private static NativeBattleWaterExecutionFrame Write(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        return frame with { Arena = frame.Arena.Write(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), BitConverter.GetBytes(value)) };
    }
}
