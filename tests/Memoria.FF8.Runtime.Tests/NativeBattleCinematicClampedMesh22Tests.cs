using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicClampedMesh22Tests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Manager = 0x2797690;
    private const int Shape = 0x10000;
    private const int Work = 0x20000;
    private const int Descriptor = 0x30000;
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Theory]
    [InlineData((ushort)8, 0)]
    [InlineData((ushort)0, -7)]
    public void Parent_MAC_transform_and_optional_ceiling_preserve_native_work_and_depth_ownership(ushort flags, short expectedY)
    {
        NativeBattleWaterExecutionFrame original = Fixture(flags);
        NativeBattleScriptedMeshProjected result = Project(original);
        Assert.Equal(17u, Dword(result.Frame, Work + 8));
        Assert.Equal(36u, Dword(result.Frame, Work + 12));
        Assert.Equal(1000u, Dword(result.Frame, Work + 16));
        Assert.Equal(250u, Dword(result.Frame, Work + 4));
        Assert.Equal((ushort)0xBEEF, Word(result.Frame, Work + 6 + 24));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(result.DepthOwner.Read(0, 4)));
        Assert.Equal(0x3F000000u, result.DepthScaleBits1877DA8);
        Assert.Equal(Base + Shape, WaterSchedulerFixture.ReadSlotDword(result.Frame, 0, 0xD8));
        Assert.Equal((uint)flags, WaterSchedulerFixture.ReadSlotDword(result.Frame, 0, 0xBC));
        Assert.Equal((ushort)expectedY, BinaryPrimitives.ReadUInt16LittleEndian(result.Registers.NormalInput!.Value.AsSpan().Slice(2)));
        Assert.Equal(result.Registers.Flags, result.Frame.ReadTemporaryDword(0xFC));
        NativeBattleScriptedMeshProjected restored = Project(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(original))!);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Initialized_descriptor_reuses_shape_and_full_MAC_coordinates_wrap_only_when_copied_to_WORDs()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(8);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD8, Base + Shape);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 8);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 30000);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 0);
        frame = frame with { Arena = frame.Arena.Write(Shape + 0x40, BitConverter.GetBytes((short)10000)) };
        NativeBattleScriptedMeshProjected result = Project(frame, (_, _) => throw new InvalidOperationException("Initialized shape must not be resolved again."));
        Assert.Equal(40000u, Dword(result.Frame, Work + 8));
        Assert.Equal(unchecked((ushort)40000), BinaryPrimitives.ReadUInt16LittleEndian(result.Registers.NormalInput!.Value.AsSpan()));
    }

    [Fact]
    public void Submission_uses_base_primitive_slot_and_retained_depths_even_with_owner_variant_bits()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(8);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x4C, 6);
        byte[] group = new byte[28];
        BinaryPrimitives.WriteInt16LittleEndian(group, 8);
        BinaryPrimitives.WriteInt16LittleEndian(group.AsSpan(2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(group.AsSpan(26), -1);
        frame = frame with { Arena = frame.Arena.Write(Shape + 0x80, group) };
        NativeBattleScriptedMeshProjected projected = Project(frame);
        NativeBattleScriptedMeshSubmission result = NativeBattleCinematicClampedMesh22.Submit(projected, 0x9C, type =>
        {
            Assert.Equal(8, type);
            return 0xB28110;
        }, type =>
        {
            Assert.Equal(8, type);
            return 20;
        }, pointer => checked((int)((pointer - 0x600000) / 4)));
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Equal(32, result.Packets.AllocationBytes);
        Assert.Equal(new[] { 11068, 11068, 11068, 0 }, packet.Primitive!.VertexPayload4);
        Assert.Equal(Base + 0x40000 + 32u, WaterSchedulerFixture.ReadCoreDword(result.Frame, Manager + 0x7C));
    }

    private static NativeBattleScriptedMeshProjected Project(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, short, uint>? resolveShape = null)
    {
        NativeBattleScriptedGraphicsMemory controls = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Depths = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x12345678u)), DepthScaleBits1877DA8 = 0x3F000000 };
        return NativeBattleCinematicClampedMesh22.Project(frame, (_, selector) => selector == -2 ? Base + 0x2100 : throw new InvalidDataException(), resolveShape ?? ((_, selector) => selector == 2 ? Base + Shape : throw new InvalidDataException()), new(null, 216, 160, 108), controls, NativeBattleScriptedLightingMemory.Unknown(), new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort flags)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x600000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, Base + Work);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, Base + 0x40000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Base + Descriptor);
        foreach (int offset in new[] { 0x8C, 0x8E, 0x90 })
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, offset, 256);
        }
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 5);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 10);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 20);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 1000);
        byte[] view = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(view.AsSpan(lane * 2), Identity.Rotation[lane]);
        }
        byte[] descriptor = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor, flags);
        BinaryPrimitives.WriteInt16LittleEndian(descriptor.AsSpan(2), 2);
        BinaryPrimitives.WriteInt16LittleEndian(descriptor.AsSpan(6), -2);
        byte[] input = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(input, 2);
        BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), 9);
        byte[] retained = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(retained, 13);
        BinaryPrimitives.WriteInt16LittleEndian(retained.AsSpan(2), -7);
        BinaryPrimitives.WriteInt16LittleEndian(retained.AsSpan(4), 1000);
        BinaryPrimitives.WriteUInt16LittleEndian(retained.AsSpan(30), 0xBEEF);
        return frame with
        {
            Core = frame.Core.Write(0x27979E8, view),
            Arena = frame.Arena.Write(Descriptor, descriptor).Write(Shape + 8, BitConverter.GetBytes(0x80u)).Write(Shape + 0x14, BitConverter.GetBytes(0x40u)).Write(Shape + 0x18, BitConverter.GetBytes(1)).Write(Shape + 0x40, input).Write(Work, retained)
        };
    }

    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
}
