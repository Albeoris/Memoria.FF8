using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325OrbitTests
{
    [Theory]
    [InlineData(60, -393, -10852, -390, 2241, -1625, -4222)]
    [InlineData(86, 1514, -10902, -784, -946, -2101, -5213)]
    [InlineData(89, 1734, -10908, -829, -1314, -2156, -5327)]
    public void Packed_target_then_eye_endpoints_and_biased_age_ratio_publish_exact_native_camera(int counter, short ex, short ey, short ez, short tx, short ty, short tz)
    {
        NativeBattleDiablos325Frame frame = Frame();
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { FocalLength = 123, Roll = 17, EyePaddingF6 = 7, TargetPaddingFE = 9 };
        NativeBattleDiablos325TailStep step = NativeBattleDiablos325Orbit.Advance656309(frame, camera, (ushort)counter);
        Assert.Equal(camera.WriteEffectGlobals(new(new(ex, ey, ez), new(tx, ty, tz), 123, 17)), step.Camera);
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Products_round_separately_before_the_signed_IR_sum_and_preserve_focal_and_roll()
    {
        NativeBattleCameraPose first = new(new(1, 1, 1), new(1, 1, 1), 123, 17);
        NativeBattleCameraPose second = first with { FocalLength = 999, Roll = -3 };
        NativeBattleCameraPose result = NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, 2048);
        Assert.Equal(new NativeBattleCameraPose(new(0, 0, 0), new(0, 0, 0), 123, 17), result);
    }

    [Fact]
    public void First_orbit_callback_raises_only_active_Y_once_before_shrink_and_preserves_inactive_unknown_fields()
    {
        NativeBattleDiablos325Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x4C62, [0x30, 0x75]).Write(0x4C6B, [1]).MarkUnknown(0x4C5C, 4).MarkUnknown(0x4C72, 2);
        frame = frame with { Memory = frame.Memory with { Arena = arena, Diablos325 = frame.Memory.Diablos325! with { Mode51F0 = 3 } } };
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess();
        NativeBattleDiablos325TailStep first = NativeBattleDiablos325Orbit.Advance656309(frame, camera, 60);
        Assert.Equal((short)-27344, BinaryPrimitives.ReadInt16LittleEndian(first.Frame.Memory.Arena!.Read(0x4C62, 2)));
        Assert.Equal((byte)0, first.Frame.Memory.Arena.Read(0x4C6B, 1)[0]);
        Assert.Equal((byte)3, first.Frame.Memory.Diablos325!.Mode51F0);
        Assert.Throws<NotSupportedException>(() => first.Frame.Memory.Arena.Read(0x4C5C, 1));
        Assert.Throws<NotSupportedException>(() => first.Frame.Memory.Arena.Read(0x4C72, 1));
        NativeBattleDiablos325TailStep second = NativeBattleDiablos325Orbit.Advance656309(first.Frame, first.Camera, 61);
        Assert.Equal((short)-27344, BinaryPrimitives.ReadInt16LittleEndian(second.Frame.Memory.Arena!.Read(0x4C62, 2)));
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    [Fact]
    public void Camera_reset_reads_retained_far_Z_and_resets_only_puff_flags_mode_and_cursor()
    {
        NativeBattleDiablos325Frame frame = Frame();
        frame = frame with { Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { Mode51F0 = 3, ParticleCursor51F4 = NativeBattleEffectNodeMemory.ArenaAddress + 0x4C6Cu, FarZ51DC = 1900, CenterZ51D4 = 1234 }, Arena = frame.Memory.Arena!.Write(0x4C5C, Enumerable.Repeat((byte)0xCC, 16).ToArray()) } };
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { FocalLength = 123, Roll = 17, EyePaddingF6 = 7, TargetPaddingFE = 9 };
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325TailStep result = NativeBattleDiablos325Orbit.Reset6563D1(frame, camera);
        Assert.Equal(camera.WriteEffectGlobals(new(new(0, -7931, 320), new(0, -8211, 1686), 123, 17)), result.Camera);
        Assert.Equal((byte)0, result.Frame.Memory.Diablos325!.Mode51F0);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x4C5Cu, result.Frame.Memory.Diablos325.ParticleCursor51F4);
        Assert.Equal(Enumerable.Repeat((byte)0xCC, 15), result.Frame.Memory.Arena!.Read(0x4C5C, 15));
        Assert.Equal((byte)0, result.Frame.Memory.Arena.Read(0x4C6B, 1)[0]);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    private static NativeBattleDiablos325Frame Frame()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = frame with { Random = new(1), Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { CenterZ51D4 = -2100 } } };
        return NativeBattleDiablos325Puffs.Reset6545B0(frame);
    }
}
