using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorAnchorCommandsTests
{
    [Theory]
    [InlineData(0xAD, false)]
    [InlineData(0x80AD, true)]
    public void PhysicalCommandRestoresTheSelectedRigThenTransformsTheCachedSignedPoint(ushort instruction, bool linked)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [5]).Write(0x2094, [0xFE, 0xFF, 3, 0, 0xFC, 0xFF]) };
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60 + 5 * 4, 0x1D975CC);
        int updates = 0;
        NativeBattleCinematicActorAnchorServices services = new((current, actor, restore, selectedLinked) =>
        {
            Assert.Equal(0x1D975CCu, actor);
            Assert.True(restore);
            Assert.Equal(linked, selectedLinked);
            return Pose(current, 7, -8, 9);
        }, (current, kind) =>
        {
            updates++;
            Assert.Equal((byte)9, kind);
            Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return current;
        }, (current, address) =>
        {
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2094u, address);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorAnchorCommands.Execute(frame, new(0x1000, 4, 0x1000), services);
        Assert.Equal(1, updates);
        Assert.Equal(5 << 16, Position(result, 0));
        Assert.Equal(-5 << 16, Position(result, 1));
        Assert.Equal(5 << 16, Position(result, 2));
        Assert.Equal(5u, result.ReadTemporaryDword(0xF0));
        Assert.Equal(unchecked((uint)-5), result.ReadTemporaryDword(0xF4));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0x706E, true)]
    [InlineData(0x106E, false)]
    public void TargetCommandUsesSignedTargetListAndTruncatingCentroidOnlyForSelectedAxes(ushort instruction, bool all)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = frame with { Core = frame.Core.Write(owner + 0x41, [2]).Write(owner + 0x48, [0, 1]), Arena = frame.Arena.Write(0x2060, BitConverter.GetBytes(123)).Write(0x2064, BitConverter.GetBytes(456)) };
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60, 0x1D972C0);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x64, 0x1D9735C);
        List<uint> actors = [];
        NativeBattleCinematicActorAnchorServices services = new((current, actor, restore, linked) =>
        {
            actors.Add(actor);
            Assert.False(restore);
            Assert.False(linked);
            return actor == 0x1D972C0 ? Pose(current, -7, 11, -9) : Pose(current, 2, -8, 4);
        }, (current, _) => current, (current, address) =>
        {
            Assert.Equal(current.TemporaryFrame.Address + 0xC0, address);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorAnchorCommands.Execute(frame, new(0x1000, 4, 0x1000), services);
        Assert.Equal(new uint[] { 0x1D972C0, 0x1D9735C }, actors);
        Assert.Equal(-2 << 16, Position(result, 0));
        Assert.Equal(all ? 1 << 16 : 123, Position(result, 1));
        Assert.Equal(all ? -2 << 16 : 456, Position(result, 2));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0x60, 0, 0, 7, 9, 4)]
    [InlineData(0x8060, -2, 0, -25, 9, 8)]
    [InlineData(0x8060, 2, 64, 7, 41, 8)]
    [InlineData(0x8060, 2, -64, 7, -23, 8)]
    public void AnchorModeUsesSignedRadialOperandsAndPublishesPcBeforeSlotUpdate(ushort instruction, short radius, short angle, int x, int z, uint advance)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60, 0x1D972C0);
        frame = frame with { Arena = frame.Arena.Write(0x1004, BitConverter.GetBytes(radius)).Write(0x1006, BitConverter.GetBytes(angle)) };
        if (instruction == 0x60)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 4) };
        }
        NativeBattleCinematicActorAnchorServices services = new((current, actor, restore, linked) =>
        {
            Assert.Equal(0x1D972C0u, actor);
            Assert.False(restore);
            Assert.False(linked);
            return Pose(current, 7, -8, 9);
        }, (current, _) =>
        {
            Assert.Equal(0x1000u + advance, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return current;
        }, (current, _) => current)
        {
            ReadScale38 = _ => 4096
        };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorAnchorCommands.Execute(frame, new(0x1000, 8, 0x1000), services);
        NativeBattleWaterExecutionFrame continued = NativeBattleCinematicActorAnchorCommands.Execute(restored, new(0x1000, 8, 0x1000), services);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(continued));
        Assert.Equal(x << 16, Position(result, 0));
        Assert.Equal(-8 << 16, Position(result, 1));
        Assert.Equal(z << 16, Position(result, 2));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(100000u)]
    public void RandomAnchorRestoresAndConsumesExactlyOneDrawWithoutReadingAnOperand(uint seed)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xB4);
        frame = frame with { Random = new(seed), Arena = frame.Arena.MarkUnknown(0x1002, 6) };
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60, 0x1D972C0);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort value);
        int selected = value * 2 >> 15;
        NativeBattleCinematicActorAnchorServices services = new((current, actor, restore, linked) =>
        {
            Assert.Equal(0x1D972C0u, actor);
            Assert.True(restore);
            Assert.False(linked);
            NativeBattleCinematicBonePose pose = Pose(current, -7, 8, -9);
            byte[] skeleton = new byte[112];
            skeleton[0] = 2;
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), -1);
            return pose with { Skeleton = skeleton.ToImmutableArray(), Bones = [pose.Bones[0], pose.Bones[0] with { X = 20 }] };
        }, (current, kind) =>
        {
            Assert.Equal((byte)9, kind);
            Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            Assert.Equal(expected, current.Random);
            return current;
        }, (current, address) =>
        {
            Assert.Equal(current.TemporaryFrame.Address + 0xC0, address);
            return current;
        });
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorAnchorCommands.Execute(frame, new(0x1000, 2, 0x1000), services);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCinematicActorAnchorCommands.Execute(restored, new(0x1000, 2, 0x1000), services)));
        Assert.Equal(expected, result.Random);
        Assert.Equal((-7 + 20 * selected) << 16, Position(result, 0));
        Assert.Equal(8 << 16, Position(result, 1));
        Assert.Equal(-9 << 16, Position(result, 2));
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void NegativeWordSelectorIsNotPromotedToAnUnsignedBoneAlias()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xAD);
        frame = frame with { Arena = frame.Arena.Write(0x1002, [0xFF, 0xFF]) };
        NativeBattleCinematicActorAnchorServices services = new((current, _, _, _) => Pose(current, 0, 0, 0), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicActorAnchorCommands.Execute(frame, new(0x1000, 4, 0x1000), services));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        return frame with { Arena = frame.Arena.Write(0x2018, [9]) };
    }

    private static int Position(NativeBattleWaterExecutionFrame frame, int lane) => BinaryPrimitives.ReadInt32LittleEndian(frame.Arena.Read(0x205C + 4 * lane, 4));

    private static NativeBattleCinematicBonePose Pose(NativeBattleWaterExecutionFrame frame, int x, int y, int z)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattleActionMatrix identity = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), 0, 0, 0, 4096);
        return new(frame, skeleton.ToImmutableArray(), [identity], identity with { X = x, Y = y, Z = z });
    }
}
