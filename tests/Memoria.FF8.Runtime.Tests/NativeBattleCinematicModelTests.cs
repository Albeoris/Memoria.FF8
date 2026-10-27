using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleCinematicModelTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const int Model = 0x50000;
    private const int Skeleton = 0x11000;
    private const int AnimationLength = 4 + 256 * 4 + 65;
    private static readonly NativeBattleCinematicModelServices Services = new((_, id) => id == 21 ? Base + 0x10000 : throw new InvalidDataException(), (frame, pointer) => pointer == Base + 0x13000 ? frame.Arena.Read(0x13000, AnimationLength) : throw new InvalidDataException());

    [Theory]
    [InlineData(0x0015)]
    [InlineData(0xFF15)]
    public void Constructor_separates_shape_and_unsigned_animation_and_preserves_every_unwritten_model_byte(ushort argument)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleWaterExecutionFrame result;
        uint address;
        (result, address) = NativeBattleCinematicModel.Allocate(frame, unchecked((short)argument), Services);
        Assert.Equal(Base + Model, address);
        Assert.Equal(Base + Model + 0x80, WaterSchedulerFixture.ReadCoreDword(result, 0x2797704));
        Assert.Equal(argument, Word(result, Model + 8));
        Assert.Equal(Base + Model + 0x30, Dword(result, Model + 0x14));
        Assert.Equal(Base + Skeleton, Dword(result, Model + 0x30));
        Assert.Equal(Base + 0x12000, Dword(result, Model + 0x34));
        Assert.Equal(Base + 0x13000, Dword(result, Model + 0x38));
        Assert.Equal((byte)(argument >> 8), result.Arena.Read(Model + 0x20, 1)[0]);
        Assert.Equal(new byte[] { 4, 3, 0, 6, 0, 1, 2 }, result.Arena.Read(Model + 0x21, 7));
        int[] known = Enumerable.Range(0, 4).Concat(Enumerable.Range(8, 2)).Concat(Enumerable.Range(0x14, 4)).Concat(Enumerable.Range(0x20, 8)).Concat(Enumerable.Range(0x30, 12)).Concat(Enumerable.Range(0x50, 0x16)).Concat(Enumerable.Range(0x68, 4)).Concat(Enumerable.Range(0x78, 4)).ToArray();
        for (int offset = 0; offset < 0x80; offset++)
        {
            if (known.Contains(offset))
            {
                Assert.Single(result.Arena.Read(Model + offset, 1));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + offset, 1));
            }
        }
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(result.Arena, Skeleton, 2);
        Assert.Equal(new NativeBattleVector(0, 0, 0), pose.Translation);
        Assert.All(pose.Bones, bone => Assert.Equal(new NativeBattleVector(0, 0, 0), bone.Rotation));
        Assert.Equal((short)0, WordSigned(result, Skeleton + 32 + 18));
        Assert.Equal((short)0, WordSigned(result, Skeleton + 32 + 48 + 18));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Skeleton + 6, 2));
        Assert.Equal((short)713, WordSigned(result, Skeleton + 2));
    }

    [Fact]
    public void Register_publishes_current_bone_and_draw_three_then_advances_four_without_yield()
    {
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryByte(0x142, 0).WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1000, new byte[] { 0x3F, 0, 21, 0 }) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.Register(frame, new(0x1000, 4, 0x1000), Services);
        Assert.Equal(Base + Model, Dword(result, 0x20BC));
        Assert.Equal((byte)3, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal(new byte[] { 0, 0xFF }, result.Core.Read(0x2797554, 2));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + 0x10, 4));
    }

    [Fact]
    public void Selection_uses_mutated_current_pointer_table_and_skeleton_and_JSON_reselection_matches()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicModel.Allocate(Fixture(), 21, Services).Frame;
        byte[] replacement = new byte[64];
        replacement[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(replacement.AsSpan(2), 713);
        BinaryPrimitives.WriteInt16LittleEndian(replacement.AsSpan(16), -1);
        frame = frame with { Arena = frame.Arena.Write(0x14000, replacement).Write(Model + 0x30, BitConverter.GetBytes(Base + 0x14000)).Write(0x14008, new byte[] { 123, 0 }) };
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(SnapshotJson(frame))!);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.Select509440(frame, Base + Model, 0, Services);
        Assert.Equal((short)0, WordSigned(result, 0x14008));
        Assert.Equal(SnapshotJson(result), SnapshotJson(NativeBattleCinematicModel.Select509440(restored, Base + Model, 0, Services)));
        frame = frame with { Arena = frame.Arena.Write(Model + 0x21, new byte[] { 1 }) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicModel.Select509440(frame, Base + Model, 0, Services));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, Base + Model);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Model, 0x80) };
        frame = Put(frame, 0x10004, 0x1000);
        frame = Put(frame, 0x10008, 0x2000);
        frame = Put(frame, 0x1000C, 0x3000);
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 713);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 20);
        frame = frame with { Arena = frame.Arena.Write(Skeleton, skeleton).MarkUnknown(Skeleton + 6, 2).MarkUnknown(Skeleton + 32 + 18, 2).MarkUnknown(Skeleton + 80 + 18, 2) };
        byte[] section = new byte[AnimationLength];
        BinaryPrimitives.WriteInt32LittleEndian(section, 256);
        for (int index = 0; index < 256; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(4 + 4 * index), 1028);
        }
        section[1028] = 2;
        return frame with { Arena = frame.Arena.Write(0x13000, section) };
    }

    private static NativeBattleWaterExecutionFrame Put(NativeBattleWaterExecutionFrame frame, int offset, uint value) => frame with { Arena = frame.Arena.Write(offset, BitConverter.GetBytes(value)) };
    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
    private static short WordSigned(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(offset, 2));
}
