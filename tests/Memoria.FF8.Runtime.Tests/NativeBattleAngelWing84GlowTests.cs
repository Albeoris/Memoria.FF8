using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84GlowTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Appearance_uses_the_original_mesh_and_separate_caster_or_camera_depth(bool caster)
    {
        var fixture = Fixture(caster, 1);
        NativeBattleAngelWing84GlowStep step = NativeBattleAngelWing84Glows.Advance(fixture.Frame, fixture.Address, fixture.Inputs);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((byte)2, task.ReadByte(41));
        Assert.Equal((ushort)4096, task.ReadWord(80));
        Assert.Equal(unchecked((ushort)-512), task.ReadWord(82));
        Assert.Equal(caster ? 0x15720B0u : 0x1572810u, task.ReadDword(76));
        Assert.Equal((ushort)(caster ? 33 : 777), task.ReadWord(32));
        NativeBattleAngelWing84Frame restored = JsonSerializer.Deserialize<NativeBattleAngelWing84Frame>(JsonSerializer.Serialize(step.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleAngelWing84Glows.Advance(step.Frame, fixture.Address, fixture.Inputs)), JsonSerializer.Serialize(NativeBattleAngelWing84Glows.Advance(restored, fixture.Address, fixture.Inputs)));
    }

    [Fact]
    public void Camera_glow_hides_at_phase_three_and_returns_only_at_phase_four()
    {
        var fixture = Fixture(false, 3);
        NativeBattleAngelWing84Frame frame = fixture.Frame with { State = fixture.Frame.State with { Scene = fixture.Frame.State.Scene!.WriteWord(64, 3) } };
        frame = NativeBattleAngelWing84Glows.Advance(frame, fixture.Address, fixture.Inputs).Frame;
        Assert.Equal(4, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address).ReadByte(38) & 4);
        frame = NativeBattleAngelWing84Glows.Advance(frame, fixture.Address, fixture.Inputs).Frame;
        Assert.Equal((byte)4, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address).ReadByte(41));
        frame = frame with { State = frame.State with { Scene = frame.State.Scene!.WriteWord(64, 4) } };
        frame = NativeBattleAngelWing84Glows.Advance(frame, fixture.Address, fixture.Inputs).Frame;
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((byte)5, task.ReadByte(41));
        Assert.Equal(0, task.ReadByte(38) & 4);
    }

    [Theory]
    [InlineData(true, 512)]
    [InlineData(false, 128)]
    public void Terminal_fade_preserves_distinct_native_rates_and_releases_one_parent(bool caster, ushort rate)
    {
        var fixture = Fixture(caster, 5);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(80, (ushort)(4096 - rate)));
        NativeBattleAngelWing84GlowStep step = NativeBattleAngelWing84Glows.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Address, fixture.Inputs);
        hierarchy = new(step.Frame.Effect.Pools);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)4096, hierarchy.Read(fixture.Address).ReadWord(80));
        Assert.Equal((byte)0, hierarchy.Read(hierarchy.Address(0, 0)).ReadByte(40));
    }

    private static (NativeBattleAngelWing84Frame Frame, uint Address, NativeBattleAngelWing84GlowInputs Inputs) Fixture(bool caster, byte phase)
    {
        var fixture = NativeBattleAngelWing84ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(6, caster ? 0x796FD0u : 0x7972B0u, 88, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = append.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(append.Address).WriteByte(41, phase).WriteWord(36, 32).WriteDword(48, 4096).WriteDword(52, 4096).WriteDword(56, 4096).WriteDword(76, caster ? 0x15720B0u : 0x1572810u);
        hierarchy = hierarchy.Write(append.Address, task);
        byte[] cameraBytes = NativeBattleMoogleCamera.Reset732AF0().Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(cameraBytes.AsSpan(224), 777);
        NativeBattleAngelWing84SceneMemory scene = NativeBattleAngelWing84SceneMemory.Initialize(new(0, 0, 0), new(0, 0, 0), NativeBattleMoogleCamera.Reset732AF0() with { Data = cameraBytes.ToImmutableArray() });
        NativeBattleAngelWing84Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools }, State = fixture.Frame.State with { Scene = scene, PrimaryDrawCursor = 200000 } };
        byte[] mesh = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 8);
        NativeBattleAngelWing84GlowResources resources = new([new(0, 0x15720B0, mesh.ToImmutableArray()), new(1, 0x1572810, mesh.ToImmutableArray())]);
        NativeBattleAngelWing84GlowInputs inputs = new((actor, bone, fraction) =>
        {
            Assert.Equal((byte)0, actor);
            Assert.Equal((byte)241, bone);
            Assert.Equal((short)0, fraction);
            return new(11, 22, 33);
        }, fixture.Inputs.Camera, new(fixture.Inputs.Camera, 256, 160, 120), NativeBattleLogicalOrdering.Clear(), NativeBattleGteColorInterpolation.CodeConstants45F270, resources);
        return (frame, append.Address, inputs);
    }
}
