using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgamesh329VariantTests
{
    [Fact]
    public void Entry_uses_actual_329_binding_and_TIM_global_and_preserves_retained_process_owners()
    {
        var fixture = Fixture();
        NativeBattleGilgameshProcess retained = fixture.Memory.Gilgamesh! with { Variant = int.MaxValue, VibrationHandle = 123, WorldView = Identity() };
        NativeBattleGilgameshFrame frame = new(fixture.Memory with { Gilgamesh = retained }, fixture.Effect, new(123));
        NativeBattleGilgameshFrame entered = NativeBattleGilgameshController.Initialize58DCF0(frame, fixture.Packets, fixture.Models);
        Assert.Equal(329, entered.Effect.Binding.Effect);
        Assert.Equal(0x58DB10u, entered.Effect.Binding.Entry);
        Assert.Equal(0x58D720u, entered.Effect.Binding.Initializer);
        NativeBattleGilgameshProcess current = entered.Memory.Gilgamesh!;
        Assert.Equal(2, current.Variant);
        Assert.Equal(int.MaxValue, retained.Variant);
        Assert.Same(retained.Models, current.Models);
        Assert.Same(retained.SwordTrail, current.SwordTrail);
        Assert.Same(retained.OpeningFlare, current.OpeningFlare);
        Assert.Same(retained.WorldView, current.WorldView);
        Assert.Equal(retained.CameraTask, current.CameraTask);
        Assert.Equal(123, current.VibrationHandle);
        Assert.NotNull(current.Entry);
        Assert.Equal(frame.Random, entered.Random);
        Assert.Equal(1, entered.Memory.Textures!.Count);
        Assert.Equal(0x40000, entered.Memory.Textures.Slots[0]!.ArenaOffset);
        Assert.Equal(0x21FF4A8u, entered.Effect.ReturnedDescriptor);
        Assert.Equal(0x596B70u, entered.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(0x592300u, entered.Effect.Pools[1].Slots[0].Callback);
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshEntry.Initialize58DCF0(fixture.Memory, fixture.Effect with { ResourcePointers = [new(0x2200EC0, 0x40000)] }, fixture.Packets, fixture.Models));
    }

    [Fact]
    public void Initial_director_event_selects_329_wave_before_common_vibration_and_flash()
    {
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        NativeBattleGilgameshDirectorStep step = Advance(0, operations, () => throw new InvalidOperationException());
        Assert.Equal(new NativeBattleGilgameshDirectorOperation[]
        {
            new(NativeBattleGilgameshDirectorService.Camera),
            new(NativeBattleGilgameshDirectorService.BeginWave, 0xE88EF4),
            new(NativeBattleGilgameshDirectorService.StartVibration, 1, 128),
            new(NativeBattleGilgameshDirectorService.AppendChild, 0x592BC0)
        }, operations);
        Assert.Equal((ushort)1, step.Task.ReadWord(12));
    }

    [Theory]
    [InlineData(238)]
    [InlineData(273)]
    public void Selected_final_file_and_attack_events_do_not_introduce_a_pending_gate(int counter)
    {
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        NativeBattleGilgameshDirectorStep step = Advance(counter, operations, () => throw new InvalidOperationException("This native event does not poll pending IO."));
        NativeBattleGilgameshDirectorOperation[] expected = counter == 238
            ? [new(NativeBattleGilgameshDirectorService.File, 756, 0xE000)]
            : [new(NativeBattleGilgameshDirectorService.Sound, 0xCD038C, 0, 128), new(NativeBattleGilgameshDirectorService.Volume, 128, 0, 96), new(NativeBattleGilgameshDirectorService.Upload, 0xE000), new(NativeBattleGilgameshDirectorService.AppendChild, 0x591250)];
        Assert.Equal(expected, operations);
        Assert.Equal((ushort)(counter + 1), step.Task.ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(237)]
    [InlineData(253)]
    [InlineData(268)]
    public void Common_pending_gates_preserve_329_task_and_event_order_across_JSON(int counter)
    {
        NativeBattleModulePoolTask original = DirectorTask(counter);
        List<string> calls = [];
        NativeBattleGilgameshDirectorStep blocked = NativeBattleGilgameshDirector.Advance592300(original, new(0, () => calls.Add("view"), () => { calls.Add("pending"); return -1; }, _ => throw new InvalidOperationException()) { Effect = 329 });
        Assert.Same(original, blocked.Task);
        Assert.Equal(new[] { "view", "pending" }, calls);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(blocked.Task))!;
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        NativeBattleGilgameshDirectorStep resumed = NativeBattleGilgameshDirector.Advance592300(restored, new(0, () => { }, () => 0, operations.Add) { Effect = 329 });
        Assert.Equal((ushort)(counter + 1), resumed.Task.ReadWord(12));
        Assert.Equal(original.Payload.AsSpan().Slice(2).ToArray(), resumed.Task.Payload.AsSpan().Slice(2).ToArray());
        Assert.Single(operations);
        Assert.Equal(counter switch { 237 => NativeBattleGilgameshDirectorService.Upload, 253 => NativeBattleGilgameshDirectorService.File, _ => NativeBattleGilgameshDirectorService.BeginWave }, operations[0].Service);
    }

    [Fact]
    public void Bit200_rechecks_pending_at_the_common_explicit_gate_after_publishing_view()
    {
        List<string> calls = [];
        int pending = 0;
        NativeBattleModulePoolTask task = DirectorTask(253);
        NativeBattleGilgameshDirectorStep blocked = NativeBattleGilgameshDirector.Advance592300(task, new(0x200, () => calls.Add("view"), () => { calls.Add("pending"); return ++pending == 1 ? 0 : -1; }, _ => throw new InvalidOperationException()) { Effect = 329 });
        Assert.Same(task, blocked.Task);
        Assert.Equal(new[] { "view", "pending", "pending" }, calls);
    }

    private static NativeBattleGilgameshDirectorStep Advance(int counter, List<NativeBattleGilgameshDirectorOperation> operations, Func<int> pending) => NativeBattleGilgameshDirector.Advance592300(DirectorTask(counter), new(0, () => { }, pending, operations.Add) { Effect = 329 });
    private static NativeBattleModulePoolTask DirectorTask(int counter) => new NativeBattleModulePoolTask(1, 0xBEEF, null, 0x592300, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()).WriteWord(12, unchecked((ushort)counter));
    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleGraphicsModel> Models) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 2, 329, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(329, 328, new(329, 0x58DB10, 0x58D720), new(0, 0, 1 << 3, 0, 0, 0), null) { ResourcePointers = [new(0x2200F9C, 0x40000)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x40000, Tim()) };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2)
        {
            Rotation14 = 0x100,
            Transform = new(new(0, 0, 0), new(checked((short)(index * 100)), 0, checked((short)(index * 200))), 0, 0x600, 4096),
            GeometryWorldMatrix = Identity()
        }).ToImmutableArray();
        return (memory, effect, packets, models);
    }

    private static byte[] Tim()
    {
        byte[] bytes = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64), 0x1234);
        return bytes;
    }
}
