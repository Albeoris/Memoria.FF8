using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa88CameraTests
{
    [Fact]
    public void Camera_reset_clears_prior_yaw_without_importing_actor_yaw()
    {
        var fixture = Fixture(0);
        NativeBattleRinoa88Frame frame = NativeBattleRinoa88Camera.Advance(fixture.Frame, fixture.Address, Inputs()).Frame;
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(frame.State.Scene!.Camera!.Data.AsSpan().Slice(232)));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address).ReadByte(41));
    }

    [Fact]
    public void Party_camera_uses_pending_phase_and_retains_unknown_anchor_padding_metadata()
    {
        var fixture = Fixture(7);
        NativeBattleRinoa88SceneMemory scene = fixture.Frame.State.Scene!.WriteWord(64, 3).WriteWord(66, 4);
        NativeBattleRinoa88Frame frame = fixture.Frame with { State = fixture.Frame.State with { Scene = scene } };
        frame = NativeBattleRinoa88Camera.Advance(frame, fixture.Address, Inputs()).Frame;
        Assert.Equal((byte)8, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address).ReadByte(41));
        Assert.False(frame.State.Scene!.CameraAnchorPaddingKnown);
        Assert.Equal(0x1555F58u, BinaryPrimitives.ReadUInt32LittleEndian(frame.State.Scene.Camera!.Data.AsSpan().Slice(172)));
    }

    private static NativeBattleRinoa88CameraInputs Inputs() => new(new(NativeBattleRinoa88CameraResources.Addresses.Select(address => new NativeBattleMoogleCameraSource(address, new byte[72].ToImmutableArray())).ToImmutableArray()), _ => new byte[8].ToImmutableArray(), _ => throw new InvalidOperationException("This camera phase consumes no random value."), (frame, _) => frame);

    private static (NativeBattleRinoa88Frame Frame, uint Address) Fixture(byte phase)
    {
        var fixture = NativeBattleRinoa88ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend camera = hierarchy.Append8DC540(5, 0x769060, 144, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = camera.Hierarchy.Write(camera.Address, camera.Hierarchy.Read(camera.Address).WriteByte(41, phase));
        byte[] bytes = NativeBattleMoogleCamera.Reset732AF0().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(232), 1024);
        NativeBattleRinoa88SceneMemory scene = new(NativeBattleEffectArena.Clear()) { Camera = new(bytes.ToImmutableArray(), true), CameraAnchorPaddingKnown = true };
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools }, State = fixture.Frame.State with { Scene = scene } }, camera.Address);
    }
}
