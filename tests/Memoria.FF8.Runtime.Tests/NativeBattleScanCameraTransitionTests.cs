using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanCameraTransitionTests
{
    [Theory]
    [InlineData(0, 256, 750, 375, 4, 100, 300)]
    [InlineData(2, 256, 750, 375, 4, 100, 300)]
    [InlineData(1, 682, 1000, 0, 1, 90, 30)]
    [InlineData(3, 512, 750, 375, 4, 100, 300)]
    public void Initial_modes_use_actual_rates_selected_yaw_and_correct_physical_bone(ushort mode, ushort rate, ushort radius, ushort height, int sampledActor, ushort targetX, ushort targetZ)
    {
        var fixture = Fixture(mode);
        List<int> sampled = [];
        NativeBattleScanCameraInputs inputs = Inputs() with { ReadBone502170 = (actor, selector, fraction) =>
        {
            sampled.Add(actor);
            Assert.Equal((byte)0xF1, selector);
            Assert.Equal(0, fraction);
            return actor == 1 ? new(10, 20, 30) : new(100, 200, 300);
        } };
        NativeBattleScanCameraStep step = NativeBattleScanCameraTransition.Advance84E810(fixture.Frame, fixture.Address, inputs);
        NativeBattleScanByteOwner camera = Camera(step.Frame);
        Assert.Equal(new[] { sampledActor }, sampled);
        Assert.Equal(rate, camera.ReadWord(0x32));
        Assert.Equal(radius, camera.ReadWord(0x3E));
        Assert.Equal(height, camera.ReadWord(0x44));
        Assert.Equal(targetX, camera.ReadWord(0x18));
        Assert.Equal((ushort)10, camera.ReadWord(0x1A));
        Assert.Equal(targetZ, camera.ReadWord(0x1C));
        Assert.Equal((ushort)0xABCD, camera.ReadWord(0x1E));
        Assert.Equal((ushort)0x3333, camera.ReadWord(0x16));
        Assert.Equal((ushort)3072, camera.ReadWord(0x36));
        Assert.Equal(unchecked((ushort)-316), camera.ReadWord(0x3C));
        Assert.Equal((ushort)450, camera.ReadWord(0x42));
        Assert.Equal((ushort)87, camera.ReadWord(0x4C));
        Assert.Equal((byte)1, Task(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Profile_fallback_copies_actual_model_W_and_invalid_mode_preserves_unconsumed_endpoint_fields()
    {
        NativeBattleScanCameraInputs inputs = Inputs();
        inputs = inputs with { Profile = inputs.Profile.WriteWord(8, 0) };
        NativeBattleScanByteOwner camera = NativeBattleScanCameraTransition.Initialize84E870(Zero(80).WriteWord(0x1E, 0xABCD), 4, 1, 0x1D972C0 + 156, Eye(), Target(), 400, inputs);
        Assert.Equal((ushort)1080, camera.ReadWord(0x18));
        Assert.Equal((ushort)10, camera.ReadWord(0x1A));
        Assert.Equal((ushort)3000, camera.ReadWord(0x1C));
        Assert.Equal((ushort)0xCAFE, camera.ReadWord(0x1E));
        NativeBattleScanByteOwner prior = Zero(80).WriteWord(0x32, 123).WriteWord(0x3E, 456).WriteWord(0x44, 789).WriteDword(0x18, 0x12345678);
        inputs = inputs with { Profile = Unknown(16), ReadBone502170 = (_, _, _) => throw new InvalidOperationException() };
        NativeBattleScanByteOwner unsupportedMode = NativeBattleScanCameraTransition.Initialize84E870(prior, 4, 0xFFFF, 0x1D972C0 + 156, Eye(), Target(), 400, inputs);
        Assert.Equal((ushort)123, unsupportedMode.ReadWord(0x32));
        Assert.Equal((ushort)456, unsupportedMode.ReadWord(0x3E));
        Assert.Equal((ushort)789, unsupportedMode.ReadWord(0x44));
        Assert.Equal(0x12345678u, unsupportedMode.ReadDword(0x18));
    }

    [Fact]
    public void Interpolation_uses_signed_word_endpoints_truncation_and_wrapped_progress_without_shortest_arc()
    {
        NativeBattleScanByteOwner camera = Zero(80).WriteWord(0x32, 2048).WriteWord(0x36, 32767).WriteWord(0x38, 0x8000).WriteWord(0x3C, 1).WriteWord(0x3E, 0);
        NativeBattleScanCameraInterpolation step = NativeBattleScanCameraTransition.Advance84EB40(camera);
        Assert.False(step.Complete);
        Assert.Equal((ushort)0, step.Camera.ReadWord(0x34));
        Assert.Equal((ushort)1, step.Camera.ReadWord(0x3A));
        camera = camera.WriteWord(0x30, 0x7FFF).WriteWord(0x32, 2).WriteWord(0x36, 0).WriteWord(0x38, 4096);
        step = NativeBattleScanCameraTransition.Advance84EB40(camera);
        Assert.False(step.Complete);
        Assert.Equal((ushort)0x8001, step.Camera.ReadWord(0x30));
        Assert.Equal((ushort)0x8001, step.Camera.ReadWord(0x34));
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(1, 7)]
    [InlineData(2, 16)]
    [InlineData(3, 8)]
    public void Terminal_callback_publishes_actual_camera_and_padding_before_parent_completion(ushort mode, int callbacks)
    {
        var fixture = Fixture(mode);
        NativeBattleScanCameraInputs inputs = Inputs();
        NativeBattleScanCameraFrame frame = NativeBattleScanCameraTransition.Advance84E810(fixture.Frame, fixture.Address, inputs).Frame;
        frame = JsonSerializer.Deserialize<NativeBattleScanCameraFrame>(JsonSerializer.Serialize(frame))!;
        for (int callback = 1; callback <= callbacks; callback++)
        {
            NativeBattleScanCameraStep step = NativeBattleScanCameraTransition.Advance84E810(frame, fixture.Address, inputs);
            frame = step.Frame;
            Assert.Equal(callback == callbacks ? 2 : 0, step.Result);
            Assert.Equal((byte)(callback == callbacks ? 0 : 1), Task(frame, 0x269AAE0).ReadByte(0x28));
        }
        NativeBattleScanByteOwner camera = Camera(frame);
        Assert.Equal((ushort)4096, camera.ReadWord(0x30));
        Assert.Equal((ushort)576, frame.Zoom);
        Assert.Equal((ushort)0x1234, frame.Eye.ReadWord(6));
        Assert.Equal((ushort)0x5678, frame.Target.ReadWord(6));
        Assert.Equal(camera.ReadDword(0), frame.Eye.ReadDword(0));
        Assert.Equal(camera.ReadDword(4), frame.Eye.ReadDword(4));
        Assert.Equal(camera.ReadDword(8), frame.Target.ReadDword(0));
        Assert.Equal(camera.ReadDword(12), frame.Target.ReadDword(4));
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)1, Task(frame, fixture.Address).Flags);
    }

    [Fact]
    public void Missing_actual_model_profile_or_output_padding_does_not_create_default_camera_values()
    {
        NativeBattleScanCameraInputs inputs = Inputs();
        Assert.Throws<InvalidDataException>(() => NativeBattleScanCameraTransition.Initialize84E870(Zero(80), 4, 0, 0, Eye(), Target(), 1, inputs));
        Assert.Throws<NotSupportedException>(() => NativeBattleScanCameraTransition.Initialize84E870(Zero(80), 4, 0, 0x1D972C0, Eye(), Target(), 1, inputs with { Profile = Unknown(16) }));
        var fixture = Fixture(0);
        NativeBattleScanByteOwner unknownPadding = new(Camera(fixture.Frame).Bytes, Camera(fixture.Frame).Known.SetItem(6, false));
        Assert.Throws<NotSupportedException>(() => NativeBattleScanCameraTransition.Publish84EC80(fixture.Frame, unknownPadding));
    }

    private static (NativeBattleScanCameraFrame Frame, uint Address) Fixture(ushort mode)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(scan.Effect.Pools).Append8DC540(2, 0x84E810, 0x78, 0x269AAE0, (_, _, _) => new(2, 4));
        NativeBattleEffectHierarchy hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteWord(0x68, mode));
        NativeBattleScanProcess process = scan.Memory.Scan!;
        process = process with { Presentation = process.Presentation with { PrimaryModel269CE30 = 0x1D972C0 + 156, Camera269A168 = Zero(80).WriteWord(6, 0x1234).WriteWord(14, 0x5678).WriteWord(0x1E, 0xABCD) } };
        scan = scan with { Effect = scan.Effect with { Pools = hierarchy.Pools }, Memory = scan.Memory with { Scan = process } };
        return (new(scan, Eye(), Target(), 400), child.Address);
    }

    private static NativeBattleScanCameraInputs Inputs()
    {
        ImmutableArray<NativeBattleScanByteOwner> models = Enumerable.Range(0, 7).Select(_ => Zero(156)).ToImmutableArray();
        models = models.SetItem(1, models[1].WriteWord(0x0E, 0).WriteWord(0x1C, 1000).WriteWord(0x1E, 2000).WriteWord(0x20, 3000).WriteWord(0x22, 0xCAFE));
        NativeBattleScanByteOwner profile = Zero(16).WriteWord(0, 1000).WriteWord(2, 80).WriteWord(4, 10).WriteWord(8, 1);
        return new(models, profile, 87, 4096, 0, (actor, _, _) => actor == 1 ? new(10, 20, 30) : new(100, 200, 300));
    }

    private static NativeBattleScanByteOwner Eye() => Zero(8).WriteWord(0, 100).WriteWord(2, 500).WriteWord(4, 300);
    private static NativeBattleScanByteOwner Target() => Zero(8).WriteWord(2, 50).WriteWord(6, 0x3333);
    private static NativeBattleScanByteOwner Camera(NativeBattleScanCameraFrame frame) => frame.Scan.Memory.Scan!.Presentation.Camera269A168;
    private static NativeBattleModulePoolTask Task(NativeBattleScanCameraFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);
    private static NativeBattleScanByteOwner Zero(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
    private static NativeBattleScanByteOwner Unknown(int length) => new(new byte[length].ToImmutableArray(), new bool[length].ToImmutableArray());
}
