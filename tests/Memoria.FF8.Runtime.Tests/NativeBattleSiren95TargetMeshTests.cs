using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95TargetMeshTests
{
    [Fact]
    public void Emitter_preserves_target_vertical_WORD_in_raw_position_copy_and_tests_byte_clock_before_stage_completion()
    {
        var fixture = Fixture(0x7456A0, 0x2585EF0, 0x64);
        var services = new NativeBattleSiren95TargetMeshServices(fixture.Packets, _ => Target(), (_, _) => throw new InvalidOperationException());
        var initial = NativeBattleSiren95TargetMeshes.Advance(fixture.Frame, fixture.Address, services);
        Assert.Equal((ushort)0xABCD, Read(initial.Frame, fixture.Address).ReadWord(0x22));
        var waiting = Put(initial.Frame, fixture.Address, Read(initial.Frame, fixture.Address).WriteWord(0x24, 15));
        var step = NativeBattleSiren95TargetMeshes.Advance(waiting, fixture.Address, services);
        Assert.Equal((byte)1, Read(step.Frame, fixture.Address).ReadByte(0x29));
        step = NativeBattleSiren95TargetMeshes.Advance(step.Frame, fixture.Address, services);
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x29));
        var emit = Put(step.Frame, fixture.Address, Read(step.Frame, fixture.Address).WriteWord(0x24, 17));
        uint director = emit.Memory.Siren95!.DirectorAddress1533010!.Value;
        emit = emit with { Memory = emit.Memory with { Siren95 = emit.Memory.Siren95!.Write(director + 0x40, BitConverter.GetBytes((short)7)) } };
        NativeCrtRandomState random = emit.Random.Draw(out _).Draw(out _).Draw(out _);
        step = NativeBattleSiren95TargetMeshes.Advance(emit, fixture.Address, services);
        Assert.Equal(random, step.Frame.Random);
        Assert.Single(step.Frame.Effect.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x745840);
        Assert.Equal((byte)3, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.False(step.Complete);
        var restored = JsonSerializer.Deserialize<NativeBattleSiren95Frame>(JsonSerializer.Serialize(emit))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(NativeBattleSiren95TargetMeshes.Advance(restored, fixture.Address, services)));
    }

    [Fact]
    public void Mesh_child_uses_two_random_operands_and_grows_before_both_initial_and_final_draws()
    {
        var fixture = Fixture(0x745840, 0x2585EF0, 0x64);
        uint control = 0x258FB88;
        var frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = fixture.Frame.Memory.Siren95!.WriteDword(0x258EAA0, control).WriteDword(control + 0x148, 0x19D1018) } };
        NativeCrtRandomState random = frame.Random.Draw(out ushort angle).Draw(out ushort speed);
        int draws = 0;
        var services = new NativeBattleSiren95TargetMeshServices(fixture.Packets, _ => throw new InvalidOperationException(), (current, pointer) =>
        {
            var task = Read(current, pointer);
            Assert.Equal(draws == 0 ? 512u : 768u, task.ReadDword(0x30));
            Assert.Equal(task.ReadDword(0x30), task.ReadDword(0x34));
            Assert.Equal(task.ReadDword(0x30), task.ReadDword(0x38));
            Assert.Equal((ushort)4096, task.ReadWord(0x50));
            Assert.Equal(0x19D1018u, task.ReadDword(0x4C));
            draws++;
            return current;
        });
        var step = NativeBattleSiren95TargetMeshes.Advance(frame, fixture.Address, services);
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal((ushort)(((angle & 4095) + (speed & 15) + 16) & 4095), Read(step.Frame, fixture.Address).ReadWord(0x48));
        frame = Put(step.Frame, fixture.Address, Read(step.Frame, fixture.Address).WriteByte(0x29, 3).WriteWord(0x50, 3840));
        step = NativeBattleSiren95TargetMeshes.Advance(frame, fixture.Address, services);
        Assert.True(step.Complete);
        Assert.Equal(2, draws);
    }

    [Theory]
    [InlineData(0xA2, 0xA0)]
    [InlineData(0xA4, 0xA6)]
    public void Final_stage_companion_restores_saved_flag_bit_then_fades_all_four_canonical_rows_to_zero(byte flags, byte restoredFlags)
    {
        var fixture = Fixture(0x746A40, 0x257FA90, 0xE0);
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { Groups = NativeBattleStageState.FromColdProcess().Groups.Select(group => group with { Flags = flags, ColorFlags = 0xAA123456 }).ToImmutableArray() };
        var step = NativeBattleSiren95Actors.Advance746920(fixture.Frame, fixture.Address, stage);
        Assert.All(step.Stage.Groups, group =>
        {
            Assert.Equal(restoredFlags, group.Flags);
            Assert.Equal(0xAA000000u, group.ColorFlags);
            Assert.Equal((short)2048, group.ColorDepth);
        });
        for (int index = 0; index < 4; index++)
        {
            step = NativeBattleSiren95Actors.Advance746920(step.Frame, fixture.Address, step.Stage);
        }
        Assert.True(step.Complete);
        Assert.All(step.Stage.Groups, group => Assert.Equal((short)0, group.ColorDepth));
    }

    [Fact]
    public void Shared_mesh_parser_reports_its_actual_consumed_cursor_even_when_all_groups_are_empty()
    {
        byte[] mesh = new byte[40];
        BitConverter.GetBytes(8).CopyTo(mesh, 0);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        int consumed = -1;
        NativeBattleGfGeometryBatch result = NativeBattleModulePackets.EmitBatch572200(mesh.ToImmutableArray(), 0, default, matrix, new(matrix, 216, 160, 108), new(0xF0, 0, 0, null, null, null, 17, 2), observeSourceCursor: cursor => consumed = cursor);
        Assert.Equal(40, consumed);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, result.AllocationBytes);
    }

    private static NativeBattleGraphicsModel Target() => new(3, 0) { GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)), Transform = new(new(0, 0, 0), new(100, 200, 300), unchecked((short)0xABCD), 0, 4096) };
    private static (NativeBattleSiren95Frame Frame, uint Address, NativeBattleActionPacketsState Packets) Fixture(uint callback, uint descriptor, int size)
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, descriptor, callback, size, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        return (appended.Frame, appended.Address, fixture.Inputs.Packets);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
