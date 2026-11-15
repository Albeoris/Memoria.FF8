using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanViewerTests
{
    [Fact]
    public void Viewer_initialization_publishes_real_task_scale_pointer_before_bone_query_and_opens_actual_windows_without_speed_reset()
    {
        var fixture = Fixture();
        int samples = 0;
        NativeBattleScanViewerInputs inputs = fixture.Inputs with
        {
            ReadBone502170 = (frame, actor, selector, fraction) =>
            {
                samples++;
                Assert.Equal(OriginalOwner(4), actor);
                Assert.Equal((byte)0xF1, selector);
                Assert.Equal(0, fraction);
                Assert.Equal(fixture.Address + 0x30, ReadModel(frame, 4).ReadDword(0x30));
                Assert.Equal(4096u, Task(frame, fixture.Address).ReadDword(0x30));
                Assert.False(frame.Scan.Memory.Scan!.Presentation.Camera269A168.Known[0x26]);
                return new(10, 20, 30);
            }
        };
        NativeBattleScanViewerFrame initial = fixture.Frame with { Text = fixture.Frame.Text with { Windows = fixture.Frame.Text.Windows.SetValue49FBC0(0, 123).SetValue49FBC0(2, 456) } };
        NativeBattleScanViewerStep result = NativeBattleScanViewer.Advance84F860(initial, fixture.Address, inputs);
        Assert.Equal(1, samples);
        Assert.Equal((byte)1, Task(result.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)2, Task(result.Frame, fixture.Address).ReadByte(0x28));
        Assert.Equal(0x84FAF0u, result.Frame.Scan.Effect.Pools[2].Slots[1].Callback);
        Assert.Equal(0x84F9E0u, result.Frame.Scan.Effect.Pools[4].Slots[0].Callback);
        Assert.Equal((ushort)123, result.Frame.Text.Windows.Row(0).ReadWord(0x10));
        Assert.Equal((ushort)456, result.Frame.Text.Windows.Row(2).ReadWord(0x10));
        Assert.Equal(0x269AA78u, result.Frame.Text.Windows.Row(2).ReadDword(8));
        Assert.Equal(0x18875B4u, result.Frame.Text.Windows.Row(0).ReadDword(8));
        Assert.Equal(new NativeBattleScanRect(170, 30, 136, 28).ToBytes(), result.Frame.Text.Windows.Row(2).Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal((ushort)90, Task(result.Frame, fixture.Address).ReadWord(0x54));
        Assert.Equal((ushort)270, Task(result.Frame, fixture.Address).ReadWord(0x58));
        Assert.False(result.Frame.Scan.Memory.Scan!.Presentation.Name269AA78.Known[2]);
    }

    [Fact]
    public void Initialization_uses_selected_physical_model_and_profile_fallback_without_inventing_padding()
    {
        var fixture = Fixture();
        NativeBattleScanProcess process = fixture.Frame.Scan.Memory.Scan!;
        NativeBattleScanViewerFrame frame = fixture.Frame with { Scan = fixture.Frame.Scan with { Effect = fixture.Frame.Scan.Effect with { ScanSelectedModel = OriginalOwner(3) }, Memory = fixture.Frame.Scan.Memory with { Scan = process with { Presentation = process.Presentation with { PrimaryModel269CE30 = NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(3)) } } } } };
        frame = NativeBattleScanViewer.Initialize84FCD0(frame, fixture.Address, fixture.Inputs with
        {
            ReadProfile = actor =>
            {
                Assert.Equal(OriginalOwner(4), actor);
                return Profile().WithWord(8, 0);
            },
            ReadBone502170 = (_, actor, _, _) =>
            {
                Assert.Equal(OriginalOwner(3), actor);
                return new(999, 999, 999);
            }
        });
        Assert.Equal(fixture.Address + 0x30, ReadModel(frame, 3).ReadDword(0x30));
        Assert.Equal(0u, ReadModel(frame, 4).ReadDword(0x30));
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x54));
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x58));
        NativeBattleScanByteOwner camera = frame.Scan.Memory.Scan!.Presentation.Camera269A168;
        Assert.False(camera.Known[0x26]);
        Assert.Equal((ushort)404, camera.ReadWord(0x2E));
    }

    [Theory]
    [InlineData(1, 5000u, 1)]
    [InlineData(4, 5000u, 1)]
    [InlineData(5, 4096u, 0)]
    public void Reset_input_requires_both_actual_low_flags(ushort held, uint expectedScale, ushort expectedManual)
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        frame = Modify(frame, fixture.Address, task => task.WriteDword(0x30, 5000).WriteWord(0x70, 1).WriteWord(0x72, 50));
        frame = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, fixture.Inputs with { Input = Input(held) });
        Assert.Equal(expectedScale, Task(frame, fixture.Address).ReadDword(0x30));
        Assert.Equal(expectedManual, Task(frame, fixture.Address).ReadWord(0x70));
    }

    [Fact]
    public void Manual_yaw_wraps_word_before_masking_to_twelve_bits()
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        frame = Modify(frame, fixture.Address, task => task.WriteWord(0x70, 1).WriteWord(0x72, 50));
        frame = WriteModel(frame, 4, ReadModel(frame, 4).WriteWord(0x0E, 0xFFF0));
        frame = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, fixture.Inputs with { Input = Input(0x8000), ReadProfile = _ => Profile().WithByte(0x0D, 1) });
        Assert.Equal((ushort)0x70, ReadModel(frame, 4).ReadWord(0x0E));
    }

    [Fact]
    public void Reset_copies_angle_padding_without_consuming_it_and_does_not_reset_idle_timer_or_position()
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        frame = Modify(frame, fixture.Address, task => task.WriteWord(0x70, 1).WriteWord(0x72, 54).WriteWord(0x40, 1).WriteWord(0x42, 2).WriteWord(0x44, 3).WriteDword(0x30, 7000));
        frame = WriteModel(frame, 4, ReadModel(frame, 4).WriteWord(0x0E, 999).WriteWord(0x1C, 999));
        NativeBattleScanViewerFrame reset = NativeBattleScanViewer.Reset850600(frame, fixture.Address);
        Assert.Equal((ushort)0, ReadModel(reset, 4).ReadWord(0x0E));
        Assert.False(ReadModel(reset, 4).Known[0x12]);
        Assert.Equal((ushort)999, ReadModel(reset, 4).ReadWord(0x1C));
        Assert.Equal((ushort)54, Task(reset, fixture.Address).ReadWord(0x72));
        Assert.Equal((ushort)0, Task(reset, fixture.Address).ReadWord(0x70));
        Assert.Equal(4096u, Task(reset, fixture.Address).ReadDword(0x30));
        Assert.Equal(4096u, Task(reset, fixture.Address).ReadDword(0x34));
        Assert.Equal((ushort)0, Task(reset, fixture.Address).ReadWord(0x44));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 128)]
    [InlineData(2, 0)]
    public void Positive_direction_starts_manual_timer_even_when_permission_is_not_exactly_one(byte permission, ushort expectedYaw)
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        frame = Modify(frame, fixture.Address, task => task.WriteWord(0x70, 1).WriteWord(0x72, 50));
        NativeBattleScanViewerProfile profile = Profile().WithByte(0x0D, permission);
        NativeBattleScanViewerFrame result = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, fixture.Inputs with { Input = Input(0x8000), ReadProfile = _ => profile });
        Assert.Equal(expectedYaw, ReadModel(result, 4).ReadWord(0x0E));
        Assert.Equal((ushort)1, Task(result, fixture.Address).ReadWord(0x70));
        Assert.Equal((ushort)75, Task(result, fixture.Address).ReadWord(0x72));
    }

    [Fact]
    public void Manual_idle_timeout_resets_then_auto_yaw_resumes_on_following_callback()
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        frame = Modify(frame, fixture.Address, task => task.WriteWord(0x70, 1).WriteWord(0x72, 1));
        NativeBattleScanViewerInputs inputs = fixture.Inputs with { ReadProfile = _ => Profile().WithByte(0x0D, 1) };
        frame = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, inputs);
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x70));
        Assert.Equal((ushort)0, ReadModel(frame, 4).ReadWord(0x0E));
        frame = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, inputs);
        Assert.Equal((ushort)64, ReadModel(frame, 4).ReadWord(0x0E));
        Assert.Equal((ushort)75, Task(frame, fixture.Address).ReadWord(0x72));
    }

    [Theory]
    [InlineData(0x4004, 8190u, 8192u)]
    [InlineData(0x1004, 2050u, 2048u)]
    [InlineData(0x4004, 4096u, 4224u)]
    [InlineData(0x1004, 4096u, 3968u)]
    public void Actual_scale_axes_apply_directional_signed_clamps_and_publish_all_three_task_factors(ushort held, uint old, uint expected)
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        frame = Modify(frame, fixture.Address, task => task.WriteDword(0x30, old));
        frame = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, fixture.Inputs with { Input = Input(held) });
        Assert.Equal(expected, Task(frame, fixture.Address).ReadDword(0x30));
        Assert.Equal(expected, Task(frame, fixture.Address).ReadDword(0x34));
        Assert.Equal(expected, Task(frame, fixture.Address).ReadDword(0x38));
    }

    [Fact]
    public void Bone_correction_reads_just_published_translation_and_retained_rotation_then_copies_only_selected_position_and_matrix()
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        ImmutableArray<byte> retainedRotation = ReadModel(frame, 4).Bytes.Slice(0x40, 18);
        NativeBattleScanByteOwner other = ReadModel(frame, 5).WriteWord(0x0E, 777).WriteDword(0x30, 0x1234);
        frame = WriteModel(frame, 5, other);
        NativeBattleScanViewerProfile profile = Profile().WithWord(8, 0).WithWord(2, 5).WithWord(0x0A, 7);
        int samples = 0;
        NativeBattleScanViewerInputs inputs = fixture.Inputs with
        {
            Input = Input(0x8001),
            ReadProfile = _ => profile,
            ReadBone502170 = (current, actor, _, _) =>
            {
                samples++;
                Assert.Equal(OriginalOwner(4), actor);
                Assert.Equal(100u, ReadModel(current, 4).ReadDword(0x54));
                Assert.Equal(200u, ReadModel(current, 4).ReadDword(0x58));
                Assert.Equal(300u, ReadModel(current, 4).ReadDword(0x5C));
                Assert.Equal(retainedRotation.ToArray(), ReadModel(current, 4).Bytes.Slice(0x40, 18).ToArray());
                Assert.False(ReadModel(current, 4).Known[0x52]);
                Assert.Equal(4096u, Task(current, fixture.Address).ReadDword(0x30));
                return new(10, 20, 30);
            }
        };
        NativeBattleScanViewerFrame result = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, inputs);
        Assert.Equal(1, samples);
        Assert.Equal((ushort)41, ReadModel(result, 4).ReadWord(0x1C));
        Assert.Equal((ushort)213, ReadModel(result, 4).ReadWord(0x1E));
        Assert.Equal((ushort)330, ReadModel(result, 4).ReadWord(0x20));
        Assert.Equal(retainedRotation.ToArray(), ReadModel(result, 5).Bytes.Slice(0x40, 18).ToArray());
        Assert.False(ReadModel(result, 5).Known[0x52]);
        Assert.Equal((ushort)405, ReadModel(result, 5).ReadWord(0x22));
        Assert.Equal((ushort)777, ReadModel(result, 5).ReadWord(0x0E));
        Assert.Equal(0x1234u, ReadModel(result, 5).ReadDword(0x30));
        Assert.Same(frame.Models.Get(OriginalOwner(6)), result.Models.Get(OriginalOwner(6)));
    }

    [Fact]
    public void Acknowledgement_requires_real_bit_forty_then_waits_eighteen_callbacks_before_restoration_and_window_close()
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        NativeBattleGraphicsModel unchanged = frame.Models.Get(OriginalOwner(6));
        frame = WriteModel(frame, 5, ReadModel(frame, 5).WriteWord(0x12, 0xABCD));
        frame = Modify(frame, fixture.Address, task => task.WriteByte(0x29, 2));
        frame = NativeBattleScanViewer.Advance84F860(frame, fixture.Address, fixture.Inputs).Frame;
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        frame = NativeBattleScanViewer.Advance84F860(frame, fixture.Address, fixture.Inputs with { Input = Input(0x40) }).Frame;
        Assert.Equal((ushort)18, Task(frame, fixture.Address).ReadWord(0x66));
        Assert.Equal((ushort)1, frame.Scan.Memory.Scan!.Presentation.Result269D938);
        for (int callback = 0; callback < 18; callback++)
        {
            frame = NativeBattleScanViewer.Advance84F860(frame, fixture.Address, fixture.Inputs).Frame;
        }
        Assert.Equal((byte)4, Task(frame, fixture.Address).ReadByte(0x29));
        NativeBattleScanViewerStep closed = NativeBattleScanViewer.Advance84F860(frame, fixture.Address, fixture.Inputs);
        Assert.Equal(2, closed.Result);
        Assert.Equal(0u, ReadModel(closed.Frame, 4).ReadDword(0x30));
        Assert.Equal(0u, ReadModel(closed.Frame, 5).ReadDword(0x30));
        Assert.False(ReadModel(closed.Frame, 4).Known[0x12]);
        Assert.False(ReadModel(closed.Frame, 5).Known[0x12]);
        Assert.Same(unchanged, closed.Frame.Models.Get(OriginalOwner(6)));
        Assert.Equal(0u, closed.Frame.Text.Windows.Row(2).ReadDword(8));
        Assert.Equal((ushort)0xF000, closed.Frame.Text.Windows.Row(0).ReadWord(0x1E));
    }

    [Fact]
    public void Byte_copy_propagates_unknown_padding_and_independent_interactive_continuation_matches()
    {
        NativeBattleScanByteOwner source = Unknown(156).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300);
        NativeBattleScanByteOwner destination = Known(156).WriteWord(0x22, 777);
        NativeBattleScanByteOwner copied = NativeBattleScanViewer.Copy(destination, 0x1C, source, 0x1C, 8);
        Assert.Equal((ushort)300, copied.ReadWord(0x20));
        Assert.False(copied.Known[0x22]);
        Assert.False(copied.Known[0x23]);

        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        NativeBattleScanViewerFrame restored = NativeBattleTestSnapshots.Independent(frame);
        NativeBattleScanViewerFrame left = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, fixture.Inputs with { Input = Input(0x8001) });
        NativeBattleScanViewerFrame right = NativeBattleScanViewer.Advance84FD90(restored, fixture.Address, fixture.Inputs with { Input = Input(0x8001) });
        Assert.Equal(NativeBattleTestSnapshots.Json(left), NativeBattleTestSnapshots.Json(right));
        Assert.False(ReadModel(left, 4).Known[0x12]);
        Assert.False(ReadModel(left, 4).Known[0x52]);
    }

    [Theory]
    [InlineData(0x8000, 0x0E, 128)]
    [InlineData(0x2000, 0x0E, 3968)]
    [InlineData(0x1000, 0x0C, 3968)]
    [InlineData(0x4000, 0x0C, 128)]
    public void Unrestricted_rotation_overrides_each_native_direction_permission_and_restores_identically(ushort held, int angle, ushort expected)
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        frame = Modify(frame, fixture.Address, task => task.WriteWord(0x70, 1).WriteWord(0x72, 50));
        NativeBattleScanViewerInputs inputs = fixture.Inputs with { Input = Input(held), ReadProfile = _ => Profile().WithByte(0x0C, 2).WithByte(0x0D, 0), UnrestrictedRotation = true };
        NativeBattleScanViewerFrame restored = NativeBattleTestSnapshots.Independent(frame);
        NativeBattleScanViewerFrame result = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, inputs);
        NativeBattleScanViewerFrame resumed = NativeBattleScanViewer.Advance84FD90(restored, fixture.Address, inputs);
        Assert.Equal(expected, ReadModel(result, 4).ReadWord(angle));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(resumed));
        NativeBattleScanViewerFrame restricted = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, inputs with { UnrestrictedRotation = false });
        Assert.Equal((ushort)0, ReadModel(restricted, 4).ReadWord(angle));
    }

    [Fact]
    public void Unrestricted_rotation_enables_automatic_yaw_without_mutating_profile_permissions()
    {
        var fixture = Fixture();
        NativeBattleScanViewerFrame frame = NativeBattleScanViewer.Initialize84FCD0(fixture.Frame, fixture.Address, fixture.Inputs);
        NativeBattleScanViewerFrame result = NativeBattleScanViewer.Advance84FD90(frame, fixture.Address, fixture.Inputs with { UnrestrictedRotation = true });
        Assert.Equal((ushort)64, ReadModel(result, 4).ReadWord(0x0E));
        Assert.Equal((byte)0, fixture.Inputs.ReadProfile(OriginalOwner(4)).Bytes16.ReadByte(0x0D));
    }

    private static NativeBattleScanByteOwner ReadModel(NativeBattleScanViewerFrame frame, int slot) => NativeBattleScanPhysicalModels.Capture(frame.Models.Get(OriginalOwner(slot)));

    private static NativeBattleScanViewerFrame WriteModel(NativeBattleScanViewerFrame frame, int slot, NativeBattleScanByteOwner bytes)
    {
        BattleModelOwner owner = OriginalOwner(slot);
        return frame with { Models = frame.Models.Set(owner, NativeBattleScanPhysicalModels.Apply(frame.Models.Get(owner), bytes)) };
    }

    private static NativeBattleScanViewerProfile Profile() => new(12, Known(16).WriteWord(8, 1));
    private static NativeBattleScanInputBank Input(ushort held) => new([Known(196).WriteByte(0xC2, 0).WriteByte(0x18, 0).WriteByte(0x1C, 1).WriteWord(0x1E, held), Known(196)]);
    private static NativeBattleScanByteOwner Known(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
    private static NativeBattleScanByteOwner Unknown(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(false, length).ToImmutableArray());
    private static NativeBattleModulePoolTask Task(NativeBattleScanViewerFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);
    private static NativeBattleScanViewerFrame Modify(NativeBattleScanViewerFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, change(hierarchy.Read(address)));
        return frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
    }

    private static (NativeBattleScanViewerFrame Frame, uint Address, NativeBattleScanViewerInputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, _) = packets.Prepare(new(OriginalOwner(2), 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(4), 1, 0, 0, 0, 0, 0), null));
        packets = packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1);
        (packets, _) = packets.Publish(1);
        NativeBattleScanProcess process = NativeBattleScanProcess.FromColdProcess();
        NativeBattleScanByteOwner camera = Unknown(80).WriteWord(0x20, 0).WriteWord(0x22, 0).WriteWord(0x24, 0).WriteWord(0x28, 100).WriteWord(0x2A, 200).WriteWord(0x2C, 300);
        process = process with { Presentation = process.Presentation with { Camera269A168 = camera, PrimaryModel269CE30 = NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(4)), Selection269A1BC = process.Presentation.Selection269A1BC.WriteDword(16, 1).WriteDword(20, 1).WriteDword(24, 2) } };
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(OriginalOwner(2), 1, BattleModelTargets.From([OriginalOwner(4)]), new BattleActionHeaderReference(0), 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess() with { Scan = process }, effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(2, 0x84F860, 0x78, hierarchy.Address(0, 0), (_, _, _) => OriginalTaskActors(2, 4));
        scan = scan with
        {
            Effect = scan.Effect with
            {
                Pools = child.Hierarchy.Pools,
                ScanSelectedModel = OriginalOwner(4),
                ScanSelectionValues = scan.Effect.ScanSelectionValues.Add(OriginalOwner(4), 1).Add(OriginalOwner(5), 1).Add(OriginalOwner(6), 2)
            }
        };
        BattleActorModels models = OriginalModels(Enumerable.Range(0, 7).Select(index =>
        {
            NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(lane => checked((short)(100 + index * 10 + lane))).ToImmutableArray(), 100, 200, 300, 4096);
            NativeBattleGraphicsModel model = new(0)
            {
                Rotation12 = 0,
                Rotation14 = 0,
                Rotation16 = 0,
                Transform = new(new(0, 0, 0), new(100, 200, 300), checked((short)(401 + index)), 0, 4096),
                ScaleVectorAddress48 = 0,
                GeometryWorldMatrix = matrix
            };
            return (index, model);
        })).Models;
        NativeBattleScanTextWindows windows = new(Enumerable.Range(0, 8).Select(_ => Known(60)).ToImmutableArray(), Enumerable.Range(0, 8).ToImmutableDictionary(index => (byte)index, _ => Known(64)));
        NativeBattleScanTextInitializationResult initialized = NativeBattleScanTextInitialization.InitializeTextStores4A0880(NativeBattleScanTextInitialization.InitializeBackendStores4B7170(windows), Known(12));
        NativeBattleScanTextFrame text = new(initialized.Windows, Known(128), 0, -1, initialized.Globals1D2B524);
        NativeBattleScanViewerFrame frame = new(scan, models, Unknown(8).WriteWord(0, 20).WriteWord(2, 40).WriteWord(4, 60), text);
        NativeBattleScanViewerInputs inputs = new(packets, Input(0), 0, Enumerable.Range(1, 12).Select(value => (byte)value).ToArray(), _ => Unknown(24).Write(0, new byte[] { 0x45, 0 }), _ => 0x18875B4, _ => Profile(), (_, _, _, _) => new(10, 20, 30));
        return (frame, child.Address, inputs);
    }
}

internal static class ScanViewerProfileTestInputs
{
    public static NativeBattleScanViewerProfile WithByte(this NativeBattleScanViewerProfile profile, int offset, byte value) => profile with { Bytes16 = profile.Bytes16.WriteByte(offset, value) };
    public static NativeBattleScanViewerProfile WithWord(this NativeBattleScanViewerProfile profile, int offset, ushort value) => profile with { Bytes16 = profile.Bytes16.WriteWord(offset, value) };
}
