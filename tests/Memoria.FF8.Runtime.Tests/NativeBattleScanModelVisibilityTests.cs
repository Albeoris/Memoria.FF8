using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanModelVisibilityTests
{
    [Fact]
    public void Fade_backs_up_all_physical_models_and_restores_selected_colors_and_individual_flags_after_json()
    {
        var fixture = Fixture();
        NativeBattleScanVisibilityFrame frame = fixture.Frame;
        NativeBattleGraphicsModel[] originals = frame.Models.ToArray();
        frame = NativeBattleScanModelVisibility.Advance(frame, fixture.Address).Frame;
        Assert.All(frame.Stage.Groups, group => Assert.Equal(0xAB000000u, group.ColorFlags));
        Assert.Equal(originals[1].Flags0, frame.Models[1].Flags0);
        Assert.Equal((ushort)(originals[0].Flags0 | 0x0800), frame.Models[0].Flags0);
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal((uint)originals[index].Flags0, frame.Scan.Memory.Scan!.Presentation.SavedFlags269A1D8.ReadDword(index * 4));
            Assert.Equal(originals[index].Color40, frame.Scan.Memory.Scan.Presentation.SavedColors269CE34.ReadDword(index * 4));
        }
        for (int tick = 0; tick < 15; tick++)
        {
            frame = NativeBattleScanModelVisibility.Advance(frame, fixture.Address).Frame;
        }
        Assert.Equal((byte)1, Task(frame, fixture.Address).ReadByte(0x29));
        frame = NativeBattleScanModelVisibility.Advance(frame, fixture.Address).Frame;
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.All(frame.Stage.Groups, group => Assert.Equal((short)4096, group.ColorDepth));
        Assert.Equal(0xCD000000u, frame.Models[0].Color40);
        Assert.Equal(originals[1].Color40, frame.Models[1].Color40);
        var finished = NativeBattleScanModelVisibility.Advance(frame, fixture.Address);
        Assert.Equal(2, finished.Result);
        frame = finished.Frame;
        Assert.Equal(0x00808080u, frame.Models[1].Color40);
        Assert.Equal((byte)4, frame.Stage.Groups[0].Flags);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleScanVisibilityFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleEffectHierarchyAppend restore = new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Append8DC540(2, 0x8507F0, 0x78, 0x269AAE0, (_, _, _) => new(2, 4));
        uint restoreAddress = restore.Address;
        Assert.NotEqual(fixture.Address, restoreAddress);
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = restore.Hierarchy.Pools } } };
        frame = NativeBattleScanModelVisibility.Advance(frame, restoreAddress).Frame;
        Assert.Equal((byte)6, frame.Stage.Groups[0].Flags);
        Assert.Equal(originals[1].Color40, frame.Models[1].Color40);
        for (int tick = 0; tick < 16; tick++)
        {
            frame = NativeBattleScanModelVisibility.Advance(frame, restoreAddress).Frame;
        }
        frame = NativeBattleScanModelVisibility.Advance(frame, restoreAddress).Frame;
        Assert.All(frame.Stage.Groups, group => Assert.Equal((short)0, group.ColorDepth));
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(originals[index].Flags0, frame.Models[index].Flags0);
            Assert.Equal(index == 1 ? originals[index].Color40 : 0xCD204060u, frame.Models[index].Color40);
        }
        Assert.Equal((byte)0, Task(frame, 0x269AAE0).ReadByte(0x28));
    }

    [Fact]
    public void Selection_DWORD_two_is_selected_for_backup_and_fade_but_not_phase_two_exact_one_branch()
    {
        var fixture = Fixture();
        NativeBattleScanVisibilityFrame frame = fixture.Frame;
        NativeBattleScanProcess process = frame.Scan.Memory.Scan!;
        frame = frame with { Scan = frame.Scan with { Memory = frame.Scan.Memory with { Scan = process with { Presentation = process.Presentation with { Selection269A1BC = process.Presentation.Selection269A1BC.WriteDword(4, 2) } } } } };
        NativeBattleGraphicsModel original = frame.Models[1];
        frame = NativeBattleScanModelVisibility.Advance(frame, fixture.Address).Frame;
        Assert.Equal(original.Flags0, frame.Models[1].Flags0);
        for (int tick = 0; tick < 16; tick++)
        {
            frame = NativeBattleScanModelVisibility.Advance(frame, fixture.Address).Frame;
        }
        Assert.Equal(original.Color40, frame.Models[1].Color40);
        NativeBattleScanVisibilityStep finished = NativeBattleScanModelVisibility.Advance(frame, fixture.Address);
        Assert.Equal(2, finished.Result);
        Assert.Equal(original.Color40, finished.Frame.Models[1].Color40);
        Assert.Equal((ushort)(original.Flags0 | 0x24), finished.Frame.Models[1].Flags0);
        Assert.Equal((byte)0, Task(finished.Frame, 0x269AAE0).ReadByte(0x28));
    }

    [Theory]
    [InlineData(0x7FFF, 0x80FF, 1)]
    [InlineData(0x0FFF, 0x1000, 2)]
    public void Fade_uses_wrapped_signed_word_before_native_clamp(ushort before, ushort after, byte phase)
    {
        var fixture = Fixture();
        NativeBattleScanVisibilityFrame frame = WriteTask(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 1).WriteWord(0x1C, before));
        frame = NativeBattleScanModelVisibility.Advance(frame, fixture.Address).Frame;
        Assert.Equal(after, Task(frame, fixture.Address).ReadWord(0x1C));
        Assert.Equal(phase, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(unchecked((short)after), frame.Stage.Groups[0].ColorDepth);
    }

    private static NativeBattleModulePoolTask Task(NativeBattleScanVisibilityFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);

    private static NativeBattleScanVisibilityFrame WriteTask(NativeBattleScanVisibilityFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, change(hierarchy.Read(address)));
        return frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
    }

    private static (NativeBattleScanVisibilityFrame Frame, uint Address) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(2, 0x84ED90, 0x78, 0x269AAE0, (_, _, _) => new(2, 4));
        scan = scan with { Effect = scan.Effect with { Pools = appended.Hierarchy.Pools }, Memory = scan.Memory with { Scan = scan.Memory.Scan! with { Presentation = scan.Memory.Scan!.Presentation with { Selection269A1BC = scan.Memory.Scan.Presentation.Selection269A1BC.WriteDword(4, 1) } } } };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, (ushort)(3 | (index == 1 ? 0x20 : 0))) { Color40 = 0xCD103050 }).ToImmutableArray();
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.Select(group => group with { Flags = 6, ColorFlags = 0xAB123456 }).ToImmutableArray() };
        return (new(scan, models, stage, 0x11204060), appended.Address);
    }
}
