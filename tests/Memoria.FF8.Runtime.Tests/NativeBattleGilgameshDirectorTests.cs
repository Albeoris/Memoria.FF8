using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshDirectorTests
{
    [Theory]
    [MemberData(nameof(Timeline))]
    public void Timeline_emits_ordered_native_operations_before_increment(int counter, NativeBattleGilgameshDirectorOperation[] expected)
    {
        NativeBattleModulePoolTask task = Task(counter);
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        List<string> calls = [];
        NativeBattleGilgameshDirectorInputs inputs = new(0, () => calls.Add("view"), () => { calls.Add("pending"); return 0; }, operation => { calls.Add("operation"); operations.Add(operation); });
        NativeBattleGilgameshDirectorStep step = NativeBattleGilgameshDirector.Advance592300(task, inputs);
        Assert.Equal(expected, operations);
        Assert.Equal("view", calls[0]);
        Assert.Equal(unchecked((ushort)(counter + 1)), step.Task.ReadWord(12));
        Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(2).ToArray(), NativeBattleStorageSnapshots.Capture(step.Task).Payload.AsSpan().Slice(2).ToArray());
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(0u, -1, 0, true)]
    [InlineData(1u, 0, 0, false)]
    [InlineData(0x201u, 0, 0, false)]
    [InlineData(0x200u, -1, 1, false)]
    [InlineData(0x200u, 0, 1, true)]
    public void Initial_pause_gate_publishes_view_first_and_reads_pending_only_for_bit200_without_bit0(uint flags, int pending, int reads, bool advanced)
    {
        NativeBattleModulePoolTask task = Task(2);
        List<string> calls = [];
        int pendingReads = 0;
        NativeBattleGilgameshDirectorStep step = NativeBattleGilgameshDirector.Advance592300(task, new(flags, () => calls.Add("view"), () => { calls.Add("pending"); pendingReads++; return pending; }, _ => calls.Add("file")));
        Assert.Equal("view", calls[0]);
        Assert.Equal(reads, pendingReads);
        Assert.Equal(advanced ? (ushort)3 : (ushort)2, step.Task.ReadWord(12));
        Assert.Equal(advanced, calls.Contains("file"));
        if (!advanced)
        {
            Assert.Same(task, step.Task);
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(100)]
    [InlineData(136)]
    [InlineData(151)]
    [InlineData(176)]
    [InlineData(191)]
    [InlineData(218)]
    [InlineData(237)]
    [InlineData(253)]
    [InlineData(268)]
    public void Explicit_IO_gate_blocks_before_event_and_requires_second_read_after_initial_200_gate(int counter)
    {
        NativeBattleModulePoolTask task = Task(counter);
        int pendingReads = 0;
        int operations = 0;
        NativeBattleGilgameshDirectorStep blocked = NativeBattleGilgameshDirector.Advance592300(task, new(0, () => { }, () => { pendingReads++; return -1; }, _ => operations++));
        Assert.Same(task, blocked.Task);
        Assert.Equal(1, pendingReads);
        Assert.Equal(0, operations);
        pendingReads = 0;
        NativeBattleGilgameshDirectorStep secondGate = NativeBattleGilgameshDirector.Advance592300(task, new(0x200, () => { }, () => ++pendingReads == 1 ? 0 : -1, _ => operations++));
        Assert.Same(task, secondGate.Task);
        Assert.Equal(2, pendingReads);
        Assert.Equal(0, operations);
    }

    [Theory]
    [InlineData(238)]
    [InlineData(273)]
    public void Late_file_and_final_upload_have_no_explicit_pending_gate(int counter)
    {
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        NativeBattleGilgameshDirectorStep step = NativeBattleGilgameshDirector.Advance592300(Task(counter), new(0, () => { }, () => throw new InvalidOperationException("No native pending call at this event."), operations.Add));
        Assert.Equal((ushort)(counter + 1), step.Task.ReadWord(12));
        Assert.Contains(operations, operation => operation.Service == (counter == 238 ? NativeBattleGilgameshDirectorService.File : NativeBattleGilgameshDirectorService.Upload));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(326, 327, 0)]
    [InlineData(327, 328, 2)]
    [InlineData(32767, -32768, 0)]
    public void Signed_counter_wrap_and_strict_terminal_threshold_retain_task_suffix(int counter, short expectedCounter, int expectedResult)
    {
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        NativeBattleGilgameshDirectorStep step = NativeBattleGilgameshDirector.Advance592300(Task(counter), new(0, () => { }, () => throw new InvalidOperationException(), operations.Add));
        Assert.Equal(unchecked((ushort)expectedCounter), step.Task.ReadWord(12));
        Assert.Equal(expectedResult, step.Result);
        Assert.Equal(expectedResult == 2 ? new[] { Op(NativeBattleGilgameshDirectorService.Stage) } : [], operations);
        Assert.All(NativeBattleStorageSnapshots.Capture(step.Task).Payload.Skip(2), value => Assert.Equal((byte)0xA5, value));
    }

    [Fact]
    public void Pending_boundary_JSON_resumes_with_same_event_order_and_next_task()
    {
        NativeBattleModulePoolTask task = Task(136);
        NativeBattleGilgameshDirectorStep blocked = NativeBattleGilgameshDirector.Advance592300(task, new(0, () => { }, () => -1, _ => throw new InvalidOperationException()));
        NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(blocked.Task))!;
        List<NativeBattleGilgameshDirectorOperation> first = [];
        List<NativeBattleGilgameshDirectorOperation> second = [];
        NativeBattleGilgameshDirectorStep expected = NativeBattleGilgameshDirector.Advance592300(task, new(0, () => { }, () => 0, first.Add));
        NativeBattleGilgameshDirectorStep actual = NativeBattleGilgameshDirector.Advance592300(restored, new(0, () => { }, () => 0, second.Add));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(first, second);
        Assert.Equal(new[] { Op(NativeBattleGilgameshDirectorService.Upload, 0x8000), Op(NativeBattleGilgameshDirectorService.File, 749, 0x22000) }, first);
    }

    public static IEnumerable<object[]> Timeline()
    {
        NativeBattleGilgameshDirectorService camera = NativeBattleGilgameshDirectorService.Camera;
        NativeBattleGilgameshDirectorService wave = NativeBattleGilgameshDirectorService.BeginWave;
        NativeBattleGilgameshDirectorService child = NativeBattleGilgameshDirectorService.AppendChild;
        NativeBattleGilgameshDirectorService particle = NativeBattleGilgameshDirectorService.AppendParticle;
        NativeBattleGilgameshDirectorService init = NativeBattleGilgameshDirectorService.Initializer;
        NativeBattleGilgameshDirectorService file = NativeBattleGilgameshDirectorService.File;
        NativeBattleGilgameshDirectorService upload = NativeBattleGilgameshDirectorService.Upload;
        NativeBattleGilgameshDirectorService fade = NativeBattleGilgameshDirectorService.FadeTask;
        Dictionary<int, NativeBattleGilgameshDirectorOperation[]> timeline = new()
        {
            [0] = [Op(camera), Op(wave, 0xE84BE0), Op(NativeBattleGilgameshDirectorService.StartVibration, 1, 128), Op(child, 0x592BC0)],
            [1] = [Sound(0xCD0378)],
            [2] = [Op(file, 745, 0xE000)],
            [8] = [Op(init, 0x592D00), Op(init, 0x5904F0)],
            [16] = [Op(upload, 0xE000), Op(file, 746)],
            [18] = [Op(init, 0x5931E0)],
            [24] = [Op(file, 747, 0x8000)],
            [44] = [Op(init, 0x593550)],
            [76] = [Op(init, 0x5904D0), Op(init, 0x5947F0), Op(particle, 0x5966B0), Op(child, 0x595EE0), Op(init, 0x592AB0)],
            [79] = [Volume()],
            [100] = [Op(upload, 0x8000)],
            [101] = [Op(child, 0x5939C0)],
            [113] = [Op(child, 0x593770)],
            [121] = [Sound(0xCD037C), Op(init, 0x594160)],
            [122] = [Op(file, 748, 0x8000), Op(child, 0x593E80)],
            [123] = [Op(child, 0x593C70)],
            [136] = [Op(upload, 0x8000), Op(file, 749, 0x22000)],
            [151] = [new(file, 750, UseWorkBuffer: true)],
            [176] = [new(wave, UseWorkBuffer: true)],
            [181] = [Sound(0xCD0380), Volume(), Op(file, 751), Op(particle, 0x596800), Op(child, 0x596710), Op(fade, 1, 92)],
            [191] = [new(file, 752, UseWorkBuffer: true)],
            [218] = [new(wave, UseWorkBuffer: true)],
            [223] = [Op(file, 755, 0xE000), Op(particle, 0x596A60), Op(child, 0x596970)],
            [233] = [Op(NativeBattleGilgameshDirectorService.Title)],
            [237] = [Op(upload, 0xE000)],
            [238] = [Op(file, 757, 0xE000)],
            [243] = [Volume()],
            [253] = [new(file, 753, UseWorkBuffer: true)],
            [263] = [Op(init, 0x5943A0)],
            [268] = [new(wave, UseWorkBuffer: true)],
            [273] = [Sound(0xCD0390), Volume(), Op(upload, 0xE000), Op(child, 0x5920E0)],
            [279] = [Op(NativeBattleGilgameshDirectorService.QueueColor, 2, 2, 12, 255), Op(child, 0x596B30)],
            [280] = [Op(fade, 1, 43)],
            [311] = [Op(init, 0x592B30)],
            [324] = [Op(NativeBattleGilgameshDirectorService.StopVibrationIfActive)]
        };
        foreach ((int counter, NativeBattleGilgameshDirectorOperation[] operations) in timeline)
        {
            yield return [counter, operations];
        }
        foreach (int counter in new[] { 3, 7, 17, 23, 25, 75, 80, 99, 120, 135, 152, 180, 190, 217, 222, 232, 236, 239, 252, 267, 272, 278, 281, 310, 312, 323, 325 })
        {
            yield return [counter, Array.Empty<NativeBattleGilgameshDirectorOperation>()];
        }
    }

    private static NativeBattleGilgameshDirectorOperation Op(NativeBattleGilgameshDirectorService service, int first = 0, int second = 0, int third = 0, int fourth = 0) => new(service, first, second, third, fourth);

    private static NativeBattleGilgameshDirectorOperation Sound(int pointer) => Op(NativeBattleGilgameshDirectorService.Sound, pointer, 0, 128);

    private static NativeBattleGilgameshDirectorOperation Volume() => Op(NativeBattleGilgameshDirectorService.Volume, 128, 0, 96);

    private static NativeBattleModulePoolTask Task(int counter) => new NativeBattleModulePoolTask(1, 0xBEEF, null, 0x592300, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()).WriteWord(12, unchecked((ushort)counter));
}
