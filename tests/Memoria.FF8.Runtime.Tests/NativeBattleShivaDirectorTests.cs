using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaDirectorTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(41)]
    [InlineData(72)]
    [InlineData(132)]
    [InlineData(157)]
    [InlineData(225)]
    public void Actual_IO_waits_publish_the_view_before_retesting_without_advancing_or_emitting(int counter)
    {
        List<string> calls = [];
        NativeBattleModulePoolTask task = Task(counter);
        NativeBattleShivaDirectorStep step = NativeBattleShivaDirector.Advance5C0F30(task, new(0, () => calls.Add("view"), () =>
        {
            calls.Add("io");
            return -1;
        }, () => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
        Assert.Equal(new[] { "view", "io" }, calls);
        Assert.Equal(task, step.Task);
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(1u, 0)]
    [InlineData(0x201u, 0)]
    [InlineData(0x200u, 1)]
    public void Pause_and_IO_graphics_gates_keep_native_short_circuit_order(uint flags, int ioReads)
    {
        int views = 0;
        int reads = 0;
        NativeBattleModulePoolTask task = Task(0);
        NativeBattleShivaDirectorStep step = NativeBattleShivaDirector.Advance5C0F30(task, new(flags, () => views++, () =>
        {
            reads++;
            return -1;
        }, () => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
        Assert.Equal(1, views);
        Assert.Equal(ioReads, reads);
        Assert.Equal(task, step.Task);
    }

    [Fact]
    public void Entry_services_keep_camera_operands_and_order_before_initial_stage_fade()
    {
        List<NativeBattleShivaDirectorOperation> calls = [];
        NativeBattleShivaDirectorStep step = Advance(Task(0), calls);
        Assert.Equal(new[] { NativeBattleShivaDirectorService.Camera, NativeBattleShivaDirectorService.BeginWave, NativeBattleShivaDirectorService.StartVibration, NativeBattleShivaDirectorService.AppendChild, NativeBattleShivaDirectorService.Stage }, calls.Select(call => call.Service));
        Assert.Equal(new(NativeBattleShivaDirectorService.Camera, 0xD32604, 48, 0x22BD018, 0x22BC198), calls[0]);
        Assert.Equal(new(NativeBattleShivaDirectorService.BeginWave, 0xE9AD20, 0x22BD0E4), calls[1]);
        Assert.Equal(new(NativeBattleShivaDirectorService.StartVibration, 0xD32344, 1, 128), calls[2]);
        Assert.Equal(0x5C4A90, calls[3].First);
        Assert.Equal((ushort)1, step.Task.ReadWord(0x0C));
    }

    [Theory]
    [InlineData(62, 0x221)]
    [InlineData(77, 0x222)]
    public void Audio_ready_gates_only_the_load_and_never_creates_a_wait(int counter, int file)
    {
        List<NativeBattleShivaDirectorOperation> calls = [];
        NativeBattleShivaDirectorStep skipped = Advance(Task(counter), calls, false);
        Assert.Empty(calls);
        Assert.Equal((ushort)(counter + 1), skipped.Task.ReadWord(0x0C));
        NativeBattleShivaDirectorStep loaded = Advance(Task(counter), calls, true);
        NativeBattleShivaDirectorOperation request = Assert.Single(calls);
        Assert.Equal(new(NativeBattleShivaDirectorService.File, file, UseWorkBuffer: true), request);
        Assert.Equal(JsonSerializer.Serialize(skipped.Task), JsonSerializer.Serialize(loaded.Task));
    }

    [Fact]
    public void Upload_and_following_IO_keep_buffer_identity_and_native_order()
    {
        List<NativeBattleShivaDirectorOperation> calls = [];
        Advance(Task(15), calls);
        Assert.Equal(NativeBattleShivaDirectorService.Upload, calls[0].Service);
        Assert.Equal(0x10000, calls[0].First);
        Assert.Equal(new(NativeBattleShivaDirectorService.File, 0x220), calls[1]);
        calls.Clear();
        Advance(Task(157), calls);
        Assert.Equal(new(NativeBattleShivaDirectorService.Upload, 0x10000), calls[0]);
        Assert.Equal(new(NativeBattleShivaDirectorService.File, 0x224, UseWorkBuffer: true), calls[1]);
        calls.Clear();
        Advance(Task(41), calls);
        Assert.Equal(new[] { 0x5C4AD0, 0x5C2DE0 }, calls.Select(call => call.First));
    }

    [Fact]
    public void Contact_and_final_fade_survive_independent_JSON_and_do_not_repeat_next_callback()
    {
        NativeBattleModulePoolTask task = Task(265);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
        List<NativeBattleShivaDirectorOperation> first = [];
        List<NativeBattleShivaDirectorOperation> second = [];
        NativeBattleShivaDirectorStep a = Advance(task, first);
        NativeBattleShivaDirectorStep b = Advance(restored, second);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal(first, second);
        Assert.Equal(new(NativeBattleShivaDirectorService.Stage, 1280), first[0]);
        Assert.Equal(NativeBattleShivaDirectorService.Contacts, first[1].Service);
        first.Clear();
        Advance(a.Task, first);
        Assert.DoesNotContain(first, item => item.Service == NativeBattleShivaDirectorService.Contacts);
        first.Clear();
        NativeBattleShivaDirectorStep completed = Advance(Task(275), first);
        Assert.Equal(2, completed.Result);
        Assert.Equal((ushort)276, completed.Task.ReadWord(0x0C));
        Assert.Equal(new[] { new NativeBattleShivaDirectorOperation(NativeBattleShivaDirectorService.Stage), new NativeBattleShivaDirectorOperation(NativeBattleShivaDirectorService.Stage) }, first);
    }

    [Fact]
    public void Signed_counter_and_increment_preserve_original_sixteen_bit_wrapping()
    {
        List<NativeBattleShivaDirectorOperation> calls = [];
        NativeBattleShivaDirectorStep step = Advance(Task(0x7FFF), calls);
        Assert.Equal((ushort)0x8000, step.Task.ReadWord(0x0C));
        Assert.Equal(0, step.Result);
        Assert.Equal((275 - 32767) << 7, Assert.Single(calls).First);
        calls.Clear();
        step = Advance(Task(0xFFFF), calls);
        Assert.Equal((ushort)0, step.Task.ReadWord(0x0C));
        Assert.Equal(-128, Assert.Single(calls).First);
        Assert.Equal(0, step.Result);
    }

    private static NativeBattleShivaDirectorStep Advance(NativeBattleModulePoolTask task, List<NativeBattleShivaDirectorOperation> calls, bool ready = true) => NativeBattleShivaDirector.Advance5C0F30(task, new(0, () => { }, () => 0, () => ready, calls.Add));

    private static NativeBattleModulePoolTask Task(int counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        (pool, int slot) = pool.Append(0x5C0F30);
        return pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)counter));
    }
}
