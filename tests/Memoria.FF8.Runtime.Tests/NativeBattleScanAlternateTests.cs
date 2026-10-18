using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanAlternateTests
{
    [Fact]
    public void Alternate_timeline_uses_forty_six_then_nine_callbacks_and_real_ordered_children()
    {
        var fixture = Fixture(0x850B40);
        NativeBattleScanAlternateFrame frame = Alternate(fixture.Scan);
        frame = Tick(frame, fixture);
        Assert.Equal((ushort)0x7825, frame.Models[4].Flags0);
        Assert.Equal((ushort)1, frame.Highlight269AA90);
        Assert.Equal((ushort)2, frame.Camera.Scan.Effect.Pools[2].Slots[1].ReadWord(0x68));
        for (int index = 0; index < 45; index++)
        {
            frame = Tick(frame, fixture);
        }
        Assert.Equal((byte)1, Read(frame.Camera.Scan, fixture.Address).ReadByte(0x29));
        frame = Tick(frame, fixture);
        Assert.Equal((ushort)8, Read(frame.Camera.Scan, fixture.Address).ReadWord(0x66));
        for (int index = 0; index < 8; index++)
        {
            frame = Tick(frame, fixture);
        }
        Assert.Equal((byte)2, Read(frame.Camera.Scan, fixture.Address).ReadByte(0x29));
        frame = Tick(frame, fixture);
        Assert.Equal((byte)3, Read(frame.Camera.Scan, fixture.Address).ReadByte(0x29));
        Assert.Equal(new uint[] { 0x850B40, 0x84E810, 0x84D5B0, 0x850FD0 }, Live(frame.Camera.Scan, 2).Select(task => task.Callback));
        Assert.Equal(new uint[] { 0x850C70, 0x850EB0 }, Live(frame.Camera.Scan, 4).Select(task => task.Callback));
        Assert.Equal((byte)5, Read(frame.Camera.Scan, fixture.Address).ReadByte(0x28));
    }

    [Theory]
    [InlineData(1, -385, -63, -10, 64, -9)]
    [InlineData(2, -384, -63, -10, 0, -10)]
    [InlineData(1, -500, -64, -1, 64, 0)]
    public void Alternate_profile_controls_rotation_and_signed_height_before_completion(byte permission, short profileY, short radius, short height, ushort yaw, short expectedHeight)
    {
        var fixture = Fixture(0x850B40);
        NativeBattleScanFrame scan = Write(fixture.Scan, fixture.Address, task => task.WriteByte(0x29, 3));
        NativeBattleScanProcess process = scan.Memory.Scan!;
        scan = scan with { Memory = scan.Memory with { Scan = process with { Presentation = process.Presentation with { Camera269A168 = process.Presentation.Camera269A168.WriteWord(0x40, unchecked((ushort)height)).WriteWord(0x44, unchecked((ushort)radius)).WriteWord(0x46, 333) } } } };
        NativeBattleScanAlternateInputs inputs = new(fixture.Packets, actor =>
        {
            Assert.Equal(4, actor);
            return new(54, Zero(16).WriteByte(0x0E, permission).WriteWord(4, unchecked((ushort)profileY)));
        });
        NativeBattleScanAlternateStep result = NativeBattleScanAlternate.Advance850B40(Alternate(scan) with { Complete269D93A = 1, Highlight269AA90 = 1 }, fixture.Address, inputs);
        NativeBattleScanByteOwner camera = result.Frame.Camera.Scan.Memory.Scan!.Presentation.Camera269A168;
        Assert.Equal(yaw, camera.ReadWord(0x34));
        Assert.Equal(expectedHeight, unchecked((short)camera.ReadWord(0x40)));
        Assert.Equal((ushort)54, camera.ReadWord(0x4C));
        Assert.Equal((ushort)333, result.Frame.Camera.Zoom);
        Assert.Equal((ushort)0, result.Frame.Highlight269AA90);
        Assert.Equal((ushort)1, result.Frame.Exit269CE2C);
        Assert.Equal((byte)4, Read(result.Frame.Camera.Scan, fixture.Address).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0, 0, 0, 0x850A30u)]
    [InlineData(0, 1, -8192, 0x84E810u)]
    [InlineData(0, 1, -8191, 0u)]
    public void Exit_chooses_saved_restore_or_angle_transition_and_preserves_other_model_flags(byte group, ushort finalGroup, short angle, uint expected)
    {
        var fixture = Fixture(0x850B40);
        NativeBattleScanFrame scan = Write(fixture.Scan, fixture.Address, task => task.WriteByte(0x29, 4).WriteByte(0x2A, group));
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        uint owner = Read(scan, fixture.Address).ReadDword(0x10);
        hierarchy = hierarchy.Write(owner, hierarchy.Read(owner).WriteWord(0x58, finalGroup));
        NativeBattleScanProcess process = scan.Memory.Scan!;
        scan = scan with { Effect = scan.Effect with { Pools = hierarchy.Pools }, Memory = scan.Memory with { Scan = process with { Presentation = process.Presentation with { Camera269A168 = process.Presentation.Camera269A168.WriteWord(0x3A, unchecked((ushort)angle)) } } } };
        NativeBattleScanAlternateFrame frame = Alternate(scan);
        frame = frame with { Models = frame.Models.SetItem(4, frame.Models[4] with { Flags0 = 0xFFFF }) };
        frame = Tick(frame, fixture);
        Assert.Equal((ushort)0x87FF, frame.Models[4].Flags0);
        NativeBattleModulePoolTask[] children = Live(frame.Camera.Scan, 2).Where(task => task.Callback != 0x850B40).ToArray();
        Assert.Equal(expected == 0 ? 0 : 1, children.Length);
        if (expected != 0)
        {
            Assert.Equal(expected, children[0].Callback);
            if (expected == 0x84E810)
            {
                Assert.Equal((ushort)3, children[0].ReadWord(0x68));
            }
        }
        Assert.Equal((byte)5, Read(frame.Camera.Scan, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void Camera_restore_copies_actual_saved_padding_knownness_and_finishes_on_next_callback_after_JSON()
    {
        var fixture = Fixture(0x850A30);
        NativeBattleScanByteOwner eye = Zero(8).WriteDword(0, 0x11223344).WriteDword(4, 0x55667788);
        eye = eye with { Known = eye.Known.SetItem(7, false) };
        NativeBattleScanSavedCamera saved = new(eye, Zero(8).WriteDword(4, 0xAABBCCDD), 777);
        NativeBattleScanCameraStep first = NativeBattleScanAlternate.Advance850A30(Alternate(fixture.Scan).Camera, fixture.Address, saved);
        Assert.Equal(0, first.Result);
        Assert.Equal(eye.Bytes, first.Frame.Eye.Bytes);
        Assert.Equal(eye.Known, first.Frame.Eye.Known);
        Assert.Equal(saved.Target8.Bytes, first.Frame.Target.Bytes);
        Assert.Equal(saved.Target8.Known, first.Frame.Target.Known);
        Assert.Equal((ushort)777, first.Frame.Zoom);
        NativeBattleScanCameraFrame restored = JsonSerializer.Deserialize<NativeBattleScanCameraFrame>(JsonSerializer.Serialize(first.Frame))!;
        NativeBattleScanCameraStep next = NativeBattleScanAlternate.Advance850A30(restored, fixture.Address, saved);
        Assert.Equal(2, next.Result);
        Assert.Equal((ushort)0, Read(next.Frame.Scan, fixture.Address).ReadWord(0x66));
        Assert.Equal((byte)2, Read(next.Frame.Scan, fixture.Address).ReadByte(0x29));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Alternate_board_and_spinner_use_distinct_draw_services_and_draw_on_terminal_callback(bool spinner)
    {
        var fixture = Fixture(spinner ? 0x850C70u : 0x850EB0u, 4);
        NativeBattleScanPairMorphFrame frame = new(fixture.Scan, 1, ImmutableDictionary<uint, NativeBattleScanByteOwner>.Empty);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanAlternateDecorationInputs inputs = new(fixture.Packets, (current, draw) =>
        {
            Assert.False(spinner);
            draws.Add(draw);
            return current;
        }, (current, draw) =>
        {
            Assert.True(spinner);
            draws.Add(draw);
            return current;
        });
        frame = NativeBattleScanAlternateDecorations.Advance(frame, fixture.Address, 0, inputs).Frame;
        Assert.Equal(spinner ? 0x15CF674u : 0x15CF614u, draws[0].ResourceAddress);
        Assert.Equal(spinner ? -512 : -136, draws[0].Matrix.X);
        Assert.Equal(spinner ? 0x6C0 : 0x240, draws[0].Matrix.Z);
        if (spinner)
        {
            for (int index = 0; index < 7; index++)
            {
                frame = NativeBattleScanAlternateDecorations.Advance(frame, fixture.Address, 0, inputs).Frame;
                Assert.Equal((byte)1, Read(frame.Scan, fixture.Address).ReadByte(0x29));
            }
            frame = NativeBattleScanAlternateDecorations.Advance(frame, fixture.Address, 0, inputs).Frame;
            Assert.Equal((ushort)0, Read(frame.Scan, fixture.Address).ReadWord(0x46));
        }
        else
        {
            frame = NativeBattleScanAlternateDecorations.Advance(frame, fixture.Address, 0, inputs).Frame;
            NativeBattleModulePoolTask[] children = Live(frame.Scan, 4).Where(task => task.Callback == 0x84F520).ToArray();
            Assert.Equal(7, children.Length);
            Assert.Equal(Enumerable.Range(1, 7).Select(index => (ushort)index), children.Select(task => task.ReadWord(0x62)));
            Assert.All(children, task => Assert.Equal((ushort)1, task.ReadWord(0x60)));
        }
        Assert.Equal((byte)2, Read(frame.Scan, fixture.Address).ReadByte(0x29));
        int before = draws.Count;
        NativeBattleScanPairMorphStep final = NativeBattleScanAlternateDecorations.Advance(frame, fixture.Address, 1, inputs);
        Assert.Equal(before + 1, draws.Count);
        Assert.Equal(spinner ? 2 : 0, final.Result);
        Assert.Equal((byte)3, Read(final.Frame.Scan, fixture.Address).ReadByte(0x29));
    }

    private static NativeBattleScanAlternateFrame Alternate(NativeBattleScanFrame scan) => new(new(scan, Zero(8), Zero(8), 1), Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0x25)).ToImmutableArray(), 0, 0, 0);
    private static NativeBattleScanAlternateFrame Tick(NativeBattleScanAlternateFrame frame, (NativeBattleScanFrame Scan, uint Address, NativeBattleActionPacketsState Packets) fixture) => NativeBattleScanAlternate.Advance850B40(frame, fixture.Address, new(fixture.Packets, _ => new(0, Zero(16)))).Frame;
    private static NativeBattleScanByteOwner Zero(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
    private static NativeBattleModulePoolTask Read(NativeBattleScanFrame scan, uint address) => new NativeBattleEffectHierarchy(scan.Effect.Pools).Read(address);
    private static IEnumerable<NativeBattleModulePoolTask> Live(NativeBattleScanFrame scan, int pool) => scan.Effect.Pools[pool].Slots.Where(task => (task.Flags & 1) != 0);
    private static NativeBattleScanFrame Write(NativeBattleScanFrame scan, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, update(hierarchy.Read(address)));
        return scan with { Effect = scan.Effect with { Pools = hierarchy.Pools } };
    }

    private static (NativeBattleScanFrame Scan, uint Address, NativeBattleActionPacketsState Packets) Fixture(uint callback, int pool = 2)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, pool == 2 ? (ushort)0x78 : (ushort)0x6C, hierarchy.Address(0, 0), (_, _, _) => new(2, 4));
        return (scan with { Effect = scan.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address, packets);
    }
}
