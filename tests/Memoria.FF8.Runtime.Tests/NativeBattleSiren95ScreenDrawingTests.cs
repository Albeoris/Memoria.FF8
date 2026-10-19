using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ScreenDrawingTests
{
    [Fact]
    public void Growing_screen_allocates_GT4_before_two_windows_but_inserts_reset_then_polygon_then_window()
    {
        var fixture = Fixture();
        const uint row = 0x2585AE8;
        NativeBattleSiren95Frame frame = fixture.Frame;
        NativeBattleSiren95State state = frame.Memory.Siren95!.Write(0x258EE3E, [0x12, 0x34]);
        frame = frame with { Memory = frame.Memory with { Siren95 = state } };
        NativeBattleGfGeometryBatch? emitted = null;
        var services = Services(fixture.Packets, (current, batch) =>
        {
            emitted = batch;
            return current;
        });
        NativeBattleSiren95Frame result = NativeBattleSiren95ScreenDrawing.Draw745F50(frame, fixture.Address, row, services);
        Assert.NotNull(emitted);
        Assert.Equal(228, emitted.AllocationBytes);
        Assert.Equal(new[] { 52, 0, 64, 128, 76, 140, 204, 152, 216 }, emitted.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(new[] { 12, 52, 12 }, emitted.Attempts.Take(3).Select(attempt => attempt.Data.Length));
        Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(emitted.Attempts[0].Data.AsSpan().Slice(4)));
        Assert.Equal(0x0C000000u, BinaryPrimitives.ReadUInt32LittleEndian(emitted.Attempts[1].Data.AsSpan()));
        Assert.Equal(0x3E404040u, BinaryPrimitives.ReadUInt32LittleEndian(emitted.Attempts[1].Data.AsSpan().Slice(40)));
        Assert.All(emitted.Attempts, attempt => Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value)));
        Assert.Equal(new byte[] { 0x12, 0x34 }, result.Memory.Siren95!.Read(0x258EC76, 2));
        Assert.Equal((ushort)1, result.Memory.Siren95.ReadWord(row + 0xCC));
        Assert.Equal(0x7000u, result.Memory.Siren95.ReadDword(row + 0xC0));
        Assert.Equal(frame.Random, result.Random);
        NativeBattleSiren95Frame restored = JsonSerializer.Deserialize<NativeBattleSiren95Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleSiren95ScreenDrawing.Draw745F50(restored, fixture.Address, row, services)));
    }

    [Fact]
    public void Screen_submits_packets_before_child_append_and_samples_each_corner_with_five_random_calls()
    {
        var fixture = Fixture();
        const uint row = 0x2585AE8;
        NativeBattleSiren95Frame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = fixture.Frame.Memory.Siren95!.Write(row + 0xCC, BitConverter.GetBytes((ushort)3)) } };
        int before = frame.Effect.Pools.Sum(pool => pool.Slots.Count(task => (task.Flags & 1) != 0));
        var services = Services(fixture.Packets, (current, batch) =>
        {
            Assert.Equal(before, current.Effect.Pools.Sum(pool => pool.Slots.Count(task => (task.Flags & 1) != 0)));
            Assert.Equal(912, batch.AllocationBytes);
            return current;
        });
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 20; index++)
        {
            random = random.Draw(out _);
        }
        NativeBattleSiren95Frame result = NativeBattleSiren95ScreenDrawing.Draw745F50(frame, fixture.Address, row, services);
        Assert.Equal(random, result.Random);
        NativeBattleModulePoolTask child = Assert.Single(result.Effect.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x7466A0 && (task.Flags & 1) != 0);
        for (int corner = 0; corner < 4; corner++)
        {
            Assert.Equal((ushort)0, child.ReadWord(0x72 + corner * 8));
        }
    }

    [Fact]
    public void Last_grid_neighbor_reads_live_task_header_and_payload_instead_of_stale_overlapping_module_bytes()
    {
        var fixture = Fixture();
        int selected = Enumerable.Range(0, fixture.Frame.Effect.Pools.Length).Single(index => fixture.Frame.Effect.Pools[index].BankAddress == 0x258EDD8);
        NativeBattleEffectTaskPool pool = fixture.Frame.Effect.Pools[selected];
        NativeBattleModulePoolTask task = pool.Slots[0] with { Flags = 1, State = 0xBEEF, Next = 1, Callback = 0x12345678 };
        task = task.WriteWord(12, 0xABCD);
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        NativeBattleSiren95Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = fixture.Frame.Effect.Pools.SetItem(selected, pool) } };
        byte[] actual = NativeBattleSiren95PhysicalMemory.Read(frame, 0x258EDD8, 14, (_, _) => throw new InvalidOperationException("Stale module aliases must not supply task bytes."));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(actual));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(2)));
        Assert.Equal(0x258EE08u, BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(4)));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(8)));
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(12)));
    }

    [Theory]
    [InlineData(4, 1024)]
    [InlineData(5, 4096)]
    public void Screen_dust_copies_each_corner_without_physics_and_selects_actual_per_corner_sprite(short stage, short scale)
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x7466A0, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        NativeBattleSiren95Frame frame = appended.Frame;
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95!.Write(director + 0x40, BitConverter.GetBytes(stage)) } };
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(appended.Address);
        for (int corner = 0; corner < 4; corner++)
        {
            task = task.WriteWord(0x6C + corner * 8, (ushort)(100 + corner)).WriteWord(0x6E + corner * 8, (ushort)(200 + corner)).WriteWord(0x70 + corner * 8, (ushort)(300 + corner)).WriteWord(0x8C + corner * 8, 1000);
        }
        frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(appended.Address, task).Pools } };
        List<uint> sources = [];
        NativeBattleSiren95Frame Draw(NativeBattleSiren95Frame current, uint pointer, short actualScale)
        {
            NativeBattleModulePoolTask particle = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(pointer);
            Assert.Equal(scale, actualScale);
            Assert.Equal((ushort)(100 + sources.Count), particle.ReadWord(0x1C));
            Assert.Equal((ushort)(200 + sources.Count), particle.ReadWord(0x1E));
            Assert.Equal((ushort)(300 + sources.Count), particle.ReadWord(0x20));
            sources.Add(particle.ReadDword(0x4C));
            return current;
        }
        var result = NativeBattleSiren95Contacts.AdvanceParticle(frame, appended.Address, Draw);
        Assert.Equal(new uint[] { 0x15334A0, 0x1533328, 0x15331D0, 0x15331D0 }, sources);
        NativeBattleModulePoolTask after = new NativeBattleEffectHierarchy(result.Frame.Effect.Pools).Read(appended.Address);
        Assert.Equal((ushort)1000, after.ReadWord(0x8C));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal((ushort)1, after.ReadWord(0x24));
        Assert.Equal((ushort)10, after.ReadWord(0x52));
    }

    private static NativeBattleSiren95ScreenDrawServices Services(NativeBattleActionPacketsState packets, Func<NativeBattleSiren95Frame, NativeBattleGfGeometryBatch, NativeBattleSiren95Frame> submit)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16384, 4096);
        return new(packets, view, new(view, 216, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), submit);
    }

    private static (NativeBattleSiren95Frame Frame, uint Address, NativeBattleActionPacketsState Packets) Fixture()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x257FA90, 0x745AD0, 0xE0, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        return (NativeBattleSiren95Screens.Initialize745BF0(appended.Frame, 0x2585AE8, 0), appended.Address, fixture.Inputs.Packets);
    }
}
