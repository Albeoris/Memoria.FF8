using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84CameraTests
{
    [Fact]
    public void Camera_initialization_publishes_immediately_and_transition_advances_both_programs()
    {
        var fixture = NativeBattleAngelWing84ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(5, 0x78DE40, 100, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleAngelWing84SceneMemory scene = NativeBattleAngelWing84SceneMemory.Initialize(new(0, 0, 0), new(1, 0, 0), null).WriteWord(66, 1);
        NativeBattleAngelWing84Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = appended.Hierarchy.Pools }, State = fixture.Frame.State with { Scene = scene } };
        List<(uint Source, ushort Frame)> publications = [];
        NativeBattleAngelWing84CameraInputs inputs = new(Resources(), (current, step) =>
        {
            publications.Add((BinaryPrimitives.ReadUInt32LittleEndian(step.State.Data.AsSpan().Slice(0xAC)), BinaryPrimitives.ReadUInt16LittleEndian(step.State.Data.AsSpan().Slice(0xE2))));
            return current;
        });
        frame = NativeBattleAngelWing84Camera.Advance(frame, appended.Address, inputs).Frame;
        Assert.Empty(publications);
        Assert.NotNull(frame.State.Scene!.Camera);
        frame = NativeBattleAngelWing84Camera.Advance(frame, appended.Address, inputs).Frame;
        Assert.Equal(new[] { (0x1575DE0u, (ushort)1) }, publications);
        NativeBattleAngelWing84Frame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleAngelWing84Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleAngelWing84Camera.Advance(frame, appended.Address, inputs).Frame;
        Assert.Equal(new[] { (0x1575DE0u, (ushort)1), (0x1575DE0u, (ushort)2), (0x1575E28u, (ushort)1) }, publications);
        Assert.Equal((byte)3, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(appended.Address).ReadByte(41));
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleAngelWing84Camera.Advance(clone, appended.Address, inputs).Frame));
    }

    private static NativeBattleAngelWing84CameraResources Resources()
    {
        uint[] addresses = [0x1575DE0, 0x1575E28, 0x1575E70, 0x1575F20, 0x1575F90, 0x1575FD8];
        ImmutableArray<NativeBattleMoogleCameraSource> sources = addresses.Select((address, index) =>
        {
            byte[] bytes = new byte[72];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, index == 3 ? 0x1575EB8u : index == 4 ? 0x1575F68u : 0u);
            foreach ((int offset, short value) in new (int, short)[] { (0x26, 400), (0x28, 400), (0x36, 300), (0x38, 300), (0x3A, 4096) })
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
            }
            return new NativeBattleMoogleCameraSource(address, bytes.ToImmutableArray());
        }).ToImmutableArray();
        NativeBattleMoogleCameraCurve Curve(uint address, int count) => new(address, Enumerable.Repeat((short)0, count - 1).Append((short)4096).ToImmutableArray());
        return new(sources, [Curve(0x1575EB8, 50), Curve(0x1575F68, 18)]);
    }
}
