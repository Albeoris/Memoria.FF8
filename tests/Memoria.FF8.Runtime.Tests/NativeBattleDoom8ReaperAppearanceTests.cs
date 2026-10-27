using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ReaperAppearanceTests
{
    [Fact]
    public void Initializer_clears_exact_scratch_and_only_proved_descriptor_fields()
    {
        NativeBattleDoom8Frame before = Frame() with { State = NativeBattleDoom8State.FromUnknown().Write(0x275850F, [77]).Write(0x275F9E0 + 0x1F, [99]).Write(0x275F9E0 + 0x26, [111]).Write(0x275ABF0, [123]) };
        NativeBattleDoom8Frame result = NativeBattleDoom8ReaperAppearance.Initialize8CD020(before, 0x275F2C8);
        Assert.Equal(new byte[] { 77 }, result.State.Read(0x275850F, 1));
        Assert.Equal((ushort)0, result.State.ReadWord(0x2758568));
        Assert.Equal(new byte[] { 0 }, result.State.Read(0x2758787, 1));
        Assert.Equal(0x2758504u, result.State.ReadDword(0x275855C));
        Assert.Equal(0x2758504u, result.State.ReadDword(0x2758560));
        Assert.Equal(0x275F2C8u, result.State.ReadDword(0x2758558));
        Assert.Equal((ushort)320, result.State.ReadWord(0x275F9F8));
        Assert.Equal(new byte[] { 128, 128, 128, 99 }, result.State.Read(0x275F9FC, 4));
        Assert.Equal(new byte[] { 111 }, result.State.Read(0x275FA06, 1));
        Assert.Equal(new byte[] { 123 }, result.State.Read(0x275ABF0, 1));
        Assert.Throws<NotSupportedException>(() => result.State.Read(0x275F9E0, 4));
    }

    [Fact]
    public void Camera_setup_uses_low_translation_WORDs_and_keeps_packed_world_padding()
    {
        NativeBattleDoom8Frame frame = NativeBattleDoom8ReaperAppearance.Initialize8CD020(Frame(), 0x275F2C8);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattlePackedMatrix32 world = NativeBattlePackedMatrix32.FromMatrix(identity with { X = 65537, Y = -65538, Z = -65539 }, 0xABCD);
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(identity with { X = 10, Y = 20, Z = 2000 }, null);
        NativeBattleDoom8Frame result = NativeBattleDoom8ReaperAppearance.PrepareCamera8CC6B0(frame, world, camera);
        Assert.Equal((ushort)0xABCD, result.State.ReadWord(0x2758522));
        Assert.Equal((ushort)0, result.State.ReadWord(0x2758542));
        Assert.Equal(11u, result.State.ReadDword(0x2758544));
        Assert.Equal(18u, result.State.ReadDword(0x2758548));
        Assert.Equal(1997u, result.State.ReadDword(0x275854C));
        Assert.Equal(new int?[] { 11, 18, 1997 }, result.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new byte[] { 1, 0, 254, 255, 253, 255, 255, 255 }, result.Memory.ScriptedColorRegisters.NormalInput!.Value);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(SnapshotJson(result), SnapshotJson(NativeBattleDoom8ReaperAppearance.PrepareCamera8CC6B0(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleDoom8Frame>(SnapshotJson(frame))!), world, camera)));
    }

    [Fact]
    public void Full_LIGHT_and_back_transform_preserves_R_T_and_real_padding()
    {
        NativeBattleDoom8Frame frame = Frame();
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters!), 0, new(100, -200, 300), null);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(identity with { X = 3, Y = 4, Z = 5 }, 0xBEEF);
        NativeBattleEffectMemory memory = NativeBattleDoom8ReaperAppearance.LoadBoneLight(frame.Memory, bone);
        NativeBattleEffectMemory result = NativeBattleLightBackTransform.Transform(memory, 0);
        Assert.Same(frame.Memory.GeometryRegisters, result.GeometryRegisters);
        Assert.Equal(new int?[] { 103, -196, 305 }, result.ScriptedColorRegisters!.Mac);
        Assert.Equal((byte)0xEF, result.ScriptedLighting!.LightMatrix!.Bytes[18]);
        Assert.Equal((byte)0xBE, result.ScriptedLighting.LightMatrix.Bytes[19]);
        Assert.All(result.ScriptedLighting.LightMatrix.Known, value => Assert.True(value));
        Assert.Equal(SnapshotJson(result), SnapshotJson(NativeBattleZantetsukenGte.EvaluatePlane(memory, 0)));
    }

    [Fact]
    public void Unknown_LIGHT_coefficient_and_padding_remain_unknown_without_R_T_substitution()
    {
        NativeBattleDoom8Frame frame = Frame();
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        bone = bone with { Known = bone.Known.SetItem(0, false).SetItem(24, false) };
        NativeBattleEffectMemory memory = NativeBattleDoom8ReaperAppearance.LoadBoneLight(frame.Memory, bone);
        NativeBattleGeometryRegisterStep input = NativeBattleGeometry.LoadVector(new(memory.GeometryRegisters!, memory.ScriptedColorRegisters!), 0, new(10, 20, 30), null);
        memory = memory with { GeometryRegisters = input.Geometry, ScriptedColorRegisters = input.Colors };
        NativeBattleEffectMemory result = NativeBattleLightBackTransform.Transform(memory, 0);
        Assert.Null(result.ScriptedColorRegisters!.Mac[0]);
        Assert.Null(result.ScriptedColorRegisters.Mac[1]);
        Assert.Null(result.ScriptedLighting!.BackColor!.ReadLane(1));
        Assert.Equal(30, result.ScriptedColorRegisters.Mac[2]);
        Assert.Null(result.ScriptedColorRegisters.Flags);
        Assert.False(result.ScriptedLighting!.LightMatrix!.Known[18]);
        Assert.False(result.ScriptedLighting.LightMatrix.Known[19]);
        Assert.Same(memory.GeometryRegisters, result.GeometryRegisters);
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleLightBackTransform.Transform(memory, 3));
    }

    internal static NativeBattleDoom8Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() };
        return new(memory, new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null), new(1), NativeBattleDoom8State.FromUnknown());
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
